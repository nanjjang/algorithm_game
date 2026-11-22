#!/usr/bin/env python
# solo/Tools/art_disciple1.py — 제자 1(char.1) 교체 묶음(solo/Assets/ArtVariants/char.1, 128×128 칸 시트들)을 Resources에 넣는다.
# 제자 2·3(Tools/art_disciple2.py, 커밋 c486955)과 같은 결과가 나오게 한다:
#   (1) 몸: 대표 자세 시트(combat, locomotion, battle_reactions, signature_cast, knockout, camp_upgrade)의 128칸을
#       players/dalgu/body_v2.png 한 장에 모으고(자세마다 한 칸, 12열), players/solo_overrides.json의 characters.dalgu에
#       37클립을 적는다. 칸 rect·발·손·머리·어깨 점은 다른 제자와 같은 64px 논리 좌표로 적고 "pixelScale": 2를 붙인다
#       (그림은 2배 해상도 그대로 — ArtLibrary가 rect×2로 자르고 PPU 128로 만든다. 줄이지 않으니 세부가 남는다).
#       클립 길이(프레임 수 ÷ fps)는 원본 manifest와 같게 둔다(표시 시간·강화 반응 시간이 그대로).
#       손·어깨 점은 원본 dalgu 프레임의 점 그대로, 머리 점은 두 그림의 불투명 영역(bbox) 비례로 옮긴 값이다 [임시: 화면 QA로 고친다].
#   (2) 초상(2×2 → 가로 띠 128×128×4), 선택 192×192, 실루엣 32×32 → players/dalgu/*_v2.png
#   (3) 지팡이 손·검사·아이콘(원본과 크기·배치가 같다) → wands/dalgu/*_v2.png + solo_texture_overrides.json
#   (4) 원본 자리가 없거나 크기가 다른 효과(불가마 테두리·불티·시전 문양, 끓어넘침, HUD 아이콘, 대장간, 김·뚜껑, 야생 물)
#       → supplement/solo/char.1/ + manifest.json(묶음 "char.1", ArtLibrary.Effect("char.1", 열쇠)).
#       효과 층(EffectLayer)은 시전자가 제자 1이면 이 묶음을 먼저 찾는다.
# 원본(ArtVariants/char.1)은 지우지 않는다. .meta는 복사하지 않는다(Unity가 새 GUID로 만든다).
# 폴더 이름(dalgu)은 ArtAliases의 대응표(char.1 = 첫 번째 폴더)와 같다.
#
# 쓰는 법(Git Bash, solo/에서):  python -I Tools/art_disciple1.py
import json
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SOLO = os.path.dirname(HERE)
SRC = os.path.join(SOLO, "Assets", "ArtVariants", "char.1")
ART = os.path.join(SOLO, "Assets", "Resources", "Art")
PLAYERS = os.path.join(ART, "players")
FOLDER = "dalgu"  # ArtAliases.CharacterFolders[0] (char.1)
GROUP = "char.1"
EXTRA = "supplement/solo/char.1/"
SCALE = 2          # 128칸 = 64px 논리 칸의 2배
CELL = 64          # 논리 칸
COLS = 12

DIRS = ["down", "right", "up"]


def sheet(name, cols, rows):
    im = Image.open(os.path.join(SRC, name)).convert("RGBA")
    cw, ch = im.width // cols, im.height // rows
    return [im.crop((c * cw, r * ch, c * cw + cw, r * ch + ch)) for r in range(rows) for c in range(cols)]


def bbox(im, thr=8):
    return im.getchannel("A").point(lambda a: 255 if a > thr else 0).getbbox()


