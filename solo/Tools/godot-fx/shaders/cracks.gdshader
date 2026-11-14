// Ground cracks / stains: radial fissures plus Voronoi crack network, fading with distance.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float reach = 0.48;     // fraction of the frame half-size
uniform int fissures = 6;
uniform float cell = 7.0;       // voronoi cell size px
uniform float web = 0.7;        // strength of the crack network
uniform float stain = 0.35;     // dark stain fill under the cracks
uniform float grow_a = 1.0;     // reach multiplier on frame 0
uniform float glow_edge = 0.0;  // bright lips on the cracks (corrosion / ember)
uniform float crack_h = 0.12;   // heat of the crack lines

void fragment() {
	vec2 p = pix(UV);
	vec2 hs = fsize * 0.5 - vec2(1.0);
	vec2 q = p / hs;
	float rr = reach * mix(grow_a, 1.0, t);
	float d = length(q) / rr;
	float a = 0.0;
	float h = 0.0;
	// voronoi F2-F1
	vec2 g = p / cell;
	vec2 gi = floor(g);
	float f1 = 9.0;
	float f2 = 9.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 o = vec2(float(x), float(y));
			vec2 c = o + h22(gi + o);
			float dd = length(g - gi - c);
			if (dd < f1) { f2 = f1; f1 = dd; } else if (dd < f2) { f2 = dd; }
		}
	}
	float edge = f2 - f1;
	float fall = 1.0 - smoothstep(0.55, 1.0, d + (vnoise(p * 0.15) - 0.5) * 0.3);
	float wm = (1.0 - smoothstep(0.06, 0.12, edge)) * web * fall;
	// radial fissures
	float ang = atan(p.y, p.x);
	float fm = 0.0;
	for (int i = 0; i < 12; i++) {
		if (i >= fissures) break;
		float fi = float(i);
		float fa = fi / float(fissures) * TAU + (h11(fi) - 0.5) * 0.7;
		vec2 dir = vec2(cos(fa), sin(fa));
		float al = dot(p, dir);
		float L = length(hs * dir) * rr * (0.75 + 0.35 * h11(fi + 4.0)) * 1.15;
		float pe = abs(dot(p, vec2(-dir.y, dir.x)) + lnoise(al * 0.35 + fi * 5.0) * 1.8);
		float w = mix(1.6, 0.4, clamp(al / L, 0.0, 1.0));
		fm = max(fm, step(0.0, al) * step(al, L) * (1.0 - smoothstep(w * 0.5, w * 0.5 + 0.6, pe)));
	}
	float cm = max(wm, fm);
	float st = stain * (1.0 - smoothstep(0.2, 0.9, d)) * (0.6 + 0.4 * fbm(p * 0.1 + seed));
	if (st > 0.0) { a = st; h = 0.35; }
	if (cm > 0.05) { a = max(a, cm); h = crack_h; }
	if (glow_edge > 0.0) {
		float lip = (1.0 - smoothstep(0.12, 0.22, edge)) * web * fall * (1.0 - cm);
		if (lip > 0.3) { a = max(a, lip * glow_edge); h = max(h, 0.7); }
	}
	COLOR = vec4(h, 0.0, 0.0, clamp(a, 0.0, 1.0));
}
