shader_type canvas_item;
// The mode-coloured shade at the bottom of a card: clear at the top, the colour at the bottom, and only where the printed layer is
// still there (not on the worn brown corners). See card_wear.gdshaderinc.
#include "res://shaders/card_wear.gdshaderinc"

uniform vec4 accent : source_color = vec4(0.29, 0.63, 1.0, 1.0);
uniform float share = 0.36;

void fragment() {
	float strength = smoothstep(1.0 - share, 1.0, UV.y);
	COLOR = vec4(accent.rgb, accent.a * strength * print_left(UV * rect_size));
}
