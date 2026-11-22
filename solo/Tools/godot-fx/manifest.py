"""Writes Assets/Resources/Art/effects/styles/<group>/manifest.json for every style sheet
(styles.STYLES that are not parts + styles.COMPOSED), in the effect manifest format that
ManifestParser.ParseEffects reads: one group "styles.<group>", entry = {path (relative to the
folder), size [w, h], frames [[x, y, w, h] ...], fps, loop, pivot [x, y] (px from the frame's
top-left), clips {name: [first, last]} (optional), use (note)}.
  python -I manifest.py"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True  # python -I ignores PYTHONDONTWRITEBYTECODE
sys.path.insert(0, HERE)
from styles import STYLES, COMPOSED  # noqa: E402

ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Art"))

REUSE = {
    "char.1": [
        "C 튀는 불씨 탄 = supplement/solo/char.1/kiln_ember_16x16_f2.png (또는 effects/special/kiln_ember_8x8_f2_v2.png)",
        "C 튕김 불티 = effects/player/hit_fire_32x32_f3_v2.png, 꼬리 = effects/wand/trail_fire_32x16_f4_v2.png",
        "K 땅 내리침 = styles/common ground_slam + ground_crack + debris_burst + dust_skirt",
    ],
    "char.2": [
        "A 적중 스파크 = effects/player/hit_lightning_32x32_f3_v2.png (큰 것 hit_large_lightning_64x64_f4_v2.png)",
        "B 작은 지짐 호(구체에서 적에게) = chain_link 를 짧게 잘라 쓰거나 effects/player/hit_lightning_32x32_f3_v2.png",
        "K 벼락 질주 궤적 = 기존 bolt_dash_trail/arrival(supplement/solo/char.2) 위에 strike_bolt + strike_blast 를 궤적 따라 시간차로",
    ],
    "char.3": [
        "A 날아가는 책장 탄 = wands/galpi/animation/page_large.png (16x16 x4, 회전) / page_small.png (10x12 x4)",
        "A 책장 적중 = wands/galpi/animation/hit_page.png (32x32 x4)",
        "K 흩날리는 책장 입자 = effects/special/scattered_pages_16x16_f3_v2.png, page_small.png",
        "K 마법진 = effects/special/arcane_copy_96x96_f4_v2.png",
    ],
    "char.4": [
        "A 3갈래 파편 탄 = effects/special/ice_screen_shard_16x16_f3_v2.png, 갈라질 때 섬광 = effects/player/hit_ice_32x32_f3_v2.png",
        "C 고드름 착지 깨짐 = CharacterAssets/char.4/thin-ice-break.png (48x48 x4) 또는 effects/status/frozen_shatter_96x96_f4_v2.png",
    ],
    "char.5": [
        "B 그림자 낫 휩쓸기 = effects/special/bloom_dark_192x192_f6_v2.png (초승달 궤적)",
        "C 눈알 추적탄 = wands/geumeum/parts_and_effects/floating_eye.png (20x20 x3) + 둘레 effects/player/shadow_orb_40x40_f4_v2.png",
        "C 갈라짐 = effects/player/shadow_orb_fade_40x40_f4_v2.png, 적중 = effects/player/hit_dark_32x32_f3_v2.png",
        "K 바닥 충격 = effects/special/shadow_pool_128x128_f4.png + styles/common ground_slam",
    ],
    "common": [],
}


def entry(out, w, h, n, meta):
    e = {"path": os.path.basename(out), "size": [w, h],
         "frames": [[i * w, 0, w, h] for i in range(n)],
         "fps": meta["fps"], "loop": meta["loop"], "pivot": meta["pivot"]}
    if meta.get("clips"):
        e["clips"] = meta["clips"]
    e["use"] = meta.get("use", "")
    return e


def main():
    groups = {}
    for j in STYLES:
        if j.get("part"):
            continue
        m = j["meta"]
        groups.setdefault(m["group"], {})[m["key"]] = entry(j["out"], j["w"], j["h"], j["frames"], m)
    for c in COMPOSED:
        m = c["meta"]
        groups.setdefault(m["group"], {})[m["key"]] = entry(c["out"], c["w"], c["h"], c["frames"], m)
    bad = 0
    for g, entries in sorted(groups.items()):
        folder = os.path.join(ART, "effects", "styles", g)
        for k, e in entries.items():
            if not os.path.exists(os.path.join(folder, e["path"])):
                print("MISSING SHEET", g, e["path"]); bad += 1
        doc = {"notes": {"origin": "top-left", "filter": "point", "mipmaps": False, "pixelsPerUnit": 64,
                         "built_by": "solo/Tools/godot-fx/bake.sh (styles.py, compose.py, manifest.py)",
                         "pivot": "pivot = 프레임 왼쪽 위 기준 픽셀 좌표",
                         "reuse": REUSE.get(g, [])},
               "styles." + g: dict(sorted(entries.items()))}
        with open(os.path.join(folder, "manifest.json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(doc, f, ensure_ascii=False, indent=1)
            f.write("\n")
        print("manifest", g, len(entries))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
