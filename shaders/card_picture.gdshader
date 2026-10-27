shader_type canvas_item;
// A mode card's picture as an old teks card: aged and grainy print, two creases, a rim of rubbed-white where the print has worn
// away and the brown paper under it showing at the sides and corners. See card_wear.gdshaderinc.
#include "res://shaders/card_wear.gdshaderinc"

uniform vec2 cover = vec2(1.0); // the share of the picture shown across and down (it is cropped to fit the card)

const vec3 KRAFT = vec3(0.82, 0.67, 0.48); // lighter brown paper
const vec3 BARE = vec3(0.93, 0.89, 0.8);

// A fold: darkened along the crease, lit on one side of it, and the print cracked and whitened here and there along it. `weight`
// fades it out away from the corner it belongs to.
vec3 crease(vec3 colour, vec2 position, vec2 centre, vec2 direction, float weight) {
	vec2 tangent = vec2(-direction.y, direction.x);
	float across = dot(direction, position - centre);
	float along = dot(tangent, position - centre);
	float core = exp(-across * across / 12.0);
	float light = exp(-(across - 3.5) * (across - 3.5) / 8.0);
	float crack = step(0.5, value_noise(vec2(along * 0.45, seed * 5.3))) * step(0.45, value_noise(vec2(along * 1.7, seed))) * exp(-across * across / 0.9);
	vec3 folded = colour;
	folded *= 1.0 - 0.75 * core;
	folded += 0.3 * light;
	folded = mix(folded, BARE, crack * 0.8);
	// the flap on the corner's side of the crease sits at a different angle to the light
	folded *= 1.0 - 0.18 * smoothstep(1.0, -6.0, across);
	return mix(colour, folded, weight);
}

// The folds: the corners of an old card get bent. Which corners, how far in the crease is and how it is tilted differ per card.
vec3 corner_folds(vec3 colour, vec2 position) {
	for (int k = 0; k < 4; k++) {
		vec2 corner;
		vec2 inward;
		float reach;
		float strength;
		bool flap;
		if (!fold_corner(k, corner, inward, reach, strength, flap)) {
			continue;
		}
		vec2 line = corner + inward * reach;
		float fade = smoothstep(reach * 2.6, reach * 0.5, length(position - corner));
		colour = crease(colour, position, line, inward, fade * strength);
		// a corner folded right over shows the back of the paper, shaded where it bends
		if (flap) {
			float across = dot(inward, position - line);
			vec3 back = vec3(0.93, 0.88, 0.78) * (0.93 + 0.07 * value_noise(position * 1.1 + seed));
			back *= 1.0 - 0.3 * exp(across / 10.0);
			colour = mix(colour, back, smoothstep(0.8, -0.8, across) * 0.92);
		}
	}
	return colour;
}

void fragment() {
	vec2 position = UV * rect_size;
	vec3 masks = wear_masks(position);
	vec3 print = texture(TEXTURE, 0.5 + (UV - 0.5) * cover).rgb;
	// aged: grainy and yellowed
	float grain = 0.5 * value_noise(position * 0.9 + seed) + 0.5 * value_noise(position * 2.7 + seed * 2.0);
	print *= 0.9 + 0.2 * grain;
	print = mix(print, print * vec3(1.08, 1.0, 0.85), 0.4);
	// the corners are bent, a different set on every card
	print = corner_folds(print, position);
	// rubbed-white specks where the print has been handled, mostly near the edges
	float speck = step(0.9, value_noise(position * 1.6 + seed * 5.0)) * smoothstep(70.0, 0.0, min(min(position.x, rect_size.x - position.x), min(position.y, rect_size.y - position.y)));
	print = mix(print, BARE, speck * 0.5);
	// the worn edge: a rim of rubbed-white, then the brown paper with its fibres, darker where the print's lip casts a shadow
	print = mix(print, BARE, 0.6 * (1.0 - smoothstep(0.0, 6.0, masks.z)) * step(0.0, masks.z));
	float fibres = 0.75 + 0.25 * value_noise(vec2(position.x * 0.5, position.y * 0.07) + seed * 9.0) + 0.1 * value_noise(position * 1.3);
	vec3 paper = KRAFT * fibres * (0.84 + 0.16 * smoothstep(-6.0, 0.0, masks.z));
	paper = mix(paper, KRAFT * 1.06, smoothstep(0.0, 4.0, -masks.z) * 0.4);
	COLOR = vec4(mix(paper, print, masks.x), masks.y);
}
