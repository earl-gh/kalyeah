shader_type canvas_item;
// Tucks a fan of cards into the horizontal card they sit on (the Cards hub): the pictures are cut by the card's own rounded shape at its
// bottom and its sides, with an anti-aliased edge, and are not cut at all above the card's top edge (the box is open upwards), so the fan
// sticks out over the top and looks pushed into a pocket at the bottom.
//
// `to_card` takes a point of the canvas (the same space as a Control's global position) into the card's own pixels (0,0 = its top left),
// so the mask follows the card wherever a page slides it and however a press squeezes it. `inset` keeps the card's border in front of the
// fan: the fan stops at the border's inner edge.
uniform mat4 to_card;
uniform vec2 card_size = vec2(656.0, 210.0);
uniform float radius = 24.0;
uniform float inset = 3.0;
// how far the open top reaches above the card (pixels): more than any fan is tall
uniform float open_top = 600.0;

varying vec2 canvas_pos;

void vertex() {
	canvas_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

float rounded_box(vec2 p, vec2 half_size, float r) {
	vec2 q = abs(p) - half_size + vec2(r);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - r;
}

void fragment() {
	vec2 p = (to_card * vec4(canvas_pos, 0.0, 1.0)).xy;
	// the card's box, grown upwards by `open_top`, shrunk by the border at the sides and the bottom
	vec2 half_size = vec2(card_size.x * 0.5 - inset, (card_size.y + open_top) * 0.5 - inset * 0.5);
	vec2 centre = vec2(card_size.x * 0.5, (card_size.y - open_top) * 0.5 - inset * 0.5);
	float d = rounded_box(p - centre, half_size, max(radius - inset, 0.0));
	float edge = max(fwidth(d), 0.0001);
	COLOR.a *= clamp(0.5 - d / edge, 0.0, 1.0);
}
