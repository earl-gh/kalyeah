shader_type canvas_item;
// Film finish over the match view (under the HUD): a soft vignette and a fine moving
// grain. Drawn as a see-through overlay, so it never reads the screen back (cheap on
// phones) and never blurs the map.

uniform float vignette = 0.34;
uniform float grain = 0.025;
// The vignette stays behind the top boundary pictures: no dark above this height of the screen (0 at the top, 1 at the bottom), and it
// comes back over `clear_feather` of the screen's height below it.
uniform float clear_above = 0.0;
uniform float clear_feather = 0.04;

void fragment() {
	vec2 centered = UV - vec2(0.5);
	centered.x *= 0.75;
	float dark = smoothstep(0.32, 0.86, length(centered)) * vignette * smoothstep(clear_above, clear_above + clear_feather, UV.y);
	float noise = fract(sin(dot(UV * vec2(1280.0, 720.0) + vec2(TIME * 61.0, TIME * 17.0), vec2(12.9898, 78.233))) * 43758.5453);
	float speck = max(noise - 0.5, 0.0) * grain * 2.0;
	// cool dark corners and a faint grain, as a see-through layer
	COLOR = vec4(0.02, 0.04, 0.1, clamp(dark + speck, 0.0, 1.0));
}
