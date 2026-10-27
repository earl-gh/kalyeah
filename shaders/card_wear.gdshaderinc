// The worn look of an old Filipino teks card, shared by the mode card's picture, its coloured shade and its tint: at the corners,
// and very slightly along the left and right sides, the printed layer has worn away to the brown paper under it. One layer only
// (print or brown paper, nothing between), every corner of the card has its own slight radius, and noise
// makes each card (its `seed`) wear differently. Sizes are in the card's pixels (`rect_size`).

uniform vec2 rect_size = vec2(385.0, 550.0);
uniform float seed = 1.0;
uniform vec4 radii = vec4(0.0); // the slight radius of each corner: top left, top right, bottom right, bottom left (set per card by CardFace)

const float WEAR_CORNER = 17.0;  // and at the corners (a little: that is where an old card wears)
const float WEAR_SIDE = 3.4;     // how far the print can wear away along the left and right sides (very slight)

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x), mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm3(vec2 p) {
	float total = 0.0;
	float amount = 0.5;
	for (int octave = 0; octave < 3; octave++) {
		total += amount * value_noise(p);
		p *= 2.03;
		amount *= 0.5;
	}
	return total;
}

// How much of the printed layer is left here: 0 where it has worn away to the brown paper, 1 where it is whole.
float print_left(vec2 position) {
	float edge = min(min(position.x, rect_size.x - position.x), min(position.y, rect_size.y - position.y));
	vec2 near = min(position, rect_size - position);
	float corner = length(near);
	float fine = fbm3(position * 0.19 + seed * 3.7);
	float corner_weight = smoothstep(90.0, 0.0, corner);
	// only the corners wear (the sides stay whole)
	float depth = WEAR_CORNER * corner_weight * corner_weight * (0.35 + 1.3 * fine);
	float left_right = min(position.x, rect_size.x - position.x);
	// a different, very slight wear along the left and the right side of every card
	float side_amount = position.x < rect_size.x * 0.5 ? 0.3 + hash21(vec2(seed, 2.3)) : 0.3 + hash21(vec2(seed, 8.6));
	float side_depth = WEAR_SIDE * side_amount * fbm3(vec2(position.y * 0.07 + seed * 5.3, position.x * 0.2)) * 1.6;
	return min(smoothstep(depth - 0.8, depth + 0.8, edge), smoothstep(side_depth - 0.6, side_depth + 0.6, left_right));
}


// The card's own outline: every corner has its own slight radius (a different one on each corner of each card), so none is sharp
// and none is very round. 1 inside the card, 0 outside, smooth at the edge.
float corner_cut(vec2 offset, float radius) {
	if (offset.x < radius && offset.y < radius) {
		return 1.0 - smoothstep(-0.7, 0.7, length(offset - vec2(radius)) - radius);
	}
	return 1.0;
}

float card_alpha(vec2 position) {
	vec2 far = rect_size - position;
	return corner_cut(position, radii.x) * corner_cut(vec2(far.x, position.y), radii.y) * corner_cut(far, radii.z) * corner_cut(vec2(position.x, far.y), radii.w);
}
