// Puffy smoke / dust / steam: noisy blobs lit from the top-left, drifting and eroding.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform int puffs = 5;
uniform float size0 = 0.22;     // fraction of min(fsize) radius at t=0
uniform float grow = 0.12;
uniform float spread = 0.25;    // outward travel (fraction of size)
uniform float rise = 0.15;      // upward travel (fraction of height)
uniform float base_y = 0.15;    // where puffs start (fraction of height below centre)
uniform float erode = 1.0;
uniform float hi = 0.85;        // max heat (lit side)
uniform float lo = 0.25;        // min heat (shadow side)
uniform float flat_y = 1.0;     // <1 squashes puffs vertically (ground dust)
uniform float wisp = 0.0;       // stretch puffs into upward wisps

void fragment() {
	vec2 p = pix(UV);
	float M = min(fsize.x, fsize.y);
	float e = 1.0 - (1.0 - t) * (1.0 - t);
	float a = 0.0;
	float h = 0.0;
	for (int i = 0; i < 12; i++) {
		if (i >= puffs) break;
		float fi = float(i);
		float ang = fi / float(puffs) * TAU + h11(fi + 2.0) * 1.2;
		vec2 dir = vec2(cos(ang), sin(ang) * flat_y);
		vec2 c = vec2(0.0, fsize.y * base_y) + dir * M * (0.12 + spread * e) * (0.5 + 0.5 * h11(fi + 5.0));
		c.y -= fsize.y * rise * e * (0.6 + 0.4 * h11(fi + 8.0));
		float rr = M * (size0 + grow * e) * (0.6 + 0.5 * h11(fi + 11.0));
		vec2 q = (p - c) / rr;
		q.y /= flat_y;
		q.y *= 1.0 + wisp * 0.6;
		q.x += wisp * sin(q.y * 2.5 + fi) * 0.25;
		float n = fbm(p / M * 5.0 + vec2(fi * 3.1, -t * 1.5));
		float dd = length(q) + (n - 0.5) * 0.55;
		float cut = 1.0 - erode * smoothstep(0.25, 1.0, t) * (0.55 + 0.45 * n);
		if (dd < cut) {
			float lit = dot(normalize(q + vec2(0.0001)), normalize(vec2(-1.0, -1.2))) * 0.5 + 0.5;
			float body = smoothstep(cut, cut * 0.4, dd);
			float hh = mix(lo, hi, clamp(lit * 0.75 + body * 0.35, 0.0, 1.0)) - 0.15 * t;
			h = max(h, hh);
			a = max(a, 1.0 - 0.35 * smoothstep(0.5, 1.0, t));
		}
	}
	COLOR = vec4(h, 0.0, 0.0, a);
}
