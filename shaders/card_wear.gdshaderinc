// The worn look of an old Filipino teks card, shared by the mode card's picture and its coloured shade: the printed layer has
// worn away at the sides and corners and shows the brown paper under it, the very rim is nibbled, and everything is made from
// noise so each card (its `seed`) wears differently. Sizes are in the card's pixels (`rect_size`).

uniform vec2 rect_size = vec2(385.0, 550.0);
uniform float seed = 1.0;

const float WEAR_SIDE = 2.2;     // how far the print wears away along the sides (a little: the sides stay mostly whole)
const float WEAR_CORNER = 32.0;  // and at the corners (a lot: that is where an old card wears)
const float NIBBLE = 2.6;        // how far the card's own rim is chewed (mostly at the corners)

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
	float corner_weight = smoothstep(100.0, 0.0, corner);
	float depth = WEAR_SIDE * (0.15 + 1.7 * broad) + WEAR_CORNER * corner_weight * corner_weight * (0.45 + 1.1 * fine);
	float nibble = NIBBLE * fine * (0.2 + 2.6 * corner_weight);
	float card = smoothstep(nibble - 0.6, nibble + 0.9, edge);
	float ink = smoothstep(depth - 1.0, depth + 1.0, edge);
	return vec3(ink, card, edge - depth);
}


// The bent corners: which corners of this card are folded, where the crease runs and whether the corner is folded right over.
// Returns false for a corner that is not bent. Shared by the picture and the shade, so the shade does not hide a fold.
bool fold_corner(int k, out vec2 corner, out vec2 inward, out float reach, out float strength, out bool flap) {
	float r = hash21(vec2(seed * 3.1, float(k) + 0.5));
	// every card has at least two bent corners (which ones differs), a third and a fourth only sometimes; the first of them is
	// folded right over
	int first = int(floor(hash21(vec2(seed, 9.1)) * 4.0));
	int second = (first + 1 + int(floor(hash21(vec2(seed, 4.4)) * 3.0))) % 4;
	if (r < 0.55 && k != first && k != second) {
		return false;
	}
	corner = vec2(mod(float(k), 2.0), floor(float(k) / 2.0)) * rect_size;
	inward = normalize(rect_size * 0.5 - corner);
	float tilt = (hash21(vec2(seed * 1.3, float(k) + 7.0)) - 0.5) * 0.7;
	inward = vec2(inward.x * cos(tilt) - inward.y * sin(tilt), inward.x * sin(tilt) + inward.y * cos(tilt));
	reach = mix(36.0, 92.0, hash21(vec2(seed * 2.7, float(k) + 3.0)));
	strength = 0.65 + 0.35 * r;
	flap = k == first || hash21(vec2(seed * 4.3, float(k) + 11.0)) > 0.55;
	return true;
}

// x: how much of this spot is a corner folded right over, y: how much it lies on a crease (0 to 1).
vec2 fold_info(vec2 position) {
	vec2 info = vec2(0.0);
	for (int k = 0; k < 4; k++) {
		vec2 corner;
		vec2 inward;
		float reach;
		float strength;
		bool flap;
		if (!fold_corner(k, corner, inward, reach, strength, flap)) {
			continue;
		}
		float across = dot(inward, position - (corner + inward * reach));
		float fade = smoothstep(reach * 2.6, reach * 0.5, length(position - corner));
		if (flap) {
			info.x = max(info.x, smoothstep(0.8, -0.8, across));
		}
		info.y = max(info.y, exp(-across * across / 12.0) * fade * strength);
	}
	return info;
}
