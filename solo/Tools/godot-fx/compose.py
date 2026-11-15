"""Style sheets whose look is mostly about form (books, ice spears and spikes, the giant shadow
hand, the page vortex). Shapes are drawn as heat/alpha at ss x resolution and pushed through
post.py's downsample + 8-colour ramp, so they share the Godot sheets' palette, rim and alpha
steps. Existing GPT art is reused where the form matters (page sprites, shadow fist).

  python -I compose.py <raw dir> [--only substr]

Reads Godot parts from <raw>/_parts (post.py writes them). Entries come from styles.COMPOSED."""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, PngImagePlugin

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True  # python -I ignores PYTHONDONTWRITEBYTECODE
sys.path.insert(0, HERE)
from palettes import RAMPS, DANGER_EXEMPT, rgb, is_danger  # noqa: E402
from post import downsample, colourise, MARK, ours  # noqa: E402
from styles import COMPOSED  # noqa: E402

ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Art"))


# ------------------------------------------------------------------ drawing kit (final-pixel units)
class Canvas:
    def __init__(self, w, h, ss=4):
        self.w, self.h, self.ss = w, h, ss
        self.H = np.zeros((h * ss, w * ss))
        self.A = np.zeros((h * ss, w * ss))
        self.S = np.zeros((h * ss, w * ss))
        ys, xs = np.mgrid[0:h * ss, 0:w * ss]
        self.X = (xs + 0.5) / ss
        self.Y = (ys + 0.5) / ss

    def poly(self, pts):
        im = Image.new("L", (self.w * self.ss, self.h * self.ss), 0)
        ImageDraw.Draw(im).polygon([(x * self.ss, y * self.ss) for x, y in pts], fill=255)
        return np.array(im) > 0

    def line(self, pts, width):
        im = Image.new("L", (self.w * self.ss, self.h * self.ss), 0)
        ImageDraw.Draw(im).line([(x * self.ss, y * self.ss) for x, y in pts], fill=255,
                                width=max(1, int(round(width * self.ss))), joint="curve")
        return np.array(im) > 0

    def ellipse(self, cx, cy, rx, ry):
        return ((self.X - cx) / rx) ** 2 + ((self.Y - cy) / ry) ** 2 <= 1.0

    def plus(self, cx, cy, arm, w=0.5):
        dx, dy = np.abs(self.X - cx), np.abs(self.Y - cy)
        return ((dx <= w) & (dy <= arm)) | ((dy <= w) & (dx <= arm))

    def paint(self, mask, heat, alpha=1.0, sel=0.0, over=True):
        """over=True: painter's order (later shapes cover earlier ones)."""
        heat = np.broadcast_to(heat, self.H.shape)
        alpha = np.broadcast_to(alpha, self.H.shape)
        m = mask & (alpha > 0)
        if over:
            self.H[m] = heat[m]
            self.S[m] = sel
            self.A[m] = np.maximum(self.A[m], alpha[m])
        else:
            k = m & (heat > self.H)
            self.H[k] = heat[k]
            self.S[k] = sel
            self.A[m] = np.maximum(self.A[m], alpha[m])

    def frame(self, ramp, ramp2=None, gamma=1.0, rim=1, floor=1, glow_floor=4, alpha_gain=1.0):
        raw = np.zeros(self.H.shape + (4,))
        raw[..., 0] = np.clip(self.H, 0, 1) * 255
        raw[..., 1] = np.clip(self.S, 0, 1) * 255
        raw[..., 3] = np.clip(self.A, 0, 1) * 255
        A, Hh, S = downsample(raw, self.ss)
        A = np.clip(A * alpha_gain, 0, 1)
        return colourise(A, Hh, S, ramp, ramp2, gamma, 0.12, rim, floor, glow_floor)


def sheet(frames):
    return np.concatenate(frames, axis=1)


def save(entry, arr, danger_check=True):
    dst = os.path.join(ART, entry["out"])
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if not ours(dst):
        print("REFUSE (not made by this tool)", dst)
        return 1
    fw, fh, n = entry["w"], entry["h"], entry["frames"]
    if arr.shape[:2] != (fh, fw * n):
        print("SIZE MISMATCH", entry["out"], arr.shape[:2], (fh, fw * n))
        return 1
    if danger_check:
        vis = arr[arr[..., 3] > 0]
        hits = sum(1 for p in vis if is_danger(*p[:3]))
        if hits:
            print("DANGER COLOUR", entry["out"], hits)
            return 1
    info = PngImagePlugin.PngInfo()
    info.add_text("Software", MARK)
    Image.fromarray(arr.astype(np.uint8), "RGBA").save(dst, pnginfo=info)
    print("composed", entry["out"])
    return 0


