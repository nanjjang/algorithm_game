// Glowing orb / mote / bullet: shaded sphere with top-left highlight, rim and halo.
// star > 0 adds a 4-point glint; ring > 0 adds a thin orbiting ring; pulse animates per frame.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float radius = 0.3;     // fraction of min frame size
uniform float pulse = 0.15;
uniform float halo = 0.6;
uniform float star = 0.0;
uniform float ring = 0.0;
uniform float hollow = 0.0;     // 1 = bright rim, darker centre (enemy bullet with core)
uniform float shrink = 0.0;     // shrink over the sheet (fading motes)
uniform float flat_shade = 0.0;
uniform float mid = 0.75;       // body heat

void fragment() {
	vec2 p = pix(UV);
	float M = min(fsize.x, fsize.y);
	float r = M * radius * (1.0 + pulse * sin(t * TAU)) * (1.0 - shrink * t);
	float d = length(p);
	float a = 0.0;
	float h = 0.0;
	float m = 1.0 - smoothstep(r - 0.5, r + 0.5, d);
	if (m > 0.0) {
		vec2 n = p / max(r, 0.01);
		float lit = clamp(dot(n, normalize(vec2(-0.7, -0.9))) * 0.5 + 0.5, 0.0, 1.0);
		float hh = mix(mid - 0.2, mid + 0.1, mix(lit, 0.5, flat_shade));
		float spec = 1.0 - smoothstep(0.0, 0.38, length(n - vec2(-0.32, -0.36)));
		hh = max(hh, spec);
		if (hollow > 0.0) {
			float rim = smoothstep(0.55, 0.8, d / r);
			hh = mix(1.0, hh, rim * hollow);
		}
		a = max(a, m);
		h = max(h, hh);
	}
	float g = exp(-max(d - r, 0.0) / (M * 0.08 + 0.6)) * halo * (1.0 - 0.3 * t);
	if (g > a) { a = g; h = max(h, 0.6); }
	if (star > 0.0) {
		vec2 ap = abs(p);
		float arm = M * 0.48 * star * (1.0 + 0.25 * sin(t * TAU + 1.0));
		float s = max((1.0 - smoothstep(0.4, 0.9, ap.y)) * (1.0 - ap.x / arm), (1.0 - smoothstep(0.4, 0.9, ap.x)) * (1.0 - ap.y / arm));
		if (s > 0.0) { a = max(a, clamp(s * 2.0, 0.0, 1.0)); h = max(h, 0.95); }
	}
	if (ring > 0.0) {
		vec2 q = rot(t * TAU * 0.25) * p;
		q.y *= 2.6;
		float rd = abs(length(q) - r * 1.55);
		float rm = (1.0 - smoothstep(0.3, 0.9, rd)) * ring;
		if (rm > 0.0 && !(q.y < 0.0 && d < r)) { a = max(a, rm); h = max(h, 0.85); }
	}
	COLOR = vec4(h, 0.0, 0.0, a);
}
