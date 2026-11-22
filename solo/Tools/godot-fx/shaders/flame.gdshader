// Looping fire: teardrop silhouette eaten by upward-scrolling noise. Noise is sampled on a
// circle in time so the last frame flows back into the first.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float horiz = 0.0;      // 1 = flame trails to the left (fireball flying right)
uniform float base = 0.85;      // base y (fraction of height from top)
uniform float radius = 0.3;     // base half-width, fraction of width
uniform float height = 0.8;     // flame height, fraction of height
uniform float turb = 0.55;
uniform float scale = 1.0;
uniform int embers = 3;
uniform float core_ball = 0.0;  // bright ball at the base (fireball head)
uniform int tongues = 0;        // extra side tongues (campfire)
uniform float envelope = 0.0;   // 1 = flare up then die down over the sheet (not looping)

void fragment() {
	vec2 p = pix(UV);
	vec2 fs = fsize;
	if (horiz > 0.5) { p = vec2(-p.y, p.x); fs = fsize.yx; }
	// now flame rises towards -y
	float yb = (base - 0.5) * fs.y;
	float Hh = height * fs.y * mix(1.0, sin(PI * (0.15 + 0.85 * t)) * 0.85 + 0.15, envelope);
	float R = radius * fs.x;
	vec2 lp = vec2(cos(t * TAU), sin(t * TAU)) * 1.3;
	float n = fbm(vec2(p.x * 0.11 * scale, p.y * 0.07 * scale) + lp);
	float n2 = fbm(vec2(p.x * 0.2 * scale, p.y * 0.14 * scale) + lp * 1.7 + 5.0);
	vec2 q = vec2(p.x / R, (yb - p.y) / Hh);    // q.y 0 at base, 1 at tip
	// licks: sideways warp that grows with height
	q.x += (n - 0.5) * 1.4 * clamp(q.y, 0.0, 1.0);
	float wy = mix(1.0, 0.0, clamp(q.y, 0.0, 1.0));
	float shape = 1.0 - (abs(q.x) / max(pow(wy, 0.75), 0.001));
	shape = min(shape, smoothstep(-0.2, 0.06, q.y) * 1.5);
	float side = -1.0;
	for (int i = 0; i < 4; i++) {
		if (i >= tongues) break;
		float fi = float(i);
		float sx = (mod(fi, 2.0) < 0.5 ? -1.0 : 1.0) * (0.55 + 0.12 * floor(fi / 2.0));
		float sh = 0.6 - 0.12 * floor(fi / 2.0) - 0.08 * mod(fi, 2.0);
		vec2 tq = vec2((q.x - sx) / 0.42, q.y / sh);
		float tw = mix(1.0, 0.0, clamp(tq.y, 0.0, 1.0));
		float ts = 1.0 - abs(tq.x) / max(pow(tw, 0.75), 0.001);
		ts = min(ts, smoothstep(-0.2, 0.06, tq.y) * 1.5);
		side = max(side, ts * 0.85);
	}
	shape = max(shape, side);
	// noise only erodes the silhouette
	float v = shape - n2 * turb * (0.25 + 0.75 * clamp(q.y, 0.0, 1.0)) * 0.9;
	float a = smoothstep(0.0, 0.05, v) * step(-0.12, q.y);
	float h = clamp(0.18 + v * 1.0 - q.y * 0.35, 0.0, 1.0);
	if (core_ball > 0.0) {
		float d = length(vec2(p.x, p.y - yb + R * 0.2)) / (R * core_ball);
		if (d < 1.0) { a = 1.0; h = max(h, mix(1.0, 0.8, d)); }
	}
	for (int i = 0; i < 8; i++) {
		if (i >= embers) break;
		float fi = float(i);
		float ph = fract(h11(fi + 2.0) + t);
		vec2 c = vec2((h11(fi + 5.0) - 0.5) * R * 2.0 + sin(ph * 6.0 + fi) * 1.5, yb - ph * Hh * 1.2);
		float m = 1.0 - smoothstep(0.4, 0.9, length(p - c));
		if (m > 0.0) { a = max(a, m * (1.0 - ph)); h = max(h, 0.8); }
	}
	COLOR = vec4(h, 0.0, 0.0, a);
}
