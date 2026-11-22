"""Style sheets for the per-character basic attack types (A/B/C) and the unique-technique (K)
upgrades. Imported by jobs.py; each entry becomes a Godot job (or a compose.py-only entry).

Output: Assets/Resources/Art/effects/styles/<group dir>/<key>_<W>x<H>_f<N>.png, one row,
left -> right. manifest.py writes <group dir>/manifest.json (effect manifest format, group
"styles.<group dir>").

Fields beyond the normal job fields:
  out    path under Assets/Resources/Art (final sheet) — or None for a "part" that compose.py uses
  part   True = post.py writes it to <raw>/_parts/<id>.png instead of the art folder
  meta   {group, key, fps, loop, pivot [x, y] px from the frame's top-left, clips {name: [first, last]}, use}
"""

STYLES = []      # Godot-rendered (post.py colours them)
COMPOSED = []    # built by compose.py (no Godot job of their own); listed for manifest.py

GAM = {"arcane": 1.15, "fire": 1.45, "lightning": 1.2, "ice": 1.0, "dark": 1.0, "soil": 1.0, "smoke": 1.0}


def _name(group, key, w, h, n):
    return "effects/styles/%s/%s_%dx%d_f%d.png" % (group, key, w, h, n)


def style(group, key, w, h, n, shader, ramp, params, fps=12, loop=False, pivot=None, use="", clips=None,
          part=False, **kw):
    d = {"id": "styles__%s__%s" % (group, key), "orig": None,
         "out": None if part else _name(group, key, w, h, n), "part": part,
         "w": w, "h": h, "frames": n, "shader": shader, "ramp": ramp, "params": params,
         "loop": loop, "gamma": GAM.get(ramp, 1.0)}
    d.update(kw)
    if not part:
        d["meta"] = {"group": group, "key": key, "fps": fps, "loop": loop,
                     "pivot": pivot or [w // 2, h // 2], "use": use}
        if clips:
            d["meta"]["clips"] = clips
    STYLES.append(d)
    return d


def composed(group, key, w, h, n, fps=12, loop=False, pivot=None, use="", clips=None):
    COMPOSED.append({"out": _name(group, key, w, h, n), "w": w, "h": h, "frames": n,
                     "meta": {"group": group, "key": key, "fps": fps, "loop": loop,
                              "pivot": pivot or [w // 2, h // 2], "use": use, **({"clips": clips} if clips else {})}})


# ------------------------------------------------------------------ char.1 fire
style("char.1", "mortar_shell", 32, 32, 4, "flame", "fire",
      {"horiz": 1.0, "base": 0.72, "radius": 0.26, "height": 0.8, "turb": 0.6, "core_ball": 0.8,
       "embers": 3, "scale": 1.2}, fps=12, loop=True, pivot=[22, 16], seed=4.1, rim=1, floor=2,
      use="A 화로 포탄: 포물선으로 나는 불덩이 탄(오른쪽으로 날아감, 진행 방향으로 회전)")
style("char.1", "mortar_blast", 96, 96, 6, "burst", "fire",
      {"rays": 14, "ray_len": 0.92, "ray_w": 5.0, "core": 15.0, "sparkles": 5, "spark_arm": 2.5,
       "embers": 16, "ring": 0.8}, fps=16, pivot=[48, 48], seed=2.7, rim=2, floor=2,
      use="A 화로 포탄 착탄 폭발")
style("char.1", "fire_floor", 64, 32, 6, "patch", "fire",
      {"squash": 0.42, "radius": 0.46, "tongues": 11, "tongue_h": 0.36, "tongue_w": 0.06, "scorch": 0.8,
       "cy": 0.22}, fps=10, loop=True, pivot=[32, 22], seed=1.9, rim=1, floor=1,
      use="A 화로 포탄 불바닥(착탄 자리에 남는 반복)")
style("char.1", "flame_jet", 96, 48, 6, "cone", "fire",
      {"mode": 0, "origin_x": 0.05, "spread": 0.3, "len": 0.9, "puffs": 30, "size0": 3.5, "size1": 11.0},
      fps=14, loop=True, pivot=[5, 24], seed=3.3, rim=1, floor=2, gamma=1.0,
      use="B 불꽃 방사: 짧은 부채꼴 화염 줄기(왼쪽 끝 = 지팡이 끝, 오른쪽으로 분사)")
style("char.1", "fire_pillar", 48, 128, 8, "pillar", "fire",
      {"width": 0.3, "tip": 0.05, "base": 0.94, "grow0": 0.08, "widen": 0.2, "motes": 6, "base_glow": 0.9,
       "flame": 1.0, "fade_end": 0.85}, fps=14, pivot=[24, 120], seed=5.5, rim=1, floor=2,
      use="K 불가마 강화: 바깥으로 연달아 솟는 불기둥(바닥 기준, 하나씩 시간차로 놓는다)")

# ------------------------------------------------------------------ char.2 lightning
style("char.2", "ball_lightning", 48, 48, 6, "plasma", "lightning",
      {"radius": 0.22, "arcs": 4, "arc_len": 0.3, "halo": 0.3}, fps=14, loop=True, pivot=[24, 24],
      seed=2.4, rim=1, floor=2, use="B 번개 구체: 구체 반복 + 주변을 지지는 작은 호")
style("char.2", "strike_marker", 64, 32, 4, "rune", "lightning",
      {"mode": 1, "squash": 0.5, "rim_w": 1.3, "fill": 0.22, "pulse": 0.25}, fps=8, loop=True,
      pivot=[32, 16], seed=1.0, floor=2,
      use="C 낙뢰 예고 원(플레이어 쪽: 노랑·흰, 위험색 없음)")
style("char.2", "strike_bolt", 48, 192, 4, "bolt", "lightning",
      {"amp": 7.0, "freq": 0.045, "thick": 7.0, "glow_w": 4.0, "branches": 3, "splash": 10.0, "fade": 0.45, "taper": 0.6},
      fps=18, pivot=[24, 188], seed=7.7, rim=1, ss=3,
      use="C 낙뢰: 하늘에서 내리치는 벼락 기둥(아래 끝 = 착지점). K 벼락 질주 강화의 궤적 낙뢰에도 쓴다")
style("char.2", "strike_blast", 96, 48, 5, "ring", "lightning",
      {"r0": 0.18, "r1": 0.96, "th0": 7.0, "th1": 2.0, "glow": 0.6, "breakup": 0.5, "debris": 14,
       "inner": 0.5, "squash": 0.5, "wob": 2.5, "wob_freq": 14.0, "wob_jag": 1.0, "fade": 0.5},
      fps=16, pivot=[48, 24], seed=4.2, rim=1, floor=2,
      use="C 낙뢰 착지 폭발(바닥 납작 고리). K 궤적 낙뢰에도 쓴다")

style("char.2", "chain_link", 64, 24, 4, "bolt", "lightning",
      {"horiz": 1.0, "tile": 1.0, "amp": 6.0, "freq": 0.11, "thick": 2.6, "glow_w": 2.0, "branches": 0},
      fps=20, loop=True, pivot=[0, 12], seed=5.9, rim=1,
      use="A 연쇄 번개: 가로로 이어 붙이는 번개 선(두 끝이 가운데 높이에서 만난다, 왼쪽 끝 피벗, 늘이지 말고 반복)")

# ------------------------------------------------------------------ char.3 arcane
style("char.3", "rune_trap", 80, 40, 8, "rune", "arcane",
      {"mode": 0, "squash": 0.5, "glyphs": 10, "rim_w": 1.3, "points": 5, "fill": 0.2}, fps=8, loop=True,
      pivot=[40, 20], seed=3.0, floor=2, use="C 룬 문장: 바닥 룬 지뢰 반복")
style("char.3", "rune_burst", 96, 48, 5, "ring", "arcane",
      {"r0": 0.3, "r1": 0.97, "th0": 6.0, "th1": 1.5, "glow": 0.55, "breakup": 0.45, "debris": 12,
       "inner": 0.6, "ticks": 12, "squash": 0.5, "fade": 0.5}, fps=16, pivot=[48, 24], seed=6.1, rim=1, floor=2,
      use="C 룬 문장 폭발(바닥 납작 고리 + 룬 눈금)")
style("char.3", "vortex_base", 192, 160, 8, "swirl", "arcane",
      {"r_in": 0.36, "r_out": 0.97, "squash": 0.62, "arms": 4, "twist": 1.4, "thin": 0.72, "motes": 16},
      loop=True, seed=2.2, ss=2, floor=2, part=True)

# ------------------------------------------------------------------ char.4 ice
style("char.4", "blizzard", 128, 64, 6, "cone", "ice",
      {"mode": 1, "origin_x": 0.04, "spread": 0.4, "len": 0.94, "flakes": 56}, fps=12, loop=True,
      pivot=[5, 32], seed=1.4, floor=2, use="B 눈보라: 넓은 눈송이 분사 반복(왼쪽 끝 = 지팡이 끝)")

style("char.4", "icicle_marker", 48, 24, 4, "rune", "ice",
      {"mode": 1, "squash": 0.5, "rim_w": 1.3, "fill": 0.22, "pulse": 0.25}, fps=8, loop=True,
      pivot=[24, 12], seed=2.0, floor=2, use="C 고드름 낙하 예고 원(바닥, 플레이어 쪽 얼음색)")

# ------------------------------------------------------------------ char.5 dark
style("char.5", "shadow_wave", 128, 96, 5, "cone", "dark",
      {"mode": 2, "origin_x": 0.05, "spread": 0.55, "len": 0.92, "bands": 3, "th0": 8.0, "squash": 0.68}, fps=14,
      pivot=[6, 48], seed=2.9, rim=1, floor=1, use="A 그림자 파동: 부채꼴로 퍼지는 어둠 물결(왼쪽 = 시전 위치)")

# ------------------------------------------------------------------ common
style("common", "ground_slam", 192, 96, 6, "ring", "soil",
      {"r0": 0.12, "r1": 0.97, "th0": 15.0, "th1": 4.0, "glow": 0.5, "breakup": 0.55, "debris": 26,
       "inner": 0.8, "fill": 0.3, "squash": 0.5, "fade": 0.6, "wob": 2.0, "wob_freq": 9.0},
      fps=14, pivot=[96, 48], seed=8.8, rim=1, ss=2,
      use="땅 내리침 충격 고리(흙빛, 바닥 납작). 불가마·그림자 손 강화 공용")
style("common", "ground_crack", 128, 64, 4, "cracks", "soil",
      {"reach": 0.95, "fissures": 9, "cell": 8.0, "web": 0.35, "stain": 0.3, "grow_a": 0.35, "crack_h": 0.08},
      fps=10, pivot=[64, 32], seed=5.0, use="땅 내리침 바닥 금(마지막 프레임을 잠시 두고 사라지게)")
style("common", "debris_burst", 64, 64, 6, "debris", "soil",
      {"base": 0.86, "chunks": 14, "vel": 0.75, "spread_x": 0.42, "csize": 2.0, "dust": 1.0},
      fps=14, pivot=[32, 55], seed=3.7, rim=1, use="솟아오름 흙·파편(아래 = 땅)")
style("common", "dust_skirt", 128, 48, 5, "smoke", "soil",
      {"puffs": 9, "size0": 0.2, "grow": 0.14, "spread": 0.75, "rise": 0.05, "base_y": 0.12,
       "flat_y": 0.32, "hi": 0.85, "lo": 0.3}, fps=12, pivot=[64, 30], seed=6.6, rim=1,
      use="땅 내리침 먼지 치마(고리 아래에 깐다)")

# ------------------------------------------------------------------ composed (compose.py)
composed("char.3", "book_spin", 32, 32, 8, fps=16, loop=True, pivot=[16, 16],
         use="B 책 던지기: 회전하며 날아가는 책")
composed("char.3", "book_open", 64, 48, 6, fps=12, pivot=[32, 30],
         use="K 베껴 쓰기 강화: 책이 촤라락 펼쳐짐(마지막 프레임 = 펼친 책)")
composed("char.3", "book_flip", 64, 48, 4, fps=12, loop=True, pivot=[32, 30],
         use="K 펼친 책 위로 책장이 넘어가는 반복(book_open 뒤에 잇는다)")
composed("char.3", "page_vortex", 192, 160, 8, fps=12, loop=True, pivot=[96, 80],
         use="K 책장 소용돌이 반복(큰 시트, 시전자 둘레)")
composed("char.4", "ice_spear", 48, 16, 2, fps=8, loop=True, pivot=[30, 8],
         use="A 얼음 창: 길쭉한 창 탄(오른쪽을 향함)")
composed("char.4", "icicle", 16, 48, 2, fps=10, loop=True, pivot=[8, 42],
         use="C 고드름 낙하: 떨어지는 고드름(아래 끝이 착지점)")
composed("char.4", "ice_spike", 48, 64, 8, fps=12, pivot=[24, 58],
         clips={"rise": [0, 2], "hold": [2, 4], "break": [5, 7]},
         use="K 얼음 병풍 강화: 땅에서 줄지어 솟는 얼음 가시(솟음 0-2, 유지 2-4, 깨짐 5-7)")
composed("char.5", "giant_hand", 128, 128, 8, fps=12, pivot=[64, 116],
         clips={"rise": [0, 1], "grip": [2, 4], "slam": [5, 6], "fade": [7, 7]},
         use="K 그림자 손 강화: 땅에서 솟는 거대한 주먹(솟음·움켜쥠·짓누름·흩어짐). 펼친 손 프레임은 GPT 필요 목록")
