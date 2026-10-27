shader_type canvas_item;
// A picture drawn as a flat silhouette: its own alpha (the torn edge and the soft glow of an owner's card picture), in the colour it is
// drawn with (draw_texture_rect's modulate), never multiplied by the picture's colours. The hand's picture cards use it for the gold edge
// of a lifted card, the soft shadow under it, the grey washes and the red flash, so none of them show a box in the card's empty corners.
varying vec4 tint;

void vertex() {
	tint = COLOR;
}

void fragment() {
	COLOR = vec4(tint.rgb, texture(TEXTURE, UV).a * tint.a);
}
