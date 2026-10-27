shader_type canvas_item;
// The yellowish tint over a whole mode card, cut to the card's own outline (the rounded corners). See card_wear.gdshaderinc.
#include "res://shaders/card_wear.gdshaderinc"

uniform vec4 tint : source_color = vec4(1.0, 0.86, 0.45, 0.11);

void fragment() {
	COLOR = vec4(tint.rgb, tint.a * card_alpha(UV * rect_size));
}
