// Shared helpers for every FX shader.
// Output convention (read by post.py):
//   R = heat 0..1 (index into the job's colour ramp, dark -> light)
//   G = ramp selector (0 = main ramp, 1 = secondary ramp)
//   A = coverage alpha (straight, not premultiplied)
// Coordinates: p is in TARGET pixels, origin at frame centre, +y down.

uniform float t = 0.0;          // 0..1 over the sheet (or loop phase)
uniform int frame = 0;
uniform int frames = 1;
uniform float seed = 1.0;
uniform vec2 fsize = vec2(32.0, 32.0); // target frame size in pixels


// Integer hashes: sin()-based hashes break up when the compiler reassociates the
// argument (lightning paths got seams), so everything goes through a uint mixer.
uint hmix(uint x) {
	x ^= x >> 16u; x *= 2146121005u;
	x ^= x >> 15u; x *= 2221713035u;
	x ^= x >> 16u;
	return x;
}
uint hseed() { return hmix(uint(int(floor(seed * 1000.0)) + 77777)); }
float h11(float x) {
	return float(hmix(uint(int(floor(x * 16.0)) + 1048576) ^ hseed()) & 16777215u) / 16777215.0;
}
float h21(vec2 p) {
	uint a = uint(int(floor(p.x)) + 1048576);
	uint b = uint(int(floor(p.y)) + 1048576);
	return float(hmix(a ^ hmix(b ^ hseed())) & 16777215u) / 16777215.0;
}
vec2 h22(vec2 p) {
	return vec2(h21(p), h21(p + vec2(531.0, 173.0)));
}
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), u.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) {
	float a = 0.5;
	float s = 0.0;
	for (int i = 0; i < 5; i++) { s += a * vnoise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
	return s;
}
// 1-D linear noise: piecewise-linear, gives jagged lightning paths.
float lnoise(float x) {
	float i = floor(x);
	float f = fract(x);
	return mix(h11(i) * 2.0 - 1.0, h11(i + 1.0) * 2.0 - 1.0, f);
}
float sd_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float k = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * k);
}
mat2 rot(float a) { return mat2(vec2(cos(a), sin(a)), vec2(-sin(a), cos(a))); }
// Plus-shaped sparkle (the "+" motif used across the v2 set). r = arm length.
float plus_mask(vec2 p, float r, float w) {
	vec2 a = abs(p);
	float arm = max(step(a.x, w) * step(a.y, r), step(a.y, w) * step(a.x, r));
	return arm;
}
// 4-point star (soft). Returns 0..1.
float star4(vec2 p, float r, float thin) {
	vec2 a = abs(p) / r;
	float d = pow(a.x, thin) + pow(a.y, thin);
	float s = 1.0 - smoothstep(0.0, 1.0, min(a.x * a.y * 8.0 + min(a.x, a.y), 1.0)) ;
	return clamp(s * (1.0 - max(a.x, a.y)), 0.0, 1.0);
}
vec2 pix(vec2 uv) { return (uv - 0.5) * fsize; }
