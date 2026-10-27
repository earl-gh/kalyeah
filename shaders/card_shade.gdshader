shader_type canvas_item;
// The mode-coloured shade at the bottom of a card: clear at the top, the colour at the bottom, and only where the printed
// layer is still there (not on the worn brown edge). See card_wear.gdshaderinc.
#include "res://shaders/card_wear.gdshaderinc"

uniform vec4 accent : source_color = vec4(0.29, 0.63, 1.0, 1.0);
uniform float share = 0.36;

void fragment() {
	vec2 position = UV * rect_size;
	vec3 masks = wear_masks(position);
	vec2 folds = fold_info(position);
	float strength = smoothstep(1.0 - share, 1.0, UV.y);
	// the shade does not hide a fold: a corner folded over is clear of it and a crease shows through it
	vec3 colour = accent.rgb * (1.0 - 0.55 * folds.y);
	COLOR = vec4(colour, accent.a * strength * masks.x * masks.y * (1.0 - folds.x) * (1.0 - 0.35 * folds.y));
}
