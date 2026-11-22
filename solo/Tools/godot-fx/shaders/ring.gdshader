// Expanding shock ring with glow, breakup, inner fill, debris and optional tick runes.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float r0 = 0.25;
uniform float r1 = 0.95;
uniform float th0 = 6.0;
uniform float th1 = 1.5;
uniform float glow = 1.0;
uniform float breakup = 0.0;
uniform float fill = 0.0;
uniform int debris = 0;
uniform int ticks = 0;
uniform float squash = 1.0;
uniform float arc = 1.0;        // fraction of the circle drawn (fan uses < 1)
uniform float arc_dir = 0.0;    // radians, centre of the arc
uniform float inner = 0.5;      // trailing inner ring strength
uniform float ease_pow = 2.0;
uniform float fade = 0.6;
uniform int dashes = 0;         // >0 cuts the ring into this many dashes
uniform float wob = 0.0;        // px radius wobble
uniform float wob_freq = 8.0;   // wobble cycles around the ring
uniform float wob_jag = 0.0;    // 1 = piecewise-linear (lightning) wobble

void fragment() {
	vec2 p = pix(UV);
	p.y /= squash;
	float R = min(fsize.x, fsize.y / squash) * 0.5 - 1.5;
	float e = 1.0 - pow(1.0 - t, ease_pow);
	float d = length(p);
	float ang = atan(p.y, p.x);
	float da = abs(mod(ang - arc_dir + TAU * 1.5, TAU) - TAU * 0.5);
	float arcm = arc >= 0.999 ? 1.0 : 1.0 - smoothstep(arc * TAU * 0.5 - 0.08, arc * TAU * 0.5, da);
	float r = mix(r0, r1, e) * R;
	if (wob > 0.0) {
		// sample on a circle so the wobble has no seam at +-PI
		vec2 cs = vec2(cos(ang), sin(ang)) * wob_freq / TAU * 1.0;
		float wn = mix(vnoise(cs + vec2(seed, t * 3.0)) * 2.0 - 1.0,
			lnoise((ang + PI) * wob_freq * 0.5 + t * 7.0 + seed) * (1.0 - smoothstep(PI - 0.3, PI, abs(ang))) , wob_jag);
		r += wn * wob;
	}
	float th = mix(th0, th1, t);
	float a = 0.0;
	float h = 0.0;
	float band = 1.0 - smoothstep(th * 0.5 - 0.5, th * 0.5 + 0.5, abs(d - r));
	vec2 cdir = vec2(cos(ang), sin(ang));
	float bn = vnoise(cdir * 1.6 + vec2(seed, t * 4.0)) * 0.6 + vnoise(cdir * 4.5 + vec2(seed * 2.0, 3.0)) * 0.4;
	float keep = smoothstep(breakup * t - 0.05, breakup * t + 0.05, bn);
	if (dashes > 0) keep *= step(0.32, fract((ang + PI) / TAU * float(dashes) + t * 0.08));
	band *= keep;
	band *= arcm;
	if (band > 0.0) {
		a = max(a, band);
		float k = abs(d - r) / max(th * 0.5, 0.5);
		float edgeh = (d < r) ? 0.65 : 0.75;
		h = max(h, mix(1.0 - 0.25 * t, edgeh - 0.2 * t, k));
	}
	float g = exp(-abs(d - r) / (th * 0.45 + 0.8)) * glow * 0.45 * arcm * (1.0 - 0.5 * t) * mix(0.25, 1.0, keep);
	if (g > a) { a = g; h = max(h, 0.62); }
	if (inner > 0.0) {
		float ri = r * 0.82;
		float b2 = (1.0 - smoothstep(0.3, 0.9, abs(d - ri))) * inner * (1.0 - t) * arcm;
		if (b2 > a) { a = b2; h = max(h, 0.6); }
	}
	if (fill > 0.0 && d < r) {
		float fm = fill * (0.6 + 0.4 * fbm(p * 0.08 + seed)) * pow(smoothstep(0.0, r, d), 3.0) * arcm;
		if (fm > a) { a = fm; h = max(h, 0.35); }
	}
	for (int i = 0; i < 32; i++) {
		if (i >= ticks) break;
		float ta = float(i) / float(ticks) * TAU + t * 0.6;
		vec2 dir = vec2(cos(ta), sin(ta));
		float al = dot(p, dir);
		float pe = abs(dot(p, vec2(-dir.y, dir.x)));
		float m = step(pe, 0.6) * step(r + th * 0.5 + 1.0, al) * step(al, r + th * 0.5 + 3.0) * arcm;
		if (m > 0.0) { a = max(a, m * (1.0 - 0.5 * t)); h = max(h, 0.8); }
	}
	for (int i = 0; i < 32; i++) {
		if (i >= debris) break;
		float fi = float(i);
		float ta = h11(fi + 3.0) * TAU;
		float rr = r * (0.9 + 0.35 * h11(fi + 9.0)) + 2.0 * t;
		vec2 c2 = vec2(cos(ta), sin(ta)) * rr;
		float sz = mix(1.3, 0.6, t) * (0.6 + 0.8 * h11(fi + 17.0));
		float m = (1.0 - smoothstep(sz - 0.4, sz + 0.4, length(p - c2))) * arcm;
		if (m > 0.0) { a = max(a, m); h = max(h, 0.7 + 0.25 * h11(fi)); }
	}
	a *= mix(1.0, 1.0 - fade, smoothstep(0.55, 1.0, t));
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
