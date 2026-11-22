"""영상에 쓰는 고화질 원본 그림의 경로와 잘라 쓰는 함수."""
from cine import W, H, cutout, region, sprite, load, text_img, Image, ImageDraw, put, Spr, LETTERBOX

RD = "art/sprite-pack/redrawn"
BG_TOWER_CAMP = f"{RD}/world/backplates/title-spec.png"      # 탑 앞 모닥불의 다섯 제자(가을 밤)
BG_TOWER_WALK = f"{RD}/world/backplates/title.png"           # 탑 앞 길을 걷는 다섯 제자
BG_STUDY = f"{RD}/world/backplates/ritual.png"               # 탑 안 책상
BG_CAMP = f"{RD}/gui/screen-assets/s-0501/camp_background.png"   # 빨랫줄이 걸린 야영지
BG_DAWN = f"{RD}/gui/screen-assets/s-1602/expedition_dawn.png"   # 새벽 벌판
BG_DUEL = f"{RD}/gui/screen-assets/s-0901/duel_floor.png"
BG_MARKET = f"{RD}/gui/screen-assets/s-2307/market_street.png"
BG_ECLIPSE = "solo/Assets/Resources/Art/backgrounds/title_1920x1080_v2.png"   # 폐허와 일식, 그믐
BG_NIGHTPATH = "solo/Assets/Resources/Art/backgrounds/title_solo_1672x941.png"  # 등불 밤길, 그믐
BG_HALL = "solo/Assets/Resources/Art/online/bg_records_hall.png"   # 서고와 비석
BG_LOGIN = "solo/Assets/Resources/Art/online/bg_login.png"         # 숲속 모닥불과 책
BG_HUB = "solo/Assets/Resources/Art/online/bg_online_hub.png"
BG_FORGE = "solo/Assets/Resources/Art/supplement/solo/char.1/forge_backdrop_1024x576.png"
BG_WORKBENCH = "art/sprite-pack/native-64/enhancement/background/workbench.png"

SEL = "art/production/players/source/selection.png"
CHARS = ["dalgu", "chorong", "galpi", "seonge", "geumeum"]
ELEM = {  # 속성 이름, 빛 색
    "dalgu": ("화염", (255, 140, 60)),
    "chorong": ("번개", (255, 226, 90)),
    "galpi": ("비전", (90, 230, 200)),
    "seonge": ("냉기", (150, 200, 255)),
    "geumeum": ("암흑", (180, 120, 255)),
}


IDLE = "art/production/cinematic-idles"   # 시네마틱용 고화질 서 있는 그림(검은 배경 없이 알파)
# 원본이 작은 그림은 최근접으로 키워 둔다(초롱은 192px뿐), 고친 판이 있으면 그쪽을 쓴다.
_IDLE_CHAR = {"chorong": ("originals/player.chorong.png", 4), "galpi": ("originals/player.galpi.revised.png", 1)}


def _idle(rel: str, up: int = 1):
    from cine import _imgs, _trim, A
    import os
    key = ("idle", rel, up)
    if key not in _imgs:
        if not os.path.exists(A(f"{IDLE}/{rel}")):
            return None
        im = load(f"{IDLE}/{rel}")
        if up > 1:
            im = im.resize((im.width * up, im.height * up), Image.NEAREST)
        _imgs[key] = _trim(im, 40)
    return _imgs[key]


def char(cid: str, staff: bool = False):
    """제자 그림. staff=True면 강화된 지팡이 판(달구·성에·그믐만 있다. 없으면 기본 판)."""
    if staff:
        im = _idle(f"upgraded-staves/player.{cid}.png")
        if im is not None:
            return im
    rel, up = _IDLE_CHAR.get(cid, (f"originals/player.{cid}.png", 1))
    im = _idle(rel, up)
    return im if im is not None else cutout(SEL, CHARS.index(cid))


def neuru_chair():
    """흔들의자에 앉은 느루(전신)."""
    return _idle("originals/master.neuru.png")


MON = "art/production/monster-rework/source"


def mon(mid: str, dark: bool = False):
    """적·보스 그림. cinematic-idles에 있으면 그쪽(dark=True면 밤 판), 없으면 monster-rework 원본."""
    base = mid.replace(".corrected", "")
    im = _idle(f"{'dark-originals' if dark else 'originals'}/{base}.png")
    if im is None and dark:
        im = _idle(f"originals/{base}.png")
    return im if im is not None else sprite(f"{MON}/{mid}.png")


