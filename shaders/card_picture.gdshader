shader_type canvas_item;
// A mode card's picture as an old teks card: aged, grainy print, and at the corners (very slightly along the sides too) the print
// worn away like real cardstock: first to the thin light tan paper under the ink and, nearest the edge, to the darker brown paperboard core (two layers of wear), with a pale fringe of loose fibres on the very edge; every corner has its own slight radius. See
// card_wear.gdshaderinc.
#include "res://shaders/card_wear.gdshaderinc"

uniform vec2 cover = vec2(1.0); // the share of the picture shown across and down (it is cropped to fit the card)

const vec3 TAN = vec3(0.7321, 0.63, 0.4682);   // the thin light paper under the ink (the first layer of wear): lightness grade 64 of 100
const vec3 CORE = vec3(0.6359, 0.4525, 0.2935);  // the darker brown paperboard core beneath it (nearest the edge): lightness grade 48 of 100
const vec3 FRINGE = vec3(0.80, 0.70, 0.56); // loose pale fibres on the very edge of a worn core

void fragment() {
	vec2 position = UV * rect_size;
	vec3 print = texture(TEXTURE, 0.5 + (UV - 0.5) * cover).rgb;
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
	COLOR = vec4(mix(brown, print, layers.x), card_alpha(position));
}
