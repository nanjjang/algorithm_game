"""온라인 시즌 영상 두 편. 기준은 docs/world-characters.md 1.4절(시즌과 후계자).

    python3 solo/Tools/cinematic/season.py start    # season_start.mp4  시즌이 시작할 때
    python3 solo/Tools/cinematic/season.py end      # season_end.mp4    시즌이 끝날 때
    python3 solo/Tools/cinematic/season.py all [--calm] [--stills x.jpg]
영상은 미리 구워 두므로 시즌 이름·후계자 이름은 넣지 않는다(화면이 영상 뒤에 따로 보여 준다).
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cine import *  # noqa
from assets import *  # noqa
from mirror import neuru_side

NAMES = ["첫 번째 은퇴", "은퇴 번복", "진짜 마지막 은퇴", "마지막이라고 한 적 없음"]


def lineup(t0=0.3, y=H * 0.5, h=520, staff=False):
    xs = [W * 0.14, W * 0.32, W * 0.5, W * 0.68, W * 0.86]
    out = []
    for i, cid in enumerate(CHARS):
        im = char(cid, staff=staff)
        el, col = ELEM[cid]
        out.append(put(im, xs[i], y, h / im.height, t0 + i * 0.22, fade=0.6, enter=(0, 40), drift=(0, -6, 0.01),
                       glow=col + (0.4,), shadow=True, bob=4, bob_hz=0.38 + i * 0.03, z=1, baked=True))
    return out


def start():
    S = []
    S.append(Scene(bg=BG_CAMP, cam=((0.62, 0.45, 1.4), (0.6, 0.5, 1.2)), music="whimsy", dark=0.3, blur=2, fade_in=1.0,
                   fx=[("particles", "fireflies", 14)],
                   sprites=[neuru_side()], clip=clip("neuru_camp"),
                   lines=[N("느루는, 후계자를 지명한 다음 날이면 마음이 바뀐다."),
                          D("neuru", "후계자는 한 명이라고 했지,"),
                          D("neuru", "한 번만 뽑는다고는 안 했다.", face="one")]))
    # 시즌 이름이 넘어가는 카드
    sprs = []
    for k, nm in enumerate(NAMES):
        im = text_img(nm, 72, big=True, ow=5, color=(244, 238, 226) if k < 3 else (255, 226, 150))
        t0 = 0.3 + k * 0.9
        sprs.append(put(im, W / 2, H / 2, 1, t0, t1=None if k == 3 else t0 + 0.6, fade=0.25, out=0.2, enter=(0, 24)))
    S.append(Scene(bg=BG_HALL, cam=((0.5, 0.5, 1.12), (0.5, 0.5, 1.03)), music="whimsy", dark=0.6, blur=3,
                   lead=0, tail=0, min_dur=5.2, sprites=sprs,
                   fx=[("particles", "motes", 18)],
                   sfx=[(0.3 + k * 0.9, SFX + "sty_page_flutter.wav", 0.4) for k in range(4)]))
    # 탑 배경(제목 그림)에는 제자들이 이미 그려져 있으므로 빈 벌판에 세운다
    S.append(Scene(bg=BG_CAMP, cam=((0.5, 0.55, 1.2), (0.5, 0.5, 1.04)), music="epic", dark=0.45, blur=1.5,
                   fx=[("particles", "fireflies", 22), ("particles", "motes", 14), ("flash", "L1-0.2", 0.6, (255, 240, 210))],
                   sfx=[("L1-0.2", SFX + "enh_fanfare.wav", 0.6)],
                   sprites=lineup(), clip=clip("lineup_walk"),
                   lines=[N("그래서, 시즌마다 다시 뽑는다."),
                          N("새 시즌이 시작된다. 이번에 가장 잘 클 제자는, 누구일까.")],
                   tail=2.0, fade_out=1.4))
    return S


def end():
    S = []
    S.append(Scene(bg=BG_HALL, cam=((0.7, 0.45, 1.45), (0.74, 0.5, 1.15)), music="calm", dark=0.2, fade_in=1.0,
                   fx=[("particles", "motes", 18), ("flash", "L1e+0.2", 0.8, (255, 240, 210))],
                   sfx=[("L1e+0.2", SFX + "enh_plus13_bell.wav", 0.7)],
                   lines=[N("시즌이 끝났다."),
                          N("가장 잘 큰 제자의 이름이, 후계자 비석에 새겨진다.")], tail=1.6))
    S.append(Scene(bg=BG_DAWN, cam=((0.5, 0.55, 1.2), (0.5, 0.5, 1.04)), music="epic", dark=0.5, blur=1.5, tint=(140, 120, 220, 0.4),
                   fx=[("particles", "fireflies", 24), ("particles", "embers", 12)],
                   sprites=lineup(staff=True), clip=clip("lineup_camp"),
                   lines=[N("후계자의 칭호와 겉모습은, 다음 시즌에도 남는다."),
                          N("지팡이는, 한뎃벌의 야생 마력을 먹고 계속 자란다.")]))
    S.append(Scene(bg=BG_CAMP, cam=((0.62, 0.45, 1.4), (0.6, 0.5, 1.2)), music="whimsy", dark=0.3, blur=2,
                   fx=[("particles", "fireflies", 14)],
                   sprites=[neuru_side()], clip=clip("neuru_camp"),
                   lines=[N("그리고 다음 날, 느루는 또 마음이 바뀐다."),
                          D("neuru", "한 번만 뽑는다고는 안 했다.", face="one")], tail=2.0, fade_out=1.4))
    return S


VIDEOS = {"start": (start, "season_start"), "end": (end, "season_end")}

if __name__ == "__main__":
    args = sys.argv[1:]
    if not args or args[0] not in ("start", "end", "all"):
        print(__doc__); sys.exit(1)
    sys.argv = [sys.argv[0]] + args[1:]
    for k in (["start", "end"] if args[0] == "all" else [args[0]]):
        fn, name = VIDEOS[k]
        main(fn, name)
