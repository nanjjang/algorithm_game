// Hollow vortex seen from slightly above: spiral wind streaks in a ring band, turning
// one arm-step per loop (seamless), with motes riding the flow.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float r_in = 0.32;
uniform float r_out = 0.96;
uniform float squash = 0.62;
uniform int arms = 5;
uniform float twist = 1.6;
uniform float thin = 0.75;     // 0..1, higher = thinner streaks
uniform int motes = 14;

void fragment() {
	vec2 p = pix(UV);
	vec2 q = vec2(p.x, p.y / squash);
	float R = min(fsize.x * 0.5, fsize.y * 0.5 / squash) - 1.5;
	float d = length(q) / R;
	float ang = atan(q.y, q.x);
	float a = 0.0;
	float h = 0.0;
	if (d > r_in * 0.7 && d < r_out) {
		float ph = ang * float(arms) + log(d) * twist * 6.0 - t * TAU;
		float s = sin(ph) * 0.5 + 0.5;
		vec2 cs = vec2(cos(ang), sin(ang)) * 2.0;
		float n = vnoise(cs + vec2(d * 9.0 + seed, 0.0));
		float band = smoothstep(r_in * 0.7, r_in, d) * (1.0 - smoothstep(r_out * 0.8, r_out, d));
		float st = smoothstep(thin, thin + 0.12, s + (n - 0.5) * 0.25) * band;
		if (st > 0.0) { a = max(a, st); h = max(h, mix(0.62, 0.95, s)); }
		float soft = smoothstep(0.35, 0.9, s) * band * 0.35;
		if (soft > a) { a = soft; h = max(h, 0.55); }
	}
	for (int i = 0; i < 32; i++) {
		if (i >= motes) break;
		float fi = float(i);
		float rr = mix(r_in, r_out * 0.9, h11(fi + 2.0)) * R;
		float ta = h11(fi + 5.0) * TAU + t * TAU / float(arms) * 2.0;
		vec2 c = vec2(cos(ta), sin(ta)) * rr;
		float m = fi < 5.0 ? plus_mask(q - c, 1.6, 0.5) : 1.0 - smoothstep(0.5, 1.0, length(q - c));
		if (m > 0.0) { a = max(a, m); h = max(h, 0.9); }
	}
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
