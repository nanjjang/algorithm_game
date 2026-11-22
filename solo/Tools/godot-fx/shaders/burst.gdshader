// Radial hit burst: hot core, tapered rays that detach outward, "+" sparkles, embers.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform int rays = 10;
uniform float ray_len = 0.95;   // fraction of radius
uniform float ray_w = 2.0;      // px at the root
uniform float core = 6.0;       // px at t=0
uniform int sparkles = 4;
uniform float spark_arm = 2.0;
uniform float jag = 0.0;        // 0 straight rays, 1 lightning-jagged
uniform int embers = 0;
uniform float shard = 0.0;      // 1 = rays become diamond shards
uniform float fade = 1.0;
uniform float ring = 0.0;       // faint shock ring strength

void fragment() {
	vec2 p = pix(UV);
	float R = min(fsize.x, fsize.y) * 0.5 - 1.0;
	float e = 1.0 - (1.0 - t) * (1.0 - t);
	float d = length(p);
	float a = 0.0;
	float h = 0.0;
	// core + halo
	float c = core * pow(1.0 - t, 2.0);
	float cm = 1.0 - smoothstep(c - 0.7, c + 0.7, d);
	float halo = exp(-max(d - c, 0.0) / (c * 0.35 + 0.8)) * (1.0 - t);
	if (cm > 0.0) { a = max(a, cm); h = max(h, 1.0); }
	if (halo * 0.6 > a) { a = max(a, halo * 0.6); h = max(h, 0.7 + 0.25 * halo); }
	// rays
	for (int i = 0; i < 24; i++) {
		if (i >= rays) break;
		float fi = float(i);
		float ang = fi / float(rays) * TAU + (h11(fi + 3.0) - 0.5) * 0.6 + seed;
		vec2 dir = vec2(cos(ang), sin(ang));
		vec2 nrm = vec2(-dir.y, dir.x);
		float L = R * ray_len * (0.55 + 0.45 * h11(fi + 11.0));
		float r1 = mix(0.62 * L, L, e);
		float r0 = mix(c * 0.5, 0.6 * r1, smoothstep(0.2, 1.0, t));
		float al = dot(p, dir);
		float pe = dot(p, nrm) + jag * lnoise(al * 0.45 + fi * 7.0) * 1.6;
		if (al > r0 && al < r1) {
			float k = (al - r0) / max(r1 - r0, 0.001);
			float w;
			if (shard > 0.5) { w = ray_w * (1.0 - abs(k * 2.0 - 1.0)) * 1.4; }
			else { w = ray_w * (1.0 - 0.75 * k) * (1.0 - 0.45 * t); }
			float m = 1.0 - smoothstep(w * 0.5 - 0.35, w * 0.5 + 0.35, abs(pe));
			if (m > 0.0) {
				a = max(a, m);
				float hh = mix(0.9, 0.62, k) - 0.12 * t;
				if (abs(pe) < w * 0.22 && k < 0.55) hh = max(hh, 0.97 - 0.15 * t);
				h = max(h, hh);
			}
		}
	}
	// "+" sparkles riding the ray tips
	for (int i = 0; i < 12; i++) {
		if (i >= sparkles) break;
		float fi = float(i);
		float ang = fi / float(sparkles) * TAU + 0.4 + h11(fi + 41.0) * 0.8 + seed;
		float rr = R * mix(0.55, 0.92, e) * (0.85 + 0.15 * h11(fi + 5.0));
		vec2 c2 = vec2(cos(ang), sin(ang)) * rr;
		float arm = spark_arm * (1.0 - 0.3 * t);
		float m = plus_mask(p - c2, arm + 0.5, 0.5);
		if (m > 0.0) { a = max(a, m); h = max(h, 0.88 - 0.12 * t); }
	}
	// embers drifting outward
	for (int i = 0; i < 24; i++) {
		if (i >= embers) break;
		float fi = float(i);
		float ang = h11(fi + 70.0) * TAU;
		float sp = 0.3 + 0.7 * h11(fi + 90.0);
		vec2 c2 = vec2(cos(ang), sin(ang)) * R * sp * (0.25 + 0.7 * e) + vec2(0.0, -2.0 * t);
		float sz = mix(1.4, 0.7, t) * (0.7 + 0.6 * h11(fi + 13.0));
		float m = 1.0 - smoothstep(sz - 0.4, sz + 0.4, length(p - c2));
		if (m > 0.0) { a = max(a, m); h = max(h, 0.7 + 0.2 * h11(fi)); }
	}
	// faint shock ring
	if (ring > 0.0) {
		float rr = R * mix(0.3, 0.95, e);
		float m = (1.0 - smoothstep(0.4, 1.2, abs(d - rr))) * ring * (1.0 - t);
		if (m > a * 0.5) { a = max(a, m); h = max(h, 0.6); }
	}
	a *= mix(1.0, 1.0 - fade * 0.3, smoothstep(0.5, 1.0, t));
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
