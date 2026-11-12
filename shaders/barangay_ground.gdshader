shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// Deadlock-grade non-repeating Barangay street, coarse stone-chip asphalt, tar-crack &
// DPWH manhole detail, crisp crosswalks, sidewalk curbs, oriented building contact AO,
// circular base plazas, and sunken Tabing-Ilog creek bed shader.

uniform float arena_radius = 84.0;
uniform vec2 arena_half = vec2(87.0, 87.0);
uniform vec2 own_base_center = vec2(0.0, 64.0);
uniform vec2 enemy_base_center = vec2(0.0, -64.0);
uniform float wear : hint_range(0.0, 1.0) = 1.0;
uniform bool carve_creek_slot = false;
uniform bool is_creek_bed = false;

uniform int parcel_count = 0;
uniform vec4 parcel_pose[32];
uniform vec4 parcel_half[32];

varying vec2 v_world_xz;

// ---- Exact Analytical Curved Barangay Road & Canal Splines (matches MapLayout) ----

float canal_z(float x) {
	return 2.4 * sin(x * 0.046) + 0.8 * sin(x * 0.105);
}

float lane_center_x(float z) {
	float taper = 1.0 - smoothstep(50.0, 62.0, abs(z));
	return 4.2 * sin(z * 0.048) * taper;
}

float lane_east_x(float z) {
	float taper = 1.0 - smoothstep(56.0, 74.0, abs(z));
	return 34.5 + (3.8 * sin(z * 0.052 + 0.55) - 0.055 * abs(z)) * taper;
}

float lane_west_x(float z) {
	return -lane_east_x(-z);
}

float eskinita_south_z(float x) {
	return 28.0 + 3.0 * sin(x * 0.058);
}

float eskinita_north_z(float x) {
	return -eskinita_south_z(-x);
}

float lane_center_hw(float z) {
	float az = abs(z);
	float d1 = (az - 28.0) / 8.5;
	float d2 = z / 8.5;
	float d3 = (az - 62.0) / 10.0;
	return 4.45 + 2.15 * exp(-d1 * d1) + 1.85 * exp(-d2 * d2) + 2.40 * exp(-d3 * d3);
}

float lane_side_hw(float z) {
	float az = abs(z);
	float d1 = (az - 28.0) / 9.0;
	float d2 = z / 8.5;
	return 4.25 + 1.95 * exp(-d1 * d1) + 1.65 * exp(-d2 * d2);
}

float eskinita_hw(float x) {
	float ax = abs(x);
	float d1 = ax / 9.0;
	float d2 = (ax - 34.5) / 8.5;
	return 3.55 + 1.55 * exp(-d1 * d1) + 1.45 * exp(-d2 * d2);
}

float ford_proximity(float x) {
	float fw = 1.0 - smoothstep(4.6, 8.8, abs(x - lane_west_x(0.0)));
	float fc = 1.0 - smoothstep(5.2, 9.6, abs(x));
	float fe = 1.0 - smoothstep(4.6, 8.8, abs(x - lane_east_x(0.0)));
	return clamp(max(max(fw, fc), fe), 0.0, 1.0);
}

