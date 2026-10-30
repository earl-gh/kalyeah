shader_type spatial;
// The smear of a fast skill (docs/ART.md "Smear frames"): ONE shared look for every swing arc, throw trail and smack streak, drawn
// on a ribbon mesh (FxKit.smear_*: UV x runs tail 0 .. head 1 along the motion, UV y -1..1 across, the vertex colour says which
// layer of the ribbon a vertex belongs to). Layers, all from a few maths ops and no texture fetch: a feathered coloured band
// with a frayed edge, a hot near-white core, thin speed lines of different lengths that break up along the motion, a bright
// leading edge, and grain sparkles in the fading tail. Unshaded, alpha-blended, UV and vertex colour only: no screen or depth
// texture, no instance uniforms (safe on the Compatibility renderer, GLES3 phones). One ShaderMaterial per pooled slot
// (FxGround) sets the four uniforms; nothing is made per hit.
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

// The band's colour (the core and the lines are near white whatever it is).
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);
// Whole-ribbon alpha: the peak at birth, 0 when the ribbon is gone.
uniform float strength : hint_range(0.0, 1.0) = 1.0;
// How far through its life the ribbon is: the tail wipes away toward the head and the band thins as it grows.
uniform float age : hint_range(0.0, 1.0) = 0.0;
// A per-slot phase so two smears at once never break up the same way.
uniform float seed : hint_range(0.0, 64.0) = 0.0;

const float LANES = 5.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	// vertex colour: r = how strong this layer is (the main ribbon 1, its echoes less), g = it has a bright leading edge, b = it carries speed lines
	float layer = COLOR.r;
	float u = UV.x;
	float across = UV.y;
	float v = abs(across) / (1.0 - 0.4 * age);
	float noise = vnoise(vec2(u * 8.0 - age * 5.0 + seed, across * 3.0 + seed * 1.7));
	// the coloured band: feathered edge (smoothstep across the width), frayed by noise, lighter toward the tail
	float edge = 0.8 + 0.2 * (noise - 0.5);
	float band = (1.0 - smoothstep(edge - 0.3, edge, v)) * mix(0.45, 0.95, smoothstep(0.0, 0.7, u));
	// the hot core: a narrow near-white stripe that is brightest near the head
	float core = (1.0 - smoothstep(0.0, 0.42, v)) * smoothstep(0.05, 0.8, u);
	// thin speed lines: a few lanes across, each starting at its own place along the tail and breaking into dashes
	float y = across * 0.5 + 0.5;
	float lane = floor(y * LANES);
	float inside = abs(fract(y * LANES) - 0.5);
	float start = 0.1 + 0.55 * hash(vec2(lane, seed));
	float dashes = step(0.32, vnoise(vec2(u * 11.0 + lane * 3.1 - age * 4.0, lane * 7.0 + seed)));
	float lines = (1.0 - smoothstep(0.05, 0.2, inside)) * smoothstep(start, start + 0.2, u) * dashes * COLOR.b * (1.0 - smoothstep(0.8, 1.0, v));
	// the bright leading edge
	float lead = COLOR.g * smoothstep(0.8, 0.97, u) * (1.0 - smoothstep(0.2, 0.95, v));
	// grain sparkles flicker in the tail and die with the ribbon
	float grain = step(0.9, hash(floor(vec2(u * 36.0, y * 8.0)) + floor(age * 6.0) + seed)) * (1.0 - u) * (1.0 - age) * (1.0 - smoothstep(0.7, 1.0, v));
	float hot = clamp(core + lead + lines * 0.8 + grain, 0.0, 1.0);
	float alpha = max(max(band, core * 0.95), max(lines * 0.9, max(lead, grain * 0.85)));
	// the tail wipes away from its far end as the smear ages
	float wipe = smoothstep(age * 0.85 - 0.05, age * 0.85 + 0.2, u);
	// a darker rim of the band's own colour just inside its soft edge: it reads against pale skin, paper and the street alike
	float rim = smoothstep(0.35, 0.65, v) * (1.0 - hot);
	ALBEDO = mix(mix(tint.rgb, vec3(1.0), hot), tint.rgb * 0.55, rim * 0.75);
	ALPHA = clamp(alpha * layer * wipe * strength, 0.0, 1.0);
}
