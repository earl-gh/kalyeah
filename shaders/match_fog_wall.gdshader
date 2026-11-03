shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;
// A wall of fog standing across one end of the street, just past its boundary row (owner, 2026-11-03). It is a real thing in the street,
// so the boundary row hides it or covers itself with it by where it stands: at the end that is far from the camera (the top of the screen)
// the wall is behind the boundary pictures and props, which stand clear of it, and the fog thickens upward above them; at the end that is
// near the camera (the bottom of the screen) the wall is in front of them and covers them, but only as high as the end line of the play area
// (the fog never reaches into the play area), thickest half way up that height and thinning to nothing at the end line.
// Which end it is is read from the camera each frame (the street swings half way round in Tumbang Preso), so the layering follows the
// view. White (owner), nearly opaque at its thickest, a smooth fade.

uniform vec4 fog_color : source_color = vec4(0.97, 0.98, 1.0, 1.0);
uniform float strength = 0.96;
// The far wall: clear up to `far_start` metres above the ground (the boundary pictures stand about that tall), full from `far_full`.
uniform float far_start = 2.0;
uniform float far_full = 5.5;
// The near wall: thickest `near_peak` metres up, clear from `near_reach` metres up (`MatchFog` sets these so the fog ends on the screen where
// the play area's end line is) and clear at the ground itself.
uniform float near_peak = 0.9;
uniform float near_reach = 1.75;
// The middle of the street, to tell which end the wall is at.
uniform vec3 street_center = vec3(0.0);

varying float height;
varying float is_far;

void vertex() {
	height = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y;
	float wall_depth = (VIEW_MATRIX * vec4(MODEL_MATRIX[3].xyz, 1.0)).z;
	float street_depth = (VIEW_MATRIX * vec4(street_center, 1.0)).z;
	// the camera looks down -z: the farther from it, the more negative
	is_far = wall_depth < street_depth ? 1.0 : 0.0;
}

void fragment() {
	float far_alpha = smoothstep(far_start, far_full, height);
	// (it eases in from the ground, so no hard line shows where the wall stands on it)
	float near_alpha = smoothstep(0.0, near_peak, height) * (1.0 - smoothstep(near_peak, near_reach, height));
	ALBEDO = fog_color.rgb;
	ALPHA = strength * mix(near_alpha, far_alpha, is_far);
}
