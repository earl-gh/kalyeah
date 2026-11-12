shader_type spatial;
// Valorant / Deadlock crisp silhouette team rim round every kid and scissors blade (KidModel.set_team_outline / ScissorsRig),
// drawn as material_overlay outside stencil 1 with grazing-normal taper so interior cloth creases never spike or halo.
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled, depth_draw_never;
stencil_mode read, compare_not_equal, 1;

uniform vec4 color : source_color = vec4(0.2, 0.5, 1.0, 1.0);
uniform float width = 0.024;

varying float v_edge_soft;

void vertex() {
	vec4 view_position = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float dist = max(-view_position.z, 0.1);
	// Balanced Deadlock shoulder close-to-mid distance stabilization: crisp ~1.8 px up close at 4.15 m,
	// clean silhouette read across Tabing-Ilog without thick halos at range.
	float dist_scale = mix(0.92, clamp(dist / 7.6, 0.76, 1.24), 0.30);
	vec3 view_n = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	vec2 across = view_n.xy;
	float across_len = length(across);
	// Smooth silhouette grazing taper prevents front-facing creases (across_len ~ 0) from snapping outward.
	float silhouette_taper = smoothstep(0.06, 0.38, across_len);
	v_edge_soft = silhouette_taper;
	vec2 push_dir = across / max(across_len, 0.0001);
	view_position.xy += push_dir * (width * dist_scale * silhouette_taper);
	view_position.z -= 0.0025;
	POSITION = PROJECTION_MATRIX * view_position;
}

void fragment() {
	// Crisp sunlit team read without harsh neon bloom or muddy edge banding.
	vec3 sunlit_team = mix(color.rgb, mix(color.rgb, vec3(1.0, 0.98, 0.94), 0.10), v_edge_soft * 0.45);
	ALBEDO = sunlit_team;
	ALPHA = color.a * mix(0.84, 0.94, v_edge_soft);
}
