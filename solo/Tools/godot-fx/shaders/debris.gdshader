// Ground chunks thrown up and falling back (ballistic), with a dust skirt at the base.
// Origin at (0, base) — base is a fraction of the height from the top.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float base = 0.8;
uniform int chunks = 14;
uniform float vel = 0.9;       // launch height, fraction of frame height
uniform float spread_x = 0.45; // fraction of width
uniform float csize = 2.2;     // px
uniform float dust = 1.0;

void fragment() {
	vec2 p = pix(UV);
	float yb = (base - 0.5) * fsize.y;
	float a = 0.0;
	float h = 0.0;
	for (int i = 0; i < 32; i++) {
		if (i >= chunks) break;
		float fi = float(i);
		float vx = (h11(fi + 1.0) - 0.5) * 2.0 * spread_x * fsize.x;
		float vy = (0.45 + 0.55 * h11(fi + 4.0)) * vel * fsize.y;
		float tt = t * 1.1;
		vec2 c = vec2(vx * tt, yb - vy * 4.0 * tt * (1.0 - tt));
		float sz = csize * (0.6 + 0.8 * h11(fi + 9.0)) * (1.0 - 0.35 * t);
		vec2 lq = rot(h11(fi + 13.0) * 6.0 + t * 6.0 * (h11(fi + 15.0) - 0.5)) * (p - c);
		float m = step(abs(lq.x), sz) * step(abs(lq.y), sz * 0.75);
		if (m > 0.0 && c.y < yb + 2.0) {
			float lit = (lq.x + lq.y < 0.0) ? 0.78 : 0.45;
			a = max(a, 1.0 - smoothstep(0.75, 1.0, t));
			h = max(h, lit);
		}
	}
	if (dust > 0.0) {
		float e = 1.0 - pow(1.0 - t, 2.0);
		for (int i = 0; i < 7; i++) {
			float fi = float(i);
			float sx = (fi / 6.0 - 0.5) * 2.0;
			vec2 c = vec2(sx * fsize.x * 0.3 * (0.4 + 0.6 * e), yb - 2.0 - e * 4.0 * (1.0 - abs(sx)));
			float rr = fsize.y * (0.045 + 0.06 * e) * (0.7 + 0.5 * h11(fi + 20.0));
			vec2 dq = (p - c) / vec2(rr * 1.3, rr);
			float n = fbm(p * 0.2 + vec2(fi, -t * 2.0));
			float dd = length(dq) + (n - 0.5) * 0.6;
			float cut = 1.0 - smoothstep(0.3, 1.0, t) * (0.6 + 0.4 * n);
			if (dd < cut) {
				float lit = dot(normalize(dq + 0.0001), normalize(vec2(-1.0, -1.2))) * 0.5 + 0.5;
				a = max(a, dust * (1.0 - 0.3 * t));
				h = max(h, mix(0.35, 0.8, lit) - 0.15 * t);
			}
		}
	}
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
