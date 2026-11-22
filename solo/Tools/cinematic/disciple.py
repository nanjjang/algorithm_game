"""제자 소개 영상(그 캐릭터를 처음 고를 때). 캐릭터 번호 1~5(char.1~char.5)마다 한 편.

    python3 solo/Tools/cinematic/disciple.py 1          # StreamingAssets/Cinematic/disciple_1.mp4
    python3 solo/Tools/cinematic/disciple.py all        # 다섯 편 모두
    python3 solo/Tools/cinematic/disciple.py 3 --calm   # disciple_3_calm.mp4
    python3 solo/Tools/cinematic/disciple.py 2 --stills x.jpg
내용의 기준은 docs/world-characters.md 4절(배경·자칭)과 7절(대사 견본의 "판 시작").
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cine import *  # noqa
from assets import *  # noqa
from story import big_char, card

# 번호 → (그림 id, 이름, 나이·한 줄, 배경, 장면 옵션, 내레이션, 대사, 발사 효과음)
DISCIPLES = {
    1: ("dalgu", "달구", "15세 · 대장간 집 둘째 딸", BG_FORGE,
        dict(fx=[("particles", "embers", 30)]),
        [N("화염의 달구."),
         N("불 조절을 못 해, 집 지붕을 두 번 태웠다."),
         N("세상에서 제일 뜨거운 마법사. 자칭.")],
        D("dalgu", "다 덤벼! 한 줄로!"), "fire_block_fireball_2.wav"),
    2: ("chorong", "초롱", "17세 · 등대지기의 아들", BG_LOGIN,
        dict(cam=((0.3, 0.45, 1.3), (0.38, 0.5, 1.2)), fx=[("particles", "fireflies", 26)]),  # 모닥불이 모자 뒤에 오지 않게 왼쪽을 본다
        [N("번개의 초롱."),
         N("벼락을 세 번 맞고도, 멀쩡했다."),
         N("세상에서 제일 빠른 마법사. 자면서도.")],
        D("chorong", "…금방 끝내자."), "sty_thunder_crack.wav"),
    3: ("galpi", "갈피", "13세 · 막내", BG_HALL,
        dict(fx=[("particles", "motes", 20)]),
        [N("비전의 갈피."),
         N("탑 서고의 책은 다 읽었지만, 주문은 한 번도 써 본 적이 없다."),
         N("세상에서 제일 똑똑한 마법사. 이론상.")],
        D("galpi", "교재 삼 장대로만 하면 됩니다.", sub="교재 3장대로만 하면 됩니다."), "fire_block_magic_bolt_2.wav"),
    4: ("seonge", "성에", "19세 · 첫째 제자", BG_DAWN,
        dict(tint=(140, 180, 255, 0.55), dark=0.4, fx=[("particles", "snow", 60)], music="calm"),
        [N("냉기의 성에."),
         N("느루의 첫 제자이자, 탑의 수석."),
         N("세상에서 제일 우아한 마법사.")],
        D("seonge", "가볍게 몸만 풀게요."), "sty_ice_spear.wav"),
    5: ("geumeum", "그믐", "나이 모름 · 어느 그믐밤에 온 아이", BG_DAWN,   # 일식 배경에는 그믐이 이미 그려져 있다
        dict(dark=0.55, tint=(120, 90, 200, 0.5), fx=[("particles", "motes", 26)], music="mystery"),
        [N("암흑의 그믐."),
         N("어디서 왔는지도, 후드 속 얼굴도, 아무도 모른다."),
         N("엄청엄청 위대한 마법사. 본인 확인 불가.")],
        D("geumeum", "……", hold=2.2), "fire_block_shadow_orb_2.wav"),
}


def scenes_for(no: int):
    cid, name, extra, bg, opt, narr, line, shot = DISCIPLES[no]
    el, col = ELEM[cid]
    opt = dict(opt)
    lines = list(narr) + [line]
    sfx = [(0.15, SFX + "sty_page_flutter.wav", 0.4), ("L3-0.15", SFX + shot, 0.7)]
    if cid == "geumeum":
        sfx.append(("L3+0.6", SFX + "sty_eye_blink.wav", 0.6))
    fx = list(opt.pop("fx", [])) + [("flash", "L3-0.1", 0.45, col)]
    s = Scene(bg=bg, cam=opt.pop("cam", ((0.45, 0.5, 1.2), (0.55, 0.5, 1.06))), dark=opt.pop("dark", 0.35),
              blur=2.5, music=opt.pop("music", "whimsy"), fx=fx, lines=lines, sfx=sfx, fade_in=0.9, tail=1.6,
              clip=clip(f"disciple_{no}", "L3"),
              fade_out=1.0, sprites=[big_char(cid, h=780), card(cid, name, extra)], **opt)
    return [s]


def main_all():
    args = sys.argv[1:]
    if not args or not (args[0] == "all" or args[0].isdigit()):
        print(__doc__); sys.exit(1)
    which = list(DISCIPLES) if args[0] == "all" else [int(args[0])]
    sys.argv = [sys.argv[0]] + args[1:]
    for no in which:
        main(lambda no=no: scenes_for(no), f"disciple_{no}")


if __name__ == "__main__":
    main_all()
