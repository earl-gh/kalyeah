shader_type spatial;
// Master stylized spatial VFX shader for sunlit 90s province hero actions, canal splashes,
// wading ripples, vault trails, and Bagsak rings (gl_compatibility-safe, vertex-colored, zero textures).
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float phase : hint_range(0.0, 1.0) = 0.0;
uniform float soft_fade : hint_range(0.0, 1.0) = 1.0;
uniform float fresnel_softness : hint_range(0.0, 1.0) = 0.16;
uniform float sunlit_lift : hint_range(0.0, 0.4) = 0.08;
uniform float scroll_speed : hint_range(-4.0, 4.0) = 0.9;

varying vec3 v_world;

float hash21(vec2 p) {
	p = fract(p * vec2(127.1, 311.7));
	p += dot(p, p + 34.23);
	return fract(p.x * p.y);
}

float vfx_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x),
		mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x),
		u.y
	);
}

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = mod(TIME, 120.0);
	float stroke = vfx_noise(v_world.xz * 2.4 + vec2(-t * scroll_speed, v_world.y * 1.8));
	// Keep dark espresso edge vertices crisp while lifting bright core vertices with a warm sunlit sheen
	float luma = dot(COLOR.rgb, vec3(0.299, 0.587, 0.114));
	float core_mask = smoothstep(0.28, 0.72, luma);
	vec3 col = COLOR.rgb * tint.rgb;
	col = mix(col, mix(col, vec3(1.0, 0.97, 0.88), sunlit_lift + stroke * 0.06), core_mask);

	// Optional grazing-angle feather when vertex normals are present (defaults to 1.0 on flat baked ribbons)
	float n_len = length(NORMAL);
	float ndv = n_len > 0.001 ? abs(dot(NORMAL / n_len, VIEW)) : 1.0;
	float rim_keep = mix(1.0, smoothstep(0.04, 0.32, ndv), fresnel_softness);

	// Smooth painterly tail erode in the final 35% of lifetime without punching holes early
	float tail_erode = 1.0 - smoothstep(0.68, 1.0, phase) * (1.0 - stroke) * 0.32;
	ALBEDO = clamp(col, vec3(0.0), vec3(1.0));
	ALPHA = clamp(COLOR.a * tint.a * soft_fade * rim_keep * tail_erode, 0.0, 1.0);
}
