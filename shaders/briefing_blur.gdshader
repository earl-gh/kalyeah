shader_type canvas_item;
// The match briefing's backdrop: the match behind it, blurred and dimmed. The blur is a ring of taps over the screen's own mipmaps
// (the Compatibility renderer builds them for `hint_screen_texture`), so it stays cheap on a phone.

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float blur_lod : hint_range(0.0, 6.0) = 3.0;
uniform float spread = 3.0;
uniform vec4 dim : source_color = vec4(0.01, 0.03, 0.08, 0.55);

void fragment() {
	vec2 px = SCREEN_PIXEL_SIZE * spread;
	vec3 sum = textureLod(screen_tex, SCREEN_UV, blur_lod).rgb * 2.0;
	for (int i = 0; i < 8; i++) {
		float angle = float(i) * 0.7853982;
		sum += textureLod(screen_tex, SCREEN_UV + vec2(cos(angle), sin(angle)) * px * float(1 + i % 2), blur_lod).rgb;
	}
	vec3 blurred = sum / 10.0;
	COLOR = vec4(mix(blurred, dim.rgb, dim.a), 1.0);
}
