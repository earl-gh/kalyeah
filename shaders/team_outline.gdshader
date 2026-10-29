shader_type spatial;
// The team outline round a kid (KidModel.set_team_outline), drawn as the mesh's material_overlay. A flat 2D edge round the
// kid's outer shape only (owner): the shell grows across the screen (never toward the camera), so it is the same width all
// round, and it draws only where no kid was drawn (the kid's material marks its pixels in the stencil, KidModel.STENCIL), so
// no line shows inside the shape. It keeps the kid's own depth (no push back), so it never fights the street and flickers.
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled, depth_draw_never;
stencil_mode read, compare_not_equal, 1;

uniform vec4 color : source_color = vec4(0.2, 0.5, 1.0, 1.0);
uniform float width = 0.035;

void vertex() {
	vec4 view_position = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	vec2 across = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xy;
	if (dot(across, across) > 0.000001) {
		view_position.xy += normalize(across) * width;
	}
	POSITION = PROJECTION_MATRIX * view_position;
}

void fragment() {
	ALBEDO = color.rgb;
	// drawn with the see-through things, after every kid has marked the stencil
	ALPHA = color.a;
}
