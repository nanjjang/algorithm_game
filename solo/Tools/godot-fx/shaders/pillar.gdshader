// Vertical light column rising from a base glow: tapered beam, scrolling streaks, rising motes.
// flame > 0 turns it into a fire column, bolt > 0 adds a crackling lightning core.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float width = 0.35;     // half-width at the base, fraction of frame width
uniform float tip = 0.05;       // y (fraction of height from top) where the tip is
uniform float base = 0.94;      // y (fraction of height from top) of the base
uniform float grow0 = 1.0;      // height scale at t=0 (grows to 1)
uniform float widen = 0.0;      // width pulse over the sheet
uniform int motes = 6;
uniform float flame = 0.0;
uniform float bolt = 0.0;
uniform float base_glow = 1.0;
uniform float streaks = 1.0;
uniform float fade_end = 0.0;   // fades the last frames

void fragment() {
	vec2 p = pix(UV);
	float H = fsize.y;
	float W = fsize.x;
	float yb = (base - 0.5) * H;
	float hgt = (base - tip) * H * mix(grow0, 1.0, smoothstep(0.0, 0.6, t));
	float k = clamp((yb - p.y) / hgt, 0.0, 1.5);   // 0 at base, 1 at tip
	float a = 0.0;
	float h = 0.0;
	float wb = W * width * (1.0 + widen * sin(t * PI));
	float wk = wb * pow(max(1.0 - k, 0.0), 0.65);
	float x = p.x;
	if (flame > 0.0) {
		x += (fbm(vec2(p.y * 0.06, t * 3.0 + seed)) - 0.5) * W * 0.25 * flame * k;
	}
	if (bolt > 0.0) {
		x -= lnoise(p.y * 0.08 + float(frame) * 4.1) * W * 0.06 * bolt;
	}
	float ax = abs(x);
	if (p.y <= yb + 1.0 && k < 1.0) {
		float n = vnoise(vec2(p.x * 0.25, p.y * 0.035 + t * 3.0 + seed));
		float edgeN = flame > 0.0 ? (fbm(vec2(p.x * 0.08, p.y * 0.05 + t * 2.5)) - 0.5) * wb * 0.9 * flame : 0.0;
		float m = 1.0 - smoothstep(wk - 0.7 + edgeN, wk + 0.7 + edgeN, ax);
		if (m > 0.0) {
			float prof = 1.0 - ax / max(wk + 1.0, 1.0);
			float hh = mix(0.5, 1.0, pow(clamp(prof, 0.0, 1.0), 1.5));
			hh += (n - 0.5) * 0.3 * streaks;
			hh -= k * 0.15;
			a = max(a, m * (1.0 - smoothstep(0.8, 1.0, k) * 0.7));
			h = max(h, clamp(hh, 0.0, 1.0));
		}
		float g = exp(-max(ax - wk, 0.0) / (wb * 0.25 + 1.0)) * 0.28 * (1.0 - k);
		if (g > a) { a = g; h = max(h, 0.55); }
	}
	// base glow ellipse
	if (base_glow > 0.0) {
		vec2 q = vec2(p.x / (wb * 1.6 + 2.0), (p.y - yb) / (wb * 0.45 + 1.5));
		float bd = length(q);
		float bm = 1.0 - smoothstep(0.8, 1.0, bd);
		if (bm > 0.0) { a = max(a, bm * base_glow); h = max(h, mix(0.95, 0.6, bd)); }
	}
	// rising motes ("+" sparkles and dots)
	for (int i = 0; i < 16; i++) {
		if (i >= motes) break;
		float fi = float(i);
		float ph = fract(h11(fi + 1.0) + t * 0.9);
		vec2 c = vec2((h11(fi + 6.0) - 0.5) * W * 0.8, yb - ph * hgt * 0.95);
		float m = (fi < 3.0) ? plus_mask(p - c, 1.6, 0.5) : 1.0 - smoothstep(0.5, 1.0, length(p - c));
		if (m > 0.0) { a = max(a, m * (1.0 - ph * 0.5)); h = max(h, 0.85); }
	}
	a *= 1.0 - fade_end * smoothstep(0.6, 1.0, t);
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
