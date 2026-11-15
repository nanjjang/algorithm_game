#!/usr/bin/env python3
"""monster-rework/source 원본을 게임 시트로 굽는다.

- 몬스터·정예·보스: Resources/Art/monsters/manifest.json 의 칸 배치(rect·origin)를 그대로 쓰고,
  source-check.json 의 프레임 이름표로 같은 동작 칸을 찾아 넣는다. 한 몸 안에서는 배율을 하나로 맞춰 떨림이 없게 한다.
- 보스 HUD·초상화: 기존 크기에 맞춰 줄인다.
- 적·전리품 효과: effects/manifest.json 의 enemy·loot 항목 중 이름이 같은 원본을 칸마다 가운데 맞춰 넣는다.
- 줄일 때는 BOX로 줄이고 알파를 끊어서 픽셀 경계를 살린다.

사용: python3 art/production/monster-rework/build.py
"""
import json
import os
from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(ROOT, "source")
REPO = os.path.abspath(os.path.join(ROOT, "..", "..", ".."))
ART = os.path.join(REPO, "solo", "Assets", "Resources", "Art")
CHECK = json.load(open(os.path.join(ROOT, "source-check.json")))

# 같은 몸의 다른 판 중 고를 것 / 뺄 것
SKIP_TAGS = (".staffless", ".natural-head", ".retake")
PREFER = {"enemy.golem_menhir.png": "enemy.golem_menhir.corrected.png"}

_img_cache = {}


def img(rel):
    if rel not in _img_cache:
        _img_cache[rel] = Image.open(os.path.join(SRC, rel)).convert("RGBA")
    return _img_cache[rel]


def cells_of(rel):
    """원본 파일의 칸들: (이름표, 잘라낸 그림)."""
    v = CHECK[rel]
    out = []
    for label, cell in zip(v["frames"], v["cells"]):
        x, y, _, _ = cell["rect"]
        b = cell.get("bounds")
        if not b:
            continue
        crop = img(rel).crop((x + b[0], y + b[1], x + b[2], y + b[3]))
        out.append((label, crop))
    return out


def shrink(im, scale):
    w = max(1, round(im.width * scale))
    h = max(1, round(im.height * scale))
    small = im.resize((w, h), Image.BOX)
    r, g, b, a = small.split()
    a = a.point(lambda p: 255 if p >= 110 else 0)
    small = Image.merge("RGBA", (r, g, b, a))
    # 투명 칸의 색 찌꺼기 제거
    clear = Image.new("RGBA", small.size, (0, 0, 0, 0))
    return Image.composite(small, clear, a)


def entity_sources(entity):
    """몸 하나에 속한 원본 파일 목록(기본 그림 → 동작 시트)."""
    files = []
    for rel in CHECK:
        name = rel.split("/")[-1]
        folder = rel.split("/")[0] if "/" in rel else ""
        if folder not in ("", "animations", "elite"):
            continue
        if any(t in name for t in SKIP_TAGS):
            continue
        stem = name[:-4]
        for suf in (".sheet-1", ".sheet-2", ".sheet-3", ".sheet-4", ".sheet-a", ".sheet-b", ".sheet", ".corrected"):
            stem = stem.replace(suf, "")
        # 동작 이름 꼬리(.move 등) 떼기
        if stem != entity:
            if not stem.startswith(entity + "."):
                continue
            tail = stem[len(entity) + 1:]
            if "." in tail or tail in ("segment", "tail", "ground", "overlay", "dive", "twin"):
                continue
        files.append(rel)
    files.sort(key=lambda r: (0 if "/" not in r else 1, r))
    out = []
    for rel in files:
        name = rel.split("/")[-1]
        if name in PREFER and PREFER[name] in CHECK:
            continue
        out.append(rel)
    return out


