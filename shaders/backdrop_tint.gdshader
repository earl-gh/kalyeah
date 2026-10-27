shader_type canvas_item;
// The main menu's background picture in the colour of whatever is in focus: the picture's brightness (its diamonds and shading)
// coloured by `tint`, so the background is blue behind the blue card, orange behind the orange one, green behind the green one.

uniform vec3 tint = vec3(0.55, 0.67, 1.0);
uniform float gain = 1.5; // the picture is dark: this keeps the background as bright as the blue one was

void fragment() {
	vec4 picture = texture(TEXTURE, UV);
	float light = dot(picture.rgb, vec3(0.2126, 0.7152, 0.0722));
	COLOR = vec4(vec3(light) * tint * gain, picture.a);
}
