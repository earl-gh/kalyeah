shader_type spatial;
// The painted light of a weapon model (owner: nearest to Clash Royale's texture): the picture's own colours, lighter on the faces that
// look up and deeper and cooler on the ones that look down, never grey or black, with no gloss. Light comes from straight above in
// the world (not from the scene's lights), so every weapon, flying or tumbling, is lit by the one rule.
render_mode unshaded, cull_back;

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic;
uniform vec4 albedo_color : source_color = vec4(1.0);
// What the underside keeps of its colour per channel: less red, most blue, so a shadow goes violet.
uniform vec3 shadow_tint = vec3(0.62, 0.58, 0.82);
// The top face is lifted by this much, never past 1.
uniform float top_boost = 1.12;
// Where along "looking down" to "looking up" the painted light turns over.
uniform vec2 turn = vec2(-0.55, 0.65);

varying float looks_up;

void vertex() {
	looks_up = normalize(mat3(MODEL_MATRIX) * NORMAL).y;
}

void fragment() {
	vec3 colour = texture(albedo_texture, UV).rgb * albedo_color.rgb;
	vec3 lit = min(colour * top_boost, vec3(1.0));
	vec3 dark = colour * shadow_tint;
	ALBEDO = mix(dark, lit, smoothstep(turn.x, turn.y, looks_up));
}
