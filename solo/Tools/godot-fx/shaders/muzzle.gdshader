// Muzzle flash pointing right from the left-centre. frame 0 flash, middle bloom, last dissipates.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float origin_x = -0.32;
uniform float len = 0.75;       // fraction of width
uniform float spread = 0.55;    // half angle radians
uniform int petals = 3;
uniform float jag = 0.0;
uniform int sparkles = 3;

void fragment() {
	vec2 p = pix(UV);
	vec2 o = vec2(fsize.x * origin_x, 0.0);
	vec2 q = p - o;
	float L = fsize.x * len * mix(0.55, 1.0, smoothstep(0.0, 0.5, t));
	float r = length(q);
	float a = 0.0;
	float h = 0.0;
	float body = 1.0 - 0.6 * smoothstep(0.5, 1.0, t);
	// central lance
	float w = mix(3.2, 1.5, t) * (1.0 - clamp(q.x / L, 0.0, 1.0));
	float lm = step(0.0, q.x) * step(q.x, L) * (1.0 - smoothstep(w - 0.5, w + 0.5, abs(q.y + jag * lnoise(q.x * 0.5 + seed) * 1.2)));
	if (lm > 0.0 && body > 0.0) { a = max(a, lm * body); h = max(h, mix(1.0, 0.7, q.x / L)); }
	// petals fanning out
	for (int i = 0; i < 7; i++) {
		if (i >= petals) break;
		float fi = float(i);
		float pa = (fi / max(float(petals - 1), 1.0) - 0.5) * 2.0 * spread;
		if (abs(pa) < 0.01) continue;
		vec2 dir = vec2(cos(pa), sin(pa));
		float al = dot(q, dir);
		float pe = abs(dot(q, vec2(-dir.y, dir.x)));
		float pl = L * 0.6;
		float pw = mix(2.6, 1.0, t) * sin(clamp(al / max(pl, 0.1), 0.0, 1.0) * PI);
		float m = step(0.0, al) * step(al, pl) * (1.0 - smoothstep(pw - 0.4, pw + 0.4, pe));
		float pbody = 1.0 - smoothstep(0.6, 0.95, t);
		if (m > 0.0 && pbody > 0.0) { a = max(a, m * pbody); h = max(h, 0.8 - 0.2 * al / max(pl, 0.1)); }
	}
	// hot origin
	float c = mix(4.0, 1.0, t);
	float cm = 1.0 - smoothstep(c - 0.5, c + 0.5, r);
	if (cm > 0.0 && t < 0.9) { a = max(a, cm); h = max(h, 1.0); }
	float g = exp(-r / (3.0 + 3.0 * t)) * 0.55 * (1.0 - t);
	if (g > a) { a = g; h = max(h, 0.7); }
	// sparkles on the later frames
	for (int i = 0; i < 6; i++) {
		if (i >= sparkles) break;
		float fi = float(i);
		vec2 c2 = o + vec2(fsize.x * len * (0.35 + 0.55 * h11(fi + 3.0)) * mix(0.6, 1.1, t), (h11(fi + 7.0) - 0.5) * fsize.y * 0.55 * t);
		float m = plus_mask(p - c2, mix(1.0, 2.0, t) + 0.3, 0.5);
		if (m > 0.0 && t > 0.3) { a = max(a, m); h = max(h, 0.85); }
	}
	COLOR = vec4(h, 0.0, 0.0, a);
}
