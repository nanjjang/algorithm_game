// Enemy attack warning area on the ground: translucent textured fill + bright rim.
// mode 0 circle, 1 lane (horizontal rect), 2 fan (sector opening to the right).
// The fill "charges" from the centre outwards over the sheet.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform int mode = 0;
uniform float size_a = 0.6;     // footprint scale on frame 0 (fraction of max)
uniform float size_b = 1.0;     // on the last frame
uniform float fan_angle = 0.9;  // half angle (mode 2)
uniform float rim_w = 1.6;
uniform float fill = 0.35;
uniform float charge = 1.0;     // 1 = inner charge front grows with t
uniform float hatch = 1.0;
uniform int glints = 0;
uniform float glint_sel = 0.0;  // ramp selector for glints
uniform float fill_sel = 0.0;   // ramp selector for the fill texture

void fragment() {
	vec2 p = pix(UV);
	float sc = mix(size_a, size_b, t);
	float dist;      // signed distance to the outline in px (negative inside)
	float prog;      // 0 centre .. 1 outline
	if (mode == 0) {
		float R = (min(fsize.x, fsize.y) * 0.5 - 1.5) * sc;
		dist = length(p) - R;
		prog = length(p) / R;
	} else if (mode == 1) {
		vec2 hs = (fsize * 0.5 - vec2(1.5)) * vec2(1.0, sc);
		vec2 dd = abs(p) - hs;
		dist = max(dd.x, dd.y);
		prog = (p.x + hs.x) / (2.0 * hs.x);
	} else {
		vec2 o = vec2(-fsize.x * 0.5 + 2.0, 0.0);
		vec2 q = p - o;
		float R = (fsize.x - 4.0) * sc;
		float ang = abs(atan(q.y, q.x));
		dist = max(length(q) - R, (ang - fan_angle) * length(q));
		prog = length(q) / R;
	}
	float a = 0.0;
	float h = 0.0;
	float s = 0.0;
	if (dist < 0.0) {
		float n = fbm(p * 0.12 + seed);
		float hl = hatch * step(0.5, fract((p.x + p.y) / 5.0));
		float n2 = fbm(p * 0.6 + seed * 3.0);
		float fa = fill * (0.45 + 0.55 * smoothstep(0.2, 0.8, n)) * (0.75 + 0.25 * hl) * (1.0 - 0.5 * smoothstep(0.6, 1.0, prog));
		float hh = 0.6 + 0.35 * n2;
		if (charge > 0.0 && prog < t * 1.02) { fa = max(fa, fill * 1.6); hh = 0.5 + 0.2 * n; }
		float front = charge * (1.0 - smoothstep(0.0, 0.05, abs(prog - t))) * step(0.02, t);
		if (front > 0.0) { fa = max(fa, 0.85 * front); hh = max(hh, 0.8); }
		a = fa;
		h = hh;
		s = fill_sel;
	}
	float rim = 1.0 - smoothstep(rim_w * 0.5 - 0.4, rim_w * 0.5 + 0.4, abs(dist + rim_w * 0.5));
	if (rim > 0.0) { a = max(a, rim); h = max(h, dist > -rim_w * 0.3 ? 0.75 : 0.95); s = 0.0; }
	float g = exp(-abs(dist) / 2.5) * 0.4;
	if (g > a) { a = g; h = max(h, 0.6); s = 0.0; }
	for (int i = 0; i < 24; i++) {
		if (i >= glints) break;
		float fi = float(i);
		vec2 c = (h22(vec2(fi, 3.0)) - 0.5) * fsize * 0.85;
		float tw = 0.5 + 0.5 * sin(t * TAU + fi * 1.7);
		float m = plus_mask(p - c, 1.0 + tw * 1.2, 0.5);
		if (m > 0.0 && tw > 0.25) { a = max(a, m); h = max(h, 0.7 + 0.3 * tw); s = glint_sel; }
	}
	COLOR = vec4(h, s, 0.0, a);
}
