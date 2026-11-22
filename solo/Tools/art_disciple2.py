#!/usr/bin/env python
# solo/Tools/art_disciple2.py — 제자 2(char.2) 교체 묶음(solo/CharacterAssets/char.2)을 Resources에 넣는다.
# 제자 3(커밋 c486955)·제자 4·5와 같은 방식이다.
#   (1) 몸·선택·초상·실루엣 → players/chorong/*_v2.png + players/solo_overrides.json의 characters.chorong
#   (2) 지팡이 손·검사·아이콘, 반응 부품·야생 물처럼 원본 자리가 있는 그림 → <원본>_v2.png + solo_texture_overrides.json
#       (크기가 원본과 같을 때만. manifest의 칸 rect를 그대로 쓴다. 크기가 다르면 연결하지 않고 알린다)
#   (3) 원본 자리가 없는 새 그림(fx/*, enhancement/*, wand/trace_* …) → supplement/solo/char.2/ + 그 폴더 manifest.json
#       (효과 manifest 형식, 묶음 "char.2", 열쇠 = 교체 묶음 manifest의 graphics 열쇠). ArtLibrary.Effect("char.2", 열쇠)로 읽는다.
#   (4) 소리 audio/* → Assets/Resources/Audio/char.2/ (복사만. 재생 연결은 하지 않는다)
# "provenance": "existing_project_asset"(기존 그림 사본)은 원본과 같으면 건너뛴다.
# 폴더 이름(chorong)은 ArtAliases의 대응표(char.2 = 두 번째 폴더)와 같다.
#
# 쓰는 법(Git Bash, solo/에서):  python -I Tools/art_disciple2.py
import hashlib
import json
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SOLO = os.path.dirname(HERE)
SRC = os.path.join(SOLO, "CharacterAssets", "char.2")
ART = os.path.join(SOLO, "Assets", "Resources", "Art")
PLAYERS = os.path.join(ART, "players")
AUDIO = os.path.join(SOLO, "Assets", "Resources", "Audio", "char.2")
EXTRA = "supplement/solo/char.2/"
FOLDER = "chorong"  # ArtAliases.CharacterFolders[1] (char.2)
GROUP = "char.2"

COPIES = [("body_64.png", "body_v2.png"), ("selection_192x192.png", "selection_192x192_v2.png"),
          ("portraits_64x64_f4.png", "portraits_64x64_f4_v2.png"), ("silhouette_32x32.png", "silhouette_32x32_v2.png")]

# 교체 묶음 graphics 열쇠 → 원본 그림(Art 기준). "source"가 적힌 항목은 그 값을 쓴다.
REPLACES = {
    "wand/hand": "wands/chorong/hand.png",
    "wand/inspection": "wands/chorong/inspection.png",
    "wand/icon": "wands/chorong/icon.png",
    "layers/head_lantern": "supplement/layers/reactive/chorong/head_lantern/sheet.png",
    "layers/body_without_lantern": "supplement/layers/reactive/chorong/body_without_lantern.png",
}
SKIP = {"portraits_64x64_f4", "silhouette_32x32", "body_64", "selection"}  # (1)에서 처리


def md5(p):
    return hashlib.md5(open(p, "rb").read()).hexdigest()


def v2_of(path):
    root, ext = os.path.splitext(path)
    return root + "_v2" + ext


