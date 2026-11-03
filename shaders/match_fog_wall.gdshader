shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;
// The fog beyond the street's two ends (owner, 2026-11-03): a wall standing across each end past its boundary row, and (at the end near the
// camera) a strip of ground beyond the wall. They are real things in the street, so the boundary row hides the fog or is covered by it by where
// the fog stands: at the end FAR from the camera (the top of the screen) the wall is behind the boundary pictures and props, which stand clear of
// it, and the fog thickens upward above them; at the end NEAR the camera (the bottom of the screen) the fog is in front of the boundary and
// covers it, lightly at first, thickening toward the edge of the screen (the card tray), and never rises into the play area.
// What matters is how far past the play area's end line a bit of fog is ON THE SCREEN (`beyond`): for the camera's tilt (45 degrees) that is
// the fog's ground distance beyond the end line, plus its height at the far end (it looks higher up the screen) or minus its height at the near
// end (it looks lower down). The fade is a smooth function of that distance alone, so both ends are built the same and the walls and the ground
// strip join without a line. Which end it is is read from the camera each frame (the street swings half way round in Tumbang Preso).
// White (owner), nearly opaque where it is thickest, a smooth fade.

uniform vec4 fog_color : source_color = vec4(0.97, 0.98, 1.0, 1.0);
uniform float strength = 0.96;
// The far end: clear up to `far_start` metres beyond the end line on the screen (just above the boundary pictures), full from `far_full`.
uniform float far_start = 8.3;
uniform float far_full = 12.5;
// The near end: clear up to `near_start` metres beyond the end line on the screen, and it only reaches its (lighter) `near_strength` at the edge of
// the card tray, `near_full` metres out: the bottom fog is lighter than the top's, because it sits on bare ground next to the bright play area and
// over the props, where the top's mostly stands above the pictures behind the header (owner: it was visually too much at the bottom).
uniform float near_start = 1.0;
uniform float near_full = 7.0;
uniform float near_strength = 0.75;
// Half the street's length: where the end line is.
uniform float lane_half_length = 24.0;
// The middle of the street, to tell which end the fog is at.
uniform vec3 street_center = vec3(0.0);
// 1 for the strip of ground (it only exists at the near end), 0 for a wall.
uniform float is_ground = 0.0;

varying float beyond;
varying float is_far;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float fog_depth = (VIEW_MATRIX * vec4(MODEL_MATRIX[3].xyz, 1.0)).z;
	float street_depth = (VIEW_MATRIX * vec4(street_center, 1.0)).z;
	// the camera looks down -z: the farther from it, the more negative
	is_far = fog_depth < street_depth ? 1.0 : 0.0;
	float past_line = abs(world.z) - lane_half_length;
	beyond = past_line + (is_far > 0.5 ? world.y : -world.y);
}

void fragment() {
	if (is_far > 0.5 && is_ground > 0.5) {
		discard;
	}
	float far_alpha = smoothstep(far_start, far_full, beyond);
	float near_alpha = smoothstep(near_start, near_full, beyond);
	ALBEDO = fog_color.rgb;
	ALPHA = is_far > 0.5 ? strength * far_alpha : near_strength * near_alpha;
}
