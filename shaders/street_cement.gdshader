shader_type spatial;
// Code-drawn cement for the play area and the raised sidewalks (no image files): the vertex
// colours give each surface its gray (street, sidewalk top, sidewalk face); this adds soft,
// painted-looking wear on top in world space, so it never repeats. Fine detail fades out with
// distance (fwidth), so nothing shimmers on a phone. Cheap: a few value-noise lookups per pixel.
render_mode diffuse_lambert_wrap, specular_disabled;

uniform float lane_half_width = 12.0;
uniform float curb_height = 0.4;
// sidewalk slabs: a joint across the walk every this many metres
uniform float slab_length = 1.5;
uniform float joint_width = 0.025;
// 1 = the full cement wear; less for the small main-screen model, where it would look busy
uniform float wear = 1.0;
// patched repairs on the street: the grid they are picked from (m)
uniform float patch_size = 3.0;

varying vec3 world_position;
varying vec3 world_normal;

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// 0 when one pixel covers more than half a feature of `size` metres (too fine to show), 1 when sharp
float keep(float footprint, float size) {
	return 1.0 - smoothstep(size * 0.25, size * 0.5, footprint);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec2 uv = world_position.xz;
	float footprint = length(fwidth(uv));
	float shade = 1.0;
	if (world_normal.y > 0.5) {
		// broad soft blotches, a mottle and a fine grain (faded with distance)
		shade += (value_noise(uv * 0.12) - 0.5) * 0.18;
		shade += (value_noise(uv * 0.9 + 7.0) - 0.5) * 0.14 * keep(footprint, 1.1);
		shade += (value_noise(uv * 6.0 + 3.0) - 0.5) * 0.08 * keep(footprint, 0.17);
		// old stains and patched spots
		shade -= smoothstep(0.62, 0.82, value_noise(uv * 0.35 + 17.0)) * 0.08;
		bool sidewalk = abs(world_position.x) > lane_half_width;
		if (sidewalk) {
			// slab joints across the walk, anti-aliased
			float along = world_position.z / slab_length;
			float to_joint = (0.5 - abs(fract(along) - 0.5)) * slab_length;
			float aa = fwidth(world_position.z);
			shade -= (1.0 - smoothstep(joint_width, joint_width + aa, to_joint)) * 0.22 * keep(footprint, 0.2);
		} else {
			// patched repairs: now and then a cell of the street was re-poured, a soft-edged
			// rectangle a little lighter or darker than the cement round it
			vec2 cell = floor(uv / patch_size);
			float pick = hash(cell + 11.0);
			if (pick > 0.86) {
				vec2 local = fract(uv / patch_size) * patch_size;
				vec2 inset = vec2(0.4 + hash(cell + 2.0) * 0.6, 0.4 + hash(cell + 5.0) * 0.6);
				vec2 inside = min(local - inset, patch_size - inset * 1.4 - local);
				float edge = min(inside.x, inside.y);
				float aa_patch = fwidth(edge) + 0.04;
				float patch = smoothstep(0.0, aa_patch, edge);
				shade += patch * (pick > 0.93 ? 0.07 : -0.06);
			}
		}
	} else {
		// the raised face toward the street: grime toward the bottom, soft streaks along it
		float height = clamp(world_position.y / curb_height, 0.0, 1.0);
		shade -= (1.0 - smoothstep(0.0, 0.7, height)) * 0.14;
		float along = abs(world_normal.x) > 0.5 ? world_position.z : world_position.x;
		shade += (value_noise(vec2(along * 0.8, height * 3.0)) - 0.5) * 0.10;
	}
	shade = 1.0 + (shade - 1.0) * wear;
	ALBEDO = COLOR.rgb * clamp(shade, 0.6, 1.2);
	ROUGHNESS = 1.0;
	RIM = 0.15;
	RIM_TINT = 0.7;
}
