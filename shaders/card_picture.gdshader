shader_type canvas_item;
// A mode card's picture as an old teks card: aged, grainy print, and at the corners the print worn away to the brown paper under
// it (one layer only, the card's outline stays straight). See card_wear.gdshaderinc.
#include "res://shaders/card_wear.gdshaderinc"

uniform vec2 cover = vec2(1.0); // the share of the picture shown across and down (it is cropped to fit the card)

const vec3 KRAFT = vec3(0.82, 0.67, 0.48); // the brown paper under the print (light)

void fragment() {
	vec2 position = UV * rect_size;
	vec3 print = texture(TEXTURE, 0.5 + (UV - 0.5) * cover).rgb;
	// aged: grainy
	float grain = 0.5 * value_noise(position * 0.9 + seed) + 0.5 * value_noise(position * 2.7 + seed * 2.0);
	print *= 0.93 + 0.14 * grain;
	float fibres = 0.82 + 0.18 * value_noise(vec2(position.x * 0.5, position.y * 0.07) + seed * 9.0) + 0.06 * value_noise(position * 1.3);
	COLOR = vec4(mix(KRAFT * fibres, print, print_left(position)), 1.0);
}