void vertex() {
	vec3 wp0 = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	if (is_creek_bed) {
		float local_z = VERTEX.z;
		VERTEX.z += canal_z(wp0.x);
		float profile = 1.0 - smoothstep(2.35, 4.55, abs(local_z));
		float target_depth = mix(-0.30, -0.09, ford_proximity(wp0.x));
		VERTEX.y += target_depth * profile;
		float dz_slope = sign(local_z) * smoothstep(2.35, 4.55, abs(local_z)) * (1.0 - smoothstep(4.45, 4.65, abs(local_z))) * (-target_depth * 0.42);
		NORMAL = normalize(vec3(0.0, 1.0, -dz_slope));
	}
	vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_world_xz = world_pos.xz;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// Prime-angle rotated multi-octave FBM: zero axis-aligned grid artifacts at any scale.
float fbm_organic(vec2 p) {
	float v = 0.0;
	float amp = 0.5;
	mat2 rot = mat2(vec2(0.80, -0.60), vec2(0.60, 0.80));
	for (int i = 0; i < 4; i++) {
		v += amp * vnoise(p);
		p = rot * p * 2.03 + vec2(19.7, 31.3);
		amp *= 0.5;
	}
	return v;
}

float box_sdf(vec2 p, vec2 half_ext, float radius) {
	vec2 q = abs(p) - half_ext + vec2(radius);
	return min(max(q.x, q.y), 0.0) + length(max(q, vec2(0.0))) - radius;
}

void fragment() {
	vec2 xz = v_world_xz;
	float cz = canal_z(xz.x);
	if (carve_creek_slot && abs(xz.x) < arena_radius + 3.0 && abs(xz.y - cz) < 4.55) {
		discard;
	}
	if (is_creek_bed && abs(xz.y - cz) > 4.62) {
		discard;
	}

	float r_dist = length(xz);
	float px_fw = min(length(dFdx(xz)), length(dFdy(xz)));
	float detail_vis = 1.0 - smoothstep(0.055, 0.22, px_fw);

	// Domain-warped macro & multi-scale mineral aggregate stipple (zero 3m simulation grid!)
	vec2 warp = vec2(
		fbm_organic(xz * 0.042 + vec2(3.1, 7.4)),
		fbm_organic(xz * 0.042 + vec2(18.6, -9.2))
	) - 0.5;
	float macro_var = fbm_organic(xz * 0.052 + warp * 1.8);
	float mid_var = fbm_organic(xz * 0.23 + warp * 0.9);
	float grit = vnoise(xz * 2.35 + warp * 2.2);
	float gravel_chip = vnoise(xz * 6.80 + warp * 1.5);
	float fine_pebble = vnoise(xz * 15.60 - warp * 1.1);
	float mineral_fleck = smoothstep(0.76, 0.93, vnoise(xz * 24.0 + vec2(5.3, -8.9)));

	// ---- 1. Base Provincial Laterite Courtyard Earth & Tropical Berm Grass ----
	vec3 earth_warm = vec3(0.35, 0.28, 0.21);
	vec3 earth_dust = vec3(0.42, 0.35, 0.27);
	vec3 clover_patch = vec3(0.25, 0.34, 0.19);
	vec3 col = mix(earth_warm, earth_dust, smoothstep(0.26, 0.76, macro_var));
	col += (mid_var - 0.5) * 0.048 + (grit - 0.5) * 0.038 + (gravel_chip - 0.5) * 0.032 * detail_vis;
	float grass_m = smoothstep(0.56, 0.75, fbm_organic(xz * 0.14 + vec2(11.2, -5.7))) * smoothstep(18.0, 48.0, r_dist);
	col = mix(col, clover_patch, grass_m * 0.56);
	float rough = 0.93;

	// ---- 2. Evaluate Curved Street Carriageways & Signed Distance to Curb ----
	float xc = lane_center_x(xz.y);
	float xw = lane_west_x(xz.y);
	float xe = lane_east_x(xz.y);
	float zs = eskinita_south_z(xz.x);
	float zn = eskinita_north_z(xz.x);

	float hwc = lane_center_hw(xz.y);
	float hws = lane_side_hw(xz.y);
	float hwe = eskinita_hw(xz.x);

	float dc_end = max(abs(xz.y) - 67.0, 0.0);
	float d_center = max(abs(xz.x - xc) - hwc, dc_end);

	float ds_end = max(abs(xz.y) - 80.0, 0.0);
	float d_west = max(abs(xz.x - xw) - hws, ds_end);
	float d_east = max(abs(xz.x - xe) - hws, ds_end);

	float de_end = max(abs(xz.x) - 80.0, 0.0);
	float d_esk_s = max(abs(xz.y - zs) - hwe, de_end);
	float d_esk_n = max(abs(xz.y - zn) - hwe, de_end);

	// Curved outer base-plaza collector loop around Z = +-54.5m
	float z_conn_s = 54.5 + 2.2 * cos(xz.x * 0.062);
	float z_conn_n = -54.5 - 2.2 * cos(xz.x * 0.062);
	float conn_end = max(abs(xz.x) - 40.0, 0.0);
	float d_conn = min(
		max(abs(xz.y - z_conn_s) - 4.2, conn_end),
		max(abs(xz.y - z_conn_n) - 4.2, conn_end)
	);

	// Smooth filleted intersections like Deadlock street corners
	float d_ns = min(d_center, min(d_west, d_east));
	float d_ew = min(min(d_esk_s, d_esk_n), d_conn);
	float corner_k = 2.8;
	float h_fillet = max(corner_k - abs(d_ns - d_ew), 0.0) / corner_k;
	float d_road = min(d_ns, d_ew) - h_fillet * h_fillet * corner_k * 0.25;

	// ---- 3. Sunlit Poured-Concrete Sidewalks (0.0m .. 2.85m outside road curb, matched to 3D curbs!) ----
	float sw_mask = (1.0 - smoothstep(2.45, 2.95, d_road)) * smoothstep(-0.12, 0.08, d_road);
	vec3 sw_base = mix(vec3(0.39, 0.37, 0.33), vec3(0.46, 0.43, 0.39), smoothstep(0.22, 0.78, macro_var));
	sw_base += (mid_var - 0.5) * 0.042 + (grit - 0.5) * 0.036 + (gravel_chip - 0.5) * 0.038 * detail_vis;
	float joint_along = abs(fract((xz.x * 0.19 + xz.y * 0.23) + warp.x * 0.42) - 0.5);
	float joint_seam = 1.0 - smoothstep(0.008, 0.024, joint_along);
	sw_base = mix(sw_base, vec3(0.25, 0.24, 0.21), joint_seam * 0.52);
	col = mix(col, sw_base, sw_mask);
	rough = mix(rough, 0.87, sw_mask);

	// ---- 4. Coarse Weathered Deadlock Asphalt Carriageways (Aggregate + Patches + Branched Tar Cracks!) ----
	float road_mask = 1.0 - smoothstep(-0.08, 0.08, d_road);
	vec3 asph_dark = vec3(0.17, 0.17, 0.18);
	vec3 asph_warm = vec3(0.26, 0.24, 0.23);
	vec3 asph = mix(asph_dark, asph_warm, smoothstep(0.18, 0.82, macro_var));
	// High-frequency crushed basalt & sun-bleached quartz stone-chip aggregate stipple (0.065..0.085 amplitude!)
	float agg_stipple = (grit - 0.5) * 0.056 + (gravel_chip - 0.5) * 0.078 + ((fine_pebble - 0.5) * 0.070 + mineral_fleck * 0.046) * detail_vis;
	asph += agg_stipple;

	// Rectangular DPWH / utility-trench bitumen re-paving patches with dark tar-sealed perimeters
	vec2 patch_uv = mat2(vec2(0.985, -0.174), vec2(0.174, 0.985)) * xz;
	vec2 patch_cell = floor(patch_uv / vec2(9.5, 13.5));
	float patch_seed = hash12(patch_cell + vec2(7.1, 13.3));
	if (patch_seed > 0.60) {
		vec2 patch_local = (fract(patch_uv / vec2(9.5, 13.5)) - 0.5) * vec2(9.5, 13.5);
		vec2 patch_off = vec2(hash12(patch_cell + 2.1) - 0.5, hash12(patch_cell + 5.7) - 0.5) * vec2(2.6, 3.4);
		vec2 patch_sz = vec2(1.4 + 1.3 * hash12(patch_cell + 9.2), 2.2 + 2.4 * hash12(patch_cell + 4.4));
		float p_sdf = box_sdf(patch_local - patch_off, patch_sz, 0.16);
		float p_fill = 1.0 - smoothstep(-0.04, 0.05, p_sdf);
		float p_tar_rim = 1.0 - smoothstep(0.012, 0.042, abs(p_sdf));
		asph = mix(asph, vec3(0.155, 0.154, 0.162) + (gravel_chip - 0.5) * 0.054, p_fill * 0.64 * wear);
		asph = mix(asph, vec3(0.08, 0.08, 0.085), p_tar_rim * 0.84 * wear);
	}

	// Dual tire-wear & oil-darkened wheel ruts along N-S lanes and E-W eskinitas + sun-bleached center crown
	float off_c = abs(abs(xz.x - xc) - 1.55);
	float off_w = abs(abs(xz.x - xw) - 1.45);
	float off_e = abs(abs(xz.x - xe) - 1.45);
	float off_es = abs(abs(xz.y - zs) - 1.25);
	float off_en = abs(abs(xz.y - zn) - 1.25);
	float min_wheel = min(min(off_c, min(off_w, off_e)), min(off_es, off_en));
	float tire_track = (1.0 - smoothstep(0.28, 1.08, min_wheel)) * wear;
	float lane_crown = (1.0 - smoothstep(0.25, 0.95, min(abs(xz.x - xc), min(abs(xz.x - xw), abs(xz.x - xe))))) * wear;
	asph = mix(asph, vec3(0.125, 0.124, 0.132), tire_track * 0.44);
	asph += vec3(0.022, 0.020, 0.018) * lane_crown;

	// Branched thin dark matte bitumen repair cracks (mild warp + narrow ridge so zero wide pale puddles!)
	vec2 crack_uv_a = xz * 0.58 + warp * 0.65;
	float crack_f0 = fbm_organic(crack_uv_a);
	float crack_a = abs(crack_f0 - 0.50);
	float crack_grad = abs(fbm_organic(crack_uv_a + vec2(0.09, 0.06)) - crack_f0);
	float anti_blob = smoothstep(0.010, 0.028, crack_grad);
	float crack_b = abs(fbm_organic(xz * 1.18 - warp * 0.55 + vec2(14.3, -8.7)) - 0.50);
	float tar_primary = (1.0 - smoothstep(0.0022, 0.0082, crack_a)) * anti_blob * smoothstep(0.32, 0.66, macro_var);
	float tar_branch = (1.0 - smoothstep(0.0025, 0.0088, crack_b)) * anti_blob * (1.0 - smoothstep(0.0, 0.08, crack_a));
	float tar_seam = clamp(tar_primary + tar_branch * 0.80, 0.0, 1.0) * wear;
	asph = mix(asph, vec3(0.08, 0.08, 0.085), tar_seam * 0.88);

	// ---- 5. Terracotta/Ochre Brick-Cobble Intersection Aprons, DPWH Manholes & Crisp Zebra Crosswalks ----
	vec2 j_sw = vec2(lane_west_x(28.0), eskinita_south_z(-31.0));
	vec2 j_sc = vec2(lane_center_x(28.0), eskinita_south_z(4.0));
	vec2 j_se = vec2(lane_east_x(28.0), eskinita_south_z(36.0));
	vec2 j_nw = -j_se;
	vec2 j_nc = -j_sc;
	vec2 j_ne = -j_sw;

	float d_junc = min(
		min(length(xz - j_sw), min(length(xz - j_sc), length(xz - j_se))),
		min(length(xz - j_nw), min(length(xz - j_nc), length(xz - j_ne)))
	);
	float cobble_apron = (1.0 - smoothstep(4.8, 7.6, d_junc)) * road_mask;
	if (cobble_apron > 0.005) {
		vec2 cp = mat2(vec2(0.94, -0.34), vec2(0.34, 0.94)) * xz * vec2(1.85, 3.40);
		float crow = floor(cp.y);
		cp.x += mod(crow, 2.0) * 0.5;
		vec2 cid = floor(cp);
		vec2 cf = fract(cp);
		float c_mortar = max(1.0 - smoothstep(0.05, 0.15, cf.x), 1.0 - smoothstep(0.07, 0.19, cf.y));
		float c_tone = hash12(cid);
		vec3 brick_col = mix(vec3(0.32, 0.24, 0.19), vec3(0.42, 0.33, 0.25), c_tone);
		brick_col += (fine_pebble - 0.5) * 0.035 * detail_vis;
		brick_col = mix(brick_col, vec3(0.14, 0.13, 0.12), c_mortar * 0.74);
		asph = mix(asph, brick_col, cobble_apron * 0.88);
	}

	// Chipped warm-ochre dashed lane centerlines along curved N-S roads
	float clear_junc = smoothstep(7.2, 9.8, d_junc) * smoothstep(8.2, 10.5, abs(xz.y - cz));
	float dash_pat = smoothstep(0.34, 0.40, fract(xz.y * 0.14)) * (1.0 - smoothstep(0.76, 0.82, fract(xz.y * 0.14)));
	float center_stripe = (
		(1.0 - smoothstep(0.09, 0.17, abs(xz.x - xc))) * (1.0 - smoothstep(45.5, 48.0, abs(xz.y)))
		+ (1.0 - smoothstep(0.08, 0.16, abs(xz.x - xw))) * (1.0 - smoothstep(64.0, 70.0, abs(xz.y)))
		+ (1.0 - smoothstep(0.08, 0.16, abs(xz.x - xe))) * (1.0 - smoothstep(64.0, 70.0, abs(xz.y)))
	);
	float chip_paint = clamp(0.68 + (grit - 0.4) * 0.45, 0.35, 0.96);
	asph = mix(asph, vec3(0.56, 0.44, 0.19), clamp(center_stripe, 0.0, 1.0) * dash_pat * clear_junc * chip_paint * 0.84);

	// Crisp Deadlock-contrast worn-cream zebra crosswalks at ALL 6 road junctions, N/S of ALL 3 creek fords, & base throats!
	float d_xwalk_esk_s = abs(abs(xz.y - zs) - 6.35);
	float d_xwalk_esk_n = abs(abs(xz.y - zn) - 6.35);
	float d_xwalk_ford = abs(abs(xz.y - cz) - 7.35);
	float d_xwalk_plaza = abs(abs(xz.y) - 48.6);
	float d_ns_xwalk = min(min(d_xwalk_esk_s, d_xwalk_esk_n), min(d_xwalk_ford, d_xwalk_plaza));

	float xwalk_band_ns = 1.0 - smoothstep(1.08, 1.26, d_ns_xwalk);
	float stop_bar_ns = 1.0 - smoothstep(0.08, 0.16, abs(d_ns_xwalk - 1.58));
	float on_ns_carriageway = 1.0 - smoothstep(-0.72, -0.28, d_ns);

	float local_lane_u = xz.x - xc;
	if (abs(xz.x - xw) < abs(local_lane_u)) {
		local_lane_u = xz.x - xw;
	}
	if (abs(xz.x - xe) < abs(local_lane_u)) {
		local_lane_u = xz.x - xe;
	}
	float zebra_u = fract(local_lane_u * 0.92 + 0.25);
	float zebra_bar_ns = smoothstep(0.20, 0.26, zebra_u) * (1.0 - smoothstep(0.74, 0.80, zebra_u));

	// East-West eskinita throat crosswalks flanking the 6 brick junction aprons
	float d_ew_xwalk = min(
		abs(abs(xz.x - xc) - (hwc + 2.05)),
		min(abs(abs(xz.x - xw) - (hws + 1.95)), abs(abs(xz.x - xe) - (hws + 1.95)))
	);
	float on_ew_eskinita = 1.0 - smoothstep(-0.68, -0.24, min(d_esk_s, d_esk_n));
	float local_esk_v = (abs(xz.y - zs) < abs(xz.y - zn)) ? (xz.y - zs) : (xz.y - zn);
	float zebra_v = fract(local_esk_v * 0.92 + 0.25);
	float zebra_bar_ew = smoothstep(0.20, 0.26, zebra_v) * (1.0 - smoothstep(0.74, 0.80, zebra_v));
	float xwalk_band_ew = (1.0 - smoothstep(1.02, 1.20, d_ew_xwalk)) * on_ew_eskinita;

	float zebra_wear = clamp(0.78 + (grit - 0.42) * 0.42 + (gravel_chip - 0.5) * 0.22 - tire_track * 0.24, 0.46, 0.96);
	float xwalk_paint = max(
		max(xwalk_band_ns * zebra_bar_ns, stop_bar_ns * 0.82) * on_ns_carriageway,
		xwalk_band_ew * zebra_bar_ew
	) * zebra_wear;
	vec3 xwalk_col = vec3(0.61, 0.58, 0.51) + (fine_pebble - 0.5) * 0.03 * detail_vis;
	asph = mix(asph, xwalk_col, clamp(xwalk_paint, 0.0, 1.0) * 0.92);

	// Procedural Circular Cast-Iron DPWH Storm-Manhole Lids (`r = 0.48 m` scored concentric rings & radial ribs)
	vec2 mh_rel = xz - (j_sc + vec2(-1.35, 1.45));
	vec2 mh_cand = xz - (j_sw + vec2(1.45, -1.55));
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }
	mh_cand = xz - (j_se + vec2(-1.45, -1.50));
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }
	mh_cand = xz - (j_nw + vec2(1.45, 1.50));
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }
	mh_cand = xz - (j_nc + vec2(1.35, -1.45));
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }
	mh_cand = xz - (j_ne + vec2(-1.45, 1.55));
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }
	mh_cand = xz - vec2(lane_center_x(16.4) - 1.15, 16.4);
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }
	mh_cand = xz - vec2(lane_center_x(-16.4) + 1.15, -16.4);
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }
	mh_cand = xz - vec2(lane_west_x(16.8) + 1.25, 16.8);
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }
	mh_cand = xz - vec2(lane_east_x(-16.8) - 1.25, -16.8);
	if ( dot(mh_cand, mh_cand) < dot(mh_rel, mh_rel) ) { mh_rel = mh_cand; }

	float d_mh = length(mh_rel);
	float mh_mask = 0.0;
	if (d_mh < 0.66 && road_mask > 0.5) {
		float collar_m = (1.0 - smoothstep(0.58, 0.64, d_mh)) * smoothstep(0.46, 0.50, d_mh);
		float seam_m = (1.0 - smoothstep(0.47, 0.50, d_mh)) * smoothstep(0.43, 0.46, d_mh);
		float lid_m = 1.0 - smoothstep(0.45, 0.48, d_mh);
		float rings = smoothstep(-0.2, 0.65, cos(d_mh * 44.0));
		float ribs = smoothstep(-0.1, 0.70, cos(atan(mh_rel.y, mh_rel.x) * 8.0)) * step(0.11, d_mh);
		vec3 iron_lid = mix(vec3(0.14, 0.13, 0.13), vec3(0.28, 0.25, 0.22), max(rings, ribs) * 0.85);
		asph = mix(asph, vec3(0.34, 0.32, 0.29), collar_m * 0.85);
		asph = mix(asph, vec3(0.19, 0.17, 0.16), lid_m);
		asph = mix(asph, iron_lid, lid_m * 0.92);
		asph = mix(asph, vec3(0.08, 0.08, 0.09), seam_m * 0.92);
		mh_mask = lid_m;
	}

	// Dark recessed storm-drain gutter seam (-0.52m .. 0.0m, smooth AO with zero comb-stripe aliasing!)
	float gutter_band = smoothstep(-0.54, -0.16, d_road) * (1.0 - smoothstep(-0.01, 0.07, d_road));
	asph = mix(asph, vec3(0.10, 0.10, 0.105), gutter_band * 0.74);

	col = mix(col, asph, road_mask);
	float road_rough = mix(0.88, 0.78, clamp(tire_track * 0.45 + gutter_band * 0.35 + mh_mask * 0.55, 0.0, 1.0));
	rough = mix(rough, road_rough, road_mask);

	// Crisp Sunlit Beveled Concrete Curb Lip at d_road in [0.0 .. 0.28m]
	float curb_lip = smoothstep(-0.03, 0.05, d_road) * (1.0 - smoothstep(0.20, 0.34, d_road));
	float curb_ao = smoothstep(-0.18, -0.01, d_road) * (1.0 - smoothstep(0.0, 0.09, d_road));
	col = mix(col, vec3(0.52, 0.49, 0.43), curb_lip * 0.78);
	col *= 1.0 - curb_ao * 0.30;

	// ---- 6. Harmonized Circular South & North Barangay Base Plazas + South Landmark Court Apron ----
	float own_d = length(xz - own_base_center);
	float own_plaza_mask = 1.0 - smoothstep(12.6, 14.8, own_d);
	if (own_plaza_mask > 0.004) {
		vec2 lo = xz - own_base_center;
		float rad_ring_o = abs(fract(own_d * 0.42 + warp.x * 0.12) - 0.5);
		float spoke_o = abs(fract(atan(lo.y, lo.x) * 3.8197) - 0.5);
		float joint_o = max(1.0 - smoothstep(0.03, 0.08, rad_ring_o), 1.0 - smoothstep(0.02, 0.06, spoke_o));
		vec3 own_stone = mix(vec3(0.46, 0.41, 0.35), vec3(0.39, 0.32, 0.26), step(6.2, own_d) * step(own_d, 9.4));
		own_stone += (gravel_chip - 0.5) * 0.036 * detail_vis;
		own_stone = mix(own_stone, vec3(0.22, 0.20, 0.18), joint_o * 0.52);
		// Subtle faded barrio liga center-circle & key lines strictly inside the circular base radius!
		float inside_base = 1.0 - smoothstep(9.4, 11.0, own_d);
		float key_o = 1.0 - smoothstep(-0.08, 0.08, min(
			box_sdf(lo - vec2(0.0, 7.2), vec2(2.4, 2.8), 0.15),
			box_sdf(lo - vec2(0.0, -7.2), vec2(2.4, 2.8), 0.15)
		));
		own_stone = mix(own_stone, vec3(0.45, 0.27, 0.21), key_o * inside_base * 0.45 * chip_paint);
		float line_o = max(
			(1.0 - smoothstep(0.05, 0.12, abs(lo.y))) * step(abs(lo.x), 8.2),
			1.0 - smoothstep(0.05, 0.12, abs(own_d - 2.3))
		) * inside_base;
		own_stone = mix(own_stone, vec3(0.56, 0.53, 0.47), clamp(line_o, 0.0, 1.0) * chip_paint * 0.68);
		col = mix(col, own_stone, own_plaza_mask);
		rough = mix(rough, 0.85, own_plaza_mask);
	}

	// Dedicated court slab out at `(0, +95.5)` tucked strictly under `CoveredBasketballCourt` (zero tab at bird's-eye edge!)
	float lm_mask = 0.0;
	if (arena_radius > 1.0) {
		vec2 court_landmark_c = vec2(own_base_center.x, arena_radius + 10.2);
		float lm_sdf = box_sdf(xz - court_landmark_c, vec2(7.4, 7.2), 2.8);
		lm_mask = (1.0 - smoothstep(-0.25, 1.15, lm_sdf)) * (1.0 - smoothstep(96.2, 98.0, abs(xz.y)));
		if (lm_mask > 0.004) {
			vec3 lm_slab = mix(vec3(0.39, 0.35, 0.29), vec3(0.44, 0.39, 0.33), mid_var);
			col = mix(col, lm_slab, lm_mask * 0.82);
			rough = mix(rough, 0.86, lm_mask);
		}
	}

	float hall_d = length(xz - enemy_base_center);
	float hall_mask = 1.0 - smoothstep(12.6, 14.8, hall_d);
	if (hall_mask > 0.004) {
		vec2 lh = xz - enemy_base_center;
		float rad_ring = abs(fract(hall_d * 0.42 + warp.x * 0.12) - 0.5);
		float spoke = abs(fract(atan(lh.y, lh.x) * 3.8197) - 0.5);
		float p_joint = max(1.0 - smoothstep(0.03, 0.08, rad_ring), 1.0 - smoothstep(0.02, 0.06, spoke));
		vec3 plaza_stone = mix(vec3(0.47, 0.42, 0.36), vec3(0.39, 0.32, 0.26), step(6.2, hall_d) * step(hall_d, 9.4));
		plaza_stone += (gravel_chip - 0.5) * 0.036 * detail_vis;
		plaza_stone = mix(plaza_stone, vec3(0.22, 0.20, 0.18), p_joint * 0.52);
		col = mix(col, plaza_stone, hall_mask);
		rough = mix(rough, 0.85, hall_mask);
	}

	// ---- 7. Meandering *Tabing-Ilog* Sunken Riverbank Silt & Cobble Fords (Natural Fade Beyond 84m!) ----
	bool creek_active = is_creek_bed || carve_creek_slot || (parcel_count > 0);
	if (creek_active) {
		float canal_abs = abs(xz.y - cz + warp.y * 0.38);
		float bank_zone = 1.0 - smoothstep(3.4, 6.8, canal_abs);
		float inside_wall = (arena_radius > 1.0) ? (1.0 - smoothstep(arena_radius - 0.5, arena_radius + 2.8, r_dist)) : 1.0;
		float outer_swale = (arena_radius > 1.0) ? ((1.0 - inside_wall) * (1.0 - smoothstep(arena_radius + 4.0, arena_radius + 24.0, r_dist))) : 0.0;
		if (bank_zone > 0.004) {
			if (inside_wall > 0.004) {
				float ford_prox = ford_proximity(xz.x);
				vec3 bank_silt = mix(vec3(0.28, 0.26, 0.21), vec3(0.22, 0.28, 0.19), smoothstep(0.40, 0.76, mid_var));
				vec3 river_cobble = mix(vec3(0.30, 0.29, 0.26), vec3(0.41, 0.38, 0.33), fine_pebble);
				vec3 bed_col = mix(bank_silt, river_cobble, max(ford_prox * 0.88, 1.0 - smoothstep(2.4, 3.8, canal_abs)));
				float wet_dark = 1.0 - smoothstep(2.8, 4.4, canal_abs);
				bed_col *= mix(1.0, 0.72, wet_dark);
				col = mix(col, bed_col, bank_zone * inside_wall * 0.92);
				rough = mix(rough, mix(0.88, 0.38, wet_dark), bank_zone * inside_wall);
			}
			if (outer_swale > 0.004) {
				// Natural mossy green-brown provincial stream bank outside the 84m wall (ZERO grey concrete stripe!)
				vec3 natural_stream = mix(vec3(0.28, 0.25, 0.19), vec3(0.23, 0.31, 0.18), smoothstep(0.35, 0.75, mid_var));
				col = mix(col, natural_stream, bank_zone * outer_swale * 0.72);
			}
		}
	}

	// ---- 8. Oriented Building Storefront Thresholds & Soft Architectural Contact AO ----
	if (parcel_count > 0) {
		float min_bldg_d = 999.0;
		float front_apron = 0.0;
		for (int i = 0; i < 32; i++) {
			if (i >= parcel_count) {
				break;
			}
			vec4 pose = parcel_pose[i];
			vec2 half_e = parcel_half[i].xy;
			vec2 rel = xz - pose.xy;
			vec2 local_p = vec2(
				rel.x * pose.z - rel.y * pose.w,
				rel.x * pose.w + rel.y * pose.z
			);
			vec2 q = abs(local_p) - half_e;
			float d_obb = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0);
			if (d_obb < min_bldg_d) {
				min_bldg_d = d_obb;
			}
			float in_front_x = 1.0 - smoothstep(half_e.x - 0.2, half_e.x + 0.5, abs(local_p.x));
			float in_front_z = smoothstep(half_e.y - 0.15, half_e.y + 0.15, local_p.y) * (1.0 - smoothstep(half_e.y + 1.55, half_e.y + 2.20, local_p.y));
			front_apron = max(front_apron, in_front_x * in_front_z);
		}
		float skirt_m = (1.0 - smoothstep(1.15, 2.05, min_bldg_d)) * smoothstep(-0.35, 0.02, min_bldg_d);
		vec3 skirt_col = mix(vec3(0.41, 0.39, 0.35), vec3(0.45, 0.42, 0.37), front_apron);
		skirt_col += (grit - 0.5) * 0.028 + (gravel_chip - 0.5) * 0.026 * detail_vis;
		col = mix(col, skirt_col, max(skirt_m * 0.76, front_apron * 0.84));

		float wall_ao = mix(0.46, 1.0, smoothstep(-0.05, 1.45, min_bldg_d));
		float moss_skirt = (1.0 - smoothstep(0.0, 0.42, abs(min_bldg_d))) * smoothstep(0.45, 0.78, mid_var);
		col = mix(col, vec3(0.22, 0.28, 0.18), moss_skirt * 0.38);
		col *= wall_ao;
	}

	// ---- 9. Circular 84m Barangay Perimeter Stone Coping & Outer Tropical Barrio ----
	if (arena_radius > 1.0) {
		float rim_delta = r_dist - arena_radius;
		float coping = 1.0 - smoothstep(0.32, 0.82, abs(rim_delta));
		col = mix(col, vec3(0.45, 0.41, 0.36), coping * 0.58);
		float outside = smoothstep(1.2, 6.5, rim_delta);
		float hall_forecourt = (1.0 - smoothstep(8.2, 10.6, abs(xz.x))) * (1.0 - smoothstep(92.0, 95.5, -xz.y)) * step(xz.y, -80.0);
		float keep_forecourt = max(lm_mask * 0.85, hall_forecourt);
		vec3 outer_soil = mix(vec3(0.34, 0.30, 0.23), vec3(0.26, 0.34, 0.20), smoothstep(0.35, 0.75, macro_var));
		outer_soil += (mid_var - 0.5) * 0.036;
		col = mix(col, outer_soil, outside * (1.0 - keep_forecourt) * 0.88);
	}

	ALBEDO = clamp(col, vec3(0.08), vec3(0.65));
	ROUGHNESS = clamp(rough, 0.42, 0.96);
	SPECULAR = 0.14;
	METALLIC = 0.0;
}
