// Ground circle seen from above (squashed). mode 0 = rune trap: double ring, a band of
// blocky glyphs and an inner star, turning one glyph step per loop and pulsing (seamless).
// mode 1 = strike marker: ring, dashed inner ring turning, four inward ticks, pulsing fill.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform int mode = 0;
uniform float squash = 0.5;
uniform int glyphs = 8;
uniform float rim_w = 1.4;
uniform int points = 5;
uniform float fill = 0.25;
uniform float pulse = 0.25;

float ringm(float d, float r, float w) { return 1.0 - smoothstep(w * 0.5 - 0.35, w * 0.5 + 0.35, abs(d - r)); }

void fragment() {
	vec2 p = pix(UV);
	vec2 q = vec2(p.x, p.y / squash);
	float R = min(fsize.x * 0.5, fsize.y * 0.5 / squash) - 1.5;
	float d = length(q);
	float ang = atan(q.y, q.x);
	float a = 0.0;
	float h = 0.0;
	float pl = 0.5 + 0.5 * sin(t * TAU);
	if (mode == 0) {
		float step_a = TAU / float(glyphs);
		float rotA = t * step_a;
		float o = ringm(d, R, rim_w * 1.2);
		float i1 = ringm(d, R * 0.66, rim_w);
		if (o > 0.0) { a = max(a, o); h = max(h, 0.92); }
		if (i1 > 0.0) { a = max(a, i1); h = max(h, 0.85); }
		float ra = (d - R * 0.83);
		float bandw = R * 0.15;
		if (abs(ra) < bandw) {
			float u = (ang - rotA + PI) / step_a;
			float cellI = mod(floor(u), float(glyphs));
			float lx = (fract(u) - 0.5) * step_a * d;   // px along the ring
			float ly = ra / squash * squash;            // px across (q space)
			float sh = bandw * 0.8;
			float v = floor(h11(cellI * 3.0 + 1.0) * 4.0);
			float v2 = floor(h11(cellI * 3.0 + 2.0) * 3.0);
			float on = step(abs(lx), 0.6) * step(abs(ly), sh);
			if (v < 1.0) on = max(on, step(abs(ly + sh), 0.6) * step(abs(lx), 2.2));
			else if (v < 2.0) on = max(on, step(abs(ly - sh), 0.6) * step(abs(lx), 2.2));
			else if (v < 3.0) on = max(on, step(abs(lx - ly * 0.7), 0.7) * step(abs(ly), sh));
			else on = max(on, step(length(vec2(abs(lx) - 2.0, ly)), 0.8));
			if (v2 < 1.0) on = max(on, step(abs(ly), 0.6) * step(abs(lx), 2.2));
			else if (v2 < 2.0) on = max(on, step(length(vec2(lx - 2.2, ly - sh)), 0.8));
			if (on > 0.0) { a = max(a, 1.0); h = max(h, 0.75 + 0.25 * pl); }
		}
		for (int i = 0; i < 9; i++) {
			if (i >= points) break;
			float a1 = float(i) / float(points) * TAU - PI * 0.5 + rotA;
			float a2 = float(i + 2) / float(points) * TAU - PI * 0.5 + rotA;
			vec2 P1 = vec2(cos(a1), sin(a1)) * R * 0.62;
			vec2 P2 = vec2(cos(a2), sin(a2)) * R * 0.62;
			float sd = sd_segment(q, P1, P2);
			float m = 1.0 - smoothstep(0.35, 0.9, sd);
			if (m > 0.0) { a = max(a, m * (0.75 + 0.25 * pl)); h = max(h, 0.78 + 0.2 * pl); }
		}
		if (d < R) {
			float f = fill * (0.7 + 0.3 * fbm(q * 0.15 + seed)) * (0.7 + 0.3 * pl);
			if (f > a) { a = f; h = max(h, 0.5); }
		}
		float c = 1.0 - smoothstep(1.0, 2.0, d);
		if (c > 0.0) { a = max(a, c); h = 1.0; }
		float g2 = exp(-abs(d - R) / 2.0) * 0.35 * (0.6 + 0.4 * pl);
		if (g2 > a) { a = g2; h = max(h, 0.6); }
	} else {
		float o = ringm(d, R, rim_w);
		if (o > 0.0) { a = max(a, o); h = max(h, 0.95); }
		float rotA = t * TAU / 8.0;
		float ri = R * 0.62;
		float dash = step(0.45, fract((ang - rotA + PI) / TAU * 8.0));
		float i1 = ringm(d, ri, rim_w * 0.9) * dash;
		if (i1 > 0.0) { a = max(a, i1); h = max(h, 0.8); }
		for (int i = 0; i < 4; i++) {
			float ta = float(i) * PI * 0.5 + PI * 0.25;
			vec2 dir = vec2(cos(ta), sin(ta));
			float al = dot(q, dir);
			float pe = abs(dot(q, vec2(-dir.y, dir.x)));
			float m = step(pe, 0.8) * step(R * 0.72, al) * step(al, R - 1.0);
			if (m > 0.0) { a = max(a, m); h = max(h, 0.9); }
		}
		if (d < R) {
			float k = d / R;
			float f = fill * (0.6 + 0.4 * pl) * smoothstep(0.35, 1.0, k);
			if (f > a) { a = f; h = max(h, 0.55); }
		}
		float c = plus_mask(vec2(p.x, p.y), 2.0 + pl, 0.5);
		if (c > 0.0) { a = max(a, c); h = 1.0; }
		float g2 = exp(-abs(d - R) / 2.2) * (0.25 + pulse * pl);
		if (g2 > a) { a = g2; h = max(h, 0.62); }
	}
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
