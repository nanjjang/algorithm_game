"""게임을 처음 켤 때 나오는 인트로(프롤로그) 영상.

    python3 solo/Tools/cinematic/intro.py           # StreamingAssets/Cinematic/intro.mp4
    python3 solo/Tools/cinematic/intro.py --calm    # intro_calm.mp4(번쩍임·흔들림 줄임)
    python3 solo/Tools/cinematic/intro.py --stills x.jpg   # 확인용 장면 모음
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cine import *  # noqa
from assets import *  # noqa


def scenes():
    S = []

    # 0. 탑
    S.append(Scene(bg=BG_TOWER_CAMP, cam=((0.56, 0.36, 1.75), (0.55, 0.42, 1.3)), music="mystery", fade_in=1.6,
                   clip=clip("tower_camp"),
                   fx=[("particles", "fireflies", 26)],
                   sfx=[(0.0, SFX + "tod_insects.wav", 0.5)],
                   lines=[N("한뎃벌 끝자락에는, 낡은 탑이 하나 서 있다."),
                          N("그 탑에는 육십 년 동안 제자를 길러 낸, 대마법사가 살고 있었다.")]))

    # 1. 느루
    ref = [None]
    s = Scene(bg=BG_STUDY, cam=((0.5, 0.45, 1.25), (0.5, 0.5, 1.12)), dark=0.25, blur=1.5, music="whimsy",
              sprites=[Spr(neuru_big(ref), [(0, dict(x=W / 2, y=H / 2 - 30, s=4, a=0)),
                                           (0.6, dict(x=W / 2, y=H / 2 - 40, s=4, a=1)),
                                           ("end", dict(x=W / 2, y=H / 2 - 50, s=4, a=1))],
                           smooth=False, glow=(255, 220, 160, 0.35))],
              lines=[N("스스로 지은 직함은, 엄청엄청 위대한 대마법사."),
                     D("neuru", "느루다. 삼백 살이지.", box=False),
                     N("제자들 생각엔, 여든쯤.", gap=0.6),
                     D("neuru", "탑 안에서 배운 놈들은 이론만 늘더군.", face="one", box=False)])
    ref[0] = s
    S.append(s)

    # 2. 내던짐
    S.append(Scene(bg=BG_TOWER_WALK, cam=((0.62, 0.4, 1.35), (0.45, 0.55, 1.05)), music="whimsy",
                   fx=[("particles", "motes", 20)],
                   sfx=[("L0e-0.2", SFX + "enh_slam_1.wav", 0.7)],
                   lines=[N("은퇴하던 날, 느루는 탑 문을 잠그고"),
                          N("남아 있던 제자 다섯을, 바깥 한뎃벌로 내던졌다.")]))

    # 3. 다섯 제자 소개
    xs = [W * 0.13, W * 0.315, W * 0.5, W * 0.685, W * 0.87]
    sprs, lines = [], []
    names = {"dalgu": "달구", "chorong": "초롱", "galpi": "갈피", "seonge": "성에", "geumeum": "그믐"}
    say = ["화염의 달구.", "번개의 초롱.", "비전의 갈피.", "냉기의 성에.", "그리고, 암흑의 그믐."]
    sfx = []
    for i, cid in enumerate(CHARS):
        im = char(cid)
        sc_ = 430 / im.height
        el, col = ELEM[cid]
        sprs.append(put(im, xs[i], H * 0.47, sc_, f"L{i}-0.1", fade=0.45, enter=(0, 40), drift=(0, -6, 0.0),
                        glow=col + (0.55,), shadow=True, bob=4, bob_hz=0.4 + i * 0.03))
        tag = text_img(names[cid], 54, big=True, color=col, ow=5)
        sprs.append(put(tag, xs[i], H * 0.47 + 262, 1, f"L{i}+0.15", fade=0.35, enter=(0, 16)))
        lines.append(N(say[i], gap=0.3))
        sfx.append((f"L{i}-0.05", SFX + f"enh_card_mark_{i * 2 + 1}.wav", 0.6))
    S.append(Scene(bg=BG_DAWN, cam=((0.5, 0.5, 1.15), (0.5, 0.5, 1.05)), dark=0.55, blur=4, music="whimsy",
                   sprites=sprs, lines=lines, sfx=sfx, tail=1.2, fx=[("particles", "motes", 18)]))

    # 4. 조건
    S.append(Scene(bg=BG_CAMP, cam=((0.75, 0.35, 1.5), (0.55, 0.5, 1.1)), music="calm",
                   fx=[("particles", "fireflies", 14)],
                   lines=[D("neuru", "마법은 책상에서 안 큰다. 한데서 큰다."),
                          D("neuru", "가장 잘 큰 놈이, 내 후계자다.", face="speak")]))

    # 5. 한뎃벌
    mons = [("enemy.slime_moss", 0.2, 240), ("enemy.wolf_bramble", 0.42, 300),
            ("enemy.golem_menhir.corrected", 0.64, 340), ("enemy.shaman_cap", 0.85, 320)]
    sprs = []
    for k, (mid, fx_, h_) in enumerate(mons):
        im = mon(mid)
        sprs.append(put(im, W * fx_, H * 0.7 - h_ / 2 + 40, h_ / im.height, f"L1+{k * 0.7}", fade=0.6,
                        enter=(60, 0), drift=(-20, 0, 0), shadow=True, z=1))
    S.append(Scene(bg=BG_DAWN, cam=((0.3, 0.45, 1.25), (0.7, 0.5, 1.1)), music="mystery", dark=0.15,
                   fx=[("particles", "motes", 30), ("darken", "L1-0.5", "L1+0.5", 0.35)],
                   sfx=[("L1+0.1", SFX + "cry_slime.wav", 0.5), ("L1+0.8", SFX + "cry_wolf.wav", 0.5),
                        ("L1+1.5", SFX + "cry_golem.wav", 0.5), ("L1+2.2", SFX + "cry_shaman.wav", 0.5)],
                   sprites=sprs,
                   lines=[N("탑 뒤로 펼쳐진 야생의 벌판, 한뎃벌."),
                          N("느루가 육십 년 동안 내다 버린 실패작들이, 야생 마력을 머금고 깨어난 곳.")], tail=1.4))

    # 6. 첫 밤의 거울
    ph = mon("boss.mirror_phantom")
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.35, 0.45, 1.3), (0.4, 0.5, 1.15)), music="tension", dark=0.35,
                   tint=(150, 130, 220, 0.35),
                   fx=[("particles", "silver", 30), ("flash", "L1-0.3", 0.6, (220, 225, 255)), ("shake", "L1-0.3", 0.7, 10)],
                   sfx=[(0.3, SFX + "phantom_1.wav", 0.6), ("L1-0.35", SFX + "boss_intro_1.wav", 0.8)],
                   sprites=[Spr(ph, [("L1-0.3", dict(x=W * 0.33, y=H * 0.5, s=0.62, a=0)),
                                     ("L1+0.2", dict(x=W * 0.33, y=H * 0.48, s=0.66, a=1)),
                                     ("end", dict(x=W * 0.33, y=H * 0.46, s=0.70, a=1))],
                                glow=(200, 210, 255, 0.7), bob=8, bob_hz=0.3)],
                   lines=[N("그리고, 첫 번째 밤."),
                          N("그들을 기다리는 것은, 결코 이길 수 없는 거울.")], tail=1.2))

    # 7. 져 봐라
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.5, 0.5, 1.05), (0.5, 0.5, 1.12)), music="tension", dark=0.6,
                   fx=[("flash", 0.4, 0.5), ("shake", 0.4, 0.5, 14), ("particles", "silver", 24)],
                   sfx=[(0.35, SFX + "crystal_break_1.wav", 0.8)],
                   sprites=[put(mirror_cracked(), W / 2, H / 2 - 40, 0.75, 0.35, fade=0.2, enter=(0, 0),
                                drift=(0, 0, 0.05), glow=(200, 210, 255, 0.5))],
                   lines=[D("neuru", "먼저, 크게 한 번 져 봐라.")], lead=1.2, tail=1.0))

    # 8. 제목
    logo = load(LOGO)
    S.append(Scene(bg=BG_TOWER_CAMP, cam=((0.5, 0.55, 1.2), (0.5, 0.5, 1.02)), music="epic", dark=0.45,
                   fx=[("particles", "fireflies", 30), ("particles", "embers", 16), ("flash", "L0e+0.4", 0.8, (255, 240, 210))],
                   sfx=[("L0e+0.4", SFX + "enh_plus13_bell.wav", 0.7)],
                   sprites=[put(logo, W / 2, H * 0.42, 1.25, "L0e+0.4", fade=1.0, enter=(0, 0), drift=(0, 0, 0.04),
                                glow=(255, 230, 180, 0.4))],
                   lines=[N("엄청엄청 위대한 마법사가 되기 위한 여정이, 지금 시작된다.")],
                   tail=4.5, fade_out=1.6))
    return S


if __name__ == "__main__":
    main(scenes, "intro")
