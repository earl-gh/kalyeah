shader_type spatial;
// The contour round a weapon model (the second pass of `weapon_look.gdshader`): the model pushed out along its normals by a fixed
// width in metres on screen, drawn from the inside, in a deep version of the picture's own colour (never black).
render_mode unshaded, cull_front;

#include "res://scripts/view/art/scissors_steel.gdshaderinc"

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic;
uniform float width = 0.05;
uniform vec3 ink = vec3(0.3, 0.24, 0.4);
// 1: the picture's blades are painted silver first (see weapon_look.gdshader), so the contour round them is a deep blue-grey, not black.
uniform float silver = 0.0;

varying vec2 at;

void vertex() {
	at = VERTEX.xy;
	float scale = (length(MODEL_MATRIX[0].xyz) + length(MODEL_MATRIX[1].xyz) + length(MODEL_MATRIX[2].xyz)) / 3.0;
	VERTEX += normalize(NORMAL) * width / scale;
}

void fragment() {
	vec3 colour = texture(albedo_texture, UV).rgb;
	if (silver > 0.5) {
		colour = steel_blade(colour, at);
	}
	ALBEDO = colour * ink;
}
