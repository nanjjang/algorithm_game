// Four-point sparkle with a soft glow and small satellite glints.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float arm = 0.46;       // fraction of min frame size
uniform float waist = 0.11;     // fraction of arm length
uniform float glow = 0.5;
uniform float diag = 0.4;       // secondary diagonal arms
uniform int glints = 2;
uniform float spin = 0.0;
uniform float scale_a = 1.0;    // scale on frame 0
uniform float scale_b = 1.0;    // scale on the last frame

float star_shape(vec2 p, float L, float w) {
	vec2 ap = abs(p);
	// concave diamond arms
	float sx = 1.0 - ap.x / L;
	float sy = 1.0 - ap.y / L;
	float hx = w * L * pow(max(sx, 0.0), 2.0);
	float hy = w * L * pow(max(sy, 0.0), 2.0);
	float m1 = step(ap.y, hx + 0.35) * step(ap.x, L);
	float m2 = step(ap.x, hy + 0.35) * step(ap.y, L);
	return max(m1, m2);
}

void fragment() {
	vec2 p = pix(UV);
	float M = min(fsize.x, fsize.y);
	float sc = mix(scale_a, scale_b, t);
	vec2 q = rot(spin * t) * p;
	float L = M * arm * sc;
	float a = 0.0;
	float h = 0.0;
	float s = star_shape(q, L, waist * 2.2);
	if (s > 0.0) {
		float dd = max(abs(q.x), abs(q.y)) / L;
		a = 1.0;
		h = mix(1.0, 0.72, smoothstep(0.1, 1.0, dd));
		if (min(abs(q.x), abs(q.y)) > 0.8 && dd > 0.25) h = min(h, 0.8);
	}
	if (diag > 0.0) {
		vec2 dq = rot(0.785398) * q;
		float s2 = star_shape(dq, L * diag, waist * 1.6);
		if (s2 > 0.0 && a < 1.0) { a = 1.0; h = max(h, 0.82); }
	}
	float g = exp(-length(q) / (L * 0.28)) * glow;
	if (g > a) { a = g; h = max(h, 0.7); }
	for (int i = 0; i < 6; i++) {
		if (i >= glints) break;
		float fi = float(i);
		float ang = h11(fi + 3.0) * TAU;
		vec2 c = vec2(cos(ang), sin(ang)) * L * (0.75 + 0.2 * h11(fi + 8.0));
		float m = plus_mask(p - c, 1.3 * sc + 0.3, 0.5);
		if (m > 0.0) { a = max(a, m); h = max(h, 0.88); }
	}
	COLOR = vec4(h, 0.0, 0.0, a);
}
