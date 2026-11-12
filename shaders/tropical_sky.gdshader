shader_type sky;
// Sunlit 90s Philippine provincial daytime sky:
// airy tropical cerulean zenith, soft pastel sky-blue mid-dome, warm sunlit cream-mango horizon,
// and fluffy painterly cumulus clouds with soft warm lavender-oatmeal underbellies.

uniform vec3 sky_zenith : source_color = vec3(0.28, 0.55, 0.86);
uniform vec3 sky_mid : source_color = vec3(0.54, 0.75, 0.93);
uniform vec3 sky_horizon : source_color = vec3(0.95, 0.85, 0.70);
uniform vec3 horizon_glow : source_color = vec3(0.99, 0.91, 0.76);
uniform vec3 ground_color : source_color = vec3(0.40, 0.34, 0.27);
uniform vec3 cloud_lit : source_color = vec3(0.99, 0.96, 0.90);
uniform vec3 cloud_mid : source_color = vec3(0.90, 0.82, 0.74);
uniform vec3 cloud_shade : source_color = vec3(0.71, 0.65, 0.69);

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
	float drift = mod(TIME * 0.0048, 100.0);

	// Bright sunlit daytime sky gradient: warm cream-mango horizon -> pastel sky-blue -> airy cerulean zenith
	vec3 sky_col = mix(horizon_glow, sky_horizon, smoothstep(0.0, 0.12, up));
	sky_col = mix(sky_col, sky_mid, smoothstep(0.08, 0.38, up));
	sky_col = mix(sky_col, sky_zenith, smoothstep(0.32, 0.88, up));

	// Gentle matte-painting gouache brush grain across the sky dome
	float sky_stroke = noise2(vec2(atan(dir.z, dir.x) * 4.2, up * 16.0));
	sky_col *= 0.975 + 0.045 * sky_stroke;

	vec3 sun_dir = normalize(vec3(0.42, 0.56, -0.71));
	if (LIGHT0_ENABLED) {
		sun_dir = normalize(LIGHT0_DIRECTION);
	}
	float sun_dot = max(dot(dir, sun_dir), 0.0);

	// Soft warm sunlit atmospheric halo & crisp afternoon sun disc
	sky_col += vec3(1.00, 0.86, 0.58) * pow(sun_dot, 6.0) * 0.20 * (1.0 - smoothstep(0.0, 0.75, up) * 0.35);
	sky_col += vec3(1.00, 0.95, 0.80) * smoothstep(0.9972, 0.9992, sun_dot) * 0.78;

	if (dir.y > 0.015) {
		float fade_horizon = smoothstep(0.015, 0.15, dir.y);
		vec2 dome_uv = dir.xz / (dir.y + 0.26);

		// Layer 1: High airy sunlit cirrus wisps
		vec2 streak_uv = dome_uv * vec2(0.82, 2.15) + vec2(drift * 0.65, -drift * 0.22);
		float streak_n = cloud_fbm(streak_uv + vec2(noise2(streak_uv * 1.7) * 0.30, 0.0));
		float streak_mask = smoothstep(0.60, 0.80, streak_n) * fade_horizon * (1.0 - smoothstep(0.48, 0.88, up));
		vec3 streak_col = mix(cloud_mid, cloud_lit, 0.55 + 0.45 * pow(sun_dot, 2.0));
		sky_col = mix(sky_col, streak_col, streak_mask * 0.36);

		// Layer 2: Fluffy sunlit provincial cumulus clouds with warm lavender-oatmeal underbellies
		vec2 base_uv = dome_uv * 1.14 + vec2(drift, drift * 0.34);
		float warp = cloud_fbm(base_uv * 1.55 + vec2(3.4, 9.1));
		vec2 brush_uv = base_uv + vec2(warp - 0.5, (warp - 0.5) * 0.62) * 0.38;
		float density = cloud_fbm(brush_uv);
		float mask = smoothstep(0.50, 0.72, density) * fade_horizon;

		vec2 sun_step = normalize(sun_dir.xz + vec2(0.001)) * 0.10 + vec2(0.0, 0.07);
		float lit_sample = cloud_fbm(brush_uv + sun_step);
		float crown = clamp((density - lit_sample) * 2.1 + smoothstep(0.51, 0.79, density) * 0.68 + pow(sun_dot, 2.5) * 0.22, 0.0, 1.0);
		float painted_step = mix(crown, floor(crown * 5.0 + 0.5) / 5.0, 0.24);

		vec3 c_col = mix(cloud_shade, cloud_mid, smoothstep(0.12, 0.48, painted_step));
		c_col = mix(c_col, cloud_lit, smoothstep(0.46, 0.84, painted_step));
		float belly_band = (1.0 - smoothstep(0.50, 0.60, density)) * mask;
		c_col = mix(c_col, cloud_shade * 0.94, belly_band * 0.30);

		sky_col = mix(sky_col, c_col, mask * 0.84);
	}

	if (dir.y < 0.0) {
		sky_col = mix(sky_horizon * 0.90, ground_color, smoothstep(0.0, 0.22, -dir.y));
	}

	COLOR = sky_col;
}
