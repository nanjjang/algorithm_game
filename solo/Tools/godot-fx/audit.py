"""Audit: lists generated _v2 sheets (tagged Software=godot-fx), checks they match the
original's pixel size, and lists files in the main art folders that still have no _v2.
  python -I audit.py [--remaining out.json]"""
import glob
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Art"))
CATS = ["effects/player", "effects/enemy", "effects/special", "effects/status", "effects/loot",
        "effects/ritual", "effects/wand", "world/tiles", "world/sprites", "world/backplates",
        "ui/kit", "ui/icon", "monsters"]


def rel(p):
    return os.path.relpath(p, ART).replace(os.sep, "/")


def main():
    mine, hand, bad = [], [], 0
    for f in glob.glob(os.path.join(ART, "**", "*_v2.png"), recursive=True):
        (mine if Image.open(f).info.get("Software") == "godot-fx" else hand).append(f)
    for f in mine:
        o = f[:-len("_v2.png")] + ".png"
        if Image.open(o).size != Image.open(f).size:
            print("SIZE MISMATCH", rel(f)); bad += 1
    print("generated:", len(mine), "hand-made v2:", len(hand), "size mismatches:", bad)
    rem = {}
    for c in CATS:
        for f in sorted(glob.glob(os.path.join(ART, c, "*.png"))):
            if f.endswith("_v2.png") or os.path.exists(f[:-4] + "_v2.png"):
                continue
            rem.setdefault(c, []).append([os.path.basename(f), list(Image.open(f).size)])
    for c, l in rem.items():
        print("remaining", c, len(l))
    if "--remaining" in sys.argv:
        json.dump(rem, open(sys.argv[sys.argv.index("--remaining") + 1], "w", encoding="utf-8"), indent=0)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
