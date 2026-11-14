// Projectile trail: bright head on the right, wispy tapering tail to the left. Loops over frames.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float head_x = 0.3;     // fraction of width from centre
uniform float head_r = 3.0;     // px
uniform float tail_w = 3.0;     // px half-width at head
uniform float wave = 1.0;       // px tail wobble
uniform int sparks = 3;
uniform float jag = 0.0;
uniform float flame = 0.0;
uniform float glow = 0.5;

void fragment() {
	vec2 p = pix(UV);
	float ph = t * TAU;
	vec2 hp = vec2(fsize.x * head_x, 0.0);
	float L = hp.x + fsize.x * 0.5;
	float k = clamp((hp.x - p.x) / L, 0.0, 1.0);
	float a = 0.0;
	float h = 0.0;
	if (p.x < hp.x) {
		float off = sin(k * 7.0 - ph) * wave * k + jag * lnoise(k * 9.0 + float(frame) * 3.7) * 2.0 * k;
		float w = tail_w * pow(1.0 - k, 0.8) * (1.0 + flame * 0.6 * (vnoise(vec2(k * 6.0 - ph / TAU * 6.0, seed)) - 0.5));
		float dy = abs(p.y - off);
		float n = vnoise(vec2(p.x * 0.35 + float(frame) * 2.0, p.y * 0.8 + seed));
		float m = 1.0 - smoothstep(w - 0.5, w + 0.5, dy + (n - 0.5) * 1.6 * k);
		if (m > 0.0) {
			a = max(a, m * (1.0 - smoothstep(0.55, 1.0, k)));
			float core = 1.0 - smoothstep(0.0, w * 0.5, dy);
			h = max(h, mix(0.55, 0.92, (1.0 - k) * 0.6 + core * 0.4));
		}
	}
	float d = length((p - hp) * vec2(0.8, 1.0));
	float hm = 1.0 - smoothstep(head_r - 0.5, head_r + 0.5, d);
	if (hm > 0.0) { a = max(a, hm); h = max(h, mix(1.0, 0.75, smoothstep(head_r * 0.3, head_r, d))); }
	float g = exp(-max(d - head_r, 0.0) / 1.6) * glow;
	if (g > a) { a = g; h = max(h, 0.7); }
	for (int i = 0; i < 8; i++) {
		if (i >= sparks) break;
		float fi = float(i);
		float kk = fract(h11(fi + 4.0) + t);
		vec2 c = vec2(hp.x - kk * L * 0.9, (h11(fi + 9.0) - 0.5) * tail_w * 3.0 * kk);
		float m = 1.0 - smoothstep(0.4, 0.9, length(p - c));
		if (m > 0.0) { a = max(a, m * (1.0 - kk * 0.6)); h = max(h, 0.85); }
	}
	COLOR = vec4(h, 0.0, 0.0, a);
}
