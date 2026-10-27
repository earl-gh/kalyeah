// The worn look of an old Filipino teks card, shared by the mode card's picture and its coloured shade: at the corners the printed
// layer has worn away to the brown paper under it. One layer only (print or brown paper, nothing between), the card's outline
// stays straight, and a little noise makes each card (its `seed`) wear differently. Sizes are in the card's pixels (`rect_size`).

uniform vec2 rect_size = vec2(385.0, 550.0);
uniform float seed = 1.0;

const float WEAR_CORNER = 17.0;  // and at the corners (a little: that is where an old card wears)

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
	return smoothstep(depth - 0.8, depth + 0.8, edge);
}


// One rolled corner: the card's corner at `corner` curls up or down like rolled paper. The curl starts softly at a slightly
// curved line that meets both edges of the corner (so the effect begins at the edges, not in the middle of the card) and gets
// steeper towards the tip, like a cylinder: no sharp crease. Returns (how much darker, how much lighter) each spot is. A corner
// rolled down (`up` false) darkens towards its tip with a pale ridge on the curl; one rolled up (`up` true) catches the light
// towards its tip and throws a soft shadow on the page beside it.
vec2 roll_corner(vec2 position, vec2 corner, vec2 inward, float reach, bool up) {
	vec2 d = position - corner;
	vec2 tangent = vec2(-inward.y, inward.x);
	float along = dot(tangent, d);
	float across = dot(inward, d) - reach + along * along * 0.0016;
	float curl = clamp(-across / reach, 0.0, 1.0); // 0 where the curl starts, 1 at the tip
	float steep = curl * curl * (3.0 - 2.0 * curl);
	float ridge = exp(-(curl - 0.55) * (curl - 0.55) / 0.03) * step(0.0, curl);
	float beside = step(0.0, across) * exp(-across * across / 260.0); // the page next to the curl
	if (up) {
		return vec2(0.22 * beside * (1.0 - steep) * smoothstep(0.0, 6.0, across) + 0.1 * ridge * curl, 0.2 * steep + 0.05 * ridge);
	}
	return vec2(0.78 * steep + 0.1 * beside * smoothstep(0.0, 4.0, across), 0.24 * ridge);
}

// The two rolled corners of every card: the top left rolls down and the bottom right rolls up (owner).
vec2 roll_effect(vec2 position) {
	vec2 top_left = roll_corner(position, vec2(0.0), normalize(vec2(1.0, 1.1)), 78.0, false);
	vec2 bottom_right = roll_corner(position, rect_size, normalize(vec2(-1.0, -1.1)), 68.0, true);
	return min(top_left + bottom_right, vec2(0.8, 0.4));
}
