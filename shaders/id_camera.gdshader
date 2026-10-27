// The camera's picture for the ID photo (IdCamera): upright, mirrored like a selfie for the front camera, and only its middle square.
// A phone camera sends its frames either as RGB or as two planes (brightness Y, then the colour pair Cb Cr); both are shown here.
shader_type canvas_item;

// Y (or the RGB picture), and the Cb Cr plane when `separate`.
uniform sampler2D luma : filter_linear;
uniform sampler2D chroma : filter_linear;
uniform bool separate = false;
uniform bool mirror = false;
// From the screen square to the camera's picture, about their middles: the inverse of the feed's turn (IdCamera.screen_to_picture,
// which the tests check). Android gives that turn as a rotation about the picture's corner, so it is applied about the middle here.
uniform vec2 turn_x = vec2(1.0, 0.0);
uniform vec2 turn_y = vec2(0.0, -1.0);
// The size of the camera's picture, to take the middle square of it.
uniform vec2 picture_size = vec2(1.0, 1.0);

void fragment() {
	vec2 uv = UV;
	if (mirror) {
		uv.x = 1.0 - uv.x;
	}
	uv = turn_x * (uv.x - 0.5) + turn_y * (uv.y - 0.5) + vec2(0.5);
	// the middle square of the picture along its longer side
	if (picture_size.x > picture_size.y) {
		uv.x = 0.5 + (uv.x - 0.5) * picture_size.y / picture_size.x;
	} else {
		uv.y = 0.5 + (uv.y - 0.5) * picture_size.x / picture_size.y;
	}
	vec3 colour = texture(luma, uv).rgb;
	if (separate) {
		float y = texture(luma, uv).r;
		vec2 cbcr = texture(chroma, uv).rg - vec2(0.5);
		colour = vec3(y + 1.402 * cbcr.y, y - 0.344136 * cbcr.x - 0.714136 * cbcr.y, y + 1.772 * cbcr.x);
	}
	COLOR = vec4(clamp(colour, 0.0, 1.0), 1.0);
}
