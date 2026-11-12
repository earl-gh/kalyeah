shader_type sky;
// Deadlock-inspired painterly golden-afternoon Philippine barangay sky:
// rich atmospheric cerulean-cobalt zenith, dusty warm sky-blue mid-dome, golden-apricot
// horizon haze matching ArenaEnvironment fog, and sculpted sunlit cumulus cloud banks.

uniform vec3 sky_zenith : source_color = vec3(0.24, 0.49, 0.80);
uniform vec3 sky_mid : source_color = vec3(0.52, 0.72, 0.89);
uniform vec3 sky_horizon : source_color = vec3(0.93, 0.84, 0.72);
uniform vec3 horizon_glow : source_color = vec3(0.97, 0.89, 0.76);
uniform vec3 ground_color : source_color = vec3(0.38, 0.33, 0.28);
uniform vec3 cloud_lit : source_color = vec3(0.98, 0.94, 0.86);
uniform vec3 cloud_mid : source_color = vec3(0.88, 0.79, 0.70);
uniform vec3 cloud_shade : source_color = vec3(0.64, 0.62, 0.69);

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
	mat2 rot = mat2(vec2(0.80, -0.60), vec2(0.60, 0.80));
	v += 0.54 * noise2(p);
	p = rot * p * 2.09 + vec2(5.7, 2.3);
	v += 0.30 * noise2(p);
	p = rot * p * 2.13 + vec2(13.1, 8.9);
	v += 0.16 * noise2(p);
	return v;
}

void sky() {
	vec3 dir = normalize(EYEDIR);
	float up = clamp(dir.y, 0.0, 1.0);
	float drift = mod(TIME * 0.0042, 100.0);

	// Painterly atmospheric afternoon sky gradient
	vec3 sky_col = mix(horizon_glow, sky_horizon, smoothstep(0.0, 0.13, up));
	sky_col = mix(sky_col, sky_mid, smoothstep(0.09, 0.40, up));
	sky_col = mix(sky_col, sky_zenith, smoothstep(0.34, 0.90, up));

	// Subtle illustrative gouache brush grain across the sky dome
	float sky_stroke = noise2(vec2(atan(dir.z, dir.x) * 3.8, up * 14.0));
	sky_col *= 0.98 + 0.038 * sky_stroke;

	vec3 sun_dir = normalize(vec3(-0.39, 0.74, -0.54));
	if (LIGHT0_ENABLED) {
		sun_dir = normalize(LIGHT0_DIRECTION);
	}
	float sun_dot = max(dot(dir, sun_dir), 0.0);

	// Golden afternoon atmospheric scatter & crisp sun disc
	sky_col += vec3(1.00, 0.85, 0.56) * pow(sun_dot, 5.5) * 0.22 * (1.0 - smoothstep(0.0, 0.78, up) * 0.38);
	sky_col += vec3(1.00, 0.94, 0.80) * smoothstep(0.9974, 0.9993, sun_dot) * 0.76;

	if (dir.y > 0.015) {
		float fade_horizon = smoothstep(0.015, 0.16, dir.y);
		vec2 dome_uv = dir.xz / (dir.y + 0.28);

		// Layer 1: High windswept golden cirrus bands
		vec2 streak_uv = dome_uv * vec2(0.78, 2.05) + vec2(drift * 0.60, -drift * 0.20);
		float streak_n = cloud_fbm(streak_uv + vec2(noise2(streak_uv * 1.6) * 0.28, 0.0));
		float streak_mask = smoothstep(0.61, 0.81, streak_n) * fade_horizon * (1.0 - smoothstep(0.46, 0.86, up));
		vec3 streak_col = mix(cloud_mid, cloud_lit, 0.52 + 0.48 * pow(sun_dot, 2.0));
		sky_col = mix(sky_col, streak_col, streak_mask * 0.34);

		// Layer 2: Sculpted painterly cumulus clouds with cool slate-violet bellies & warm cream-gold crowns
		vec2 base_uv = dome_uv * 1.08 + vec2(drift, drift * 0.32);
		float warp = cloud_fbm(base_uv * 1.48 + vec2(3.4, 9.1));
		vec2 brush_uv = base_uv + vec2(warp - 0.5, (warp - 0.5) * 0.60) * 0.36;
		float density = cloud_fbm(brush_uv);
		float mask = smoothstep(0.51, 0.73, density) * fade_horizon;

		vec2 sun_step = normalize(sun_dir.xz + vec2(0.001)) * 0.11 + vec2(0.0, 0.07);
		float lit_sample = cloud_fbm(brush_uv + sun_step);
		float crown = clamp((density - lit_sample) * 2.25 + smoothstep(0.52, 0.80, density) * 0.66 + pow(sun_dot, 2.4) * 0.24, 0.0, 1.0);
		float painted_step = mix(crown, floor(crown * 6.0 + 0.5) / 6.0, 0.18);

		vec3 c_col = mix(cloud_shade, cloud_mid, smoothstep(0.14, 0.50, painted_step));
		c_col = mix(c_col, cloud_lit, smoothstep(0.48, 0.84, painted_step));
		float belly_band = (1.0 - smoothstep(0.51, 0.61, density)) * mask;
		c_col = mix(c_col, cloud_shade * 0.93, belly_band * 0.32);

		sky_col = mix(sky_col, c_col, mask * 0.82);
	}

	if (dir.y < 0.0) {
		sky_col = mix(sky_horizon * 0.90, ground_color, smoothstep(0.0, 0.22, -dir.y));
	}

	COLOR = sky_col;
}
