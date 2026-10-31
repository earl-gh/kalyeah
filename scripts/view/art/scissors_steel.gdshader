shader_type spatial;
// The scissors' own look (ScissorsRig, when it carries a team outline): the matte, wrap-lit model look of the kids and props with the
// blades painted silver (scissors_steel.gdshaderinc) and the handles' red kept. It marks the stencil like a kid's material so the team
// outline (team_outline.gdshader) draws only round the scissors. No gloss: no specular, no normal map.
render_mode diffuse_lambert_wrap, specular_disabled;
stencil_mode write, compare_always, 1;

#include "res://scripts/view/art/scissors_steel.gdshaderinc"

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic;

varying vec2 at;

void vertex() {
	at = VERTEX.xy;
}

void fragment() {
	ALBEDO = steel_blade(texture(albedo_texture, UV).rgb, at);
	ROUGHNESS = 1.0;
	RIM = 0.1;
	RIM_TINT = 0.3;
}
