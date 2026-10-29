shader_type spatial;
// The ground shadow under a flying skill object (docs/ART.md "Skill effects"): a soft dark blob like the kid's own, plus a
// thin ring in the thrower's team colour so a thing in the air says where it is and who threw it. One quad, UV only:
// no screen or depth texture, no instance uniforms (safe on the Compatibility renderer, GLES3 phones).
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec4 rim_color : source_color = vec4(1.0, 1.0, 1.0, 0.9);
// The blob's darkness in the middle (0.45 on the ground, lighter the higher the object is).
uniform float alpha : hint_range(0.0, 1.0) = 0.45;
// The team ring's width as a fraction of the shadow's radius.
uniform float rim_width : hint_range(0.0, 0.5) = 0.08;

void fragment() {
	float d = length(UV - vec2(0.5)) * 2.0;
	float blob = alpha * (1.0 - smoothstep(0.0, 1.0, d));
	float ring = smoothstep(1.0 - rim_width - 0.03, 1.0 - rim_width, d) * (1.0 - smoothstep(0.97, 1.0, d));
	ALBEDO = mix(vec3(0.0), rim_color.rgb, ring);
	ALPHA = max(blob, ring * rim_color.a);
}
