shader_type canvas_item;
// Puts a line of text in perspective, as if it stood on the ground and leaned back: its upper part is pushed away from the viewer, so it
// is narrower and a little shorter than its lower part, and the letters read as tall and solid instead of flat. Every vertex of the
// text (letters, outline and shadow) is moved the same way, so it works on any Label. See `TitleScreen._place_title`.

uniform vec2 rect_size = vec2(720.0, 80.0); // the label's size in pixels
uniform float top_scale : hint_range(0.3, 1.0) = 0.66; // how wide the very top is next to the very bottom
uniform float squash : hint_range(0.5, 1.0) = 0.97;    // how much shorter the leaning-back text is (hardly: it stays tall)

void vertex() {
	// the letters fill the middle of the label (not its whole height): 0 is the top of the letters, 1 their bottom
	float t = clamp((VERTEX.y / rect_size.y - 0.14) / 0.72, 0.0, 1.0);
	float far = mix(top_scale, 1.0, t); // the further up, the further away
	VERTEX.x = rect_size.x * 0.5 + (VERTEX.x - rect_size.x * 0.5) * far;
	VERTEX.y = rect_size.y - (rect_size.y - VERTEX.y) * squash;
}
