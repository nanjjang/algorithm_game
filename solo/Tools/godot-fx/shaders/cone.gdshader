// Directional spray from a point on the left edge, opening to the right.
// mode 0 = flame jet (looping puffs), 1 = snow/blizzard (looping flakes + wind streaks),
// 2 = dark wave (one-shot arc bands rolling outward, trailing mist).
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform int mode = 0;
uniform float origin_x = 0.06;   // fraction of width from the left edge
uniform float spread = 0.45;     // half angle, radians
uniform float len = 0.92;        // reach, fraction of width
uniform int puffs = 14;
uniform float size0 = 2.0;       // px puff radius at the nozzle
uniform float size1 = 9.0;       // px puff radius at full reach
uniform int flakes = 40;
uniform int bands = 3;
uniform float th0 = 6.0;         // band thickness px
uniform float squash = 1.0;      // vertical squash of the whole spray

void fragment() {
	vec2 p = pix(UV);
	vec2 o = vec2(-fsize.x * 0.5 + fsize.x * origin_x, 0.0);
	vec2 q = p - o;
	q.y /= squash;
	float L = fsize.x * len;
	float r = length(q);
	float ang = atan(q.y, q.x);
	float a = 0.0;
	float h = 0.0;
	if (mode == 0) {
		vec2 lp = vec2(cos(t * TAU), sin(t * TAU)) * 1.3;
		for (int i = 0; i < 32; i++) {
			if (i >= puffs) break;
			float fi = float(i);
			float ph = fract(h11(fi + 1.0) + t);
			float aa = (h11(fi + 7.0) - 0.5) * 2.0 * spread * 0.8 * (0.3 + 0.7 * ph);
			vec2 c = vec2(cos(aa), sin(aa)) * ph * L * (0.85 + 0.15 * h11(fi + 3.0));
			float rr = mix(size0, size1, pow(ph, 0.8)) * (0.75 + 0.5 * h11(fi + 11.0));
			float n = fbm((q - c) * 0.16 + lp + fi);
			float dd = length(q - c) / rr + (n - 0.5) * 0.9;
			float cut = 1.0 - smoothstep(0.75, 1.0, ph) * 0.8;
			if (dd < cut) {
				float body = 1.0 - dd / cut;
				float hh = mix(1.0, 0.5, pow(ph, 1.2)) * (0.75 + 0.35 * body);
				h = max(h, clamp(hh, 0.0, 1.0));
				a = max(a, 1.0);
			}
		}
		// hot stream at the nozzle
		float k = r / L;
		float core = (1.0 - smoothstep(0.0, spread * 0.35 * (0.4 + k), abs(ang))) * (1.0 - smoothstep(0.25, 0.5, k));
		if (core > 0.3 && q.x > 0.0) { a = 1.0; h = max(h, 0.95); }
		// rising embers
		for (int i = 0; i < 8; i++) {
			float fi = float(i);
			float ph = fract(h11(fi + 40.0) + t);
			float aa = (h11(fi + 47.0) - 0.5) * 2.0 * spread;
			vec2 c = vec2(cos(aa), sin(aa)) * (0.35 + 0.65 * ph) * L * 1.02 + vec2(0.0, -ph * 5.0);
			float m = 1.0 - smoothstep(0.4, 0.9, length(q - c));
			if (m > 0.0) { a = max(a, m * (1.0 - ph * 0.5)); h = max(h, 0.85); }
		}
	} else if (mode == 1) {
		// soft wind streaks (scroll outward, seamless because the scroll is a whole noise period)
		float k = r / L;
		float am = 1.0 - smoothstep(spread * 0.75, spread * 1.05, abs(ang));
		float sx = r * 0.05;
		float s0 = vnoise(vec2(sx - t * 3.0, ang * 34.0 + seed));
		float s1 = vnoise(vec2(sx - (t - 1.0) * 3.0, ang * 34.0 + seed));
		float s = mix(s0, s1, t);
		float body = am * smoothstep(0.03, 0.15, k) * (1.0 - smoothstep(0.7, 1.0, k));
		float streak = smoothstep(0.72, 0.86, s) * body;
		if (streak > 0.2) { a = max(a, 0.5); h = max(h, 0.8); }
		for (int i = 0; i < 64; i++) {
			if (i >= flakes) break;
			float fi = float(i);
			float ph = fract(h11(fi + 1.0) + t);
			float aa = (h11(fi + 7.0) - 0.5) * 2.0 * spread * (0.4 + 0.6 * ph);
			vec2 c = vec2(cos(aa), sin(aa)) * ph * L + vec2(0.0, sin(ph * 9.0 + fi) * 1.5);
			float big = h11(fi + 13.0);
			float m;
			if (big > 0.8) m = plus_mask(q - c, 1.6, 0.5);
			else m = 1.0 - smoothstep(0.5 + big * 0.6, 0.9 + big * 0.6, length(q - c));
			if (m > 0.0) { a = max(a, m * (1.0 - smoothstep(0.8, 1.0, ph))); h = max(h, big > 0.8 ? 1.0 : 0.8 + 0.15 * big); }
		}
	} else {
		float e = 1.0 - pow(1.0 - t, 1.6);
		float am = 1.0 - smoothstep(spread * 0.85, spread * 1.05, abs(ang));
		for (int i = 0; i < 6; i++) {
			if (i >= bands) break;
			float fi = float(i);
			float rb = (e * 1.05 - fi * 0.16) * L;
			if (rb <= 2.0) continue;
			float th = th0 * (1.0 - 0.45 * fi / float(bands)) * (1.0 - 0.4 * t);
			float wob = (vnoise(vec2(ang * 7.0 + fi * 3.0, seed + t * 2.0)) - 0.5) * th * 0.9;
			float d = abs(r - rb + wob);
			float band = 1.0 - smoothstep(th * 0.5 - 0.5, th * 0.5 + 0.5, d);
			float bn = vnoise(vec2(ang * 10.0 + fi * 5.0, seed + fi));
			band *= smoothstep(0.2 * t + 0.1 * fi, 0.25 * t + 0.1 * fi + 0.1, bn);
			band *= am;
			float str = 1.0 - fi * 0.28;
			if (band > 0.0) {
				float kk = (r < rb) ? 0.7 : 0.95;
				a = max(a, band * str);
				h = max(h, mix(kk, 0.55, d / max(th * 0.5, 0.5)) - 0.12 * fi);
			}
		}
		// trailing mist inside the front
		float front = e * 1.05 * L;
		if (r < front) {
			float n = fbm(q * 0.07 + vec2(seed, t * 2.0));
			float mist = am * smoothstep(0.35, 0.75, n) * smoothstep(front * 0.25, front, r) * 0.55 * (1.0 - 0.6 * t);
			if (mist > a) { a = mist; h = max(h, 0.45); }
		}
		for (int i = 0; i < 10; i++) {
			float fi = float(i);
			float aa = (h11(fi + 30.0) - 0.5) * 2.0 * spread;
			float rr = front * (0.75 + 0.3 * h11(fi + 33.0));
			vec2 c = vec2(cos(aa), sin(aa)) * rr;
			float m = 1.0 - smoothstep(0.5, 1.0, length(q - c));
			if (m > 0.0) { a = max(a, m * (1.0 - 0.5 * t)); h = max(h, 0.8); }
		}
		a *= 1.0 - smoothstep(0.6, 1.0, t) * 0.55;
	}
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
