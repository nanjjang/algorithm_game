// Ball lightning: churning orb with a bright core, crackling arcs crawling over the rim
// and short zaps leaping outward. Arcs re-roll every frame (flicker), the orb noise loops.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float radius = 0.26;   // fraction of min frame size
uniform int arcs = 4;
uniform float arc_len = 0.42;  // fraction of min frame size
uniform float halo = 0.5;

void fragment() {
	vec2 p = pix(UV);
	float M = min(fsize.x, fsize.y);
	float r = M * radius;
	float d = length(p);
	vec2 lp = vec2(cos(t * TAU), sin(t * TAU)) * 1.5;
	float a = 0.0;
	float h = 0.0;
	if (d < r + 0.5) {
		float n = fbm(p / r * 1.4 + lp);
		vec2 nn = p / max(r, 0.01);
		float lit = clamp(dot(nn, normalize(vec2(-0.7, -0.9))) * 0.5 + 0.5, 0.0, 1.0);
		float hh = 0.55 + 0.3 * n + 0.15 * lit;
		float vein = 1.0 - smoothstep(0.0, 0.07, abs(n - 0.5));
		hh = max(hh, vein * 0.95);
		float rimk = smoothstep(0.72, 0.95, d / r);
		hh = max(hh, rimk * 0.9);
		if (d < r * 0.32) hh = 1.0;
		a = 1.0 - smoothstep(r - 0.5, r + 0.5, d);
		h = hh;
	}
	float g = exp(-max(d - r, 0.0) / (M * 0.07 + 0.6)) * halo;
	if (g > a) { a = g; h = max(h, 0.6); }
	float fs = float(frame) * 7.31;
	for (int i = 0; i < 8; i++) {
		if (i >= arcs) break;
		float fi = float(i);
		float a0 = (fi / float(arcs) + h11(fi + fs + 1.0) * 0.6) * TAU;
		float Lr = M * arc_len * (0.55 + 0.45 * h11(fi + fs + 4.0));
		vec2 dir = vec2(cos(a0), sin(a0));
		vec2 nrm = vec2(-dir.y, dir.x);
		float bend = (h11(fi + fs + 8.0) - 0.5) * Lr * 0.5;
		float al = dot(p, dir) - r * 0.8;
		if (al > 0.0 && al < Lr) {
			float k = al / Lr;
			float off = lnoise(al * 0.35 + fi * 9.0 + fs) * 3.2 * k + bend * k * k;
			float pe = abs(dot(p, nrm) - off);
			float w = mix(1.3, 0.55, k);
			float m = 1.0 - smoothstep(w * 0.5, w * 0.5 + 0.55, pe);
			if (m > 0.0) { a = max(a, m); h = max(h, mix(1.0, 0.8, k)); }
			float bg = exp(-pe / 1.2) * 0.35 * (1.0 - k);
			if (bg > a) { a = bg; h = max(h, 0.62); }
		}
		vec2 tipc = dir * (r * 0.8 + Lr) + nrm * bend;
		float pm = plus_mask(p - tipc, 1.5, 0.5) * step(0.5, h11(fi + fs + 12.0));
		if (pm > 0.0) { a = max(a, pm); h = max(h, 0.95); }
	}
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
