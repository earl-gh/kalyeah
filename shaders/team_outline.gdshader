shader_type spatial;
// Harmonious outer team outline round every kid and scissors blade (KidModel.set_team_outline / ScissorsRig),
// drawn as material_overlay outside stencil 1 so it frames the inner warm-espresso outline cleanly on 0.82x kids.
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled, depth_draw_never;
stencil_mode read, compare_not_equal, 1;

uniform vec4 color : source_color = vec4(0.2, 0.5, 1.0, 1.0);
uniform float width = 0.026;

varying float v_edge_soft;

void vertex() {
	vec4 view_position = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float dist = max(-view_position.z, 0.1);
	// Mild distance stabilization keeps the outer team rim balanced beside the inner espresso line
	// both in 3rd-person close-up and across the walkable mid canal.
	float dist_scale = mix(1.0, clamp(dist / 8.5, 0.82, 1.36), 0.32);
	vec3 view_n = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	vec2 across = view_n.xy;
	float across_len = length(across);
	v_edge_soft = clamp(across_len, 0.0, 1.0);
	if (across_len > 0.0005) {
		view_position.xy += (across / across_len) * (width * dist_scale);
	}
	POSITION = PROJECTION_MATRIX * view_position;
}

void fragment() {
	// Subtle sunlit edge lift keeps the team aura lively and crisp without harsh neon banding.
	vec3 sunlit_team = mix(color.rgb, mix(color.rgb, vec3(1.0, 0.98, 0.94), 0.12), v_edge_soft * 0.5);
	ALBEDO = sunlit_team;
	ALPHA = color.a * mix(0.86, 0.96, v_edge_soft);
}