def frames_of(path, fw, fh):
    im = Image.open(os.path.join(ART, path)).convert("RGBA")
    return [im.crop((i * fw, 0, (i + 1) * fw, fh)) for i in range(im.width // fw)]


def rot_uv(c, cx, cy, ang):
    dx, dy = c.X - cx, c.Y - cy
    ca, sa = np.cos(ang), np.sin(ang)
    return ca * dx + sa * dy, -sa * dx + ca * dy


# ------------------------------------------------------------------ char.3 books
def book_spin(e):
    out = []
    for i in range(e["frames"]):
        c = Canvas(e["w"], e["h"])
        ang = i / e["frames"] * 2 * np.pi
        bw, bh = 9.0, 6.5
        # page block = cover shape shifted down (thickness always faces the viewer's bottom)
        u, v = rot_uv(c, 16, 17.6, ang)
        pages = (np.abs(u) <= bw) & (np.abs(v) <= bh)
        edge = pages & ((np.abs(u) > bw - 1.0) | (np.abs(v) > bh - 1.0))
        c.paint(pages, 0.86, sel=1.0)
        c.paint(edge, 0.7, sel=1.0)
        u, v = rot_uv(c, 16, 16, ang)
        cover = (np.abs(u) <= bw) & (np.abs(v) <= bh)
        shade = 0.5 - (c.Y - 16) * 0.012
        c.paint(cover, shade)
        trim = cover & ((np.abs(u) > bw - 1.6) | (np.abs(v) > bh - 1.6)) & ~((np.abs(u) > bw - 0.6) | (np.abs(v) > bh - 0.6))
        c.paint(trim, 0.78)
        spine = cover & (u < -bw + 2.6)
        c.paint(spine, 0.3)
        gem = (np.abs(u - 1.0) + np.abs(v)) <= 3.2
        c.paint(gem & cover, 0.86)
        c.paint((np.abs(u - 1.0) + np.abs(v)) <= 1.8, 1.0)
        # speed motes trailing to the left (book flies right)
        for k, (dx, dy) in enumerate([(-12, -3), (-13, 4), (-10, 7)]):
            if (i + k) % 3:
                c.paint(c.ellipse(16 + dx - (i % 2), 16 + dy, 0.7, 0.7), 0.85, alpha=0.9)
        if i % 2 == 0:
            c.paint(c.plus(16 + 10 * np.cos(ang + 0.8), 16 + 7 * np.sin(ang + 0.8), 1.6), 1.0)
        out.append(c.frame("arcane", "heal", gamma=1.0, rim=1, floor=1))
    return sheet(out)


def _text_lines(c, x0, x1, y0, y1, seed):
    rng = np.random.default_rng(seed)
    y = np.floor(y0) + 0.5
    while y < y1:
        xa = x0 + 1.5
        while xa < x1 - 2:
            L = rng.uniform(2.5, 6.5)
            xb = min(xa + L, x1 - 1.5)
            c.paint((np.abs(c.Y - y) < 0.5) & (c.X >= np.floor(xa)) & (c.X <= np.floor(xb) + 1), 0.4, sel=0.0)
            xa = xb + rng.uniform(1.2, 2.2)
        y += 3.0


def _open_book(c, cx=32, cy=28, pw=25.0, ph=17.5, lines=True):
    # back covers (teal rim) and two page blocks
    c.paint(c.poly([(cx - pw - 1.5, cy - ph - 1.5), (cx + pw + 1.5, cy - ph - 1.5),
                    (cx + pw + 1.5, cy + ph + 2.5), (cx - pw - 1.5, cy + ph + 2.5)]), 0.42)
    for side in (-1, 1):
        x0, x1 = (cx - pw, cx - 0.5) if side < 0 else (cx + 0.5, cx + pw)
        pg = c.poly([(x0, cy - ph), (x1, cy - ph - 1.2 * (side < 0) - 1.2 * (side > 0) * 0),
                     (x1, cy + ph), (x0, cy + ph)])
        c.paint(pg, 0.9, sel=1.0)
        # page edges at the bottom (stack)
        c.paint(c.poly([(x0, cy + ph), (x1, cy + ph), (x1, cy + ph + 1.5), (x0, cy + ph + 1.5)]), 0.7, sel=1.0)
        if lines:
            _text_lines(c, x0 + 1, x1 - 1, cy - ph + 3.5, cy + ph - 2, 11 if side < 0 else 23)
    c.paint(c.line([(cx, cy - ph - 0.5), (cx, cy + ph + 1.5)], 1.2), 0.22)


def book_open(e):
    out = []
    cx, cy, pw, ph = 32, 28, 25.0, 17.5
    thetas = [0.0, 0.22, 0.48, 0.7, 0.88, 1.0]
    for i, th in enumerate(thetas):
        c = Canvas(e["w"], e["h"])
        a = th * np.pi
        cs, sn = np.cos(a), np.sin(a)
        if th < 1.0:
            # closed stack on the right of the spine: back cover + page block
            c.paint(c.poly([(cx - 0.5, cy - ph - 1.5), (cx + pw + 1.5, cy - ph - 1.5),
                            (cx + pw + 1.5, cy + ph + 2.5), (cx - 0.5, cy + ph + 2.5)]), 0.42)
            c.paint(c.poly([(cx + 0.5, cy - ph), (cx + pw, cy - ph), (cx + pw, cy + ph), (cx + 0.5, cy + ph)]), 0.88, sel=1.0)
            c.paint(c.poly([(cx + 0.5, cy + ph), (cx + pw, cy + ph), (cx + pw, cy + ph + 1.5), (cx + 0.5, cy + ph + 1.5)]), 0.7, sel=1.0)
            if th > 0.2:
                _text_lines(c, cx + 1.5, cx + pw - 1, cy - ph + 3.5, cy + ph - 2, 23)
            if th >= 0.48:
                # the left half appears as the cover swings over
                c.paint(c.poly([(cx - pw * (-cs) - 1.5, cy - ph - 1.5), (cx, cy - ph - 1.5),
                                (cx, cy + ph + 2.5), (cx - pw * (-cs) - 1.5, cy + ph + 2.5)]), 0.42)
            # fluttering loose pages lag behind the cover
            for lag, hh in ((0.55, 0.95), (0.78, 0.92)):
                pa = a * lag
                if 0.1 < pa < np.pi - 0.1 and th > 0.2:
                    fx = cx + (pw - 1) * np.cos(pa)
                    lift = 7 * np.sin(pa)
                    c.paint(c.poly([(cx, cy - ph + 0.5), (fx, cy - ph - lift + 1), (fx, cy + ph - lift), (cx, cy + ph)]),
                            hh, sel=1.0)
            # the front cover
            fx = cx + (pw + 1.5) * cs
            lift = 8 * sn
            quad = [(cx, cy - ph - 1.5), (fx, cy - ph - 1.5 - lift - 1.5 * sn), (fx, cy + ph + 2.5 - lift + 1.5 * sn), (cx, cy + ph + 2.5)]
            q = c.poly(quad)
            if cs >= 0:
                c.paint(q, 0.5 - 0.1 * sn)
                inner = c.poly([(cx + 2.5 * cs, cy - ph + 0.5), (fx - 1.6 * cs, cy - ph + 0.5 - lift - 1.2 * sn),
                                (fx - 1.6 * cs, cy + ph + 0.5 - lift + 1.2 * sn), (cx + 2.5 * cs, cy + ph + 0.5)])
                ring = q & ~inner
                c.paint(ring, 0.78)
                c.paint(q & (c.X < cx + 3.0 * max(cs, 0.3)), 0.3)
                gx, gy = cx + (pw * 0.55) * cs, cy - lift * 0.55
                c.paint(q & ((np.abs(c.X - gx) / max(cs, 0.25) + np.abs(c.Y - gy)) <= 4.0), 0.9)
                c.paint(q & ((np.abs(c.X - gx) / max(cs, 0.25) + np.abs(c.Y - gy)) <= 2.2), 1.0)
            else:
                c.paint(q, 0.45)
                inner = c.poly([(cx - 0.5, cy - ph), (fx + 1.6, cy - ph - lift - 0.8 * sn), (fx + 1.6, cy + ph - lift + 0.8 * sn), (cx - 0.5, cy + ph)])
                c.paint(inner, 0.92, sel=1.0)
        else:
            _open_book(c, cx, cy, pw, ph)
        if th >= 0.7:
            rng = np.random.default_rng(40 + i)
            for k in range(5 if th < 1 else 7):
                x = cx + rng.uniform(-pw + 3, pw - 3)
                y = cy - ph - rng.uniform(-6, 6) - (3 if th == 1 else 0)
                if k < 2:
                    c.paint(c.plus(x, y, 1.6), 1.0)
                else:
                    c.paint((np.abs(c.X - x) <= 1.0) & (np.abs(c.Y - y) <= 1.0), 0.95)
        out.append(c.frame("arcane", "heal", rim=1, floor=1))
    return sheet(out)


def book_flip(e):
    out = []
    cx, cy, pw, ph = 32, 28, 25.0, 17.5
    n = e["frames"]
    for i in range(n):
        c = Canvas(e["w"], e["h"])
        _open_book(c, cx, cy, pw, ph)
        a = (i + 0.5) / n * np.pi
        fx = cx + (pw - 0.5) * np.cos(a)
        lift = 7 * np.sin(a)
        pq = c.poly([(cx, cy - ph + 0.3), (fx, cy - ph - lift + 0.6), (fx, cy + ph - lift - 0.4), (cx, cy + ph)])
        c.paint(pq, 0.97, sel=1.0)
        c.paint(pq & (np.abs(c.X - fx) < 1.2), 0.75, sel=1.0)
        # rising glyph motes (looping)
        for k in range(6):
            ph_k = ((k / 6) + i / n) % 1.0
            x = cx + (-pw + 4 + (k * 37 % 46))
            y = cy - 2 - ph_k * 26
            if k % 3 == 0:
                c.paint(c.plus(x, y, 1.6), 1.0, alpha=1.0 if ph_k < 0.7 else 0.58)
            else:
                c.paint((np.abs(c.X - x) <= 1.0) & (np.abs(c.Y - y) <= 1.0), 0.9, alpha=1.0 if ph_k < 0.7 else 0.58)
        out.append(c.frame("arcane", "heal", rim=1, floor=1))
    return sheet(out)


def page_vortex(e, raw):
    base = Image.open(os.path.join(raw, "_parts", "styles__char.3__vortex_base.png")).convert("RGBA")
    fw, fh, n = e["w"], e["h"], e["frames"]
    big = frames_of("wands/galpi/animation/page_large.png", 16, 16)
    small = frames_of("wands/galpi/animation/page_small.png", 10, 12)
    # page_small.png carries a stray pure-red pixel; repaint danger colours as the page's brown edge
    fix = []
    for sp in big + small:
        a = np.array(sp)
        bad = np.array([[is_danger(*p[:3]) and p[3] > 0 for p in row] for row in a])
        a[bad, :3] = rgb(RAMPS["heal"][2])
        fix.append(Image.fromarray(a, "RGBA"))
    big, small = fix[:len(big)], fix[len(big):]
    R = min(fw / 2, fh / 2 / 0.62) - 1.5
    out = Image.new("RGBA", (fw * n, fh))
    for i in range(n):
        fr = base.crop((i * fw, 0, (i + 1) * fw, fh))
        sprites = []
        for ring, count, rad, imgs, ph0 in ((0, 8, 0.8, big, 0.0), (1, 6, 0.52, small, 0.4)):
            for k in range(count):
                ang = 2 * np.pi * (k + ph0) / count + (i / n) * 2 * np.pi / count
                x = fw / 2 + R * rad * np.cos(ang)
                y = fh / 2 + R * rad * 0.62 * np.sin(ang) - 3 * np.sin(2 * ang + k)
                sp = imgs[(k + i) % len(imgs)]
                sprites.append((y, x, sp))
        for y, x, sp in sorted(sprites, key=lambda s: s[0]):
            fr.alpha_composite(sp, (int(round(x - sp.width / 2)), int(round(y - sp.height / 2))))
        out.paste(fr, (i * fw, 0))
    return np.array(out)


# ------------------------------------------------------------------ char.4 ice
def ice_spear(e):
    out = []
    for i in range(e["frames"]):
        c = Canvas(e["w"], e["h"])
        # frost tail
        for k, (y, L) in enumerate([(6.5, 9), (8.0, 13), (9.5, 8)]):
            L2 = L + (3 if (i + k) % 2 else 0)
            c.paint(c.line([(11 - L2, y), (11, y)], 0.8), 0.78, alpha=0.58)
        for k, (x, y) in enumerate([(3, 5), (6, 11), (1, 9)]):
            if (i + k) % 2 == 0:
                c.paint(c.ellipse(x + i, y, 0.7, 0.7), 0.9, alpha=0.9)
        body = c.poly([(47, 8), (35, 4.4), (11, 6.2), (6.5, 8), (11, 9.8), (35, 11.6)])
        top = body & (c.Y < 8.0 - (c.X - 30) * 0.0)
        c.paint(body, 0.55)
        c.paint(top, 0.85)
        c.paint(body & c.poly([(47, 8), (35, 4.4), (33, 6.0)]), 0.98)
        c.paint(c.line([(12, 7.7), (45, 7.7)], 0.7), 1.0)
        c.paint(body & c.line([(17, 6.0), (20, 9.6)], 0.6), 0.4)
        c.paint(body & c.line([(26, 5.4), (28.5, 10.4)], 0.6), 0.45)
        gx = 31 if i == 0 else 40
        c.paint(c.plus(gx, 5.5, 2.0), 1.0)
        out.append(c.frame("ice", rim=1, floor=1))
    return sheet(out)


def icicle(e):
    out = []
    for i in range(e["frames"]):
        c = Canvas(e["w"], e["h"])
        for k, x in enumerate((4.5, 8.0, 11.5)):
            L = 6 + 3 * ((i + k) % 2)
            c.paint(c.line([(x, 1), (x, 1 + L)], 0.8), 0.8, alpha=0.58)
        body = c.poly([(2.5, 14), (4, 10), (6, 12.5), (8, 8.5), (10.2, 12), (12, 9.8), (13.5, 14), (8.6, 44.5), (7.4, 44.5)])
        c.paint(body, 0.55)
        c.paint(body & (c.X < 7.8), 0.86)
        c.paint(c.line([(7.4, 15), (7.9, 41)], 0.7), 1.0)
        c.paint(body & (c.Y < 14.5), 0.95)
        c.paint(body & c.line([(4, 22), (11, 20)], 0.6), 0.45)
        c.paint(c.plus(5.5 if i == 0 else 10, 18 if i == 0 else 27, 1.6), 1.0)
        out.append(c.frame("ice", rim=1, floor=1))
    return sheet(out)


def _spikes(c, s, ground=58.0):
    """Three faceted spikes, height scale s (0..1.1)."""
    specs = [(13, 27, 5.0, -4), (35, 33, 5.5, 4), (24, 46, 7.5, 0)]
    for x0, h, w, tilt in specs:
        hh = h * s
        if hh < 1:
            continue
        ww = w * min(1.0, 0.45 + s * 0.6)
        tip = (x0 + tilt * s, ground - hh)
        mid = (x0 + ww * 0.15, ground)
        left = c.poly([(x0 - ww, ground), tip, mid])
        right = c.poly([mid, tip, (x0 + ww, ground)])
        c.paint(left, 0.86)
        c.paint(right, 0.55)
        c.paint(c.line([mid, tip], 0.7), 1.0)
        # a side facet near the base for weight
        c.paint(left & (c.Y > ground - hh * 0.35) & (c.X < x0 - ww * 0.45), 0.7)


def _shift(a, oy, ox):
    """Translate without wrapping (vacated cells become 0/False)."""
    out = np.zeros_like(a)
    H, W = a.shape
    ys, yd = (slice(0, H - oy), slice(oy, H)) if oy >= 0 else (slice(-oy, H), slice(0, H + oy))
    xs, xd = (slice(0, W - ox), slice(ox, W)) if ox >= 0 else (slice(-ox, W), slice(0, W + ox))
    out[yd, xd] = a[ys, xs]
    return out


def ice_spike(e):
    fw, fh, n = e["w"], e["h"], e["frames"]
    g = 58.0
    scales = [0.35, 0.85, 1.07, 1.0, 1.0, 1.0]
    out = []
    for i in range(n):
        c = Canvas(fw, fh)
        if i <= 5:
            c.paint(c.ellipse(24, g, 18 * min(1, 0.6 + i * 0.2), 4.5), 0.78, alpha=0.58)
            _spikes(c, scales[i], g)
            c.A[c.Y > g + 0.5] = 0
            c.A[(c.Y > g + 0.5) & c.ellipse(24, g, 18 * min(1, 0.6 + i * 0.2), 4.5)] = 0.58
            if i <= 1:
                rng = np.random.default_rng(7 + i)
                for k in range(7):
                    x = 24 + rng.uniform(-16, 16)
                    y = g - rng.uniform(2, 12) * (1 + i)
                    c.paint((np.abs(c.X - x) <= 1.0) & (np.abs(c.Y - y) <= 0.8), 0.8)
            if i == 2:
                c.paint(c.plus(24.5, 18, 2.5), 1.0)
            if i == 3:
                c.paint(c.plus(23, 28, 1.6), 1.0)
            if i == 4:
                c.paint(c.plus(35.5, 32, 1.6), 1.0)
            if i == 5:
                body = c.A > 0.9
                crack = c.line([(20, 24), (26, 31), (21, 37), (27, 45), (23, 52)], 0.8) | c.line([(26, 31), (29, 34)], 0.6) \
                    | c.line([(11, 41), (15, 46), (12, 51)], 0.6) | c.line([(37, 36), (34, 42), (37, 49)], 0.6)
                c.paint(crack & body, 0.25)
                c.paint(c.plus(26, 31, 2.0), 1.0)
        else:
            full = Canvas(fw, fh)
            _spikes(full, 1.0, g)
            full.A[full.Y > g + 0.5] = 0
            rng = np.random.default_rng(31)
            seeds = np.stack([rng.uniform(8, 40, 11), rng.uniform(14, 50, 11)], 1)
            d = np.stack([(full.X - sx) ** 2 + (full.Y - sy) ** 2 for sx, sy in seeds], 0)
            cell = d.argmin(0)
            stump = full.Y > g - 4
            dist = 5.0 if i == 6 else 12.0
            drop = 0.0 if i == 6 else 5.0
            c.paint(c.ellipse(24, g, 18, 4.5), 0.78, alpha=0.58 if i == 6 else 0.4)
            c.A[c.Y > g + 0.5] = np.where(c.ellipse(24, g, 18, 4.5)[c.Y > g + 0.5], c.A[c.Y > g + 0.5], 0)
            keep = stump & (full.A > 0)
            c.paint(keep, full.H)
            for k, (sx, sy) in enumerate(seeds):
                m = (cell == k) & (full.A > 0) & ~stump
                if not m.any():
                    continue
                v = np.array([sx - 24, sy - (g - 10)])
                v = v / (np.linalg.norm(v) + 1e-6)
                ox = int(round(v[0] * dist * c.ss))
                oy = int(round((v[1] * dist * 0.7 - (2.5 if i == 6 else 0) + drop) * c.ss))
                mm = _shift(m, oy, ox)
                hh = _shift(full.H, oy, ox)
                c.paint(mm, hh, alpha=1.0 if i == 6 else 0.76)
            rng2 = np.random.default_rng(50 + i)
            for k in range(6):
                x, y = 24 + rng2.uniform(-18, 18), g - rng2.uniform(4, 30)
                if i == 7:
                    c.paint(c.ellipse(x, y, 1.6, 1.2), 0.9, alpha=0.4, over=False)
                else:
                    c.paint(c.plus(x, y, 1.5), 1.0)
        out.append(c.frame("ice", rim=1, floor=1))
    return sheet(out)


# ------------------------------------------------------------------ char.5 giant hand
def _src_heat(img):
    """GPT dark sprite -> heat (index into the dark ramp) + alpha, at source resolution."""
    a = np.array(img).astype(np.float64)
    ramp = np.array([rgb(h) for h in RAMPS["dark"]], dtype=np.float64)
    col = a[..., :3]
    d = ((col[..., None, :] - ramp[None, None]) ** 2).sum(-1)
    idx = d.argmin(-1)
    lum = col.mean(-1) / 255.0
    heat = np.clip(0.55 * idx / 7.0 + 0.45 * np.clip((lum - 0.1) / 0.8, 0, 1), 0, 1)
    return heat, a[..., 3] / 255.0


def _blit_scaled(c, img, cx, cy, sx, sy, heat_add=0.0, clip_below=None):
    """Draws a GPT sprite scaled into the canvas (bicubic on heat/alpha, then ramp-quantised later)."""
    heat, alpha = _src_heat(img)
    W = int(round(img.width * sx * c.ss))
    H = int(round(img.height * sy * c.ss))
    hi = np.array(Image.fromarray((heat * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)) / 255.0
    ai = np.array(Image.fromarray((alpha * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)) / 255.0
    x0 = int(round((cx - img.width * sx / 2) * c.ss))
    y0 = int(round((cy - img.height * sy / 2) * c.ss))
    Hc, Wc = c.H.shape
    ys, xs = slice(max(0, y0), min(Hc, y0 + H)), slice(max(0, x0), min(Wc, x0 + W))
    sub_h = hi[ys.start - y0:ys.stop - y0, xs.start - x0:xs.stop - x0]
    sub_a = ai[ys.start - y0:ys.stop - y0, xs.start - x0:xs.stop - x0]
    if clip_below is not None:
        yy = (np.arange(ys.start, ys.stop) + 0.5) / c.ss
        sub_a = sub_a * (yy[:, None] <= clip_below)
    m = sub_a > 0.45
    tgtH, tgtA = c.H[ys, xs], c.A[ys, xs]
    tgtH[m] = np.clip(sub_h[m] + heat_add, 0, 1)
    tgtA[m] = np.maximum(tgtA[m], np.where(sub_a[m] > 0.8, 1.0, sub_a[m]))


def giant_hand(e):
    fw, fh, n = e["w"], e["h"], e["frames"]
    fist = frames_of("effects/special/shadow_fist_64x64_f3.png", 64, 64)
    g = 116.0
    out = []
    # rise/grip use the fist flipped upside down (wrist sinks into the pool, knuckles up);
    # slam uses the downward punch frame as drawn. (src, centre y, sx, sy, clip at ground, pool rx, heat boost, flip)
    plan = [(0, g + 30, 2.05, 2.05, True, 34, 0.0, True), (0, g - 10, 2.05, 2.05, True, 42, 0.0, True),
            (0, g - 46, 2.1, 2.1, True, 48, 0.0, True), (0, g - 50, 2.1, 2.1, True, 48, 0.06, True),
            (0, g - 50, 2.1, 2.1, True, 48, 0.12, True), (1, g - 54, 2.1, 1.8, False, 54, 0.05, False),
            (1, g - 52, 2.15, 1.75, False, 58, 0.18, False), (2, g - 54, 2.0, 2.0, False, 50, -0.05, False)]
    for i, (src, cy, sx, sy, clip, prx, boost, flip) in enumerate(plan):
        c = Canvas(fw, fh, ss=2)
        pool_a = 1.0 if i < 7 else 0.58
        c.paint(c.ellipse(64, g, prx, prx * 0.22), 0.18, alpha=pool_a)
        c.paint(c.ellipse(64, g, prx * 0.7, prx * 0.14), 0.08, alpha=pool_a)
        c.paint(c.ellipse(64, g, prx, prx * 0.22) & ~c.ellipse(64, g, prx - 2.5, prx * 0.22 - 1.6), 0.62, alpha=pool_a)
        img = fist[src].transpose(Image.FLIP_TOP_BOTTOM) if flip else fist[src]
        _blit_scaled(c, img, 64, cy, sx, sy, boost, clip_below=g if clip else None)
        if i in (5, 6):
            c.paint(c.ellipse(64, g, prx + 6, prx * 0.22 + 2) & ~c.ellipse(64, g, prx + 3, prx * 0.22), 0.85, alpha=0.76)
        if i in (3, 4):
            for k, (x, y) in enumerate([(30, 40), (98, 52), (88, 22)]):
                if (i + k) % 2 == 0:
                    c.paint(c.plus(x, y, 2.0), 1.0)
        out.append(c.frame("dark", rim=1, floor=0, glow_floor=3))
    return sheet(out)


BUILDERS = {"book_spin": book_spin, "book_open": book_open, "book_flip": book_flip, "ice_spear": ice_spear,
            "icicle": icicle, "ice_spike": ice_spike, "giant_hand": giant_hand}


def run(raw, only=""):
    bad = 0
    for e in COMPOSED:
        key = e["meta"]["key"]
        if only and only not in e["out"]:
            continue
        if key == "page_vortex":
            arr = page_vortex(e, raw)
        else:
            arr = BUILDERS[key](e)
        bad += save(e, arr)
    print("compose problems:", bad)
    return 1 if bad else 0


if __name__ == "__main__":
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else ""
    sys.exit(run(sys.argv[1], only))
