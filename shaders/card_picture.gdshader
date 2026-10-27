shader_type canvas_item;
// A mode card's picture as an old teks card: aged, grainy print, and at the corners (very slightly along the sides too) the print
// worn away like real cardstock: first to the thin light tan paper under the ink and, nearest the edge, to the darker brown paperboard core (two layers of wear), with a pale fringe of loose fibres on the very edge; every corner has its own slight radius. See
// card_wear.gdshaderinc.
#include "res://shaders/card_wear.gdshaderinc"

uniform vec2 cover = vec2(1.0); // the share of the picture shown across and down (it is cropped to fit the card)
uniform vec4 ground = vec4(0.0); // a card printed on a flat colour (rgb; a = 1) with the picture as a small art on it (the hand's cards); a = 0: the picture is the whole print
uniform vec4 art_rect = vec4(0.0, 0.0, 1.0, 1.0); // where that art sits on the card: x, y, width, height in the card's pixels
uniform float dim : hint_range(0.0, 1.0) = 0.0; // 1 for a card that is not chosen: dimmed with a dark tint (still in colour)

const vec3 DIM_TINT = vec3(0.24, 0.24, 0.27); // what an unchosen card is multiplied by: a dark, nearly black tint

const vec3 TAN = vec3(0.7321, 0.63, 0.4682);   // the thin light paper under the ink (the first layer of wear): lightness grade 64 of 100
const vec3 CORE = vec3(0.6359, 0.4525, 0.2935);  // the darker brown paperboard core beneath it (nearest the edge): lightness grade 48 of 100
const vec3 FRINGE = vec3(0.80, 0.70, 0.56); // loose pale fibres on the very edge of a worn core

void fragment() {
	vec2 position = UV * rect_size;
	vec3 print;
	if (ground.a > 0.0) {
		vec2 art_at = (position - art_rect.xy) / art_rect.zw;
		vec4 art = texture(TEXTURE, clamp(art_at, 0.0, 1.0));
		float on_art = step(0.0, min(art_at.x, art_at.y)) * step(max(art_at.x, art_at.y), 1.0);
		print = mix(ground.rgb, art.rgb, art.a * on_art);
	} else {
		print = texture(TEXTURE, 0.5 + (UV - 0.5) * cover).rgb;
	}
	// aged: grainy
	float grain = 0.5 * value_noise(position * 0.9 + seed) + 0.5 * value_noise(position * 2.7 + seed * 2.0);
	print *= 0.93 + 0.14 * grain;
	// (the fibre texture averages 1, so the layers keep their lightness grades)
	float fibres = (0.82 + 0.18 * value_noise(vec2(position.x * 0.5, position.y * 0.07) + seed * 9.0) + 0.06 * value_noise(position * 1.3)) / 0.94;
	vec2 layers = wear_layers(position);
	vec3 brown = mix(CORE * fibres, TAN * (0.94 + 0.06 * fibres), layers.y);
	// the very edge of the worn core frays: a pale fringe of loose fibres
	float edge = min(min(position.x, rect_size.x - position.x), min(position.y, rect_size.y - position.y));
	brown = mix(brown, FRINGE, (1.0 - layers.y) * (1.0 - smoothstep(0.0, 1.8, edge)) * 0.6);
	vec3 colour = mix(brown, print, layers.x);
	// a dark tint (no black and white) when the card is not the chosen one
	colour *= mix(vec3(1.0), DIM_TINT, dim);
	COLOR = vec4(colour, card_alpha(position));
}
