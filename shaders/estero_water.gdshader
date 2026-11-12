shader_type spatial;
// Sunlit 90s Philippine province walkable shallow canal (sapa / tabing-ilog) water shader:
// clear, inviting shallow turquoise-jade water with dancing sunlit caustics, concentric wading ripples
// across the 3 main lane crossings (X = -34, 0, +34), and soft warm-espresso shoreline foam edges.
render_mode blend_mix, depth_draw_opaque, cull_disabled, specular_schlick_ggx;

uniform vec4 shallow_color : source_color = vec4(0.27, 0.61, 0.57, 0.72);
uniform vec4 deep_color : source_color = vec4(0.15, 0.47, 0.50, 0.84);
uniform vec4 foam_color : source_color = vec4(0.62, 0.65, 0.61, 0.92);
uniform vec4 ink_color : source_color = vec4(0.20, 0.16, 0.15, 0.82);
uniform float creek_half_width = 4.2;
uniform float flow_speed = 0.58;

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
	v += 0.58 * value_noise(p);
	v += 0.29 * value_noise(p * 2.13 + vec2(7.1, 3.4));
	v += 0.13 * value_noise(p * 4.21 + vec2(15.7, 9.2));
	return v;
}

void vertex() {
	float t = mod(TIME, 120.0);
	vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Gentle shallow-riffle surface shimmer (low amplitude so ankle-deep wading reads calm and clear)
	float swell = sin(wp.x * 0.95 + t * 1.75) * cos(wp.z * 1.45 - t * 1.30) * 0.018;
	VERTEX.y += swell;
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = mod(TIME, 120.0);
	float edge_norm = min(UV.y, 1.0 - UV.y) * 2.0;
	float bank_dist = edge_norm * creek_half_width;
	float depth_t = clamp(bank_dist / max(creek_half_width * 0.65, 0.5), 0.0, 1.0);

	// Shallow wading ford clarity along the 3 main N-S pathways (West -34, Center 0, East +34)
	float ford_west = 1.0 - smoothstep(4.5, 8.5, abs(v_world.x + 34.0));
	float ford_mid = 1.0 - smoothstep(5.0, 9.2, abs(v_world.x));
	float ford_east = 1.0 - smoothstep(4.5, 8.5, abs(v_world.x - 34.0));
	float ford_mask = clamp(max(max(ford_west, ford_mid), ford_east), 0.0, 1.0);

	// Anisotropic sunlit ripple UVs drifting gently along the shallow canal (+X / -X)
	vec2 flow_uv_a = vec2(v_world.x * 0.32 - t * flow_speed * 0.38, v_world.z * 1.25 + sin(v_world.x * 0.32) * 0.18);
	vec2 flow_uv_b = vec2(v_world.x * 0.52 + t * flow_speed * 0.26 + 9.7, v_world.z * 1.95 - cos(v_world.x * 0.28) * 0.15);
	float s1 = stroke_fbm(flow_uv_a);
	float s2 = stroke_fbm(flow_uv_b);
	float brush = s1 * 0.58 + s2 * 0.42;

	// Clear sunlit turquoise-jade base, extra translucent & aqua-bright across the 3 walkable fords
	float eff_depth = depth_t * (1.0 - ford_mask * 0.38);
	vec3 water = mix(shallow_color.rgb, deep_color.rgb, smoothstep(0.08, 0.88, eff_depth));

	// Sunlit shallow caustics net dancing over the pebble bed
	float caustic_a = abs(s1 - s2);
	float caustic = (1.0 - smoothstep(0.02, 0.13, caustic_a)) * (1.0 - eff_depth * 0.35);
	float mid_stroke = smoothstep(0.55, 0.67, brush);
	float lit_ripple = smoothstep(0.74, 0.82, brush);
	float ripple_accent = (1.0 - smoothstep(0.015, 0.042, abs(brush - 0.73))) * smoothstep(0.16, 0.55, depth_t);

	water = mix(water, vec3(0.35, 0.64, 0.59), mid_stroke * 0.34);
	water = mix(water, vec3(0.49, 0.65, 0.61), caustic * 0.40);
	water = mix(water, foam_color.rgb, lit_ripple * 0.42);
	water = mix(water, ink_color.rgb, ripple_accent * 0.22);

	// Gentle concentric wading/stand rings around the 3 shallow crossings (-34,0), (0,0), (+34,0)
	float d_stand = min(length(v_world.xz - vec2(-34.0, 0.0)), min(length(v_world.xz), length(v_world.xz - vec2(34.0, 0.0))));
	if (d_stand < 5.6) {
		float ring_wave = fract(d_stand * 0.85 - t * 0.55);
		float wade_ring = (1.0 - smoothstep(0.04, 0.14, abs(ring_wave - 0.5))) * (1.0 - smoothstep(1.4, 5.4, d_stand));
		water = mix(water, foam_color.rgb, wade_ring * 0.46);
	}

	// Soft scalloped shoreline foam & delicate warm-espresso bank contact line
	float scallop = 0.46 + 0.16 * sin(v_world.x * 1.45 + t * 1.55) + 0.10 * cos(v_world.x * 2.9 - t * 1.0) + (s1 - 0.5) * 0.20;
	float foam_body = 1.0 - smoothstep(scallop - 0.08, scallop + 0.05, bank_dist);
	float foam_edge = smoothstep(scallop - 0.10, scallop - 0.01, bank_dist) * (1.0 - smoothstep(scallop + 0.02, scallop + 0.12, bank_dist));
	float echo_line = (1.0 - smoothstep(0.03, 0.08, abs(bank_dist - (scallop + 0.30)))) * step(0.48, s2);

	water = mix(water, foam_color.rgb, clamp(foam_body + echo_line * 0.58, 0.0, 1.0) * 0.78);
	water = mix(water, ink_color.rgb, clamp(foam_edge * 0.45, 0.0, 1.0) * 0.42);

	NORMAL = normalize(vec3((s1 - 0.5) * 0.12, 1.0, (s2 - 0.5) * 0.14));
	ALBEDO = clamp(water, vec3(0.06), vec3(0.65));
	ROUGHNESS = mix(0.18, 0.52, clamp(foam_body * 0.85, 0.0, 1.0));
	SPECULAR = 0.42;
	float base_alpha = mix(shallow_color.a, deep_color.a, eff_depth);
	ALPHA = clamp(base_alpha - ford_mask * 0.08 + foam_body * 0.10, 0.58, 0.90);
}
