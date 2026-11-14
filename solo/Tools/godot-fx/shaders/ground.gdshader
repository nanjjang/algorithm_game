// Seamless 64x64 ground textures for the world tile sheets (tiles.py composites them).
// Everything is periodic over the frame so any two cells of the same material meet
// without a seam; variants only add decoration away from the cell border.
// mat: 0 grass, 1 dirt, 2 packed earth, 3 paving stones, 4 slate flagstones.
shader_type canvas_item;
render_mode blend_disabled;
#include "res://shaders/common.gdshaderinc"

uniform int mat = 0;
uniform int variant = 0;

float hp(vec2 i, float per) { return h21(mod(i, per)); }
float pnoise(vec2 p, float per) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hp(i, per), hp(i + vec2(1.0, 0.0), per), u.x), mix(hp(i + vec2(0.0, 1.0), per), hp(i + vec2(1.0, 1.0), per), u.x), u.y);
}
// periodic fbm: p in tile pixels, base cell size c (must divide 64)
float pfbm(vec2 p, float c) {
	float per = fsize.x / c;
	vec2 q = p / c;
	float a = 0.5;
	float s = 0.0;
	for (int i = 0; i < 4; i++) { s += a * pnoise(q, per); q *= 2.0; per *= 2.0; a *= 0.5; }
	return s / 0.9375;
}
vec2 wrapd(vec2 d) { return d - fsize * round(d / fsize); }

// decoration seeds only fire inside the margin, so borders stay identical between variants
bool interior(vec2 c) { return c.x > 8.0 && c.x < fsize.x - 8.0 && c.y > 8.0 && c.y < fsize.y - 8.0; }

float grass(vec2 p) {
	float h = 0.42 + (pfbm(p, 16.0) - 0.5) * 0.28 + (pfbm(p + 17.0, 4.0) - 0.5) * 0.12;
	// blade ticks on a 4px grid
	vec2 g = floor(p / 4.0);
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 cell = mod(g + vec2(float(x), float(y)), fsize / 4.0);
			vec2 j = vec2(hp(cell, 99.0), hp(cell + 31.0, 99.0));
			if (j.x < 0.25) continue;
			vec2 base = cell * 4.0 + vec2(j.y * 3.0 + 0.5, 3.5);
			vec2 d = wrapd(p - base);
			float len = 2.0 + j.x * 2.5;
			float lean = (j.y - 0.5) * 0.8;
			if (d.y <= 0.0 && d.y > -len && abs(d.x - lean * (-d.y) / len) < 0.6) {
				float k = -d.y / len;
				h = max(h, 0.5 + 0.28 * k + 0.08 * j.x);
			}
			if (d.y > 0.0 && d.y < 1.2 && abs(d.x) < 1.0) h = min(h, 0.3);
		}
	}
	return h;
}

float dirt(vec2 p, float stones) {
	float h = 0.45 + (pfbm(p, 16.0) - 0.5) * 0.2 + (pfbm(p + 5.0, 2.0) - 0.5) * 0.07;
	vec2 g = floor(p / 8.0);
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 cell = mod(g + vec2(float(x), float(y)), fsize / 8.0);
			vec2 j = vec2(hp(cell + 7.0, 99.0), hp(cell + 51.0, 99.0));
			if (j.x > stones) continue;
			vec2 c = cell * 8.0 + 2.0 + vec2(j.y, fract(j.y * 7.3)) * 4.0;
			vec2 d = wrapd(p - c);
			float r = 0.9 + j.x / max(stones, 0.01) * 1.3;
			float e = length(d * vec2(1.0, 1.3));
			if (e < r) {
				float lit = dot(normalize(d + vec2(0.001)), normalize(vec2(-1.0, -1.0)));
				h = 0.55 + 0.2 * lit;
			} else if (e < r + 1.0 && d.y > 0.0) {
				h = min(h, 0.25);
			}
		}
	}
	return h;
}

