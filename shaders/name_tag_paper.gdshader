shader_type canvas_item;
// The name tag's white card, with its punched slot cut clean through: inside the slot the card (and its shadow) is gone, so whatever is
// behind shows (the back strap of the lanyard, the backdrop). A thin pale rim rings the slot, like the cut edge of the card.

uniform vec2 rect_size = vec2(300.0, 156.0);
uniform vec2 slot_centre = vec2(150.0, 22.0);  // in the card's pixels
uniform vec2 slot_half = vec2(32.0, 8.0);      // half of the slot's width and height
uniform float rim = 3.0;
uniform vec4 rim_colour : source_color = vec4(0.72, 0.75, 0.83, 1.0);

// Distance to a rounded rectangle (a capsule when the radius is half its height): negative inside.
float slot_distance(vec2 p) {
	float radius = slot_half.y;
	vec2 q = abs(p - slot_centre) - (slot_half - vec2(radius));
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

varying vec2 local;

void vertex() {
	local = VERTEX;
}

void fragment() {
	vec2 p = local;
	float d = slot_distance(p);
	// transparent inside the slot, with a smooth edge a pixel wide
	float hole = smoothstep(-0.6, 0.6, d);
	vec4 base = COLOR;
	// the cut edge: a pale ring just outside the slot
	float ring = 1.0 - smoothstep(rim - 0.8, rim + 0.2, d);
	base.rgb = mix(base.rgb, rim_colour.rgb, ring * step(-0.6, d) * base.a);
	COLOR = vec4(base.rgb, base.a * hole);
}
