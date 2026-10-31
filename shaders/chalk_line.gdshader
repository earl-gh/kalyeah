shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled;
// A line of chalk drawn on the street (Tumbang Preso's throw line and far line, Patintero's lines): code-drawn until the owner's chalk decal.
// The stroke wanders a little, its edge is ragged, it breaks into pieces where the chalk lifted (so a line is never fully connected, but a
// gap is always short: the line is a boundary), and inside every piece the chalk is grainy, as if it skipped over the rough cement.
// It is drawn on a flat quad: UV.x runs along the line (`length_m` metres), UV.y across it (`width_m` metres).

uniform vec4 chalk : source_color = vec4(1.0, 1.0, 1.0, 0.95);
uniform float length_m = 20.0;
uniform float width_m = 0.3;
// Differs for every line, so no two lines break in the same places.
uniform float seed = 0.0;
// How much of the line is bare: 0 is a solid stroke, 1 is nearly all gaps.
uniform float breaks : hint_range(0.0, 1.0) = 0.35;

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 metres = vec2(UV.x * length_m, (UV.y - 0.5) * width_m);
	// the hand wanders: the stroke's middle drifts a little along the line
	float drift = (value_noise(vec2(metres.x * 0.8 + seed, 3.0)) - 0.5) * 0.4 * width_m;
	float across = abs(metres.y - drift) / (0.5 * width_m);
	// a ragged edge: fine noise eats into the stroke's sides
	float ragged = value_noise(vec2(metres.x * 16.0 + seed, metres.y * 46.0));
	float body = smoothstep(1.0, 0.5, across + (ragged - 0.5) * 0.6);
	// pressure rises and falls along the line; where it dips low the chalk lifts and the line breaks (the dips are quick, so the gaps are short)
	float pressure = 0.5 * value_noise(vec2(metres.x * 1.4 + seed * 1.7, 9.0)) + 0.5 * value_noise(vec2(metres.x * 5.0 + seed, 5.0));
	float lift = mix(0.15, 0.36, breaks);
	float piece = smoothstep(lift, lift + 0.04, pressure);
	// grain: speckles of bare cement inside the chalk, more of them where the pressure is light
	float speck = hash(floor(vec2(metres.x * 70.0, metres.y * 70.0)) + seed);
	float fibre = value_noise(metres * 34.0 + seed);
	float grain = smoothstep(0.12, 0.5, 0.45 * speck + 0.55 * fibre + (pressure - 0.5) * 0.9);
	ALBEDO = chalk.rgb;
	ALPHA = chalk.a * body * piece * grain;
}
