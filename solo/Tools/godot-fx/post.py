"""Turns raw Godot renders into final pixel-art sheets.

  python -I post.py <raw dir> [--only substr]

Per frame: alpha-weighted box downsample (ss x ss) -> heat mapped onto the
job's 8-colour ramp -> alpha snapped to a few levels -> frames laid out in one
row exactly like the original sheet. Writes <original>_v2.png next to the
original. Refuses to overwrite a _v2 file that this tool did not make."""
import json
import os
import sys

import numpy as np
from PIL import Image, PngImagePlugin

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from palettes import RAMPS, DANGER_EXEMPT, rgb, is_danger  # noqa: E402

ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Art"))
MARK = "godot-fx"
ALPHA_LEVELS = np.array([0.0, 0.22, 0.4, 0.58, 0.76, 0.9, 1.0])


def ours(path):
    if not os.path.exists(path):
        return True
    try:
        return Image.open(path).info.get("Software") == MARK
    except Exception:
        return False


def downsample(raw, ss):
    a = raw[..., 3] / 255.0
    h = raw[..., 0] / 255.0
    s = raw[..., 1] / 255.0
    H, W = a.shape
    sh = (H // ss, ss, W // ss, ss)
    A = a.reshape(sh).mean(axis=(1, 3))
    wsum = a.reshape(sh).sum(axis=(1, 3))
    Hh = (h * a).reshape(sh).sum(axis=(1, 3)) / np.maximum(wsum, 1e-6)
    # heat: also let the brightest sub-sample pull up a little (keeps 1px cores)
    Hmax = np.where(a > 0.5, h, 0).reshape(sh).max(axis=(1, 3))
    Hh = np.maximum(Hh, Hmax * 0.92)
    S = (s * a).reshape(sh).sum(axis=(1, 3)) / np.maximum(wsum, 1e-6)
    return A, Hh, S


def colourise(A, Hh, S, ramp, ramp2, gamma, alpha_cut, rim=0, floor=0, glow_floor=4):
    r1 = np.array([rgb(c) for c in RAMPS[ramp]], dtype=np.uint8)
    r2 = np.array([rgb(c) for c in RAMPS[ramp2 or ramp]], dtype=np.uint8)
    n = len(r1)
    idx = np.clip(np.rint(np.power(np.clip(Hh, 0, 1), gamma) * (n - 1)), 0, n - 1).astype(int)
    if rim:
        solid = A >= 0.5
        pad = np.pad(solid, 1)
        nb = pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:]
        edge = solid & ~nb
        idx = np.where(edge, np.maximum(idx - rim, 1), idx)
    idx = np.maximum(idx, floor)
    # translucent pixels read as glow: keep them in the bright half of the ramp
    idx = np.where(A < 0.5, np.maximum(idx, glow_floor), idx)
    col = np.where((S > 0.5)[..., None], r2[idx], r1[idx])
    q = ALPHA_LEVELS[np.abs(A[..., None] - ALPHA_LEVELS[None, None, :]).argmin(axis=2)]
    q[A < alpha_cut] = 0.0
    out = np.zeros(A.shape + (4,), dtype=np.uint8)
    out[..., :3] = col
    out[..., 3] = np.rint(q * 255).astype(np.uint8)
    out[out[..., 3] == 0, :3] = 0
    return out


def run(raw_dir, only=""):
    jobs = json.load(open(os.path.join(HERE, "jobs.json"), encoding="utf-8"))
    report = []
    bad = 0
    for job in jobs:
        if only and only not in job["id"]:
            continue
        if job["shader"] == "ground":
            continue  # tiles are assembled by tiles.py
        orig = os.path.join(ART, job["orig"])
        dst = orig[:-4] + "_v2.png"
        if not os.path.exists(orig):
            print("MISSING ORIGINAL", job["orig"]); bad += 1; continue
        if not ours(dst):
            print("REFUSE (hand-made v2 exists)", dst); bad += 1; continue
        ow, oh = Image.open(orig).size
        fw, fh, n, ss = job["w"], job["h"], job["frames"], job.get("ss", 4)
        if (ow, oh) != (fw * n, fh):
            print("SIZE MISMATCH", job["orig"], (ow, oh), (fw * n, fh)); bad += 1; continue
        sheet = np.zeros((fh, fw * n, 4), dtype=np.uint8)
        for i in range(n):
            raw = np.array(Image.open(os.path.join(raw_dir, "%s__%d.png" % (job["id"], i))).convert("RGBA")).astype(np.float64)
            A, Hh, S = downsample(raw, ss)
            A = np.clip(A * job.get("alpha_gain", 1.35), 0, 1)
            fr = colourise(A, Hh, S, job["ramp"], job.get("ramp2"), job.get("gamma", 1.0), job.get("alpha_cut", 0.12),
                            job.get("rim", 0), job.get("floor", 0),
                            job.get("glow_floor", 0 if job["shader"] in ("smoke", "cracks") else 4))
            sheet[:, i * fw:(i + 1) * fw] = fr
        if job["ramp"] not in DANGER_EXEMPT and (job.get("ramp2") or job["ramp"]) not in DANGER_EXEMPT:
            vis = sheet[sheet[..., 3] > 0]
            hits = sum(1 for p in vis if is_danger(*p[:3]))
            if hits:
                print("DANGER COLOUR in player-side asset", job["orig"], hits); bad += 1; continue
        info = PngImagePlugin.PngInfo()
        info.add_text("Software", MARK)
        Image.fromarray(sheet, "RGBA").save(dst, pnginfo=info)
        report.append(dst)
    print("written:", len(report), "problems:", bad)
    return 1 if bad else 0


if __name__ == "__main__":
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else ""
    sys.exit(run(sys.argv[1], only))
