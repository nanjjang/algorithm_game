// Burning ground patch (looping): scorched ellipse with glowing embers and a crowd of short
// flame tongues that flicker in place. Noise is sampled on a circle in time (seamless).
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float squash = 0.45;
uniform float radius = 0.46;   // fraction of width
uniform int tongues = 9;
uniform float tongue_h = 0.55; // fraction of height
uniform float tongue_w = 0.09; // fraction of width
uniform float scorch = 0.6;
uniform float cy = 0.18;       // ellipse centre below the frame centre, fraction of height

void fragment() {
	vec2 p = pix(UV);
	float W = fsize.x;
	float cyp = fsize.y * cy;
	vec2 q = vec2(p.x, (p.y - cyp) / squash);
	float R = W * radius;
	float d = length(q) / R;
	vec2 lp = vec2(cos(t * TAU), sin(t * TAU)) * 1.2;
	float a = 0.0;
	float h = 0.0;
	if (d < 1.0) {
		float n = fbm(q * 0.12 + seed);
		float sc = scorch * (1.0 - smoothstep(0.6, 1.0, d + (n - 0.5) * 0.4));
		if (sc > 0.0) { a = sc; h = 0.18 + 0.2 * n; }
		float em = fbm(q * 0.3 + lp * 0.6 + seed * 2.0);
		if (em > 0.62 && d < 0.85) { a = max(a, 0.95); h = max(h, 0.55 + (em - 0.62) * 2.0); }
	}
	for (int i = 0; i < 16; i++) {
		if (i >= tongues) break;
		float fi = float(i);
		float ta = h11(fi + 1.0) * TAU;
		float tr = sqrt(h11(fi + 3.0)) * 0.78;
		vec2 c = vec2(cos(ta) * tr * R, cyp + sin(ta) * tr * R * squash);
		float th = fsize.y * tongue_h * (0.55 + 0.45 * h11(fi + 6.0)) * (0.8 + 0.2 * sin(t * TAU * 2.0 + fi * 1.7));
		float tw = W * tongue_w * (0.7 + 0.5 * h11(fi + 8.0));
		vec2 tq = vec2((p.x - c.x) / tw, (c.y - p.y) / th);
		float nn = fbm(vec2(p.x * 0.18, p.y * 0.12) + lp + fi * 2.3);
		tq.x += (nn - 0.5) * 1.2 * clamp(tq.y, 0.0, 1.0);
		float wy = 1.0 - clamp(tq.y, 0.0, 1.0);
		float shape = 1.0 - abs(tq.x) / max(pow(wy, 0.7), 0.001);
		shape = min(shape, smoothstep(-0.25, 0.05, tq.y) * 1.5);
		float v = shape - nn * 0.45 * (0.3 + 0.7 * clamp(tq.y, 0.0, 1.0));
		if (v > 0.0 && tq.y > -0.3) {
			a = 1.0;
			h = max(h, clamp(0.35 + v * 1.1 - tq.y * 0.3, 0.0, 1.0));
		}
	}
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
