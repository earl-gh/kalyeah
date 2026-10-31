shader_type spatial;
// The painted light of a weapon model (owner: nearest to Clash Royale's texture): the picture's own colours, lighter on the faces that
// look up and deeper and cooler on the ones that look down, never grey or black, with no gloss. Light comes from straight above in
// the world (not from the scene's lights), so every weapon, flying or tumbling, is lit by the one rule.
render_mode unshaded, cull_back;

#include "res://scripts/view/art/scissors_steel.gdshaderinc"

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic;
uniform vec4 albedo_color : source_color = vec4(1.0);
// What the underside keeps of its colour per channel: less red, most blue, so a shadow goes violet.
uniform vec3 shadow_tint = vec3(0.62, 0.58, 0.82);
// The top face is lifted by this much, never past 1.
uniform float top_boost = 1.12;
// Where along "looking down" to "looking up" the painted light turns over.
uniform vec2 turn = vec2(-0.55, 0.65);
// Painting the picture flat (the owner's textures stay as they are; this only changes how they are drawn): how many mip levels
// blurred (0 = as made), into how many value steps (0 = no steps), and how much colour (1 = as made).
uniform float flatten = 0.0;
uniform float steps = 0.0;
uniform float colour_gain = 1.0;
// 1: paint the black blades of the scissors picture as silver steel (scissors_steel.gdshaderinc); 0: the picture as made.
uniform float silver = 0.0;

varying float looks_up;
varying vec2 at;

void vertex() {
	looks_up = normalize(mat3(MODEL_MATRIX) * NORMAL).y;
	at = VERTEX.xy;
}

void fragment() {
	vec3 colour = texture(albedo_texture, UV, flatten).rgb;
	if (silver > 0.5) {
		colour = steel_blade(colour, at);
	}
	if (steps > 0.0) {
		float top = max(max(colour.r, colour.g), max(colour.b, 0.001));
		colour = mix(colour, colour * (floor(top * steps + 0.5) / steps / top), 0.75);
	}
	colour = mix(vec3(dot(colour, vec3(0.299, 0.587, 0.114))), colour, colour_gain);
	colour = clamp(colour, 0.0, 1.0) * albedo_color.rgb;
	vec3 lit = min(colour * top_boost, vec3(1.0));
	vec3 dark = colour * shadow_tint;
	ALBEDO = mix(dark, lit, smoothstep(turn.x, turn.y, looks_up));
}
