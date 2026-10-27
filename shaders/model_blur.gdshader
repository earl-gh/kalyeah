shader_type canvas_item;
// A soft blur for a mode model standing behind the chosen one, so it reads as far away (depth of field): a spiral of taps in a disc of
// `radius` texels, with the colour weighted by alpha so the transparent edge of the picture does not darken or fringe.

uniform float radius : hint_range(0.0, 40.0) = 8.0;

const int TAPS = 24;
const float GOLDEN_ANGLE = 2.39996323;

void fragment() {
	vec2 pixel = TEXTURE_PIXEL_SIZE * radius;
	vec4 sum = vec4(0.0);
	float weight_sum = 0.0;
	for (int i = 0; i < TAPS; i++) {
		float t = (float(i) + 0.5) / float(TAPS);
		float angle = float(i) * GOLDEN_ANGLE;
		vec2 offset = vec2(cos(angle), sin(angle)) * sqrt(t) * pixel;
		vec4 sample_colour = texture(TEXTURE, UV + offset);
		float weight = 1.0 - 0.5 * t; // a little heavier toward the middle
		sum += vec4(sample_colour.rgb * sample_colour.a, sample_colour.a) * weight;
		weight_sum += weight;
	}
	sum /= weight_sum;
	COLOR = vec4(sum.a > 0.0001 ? sum.rgb / sum.a : vec3(0.0), sum.a) * COLOR;
}
