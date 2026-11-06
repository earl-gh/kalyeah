shader_type canvas_item;
// Langit Lupa's brownout (docs/GDD.md "Langit Lupa"): the screen goes dark except a soft circle round your own kid.

uniform vec2 rect_size = vec2(720.0, 1280.0);
uniform vec2 hole = vec2(360.0, 640.0);
uniform float radius = 200.0;
uniform float feather = 60.0;
uniform float strength : hint_range(0.0, 1.0) = 0.0;
uniform vec4 night : source_color = vec4(0.02, 0.03, 0.08, 0.9);

void fragment() {
	float distance_px = length(UV * rect_size - hole);
	float dark = smoothstep(radius - feather, radius, distance_px);
	COLOR = vec4(night.rgb, night.a * dark * strength);
}
