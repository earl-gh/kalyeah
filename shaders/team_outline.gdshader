shader_type spatial;
// The team outline round a kid (KidModel.set_team_outline), drawn as the mesh's material_overlay over the authored material.
// A flat 2D edge round the kid's outer shape only (owner): the shell grows across the screen (never toward the camera), so it
// is the same width all round, and it is pushed `behind` metres away from the camera, so the kid's own body always covers it
// and no line shows inside the shape (an arm in front of the chest has none).
render_mode unshaded, cull_front, shadows_disabled, fog_disabled;

uniform vec4 color : source_color = vec4(0.2, 0.5, 1.0, 1.0);
uniform float width = 0.035;
uniform float behind = 0.3;

void vertex() {
	vec4 view_position = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	vec2 across = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xy;
	if (dot(across, across) > 0.000001) {
		view_position.xy += normalize(across) * width;
	}
	view_position.z -= behind;
	POSITION = PROJECTION_MATRIX * view_position;
}

void fragment() {
	ALBEDO = color.rgb;
}
