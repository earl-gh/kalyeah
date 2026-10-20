shader_type canvas_item;
// A dim over the whole screen with up to two round holes (the controls being explained), soft at the edge.

uniform vec4 dim : source_color = vec4(0.01, 0.025, 0.06, 0.8);
uniform vec3 spot_a = vec3(0.0); // x, y, radius in the overlay's own pixels (radius 0 = no hole)
uniform vec3 spot_b = vec3(0.0);
uniform float feather = 6.0;

varying vec2 local_position;

void vertex() {
	local_position = VERTEX;
}

void fragment() {
	float hole = 0.0;
	if (spot_a.z > 0.0) {
		hole = max(hole, 1.0 - smoothstep(spot_a.z - feather, spot_a.z, length(local_position - spot_a.xy)));
	}
	if (spot_b.z > 0.0) {
		hole = max(hole, 1.0 - smoothstep(spot_b.z - feather, spot_b.z, length(local_position - spot_b.xy)));
	}
	COLOR = vec4(dim.rgb, dim.a * (1.0 - hole));
}
