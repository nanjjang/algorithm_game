#!/usr/bin/env python
# solo/Tools/art_supplement.py — "마젠타 배경 + 정수배 확대"로 만들어 온 보충 그림(09 요약서 9절 빠진 그림)을
# 규격 크기의 투명 배경 시트로 바꿔 Assets/Resources/Art/supplement/solo/ 아래에 두고, 그 시트들의 manifest.json(효과 manifest 형식)을 쓴다.
#
# 쓰는 법(Git Bash, solo/에서):  python -I Tools/art_supplement.py [--check]
#   --check = 파일을 쓰지 않고, 만들 결과가 지금 파일과 같은지만 본다(다르면 끝 코드 1).
#
# 원본(art/)은 읽기만 한다. 원본은 artsync.json의 exclude로 Resources에 들이지 않고, 결과 폴더는 keep으로 지킨다.
# 처리: (1) 순마젠타 #FF00FF → 투명  (2) 확대 배수 s의 격자 위상을 색이 바뀌는 자리로 찾아 칸 가운데를 집는 최근접 축소
#       (3) 프레임 나누기: grid = 원본 칸이 규격 크기 그대로인 격자, blobs = 빈 줄(가로 또는 세로)로 갈라지는 덩어리 N개
#       (4) 각 프레임의 그림을 규격 칸 가운데에 놓는다(grid는 칸 그대로). 그림이 칸보다 크면 멈춘다.
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SOLO = os.path.dirname(HERE)
ART = os.path.normpath(os.path.join(SOLO, "..", "art", "production"))
OUT = os.path.join(SOLO, "Assets", "Resources", "Art", "supplement", "solo")
MAGENTA = (255, 0, 255)

