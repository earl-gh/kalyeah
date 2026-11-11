shader_type spatial;
// Provincial Pinoy Sapa / Estero water shader: murky olive-teal channel with
// stylized Arcane painted current brushstrokes and crisp dark-inked shoreline foam.
render_mode blend_mix, depth_draw_opaque, cull_disabled, specular_schlick_ggx;

uniform vec4 shallow_color : source_color = vec4(0.17, 0.32, 0.24, 0.89);
uniform vec4 deep_color : source_color = vec4(0.08, 0.19, 0.19, 0.95);
uniform vec4 foam_color : source_color = vec4(0.62, 0.59, 0.50, 0.95);
uniform vec4 ink_color : source_color = vec4(0.08, 0.08, 0.10, 0.98);
uniform float creek_half_width = 5.5;
uniform float flow_speed = 0.65;

varying vec3 v_world;

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
	v += 0.60 * value_noise(p);
	v += 0.28 * value_noise(p * 2.13 + vec2(7.1, 3.4));
	v += 0.12 * value_noise(p * 4.21 + vec2(15.7, 9.2));
	return v;
}

void vertex() {
	float t = mod(TIME, 120.0);
	vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float swell = sin(wp.x * 0.85 + t * 1.9) * cos(wp.z * 1.35 - t * 1.4) * 0.032;
	VERTEX.y += swell;
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = mod(TIME, 120.0);
	// Bank distance works cleanly for any PlaneMesh creek width via UV.y (0 at North bank, 1 at South bank).
	float edge_norm = min(UV.y, 1.0 - UV.y) * 2.0;
	float bank_dist = edge_norm * creek_half_width;
	float depth_t = clamp(bank_dist / max(creek_half_width * 0.62, 0.5), 0.0, 1.0);

	// Anisotropic Arcane brushstroke UVs stretched along the creek flow (+X / -X)
	vec2 flow_uv_a = vec2(v_world.x * 0.28 - t * flow_speed * 0.42, v_world.z * 1.35 + sin(v_world.x * 0.35) * 0.22);
	vec2 flow_uv_b = vec2(v_world.x * 0.46 - t * flow_speed * 0.64 + 11.3, v_world.z * 2.10 - cos(v_world.x * 0.25) * 0.18);
	float s1 = stroke_fbm(flow_uv_a);
	float s2 = stroke_fbm(flow_uv_b);
	float brush = s1 * 0.62 + s2 * 0.38;

	// Murky olive-teal sapa base with posterized painterly gouache value steps
	vec3 water = mix(shallow_color.rgb, deep_color.rgb, smoothstep(0.08, 0.88, depth_t));
	float dark_trough = 1.0 - smoothstep(0.30, 0.42, brush);
	float mid_stroke = smoothstep(0.54, 0.64, brush);
	float lit_ripple = smoothstep(0.73, 0.80, brush);
	// Crisp thin painted ink filament bordering sunlit ripple strokes
	float ripple_ink = (1.0 - smoothstep(0.018, 0.048, abs(brush - 0.72))) * smoothstep(0.18, 0.55, depth_t);

	water = mix(water, deep_color.rgb * 0.72, dark_trough * 0.42);
	water = mix(water, vec3(0.22, 0.37, 0.28), mid_stroke * 0.38);
	water = mix(water, mix(foam_color.rgb, vec3(0.46, 0.50, 0.38), 0.45), lit_ripple * 0.52);
	water = mix(water, ink_color.rgb, ripple_ink * 0.55);

	// Scalloped shoreline bank foam with crisp dark charcoal komiks outline
	float scallop = 0.56 + 0.20 * sin(v_world.x * 1.55 + t * 1.8) + 0.12 * cos(v_world.x * 3.2 - t * 1.1) + (s1 - 0.5) * 0.24;
	float foam_body = 1.0 - smoothstep(scallop - 0.07, scallop + 0.04, bank_dist);
	float foam_ink_outer = smoothstep(scallop - 0.11, scallop - 0.02, bank_dist) * (1.0 - smoothstep(scallop + 0.03, scallop + 0.14, bank_dist));
	float bank_contact_ink = 1.0 - smoothstep(0.0, 0.11, bank_dist);
	// Secondary broken Arcane sketch-line parallel to the bank
	float echo_line = (1.0 - smoothstep(0.035, 0.085, abs(bank_dist - (scallop + 0.34)))) * step(0.46, s2);
	float echo_ink = (1.0 - smoothstep(0.055, 0.11, abs(bank_dist - (scallop + 0.34)))) * step(0.46, s2) * (1.0 - echo_line);

	water = mix(water, foam_color.rgb, clamp(foam_body + echo_line * 0.72, 0.0, 1.0) * 0.90);
	float total_ink = clamp(max(max(foam_ink_outer, bank_contact_ink * 0.85), echo_ink * 0.75), 0.0, 1.0);
	water = mix(water, ink_color.rgb, total_ink * 0.88);

	NORMAL = normalize(vec3((s1 - 0.5) * 0.16, 1.0, (s2 - 0.5) * 0.18));
	ALBEDO = clamp(water, vec3(0.03), vec3(0.65));
	ROUGHNESS = mix(0.32, 0.68, clamp(foam_body + total_ink, 0.0, 1.0));
	SPECULAR = 0.36;
	ALPHA = clamp(mix(shallow_color.a, deep_color.a, depth_t) + foam_body * 0.08, 0.0, 0.98);
}