def write_json(path, data, indent=2):
    raw = open(path, "rb").read().decode("utf-8-sig") if os.path.exists(path) else ""
    crlf = "\r\n" in raw
    text = json.dumps(data, ensure_ascii=False, indent=indent) + "\n"
    if crlf:
        text = text.replace("\n", "\r\n")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def main():
    combat = sheet("combat_128x128_f12.png", 4, 3)            # 대기, 이동 A, 이동 B, 시전
    loco = sheet("locomotion_128x128_f12.png", 4, 3)           # 지나가기 A, B, 자세 이동 A, B
    react = sheet("battle_reactions_128x128_f12.png", 4, 3)    # 자세 준비, 피격, 발사 반동, 강한 피격
    sig = sheet("signature_cast_128x128_f12.png", 4, 3)        # 준비, 모으기, 발동, 회복
    knock = sheet("knockout_128x128_f8.png", 4, 2)             # 반동, 휘청, 무릎, 넘어짐, 쓰러짐, 웅크림, 기상, 앉기
    camp = sheet("camp_upgrade_128x128_f12.png", 4, 3)         # 앉기 인사 박수 도발 / 절 춤 손들기 좌절 / 대기 충전 성공 실패

    poses = []   # (이름, 그림)
    index = {}

    def pose(name, im):
        if name not in index:
            index[name] = len(poses)
            poses.append((name, im))
        return name

    for d, dn in enumerate(DIRS):
        r = d * 4
        pose("idle_" + dn, combat[r + 0]); pose("stepA_" + dn, combat[r + 1]); pose("stepB_" + dn, combat[r + 2]); pose("cast_" + dn, combat[r + 3])
        pose("passA_" + dn, loco[r + 0]); pose("passB_" + dn, loco[r + 1]); pose("braceA_" + dn, loco[r + 2]); pose("braceB_" + dn, loco[r + 3])
        pose("ready_" + dn, react[r + 0]); pose("flinch_" + dn, react[r + 1]); pose("recoil_" + dn, react[r + 2]); pose("wound_" + dn, react[r + 3])
        pose("sigPrep_" + dn, sig[r + 0]); pose("sigGather_" + dn, sig[r + 1]); pose("sigRelease_" + dn, sig[r + 2]); pose("sigRecover_" + dn, sig[r + 3])
    for i, n in enumerate(["koRecoil", "koStagger", "koKneel", "koFall", "koDown", "koCurl", "koRise", "koSit"]):
        pose(n, knock[i])
    for i, n in enumerate(["sit", "wave", "clap", "taunt", "bow", "dance", "raise", "frustrated", "upWait", "upCharge", "upSuccess", "upFail"]):
        pose(n, camp[i])

    # 클립: 원본 클립 이름 → 자세 목록(fps는 원본 길이를 지키게 정한다)
    clips = {}
    for dn in DIRS:
        clips["idle_" + dn] = ["idle_" + dn] * 4
        clips["walk_" + dn] = ["stepA_" + dn, "passA_" + dn, "stepB_" + dn, "passB_" + dn]
        clips["stance_walk_" + dn] = ["braceA_" + dn, "ready_" + dn, "braceB_" + dn, "ready_" + dn]
        clips["fire_" + dn] = ["cast_" + dn, "recoil_" + dn, "ready_" + dn]
        clips["special_" + dn] = ["ready_" + dn, "cast_" + dn, "cast_" + dn, "recoil_" + dn]
        clips["signature_" + dn] = ["sigPrep_" + dn, "sigGather_" + dn, "sigGather_" + dn, "sigRelease_" + dn, "sigRelease_" + dn, "sigRecover_" + dn]
        clips["hit_" + dn] = ["wound_" + dn, "flinch_" + dn]
    clips["death_down"] = ["koRecoil", "koStagger", "koKneel", "koFall", "koDown", "koCurl"]
    clips["sit_down"] = ["koSit", "koSit"]
    clips["wave_down"] = ["wave"] * 4
    clips["clap_down"] = ["clap"] * 4
    clips["taunt_down"] = ["taunt"] * 4
    clips["bow_down"] = ["bow"] * 4
    clips["sit_down_down"] = ["sit"] * 4
    clips["dance_down"] = ["dance"] * 4
    clips["lift_wand_down"] = ["raise"] * 4
    clips["frustrated_down"] = ["frustrated"] * 4
    clips["upgrade_wait_down"] = ["upWait"] * 2
    clips["upgrade_charge_down"] = ["upCharge"] * 2
    clips["upgrade_success_down"] = ["upSuccess"] * 4
    clips["upgrade_fail_down"] = ["upFail"] * 4
    clips["duel_win_down"] = ["upSuccess"] * 4
    clips["duel_loss_down"] = ["frustrated"] * 4

    # 원본(players/manifest.json의 dalgu)과 원본 몸 시트
    base = json.load(open(os.path.join(PLAYERS, "manifest.json"), encoding="utf-8"))
    pc = base["characters"][FOLDER]
    old_body = Image.open(os.path.join(PLAYERS, FOLDER, "body.png")).convert("RGBA")
    if set(pc["clips"].keys()) != set(clips.keys()):
        raise SystemExit("클립 이름이 원본과 다르다: %s" % (set(pc["clips"].keys()) ^ set(clips.keys())))

    # (1) 몸 시트
    rows = (len(poses) + COLS - 1) // COLS
    body = Image.new("RGBA", (COLS * CELL * SCALE, rows * CELL * SCALE), (0, 0, 0, 0))
    for i, (name, im) in enumerate(poses):
        if im.size != (CELL * SCALE, CELL * SCALE):
            raise SystemExit("칸 크기가 128이 아니다: " + name)
        body.paste(im, ((i % COLS) * CELL * SCALE, (i // COLS) * CELL * SCALE))
    dst_dir = os.path.join(PLAYERS, FOLDER)
    body.save(os.path.join(dst_dir, "body_v2.png"))
    print("body_v2.png: %d poses, %dx%d (pixelScale %d)" % (len(poses), body.width, body.height, SCALE))

    def map_point(p, ob, nb):
        # 원본 bbox(64px) 안의 비율을 새 bbox(논리 64px)에 옮긴다.
        ox0, oy0, ox1, oy1 = ob
        nx0, ny0, nx1, ny1 = nb
        fx = (p[0] - ox0) / max(1.0, ox1 - ox0)
        fy = (p[1] - oy0) / max(1.0, oy1 - oy0)
        return [int(round(nx0 + fx * (nx1 - nx0))), int(round(ny0 + fy * (ny1 - ny0)))]

    out_clips = {}
    for name, old in pc["clips"].items():
        seq = clips[name]
        old_frames = old["frames"]
        duration = len(old_frames) / float(old["fps"])
        fps = round(len(seq) / duration, 3)
        frames = []
        for j, pname in enumerate(seq):
            k = min(len(old_frames) - 1, int(j * len(old_frames) / len(seq)))
            of = old_frames[k]
            x, y, w, h = of["rect"]
            ob = bbox(old_body.crop((x, y, x + w, y + h)))
            nb = bbox(poses[index[pname]][1])
            nb = (nb[0] / SCALE, nb[1] / SCALE, nb[2] / SCALE, nb[3] / SCALE)
            i = index[pname]
            f = {"rect": [(i % COLS) * CELL, (i // COLS) * CELL, CELL, CELL], "feet": [30, 60]}
            # 손·어깨는 원본 점을 그대로 쓴다(두 그림 모두 발 (30,60) 기준이고 몸 폭이 비슷해 손 자리가 맞는다 — 화면 비교로 확인).
            # 머리는 새 그림의 모자가 더 커서 bbox 비례로 옮긴다(머리 위 표시·야생 물이 모자에 붙게).
            for key in ("hand", "shoulder"):
                if key in of:
                    f[key] = list(of[key])
            if "head" in of and ob:
                f["head"] = map_point(of["head"], ob, nb)
            frames.append(f)
        out_clips[name] = {"fps": fps, "loop": bool(old.get("loop")), "frames": frames}

    # (2) 초상(2×2 → 가로 띠), 선택, 실루엣
    por = sheet("portraits_128x128_f4.png", 2, 2)
    strip = Image.new("RGBA", (128 * 4, 128), (0, 0, 0, 0))
    for i, im in enumerate(por):
        strip.paste(im, (i * 128, 0))
    strip.save(os.path.join(dst_dir, "portraits_128x128_f4_v2.png"))
    shutil.copyfile(os.path.join(SRC, "selection_192x192.png"), os.path.join(dst_dir, "selection_192x192_v2.png"))
    shutil.copyfile(os.path.join(SRC, "silhouette_32x32.png"), os.path.join(dst_dir, "silhouette_32x32_v2.png"))

    path = os.path.join(PLAYERS, "solo_overrides.json")
    data = json.loads(open(path, "rb").read().decode("utf-8-sig"))
    data.setdefault("characters", {})[FOLDER] = {
        "body": FOLDER + "/body_v2.png",
        "frames": len(poses),
        "pixelScale": SCALE,
        "portrait": FOLDER + "/portraits_128x128_f4_v2.png",
        "selection": FOLDER + "/selection_192x192_v2.png",
        "silhouette": FOLDER + "/silhouette_32x32_v2.png",
        "artworkSource": "solo/Assets/ArtVariants/char.1 (Tools/art_disciple1.py)",
        "clips": out_clips,
    }
    write_json(path, data)
    print("players/solo_overrides.json: characters.%s = %d clips" % (FOLDER, len(out_clips)))

    # (3) 지팡이(원본과 크기가 같을 때만 덮어쓰기 표)
    tov_path = os.path.join(ART, "solo_texture_overrides.json")
    tov = json.loads(open(tov_path, "rb").read().decode("utf-8-sig"))
    for src, orig in [("wand_hand_32x64_f84.png", "wands/dalgu/hand.png"),
                      ("wand_inspection_192x192_f14.png", "wands/dalgu/inspection.png"),
                      ("wand_icons_64x64_f14.png", "wands/dalgu/icon.png")]:
        s = os.path.join(SRC, src)
        o = os.path.join(ART, orig.replace("/", os.sep))
        if Image.open(s).size != Image.open(o).size:
            print("NOT WIRED %s: 크기 다름" % src)
            continue
        v2 = orig[:-4] + "_v2.png"
        shutil.copyfile(s, os.path.join(ART, v2.replace("/", os.sep)))
        tov[orig] = v2
        print("replace %s -> %s" % (src, v2))
    write_json(tov_path, tov)

    # (4) 새 묶음 "char.1"
    extra = {}

    def add(key, file, cols, rows, cell, count=None, fps=12, loop=False, scale=1, pivot=None, note=None):
        src = os.path.join(SRC, file)
        dst = os.path.join(ART, (EXTRA + file).replace("/", os.sep))
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        n = count or cols * rows
        lw, lh = int(round(cell[0] / scale)), int(round(cell[1] / scale))
        e = {"path": file, "size": [lw, lh],
             "frames": [[(i % cols) * lw, (i // cols) * lh, lw, lh] for i in range(n)],
             "fps": fps, "loop": loop}
        if scale != 1:
            e["pixelScale"] = scale
        if pivot:
            e["pivot"] = pivot
        if note:
            e["note"] = note
        extra[key] = e

    add("kiln_border", "kiln_border_320x320_f4.png", 4, 1, (320, 320), note="special/kiln_border")
    add("kiln_ember", "kiln_ember_16x16_f2.png", 2, 1, (16, 16), scale=2, note="special/kiln_ember (8x8 logical)")
    # 시전 문양은 원본 자리(cast_seal_11)가 96px이라 128px 그림을 4/3 배수로 적어 화면 크기를 원본과 같게 둔다.
    add("cast_seal", "kiln_cast_seal_128x128_f4.png", 4, 1, (128, 128), scale=round(128 / 96.0, 6), note="special/cast_seal_11")
    add("overflow_steam_arc", "overflow_320x320_f5.png", 5, 1, (320, 320), note="player/overflow_steam_arc")
    add("hud_icons", "hud_icons_64x64_f4.png", 2, 2, (64, 64), note="kiln, overflow, heat lock, inside kiln")
    add("steam", "steam_32x32_f12.png", 4, 3, (32, 32), fps=10, loop=True, scale=2, note="layers reactive/dalgu/steam (normal, threshold, near_lock)")
    add("lid", "lid_48x32_f4.png", 4, 1, (48, 32), fps=10, loop=True, scale=2, note="layers reactive/dalgu/lid")
    add("body_lidless", "body_lidless_128x128_f12.png", 4, 3, (128, 128), scale=2, pivot=[30, 60],
        note="rows down/right/up; columns idle, step A, step B, cast")
    add("wild_stain", "wild_stain_64x64_f15.png", 3, 5, (64, 64), note="rows soot, grass_hem, metal_patch, lid_feather, spout_embers; columns down/right/up")
    add("forge", "forge_128x128_f8.png", 4, 2, (128, 128), note="hammer raised, mid, impact, sparks / coal, copper ring, scar, scar smoke")
    add("forge_sparks", "forge_sparks_64x64_f4.png", 4, 1, (64, 64))
    add("forge_backdrop", "forge_backdrop_1024x576.png", 1, 1, (1024, 576))
    with open(os.path.join(ART, EXTRA.replace("/", os.sep), "manifest.json"), "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps({GROUP: extra}, ensure_ascii=False, indent=1) + "\n")
    print("supplement/solo/char.1/manifest.json: %d entries" % len(extra))
    return 0


if __name__ == "__main__":
    sys.exit(main())
