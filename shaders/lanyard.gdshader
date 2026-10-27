shader_type canvas_item;
// One strap of the name tag's lanyard: plain black woven webbing. A soft round across the width (a little lighter in the middle, darker at
// the edges), a very fine weave along its length, a dashed stitch just inside each edge, and a rounded end where it goes into the slot.
// The length is measured up from the end at the slot, so the pieces of one strap line up exactly.

uniform vec2 rect_size = vec2(22.0, 200.0);
uniform vec3 base_colour : source_color = vec3(0.05, 0.05, 0.07);
uniform float stitch_alpha = 0.16;
uniform float edge_alpha = 0.14;

void fragment() {
	float across = UV.x;
	float up = (1.0 - UV.y) * rect_size.y;          // pixels up from the end at the slot
	float from_edge = min(across, 1.0 - across) * rect_size.x;
	// round of the webbing
	float round_light = 1.0 - pow(abs(across - 0.5) * 2.0, 2.0);
	vec3 colour = base_colour * (0.78 + 0.45 * round_light);
	// a fine weave along the length (diagonal ticks)
	float weave = 0.5 + 0.5 * sin((up + across * rect_size.x) * 1.9);
	colour += vec3(0.012) * weave;
	// a dashed stitch 3 px in from each edge
	float stitch_line = 1.0 - smoothstep(0.35, 0.95, abs(from_edge - 3.4));
	float dash = step(0.5, fract(up / 7.0));
	colour += vec3(stitch_alpha) * stitch_line * dash;
	// a thin pale edge catching the light on the very edge
	colour += vec3(edge_alpha) * (1.0 - smoothstep(0.0, 1.2, from_edge));
	// the end that goes into the slot is rounded
	float radius = rect_size.x * 0.5;
	float alpha = 1.0;
	if (up < radius) {
		vec2 p = vec2((across - 0.5) * rect_size.x, up - radius);
		alpha = 1.0 - smoothstep(radius - 0.8, radius + 0.2, length(p));
	}
	COLOR = vec4(colour, alpha);
}
