"""Contact sheet of every style sheet (effects/styles/**), grouped by folder, nearest-neighbour
enlarged on a dark checkerboard with a field-coloured strip under each row (to judge contrast
on the play field).
  python -I preview_styles.py [--out path] [--only substr]"""
import glob
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Art"))
ROOT = os.path.join(ART, "effects", "styles")
MAXW = 2000


def checker(w, h):
    bg = Image.new("RGBA", (w, h), (34, 37, 46, 255))
    d = ImageDraw.Draw(bg)
    for y in range(0, h, 8):
        for x in range(0, w, 8):
            if (x // 8 + y // 8) % 2:
                d.rectangle([x, y, x + 7, y + 7], fill=(42, 46, 57, 255))
    return bg


def main():
    out = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else os.path.join(HERE, "preview-styles.png")
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else ""
    rows = []
    for f in sorted(glob.glob(os.path.join(ROOT, "*", "*.png"))):
        rel = os.path.relpath(f, ROOT).replace(os.sep, "/")
        if only and only not in rel:
            continue
        im = Image.open(f).convert("RGBA")
        fh = im.height
        s = 4 if fh <= 32 else 3 if fh <= 64 else 2 if fh <= 128 else 1
        while im.width * s > MAXW and s > 1:
            s -= 1
        if im.width * s > MAXW:
            k = MAXW / im.width
            im = im.resize((int(im.width * k), int(im.height * k)), Image.NEAREST)
        else:
            im = im.resize((im.width * s, im.height * s), Image.NEAREST)
        rows.append((rel + "  x%d" % s, im))
    W = max(r[1].width for r in rows) + 16
    H = sum(16 + r[1].height * 2 + 10 for r in rows)
    img = checker(W, H)
    d = ImageDraw.Draw(img)
    y = 2
    for name, im in rows:
        d.text((6, y), name, fill=(225, 225, 235, 255))
        y += 14
        img.alpha_composite(im, (8, y))
        y += im.height
        field = Image.new("RGBA", (im.width, im.height), (74, 92, 58, 255))
        field.alpha_composite(im)
        img.alpha_composite(field, (8, y))
        y += im.height + 10
        d.line([(0, y - 4), (W, y - 4)], fill=(70, 74, 88, 255))
    img.convert("RGB").save(out)
    print("preview-styles:", out, img.size)


if __name__ == "__main__":
    main()
