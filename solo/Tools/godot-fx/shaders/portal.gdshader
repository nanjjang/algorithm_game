// Ground portal: squashed ellipse with spiral arms, bright rim and rising motes. Loops.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform float radius = 0.44;
uniform float squash = 0.55;
uniform int arms = 3;
uniform float twist = 2.6;
uniform int motes = 5;
uniform float cy = 0.12;        // centre y offset (fraction of height)

void fragment() {
	vec2 p = pix(UV);
	float M = min(fsize.x, fsize.y);
	float R = M * radius;
	vec2 c = vec2(0.0, fsize.y * cy);
	vec2 q = (p - c) / vec2(1.0, squash);
	float d = length(q) / R;
	float ang = atan(q.y, q.x);
	float a = 0.0;
	float h = 0.0;
	if (d < 1.0) {
		float sp = sin(ang * float(arms) + log(max(d, 0.02)) * twist * 3.0 - t * TAU);
		float body = smoothstep(-0.1, 0.6, sp);
		float hh = mix(0.18, 0.62, body) + (1.0 - d) * 0.15;
		float rim = smoothstep(0.78, 0.92, d);
		hh = mix(hh, 0.95, rim);
		if (d < 0.12) hh = 0.9;
		a = 1.0 - smoothstep(0.96, 1.0, d);
		a *= mix(0.85, 1.0, rim);
		h = hh;
	}
	float g = exp(-max(d - 1.0, 0.0) * R / 2.0) * 0.5;
	if (g > a) { a = g; h = max(h, 0.6); }
	for (int i = 0; i < 10; i++) {
		if (i >= motes) break;
		float fi = float(i);
		float ph = fract(h11(fi + 1.0) + t);
		float ma = h11(fi + 4.0) * TAU;
		vec2 mc = c + vec2(cos(ma) * R * 0.7, sin(ma) * R * 0.7 * squash) - vec2(0.0, ph * fsize.y * 0.45);
		float m = fi < 2.0 ? plus_mask(p - mc, 1.5, 0.5) : 1.0 - smoothstep(0.4, 0.9, length(p - mc));
		if (m > 0.0) { a = max(a, m * (1.0 - ph * 0.6)); h = max(h, 0.88); }
	}
	COLOR = vec4(h, 0.0, 0.0, a);
}
