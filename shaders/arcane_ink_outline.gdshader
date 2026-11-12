shader_type spatial;
// Sunlit 90s Philippine province delicate warm-espresso inverted-hull ink outline.
// gl_compatibility-safe: pure vertex-space silhouette extrusion with grazing-angle taper, gentle distance compensation,
// and subtle organic pen taper so buildings, props, weapons, and 0.82x kids read cleanly without heavy black cages.
render_mode unshaded, cull_front, shadows_disabled;

uniform float ink_width : hint_range(0.0, 0.12) = 0.0075;
uniform vec4 ink_color : source_color = vec4(0.14, 0.11, 0.12, 1.0);
uniform float wobble_amount : hint_range(0.0, 0.6) = 0.08;
uniform float wobble_freq : hint_range(1.0, 40.0) = 13.5;
uniform float distance_comp : hint_range(0.0, 1.0) = 0.38;
uniform float max_distance : hint_range(20.0, 260.0) = 150.0;

varying float v_stroke;

void vertex() {
	vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec4 view_pos = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float dist = max(-view_pos.z, 0.1);

	// Gentle distance compensation keeps 0.82x kids & props crisp up close and mid-ground
	// barangay silhouettes airy at 1280x720 without thickening far houses.
	float dist_factor = mix(0.95, clamp(dist / 10.0, 0.70, 1.95), distance_comp);
	dist_factor *= 1.0 - smoothstep(max_distance * 0.68, max_distance, dist);

	// Subtle spatial pen-pressure modulation (anchored in world space, zero temporal shimmer).
	float w1 = sin(world_pos.x * wobble_freq + world_pos.y * (wobble_freq * 1.37) + world_pos.z * (wobble_freq * 0.83));
	float w2 = cos(world_pos.z * (wobble_freq * 2.13) - world_pos.x * (wobble_freq * 1.61) + world_pos.y * (wobble_freq * 0.91));
	float wobble = w1 * 0.65 + w2 * 0.35;
	float width = ink_width * dist_factor * max(1.0 + wobble_amount * wobble, 0.60);
	v_stroke = wobble * 0.5 + 0.5;

	vec3 view_n = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	float n_len = length(view_n);
	if (n_len > 0.0001) {
		vec3 dir = view_n / n_len;
		float graze = smoothstep(0.05, 0.34, length(dir.xy));
		// Push across screen XY with slight depth bias (-Z) to avoid coplanar z-fighting.
		vec3 hull_push = normalize(vec3(dir.xy, dir.z * 0.18 - 0.001));
		view_pos.xyz += hull_push * (width * graze);
		view_pos.z -= 0.0025;
	}

	POSITION = PROJECTION_MATRIX * view_pos;
}

void fragment() {
	// Warm sunlit dark-espresso ink density variation (preserves warm umber hue without crushing to black).
	vec3 espresso = mix(ink_color.rgb * 0.92, ink_color.rgb * vec3(1.14, 1.08, 1.06), v_stroke * 0.28);
	ALBEDO = clamp(espresso, vec3(0.0), vec3(0.32));
}
