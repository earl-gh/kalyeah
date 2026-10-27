// The worn look of an old Filipino teks card, shared by the mode card's picture and its coloured shade: the printed layer has
// worn away at the sides and corners and shows the brown paper under it, the very rim is nibbled, and everything is made from
// noise so each card (its `seed`) wears differently. Sizes are in the card's pixels (`rect_size`).

uniform vec2 rect_size = vec2(385.0, 550.0);
uniform float seed = 1.0;

const float WEAR_SIDE = 6.5;     // how far the print wears away along the sides (varies a lot along them)
const float WEAR_CORNER = 24.0;  // and at the corners
const float NIBBLE = 2.6;        // how far the card's own rim is chewed

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

// x: how much of the printed layer is left here (0 worn away, 1 whole), y: how much of the card is left (0 chewed off), z: how
// many pixels the printed layer's edge is from here (negative where it is worn away).
vec3 wear_masks(vec2 position) {
	float edge = min(min(position.x, rect_size.x - position.x), min(position.y, rect_size.y - position.y));
	vec2 near = min(position, rect_size - position);
	float corner = length(near);
	float broad = fbm3(position * 0.04 + seed * 7.1);
	float fine = fbm3(position * 0.17 + seed * 3.7);
	float corner_weight = smoothstep(80.0, 0.0, corner);
	float depth = WEAR_SIDE * (0.2 + 1.9 * broad) + WEAR_CORNER * corner_weight * (0.4 + 1.1 * fine);
	float nibble = NIBBLE * fine * (0.5 + 2.2 * corner_weight);
	float card = smoothstep(nibble - 0.6, nibble + 0.9, edge);
	float ink = smoothstep(depth - 1.0, depth + 1.0, edge);
	return vec3(ink, card, edge - depth);
}