# 원본 → 결과. group/key는 ArtLibrary가 읽는 효과 묶음과 열쇠다(groups가 기존 효과 묶음과 같으면 그 묶음에 더해진다).
# fps·loop는 같은 묶음의 형제 그림 값을 따른다(09 5절: 효과는 12fps, 드랍은 7fps 반복, 결정 2프레임은 5fps 반복).
JOBS = [
    # 부메랑 탄(09 8절 U-17: 그동안 mana_bolt 대체). 탄 그림이라 mana_bolt처럼 반복.
    dict(src="effects/player/boomerang_32x32_f4_magenta_6x.png", scale=6, split="blobs", axis="x", count=4,
         size=(32, 32), out="player/boomerang_32x32_f4.png", group="player", key="boomerang", fps=12, loop=True),
    # ⑨ 뇌광창 흰 심 64×16×2 (FX 20.4, 속성 없이 1벌)
    dict(src="effects/special/thunder_lance_white_core_64x16_f2_magenta_6x.png", scale=6, split="blobs", axis="y", count=2,
         size=(64, 16), out="special/thunderlance_core_64x16_f2.png", group="special", key="thunderlance_core", fps=12, loop=False),
    # ⑥ 긁힌 자국 64×16×1 (FX 20.4)
    dict(src="effects/supplement/spear_scratch_64x16_f1_magenta_8x.png", scale=8, split="blobs", axis="x", count=1,
         size=(64, 16), out="special/spear_scratch_64x16_f1.png", group="special", key="spear_scratch", fps=12, loop=False),
    # ② "끊음!" 표시 64×24×3 (FX 20.4)
    dict(src="ui/kit/interrupt_plate_64x24_f3_magenta_6x.png", scale=6, split="blobs", axis="y", count=3,
         size=(64, 24), out="special/interrupt_plate_64x24_f3.png", group="special", key="interrupt_plate", fps=12, loop=False),
    # 반딧불 4×4×2 (FX 20.5, 밤 장식, 연노랑). 환경 반복이라 결정처럼 느린 반복 [임시].
    dict(src="effects/supplement/firefly_4x4_f2_64x.png", scale=64, split="blobs", axis="x", count=2,
         size=(4, 4), out="ambient/firefly_4x4_f2.png", group="ambient", key="firefly", fps=5, loop=True),
    # 꾸러미 여는 연출 64×64×5 (UI I-M11/bundle_open). 원본이 이미 64칸 격자(4배).
    dict(src="effects/loot/crow_pack_open_64x64_f5.png", scale=4, split="grid", cols=5, rows=1,
         size=(64, 64), out="loot/crow_pack_open_64x64_f5.png", group="loot", key="crow_pack_open", fps=12, loop=False,
         ui="I-M11/bundle_open"),
    # 변이 표식 고리(지팡이에 붙는) 32×32×2 (FX 20.1). 원본 8배 격자.
    dict(src="wands/parts/wild_mutation_ring_32x32_f2.png", scale=8, split="grid", cols=2, rows=1,
         size=(32, 32), out="wand/mutation_ring_32x32_f2.png", group="wand", key="mutation_ring", fps=5, loop=True),
    # 시간대 아이콘 4 (I-W3, 32×32). 칸 순서 = 낮, 해 질 녘, 밤, 보스(그림을 열어 확인).
    dict(src="ui/icon/i-w3_wave_time_of_day_sheet_8x.png", scale=8, split="grid", cols=4, rows=1,
         size=(32, 32), out="icons/tod_32x32_f4.png", group="icons", key="sheet.tod",
         icons=["tod.day", "tod.dusk", "tod.night", "tod.boss"]),
    # 변덕 아이콘 8 (I-W1, 32×32). 칸 순서 = Ids.Whim 선언 순서(그림을 열어 확인: 몰려옴 화살·모래시계, 두꺼운 가죽 방패,
    # 큰 무리 덤불, 대장 행진 왕관, 마지막 숨 꺼진 초, 느린 탄막 달팽이, 긴 밤 달·박쥐, 좁은 들 가시 벽).
    dict(src="ui/icon/wave-modifiers_32x32_8x.png", scale=8, split="grid", cols=4, rows=2,
         size=(32, 32), out="icons/whim_32x32_f8.png", group="icons", key="sheet.whim",
         icons=["whim.crowding", "whim.thick_hide", "whim.big_pack", "whim.chief_parade",
                "whim.last_breath", "whim.slow_barrage", "whim.long_night", "whim.narrow_field"]),
    # 웨이브 틀 아이콘 5 (I-W2, 32×32). 원본은 1배(32칸 + 2px 틈). 칸 순서 = 06 요약서 표 순서(떼, 사냥대, 포대, 매복, 혼합).
    dict(src="ui/icon/wave_formations_32x32.png", scale=1, split="grid", cols=5, rows=1, gap=2,
         size=(32, 32), out="icons/frame_32x32_f5.png", group="icons", key="sheet.frame",
         icons=["frame.swarm", "frame.hunting_party", "frame.battery", "frame.ambush", "frame.mixed"]),
    # "빌림" 꼬리표 32×16 (I-M10/borrowed_tag)
    dict(src="ui/icon/borrow-tag.png", scale=12, split="blobs", axis="x", count=1,
         size=(32, 16), out="icons/borrowed_tag_32x16.png", group="icons", key="I-M10/borrowed_tag"),
    # 귀환 가방 칸 3 (I-M12, 32×32): 빈 / 찬 / 영웅(금 점선)
    dict(src="ui/screen/results/s-1601_return_bag_slots_32x32_f3.png", scale=8, split="grid", cols=3, rows=1,
         size=(32, 32), out="icons/bag_slot_32x32_f3.png", group="icons", key="sheet.bag_slot",
         icons=["I-M12/bag_slot_empty", "I-M12/bag_slot_filled", "I-M12/bag_slot_hero"]),
]

# 깨지는 조각 둘째 벌 64×64×18 ×5 (FX 20.1 "18개 × 2", 재질 = 쇳물 껍질·유리·종이·얼음·어둠 = fire·lightning·arcane·ice·dark 순).
for material, element in [("metal", "fire"), ("glass", "lightning"), ("paper", "arcane"), ("ice", "ice"), ("dark", "dark")]:
    JOBS.append(dict(src="effects/wand/break-shards-v2/%s_64x64_6x3_4x.png" % material, scale=4, split="grid", cols=6, rows=3,
                     size=(64, 64), out="ritual/break_pieces_b_%s_64x64_f18.png" % element, group="ritual",
                     key="break_pieces_b_" + element, fps=12, loop=False))


def load_rgba(path):
    im = Image.open(path).convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if (r, g, b) == MAGENTA:
                px[x, y] = (0, 0, 0, 0)
    return im


