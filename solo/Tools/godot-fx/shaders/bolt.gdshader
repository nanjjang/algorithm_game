// Lightning bolt along an axis (vertical by default) with branches, glow and a ground splash.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float horiz = 0.0;
uniform float amp = 6.0;        // px
uniform float freq = 0.12;      // per px
uniform float thick = 2.0;
uniform float glow_w = 3.0;
uniform int branches = 2;
uniform float splash = 0.0;     // ground splash radius at the far end (px)
uniform float taper = 0.0;      // 1 = thins towards the start
uniform float fade = 0.0;       // extra fade on later frames
uniform float tile = 0.0;       // 1 = path returns to centre at both ends (vertically repeating strip)

float path(float s, float fseed) {
	return lnoise(s * freq + fseed) * 0.7 + lnoise(s * freq * 2.7 + fseed * 3.0 + 17.0) * 0.3;
}

void fragment() {
	vec2 p = pix(UV);
	vec2 fs = fsize;
	if (horiz > 0.5) { p = vec2(p.y, p.x); fs = fsize.yx; }
	float half_len = fs.y * 0.5;
	float s = p.y + half_len;
	float fseed = float(frame) * 5.3;
	float edge = smoothstep(0.0, 6.0, s) * smoothstep(0.0, 6.0, fs.y - s);
	float x = path(s, fseed) * amp * mix(1.0, edge, mix(0.6, 1.0, tile));
	float d = abs(p.x - x);
	float th = thick * mix(1.0, 0.4 + 0.6 * s / fs.y, taper) * (1.0 - 0.35 * t);
	float a = 0.0;
	float h = 0.0;
	float core = 1.0 - smoothstep(th * 0.5 - 0.35, th * 0.5 + 0.35, d);
	if (core > 0.0) { a = max(a, core); h = max(h, d < th * 0.25 ? 1.0 : 0.88); }
	float g = exp(-d / glow_w) * 0.38 * (1.0 - 0.4 * t);
	if (g > a) { a = g; h = max(h, 0.62); }
	for (int i = 0; i < 6; i++) {
		if (i >= branches) break;
		float fi = float(i);
		float s0 = fs.y * (0.2 + 0.55 * h11(fi + fseed + 2.0));
		float bl = fs.y * (0.15 + 0.15 * h11(fi + 5.0));
		float side = h11(fi + fseed + 9.0) < 0.5 ? -1.0 : 1.0;
		if (s > s0 && s < s0 + bl) {
			float k = (s - s0) / bl;
			float bx = path(s0, fseed) * amp * mix(1.0, smoothstep(0.0, 6.0, s0) * smoothstep(0.0, 6.0, fs.y - s0), mix(0.6, 1.0, tile)) + side * k * amp * 1.4 + lnoise(s * 0.4 + fi * 9.0) * 1.5 * k;
			float bd = abs(p.x - bx);
			float bt = th * 0.6 * (1.0 - k);
			float m = 1.0 - smoothstep(bt * 0.5 - 0.3, bt * 0.5 + 0.4, bd);
			if (m > 0.0) { a = max(a, m); h = max(h, 0.8); }
			float bg = exp(-bd / (glow_w * 0.6)) * 0.35 * (1.0 - k);
			if (bg > a) { a = bg; h = max(h, 0.55); }
		}
	}
	if (splash > 0.0) {
		vec2 sp = vec2(p.x - x, (fs.y - s) * 2.2);
		float sd = length(sp);
		float sr = splash * (0.6 + 0.6 * t);
		float m = 1.0 - smoothstep(sr - 0.6, sr + 0.6, sd);
		if (m > a * 0.8) { a = max(a, m * 0.9); h = max(h, mix(1.0, 0.7, sd / sr)); }
	}
	a *= 1.0 - fade * t;
	COLOR = vec4(h, 0.0, 0.0, a);
}
