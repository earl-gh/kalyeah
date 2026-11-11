shader_type sky;
// 90s Filipino anime x Arcane golden-hour provincial sky:
// rich cobalt-indigo zenith, warm mango-amber horizon, and painterly brushstroke clouds
// with dark-warm plum/umber underbellies.

uniform vec3 sky_zenith : source_color = vec3(0.09, 0.16, 0.42);
uniform vec3 sky_mid : source_color = vec3(0.23, 0.35, 0.63);
uniform vec3 sky_horizon : source_color = vec3(0.90, 0.54, 0.25);
uniform vec3 horizon_glow : source_color = vec3(0.96, 0.72, 0.38);
uniform vec3 ground_color : source_color = vec3(0.25, 0.20, 0.18);
uniform vec3 cloud_lit : source_color = vec3(0.95, 0.77, 0.50);
uniform vec3 cloud_mid : source_color = vec3(0.66, 0.39, 0.34);
uniform vec3 cloud_shade : source_color = vec3(0.33, 0.22, 0.30);

float hash2(vec2 p) {
	p = fract(p * vec2(127.1, 311.7));
	p += dot(p, p + 34.23);
	return fract(p.x * p.y);
}

float noise2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(hash2(i), hash2(i + vec2(1.0, 0.0)), u.x),
		mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), u.x),
		u.y
	);
}

float cloud_fbm(vec2 p) {
	float v = 0.0;
	v += 0.54 * noise2(p);
	v += 0.30 * noise2(p * 2.11 + vec2(5.7, 2.3));
	v += 0.16 * noise2(p * 4.29 + vec2(13.1, 8.9));
	return v;
}

void sky() {
	vec3 dir = normalize(EYEDIR);
	float up = clamp(dir.y, 0.0, 1.0);
	float drift = mod(TIME * 0.0055, 100.0);

	// Painterly multi-stop sky gradient: mango-amber horizon -> twilight cerulean -> cobalt-indigo zenith
	vec3 sky_col = mix(horizon_glow, sky_horizon, smoothstep(0.0, 0.10, up));
	sky_col = mix(sky_col, sky_mid, smoothstep(0.07, 0.34, up));
	sky_col = mix(sky_col, sky_zenith, smoothstep(0.28, 0.86, up));

	// Subtle horizontal matte-painting gouache brush texture in the sky dome
	float sky_stroke = noise2(vec2(atan(dir.z, dir.x) * 4.5, up * 18.0));
	sky_col *= 0.96 + 0.07 * sky_stroke;

	vec3 sun_dir = normalize(vec3(0.45, 0.32, -0.83));
	float sun_dot = 0.0;
	if (LIGHT0_ENABLED) {
		sun_dir = normalize(LIGHT0_DIRECTION);
	}
	sun_dot = max(dot(dir, sun_dir), 0.0);

	// Warm golden-hour atmospheric halo & painted anime sun disc
	sky_col += vec3(0.95, 0.58, 0.24) * pow(sun_dot, 5.0) * 0.28 * (1.0 - smoothstep(0.0, 0.65, up) * 0.45);
	sky_col += vec3(0.98, 0.84, 0.56) * smoothstep(0.9968, 0.9991, sun_dot) * 0.92;

	if (dir.y > 0.015) {
		float fade_horizon = smoothstep(0.015, 0.16, dir.y);
		vec2 dome_uv = dir.xz / (dir.y + 0.24);

		// Layer 1: High anisotropic golden-hour brushstroke streaks
		vec2 streak_uv = dome_uv * vec2(0.85, 2.35) + vec2(drift * 0.7, -drift * 0.25);
		float streak_n = cloud_fbm(streak_uv + vec2(noise2(streak_uv * 1.8) * 0.35, 0.0));
		float streak_mask = smoothstep(0.58, 0.78, streak_n) * fade_horizon * (1.0 - smoothstep(0.45, 0.85, up));
		vec3 streak_col = mix(cloud_mid, cloud_lit, 0.45 + 0.55 * pow(sun_dot, 2.0));
		sky_col = mix(sky_col, streak_col, streak_mask * 0.42);

		// Layer 2: Billowing painterly cumulus clouds with dark-warm plum/umber underbellies
		vec2 base_uv = dome_uv * 1.18 + vec2(drift, drift * 0.38);
		float warp = cloud_fbm(base_uv * 1.65 + vec2(3.4, 9.1));
		vec2 brush_uv = base_uv + vec2(warp - 0.5, (warp - 0.5) * 0.65) * 0.42;
		float density = cloud_fbm(brush_uv);
		float mask = smoothstep(0.49, 0.71, density) * fade_horizon;

		// Directional light + vertical crown vs dark-warm underbelly separation
		vec2 sun_step = normalize(sun_dir.xz + vec2(0.001)) * 0.11 + vec2(0.0, 0.07);
		float lit_sample = cloud_fbm(brush_uv + sun_step);
		float crown = clamp((density - lit_sample) * 2.4 + smoothstep(0.52, 0.80, density) * 0.65 + pow(sun_dot, 3.0) * 0.25, 0.0, 1.0);
		// Subtle Arcane gouache step quantization so clouds read like hand-painted strokes
		float painted_step = mix(crown, floor(crown * 4.5 + 0.5) / 4.5, 0.36);

		vec3 c_col = mix(cloud_shade, cloud_mid, smoothstep(0.14, 0.52, painted_step));
		c_col = mix(c_col, cloud_lit, smoothstep(0.50, 0.86, painted_step));
		// Dark-warm rim along the very bottom base of low horizon clouds
		float belly_band = (1.0 - smoothstep(0.49, 0.60, density)) * mask;
		c_col = mix(c_col, cloud_shade * 0.86, belly_band * 0.48);

		sky_col = mix(sky_col, c_col, mask * 0.88);
	}

	if (dir.y < 0.0) {
		sky_col = mix(sky_horizon * 0.82, ground_color, smoothstep(0.0, 0.20, -dir.y));
	}

	COLOR = sky_col;
}