// rounded blocks with bevel; bw/bh block size, offset rows
float blocks(vec2 p, float bw, float bh, float gap, float crack) {
	float row = floor(p.y / bh);
	float off = mod(row, 2.0) * bw * 0.5;
	vec2 q = vec2(mod(p.x + off, fsize.x), p.y);
	vec2 id = vec2(floor(q.x / bw), row);
	vec2 lp = vec2(mod(q.x, bw), mod(q.y, bh)) - vec2(bw, bh) * 0.5;
	float jit = hp(id, 99.0);
	vec2 hs = vec2(bw, bh) * 0.5 - gap;
	vec2 dd = abs(lp) - hs + 2.0;
	float sd = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - 2.0;
	float n = pfbm(p, 8.0);
	float h = 0.42 + (jit - 0.5) * 0.16 + (n - 0.5) * 0.18;
	if (sd > 0.0) return 0.14 + (n - 0.5) * 0.08;   // mortar
	// bevel: light top-left edge, dark bottom-right edge
	float edge = 1.0 - smoothstep(0.0, 1.6, -sd);
	vec2 nrm = normalize(lp / (hs + 0.001) + vec2(0.0001));
	float lit = dot(nrm, normalize(vec2(-1.0, -1.0)));
	h += edge * lit * 0.22;
	// hairline cracks
	if (crack > 0.0) {
		float c = abs(pfbm(p * 1.0 + jit * 30.0, 8.0) - 0.5);
		if (c < 0.018 && jit < crack) h = 0.2;
	}
	return h;
}

void fragment() {
	vec2 p = UV * fsize;
	float h = 0.5;
	if (mat == 0) {
		h = grass(p);
		if (variant > 0) {
			// a small tuft of brighter grass / clover in the interior
			vec2 c = vec2(16.0 + h11(float(variant) * 3.0) * 32.0, 16.0 + h11(float(variant) * 5.0) * 32.0);
			float d = length(p - c);
			if (d < 5.0 + 2.0 * pfbm(p, 4.0)) h += 0.08 + 0.06 * step(0.6, pnoise(p / 2.0, 32.0));
			if (variant == 2 || variant == 4) {
				vec2 c2 = vec2(20.0 + h11(float(variant) * 7.0) * 24.0, 20.0 + h11(float(variant) * 9.0) * 24.0);
				if (length(p - c2) < 1.2) h = 0.92;      // tiny flower
				if (length(p - c2 - vec2(3.0, 2.0)) < 1.0) h = 0.85;
			}
		}
	} else if (mat == 1) {
		h = dirt(p, 0.28);
		if (variant > 0) {
			vec2 c = vec2(18.0 + h11(float(variant) * 3.0) * 28.0, 18.0 + h11(float(variant) * 5.0) * 28.0);
			vec2 d = p - c;
			float r = 2.6 + h11(float(variant) * 11.0) * 1.5;
			float e = length(d * vec2(1.0, 1.25));
			if (e < r) h = 0.55 + 0.25 * dot(normalize(d + vec2(0.001)), normalize(vec2(-1.0, -1.0)));
			else if (e < r + 1.2 && d.y > 0.0) h = 0.2;
		}
	} else if (mat == 2) {
		h = dirt(p, 0.12) * 0.9 + 0.05;
		h += (pfbm(p, 32.0) - 0.5) * 0.12;
		if (variant > 0) {
			float c = abs(pfbm(p + float(variant) * 13.0, 16.0) - 0.5);
			vec2 cc = p - vec2(32.0);
			if (c < 0.02 && length(cc) < 22.0) h = 0.25;
		}
	} else if (mat == 3) {
		h = blocks(p, 16.0, 16.0, 1.0, variant > 2 ? 0.35 : 0.0);
		if (variant == 1 || variant == 4) { if (abs(pfbm(p * 2.0, 8.0) - 0.5) < 0.05) h = max(h - 0.08, 0.14); }
	} else {
		h = blocks(p, 32.0, 32.0, 1.0, 0.45 + 0.1 * float(variant));
		h += (pfbm(p + 40.0, 4.0) - 0.5) * 0.06;
	}
	COLOR = vec4(clamp(h, 0.0, 1.0), 0.0, 0.0, 1.0);
}
