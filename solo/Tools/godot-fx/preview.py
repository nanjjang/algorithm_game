"""Contact sheet: for every generated sheet, original (top) and _v2 (bottom),
enlarged with nearest-neighbour on a dark checkerboard.
  python -I preview.py [--only substr] [--out path]"""
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Art"))
MAXW = 1800


def checker(w, h):
    bg = Image.new("RGBA", (w, h), (34, 37, 46, 255))
    d = ImageDraw.Draw(bg)
    for y in range(0, h, 8):
        for x in range(0, w, 8):
            if (x // 8 + y // 8) % 2:
                d.rectangle([x, y, x + 7, y + 7], fill=(42, 46, 57, 255))
    return bg


def entries(only):
    jobs = json.load(open(os.path.join(HERE, "jobs.json"), encoding="utf-8"))
    seen = set()
    for j in jobs:
        if only and not any(o in j["id"] for o in only.split(",")):
            continue
        o = j["orig"]
        if o is None or o in seen:
            continue
        seen.add(o)
        yield o, j.get("w", 64), j.get("h", 64)
    for area in ("field", "plaza", "arena"):
        for time in ("day", "dusk", "night"):
            o = "world/tiles/%s.%s.png" % (area, time)
            if not only or any(x in o for x in only.split(",")):
                yield o, 64, 64


def main():
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else ""
    out = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else os.path.join(HERE, "preview.png")
    blocks = []
    for o, fw, fh in entries(only):
        a = Image.open(os.path.join(ART, o)).convert("RGBA")
        v = os.path.join(ART, o[:-4] + "_v2.png")
        if not os.path.exists(v):
            continue
        b = Image.open(v).convert("RGBA")
        s = max(1, min(4, 112 // max(fh, 1)))
        while a.width * s > MAXW and s > 1:
            s -= 1
        if a.width * s > MAXW or fh >= 200:  # very wide or tall sheets: halve
            a = a.resize((a.width // 2, a.height // 2), Image.NEAREST)
            b = b.resize((b.width // 2, b.height // 2), Image.NEAREST)
        a = a.resize((a.width * s, a.height * s), Image.NEAREST)
        b = b.resize((b.width * s, b.height * s), Image.NEAREST)
        blocks.append((o, a, b))
    if not blocks:
        print("nothing to preview"); return
    W = min(MAXW, max(x[1].width for x in blocks)) + 16
    H = sum(14 + x[1].height + x[2].height + 12 for x in blocks) + 8
    img = checker(W, H)
    d = ImageDraw.Draw(img)
    y = 4
    for o, a, b in blocks:
        d.text((6, y), o + "   (top: original, bottom: _v2)", fill=(220, 220, 230, 255))
        y += 14
        img.alpha_composite(a, (8, y)); y += a.height + 2
        img.alpha_composite(b, (8, y)); y += b.height + 10
        d.line([(0, y - 4), (W, y - 4)], fill=(70, 74, 88, 255))
    img.convert("RGB").save(out)
    print("preview:", out, img.size)


if __name__ == "__main__":
    main()
