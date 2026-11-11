shader_type spatial;
// Provincial Pinoy Barangay circular ground shader (Valorant clarity x Arcane brushwork x 90s Komiks ink).
// Strict lighting discipline: diffuse albedo never exceeds 0.65 on any channel (zero white clipping).
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

	// Inside circular barangay compound (r <= arena_radius)
	float inside_arena = 1.0 - smoothstep(eff_radius - 1.2, eff_radius + 1.6, r);

	// Organic barrio road & eskinita lane masks inside the 84m circle
	float spine_wobble = sin(xz.y * 0.055) * 1.4;
	float lane_spine = 1.0 - smoothstep(4.2, 4.9, abs(xz.x + spine_wobble));
	// Flank lanes curving through West Talipapa & East Toda crossings
	float flank_x = 46.0 - 0.14 * min(axz.y, 60.0);
	float lane_flanks = (1.0 - smoothstep(3.4, 4.1, abs(axz.x - flank_x))) * (1.0 - smoothstep(58.0, 66.0, axz.y));
	// Concentric barrio ring lanes & creek-bank walk
	float ring_mid = 1.0 - smoothstep(2.6, 3.2, abs(r - 39.0));
	float ring_outer = 1.0 - smoothstep(3.0, 3.7, abs(r - 64.0));
	float creek_walk = (1.0 - smoothstep(2.4, 3.0, abs(axz.y - 9.2))) * (1.0 - smoothstep(eff_radius - 8.0, eff_radius - 3.0, axz.x));

	float asphalt_lane = clamp(max(max(lane_spine, lane_flanks), max(ring_outer, creek_walk * 0.85)), 0.0, 1.0) * inside_arena;

	// Narrow barrio concrete slab eskinitas & residential walkways inside r <= 84m
	float eskinita_diag = 1.0 - smoothstep(2.1, 2.6, abs(abs(xz.x) * 0.72 - abs(xz.y) * 0.58 + 4.0));
	float concrete_pad = clamp(max(ring_mid, eskinita_diag * 0.9) + smoothstep(0.36, 0.68, macro_var) * 0.78, 0.0, 1.0) * inside_arena;

	// Estero / Sapa creek bed mask (narrow drainage creek around z = 0, spared at the 3 bridges)
	float bridge_center = 1.0 - smoothstep(7.2, 7.8, axz.x);
	float bridge_flanks = max(
		1.0 - smoothstep(5.8, 6.4, abs(axz.x - 46.0)),
		1.0 - smoothstep(5.8, 6.4, abs(axz.x - 67.0))
	);
	float bridge_span = clamp(max(bridge_center, bridge_flanks), 0.0, 1.0);
	float in_creek_bed = (1.0 - smoothstep(5.0, 5.8, axz.y)) * (1.0 - bridge_span) * inside_arena;

	// 1. Compacted brown provincial earth & outer barangay countryside (r > 84m & between lanes)
	vec3 earth_dry = vec3(0.41, 0.31, 0.22);
	vec3 earth_damp = vec3(0.27, 0.20, 0.15);
	vec3 earth_col = mix(earth_damp, earth_dry, clamp(brush_var * 0.75 + fine_grain * 0.25, 0.0, 1.0));
	// Sparse interior damuhan weeds inside the compound
	float weed_mask = smoothstep(0.60, 0.82, fbm(xz * 0.42 + 19.4)) * (1.0 - asphalt_lane);
	earth_col = mix(earth_col, vec3(0.22, 0.28, 0.16), weed_mask * 0.55);

	// Outer provincial countryside beyond the 84m perimeter (r >= 86m):
	// dark-olive damuhan/talahib grass patches + meandering earthen footpaths (zero square borders).
	float outer_zone = smoothstep(eff_radius + 2.0, eff_radius + 9.5, r);
	if (outer_zone > 0.001) {
		float meadow_macro = fbm(xz * 0.055 + vec2(31.2, -17.8));
		float meadow_detail = fbm(xz * 0.24 - vec2(9.4, 22.1));
		float olive_patch = smoothstep(0.36, 0.66, meadow_macro * 0.68 + meadow_detail * 0.32);
		vec3 dark_olive = mix(vec3(0.14, 0.21, 0.11), vec3(0.21, 0.28, 0.14), meadow_detail);
		vec3 sun_grass = vec3(0.33, 0.29, 0.17);
		vec3 outer_grass = mix(dark_olive, sun_grass, smoothstep(0.66, 0.88, grain) * 0.48);

		// Organic meandering provincial earthen footpaths (pilapil / landas) radiating & curving past r = 90m
		float warp_ang = theta + sin(r * 0.048 + macro_var * 2.6) * 0.28 + (brush_var - 0.5) * 0.16;
		float radial_spoke = abs(sin(warp_ang * 3.0 + 0.45)) * r;
		float radial_path = 1.0 - smoothstep(1.6, 3.1, radial_spoke);
		float loop_radius = eff_radius + 14.5 + sin(theta * 3.0 + 1.1) * 4.2 + (macro_var - 0.5) * 6.5;
		float loop_path = 1.0 - smoothstep(1.3, 2.5, abs(r - loop_radius));
		float footpath = clamp(max(radial_path, loop_path), 0.0, 1.0) * (1.0 - smoothstep(126.0, 142.0, r));

		vec3 path_clay = mix(vec3(0.32, 0.24, 0.17), vec3(0.43, 0.33, 0.23), fine_grain * 0.65 + grain * 0.35);
		float path_edge_ink = smoothstep(0.15, 0.45, footpath) * (1.0 - smoothstep(0.55, 0.85, footpath));
		path_clay = mix(path_clay, vec3(0.13, 0.11, 0.10), path_edge_ink * 0.28);

		vec3 countryside = mix(earth_col, outer_grass, olive_patch * 0.86);
		countryside = mix(countryside, path_clay, footpath * 0.90);
		earth_col = mix(earth_col, countryside, outer_zone);
	}

	// 2. Cracked wet barrio cement slabs (strictly <= 0.52)
	vec3 cement_lit = vec3(0.49, 0.46, 0.42);
	vec3 cement_worn = vec3(0.37, 0.35, 0.33);
	vec3 concrete_col = mix(cement_lit, cement_worn, clamp(brush_var * wear, 0.0, 1.0));
	float wet_slab = smoothstep(0.56, 0.78, fbm(xz * 0.21 + 7.3)) * wear;
	concrete_col = mix(concrete_col, vec3(0.28, 0.27, 0.26), wet_slab * 0.52);

	// Concrete pour expansion joints (2.4m slabs) + komiks diagonal slab cracks
	float slab_seam = grid_line(xz / 2.4, 1.15);
	vec2 crack_uv = xz * 0.42 + vec2(grain * 0.55, brush_var * 0.55);
	float crack_field = abs(fract(crack_uv.x + crack_uv.y * 0.73) - 0.5);
	float crack_line = (1.0 - smoothstep(0.008, 0.026, crack_field)) * smoothstep(0.52, 0.74, macro_var) * wear;
	vec3 komiks_ink = vec3(0.09, 0.08, 0.10);
	concrete_col = mix(concrete_col, komiks_ink, clamp(slab_seam * 0.48 + crack_line * 0.55, 0.0, 0.72));

	// 3. Worn barrio asphalt lanes
	vec3 asphalt_col = mix(vec3(0.18, 0.19, 0.21), vec3(0.27, 0.27, 0.29), grain * 0.65 + fine_grain * 0.35);
	float tire_rut = exp(-pow((abs(xz.x + spine_wobble) - 1.6) * 1.1, 2.0)) * lane_spine;
	asphalt_col *= 1.0 - tire_rut * 0.12;
	// Faded painted barrio lane center dashes (warm ochre-amber, capped well below 0.65)
	float center_dash = (1.0 - smoothstep(0.09, 0.16, abs(xz.x + spine_wobble))) * step(0.48, fract(xz.y * 0.18)) * lane_spine * (1.0 - in_creek_bed);
	float paint_chip = smoothstep(0.28, 0.62, grain * wear);
	asphalt_col = mix(asphalt_col, vec3(0.56, 0.45, 0.22), center_dash * (1.0 - paint_chip * 0.45) * 0.72);

	// 4. Mossy kanal street-edge gutters & komiks cross-hatching along lane borders
	float kanal_band = smoothstep(0.08, 0.38, asphalt_lane) * (1.0 - smoothstep(0.62, 0.92, asphalt_lane));
	float grate_rib = step(0.55, fract((xz.x + xz.y) * 2.6));
	vec3 kanal_moss = mix(vec3(0.14, 0.16, 0.15), vec3(0.17, 0.25, 0.14), smoothstep(0.35, 0.70, grain));
	kanal_moss = mix(kanal_moss, komiks_ink, grate_rib * 0.32);

	// Compose base ground from earth -> concrete eskinitas -> asphalt lanes -> mossy kanal gutters
	vec3 col = mix(earth_col, concrete_col, concrete_pad);
	col = mix(col, asphalt_col, asphalt_lane);
	col = mix(col, kanal_moss, kanal_band * 0.88);

	// Subtle painted 90s Komiks pen hatch lines in worn concrete & gutter shadows
	float hatch_stripe = 1.0 - smoothstep(0.06, 0.18, abs(fract((xz.x - xz.y) * 3.4) - 0.5));
	float hatch_mask = (kanal_band * 0.65 + crack_line * 0.45 + smoothstep(0.68, 0.86, macro_var) * concrete_pad * 0.35) * wear;
	col = mix(col, komiks_ink, clamp(hatch_stripe * hatch_mask * 0.34, 0.0, 0.40));

	// Rain puddles on provincial earth & worn concrete corners (dark wet albedo + low roughness, tapered out past r = 104m)
	float puddle_noise = fbm(xz * 0.19 - vec2(13.7, 8.2));
	float puddle_fade = 1.0 - smoothstep(96.0, 112.0, r);
	float puddle_mask = smoothstep(0.67, 0.76, puddle_noise) * (1.0 - asphalt_lane * 0.75) * (1.0 - in_creek_bed) * wear * puddle_fade;
	float puddle_rim = smoothstep(0.64, 0.68, puddle_noise) * (1.0 - puddle_mask) * wear * puddle_fade;
	col = mix(col, vec3(0.16, 0.15, 0.15), puddle_mask * 0.62);
	col = mix(col, komiks_ink, puddle_rim * 0.25);

	// Central Rotonda / Poso Well radial stone apron around (0, 0)
	if (r < 11.5) {
		float hub_blend = (1.0 - smoothstep(10.2, 11.5, r)) * (1.0 - in_creek_bed * 0.4);
		float cobble_ring = abs(fract(r * 0.55) - 0.5);
		float cobble_spoke = abs(fract(theta * 3.82) - 0.5);
		float mortar = 1.0 - smoothstep(0.04, 0.11, min(cobble_ring, cobble_spoke));
		vec3 poso_stone = mix(vec3(0.44, 0.38, 0.32), vec3(0.51, 0.45, 0.38), grain);
		poso_stone = mix(poso_stone, vec3(0.38, 0.26, 0.20), 1.0 - smoothstep(2.4, 2.9, r));
		poso_stone = mix(poso_stone, komiks_ink, mortar * 0.42);
		col = mix(col, poso_stone, hub_blend * 0.88);
	}

	// 5. South Half-Court rustic painted basketball lines at own_base_center (default 0, +64)
	vec2 d_south = xz - own_base_center;
	if (abs(d_south.x) < 13.8 && abs(d_south.y) < 13.8) {
		float court_scuff = clamp(grain * 0.7 + fine_grain * 0.3, 0.0, 1.0) * wear;
		vec3 court_slab = mix(vec3(0.21, 0.34, 0.28), concrete_col, court_scuff * 0.42);
		vec3 key_paint = mix(vec3(0.47, 0.24, 0.19), concrete_col, court_scuff * 0.36);
		// Key lane toward the south baseline + free-throw circle + 3-point arc
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

		// Rustic weathered cream-ochre paint (strictly <= 0.62, chipped by street wear)
		float line_wear = 1.0 - smoothstep(0.58, 0.88, fine_grain * wear) * 0.42;
		vec3 rustic_line_col = vec3(0.61, 0.56, 0.45);
		court_col = mix(court_col, rustic_line_col, court_lines * line_wear);
		float court_blend = (1.0 - smoothstep(12.2, 13.6, abs(d_south.x))) * (1.0 - smoothstep(12.2, 13.6, abs(d_south.y)));
		col = mix(col, court_col, court_blend * 0.94);
	}

	// 6. North Barrio Plaza terracotta & sandstone tiles at enemy_base_center (default 0, -64)
	vec2 d_north = xz - enemy_base_center;
	if (abs(d_north.x) < 13.8 && abs(d_north.y) < 13.8) {
		vec2 tile_uv = d_north * 0.85;
		vec2 tile_id = floor(tile_uv);
		float checker = mod(tile_id.x + tile_id.y, 2.0);
		float tile_mortar = grid_line(tile_uv, 1.25);
		vec3 clay_tile = vec3(0.47, 0.29, 0.22);
		vec3 adobe_tile = vec3(0.53, 0.46, 0.37);
		vec3 plaza_col = mix(adobe_tile, clay_tile, checker * 0.72);
		plaza_col *= 0.90 + 0.14 * value_noise(tile_id * 1.7);
		// Faded barrio sun-seal ring & mortar ink
		float seal_ring = 1.0 - smoothstep(0.12, 0.24, abs(length(d_north) - 5.4));
		plaza_col = mix(plaza_col, vec3(0.58, 0.45, 0.22), seal_ring * 0.82);
		plaza_col = mix(plaza_col, komiks_ink, tile_mortar * 0.50);
		float plaza_blend = (1.0 - smoothstep(12.2, 13.6, abs(d_north.x))) * (1.0 - smoothstep(12.2, 13.6, abs(d_north.y)));
		col = mix(col, plaza_col, plaza_blend * 0.94);
	}

	// Creek bed mossy silt & stone under Estero water
	if (in_creek_bed > 0.01) {
		vec3 creek_bed = mix(vec3(0.11, 0.17, 0.14), vec3(0.18, 0.24, 0.18), grain);
		col = mix(col, creek_bed, in_creek_bed);
	}

	// Circular 84m barangay boundary kanal curb ring
	float ring_edge = abs(r - eff_radius);
	if (ring_edge < 1.1) {
		float curb_joint = step(0.5, fract(theta * 28.0));
		vec3 perim_curb = mix(vec3(0.36, 0.33, 0.28), vec3(0.20, 0.24, 0.17), curb_joint * 0.55);
		perim_curb = mix(perim_curb, komiks_ink, 1.0 - smoothstep(0.0, 0.18, abs(ring_edge - 0.45)));
		float ring_mask = 1.0 - smoothstep(0.35, 1.05, ring_edge);
		col = mix(col, perim_curb, ring_mask * 0.82);
	}

	// Radial edge feathering into warm golden-hour horizon haze (r = 102..142m) and sky-dome ground match (r = 142..176m)
	// so high bird's-eye overview never reveals a square outer PlaneMesh boundary or corner seam.
	float radial_wobble = (macro_var - 0.5) * 11.0 + (brush_var - 0.5) * 4.5;
	float haze_fade = smoothstep(102.0, 142.0, r + radial_wobble);
	vec3 golden_haze_earth = mix(vec3(0.39, 0.29, 0.20), vec3(0.31, 0.24, 0.18), smoothstep(112.0, 142.0, r));
	col = mix(col, golden_haze_earth, haze_fade * 0.90);

	float outer_span = max(r, max(axz.x, axz.y) * 0.98);
	float skirt_fade = smoothstep(140.0, 174.0, outer_span + radial_wobble * 0.45);
	// Matches tropical_sky.gdshader's lower hemisphere ground_color (0.25, 0.20, 0.18) under StreetMap golden-hour light.
	col = mix(col, vec3(0.24, 0.20, 0.19), skirt_fade);

	// Silence unused uniform optimization warning if arena_half is set from GDScript while keeping zero square border.
	col += vec3(0.000001) * clamp(arena_half.x + arena_half.y, 0.0, 1.0);

	// Hard anti-overexposure guarantee: max RGB <= 0.65 everywhere.
	ALBEDO = clamp(col, vec3(0.04), vec3(0.65));
	float base_rough = mix(0.92, 0.76, max(asphalt_lane, concrete_pad * 0.6));
	base_rough = mix(base_rough, 0.58, wet_slab * concrete_pad);
	base_rough = mix(base_rough, 0.26, puddle_mask);
	ROUGHNESS = mix(base_rough, 0.98, clamp(haze_fade * 0.85 + skirt_fade, 0.0, 1.0));
	METALLIC = 0.0;
}
