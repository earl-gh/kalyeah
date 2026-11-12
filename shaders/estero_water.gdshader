shader_type spatial;
// Sunlit Philippine barangay shallow walkable Tabing-Ilog creek shader:
// seated INSIDE the sunken -0.30m canal bed (Y = -0.12m deep, -0.055m over cobbled fords),
// bending organically along MapLayout.canal_z(x) with olive-jade / warm tea creek water.
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec4 shallow_color : source_color = vec4(0.23, 0.31, 0.21, 0.72);
uniform vec4 deep_color : source_color = vec4(0.10, 0.20, 0.17, 0.85);
uniform vec4 foam_color : source_color = vec4(0.58, 0.59, 0.53, 0.88);
uniform vec4 silt_tint : source_color = vec4(0.31, 0.29, 0.22, 0.76);
uniform float creek_half_width = 3.75;
uniform float curve_scale = 1.0;
uniform float flow_speed = 0.50;

varying vec3 v_world;

float canal_z(float x) {
	return (2.4 * sin(x * 0.046) + 0.8 * sin(x * 0.105)) * curve_scale;
}

float ford_mask_at(float x) {
	float x_east0 = 34.5 + 3.8 * sin(0.55);
	float x_west0 = -x_east0;
	float fw = 1.0 - smoothstep(4.6, 8.8, abs(x - x_west0));
	float fc = 1.0 - smoothstep(5.2, 9.6, abs(x));
	float fe = 1.0 - smoothstep(4.6, 8.8, abs(x - x_east0));
	return clamp(max(max(fw, fc), fe), 0.0, 1.0);
}

float hash21(vec2 p) {
	p = fract(p * vec2(127.1, 311.7));
	p += dot(p, p + 34.23);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x),
		mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x),
		u.y
	);
}

float stroke_fbm(vec2 p) {
	float v = 0.0;
	mat2 rot = mat2(vec2(0.80, -0.60), vec2(0.60, 0.80));
	v += 0.56 * value_noise(p);
	p = rot * p * 2.07 + vec2(7.1, 3.4);
	v += 0.30 * value_noise(p);
	p = rot * p * 2.03 + vec2(15.7, 9.2);
	v += 0.14 * value_noise(p);
	return v;
}

void vertex() {
	float t = mod(TIME, 120.0);
	vec3 wp0 = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX.z += canal_z(wp0.x);
	if (curve_scale > 0.01) {
		VERTEX.y += ford_mask_at(wp0.x) * 0.065;
	}
	float swell = sin(wp0.x * 0.88 + t * 1.60) * cos((wp0.z + VERTEX.z) * 1.35 - t * 1.15) * 0.012;
	VERTEX.y += swell;
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = mod(TIME, 120.0);
	float center_z = canal_z(v_world.x);
	float local_z = v_world.z - center_z;

	float edge_norm = min(UV.y, 1.0 - UV.y) * 2.0;
	float shore_warp = (stroke_fbm(vec2(v_world.x * 0.28, UV.y * 2.4)) - 0.5) * 0.26;
	float bank_dist = max(edge_norm * creek_half_width + shore_warp, 0.0);
	float depth_t = clamp(bank_dist / max(creek_half_width * 0.68, 0.5), 0.0, 1.0);

	float ford_mask = ford_mask_at(v_world.x);

	vec2 flow_uv_a = vec2(v_world.x * 0.30 - t * flow_speed * 0.36, local_z * 1.18);
	vec2 flow_uv_b = vec2(v_world.x * 0.48 + t * flow_speed * 0.24 + 9.7, local_z * 1.85);
	float s1 = stroke_fbm(flow_uv_a);
	float s2 = stroke_fbm(flow_uv_b);
	float brush = s1 * 0.58 + s2 * 0.42;

	float eff_depth = depth_t * (1.0 - ford_mask * 0.46);
	vec3 water = mix(silt_tint.rgb, shallow_color.rgb, smoothstep(0.02, 0.36, eff_depth));
	water = mix(water, deep_color.rgb, smoothstep(0.24, 0.90, eff_depth));

	float caustic_a = abs(s1 - s2);
	float caustic = (1.0 - smoothstep(0.02, 0.12, caustic_a)) * (0.42 + 0.58 * ford_mask) * (1.0 - eff_depth * 0.34);
	float mid_stroke = smoothstep(0.54, 0.68, brush);
	float lit_ripple = smoothstep(0.74, 0.84, brush);

	water = mix(water, vec3(0.28, 0.37, 0.26), mid_stroke * 0.30);
	water = mix(water, vec3(0.46, 0.48, 0.38), caustic * 0.40);
	water = mix(water, foam_color.rgb, lit_ripple * 0.34);

	float x_east0 = 34.5 + 3.8 * sin(0.55);
	vec2 f_west_pt = vec2(-x_east0, canal_z(-x_east0));
	vec2 f_east_pt = vec2(x_east0, canal_z(x_east0));
	float d_ford = min(length(v_world.xz - f_west_pt), min(length(v_world.xz), length(v_world.xz - f_east_pt)));
	if (d_ford < 5.8) {
		float ring_wave = fract(d_ford * 0.78 - t * 0.46);
		float wade_ring = (1.0 - smoothstep(0.04, 0.15, abs(ring_wave - 0.5))) * (1.0 - smoothstep(1.6, 5.6, d_ford));
		water = mix(water, foam_color.rgb, wade_ring * 0.34);
	}

	float scallop = 0.40 + 0.13 * sin(v_world.x * 1.35 + t * 1.35) + (s1 - 0.5) * 0.22;
	float foam_body = 1.0 - smoothstep(scallop - 0.10, scallop + 0.08, bank_dist);
	float echo_line = (1.0 - smoothstep(0.025, 0.075, abs(bank_dist - (scallop + 0.26)))) * step(0.48, s2);
	water = mix(water, foam_color.rgb, clamp(foam_body * 0.56 + echo_line * 0.34, 0.0, 1.0));

	NORMAL = normalize(vec3((s1 - 0.5) * 0.11, 1.0, (s2 - 0.5) * 0.13));
	ALBEDO = clamp(water, vec3(0.06), vec3(0.65));
	ROUGHNESS = mix(0.16, 0.54, clamp(foam_body * 0.80 + ford_mask * 0.18, 0.0, 1.0));
	SPECULAR = 0.42;
	float edge_feather = smoothstep(0.0, 0.065, edge_norm);
	if (edge_feather < 0.02) {
		discard;
	}
	float base_alpha = mix(shallow_color.a, deep_color.a, eff_depth);
	ALPHA = clamp((base_alpha - ford_mask * 0.14 + foam_body * 0.08) * edge_feather, 0.48, 0.88);
}