def write_json(path, data):
    raw = open(path, "rb").read().decode("utf-8-sig") if os.path.exists(path) else ""
    crlf = "\r\n" in raw
    text = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    if crlf:
        text = text.replace("\n", "\r\n")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def main():
    man = json.load(open(os.path.join(SRC, "manifest.json"), encoding="utf-8"))
    if man.get("characterId") != "char.2":
        raise SystemExit("characterId가 char.2가 아니다")
    g = man["graphics"]
    body = g["body_64"]
    if body["file"] != "body_64.png" or len(body["clips"]) != 37 or body["frameCount"] != 147:
        raise SystemExit("body_64 항목이 37클립·147칸이 아니다")

    # (1) 캐릭터
    dst_dir = os.path.join(PLAYERS, FOLDER)
    os.makedirs(dst_dir, exist_ok=True)
    for src, dst in COPIES:
        shutil.copyfile(os.path.join(SRC, src), os.path.join(dst_dir, dst))
        print("copy %s -> players/%s/%s" % (src, FOLDER, dst))
    path = os.path.join(PLAYERS, "solo_overrides.json")
    data = json.loads(open(path, "rb").read().decode("utf-8-sig"))
    data.setdefault("characters", {})[FOLDER] = {
        "body": FOLDER + "/body_v2.png",
        "frames": body["frameCount"],
        "portrait": FOLDER + "/portraits_64x64_f4_v2.png",
        "selection": FOLDER + "/selection_192x192_v2.png",
        "silhouette": FOLDER + "/silhouette_32x32_v2.png",
        "artworkSource": "solo/CharacterAssets/char.2 (manifest.json graphics.body_64)",
        "clips": body["clips"],
    }
    write_json(path, data)
    print("players/solo_overrides.json: characters.%s = %d clips" % (FOLDER, len(body["clips"])))

    # (2)(3) 그 밖의 그림
    tov_path = os.path.join(ART, "solo_texture_overrides.json")
    tov = json.loads(open(tov_path, "rb").read().decode("utf-8-sig"))
    extra = {GROUP: {}}
    not_wired = []
    for key, e in g.items():
        if key in SKIP or not e.get("file"):
            continue
        src = os.path.join(SRC, e["file"].replace("/", os.sep))
        orig = REPLACES.get(key) or e.get("source")
        if orig:
            orig_abs = os.path.join(ART, orig.replace("/", os.sep))
            if not os.path.exists(orig_abs):
                not_wired.append((key, "원본 없음 " + orig))
                continue
            if md5(orig_abs) == md5(src):
                continue  # 원본과 같은 사본
            if Image.open(orig_abs).size != Image.open(src).size:
                not_wired.append((key, "크기 다름 %s vs %s" % (Image.open(src).size, Image.open(orig_abs).size)))
                continue
            v2 = v2_of(orig)
            shutil.copyfile(src, os.path.join(ART, v2.replace("/", os.sep)))
            tov[orig] = v2
            print("replace %s -> %s" % (e["file"], v2))
            continue
        rel = EXTRA + e["file"]
        dst = os.path.join(ART, rel.replace("/", os.sep))
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        rects = e.get("rects")
        if not rects:
            w, h = Image.open(src).size
            rects = [[0, 0, w, h]]
        cell = e.get("cell") or rects[0][2:4]
        entry = dict(e)
        entry.update({"path": e["file"], "size": cell, "frames": rects, "fps": 12, "loop": False})
        extra[GROUP][key] = entry
    write_json(tov_path, tov)

    # 머리 등불은 칸 배치가 원본(8열 2줄 128×32)과 달라(9칸 한 줄 144×16) 덮어쓰기 표가 아니라
    # supplement/layers/solo_overrides.json의 그룹 항목으로 바꾼다(제자 4·5의 층 교체와 같은 방식). 프레임 열쇠 순서는 같다.
    hl = g.get("layers/head_lantern")
    if hl:
        lay_dir = os.path.join(ART, "supplement", "layers")
        base = json.load(open(os.path.join(lay_dir, "manifest.json"), encoding="utf-8"))["groups"]["reactive/chorong/head_lantern"]
        if len(base["frames"]) != len(hl["rects"]):
            raise SystemExit("head_lantern 프레임 수가 원본과 다르다")
        shutil.copyfile(os.path.join(SRC, hl["file"].replace("/", os.sep)),
                        os.path.join(lay_dir, "reactive", "chorong", "head_lantern", "sheet_v2.png"))
        entry = json.loads(json.dumps(base))
        entry["sheet"] = "reactive/chorong/head_lantern/sheet_v2.png"
        entry["frameSize"] = hl["cell"]
        entry["columns"] = len(hl["rects"])
        for f, r in zip(entry["frames"], hl["rects"]):
            f["rect"] = r
        ov_path = os.path.join(lay_dir, "solo_overrides.json")
        ov = json.loads(open(ov_path, "rb").read().decode("utf-8-sig"))
        ov.setdefault("groups", {})["reactive/chorong/head_lantern"] = entry
        write_json(ov_path, ov)
        not_wired = [x for x in not_wired if x[0] != "layers/head_lantern"]
        print("replace layers/head_lantern.png -> supplement/layers/reactive/chorong/head_lantern/sheet_v2.png (layers/solo_overrides)")
    os.makedirs(os.path.join(ART, EXTRA), exist_ok=True)
    with open(os.path.join(ART, EXTRA.replace("/", os.sep), "manifest.json"), "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(extra, ensure_ascii=False, indent=1) + "\n")
    print("supplement/solo/char.2/manifest.json: %d entries" % len(extra[GROUP]))
    for k, why in not_wired:
        print("NOT WIRED %s: %s" % (k, why))

    # (4) 소리(복사만)
    a_src = os.path.join(SRC, "audio")
    if os.path.isdir(a_src):
        os.makedirs(AUDIO, exist_ok=True)
        n = 0
        for name in sorted(os.listdir(a_src)):
            if name.endswith(".wav") or name.endswith(".json"):
                shutil.copyfile(os.path.join(a_src, name), os.path.join(AUDIO, name))
                n += 1
        print("audio: %d files -> Assets/Resources/Audio/char.2/" % n)
    return 0


if __name__ == "__main__":
    sys.exit(main())
