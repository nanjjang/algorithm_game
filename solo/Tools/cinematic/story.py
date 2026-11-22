"""스토리 한눈에 보기 영상(세계관 요약). 내용의 기준은 docs/world-characters.md, docs/monsters.md 3.9절.

    python3 solo/Tools/cinematic/story.py           # StreamingAssets/Cinematic/story.mp4
    python3 solo/Tools/cinematic/story.py --calm    # story_calm.mp4
    python3 solo/Tools/cinematic/story.py --stills x.jpg
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cine import *  # noqa
from assets import *  # noqa


def big_char(cid, x=W * 0.7, y=H * 0.5, h=720, t0=0.2, **kw):
    im = char(cid)
    el, col = ELEM[cid]
    return put(im, x, y, h / im.height, t0, fade=0.8, enter=(60, 0), drift=(-25, -8, 0.02),
               glow=col + (0.45,), shadow=True, bob=5, z=2, baked=True, **kw)


def card(cid, name, extra, x=W * 0.27, y=H * 0.3, t0=0.5):
    return put(name_card(cid, name, extra), x, y, 1, t0, fade=0.7, enter=(-40, 0), drift=(10, 0, 0), z=3)


def character_scene(cid, name, extra, bg, lines, cam=None, dark=0.35, fx=(), tint=None, music="whimsy", extra_sprites=()):
    return Scene(bg=bg, cam=cam or ((0.45, 0.5, 1.2), (0.55, 0.5, 1.08)), dark=dark, blur=2.5, tint=tint,
                 music=music, fx=list(fx), lines=lines, clip=clip(f"disciple_{CHARS.index(cid) + 1}"),
                 sfx=[(0.15, SFX + "sty_page_flutter.wav", 0.4)],
                 sprites=[big_char(cid), card(cid, name, extra), *extra_sprites])


def scenes():
    S = []
    logo = load(LOGO)

    # ── 오프닝 ──
    S.append(Scene(bg=BG_HALL, cam=((0.75, 0.45, 1.6), (0.7, 0.5, 1.3)), dark=0.45, music="mystery", fade_in=1.2,
                   fx=[("particles", "motes", 20)],
                   lines=[D("neuru", "후계자는 한 명이라고 했지,"),
                          D("neuru", "한 번만 뽑는다고는 안 했다.")], tail=1.0))
    sub = text_img("스토리 한눈에 보기", 44, color=(220, 200, 160), ow=4)
    S.append(Scene(bg=BG_TOWER_CAMP, cam=((0.5, 0.5, 1.15), (0.5, 0.5, 1.03)), dark=0.5, music="epic", min_dur=5.0,
                   lead=0, tail=0, fade_in=0.8, fade_out=0.9,
                   fx=[("particles", "fireflies", 24), ("flash", 0.3, 0.7, (255, 240, 210))],
                   sfx=[(0.3, SFX + "enh_plus13_bell.wav", 0.7)],
                   sprites=[put(logo, W / 2, H * 0.42, 1.2, 0.3, fade=1.0, enter=(0, 0), drift=(0, 0, 0.03), glow=(255, 230, 180, 0.4)),
                            put(sub, W / 2, H * 0.68, 1, 1.3, fade=0.8, enter=(0, 14))]))

    # ── 1장 대마법사 느루 ──
    S.append(chapter("제1장", "대마법사 느루", BG_STUDY))
    ref = [None]
    s = Scene(bg=BG_STUDY, cam=((0.5, 0.45, 1.25), (0.5, 0.5, 1.1)), dark=0.3, blur=2, music="whimsy",
              fx=[("particles", "motes", 12)],
              sprites=[Spr(neuru_big(ref), [(0, dict(x=W / 2, y=H / 2 - 40, s=4, a=0)),
                                           (0.8, dict(x=W / 2, y=H / 2 - 50, s=4, a=1)),
                                           ("end", dict(x=W / 2, y=H / 2 - 60, s=4, a=1))],
                           smooth=False, glow=(255, 220, 160, 0.35))],
              lines=[N("이야기는 한 노인에게서 시작한다."),
                     N("한뎃벌 끝자락의 탑에 사는 대마법사, 느루."),
                     N("공식 직함은 엄청엄청 위대한 대마법사. 물론, 본인이 지었다."),
                     N("본인 주장으로는 삼백 살. 제자들 추정으로는 여든 살."),
                     D("neuru", "삼백 살이다.", box=False, face="speak")])
    ref[0] = s
    S.append(s)
    S.append(Scene(bg=BG_CAMP, cam=((0.72, 0.4, 1.6), (0.68, 0.45, 1.25)), music="whimsy",
                   fx=[("particles", "fireflies", 12)],
                   lines=[N("흔들의자에 앉은 작은 노인으로, 수염이 무릎 담요를 겸한다."),
                          N("그가 쓰던 지팡이는, 은퇴한 지금 빨랫줄 받침대가 되었다."),
                          N("귀찮은 걸 싫어하고, 가르치는 대신 내던지며, 틀린 적은 없다고 우기는 사람.")]))
    S.append(Scene(bg=BG_TOWER_CAMP, cam=((0.56, 0.36, 1.7), (0.55, 0.42, 1.25)), music="calm",
                   fx=[("particles", "fireflies", 22)],
                   lines=[N("느루는 육십 년 동안, 이 탑에서 제자를 가르쳤다."),
                          N("하지만 탑 안에서 배운 제자들은, 이상하게도 이론만 늘었다."),
                          D("neuru", "마법은 책상에서 안 큰다. 한데서 큰다.")]))
    S.append(Scene(bg=BG_TOWER_WALK, cam=((0.62, 0.4, 1.35), (0.45, 0.55, 1.05)), music="whimsy",
                   fx=[("particles", "motes", 20)],
                   sfx=[("L0e-0.3", SFX + "enh_slam_1.wav", 0.7)],
                   lines=[N("은퇴하던 날, 느루는 탑 문을 잠그고"),
                          N("남아 있던 제자 다섯을, 야생의 벌판 한뎃벌로 내보냈다."),
                          N("조건은 단 하나."),
                          D("neuru", "가장 잘 큰 놈이, 후계자다.")]))

    # ── 2장 다섯 제자 ──
    S.append(chapter("제2장", "다섯 제자", BG_TOWER_WALK))
    S.append(Scene(bg=BG_TOWER_WALK, cam=((0.4, 0.62, 1.5), (0.45, 0.6, 1.25)), music="whimsy",
                   lines=[N("그렇게 쫓겨난 다섯 제자. 다들 아직 수습이지만,"),
                          N("하나같이, 자기가 엄청엄청 위대한 마법사가 될 거라 믿는다.")]))
    S.append(character_scene("dalgu", "달구", "15세 · 대장간 집 둘째 딸", BG_FORGE, fx=[("particles", "embers", 26)], lines=[
        N("첫째로, 화염의 달구."),
        N("불 조절을 못 해 집 지붕을 두 번 태우고, 느루에게 보내졌다."),
        N("주전자 투구는, 머리라도 지키라며 어머니가 씌워 준 것."),
        N("진 건 삼 초 만에 잊고, 이긴 건 평생 말한다."),
        D("dalgu", "아직 안 끓었어. 끓으면 진짜 무서워!")]))
    S.append(character_scene("chorong", "초롱", "17세 · 등대지기의 아들", BG_LOGIN, cam=((0.6, 0.45, 1.25), (0.65, 0.5, 1.1)),
                             fx=[("particles", "fireflies", 24)], lines=[
        N("번개의 초롱."),
        N("밤새 등대 불을 지키던 버릇 탓에, 낮에는 늘 졸리다."),
        N("벼락을 세 번 맞고도 멀쩡해서, 느루가 데려왔다."),
        N("말은 제일 느린데, 몸은 제일 빠르다."),
        D("chorong", "오 분만… 아니, 벌써 끝났어?", sub="5분만… 아니, 벌써 끝났어?")]))
    S.append(character_scene("galpi", "갈피", "13세 · 막내", BG_HALL, fx=[("particles", "motes", 16)], lines=[
        N("비전의 갈피. 다섯 중 막내다."),
        N("느루의 탑 서고에 몰래 살던 아이로, 삼 년 동안 아무도 몰랐다."),
        N("서고의 책은 다 읽었지만, 주문은 한 번도 써 본 적이 없다."),
        N("틀리면, 판본 차이라고 한다."),
        D("galpi", "이론상, 저는 이미 위대합니다.")]))
    S.append(character_scene("seonge", "성에", "19세 · 첫째 제자", BG_DAWN, tint=(140, 180, 255, 0.55), dark=0.4,
                             fx=[("particles", "snow", 60)], music="calm", lines=[
        N("냉기의 성에. 느루의 첫 제자이자, 탑의 수석."),
        N("본인은 후계자가 이미 정해졌다고 믿었다."),
        N("한뎃벌에 던져진 것이, 인생 첫 번째 계획 밖의 일."),
        N("느루가 제자들을 내던질 때, 가장 망설였던 제자다. 무려, 이 초.", sub="느루가 제자들을 내던질 때, 가장 망설였던 제자다. 무려, 2초."),
        D("seonge", "서두를 것 없어요. 어차피 제가 이기니까.")]))
    S.append(Scene(bg=BG_DAWN, cam=((0.5, 0.5, 1.0), (0.74, 0.5, 1.6)), music="mystery", dark=0.55, tint=(120, 90, 200, 0.5),
                   fx=[("particles", "motes", 24)], clip=clip("disciple_5"),
                   sprites=[big_char("geumeum", t0=0.8),
                            card("geumeum", "그믐", "나이 모름 · 어느 그믐밤에 온 아이", x=W * 0.27, y=H * 0.3, t0=1.0)],
                   lines=[N("그리고, 암흑의 그믐."),
                          N("어느 그믐밤, 탑 문 앞에 서 있던 아이."),
                          N("어디서 왔는지도, 후드 속 얼굴도, 아무도 모른다."),
                          N("겉보기와 달리 다섯 중 가장 소심하고, 작은 동물을 좋아한다."),
                          D("geumeum", "……", hold=2.0),
                          D("galpi", "나는 이미 위대하다, 라는 뜻입니다. 아마.")],
                   sfx=[("L4", SFX + "sty_eye_blink.wav", 0.6)]))

    # ── 3장 야생 한뎃벌 ──
    S.append(chapter("제3장", "야생 한뎃벌", BG_DAWN))
    S.append(Scene(bg=BG_DAWN, cam=((0.3, 0.45, 1.3), (0.7, 0.5, 1.1)), music="mystery",
                   fx=[("particles", "motes", 30)],
                   lines=[N("탑 뒤로 펼쳐진 벌판, 한뎃벌. 한데, 그러니까 바깥의 벌판이라는 뜻이다."),
                          N("느루는 육십 년 동안, 실패한 실험을 전부 이 벌판에 내다 버렸다."),
                          N("그 야생 마력이, 벌판의 생물과 물건에 스며들었다.")]))
    mons = [("enemy.slime_moss", "이끼 슬라임", 260), ("enemy.wolf_bramble", "덤불 늑대", 300),
            ("enemy.golem_menhir.corrected", "선돌지기", 380), ("enemy.shaman_cap", "갓버섯 술사", 360)]
    xs = [W * 0.16, W * 0.39, W * 0.62, W * 0.85]
    sprs, sfx = [], []
    cries = ["cry_slime", "cry_wolf", "cry_golem", "cry_shaman"]
    for k, (mid, nm, h_) in enumerate(mons):
        im = mon(mid)
        sprs.append(put(im, xs[k], H * 0.5 - h_ / 2 + 120, h_ / im.height, f"L{k}-0.1", fade=0.5, enter=(0, 40),
                        shadow=True, bob=3, z=1))
        sprs.append(put(text_img(nm, 40, big=True, color=(200, 230, 170), ow=4), xs[k], H * 0.5 + 180, 1, f"L{k}+0.2",
                        fade=0.4, enter=(0, 10)))
        sfx.append((f"L{k}-0.05", SFX + cries[k] + ".wav", 0.5))
    S.append(Scene(bg=BG_LOGIN, cam=((0.5, 0.5, 1.1), (0.5, 0.5, 1.02)), dark=0.6, blur=3, music="mystery",
                   sprites=sprs, sfx=sfx, fx=[("particles", "motes", 16)],
                   lines=[N("엎지른 물약이 이끼에 스며든, 이끼 슬라임."),
                          N("느루의 등잔에서 도망친 그림자가 깃든, 덤불 늑대."),
                          N("맞는 데 질려서, 이제는 때리는 선돌지기."),
                          N("창문으로 수업을 훔쳐보다, 세 갈래 탄 하나만 배운 갓버섯 술사.")], tail=1.2))
    bosses = ["boss.kiln_boar", "boss.spore_queen", "boss.storm_owl", "boss.frost_goat", "boss.night_serpent"]
    sprs = []
    for k, b in enumerate(bosses):
        im = mon(b)
        sprs.append(put(im, W * (0.14 + k * 0.18), H * 0.48, 330 / max(im.height, im.width * 0.8), 0.6 + k * 0.45,
                        fade=0.6, enter=(0, 30), shadow=True, bob=4, bob_hz=0.3 + k * 0.04))
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.5, 0.5, 1.1), (0.5, 0.5, 1.0)), dark=0.65, blur=3, music="tension",
                   sprites=sprs, fx=[("particles", "embers", 12)],
                   sfx=[(0.6, SFX + "boss_intro_2.wav", 0.6)],
                   lines=[N("그리고 밤이 깊으면, 더 큰 것들이 깨어난다."),
                          N("느루가 버린 대장간 집게, 화분, 옷걸이 같은 실패작들이 박혀 자라난 것들이다.")], lead=2.4))
    S.append(Scene(bg=BG_WORKBENCH, cam=((0.5, 0.45, 1.3), (0.5, 0.5, 1.08)), dark=0.25, music="calm",
                   fx=[("particles", "motes", 34)],
                   lines=[N("벌판 곳곳에 떨어진 주문 조각은, 느루가 쓰다 버린 주문들."),
                          N("제자들의 지팡이는, 한뎃벌의 야생 마력을 먹고 자란다."),
                          N("그래서 이 세계에서 지팡이는, 만드는 것이 아니라 키우는 것이다.")]))

    # ── 4장 첫 밤의 거울 ──
    S.append(chapter("제4장", "첫 밤의 거울", BG_ECLIPSE, music="tension",
                     sfx=[(0.2, SFX + "phantom_1.wav", 0.6)]))
    ph = mon("boss.mirror_phantom")
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.35, 0.45, 1.3), (0.4, 0.5, 1.15)), music="tension", dark=0.35,
                   tint=(150, 130, 220, 0.35),
                   fx=[("particles", "silver", 30), ("flash", "L1-0.3", 0.6, (220, 225, 255)), ("shake", "L1-0.3", 0.6, 8)],
                   sfx=[("L1-0.35", SFX + "boss_intro_1.wav", 0.8)],
                   sprites=[Spr(ph, [("L1-0.3", dict(x=W * 0.33, y=H * 0.5, s=0.62, a=0)),
                                     ("L1+0.2", dict(x=W * 0.33, y=H * 0.48, s=0.66, a=1)),
                                     ("end", dict(x=W * 0.33, y=H * 0.46, s=0.70, a=1))],
                                glow=(200, 210, 255, 0.7), bob=8, bob_hz=0.3)],
                   lines=[N("한뎃벌의 첫 밤이 끝날 무렵, 느루는 제자에게 첫 시험을 보낸다."),
                          N("느루가 자세 교정용으로 쓰던 전신 거울, 스승의 허상."),
                          N("앞에 선 사람의 주문을, 그대로 따라 한다.")]))
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.5, 0.5, 1.05), (0.5, 0.5, 1.12)), music="tension", dark=0.6,
                   fx=[("flash", "L0+0.2", 0.5), ("shake", "L0+0.2", 0.5, 12), ("particles", "silver", 24)],
                   sfx=[("L0+0.15", SFX + "crystal_break_1.wav", 0.7), ("L1e", SFX + "player_down.wav", 0.6)],
                   sprites=[put(mirror_cracked(), W / 2, H / 2 - 40, 0.75, "L0+0.15", fade=0.2, enter=(0, 0),
                                drift=(0, 0, 0.05), glow=(200, 210, 255, 0.5))],
                   lines=[N("금이 가도 메워지고, 무엇을 해도 쓰러지지 않는다."),
                          N("제자는, 반드시 진다."),
                          D("neuru", "먼저, 크게 한 번 져 봐라.")]))
    S.append(Scene(bg=BG_NIGHTPATH, cam=((0.6, 0.5, 1.3), (0.5, 0.5, 1.1)), music="mystery", dark=0.45,
                   fx=[("particles", "silver", 14), ("particles", "fireflies", 10)],
                   sprites=[put(ph, W * 0.3, H * 0.47, 0.55, 0.3, t1="L0e", enter=(0, 0), glow=(200, 210, 255, 0.4))],
                   lines=[N("하지만 거울은 제자를 해치지 않는다. 쓰러진 제자를 내려다보다, 돌아갈 뿐."),
                          N("그리고 다음 날 밤 끝에서, 다시 기다린다.")]))
    S.append(Scene(bg=BG_ECLIPSE, cam=((0.5, 0.5, 1.15), (0.5, 0.5, 1.02)), music="epic", dark=0.55,
                   fx=[("flash", "L0e+0.2", 0.7), ("shake", "L0e+0.2", 0.6, 16), ("particles", "silver", 40)],
                   sfx=[("L0e+0.2", SFX + "crystal_break_2.wav", 0.9), ("L0e+0.5", SFX + "revenge_1.wav", 0.6)],
                   sprites=[put(mirror_cracked(), W / 2, H / 2 - 40, 0.75, 0, t1="L0e+0.2", out=0.05, fade=0.4, enter=(0, 0),
                                glow=(200, 210, 255, 0.4)),
                            put(mirror_shattered(), W / 2, H / 2 - 40, 0.8, "L0e+0.2", fade=0.1, enter=(0, 0),
                                drift=(0, 0, 0.12), glow=(220, 225, 255, 0.6))],
                   lines=[N("그 거울을 깨는 것."),
                          N("그것이, 제자의 첫 번째 목표다.")], tail=1.4))

    # ── 5장 후계자 ──
    S.append(chapter("제5장", "후계자", BG_HALL, music="calm"))
    S.append(Scene(bg=BG_HALL, cam=((0.7, 0.45, 1.4), (0.75, 0.5, 1.15)), music="calm", dark=0.15,
                   fx=[("particles", "motes", 16)],
                   lines=[N("시즌이 끝나면, 가장 잘 큰 제자가 느루의 후계자로 지명된다."),
                          N("그런데 느루는, 후계자를 지명한 다음 날이면 마음이 바뀐다."),
                          D("neuru", "후계자는 한 명이라고 했지, 한 번만 뽑는다고는 안 했다."),
                          N("그래서 시즌마다 다시 뽑는다."),
                          N("첫 번째 은퇴. 은퇴 번복. 진짜 마지막 은퇴. 마지막이라고 한 적 없음."),
                          N("지난 후계자의 이름은, 광장의 후계자 비석에 새겨진다.")]))
    d = char("dalgu")
    sc_ = 560 / d.height
    S.append(Scene(bg=BG_CAMP, cam=((0.5, 0.55, 1.15), (0.5, 0.55, 1.05)), music="whimsy", dark=0.3, blur=2,
                   fx=[("particles", "fireflies", 14)],
                   sfx=[("L2", SFX + "ui_confirm.wav", 0.5), ("L2+0.4", SFX + "ui_confirm.wav", 0.5)],
                   sprites=[put(d, W * 0.33, H * 0.5, sc_, "L2-0.2", enter=(-80, 0), shadow=True, bob=5, glow=(255, 140, 60, 0.4)),
                            put(flipped(d), W * 0.67, H * 0.5, sc_, "L2+0.2", enter=(80, 0), shadow=True, bob=5, bob_hz=0.42,
                                glow=(255, 140, 60, 0.4))],
                   lines=[N("광장에서는, 같은 제자를 여럿 만나기도 한다."),
                          N("한뎃벌의 야생 마력이, 될 수도 있었던 제자들을 겹쳐 놓기 때문이다."),
                          N("달구가 다른 달구를 만나면, 서로 자기가 진짜라고 우긴다."),
                          N("느루는, 설명하지 않는다.", gap=0.8)]))

    # ── 맺음 ──
    S.append(Scene(bg=BG_TOWER_CAMP, cam=((0.5, 0.55, 1.25), (0.5, 0.5, 1.02)), music="epic", dark=0.4,
                   fx=[("particles", "fireflies", 30), ("particles", "embers", 16), ("flash", "L1e+0.4", 0.8, (255, 240, 210))],
                   sfx=[("L1e+0.4", SFX + "enh_plus13_bell.wav", 0.7)],
                   sprites=[put(logo, W / 2, H * 0.42, 1.25, "L1e+0.4", fade=1.0, enter=(0, 0), drift=(0, 0, 0.04),
                                glow=(255, 230, 180, 0.4))],
                   lines=[N("이것이, 지금까지의 이야기."),
                          N("엄청엄청 위대한 마법사가 될 다음 제자는, 바로 당신이다.")],
                   tail=4.5, fade_out=1.8))
    return S


if __name__ == "__main__":
    main(scenes, "story")