def build_monsters(report):
    mpath = os.path.join(ART, "monsters", "manifest.json")
    entries = json.load(open(mpath))["entries"]
    for key, e in entries.items():
        sheet_path = os.path.join(ART, "monsters", e["path"])
        if key.startswith("elite."):
            srcs = ["elite/" + e["path"]] if ("elite/" + e["path"]) in CHECK else []
        else:
            srcs = entity_sources(key)
        pool = {}
        for rel in srcs:
            for label, crop in cells_of(rel):
                pool.setdefault(label, []).append(crop)
        if not pool:
            report.append(f"SKIP {key}: 원본 없음")
            continue
        old = Image.open(sheet_path)
        sheet = Image.new("RGBA", old.size, (0, 0, 0, 0))
        fw, fh = e["size"]
        plan = []
        for clip, frames in e["clips"].items():
            cand = pool.get(clip) or pool.get("base") or next(iter(pool.values()))
            if clip not in pool:
                report.append(f"WARN {key}.{clip}: 이름표 없음 → 대체 칸")
            for i, fr in enumerate(frames):
                plan.append((fr, cand[i % len(cand)]))
        # 몸 전체 배율 하나: 가장 큰 칸이 프레임에 들어가게
        ox, oy = e["origin"]
        room_w = min(ox, fw - ox) * 2 - 2
        room_h = oy - 1
        scale = min(min(room_w / c.width, room_h / c.height) for _, c in plan)
        for fr, crop in plan:
            rx, ry, rw, rh = fr["rect"]
            fox, foy = fr["origin"]
            s = shrink(crop, scale)
            px = rx + fox - s.width // 2
            py = ry + foy - s.height
            sheet.alpha_composite(s, (max(rx, px), max(ry, py)))
        sheet.save(sheet_path)
        report.append(f"OK   {key}: {len(plan)}칸 x{scale:.4f}")


def build_boss_ui(report):
    for rel in CHECK:
        if not rel.startswith("boss-ui/") or "natural-head" in rel:
            continue
        name = rel.split("/")[-1]
        dst = os.path.join(ART, "monsters", name)
        if not os.path.exists(dst):
            report.append(f"SKIP {name}: 게임 쪽 파일 없음")
            continue
        w, h = Image.open(dst).size
        _, crop = cells_of(rel)[0]
        scale = min((w - 2) / crop.width, (h - 2) / crop.height)
        s = shrink(crop, scale)
        out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        out.alpha_composite(s, ((w - s.width) // 2, (h - s.height) // 2))
        out.save(dst)
        report.append(f"OK   {name}: {w}x{h}")


def build_effects(report):
    eman = json.load(open(os.path.join(ART, "effects", "manifest.json")))
    for group in ("enemy", "loot"):
        for key, e in eman.get(group, {}).items():
            src_rel = f"effects/{key}.png"
            if key == "toad_buff":
                srcs = [f"effects/toad_buff.0{i}.png" for i in (1, 2, 3)]
            elif key == "campfire":
                srcs = ["effects/campfire.retake.png"]
            else:
                srcs = [src_rel]
            if not all(s in CHECK for s in srcs):
                report.append(f"SKIP fx {key}: 원본 없음")
                continue
            crops = [c for s in srcs for _, c in cells_of(s)]
            dst = os.path.join(ART, "effects", e["path"])
            frames = e["frames"]
            fw, fh = e["size"]
            scale = min(min((fw - 1) / c.width, (fh - 1) / c.height) for c in crops)
            sheet = Image.new("RGBA", Image.open(dst).size, (0, 0, 0, 0))
            for i, (x, y, w, h) in enumerate(frames):
                s = shrink(crops[i % len(crops)], scale)
                sheet.alpha_composite(s, (x + (w - s.width) // 2, y + (h - s.height) // 2))
            sheet.save(dst)
            report.append(f"OK   fx {key}: {len(frames)}칸")


if __name__ == "__main__":
    rep = []
    build_monsters(rep)
    build_boss_ui(rep)
    build_effects(rep)
    print("\n".join(rep))
