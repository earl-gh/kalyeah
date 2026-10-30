// The scissors' blades drawn as painted silver steel (owner: "silver metal instead of black"), shared by every shader that draws the
// scissors model (scissors_steel.gdshader for the rig, weapon_look.gdshader and weapon_outline.gdshader for the static model).
// The owner's picture is untouched: its blades are black and its handles red, so the texel tells which is which (a handle texel is
// clearly redder than it is green or blue; a blade or screw texel is grey). The blade texels are then painted from where they are on
// the model (the mesh's own units: length 1 along Y, the screw at STEEL_PIVOT): a pale blue-grey that darkens toward the screw, one soft
// white streak along each blade near its cutting edge, a thin deeper blue-grey rim and a bright screw head with a ring. No gloss.
const vec2 STEEL_PIVOT = vec2(-0.005, 0.515);
const vec3 STEEL_LIGHT = vec3(0.70, 0.79, 0.90);
const vec3 STEEL_DARK = vec3(0.45, 0.55, 0.70);
const vec3 STEEL_RIM = vec3(0.24, 0.34, 0.52);
const vec3 STEEL_SHINE = vec3(1.0);

vec3 steel_blade(vec3 picture, vec2 at) {
	float handle = smoothstep(0.08, 0.25, picture.r - max(picture.g, picture.b));
	// how far along the blade from the screw (0) to the tip (1)
	float along = clamp((at.y - STEEL_PIVOT.y) / (1.0 - STEEL_PIVOT.y), 0.0, 1.0);
	// across the blade: -1 on the cutting edge (toward the scissors' axis), +1 on the spine (both blades mirror each other)
	float y = max(at.y, 0.6);
	float side = abs(at.x - STEEL_PIVOT.x);
	float across = (side - (0.048 + 0.2 * (y - 0.6))) / max(0.052 - 0.1 * (y - 0.6), 0.015);
	vec3 steel = mix(STEEL_DARK, STEEL_LIGHT, smoothstep(0.0, 0.85, along));
	steel = mix(steel, STEEL_LIGHT, 0.3 * (1.0 - smoothstep(-1.0, 0.2, across)));
	float streak = exp(-pow((across + 0.35) / 0.22, 2.0)) * smoothstep(0.08, 0.3, along) * (1.0 - smoothstep(0.82, 0.97, along));
	steel = mix(steel, STEEL_SHINE, 0.9 * streak);
	float rim = smoothstep(0.7, 0.95, abs(across)) * smoothstep(0.1, 0.25, along);
	steel = mix(steel, STEEL_RIM, 0.8 * rim);
	// the screw: a bright head with a darker ring round it
	float screw = length(at - STEEL_PIVOT);
	steel = mix(steel, STEEL_LIGHT, 1.0 - smoothstep(0.02, 0.026, screw));
	steel = mix(steel, STEEL_RIM, smoothstep(0.026, 0.03, screw) * (1.0 - smoothstep(0.034, 0.04, screw)));
	return mix(steel, picture, handle);
}
