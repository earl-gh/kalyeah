shader_type spatial;
// Valorant x Arcane x 90s Filipino Komiks fake-2D inverted-hull ink outline.
// gl_compatibility-safe: pure vertex-space silhouette extrusion with distance compensation
// and spatial hand-inked pen-pressure wobble (no depth/normal screen textures needed).
render_mode unshaded, cull_front, shadows_disabled;

uniform float ink_width : hint_range(0.0, 0.12) = 0.022;
uniform vec4 ink_color : source_color = vec4(0.09, 0.07, 0.10, 1.0);
uniform float wobble_amount : hint_range(0.0, 0.6) = 0.24;
uniform float wobble_freq : hint_range(1.0, 40.0) = 14.5;
uniform float distance_comp : hint_range(0.0, 1.0) = 0.58;
uniform float max_distance : hint_range(20.0, 260.0) = 150.0;

varying float v_stroke;

void vertex() {
	vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec4 view_pos = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float dist = max(-view_pos.z, 0.1);

	// Distance compensation keeps close props/weapons razor-crisp and mid-ground
	// barangay houses readable at 1280x720 without blobbing far silhouettes.
	float dist_factor = mix(1.0, clamp(dist / 9.0, 0.62, 3.35), distance_comp);
	dist_factor *= 1.0 - smoothstep(max_distance * 0.72, max_distance, dist);

	// Spatial hand-drawn brush-pen pressure wobble (anchored to geometry, zero temporal shimmer).
	float w1 = sin(world_pos.x * wobble_freq + world_pos.y * (wobble_freq * 1.37) + world_pos.z * (wobble_freq * 0.83));
	float w2 = cos(world_pos.z * (wobble_freq * 2.13) - world_pos.x * (wobble_freq * 1.61) + world_pos.y * (wobble_freq * 0.91));
	float wobble = w1 * 0.65 + w2 * 0.35;
	float width = ink_width * dist_factor * max(1.0 + wobble_amount * wobble, 0.35);
	v_stroke = wobble * 0.5 + 0.5;

	vec3 view_n = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	float n_len = length(view_n);
	if (n_len > 0.0001) {
		vec3 dir = view_n / n_len;
		// Push primarily across screen XY and nudge slightly deeper (-Z) to avoid coplanar z-fighting.
		vec3 hull_push = normalize(vec3(dir.xy, dir.z * 0.22));
		view_pos.xyz += hull_push * width;
		view_pos.z -= 0.0025;
	}

	POSITION = PROJECTION_MATRIX * view_pos;
}

void fragment() {
	// Subtle warm-plum charcoal ink density variation while staying crisp dark komiks ink.
	vec3 charcoal = mix(ink_color.rgb * 0.88, ink_color.rgb * vec3(1.16, 1.08, 1.20), v_stroke * 0.35);
	ALBEDO = clamp(charcoal, vec3(0.0), vec3(0.22));
}
