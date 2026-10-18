shader_type spatial;
// A painted-looking puddle for the play area (no image files): a darker wet rim round a flat sky-
// blue pool, a ring of ripple and a soft white glint, the same chunky two-tone look as the models.
// uv is the position in the puddle: 0 at the middle, length 1 at its edge.
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_lambert_wrap, specular_schlick_ggx;

uniform vec3 rim_color : source_color = vec3(0.17, 0.2, 0.27);
uniform vec3 deep_color : source_color = vec3(0.26, 0.5, 0.78);
uniform vec3 sky_color : source_color = vec3(0.58, 0.8, 0.98);
uniform vec3 glint_color : source_color = vec3(1.0, 0.97, 0.88);

void fragment() {
	float r = length(UV);
	float edge = 1.0 - smoothstep(0.93, 1.0, r);
	float wet = smoothstep(0.66, 0.9, r);
	vec3 water = mix(sky_color, deep_color, smoothstep(0.0, 0.8, r));
	vec3 color = mix(water, rim_color, wet * 0.85);
	// one thin lighter ripple ring
	color = mix(color, sky_color, (1.0 - smoothstep(0.0, 0.035, abs(r - 0.5))) * 0.4);
	// a glint where the low gold sun catches the water
	float glint = 1.0 - smoothstep(0.0, 0.16, length((UV - vec2(-0.28, -0.22)) * vec2(1.0, 1.7)));
	color = mix(color, glint_color, glint * 0.9);
	ALBEDO = color;
	ROUGHNESS = 0.2;
	SPECULAR = 0.6;
	ALPHA = edge * mix(0.82, 0.94, 1.0 - wet);
}
