shader_type spatial;
// Sunlit 90s Philippine Provincial Barangay circular ground shader (Overhaul v2):
// - 3 wider main N-S pathways (West X = -34, Center X = 0, East X = +34) + 2 E-W alleys (Z = -26, +26)
// - Shallow walkable mid canal bed across Z = 0 (NO mid bridge at X = 0!) with sloped Tabing-Ilog stone wading ramps
// - Warm sunlit provincial concrete slabs, golden-ochre packed earth, damp green lumot (moss) patches,
//   and soft warm-espresso slab seams (diffuse ALBEDO strictly <= 0.65 for zero white overexposure).
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform float arena_radius = 84.0;
uniform vec2 arena_half = vec2(87.0, 87.0);
uniform vec2 own_base_center = vec2(0.0, 64.0);
uniform vec2 enemy_base_center = vec2(0.0, -64.0);
uniform float wear : hint_range(0.0, 1.0) = 1.0;

varying vec3 v_world;

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	v += 0.55 * value_noise(p);
	v += 0.30 * value_noise(p * 2.07 + 11.3);
	v += 0.15 * value_noise(p * 4.19 + 27.1);
	return v;
}

float grid_line(vec2 uv, float thickness) {
	vec2 fw = max(fwidth(uv), vec2(0.002));
	vec2 g = abs(fract(uv - 0.5) - 0.5) / fw;
	return 1.0 - clamp(min(g.x, g.y) / thickness, 0.0, 1.0);
}

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 xz = v_world.xz;
	vec2 axz = abs(xz);
	float r = length(xz);
	float theta = atan(xz.y, xz.x);
	float eff_radius = max(arena_radius, 24.0);

	float grain = fbm(xz * 0.92);
	float fine_grain = value_noise(xz * 3.8);
	float macro_var = fbm(xz * 0.085);
	float brush_var = fbm(xz * 0.28 + vec2(grain * 0.65, -macro_var * 0.55));
	float moss_noise = fbm(xz * 0.36 + vec2(17.4, -23.1));

	// Inside circular barangay compound (r <= arena_radius)
	float inside_arena = 1.0 - smoothstep(eff_radius - 1.2, eff_radius + 1.6, r);

	// 3 Wider Main N-S Pathways (~12m wide: West X = -34 Tabing-Ilog/Karinderya, Center X = 0, East X = +34 TODA/Barberya)
	float wobble_ns = sin(xz.y * 0.05) * 0.65;
	float lane_center = (1.0 - smoothstep(5.6, 6.6, abs(xz.x + wobble_ns))) * (1.0 - smoothstep(66.0, 73.0, axz.y));
	float lane_west = (1.0 - smoothstep(5.2, 6.2, abs(xz.x + 34.0 - wobble_ns * 0.7))) * (1.0 - smoothstep(56.0, 64.0, axz.y));
	float lane_east = (1.0 - smoothstep(5.2, 6.2, abs(xz.x - 34.0 + wobble_ns * 0.7))) * (1.0 - smoothstep(56.0, 64.0, axz.y));
	float main_ns_lanes = max(lane_center, max(lane_west, lane_east));

	// 2 Connecting E-W Alleyways at Z = -26 and Z = +26 (~9.6m wide) + outer base connector ring (r ~ 62m)
	float wobble_ew = cos(xz.x * 0.06) * 0.55;
	float alley_ew = (1.0 - smoothstep(4.2, 5.1, abs(axz.y - 26.0 + wobble_ew))) * (1.0 - smoothstep(42.0, 50.0, axz.x));
	float ring_outer = 1.0 - smoothstep(3.6, 4.5, abs(r - 62.0));
	float canal_promenade = (1.0 - smoothstep(2.4, 3.2, abs(axz.y - 7.8))) * (1.0 - smoothstep(eff_radius - 7.0, eff_radius - 2.5, axz.x));

	float road_lane = clamp(max(max(main_ns_lanes, alley_ew), ring_outer * 0.92), 0.0, 1.0) * inside_arena;
	float concrete_pad = clamp(max(alley_ew, canal_promenade) + smoothstep(0.34, 0.68, macro_var) * 0.80, 0.0, 1.0) * inside_arena;

	// Continuous Shallow Walkable Mid Canal Bed across Z = 0 (NO mid bridge at X = 0!)
	float in_canal_bed = (1.0 - smoothstep(4.0, 4.9, axz.y)) * inside_arena;
	float canal_bank_zone = smoothstep(3.6, 4.6, axz.y) * (1.0 - smoothstep(6.8, 8.8, axz.y)) * inside_arena;

	// Soft warm-espresso pen/mortar tone (replaces harsh #141016 black comic ink)
	vec3 warm_espresso = vec3(0.20, 0.16, 0.14);

	// 1. Warm sunlit golden-ochre & sandy provincial earth (between slabs & outside 84m perimeter)
	vec3 earth_sunlit = vec3(0.52, 0.42, 0.31);
	vec3 earth_warm = vec3(0.41, 0.33, 0.24);
	vec3 earth_col = mix(earth_warm, earth_sunlit, clamp(brush_var * 0.72 + fine_grain * 0.28, 0.0, 1.0));
	// Sunlit tropical grass tufts on unpaved earth pockets
	float grass_tuft = smoothstep(0.56, 0.80, fbm(xz * 0.40 + 19.4)) * (1.0 - road_lane);
	earth_col = mix(earth_col, vec3(0.30, 0.42, 0.21), grass_tuft * 0.58);

	// Outer sunlit provincial countryside beyond the 84m perimeter (r >= 85m):
	// lively tropical damuhan grass + warm sandy footpaths (pilapil / landas), zero square borders.
	float outer_zone = smoothstep(eff_radius + 1.5, eff_radius + 9.0, r);
	if (outer_zone > 0.001) {
		float meadow_macro = fbm(xz * 0.055 + vec2(31.2, -17.8));
		float meadow_detail = fbm(xz * 0.24 - vec2(9.4, 22.1));
		float grass_patch = smoothstep(0.32, 0.65, meadow_macro * 0.66 + meadow_detail * 0.34);
		vec3 lush_grass = mix(vec3(0.24, 0.37, 0.18), vec3(0.32, 0.45, 0.22), meadow_detail);
		vec3 sun_meadow = vec3(0.47, 0.44, 0.27);
		vec3 outer_grass = mix(lush_grass, sun_meadow, smoothstep(0.62, 0.86, grain) * 0.46);

		float warp_ang = theta + sin(r * 0.048 + macro_var * 2.6) * 0.28 + (brush_var - 0.5) * 0.16;
		float radial_spoke = abs(sin(warp_ang * 3.0 + 0.45)) * r;
		float radial_path = 1.0 - smoothstep(1.6, 3.1, radial_spoke);
		float loop_radius = eff_radius + 14.5 + sin(theta * 3.0 + 1.1) * 4.2 + (macro_var - 0.5) * 6.5;
		float loop_path = 1.0 - smoothstep(1.3, 2.5, abs(r - loop_radius));
		float footpath = clamp(max(radial_path, loop_path), 0.0, 1.0) * (1.0 - smoothstep(126.0, 142.0, r));

		vec3 path_sand = mix(vec3(0.44, 0.36, 0.26), vec3(0.54, 0.45, 0.33), fine_grain * 0.65 + grain * 0.35);
		vec3 countryside = mix(earth_col, outer_grass, grass_patch * 0.88);
		countryside = mix(countryside, path_sand, footpath * 0.88);
		earth_col = mix(earth_col, countryside, outer_zone);
	}

	// 2. Sunlit provincial poured-concrete barangay slabs (warm cream-stone, <= 0.59)
	vec3 cement_sun = vec3(0.58, 0.54, 0.48);
	vec3 cement_shaded = vec3(0.46, 0.43, 0.39);
	vec3 concrete_col = mix(cement_sun, cement_shaded, clamp(brush_var * wear * 0.85, 0.0, 1.0));
	float damp_slab = smoothstep(0.60, 0.82, fbm(xz * 0.21 + 7.3)) * wear;
	concrete_col = mix(concrete_col, vec3(0.39, 0.37, 0.34), damp_slab * 0.42);

	// 3.0m concrete expansion joints + delicate warm-espresso hairline cracks
	float slab_seam = grid_line(xz / 3.0, 1.05);
	vec2 crack_uv = xz * 0.38 + vec2(grain * 0.50, brush_var * 0.50);
	float crack_field = abs(fract(crack_uv.x + crack_uv.y * 0.73) - 0.5);
	float crack_line = (1.0 - smoothstep(0.007, 0.022, crack_field)) * smoothstep(0.58, 0.78, macro_var) * wear;
	concrete_col = mix(concrete_col, warm_espresso, clamp(slab_seam * 0.28 + crack_line * 0.32, 0.0, 0.45));

	// 3. Sun-bleached warm barrio road macadam / concrete lanes along X = {-34, 0, +34}
	vec3 road_col = mix(vec3(0.38, 0.36, 0.35), vec3(0.48, 0.45, 0.42), grain * 0.62 + fine_grain * 0.38);
	float road_seam = grid_line(xz / 3.0, 0.95) * 0.22;
	road_col = mix(road_col, warm_espresso, road_seam);
	// Faded warm mango-ochre barrio center dashes on the 3 main N-S pathways (spared across the shallow canal)
	float nearest_lane_dx = min(abs(xz.x + wobble_ns), min(abs(xz.x + 34.0 - wobble_ns * 0.7), abs(xz.x - 34.0 + wobble_ns * 0.7)));
	float center_dash = (1.0 - smoothstep(0.09, 0.16, nearest_lane_dx)) * step(0.50, fract(xz.y * 0.16)) * main_ns_lanes * (1.0 - in_canal_bed) * (1.0 - canal_bank_zone * 0.6);
	float paint_chip = smoothstep(0.30, 0.66, grain * wear);
	road_col = mix(road_col, vec3(0.61, 0.50, 0.28), center_dash * (1.0 - paint_chip * 0.42) * 0.68);

	// Compose sunlit ground layers
	vec3 col = mix(earth_col, concrete_col, concrete_pad);
	col = mix(col, road_col, road_lane);

	// 4. Realistic damp green lumot (moss) along canal banks, street gutters, slab seams, and perimeter CHB wall bases
	float gutter_band = smoothstep(0.06, 0.34, road_lane) * (1.0 - smoothstep(0.66, 0.94, road_lane));
	float perim_wall_moss = (1.0 - smoothstep(0.0, 4.8, abs(r - (eff_radius - 0.8)))) * inside_arena;
	float lumot_carrier = clamp(gutter_band * 0.78 + canal_bank_zone * 0.92 + perim_wall_moss * 0.85 + slab_seam * concrete_pad * (1.0 - main_ns_lanes * 0.65) * 0.45, 0.0, 1.0);
	float lumot_mask = smoothstep(0.42, 0.70, moss_noise * 0.72 + fine_grain * 0.28) * lumot_carrier * wear;
	vec3 lumot_col = mix(vec3(0.22, 0.36, 0.17), vec3(0.32, 0.46, 0.21), grain);
	col = mix(col, lumot_col, clamp(lumot_mask * 0.78, 0.0, 0.78));

	// Shallow sunlit damp patches after a brief provincial shower (subtle warm reflection, not dark mud)
	float puddle_noise = fbm(xz * 0.19 - vec2(13.7, 8.2));
	float puddle_fade = 1.0 - smoothstep(96.0, 112.0, r);
	float puddle_mask = smoothstep(0.71, 0.79, puddle_noise) * (1.0 - road_lane * 0.80) * (1.0 - in_canal_bed) * wear * puddle_fade;
	col = mix(col, col * 0.80 + vec3(0.04, 0.05, 0.05), puddle_mask * 0.55);

	// 5. Sloped Tabing-Ilog river-stone wading aprons & shallow pebble canal bed across Z = 0
	float d_powerup = min(length(xz - vec2(-34.0, 0.0)), min(length(xz), length(xz - vec2(34.0, 0.0))));
	if (axz.y < 8.2 && inside_arena > 0.01) {
		float ford_lane = max(lane_center, max(lane_west, lane_east));
		// Smooth river-cobble wading ramp along canal banks (4.0..7.6m)
		float ramp_mask = smoothstep(3.8, 4.5, axz.y) * (1.0 - smoothstep(6.2, 7.8, axz.y)) * (0.55 + 0.45 * ford_lane) * inside_arena;
		float cobble_n = value_noise(xz * 2.6 + vec2(grain * 0.8, 0.0));
		vec3 river_stone = mix(vec3(0.45, 0.43, 0.39), vec3(0.55, 0.51, 0.45), cobble_n);
		river_stone = mix(river_stone, lumot_col, smoothstep(0.58, 0.82, moss_noise) * 0.38);
		col = mix(col, river_stone, ramp_mask * 0.85);

		// Sunlit shallow river-pebble & aqua-tinted silt bed beneath the walkable turquoise canal
		if (in_canal_bed > 0.01) {
			float pebble = value_noise(xz * 3.4);
			vec3 bed_stone = mix(vec3(0.28, 0.38, 0.34), vec3(0.42, 0.47, 0.40), pebble * 0.65 + grain * 0.35);
			bed_stone = mix(bed_stone, vec3(0.24, 0.39, 0.24), smoothstep(0.62, 0.84, moss_noise) * 0.35);
			// Subtle carved stone stepping rondels under the 3 canal power-up spots (-34,0), (0,0), (+34,0)
			if (d_powerup < 3.4) {
				float rondel = 1.0 - smoothstep(2.6, 3.2, d_powerup);
				float ring_groove = 1.0 - smoothstep(0.06, 0.14, abs(d_powerup - 2.1));
				vec3 stand_stone = mix(vec3(0.48, 0.46, 0.40), vec3(0.57, 0.52, 0.42), grain);
				stand_stone = mix(stand_stone, warm_espresso, ring_groove * 0.35);
				bed_stone = mix(bed_stone, stand_stone, rondel * 0.82);
			}
			col = mix(col, bed_stone, in_canal_bed);
		}
	}

	// 6. South Half-Court sunlit barrio basketball court lines at own_base_center (default 0, +64)
	vec2 d_south = xz - own_base_center;
	if (abs(d_south.x) < 13.8 && abs(d_south.y) < 13.8) {
		float court_scuff = clamp(grain * 0.7 + fine_grain * 0.3, 0.0, 1.0) * wear;
		vec3 court_slab = mix(vec3(0.29, 0.46, 0.39), concrete_col, court_scuff * 0.40);
		vec3 key_paint = mix(vec3(0.55, 0.33, 0.25), concrete_col, court_scuff * 0.34);
		float key_dir = own_base_center.y >= 0.0 ? 1.0 : -1.0;
		float local_y = d_south.y * key_dir;
		float in_key = step(abs(d_south.x), 3.2) * step(2.2, local_y) * step(local_y, 11.6);
		vec3 court_col = mix(court_slab, key_paint, in_key);

		float outer_box = max(
			1.0 - smoothstep(0.10, 0.18, abs(abs(d_south.x) - 10.8)),
			1.0 - smoothstep(0.10, 0.18, abs(abs(d_south.y) - 11.6))
		) * step(abs(d_south.x), 11.0) * step(abs(d_south.y), 11.8);
		float key_border = max(
			(1.0 - smoothstep(0.09, 0.17, abs(abs(d_south.x) - 3.2))) * step(2.2, local_y) * step(local_y, 11.6),
			(1.0 - smoothstep(0.09, 0.17, abs(local_y - 2.2))) * step(abs(d_south.x), 3.3)
		);
		float ft_circle = 1.0 - smoothstep(0.09, 0.17, abs(length(vec2(d_south.x, local_y - 2.2)) - 2.4));
		vec2 hoop_pt = vec2(0.0, 10.2);
		float arc_3pt = (1.0 - smoothstep(0.10, 0.18, abs(length(vec2(d_south.x, local_y) - hoop_pt) - 8.2))) * step(local_y, 10.4);
		float half_line = (1.0 - smoothstep(0.09, 0.17, abs(local_y + 11.6))) * step(abs(d_south.x), 10.9);
		float court_lines = clamp(max(max(outer_box, key_border), max(max(ft_circle, arc_3pt), half_line)), 0.0, 1.0);

		float line_wear = 1.0 - smoothstep(0.58, 0.88, fine_grain * wear) * 0.38;
		vec3 rustic_line_col = vec3(0.64, 0.60, 0.50);
		court_col = mix(court_col, rustic_line_col, court_lines * line_wear);
		float court_blend = (1.0 - smoothstep(12.2, 13.6, abs(d_south.x))) * (1.0 - smoothstep(12.2, 13.6, abs(d_south.y)));
		col = mix(col, court_col, court_blend * 0.94);
	}

	// 7. North Barrio Plaza sunlit terracotta & warm sandstone tiles at enemy_base_center (default 0, -64)
	vec2 d_north = xz - enemy_base_center;
	if (abs(d_north.x) < 13.8 && abs(d_north.y) < 13.8) {
		vec2 tile_uv = d_north * 0.85;
		vec2 tile_id = floor(tile_uv);
		float checker = mod(tile_id.x + tile_id.y, 2.0);
		float tile_mortar = grid_line(tile_uv, 1.15);
		vec3 clay_tile = vec3(0.54, 0.36, 0.27);
		vec3 adobe_tile = vec3(0.59, 0.53, 0.44);
		vec3 plaza_col = mix(adobe_tile, clay_tile, checker * 0.68);
		plaza_col *= 0.92 + 0.12 * value_noise(tile_id * 1.7);
		float seal_ring = 1.0 - smoothstep(0.12, 0.24, abs(length(d_north) - 5.4));
		plaza_col = mix(plaza_col, vec3(0.62, 0.50, 0.28), seal_ring * 0.78);
		plaza_col = mix(plaza_col, warm_espresso, tile_mortar * 0.34);
		float plaza_blend = (1.0 - smoothstep(12.2, 13.6, abs(d_north.x))) * (1.0 - smoothstep(12.2, 13.6, abs(d_north.y)));
		col = mix(col, plaza_col, plaza_blend * 0.94);
	}

	// Circular 84m barangay boundary mossy concrete curb ring
	float ring_edge = abs(r - eff_radius);
	if (ring_edge < 1.1) {
		float curb_joint = step(0.5, fract(theta * 28.0));
		vec3 perim_curb = mix(vec3(0.47, 0.44, 0.38), lumot_col, curb_joint * 0.42 + smoothstep(0.45, 0.75, moss_noise) * 0.35);
		perim_curb = mix(perim_curb, warm_espresso, (1.0 - smoothstep(0.0, 0.16, abs(ring_edge - 0.45))) * 0.42);
		float ring_mask = 1.0 - smoothstep(0.35, 1.05, ring_edge);
		col = mix(col, perim_curb, ring_mask * 0.80);
	}

	// Seamless radial feather into warm sunlit horizon earth & tropical_sky.gdshader ground_color (0.40, 0.34, 0.27)
	float radial_wobble = (macro_var - 0.5) * 11.0 + (brush_var - 0.5) * 4.5;
	float haze_fade = smoothstep(102.0, 142.0, r + radial_wobble);
	vec3 sunlit_haze_earth = mix(vec3(0.46, 0.39, 0.29), vec3(0.41, 0.35, 0.28), smoothstep(112.0, 142.0, r));
	col = mix(col, sunlit_haze_earth, haze_fade * 0.88);

	float outer_span = max(r, max(axz.x, axz.y) * 0.98);
	float skirt_fade = smoothstep(140.0, 174.0, outer_span + radial_wobble * 0.45);
	col = mix(col, vec3(0.40, 0.34, 0.27), skirt_fade);

	// Keep arena_half active without introducing a square border seam.
	col += vec3(0.000001) * clamp(arena_half.x + arena_half.y, 0.0, 1.0);

	// Hard anti-overexposure guarantee: max RGB <= 0.65 everywhere.
	ALBEDO = clamp(col, vec3(0.08), vec3(0.65));
	float base_rough = mix(0.90, 0.78, max(road_lane, concrete_pad * 0.65));
	base_rough = mix(base_rough, 0.62, damp_slab * concrete_pad);
	base_rough = mix(base_rough, 0.30, puddle_mask);
	ROUGHNESS = mix(base_rough, 0.96, clamp(haze_fade * 0.85 + skirt_fade, 0.0, 1.0));
	METALLIC = 0.0;
}
