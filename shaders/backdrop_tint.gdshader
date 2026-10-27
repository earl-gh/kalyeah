shader_type canvas_item;
// The main menu's background picture. Its own blue is kept: the colour of whatever is in focus only turns the picture's hue (blue
// stays blue behind the blue card, turns orange behind the orange one, green behind the green one), so the picture's own light and
// dark stay. Brightness and saturation are a separate, constant adjustment on top (owner).

uniform float hue_shift = 0.0;   // in turns (0.5 is the opposite colour): how far the focus colour is from the picture's blue
uniform float saturation = 1.2;  // the slight extra saturation
uniform float brightness = 1.0;  // and brightness

vec3 rgb_to_hsv(vec3 c) {
	vec4 k = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, k.wz), vec4(c.gb, k.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv_to_rgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec4 picture = texture(TEXTURE, UV);
	vec3 hsv = rgb_to_hsv(picture.rgb);
	hsv.x = fract(hsv.x + hue_shift);
	vec3 turned = hsv_to_rgb(hsv);
	float grey = dot(turned, vec3(0.2126, 0.7152, 0.0722));
	COLOR = vec4(mix(vec3(grey), turned, saturation) * brightness, picture.a);
}
