shader_type canvas_item;
// A mode card's picture as an old teks card: aged, grainy print, and at the corners the print worn away to the brown paper under
// it (one layer only, the card's outline stays straight). See card_wear.gdshaderinc.
#include "res://shaders/card_wear.gdshaderinc"

uniform vec2 cover = vec2(1.0); // the share of the picture shown across and down (it is cropped to fit the card)

const vec3 KRAFT = vec3(0.72, 0.56, 0.38); // the brown paper under the print (slightly darker, owner)

void fragment() {
	vec2 position = UV * rect_size;
	vec3 print = texture(TEXTURE, 0.5 + (UV - 0.5) * cover).rgb;
	// aged: grainy
	float grain = 0.5 * value_noise(position * 0.9 + seed) + 0.5 * value_noise(position * 2.7 + seed * 2.0);
	print *= 0.93 + 0.14 * grain;
	float fibres = 0.82 + 0.18 * value_noise(vec2(position.x * 0.5, position.y * 0.07) + seed * 9.0) + 0.06 * value_noise(position * 1.3);
	vec3 colour = mix(KRAFT * fibres, print, print_left(position));
	// the top left corner rolls down, the bottom right one rolls up
	vec2 roll = roll_effect(position);
	COLOR = vec4(colour * (1.0 - roll.x) + roll.y, 1.0);
}
