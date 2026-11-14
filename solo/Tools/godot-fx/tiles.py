"""Builds world/tiles/<area>.<time>_v2.png from the Godot ground textures.

  python -I tiles.py <raw dir>

For every cell the world manifest maps into a tile sheet:
  * plain cells (grass.01-04, dirt.01-04, packed.01-04, paving.01-06, slate.01-04)
    use the matching texture variant;
  * edge / corner / mottle cells copy the material layout (which pixel is grass
    and which is dirt, packed or paving) from the original DAY sheet, so the
    autotile shapes and their seams stay exactly as before, and fill each side
    with the seamless base texture (variant 0).
Each material is recoloured with an 8-step ramp built around the manifest's
paletteTargets colour for that time of day. Cells the original leaves empty stay
transparent."""
import colorsys
import json
import os
import re
import sys

import numpy as np
from PIL import Image, PngImagePlugin

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Art"))
WORLD = os.path.join(ART, "world")
MARK = "godot-fx"
TIMES = ("day", "dusk", "night")
# value multipliers for the 8 ramp steps (dark -> light) and hue drift (cool shadows, warm lights)
VMUL = [0.58, 0.7, 0.82, 0.93, 1.04, 1.16, 1.3, 1.46]
HSHIFT = [0.018, 0.012, 0.006, 0.0, -0.004, -0.008, -0.012, -0.016]


def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float64)


def ramp(base_hex):
    r, g, b = hexrgb(base_hex) / 255.0
    hh, ss, vv = colorsys.rgb_to_hsv(r, g, b)
    out = []
    for vm, hs in zip(VMUL, HSHIFT):
        rr, gg, bb = colorsys.hsv_to_rgb((hh + hs) % 1.0, min(1.0, ss * (1.05 if vm < 1 else 0.92)), min(1.0, vv * vm))
        out.append([rr * 255, gg * 255, bb * 255])
    return np.clip(np.rint(np.array(out)), 0, 255).astype(np.uint8)


def heat(raw_dir, name, v, ss=4):
    im = np.array(Image.open(os.path.join(raw_dir, "ground__%s__v%d__0.png" % (name, v))).convert("RGBA")).astype(np.float64)
    h = im[..., 0] / 255.0
    H, W = h.shape
    return h.reshape(H // ss, ss, W // ss, ss).mean(axis=(1, 3))


def colour(hm, rmp):
    idx = np.clip(np.rint(hm * (len(rmp) - 1)), 0, len(rmp) - 1).astype(int)
    return idx


def ours(path):
    if not os.path.exists(path):
        return True
    try:
        return Image.open(path).info.get("Software") == MARK
    except Exception:
        return False


def run(raw_dir):
    man = json.load(open(os.path.join(WORLD, "manifest.json"), encoding="utf-8"))
    pal = man["paletteTargets"]
    cells = {}  # sheet base name -> list of (key, rect)
    for key, a in man["assets"].items():
        d = a.get("variants", {}).get("day")
        if not d or not d.get("file", "").startswith("tiles/"):
            continue
        sheet = d["file"][len("tiles/"):-len(".day.png")]
        cells.setdefault(sheet, []).append((key, d["rect"]))
    tex = {}
    for name, n in (("grass", 4), ("dirt", 4), ("packed", 4), ("paving", 6), ("slate", 4)):
        for v in range(n):
            tex[(name, v)] = heat(raw_dir, name, v)
    pairs = {"field": ("grass", "dirt"), "plaza": ("packed", "paving"), "arena": ("slate", "slate")}
    written = 0
    for sheet, lst in cells.items():
        day = np.array(Image.open(os.path.join(WORLD, "tiles", sheet + ".day.png")).convert("RGBA")).astype(np.float64)
        A, B = pairs[sheet]
        for time in TIMES:
            src = os.path.join(WORLD, "tiles", "%s.%s.png" % (sheet, time))
            dst = src[:-4] + "_v2.png"
            if not ours(dst):
                print("REFUSE (hand-made v2 exists)", dst)
                return 1
            orig = np.array(Image.open(src).convert("RGBA"))
            out = np.zeros_like(orig)
            ra, rb = ramp(pal[time][A]), ramp(pal[time][B])
            for key, (x, y, w, h) in lst:
                m = re.match(r"\w+\.tile\.(\w+)\.(\w+)$", key)
                kind, num = m.group(1), (int(m.group(2)) if m.group(2).isdigit() else 0)
                if kind in (A, B) and kind in ("grass", "dirt", "packed", "paving", "slate"):
                    r = ra if kind == A else rb
                    idx = colour(tex[(kind, num - 1)], r)
                    rgb = r[idx]
                else:
                    # mask from the day original: nearest of the two day palette targets
                    cell = day[y:y + h, x:x + w, :3]
                    da = np.linalg.norm(cell - hexrgb(pal["day"][A]), axis=2)
                    db = np.linalg.norm(cell - hexrgb(pal["day"][B]), axis=2)
                    isA = da <= db
                    ia = colour(tex[(A, 0)], ra)
                    ib = colour(tex[(B, 0)], rb)
                    pad = np.pad(isA, 1, mode="edge")
                    nbA = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
                    nbB = ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
                    below_A = np.pad(isA, ((1, 0), (0, 0)), mode="edge")[:-1, :]  # pixel above is A
                    # B side: shadow cast by the A lip (stronger right under A)
                    ib = np.where(~isA & nbA, np.maximum(ib - 1, 0), ib)
                    ib = np.where(~isA & below_A, np.maximum(ib - 1, 0), ib)
                    # A side: a lit rim along the boundary
                    ia = np.where(isA & nbB, np.minimum(ia + 1, 7), ia)
                    rgb = np.where(isA[..., None], ra[ia], rb[ib])
                out[y:y + h, x:x + w, :3] = rgb
                out[y:y + h, x:x + w, 3] = orig[y:y + h, x:x + w, 3]
            out[out[..., 3] == 0, :3] = 0
            info = PngImagePlugin.PngInfo()
            info.add_text("Software", MARK)
            Image.fromarray(out, "RGBA").save(dst, pnginfo=info)
            written += 1
    print("tiles written:", written)
    return 0


if __name__ == "__main__":
    sys.exit(run(sys.argv[1]))
