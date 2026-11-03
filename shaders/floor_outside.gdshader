shader_type spatial;
render_mode unshaded, cull_back;
// The floor round the play area. Past the two sides it is plain pavement (no dark part); past the top and the bottom it is the owner's
// floor picture, shading smoothly darker the farther it is from the play area's edge, so there is no hard line where the bright play
// area stops. Unshaded: the picture is the colour (docs/ART.md "Light and colour by asset class").

uniform sampler2D floor_picture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec2 picture_scale = vec2(1.0);
uniform vec2 picture_offset = vec2(0.0);
// The play area's half width and half length (metres).
uniform vec2 lane_half = vec2(12.0, 24.0);
// Past the sides: the pavement's colour, and how far (m) the picture melts into it.
uniform vec3 side_color : source_color = vec3(0.58, 0.5, 0.44);
uniform float side_blend = 0.8;
// Past the top and the bottom: how far (m) the shade takes to reach `far_shade`, and how dark it gets (1 = not at all).
uniform float fade_length = 9.0;
uniform float far_shade = 0.74;

varying vec2 ground;

void vertex() {
	ground = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
	UV = UV * picture_scale + picture_offset;
}

void fragment() {
	vec3 picture = texture(floor_picture, UV).rgb;
	float beyond_x = max(abs(ground.x) - lane_half.x, 0.0);
	float beyond_z = max(abs(ground.y) - lane_half.y, 0.0);
	vec3 colour = mix(picture, side_color, smoothstep(0.0, side_blend, beyond_x));
	float ramp = smoothstep(0.0, fade_length, beyond_z);
	ALBEDO = colour * mix(1.0, far_shade, ramp);
}