def mirror_cracked():
    return region(f"{MON}/effects/mirror_shards.png", (0, 0, 887, 887))


def mirror_shattered():
    return region(f"{MON}/effects/mirror_shards.png", (887, 0, 1774, 887))


LOGO = "art/production/ui/screen/title/s-0202_title_korean_960x320.png"

SFX = "solo/Assets/Resources/Audio/common/"


def name_card(cid: str, title: str, extra: str = ""):
    """'화염 · 달구' 같은 이름 띠."""
    el, col = ELEM[cid]
    a = text_img(f"{el}의 마법사", 34, color=col, ow=3)
    b = text_img(title, 92, big=True, ow=6)
    c = text_img(extra, 30, color=(210, 200, 185), ow=3) if extra else None
    w = max(a.width, b.width, c.width if c else 0) + 20
    h = a.height + b.height + (c.height if c else 0)
    im = Image.new("RGBA", (w, h + 20), (0, 0, 0, 0))
    im.alpha_composite(a, (10, 0))
    im.alpha_composite(b, (10, a.height - 10))
    d = ImageDraw.Draw(im)
    y = a.height + b.height - 14
    d.rectangle((22, y, min(w - 10, 22 + b.width - 30), y + 3), fill=col + (230,))
    if c:
        im.alpha_composite(c, (10, y + 10))
    return im


# 로컬 영상 AI(Wan 2.2 TI2V-5B)에 주는 움직임 프롬프트. 클립 이름 → 프롬프트. 바탕 그림은 장면에서 뽑는다.
_STYLE = "Detailed pixel art game illustration, painterly pixel shading. "
_STILL = " Subtle looping idle motion only. Static locked-off camera, no zoom, no pan, no cuts. Keep the art style and character design unchanged."
CLIP_PROMPTS = {
    "disciple_1": "A young girl wizard wearing a copper kettle as a helmet and a green coat with an apron stands in a warm forge, "
                  "holding a long staff with a glowing ember cage. She breathes gently, her hair and coat sway, the forge fire flickers and sparks drift up.",
    "disciple_2": "A sleepy young wizard in a long floppy purple hat and an oversized yellow robe leans on a golden staff with a glowing lantern at night. "
                  "His head nods drowsily, the hat tip sways, the lantern light flickers softly, fireflies drift.",
    "disciple_3": "A young scholar wizard in layered purple robes with goggles on his head holds a tall ornate staff with a teal gem in a dim library hall. "
                  "His robe sways slightly, the teal gem pulses with light, candle flames flicker.",
    "disciple_4": "An elegant white-haired ice wizard in white and blue robes holds a crystal staff while small ice crystals float above her at a snowy dawn. "
                  "The ice crystals slowly bob and sparkle, her robe and hair move in a cold breeze, snow falls gently.",
    "disciple_5": "A small mysterious hooded wizard in a crimson cloak with two glowing eyes in the dark hood holds a staff with a violet crystal under an eclipse. "
                  "The cloak flutters, the glowing eyes slowly blink, the violet crystal pulses, purple motes drift.",
    "phantom": "A ghostly spirit made of flowing lavender cloth with an ornate oval mirror for a face hovers in a dark ruined field at night. "
               "The cloth ripples and streams like smoke, it floats up and down slowly, light glints across the mirror surface.",
    "neuru_camp": "A tiny old wizard with an enormous long white beard dozes in a wooden rocking chair at a forest campsite at night. "
                  "The rocking chair rocks slowly back and forth, his beard sways, he breathes calmly in his sleep.",
    "lineup_walk": "Five young wizards stand side by side in a row at a campsite at night, each holding a glowing staff. "
                   "Each one idles subtly, robes sway, staffs glow and pulse, fireflies drift.",
    "lineup_camp": "Five young wizards holding upgraded glowing staffs stand in a row on a misty field at night. "
                   "Robes sway, staff crystals glow and pulse, embers drift.",
    "tower_camp": "Five small wizards rest around a campfire in front of an old stone tower on an autumn night. "
                  "The campfire flickers, smoke rises, fallen leaves drift, the tower windows glow.",
}


def clip(name: str, t=None):
    """Scene(clip=...)에 넣는 값."""
    p = _STYLE + CLIP_PROMPTS[name] + _STILL
    return (name, p) if t is None else (name, p, t)


def flipped(im):
    return im.transpose(Image.FLIP_LEFT_RIGHT)
