shader_type spatial;
render_mode unshaded, cull_back;
// The floor round the play area (owner, 2026-11-03), made only of the owner's floor picture, with no dark tint anywhere:
//  - past the two SIDES the picture's own sidewalk strip (the part of the picture the play area crops off at each side) is copied outward,
//    mirrored back and forth so the copies join without a seam;
//  - past the TOP and the BOTTOM the whole picture is copied, copy after copy, lined up with the play area;
//  - where a copy meets the play area (or another copy) the picture's bottom edge does not match its top edge (stains, shade), so each copy is
//    colour-corrected near that seam: the difference between the two edges' blurred colours is added, fading away over `seam_blend` of the
//    copy's height, so the copy's edge takes the colour of the edge it touches.
// Unshaded: the picture is the colour (docs/ART.md "Light and colour by asset class").

uniform sampler2D floor_picture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
// The play area's half width and half length (metres).
uniform vec2 lane_half = vec2(12.0, 24.0);
// The share of the picture's width cropped off at each side of the play area (its sidewalk strip).
uniform float edge_u = 0.0556;
// How far into a copy (a share of its height) the seam correction reaches, and the blur (mip level) its edge colours are read at.
uniform float seam_blend = 0.22;
uniform float seam_lod = 5.0;

varying vec2 ground;

void vertex() {
	ground = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

// 0 up to `width` and back, over and over: a triangle wave.
float mirrored(float t, float width) {
	float m = mod(t, 2.0 * width);
	return m <= width ? m : 2.0 * width - m;
}

void fragment() {
	float share = 1.0 - 2.0 * edge_u;
	float across = ground.x / (lane_half.x * 2.0) + 0.5;
	float along = (ground.y + lane_half.y) / (lane_half.y * 2.0);
	float u = edge_u + across * share;
	if (across < 0.0) {
		u = edge_u - mirrored(-across * share, edge_u);
	} else if (across > 1.0) {
		u = 1.0 - edge_u + mirrored((across - 1.0) * share, edge_u);
	}
	float copy = floor(along);
	float v = along - copy;
	// the unfolded coordinates give the sampler its slopes, so the folds and wraps leave no seam line
	vec2 slope_x = dFdx(vec2(edge_u + across * share, along));
	vec2 slope_y = dFdy(vec2(edge_u + across * share, along));
	vec3 colour = textureGrad(floor_picture, vec2(u, v), slope_x, slope_y).rgb;
	if (copy != 0.0) {
		vec2 size = vec2(textureSize(floor_picture, 0));
		float reach = exp2(seam_lod) * 0.5 / size.y;
		vec3 top = textureLod(floor_picture, vec2(u, reach), seam_lod).rgb;
		vec3 bottom = textureLod(floor_picture, vec2(u, 1.0 - reach), seam_lod).rgb;
		if (copy < 0.0) {
			// above the play area: this copy's bottom edge touches the play area's top edge
			colour += (top - bottom) * smoothstep(1.0 - seam_blend, 1.0, v);
		} else {
			// below it: this copy's top edge touches the play area's bottom edge
			colour += (bottom - top) * (1.0 - smoothstep(0.0, seam_blend, v));
		}
	}
	ALBEDO = clamp(colour, 0.0, 1.0);
}
