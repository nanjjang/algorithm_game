"""스승의 허상(첫 보스) 영상 두 편. 기준은 docs/world-characters.md 1.3절 "첫 밤의 거울", 1.6절 느루의 두 한마디.

    python3 solo/Tools/cinematic/mirror.py first     # mirror_first.mp4  웨이브 10에서 처음 진 뒤, 결과 화면 전에
    python3 solo/Tools/cinematic/mirror.py break     # mirror_break.mp4  웨이브 20에서 처음 이긴 뒤, 복수 보상 전에
    python3 solo/Tools/cinematic/mirror.py all [--calm] [--stills x.jpg]
느루의 두 한마디는 문서에서 자리표시 문구다. 정해지면 이 파일의 줄만 바꿔 다시 굽는다.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cine import *  # noqa
from assets import *  # noqa

SILVER = (200, 210, 255)
EL_COLS = [ELEM[c][1] for c in CHARS]


def phantom_spr(ph, keys, **kw):
    return Spr(ph, keys, glow=SILVER + (0.7,), bob=8, bob_hz=0.3, baked=True, **kw)


def neuru_side(t0=0.2, x=W * 0.72):
    im = neuru_chair()
    return put(im, x, H * 0.52, 640 / im.height, t0, fade=0.9, enter=(40, 0), drift=(-12, 0, 0.015),
               shadow=True, glow=(255, 220, 160, 0.25), z=2, baked=True)


def first():
    S = []
    ph = mon("boss.mirror_phantom", dark=True)
    # 0. 첫 밤의 끝
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.35, 0.45, 1.35), (0.4, 0.5, 1.15)), music="tension", dark=0.4,
                   tint=(150, 130, 220, 0.35), fade_in=1.2,
                   fx=[("particles", "silver", 30), ("flash", "L1-0.3", 0.6, SILVER), ("shake", "L1-0.3", 0.6, 8)],
                   sfx=[(0.2, SFX + "phantom_1.wav", 0.6), ("L1-0.35", SFX + "boss_intro_1.wav", 0.8)],
                   sprites=[phantom_spr(ph, [("L1-0.3", dict(x=W * 0.36, y=H * 0.5, s=0.6, a=0)),
                                             ("L1+0.2", dict(x=W * 0.36, y=H * 0.48, s=0.64, a=1)),
                                             ("end", dict(x=W * 0.36, y=H * 0.46, s=0.68, a=1))])],
                   lines=[N("첫 번째 밤의 끝."),
                          N("느루가 보낸 첫 시험, 스승의 허상.")]))
    # 1. 되돌아오는 주문, 메워지는 금
    flashes = [("flash", f"L0+{0.15 + k * 0.32:.2f}", 0.28, c) for k, c in enumerate(EL_COLS)]
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.5, 0.5, 1.05), (0.5, 0.5, 1.14)), music="tension", dark=0.6,
                   clip=clip("phantom", 0.2),
                   fx=[("particles", "silver", 26), ("shake", "L0+0.15", 1.6, 7), *flashes],
                   sfx=[("L0+0.1", SFX + "phantom_2.wav", 0.7), ("L1-0.1", SFX + "crystal_break_1.wav", 0.7),
                        ("L1+1.2", SFX + "crystal_note.wav", 0.6)],
                   sprites=[phantom_spr(ph, [(0, dict(x=W / 2, y=H * 0.5, s=0.82, a=1)), ("end", dict(x=W / 2, y=H * 0.48, s=0.9, a=1))]),
                            put(mirror_cracked(), W / 2 + 10, H * 0.36, 0.42, "L1-0.1", t1="L1+1.1", fade=0.15, out=0.9,
                                enter=(0, 0), glow=SILVER + (0.6,), z=2)],
                   lines=[N("쏘아 낸 주문은, 거울 속에서 그대로 되돌아오고"),
                          N("금이 가도, 거울은 곧 다시 메워진다.")]))
    # 2. 내려다보다 돌아간다
    S.append(Scene(bg=BG_NIGHTPATH, cam=((0.6, 0.5, 1.3), (0.5, 0.5, 1.1)), music="mystery", dark=0.5,
                   fx=[("particles", "silver", 14), ("particles", "fireflies", 8)],
                   sfx=[(0.2, SFX + "player_down.wav", 0.6), ("L0e", SFX + "phantom_3.wav", 0.5)],
                   sprites=[put(ph, W * 0.32, H * 0.46, 0.6, 0.3, t1="L0e+0.2", out=1.4, enter=(0, 0), drift=(0, -30, -0.04),
                                glow=SILVER + (0.4,))],
                   lines=[N("거울은 쓰러진 제자를 한참 내려다보다, 돌아갔다."),
                          N("다음 날 밤 끝에서, 다시 기다리겠다는 듯이.")]))
    # 3. 느루의 한마디
    S.append(Scene(bg=BG_CAMP, cam=((0.62, 0.45, 1.4), (0.6, 0.5, 1.2)), music="calm", dark=0.35, blur=2,
                   fx=[("particles", "fireflies", 12)],
                   sprites=[neuru_side()], clip=clip("neuru_camp"),
                   lines=[D("neuru", "거울도 못 이기면서 위대하긴.", face="one")], lead=1.0, tail=1.2))
    # 4. 복수 목표
    a = text_img("복수", 40, color=(200, 180, 140), ow=3)
    b = text_img("웨이브 20의 스승의 허상을 쓰러뜨려라", 64, big=True, ow=5)
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.5, 0.5, 1.12), (0.5, 0.5, 1.04)), music="tension", dark=0.7, blur=3,
                   lead=0, tail=0, min_dur=4.2, fade_in=0.6, fade_out=1.2,
                   fx=[("particles", "silver", 20)], sfx=[(0.3, SFX + "revenge_2.wav", 0.6)],
                   sprites=[put(a, W / 2, H / 2 - 80, 1, 0.3, enter=(0, -16)),
                            put(ornament(min(1300, b.width + 200)), W / 2, H / 2 + 70, 1, 0.5, enter=(0, 0)),
                            put(b, W / 2, H / 2 + 5, 1, 0.45, enter=(0, 20))]))
    return S


def brk():
    S = []
    ph = mon("boss.mirror_phantom", dark=True)
    # 0. 다시 선 거울
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.4, 0.45, 1.3), (0.45, 0.5, 1.12)), music="tension", dark=0.4,
                   tint=(150, 130, 220, 0.3), fade_in=1.0,
                   fx=[("particles", "silver", 30)],
                   sfx=[(0.3, SFX + "boss_intro_1.wav", 0.7)],
                   clip=clip("phantom"),
                   sprites=[phantom_spr(ph, [(0, dict(x=W / 2, y=H * 0.5, s=0.82, a=0)), (1.0, dict(x=W / 2, y=H * 0.5, s=0.82, a=1)),
                                             ("end", dict(x=W / 2, y=H * 0.48, s=0.86, a=1))])],
                   lines=[N("이튿날 밤의 끝. 거울이, 다시 섰다."),
                          N("이번에는, 물러서지 않았다.")]))
    # 1. 쏟아붓기 → 금 → 깨짐
    flashes = [("flash", f"{0.4 + k * 0.28:.2f}", 0.25, c) for k, c in enumerate(EL_COLS * 2)]
    hit = [(0.4 + k * 0.28, SFX + f"fire_block_{b}_1.wav", 0.5) for k, b in
           enumerate(["fireball", "chain_lightning", "magic_bolt", "ice_spike", "shadow_orb"] * 2)]
    t_crack, t_break = 1.5, 3.4
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.5, 0.5, 1.05), (0.5, 0.5, 1.2)), music="epic", dark=0.55,
                   lead=0, tail=0, min_dur=6.2,
                   fx=[("particles", "silver", 30), ("shake", 0.4, 2.8, 6), *flashes,
                       ("flash", t_break, 0.9, (235, 240, 255)), ("shake", t_break, 0.8, 18)],
                   sfx=hit + [(t_crack, SFX + "crystal_break_1.wav", 0.8), (t_break, SFX + "crystal_break_2.wav", 1.0),
                              (t_break + 0.3, SFX + "boss_kill.wav", 0.6)],
                   sprites=[phantom_spr(ph, [(0, dict(x=W / 2, y=H * 0.5, s=0.82, a=1)), (t_break, dict(x=W / 2, y=H * 0.49, s=0.86, a=1)),
                                             (t_break + 0.15, dict(x=W / 2, y=H * 0.49, s=0.86, a=0))]),
                            put(mirror_cracked(), W / 2 + 10, H * 0.36, 0.42, t_crack, t1=t_break, fade=0.1, out=0.05,
                                enter=(0, 0), glow=SILVER + (0.6,), z=2),
                            put(mirror_shattered(), W / 2, H * 0.45, 0.7, t_break, fade=0.08, enter=(0, 0),
                                drift=(0, 20, 0.25), glow=SILVER + (0.7,), z=3)]))
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.5, 0.5, 1.15), (0.5, 0.5, 1.05)), music="epic", dark=0.6, blur=2,
                   fx=[("particles", "silver", 50)],
                   lines=[N("금이, 더는 메워지지 않는다.")], tail=1.0))
    # 2. 느루의 한마디
    S.append(Scene(bg=BG_CAMP, cam=((0.62, 0.45, 1.4), (0.6, 0.5, 1.2)), music="whimsy", dark=0.3, blur=2,
                   fx=[("particles", "fireflies", 12)],
                   sprites=[neuru_side()], clip=clip("neuru_camp"),
                   lines=[D("neuru", "…거울 값은 네가 물어라.", face="one")], lead=1.0, tail=1.2))
    # 3. 칭호
    a = text_img("칭호", 40, color=(200, 180, 140), ow=3)
    b = text_img("거울을 깬", 110, big=True, color=(244, 241, 230), ow=6)
    c = text_img("금 간 거울 조각을 얻었다", 38, color=(174, 182, 194), ow=3)
    shard = region(f"{MON}/effects/mirror_shards.png", (887, 0, 1774, 887))
    S.append(Scene(bg=BG_HALL, cam=((0.5, 0.5, 1.15), (0.5, 0.5, 1.04)), music="epic", dark=0.6, blur=3,
                   lead=0, tail=0, min_dur=5.0, fade_in=0.6, fade_out=1.4,
                   fx=[("particles", "silver", 30), ("flash", 0.5, 0.7, (255, 245, 225))],
                   sfx=[(0.5, SFX + "enh_title_band.wav", 0.7), (0.5, SFX + "revenge_1.wav", 0.5)],
                   sprites=[put(shard, W / 2, H * 0.3, 0.28, 0.3, fade=0.8, enter=(0, 0), drift=(0, -6, 0.02),
                                glow=SILVER + (0.6,), bob=5),
                            put(a, W / 2, H * 0.5, 1, 0.5, enter=(0, -12)),
                            put(b, W / 2, H * 0.6, 1, 0.6, enter=(0, 16), glow=(255, 240, 210, 0.3)),
                            put(c, W / 2, H * 0.72, 1, 1.1, enter=(0, 10))]))
    return S


VIDEOS = {"first": (first, "mirror_first"), "break": (brk, "mirror_break")}

if __name__ == "__main__":
    args = sys.argv[1:]
    if not args or args[0] not in ("first", "break", "all"):
        print(__doc__); sys.exit(1)
    sys.argv = [sys.argv[0]] + args[1:]
    for k in (["first", "break"] if args[0] == "all" else [args[0]]):
        fn, name = VIDEOS[k]
        main(fn, name)