def phase(im, s, axis):
    """확대 격자의 위상: 이웃과 색이 다른 자리(경계)의 좌표를 s로 나눈 나머지 중 가장 많은 것."""
    if s == 1:
        return 0
    w, h = im.size
    px = im.load()
    hist = [0] * s
    if axis == "x":
        for x in range(1, w):
            for y in range(0, h, max(1, s // 2)):
                if px[x, y] != px[x - 1, y]:
                    hist[x % s] += 1
    else:
        for y in range(1, h):
            for x in range(0, w, max(1, s // 2)):
                if px[x, y] != px[x, y - 1]:
                    hist[y % s] += 1
    return max(range(s), key=lambda i: hist[i])


def shrink(im, s, fixed=None):
    """최근접 축소. 칸 하나가 한 색이 아니면 그 수를 돌려준다(0이어야 정상). fixed = 위상을 정해 줄 때."""
    if s == 1:
        return im.copy(), 0, (0, 0)
    ox, oy = fixed if fixed is not None else (phase(im, s, "x"), phase(im, s, "y"))
    w, h = im.size
    nw, nh = (w - ox) // s, (h - oy) // s
    src = im.load()
    out = Image.new("RGBA", (nw, nh), (0, 0, 0, 0))
    dst = out.load()
    bad = 0
    for j in range(nh):
        for i in range(nw):
            x0, y0 = ox + i * s, oy + j * s
            c = src[x0 + s // 2, y0 + s // 2]
            dst[i, j] = c
            if src[x0, y0] != c or src[x0 + s - 1, y0 + s - 1] != c or src[x0 + s - 1, y0] != c or src[x0, y0 + s - 1] != c:
                bad += 1
    # 위상 앞쪽 자투리(ox, oy px)에 그림이 있으면 잘린다 → 검사
    for y in range(h):
        for x in range(w):
            if (x < ox or y < oy or x >= ox + nw * s or y >= oy + nh * s) and src[x, y][3] != 0:
                raise SystemExit("자투리에 그림이 있다: %s" % ((x, y),))
    return out, bad, (ox, oy)


def bbox(im):
    return im.getbbox()  # 알파 0 아닌 영역(RGBA는 알파 기준)


def split_blobs(im, axis, count):
    """빈 줄로 갈라지는 덩어리. 덩어리가 count보다 많으면 가장 좁은 틈부터 이어 붙인다."""
    w, h = im.size
    a = im.getchannel("A").load()
    n = w if axis == "x" else h
    filled = []
    for i in range(n):
        if axis == "x":
            filled.append(any(a[i, y] for y in range(h)))
        else:
            filled.append(any(a[x, i] for x in range(w)))
    runs, start = [], None
    for i, f in enumerate(filled + [False]):
        if f and start is None:
            start = i
        elif not f and start is not None:
            runs.append([start, i])
            start = None
    while len(runs) > count:
        gaps = [(runs[k + 1][0] - runs[k][1], k) for k in range(len(runs) - 1)]
        _, k = min(gaps)
        runs[k][1] = runs[k + 1][1]
        del runs[k + 1]
    if len(runs) != count:
        raise SystemExit("덩어리 수가 다르다: %d (기대 %d)" % (len(runs), count))
    frames = []
    for s0, s1 in runs:
        box = (s0, 0, s1, h) if axis == "x" else (0, s0, w, s1)
        part = im.crop(box)
        bb = bbox(part)
        frames.append(part.crop(bb))
    return frames


def place(frame, size, label):
    fw, fh = size
    w, h = frame.size
    if w > fw or h > fh:
        raise SystemExit("%s: 그림 %dx%d가 칸 %dx%d보다 크다" % (label, w, h, fw, fh))
    cell = Image.new("RGBA", size, (0, 0, 0, 0))
    cell.paste(frame, ((fw - w) // 2, (fh - h) // 2))
    return cell


def build(job):
    path = os.path.join(ART, job["src"])
    im = load_rgba(path)
    fw, fh = job["size"]
    s = job["scale"]
    if job["split"] == "blobs":
        # 덩어리마다 격자 위상이 다를 수 있어(반딧불 두 프레임이 반 칸 어긋나 있다) 원본 크기에서 먼저 나누고,
        # 덩어리 테두리(= 확대 칸의 경계)에서 위상 0으로 줄인다.
        frames, bad = [], 0
        for part in split_blobs(im, job["axis"], job["count"]):
            if part.size[0] % s or part.size[1] % s:
                raise SystemExit("%s: 덩어리 %s가 %d배 칸에 맞지 않는다" % (job["src"], part.size, s))
            small, b, _ = shrink(part, s, (0, 0))
            bad += b
            frames.append(place(small, job["size"], job["src"]))
        ph = "덩어리별"
    else:
        small, bad, ph = shrink(im, s)
    if job["split"] == "grid":
        cols, rows, gap = job["cols"], job["rows"], job.get("gap", 0)
        if small.size != (cols * fw + (cols - 1) * gap, rows * fh + (rows - 1) * gap):
            raise SystemExit("%s: 축소 크기 %s가 격자 %dx%d칸 × %dx%d(틈 %d)와 다르다" % (job["src"], small.size, cols, rows, fw, fh, gap))
        px, py = fw + gap, fh + gap
        frames = [small.crop((c * px, r * py, c * px + fw, r * py + fh)) for r in range(rows) for c in range(cols)]
        for c in range(cols - 1):
            # 틈에 그림이 있으면 칸 나누기가 틀린 것
            if gap and small.crop((c * px + fw, 0, (c + 1) * px, small.size[1])).getbbox():
                raise SystemExit("%s: 칸 틈에 그림이 있다" % job["src"])
    sheet = Image.new("RGBA", (fw * len(frames), fh), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * fw, 0))
    return sheet, len(frames), bad, ph


def entry(job, n, frames=None):
    fw, fh = job["size"]
    rects = [[i * fw, 0, fw, fh] for i in range(n)] if frames is None else frames
    return {"path": job["out"], "size": [fw, fh], "frames": rects, "fps": job.get("fps", 0), "loop": job.get("loop", False),
            "source": "art/production/" + job["src"]}


def main():
    check = "--check" in sys.argv[1:]
    manifest = {"notes": {"origin": "top-left", "filter": "point", "mipmaps": False,
                          "built_by": "solo/Tools/art_supplement.py",
                          "about": "09 요약서 9절의 빠진 그림. 마젠타 배경·정수배 확대 원본을 규격 크기 투명 시트로 바꾼 것. "
                                   "icons 묶음의 열쇠는 콘텐츠 id(tod.*, whim.*, frame.*) 또는 'UI id/변형'(I-M10/borrowed_tag …)이다."}}
    differ = 0
    for job in JOBS:
        sheet, n, bad, ph = build(job)
        dst = os.path.join(OUT, job["out"].replace("/", os.sep))
        same = False
        if os.path.exists(dst):
            old = Image.open(dst).convert("RGBA")
            same = old.size == sheet.size and old.tobytes() == sheet.tobytes()
        if not same:
            differ += 1
            if not check:
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                sheet.save(dst, optimize=True)
        print("%-62s -> %-44s %dx%d x%d  위상%s 섞인칸%d %s" % (job["src"], job["out"], job["size"][0], job["size"][1], n, ph, bad,
                                                            "같음" if same else ("다름" if check else "씀")))
        g = manifest.setdefault(job["group"], {})
        g[job["key"]] = entry(job, n)
        fw, fh = job["size"]
        for i, cid in enumerate(job.get("icons") or []):
            e = entry(job, 1, [[i * fw, 0, fw, fh]])
            g[cid] = e
        if job.get("ui"):
            manifest.setdefault("icons", {})[job["ui"]] = entry(job, n)
    text = json.dumps(manifest, ensure_ascii=False, indent=1) + "\n"
    mpath = os.path.join(OUT, "manifest.json")
    old = open(mpath, encoding="utf-8").read() if os.path.exists(mpath) else None
    if old != text:
        differ += 1
        if not check:
            with open(mpath, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
    print("art_supplement: 결과 %d장 + manifest, %s %d" % (len(JOBS), "다른 것" if check else "새로 쓴 것", differ))
    return 1 if check and differ else 0


if __name__ == "__main__":
    sys.exit(main())
