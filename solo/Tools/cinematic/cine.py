"""시네마틱 영상 렌더러.

장면 목록(intro.py, story.py)을 받아 1920x1080 30fps mp4로 굽는다.
결과는 solo/Assets/StreamingAssets/Cinematic/<영상>.mp4, --calm이면 번쩍임·흔들림을 줄인 <영상>_calm.mp4
(연출 감도 "줄임/최소"를 고른 사람에게 CinematicScreen이 튼다).
그림은 저장소의 고화질 원본(art/sprite-pack/redrawn, art/production/*/source)을 쓰고,
목소리는 macOS `say`(임시 TTS), 음악은 절차 생성, 효과음은 게임 효과음(solo/Assets/Resources/Audio)을 쓴다.

녹음한 목소리로 바꾸려면 art/cinematic/voice/<영상>/<줄 id>.wav 를 두면 그 파일을 쓴다.
줄 id 목록은 렌더할 때 lines/<영상>_lines.tsv 로 나온다.
그림은 저장소의 원본을 읽기만 한다(고치지 않는다).
"""
from __future__ import annotations

import hashlib
import math
import os
import random
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field
from multiprocessing import Pool

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))   # 저장소 뿌리
CACHE = os.path.join(HERE, ".cache")
OUT = os.path.join(ROOT, "solo/Assets/StreamingAssets/Cinematic")
LINES = os.path.join(HERE, "lines")
CALM = False   # 번쩍임 세기를 15%로, 흔들림을 없앤다
W, H, FPS, SR = 1920, 1080, 30, 48000
FONT = os.path.join(ROOT, "art/production/ui/font/Galmuri11.ttf")
FONT_BIG = os.path.join(ROOT, "art/production/ui/font/neodgm.ttf")
LETTERBOX = 72
CLIPS = os.path.join(HERE, "clips")   # 로컬 영상 AI(Wan 2.2)로 움직인 장면 클립. ai_clips.py가 만든다
MODE = "normal"                       # "plate" = AI에 넣을 바탕 그림만 그린다(구운 그림 + 배경)


def A(p: str) -> str:
    return p if os.path.isabs(p) else os.path.join(ROOT, p)


# ───────────────────────── 그림 ─────────────────────────
_imgs: dict = {}


def load(path: str) -> Image.Image:
    if path not in _imgs:
        _imgs[path] = Image.open(A(path)).convert("RGBA")
    return _imgs[path]


def _trim(im: Image.Image, thr: int = 110) -> Image.Image:
    a = np.array(im)
    a[:, :, 3] = np.where(a[:, :, 3] < thr, 0, a[:, :, 3])
    im = Image.fromarray(a)
    bb = im.getbbox()
    return im.crop(bb) if bb else im


def cutout(path: str, index: int, minw: int = 20) -> Image.Image:
    """알파가 빈 세로 띠로 나뉜 시트에서 index번째 그림."""
    key = ("cut", path, index)
    if key in _imgs:
        return _imgs[key]
    im = load(path)
    a = np.array(im)[:, :, 3]
    cols = (a > 128).sum(0)
    runs, s = [], None
    for x, v in enumerate(cols):
        if v and s is None:
            s = x
        if not v and s is not None:
            runs.append((s, x)); s = None
    if s is not None:
        runs.append((s, len(cols)))
    runs = [r for r in runs if r[1] - r[0] >= minw]
    x0, x1 = runs[index]
    r = _trim(im.crop((x0, 0, x1, im.height)))
    _imgs[key] = r
    return r


def region(path: str, box, trim=True) -> Image.Image:
    key = ("reg", path, tuple(box))
    if key not in _imgs:
        im = load(path).crop(box)
        _imgs[key] = _trim(im) if trim else im
    return _imgs[key]


def sprite(path: str) -> Image.Image:
    key = ("spr", path)
    if key not in _imgs:
        _imgs[key] = _trim(load(path))
    return _imgs[key]


_fonts: dict = {}


def font(size: int, big=False):
    k = (size, big)
    if k not in _fonts:
        _fonts[k] = ImageFont.truetype(FONT_BIG if big else FONT, size)
    return _fonts[k]


def text_img(text: str, size: int = 44, color=(244, 238, 226), outline=(18, 14, 28), ow: int = 4,
             big=False, spacing: int = 14, align="center", shadow=True) -> Image.Image:
    f = font(size, big)
    d = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    bb = d.multiline_textbbox((0, 0), text, font=f, spacing=spacing, align=align, stroke_width=ow)
    w, h = int(bb[2] - bb[0]) + 24, int(bb[3] - bb[1]) + 24
    im = Image.new("RGBA", (w, h + 8), (0, 0, 0, 0))
    dr = ImageDraw.Draw(im)
    o = (12 - bb[0], 12 - bb[1])
    if shadow:
        sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
        ImageDraw.Draw(sh).multiline_text((o[0] + 4, o[1] + 6), text, font=f, fill=(0, 0, 0, 150), spacing=spacing,
                                          align=align, stroke_width=ow, stroke_fill=(0, 0, 0, 150))
        im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(3)))
    dr.multiline_text(o, text, font=f, fill=color, spacing=spacing, align=align, stroke_width=ow, stroke_fill=outline)
    return im


def wrap(text: str, n: int = 30) -> str:
    """한국어 자막 줄바꿈(어절 단위, 최대 두 줄 권장)."""
    if "\n" in text:
        return text
    words, lines, cur = text.split(" "), [], ""
    for w_ in words:
        if len(cur) + len(w_) + 1 > n and cur:
            lines.append(cur); cur = w_
        else:
            cur = (cur + " " + w_).strip()
    lines.append(cur)
    if len(lines) == 2 and len(lines[1]) < 6:  # 외톨이 어절 방지
        a = lines[0].split(" ")
        if len(a) > 2:
            lines = [" ".join(a[:-1]), a[-1] + " " + lines[1]]
    return "\n".join(lines)


def ease(t: float) -> float:
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def ease_out(t: float) -> float:
    t = min(1.0, max(0.0, t))
    return 1 - (1 - t) ** 3


# ───────────────────────── 장면 서술 ─────────────────────────
SPEAKERS = {
    # id: (화면 이름, say 목소리, 속도, 음높이 배율, 이름 색)
    "narr": (None, "Yuna", 168, 1.0, None),
    "neuru": ("느루", "Yuna", 150, 0.74, (226, 200, 150)),
    "dalgu": ("달구", "Yuna", 185, 1.12, (255, 160, 90)),
    "chorong": ("초롱", "Yuna", 140, 0.86, (250, 226, 110)),
    "galpi": ("갈피", "Yuna", 190, 1.2, (110, 220, 200)),
    "seonge": ("성에", "Yuna", 160, 1.04, (170, 210, 255)),
    "geumeum": ("그믐", None, 0, 1.0, (200, 160, 255)),
}


@dataclass
class Line:
    who: str
    text: str
    gap: float = 0.45
    face: str = "speak"   # neuru 초상: speak / both / one / sleep / laugh
    sub: str | None = None  # 자막을 따로 쓸 때
    hold: float = 0.0       # 목소리 없는 줄의 길이
    box: bool = True        # neuru 줄에서 초상 상자를 띄울지
    # 계산됨
    id: str = ""
    t0: float = 0.0
    dur: float = 0.0
    audio: np.ndarray | None = None


def N(text, gap=0.45, **k):
    return Line("narr", text, gap, **k)


def D(who, text, gap=0.5, **k):
    return Line(who, text, gap, **k)


@dataclass
class Spr:
    img: object            # PIL 이미지 또는 (t)->이미지 함수
    keys: list             # [(t, dict(x,y,s,a,r))]
    smooth: bool = True    # 확대 보간(False = 최근접)
    bob: float = 0.0       # 위아래 흔들림 픽셀
    bob_hz: float = 0.35
    z: int = 0
    glow: tuple | None = None   # (r,g,b,세기) 뒤에 번지는 빛
    shadow: bool = False
    baked: bool = False   # 장면 클립이 있으면 클립 안에 이미 들어 있으므로 그리지 않는다


def put(img, x, y, s=1.0, t0=0.0, t1=None, fade=0.6, enter=(0, 30), drift=(0, 0, 0.0), out=0.5, **kw) -> Spr:
    """t0에 enter 만큼 떨어진 곳에서 스며들어 (x,y)에 서고, 끝까지 drift(dx,dy,ds)만큼 흐르고, t1에 사라진다."""
    ex, ey = enter
    dx, dy, ds = drift
    keys = [(t0, dict(x=x + ex, y=y + ey, s=s, a=0.0)),
            (f"{t0}+{fade}" if isinstance(t0, str) else t0 + fade, dict(x=x, y=y, s=s, a=1.0))]
    if t1 is None:
        keys.append(("end", dict(x=x + dx, y=y + dy, s=s + ds, a=1.0)))
    else:
        keys.append((t1, dict(x=x + dx, y=y + dy, s=s + ds, a=1.0)))
        keys.append((f"{t1}+{out}" if isinstance(t1, str) else t1 + out, dict(x=x + dx, y=y + dy, s=s + ds, a=0.0)))
    return Spr(img, keys, **kw)


@dataclass
class Scene:
    bg: object = (8, 6, 14)             # 그림 경로 또는 RGB
    cam: tuple = ((0.5, 0.5, 1.0), (0.5, 0.5, 1.08))   # (중심x, 중심y, 확대) 시작→끝
    lines: list = field(default_factory=list)
    sprites: list = field(default_factory=list)
    fx: list = field(default_factory=list)  # ("particles", kind, n) / ("flash", t, dur) / ("shake", t, dur, amp)
    sfx: list = field(default_factory=list)  # (t, 경로, 음량)
    music: str = "calm"
    lead: float = 0.7
    tail: float = 0.9
    min_dur: float = 0.0
    fade_in: float = 0.6
    fade_out: float = 0.6
    dark: float = 0.0                      # 배경 어둡게(0~1)
    tint: tuple | None = None              # (r,g,b,세기)
    letterbox: bool = True
    subs: bool = True
    blur: float = 0.0
    # 로컬 AI 클립: (이름, 움직임 프롬프트[, 바탕 그림을 뜰 시각]). clips/<이름>.mp4가 있으면 배경 대신 그 클립을
    # 앞뒤로 되감으며 틀고 baked 그림은 건너뛴다. 없으면 지금처럼 그린다.
    clip: tuple | None = None
    clip_cam: tuple = ((0.5, 0.5, 1.0), (0.5, 0.5, 1.05))
    clip_speed: float = 0.8
    # 계산됨
    dur: float = 0.0
    t_start: float = 0.0


def chapter(no: str, title: str, bg, cam=None, music="mystery", dur=3.6, dark=0.55, sfx=None) -> Scene:
    """장 제목 카드."""
    s1 = text_img(no, 40, color=(200, 180, 140), ow=3)
    s2 = text_img(title, 96, big=True, ow=6)
    orn = ornament(min(1100, s2.width + 260))
    return Scene(bg=bg, cam=cam or ((0.5, 0.5, 1.12), (0.5, 0.5, 1.04)), dark=dark, blur=2.0, music=music,
                 lead=0, tail=0, min_dur=dur, fade_in=0.7, fade_out=0.7, letterbox=True,
                 sfx=sfx or [(0.2, "solo/Assets/Resources/Audio/common/sty_page_flutter.wav", 0.5)],
                 sprites=[put(s1, W / 2, H / 2 - 95, 1, 0.3, enter=(0, -16)),
                          put(orn, W / 2, H / 2 + 75, 1, 0.5, enter=(0, 0)),
                          put(s2, W / 2, H / 2 + 5, 1, 0.45, enter=(0, 20))])


def ornament(w: int) -> Image.Image:
    im = Image.new("RGBA", (w, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = (210, 186, 140, 230)
    d.line((20, 12, w / 2 - 22, 12), fill=c, width=2)
    d.line((w / 2 + 22, 12, w - 20, 12), fill=c, width=2)
    d.polygon([(w / 2, 2), (w / 2 + 10, 12), (w / 2, 22), (w / 2 - 10, 12)], fill=c)
    d.ellipse((12, 8, 20, 16), fill=c); d.ellipse((w - 20, 8, w - 12, 16), fill=c)
    return im


# ───────────────────────── 목소리 ─────────────────────────
def _ffmpeg_pcm(path: str, af: str | None = None) -> np.ndarray:
    cmd = ["ffmpeg", "-v", "error", "-i", path]
    if af:
        cmd += ["-af", af]
    cmd += ["-ac", "1", "-ar", str(SR), "-f", "f32le", "-"]
    return np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, np.float32).copy()


def tts(line: Line, video: str) -> np.ndarray | None:
    override = os.path.join(HERE, "voice", video, line.id + ".wav")
    if os.path.exists(override):
        return _ffmpeg_pcm(override)
    _, voice, rate, pitch, _ = SPEAKERS[line.who]
    if voice is None:
        return None
    say_text = line.text.replace("…", ",").replace("—", ",")
    h = hashlib.sha1(f"{voice}|{rate}|{pitch}|{say_text}".encode()).hexdigest()[:16]
    os.makedirs(CACHE, exist_ok=True)
    npy = os.path.join(CACHE, h + ".npy")
    if os.path.exists(npy):
        return np.load(npy)
    with tempfile.TemporaryDirectory() as td:
        aiff = os.path.join(td, "v.aiff")
        subprocess.run(["say", "-v", voice, "-r", str(rate), "-o", aiff, say_text], check=True)
        af = None
        if abs(pitch - 1) > 1e-3:
            sr0 = int(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "stream=sample_rate", "-of",
                                      "csv=p=0", aiff], capture_output=True, text=True).stdout.strip())
            af = f"asetrate={int(sr0 * pitch)},aresample={SR},atempo={1 / pitch:.4f}"
        pcm = _ffmpeg_pcm(aiff, af)
    # 앞뒤 무음 다듬기
    nz = np.nonzero(np.abs(pcm) > 0.01)[0]
    if len(nz):
        pcm = pcm[max(0, nz[0] - 600): nz[-1] + 2400]
    pcm = pcm / max(1e-6, np.abs(pcm).max()) * 0.9
    np.save(npy, pcm)
    return pcm


# ───────────────────────── 시간 계산 ─────────────────────────
def resolve_t(t, sc: Scene) -> float:
    if isinstance(t, (int, float)):
        return float(t)
    import re
    t = t.replace(" ", "")
    m = re.match(r"^([A-Za-z]+\d*e?|[\d.]+)((?:[+-][\d.]+)*)$", t)
    base = m.group(1)
    off = sum(float(x) for x in re.findall(r"[+-][\d.]+", m.group(2)))
    if base == "end":
        b = sc.dur
    elif base[0].isdigit():
        b = float(base)
    elif base.startswith("L") and base.endswith("e"):
        ln = sc.lines[int(base[1:-1])]
        b = ln.t0 + ln.dur
    elif base.startswith("L"):
        b = sc.lines[int(base[1:])].t0
    else:
        raise ValueError(t)
    return b + off


def layout(scenes: list[Scene], video: str):
    t = 0.0
    for si, sc in enumerate(scenes):
        cur = sc.lead
        for li, ln in enumerate(sc.lines):
            ln.id = f"{video}_{si:02d}_{li:02d}"
            ln.audio = tts(ln, video)
            ln.dur = len(ln.audio) / SR if ln.audio is not None else max(1.4, ln.hold or 1.6)
            if ln.hold:
                ln.dur = max(ln.dur, ln.hold)
            ln.t0 = cur
            cur += ln.dur + ln.gap
        sc.dur = max(sc.min_dur, cur - (sc.lines[-1].gap if sc.lines else 0) + sc.tail)
        sc.t_start = t
        t += sc.dur
    return t


# ───────────────────────── 그리기 ─────────────────────────
_vig = None


def vignette() -> Image.Image:
    global _vig
    if _vig is None:
        y, x = np.mgrid[0:H, 0:W]
        d = np.sqrt(((x - W / 2) / (W * 0.62)) ** 2 + ((y - H / 2) / (H * 0.62)) ** 2)
        a = np.clip((d - 0.55) / 0.6, 0, 1) ** 1.6 * 200
        arr = np.zeros((H, W, 4), np.uint8)
        arr[:, :, 3] = a.astype(np.uint8)
        _vig = Image.fromarray(arr)
    return _vig


class Renderer:
    def __init__(self, sc: Scene, seed: int):
        self.sc = sc
        self.rng = random.Random(seed)
        if isinstance(sc.bg, str):
            bg = load(sc.bg).convert("RGB")
            if sc.blur:
                bg = bg.filter(ImageFilter.GaussianBlur(sc.blur))
            arr = np.asarray(bg).astype(np.float32)
            if sc.tint:
                r, g, b, k = sc.tint
                arr = arr * (1 - k) + arr.mean(2, keepdims=True) * k * np.array([r, g, b]) / 255.0 * 1.4
            if sc.dark:
                arr *= (1 - sc.dark)
            self.bg = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
        else:
            self.bg = None
        self.clip = None
        if sc.clip and MODE != "plate" and os.path.exists(clip_path(sc.clip[0])):
            self.clip = ClipFrames(clip_path(sc.clip[0]))
        self.parts = self._particles()
        self._scaled: dict = {}
        self.subs = {}
        for ln in sc.lines:
            name, *_r, col = SPEAKERS[ln.who]
            txt = wrap(ln.sub or ln.text, 30)
            im = text_img(txt, 46, ow=4)
            tag = text_img(name, 34, color=col, ow=4) if name else None
            self.subs[ln.id] = (im, tag)
        for sp in sc.sprites:
            sp.rkeys = [(resolve_t(t, sc), v) for t, v in sp.keys]

    def _particles(self):
        ps = []
        for f in self.sc.fx:
            if f[0] != "particles":
                continue
            kind, n = f[1], f[2]
            for _ in range(n):
                ps.append(dict(kind=kind, x=self.rng.uniform(0, W), y=self.rng.uniform(0, H),
                               ph=self.rng.uniform(0, 6.28), sp=self.rng.uniform(0.5, 1.5),
                               sz=self.rng.choice([2, 3, 3, 4, 5])))
        return ps

    def _bg(self, t: float) -> Image.Image:
        sc = self.sc
        src, cam = self.bg, sc.cam
        if self.clip is not None:
            src, cam = self.clip.frame(t, sc.clip_speed), sc.clip_cam
        if src is None:
            return Image.new("RGB", (W, H), sc.bg)
        (x0, y0, z0), (x1, y1, z1) = cam
        k = ease(t / max(sc.dur, 1e-3)) if len(cam) == 2 else 0
        cx, cy, z = x0 + (x1 - x0) * k, y0 + (y1 - y0) * k, z0 + (z1 - z0) * k
        bw, bh = src.size
        cover = max(W / bw, H / bh) * z
        vw, vh = W / cover, H / cover
        left = min(max(cx * bw - vw / 2, 0), bw - vw)
        top = min(max(cy * bh - vh / 2, 0), bh - vh)
        sh = self._shake(t)
        return src.transform((W, H), Image.AFFINE, (1 / cover, 0, left + sh[0] / cover, 0, 1 / cover,
                                                         top + sh[1] / cover), resample=Image.BILINEAR)

    def _shake(self, t):
        if CALM:
            return (0, 0)
        for f in self.sc.fx:
            if f[0] == "shake":
                t0, d, amp = resolve_t(f[1], self.sc), f[2], f[3]
                if t0 <= t < t0 + d:
                    k = (1 - (t - t0) / d) * amp
                    return (math.sin(t * 91) * k, math.cos(t * 73) * k)
        return (0, 0)

    def _scaled_img(self, sp, im, s):
        q = round(s, 3)
        key = (id(im), q)
        r = self._scaled.get(key)
        if r is None:
            w, h = max(1, int(im.width * q)), max(1, int(im.height * q))
            r = im.resize((w, h), Image.LANCZOS if sp.smooth else Image.NEAREST)
            if len(self._scaled) > 400:
                self._scaled.clear()
            self._scaled[key] = r
        return r

    def _sprite_state(self, sp, t):
        ks = sp.rkeys
        if t <= ks[0][0]:
            return ks[0][1] if t >= ks[0][0] else None
        for (ta, va), (tb, vb) in zip(ks, ks[1:]):
            if ta <= t <= tb:
                k = ease((t - ta) / max(tb - ta, 1e-4))
                return {n: va.get(n, 0) + (vb.get(n, va.get(n, 0)) - va.get(n, 0)) * k for n in set(va) | set(vb)}
        return ks[-1][1]

    def frame(self, t: float) -> Image.Image:
        sc = self.sc
        fr = self._bg(t).convert("RGBA")
        sh = self._shake(t)
        for sp in sorted(sc.sprites, key=lambda s: s.z):
            if (self.clip is not None and sp.baked) or (MODE == "plate" and not sp.baked):
                continue
            st = self._sprite_state(sp, t)
            if not st or st.get("a", 1) <= 0.01:
                continue
            im = sp.img(t) if callable(sp.img) else sp.img
            s = st.get("s", 1.0)
            si = self._scaled_img(sp, im, s)
            a = st.get("a", 1.0)
            if st.get("r"):
                si = si.rotate(st["r"], resample=Image.BICUBIC, expand=True)
            x = st["x"] - si.width / 2 + sh[0]
            y = st["y"] - si.height / 2 + sp.bob * math.sin(t * 6.283 * sp.bob_hz) + sh[1]
            if sp.glow:
                r, g, b, k = sp.glow
                gl = Image.new("RGBA", si.size, (r, g, b, 0))
                gl.putalpha(si.getchannel("A").point(lambda v: int(v * k * a)))
                pad = 60
                g2 = Image.new("RGBA", (si.width + pad * 2, si.height + pad * 2), (0, 0, 0, 0))
                g2.paste(gl, (pad, pad))
                g2 = g2.filter(ImageFilter.GaussianBlur(28))
                fr.alpha_composite(g2, (int(x) - pad, int(y) - pad))
            if sp.shadow:
                shd = Image.new("RGBA", (int(si.width * 0.8), 40), (0, 0, 0, 0))
                ImageDraw.Draw(shd).ellipse((0, 0, shd.width - 1, 39), fill=(0, 0, 0, int(110 * a)))
                shd = shd.filter(ImageFilter.GaussianBlur(6))
                fr.alpha_composite(shd, (int(x + si.width * 0.1), int(y + si.height - 26)))
            if a < 0.999:
                si = si.copy()
                si.putalpha(si.getchannel("A").point(lambda v: int(v * a)))
            _composite(fr, si, int(x), int(y))
        if MODE == "plate":
            return fr.convert("RGB")
        self._draw_particles(fr, t)
        fr.alpha_composite(vignette())
        self._fx_overlay(fr, t)
        if sc.letterbox:
            d = ImageDraw.Draw(fr)
            d.rectangle((0, 0, W, LETTERBOX), fill=(0, 0, 0, 255))
            d.rectangle((0, H - LETTERBOX, W, H), fill=(0, 0, 0, 255))
        if sc.subs:
            self._draw_subs(fr, t)
        # 장면 페이드
        k = 1.0
        if sc.fade_in and t < sc.fade_in:
            k = ease(t / sc.fade_in)
        if sc.fade_out and t > sc.dur - sc.fade_out:
            k = min(k, ease((sc.dur - t) / sc.fade_out))
        out = fr.convert("RGB")
        if k < 0.999:
            out = Image.blend(Image.new("RGB", (W, H)), out, k)
        return out

    def _draw_particles(self, fr, t):
        if not self.parts:
            return
        lay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        for p in self.parts:
            k = p["kind"]
            if k == "fireflies":
                x = (p["x"] + math.sin(t * 0.6 * p["sp"] + p["ph"]) * 40) % W
                y = (p["y"] - t * 14 * p["sp"]) % H
                a = int(140 + 110 * math.sin(t * 2.2 * p["sp"] + p["ph"]))
                c = (255, 226, 130, max(0, a))
            elif k == "embers":
                x = (p["x"] + math.sin(t * 1.3 + p["ph"]) * 25) % W
                y = (p["y"] - t * 70 * p["sp"]) % H
                c = (255, 150, 60, int(180 * (0.5 + 0.5 * math.sin(t * 5 + p["ph"]))))
            elif k == "snow":
                x = (p["x"] + math.sin(t * 0.8 + p["ph"]) * 30) % W
                y = (p["y"] + t * 45 * p["sp"]) % H
                c = (225, 238, 255, 190)
            elif k == "stars":
                x, y = p["x"], p["y"] * 0.55
                c = (255, 255, 240, int(120 + 120 * math.sin(t * 2.5 * p["sp"] + p["ph"])))
            elif k == "motes":  # 보랏빛 마력 티끌
                x = (p["x"] + t * 12 * p["sp"]) % W
                y = (p["y"] - t * 22 * p["sp"] + math.sin(t + p["ph"]) * 20) % H
                c = (190, 150, 255, int(110 + 90 * math.sin(t * 1.7 + p["ph"])))
            elif k == "silver":
                x = (p["x"] + math.sin(t * 0.9 + p["ph"]) * 30) % W
                y = (p["y"] - t * 30 * p["sp"]) % H
                c = (225, 232, 255, int(130 + 110 * math.sin(t * 3 + p["ph"])))
            else:
                continue
            s = p["sz"]
            d.rectangle((x, y, x + s, y + s), fill=c)
        glow = lay.filter(ImageFilter.GaussianBlur(4))
        fr.alpha_composite(glow)
        fr.alpha_composite(lay)

    def _fx_overlay(self, fr, t):
        for f in self.sc.fx:
            if f[0] == "flash":
                t0, d = resolve_t(f[1], self.sc), f[2]
                col = f[3] if len(f) > 3 else (255, 255, 255)
                if t0 <= t < t0 + d:
                    a = int(255 * (0.15 if CALM else 1.0) * (1 - (t - t0) / d) ** 2)
                    fr.alpha_composite(Image.new("RGBA", (W, H), col + (a,)))
            elif f[0] == "darken":   # ("darken", t0, t1, 세기)
                t0, t1, k = resolve_t(f[1], self.sc), resolve_t(f[2], self.sc), f[3]
                if t > t0:
                    a = int(255 * k * ease((t - t0) / max(0.01, t1 - t0)))
                    fr.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, a)))

    def _draw_subs(self, fr, t):
        for ln in self.sc.lines:
            t0, t1 = ln.t0 - 0.1, ln.t0 + ln.dur + min(ln.gap, 0.35)
            if not (t0 <= t <= t1):
                continue
            a = min(1.0, (t - t0) / 0.18, (t1 - t) / 0.18)
            im, tag = self.subs[ln.id]
            y = H - LETTERBOX - im.height - 18
            boxes = []
            if ln.who == "neuru" and ln.box:
                pim = neuru_face(ln.face, t - ln.t0, ln.dur)
                pim = pim.resize((pim.width * 2, pim.height * 2), Image.NEAREST)
                px, py = 150, H - LETTERBOX - pim.height - 14
                frame = Image.new("RGBA", (pim.width + 16, pim.height + 16), (24, 18, 34, 230))
                ImageDraw.Draw(frame).rectangle((0, 0, frame.width - 1, frame.height - 1), outline=(210, 186, 140, 255), width=3)
                frame.alpha_composite(pim, (8, 8))
                boxes.append((frame, px - 8, py - 8))
            if tag is not None:
                boxes.append((tag, W // 2 - tag.width // 2, y - tag.height + 6))
            boxes.append((im, W // 2 - im.width // 2, y))
            for b, x, yy in boxes:
                if a < 0.999:
                    b = b.copy(); b.putalpha(b.getchannel("A").point(lambda v: int(v * a)))
                fr.alpha_composite(b, (int(x), int(yy)))


def _composite(fr, im, x, y):
    # 화면 밖으로 나간 부분 잘라서 합성
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + im.width), min(H, y + im.height)
    if x1 <= x0 or y1 <= y0:
        return
    fr.alpha_composite(im.crop((x0 - x, y0 - y, x1 - x, y1 - y)), (x0, y0))


NEURU = "art/sprite-pack/redrawn/gui/screen-assets/s-2405/neuru_{}.png"


def neuru_face(face: str, t: float, dur: float) -> Image.Image:
    if face == "speak":
        if t < dur - 0.15:
            return load(NEURU.format("speaking_01" if int(t * 7) % 2 == 0 else "speaking_02"))
        return load(NEURU.format("both_eyes"))
    return load(NEURU.format({"both": "both_eyes", "one": "one_eye", "sleep": "sleeping"}.get(face, face)))


def neuru_big(sc_ref: list, default="both_eyes"):
    """장면 안 느루 줄에 맞춰 입을 움직이는 큰 초상(그림 함수). sc_ref[0]에 장면을 나중에 넣는다."""
    def f(t):
        for ln in sc_ref[0].lines:
            if ln.who == "neuru" and ln.t0 <= t <= ln.t0 + ln.dur:
                return neuru_face(ln.face, t - ln.t0, ln.dur)
        return load(NEURU.format(default))
    return f


# ───────────────────────── 음악 ─────────────────────────
NOTE = {n: i for i, n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}


def hz(name: str, octv: int) -> float:
    return 440.0 * 2 ** ((NOTE[name] + 12 * (octv + 1) - 69) / 12)


CHORDS = {
    "Am": ["A", "C", "E"], "F": ["F", "A", "C"], "C": ["C", "E", "G"], "G": ["G", "B", "D"],
    "Dm": ["D", "F", "A"], "A#": ["A#", "D", "F"], "Gm": ["G", "A#", "D"], "A": ["A", "C#", "E"],
    "Em": ["E", "G", "B"], "E": ["E", "G#", "B"],
}
MOODS = {
    #  진행, 화음 길이(초), 패드, 오르골 밀도, 베이스, 장식
    "calm":    (["Am", "F", "C", "G"], 4.0, 0.20, 0.45, 0.10, "box"),
    "whimsy":  (["C", "G", "Am", "F"], 2.0, 0.12, 0.85, 0.16, "pluck"),
    "mystery": (["Dm", "A#", "Gm", "A"], 5.0, 0.22, 0.22, 0.10, "bell"),
    "tension": (["Dm", "Dm", "A#", "A"], 4.0, 0.18, 0.10, 0.22, "pulse"),
    "epic":    (["Am", "F", "C", "G"], 3.0, 0.28, 0.65, 0.20, "box"),
    "silent":  ([], 1, 0, 0, 0, ""),
}


def synth_mood(mood: str, t0: float, t1: float, seed: int) -> np.ndarray:
    prog, cl, pad_a, dens, bass_a, deco = MOODS[mood]
    n = int((t1 - t0) * SR)
    out = np.zeros(n, np.float32)
    if not prog:
        return out
    rng = random.Random(seed)
    tt = np.arange(n) / SR + t0
    # 화음 블록
    first = int(t0 // cl)
    last = int(t1 // cl) + 1
    for ci in range(first, last):
        cs, ce = ci * cl, (ci + 1) * cl
        ch = CHORDS[prog[ci % len(prog)]]
        a, b = max(0, int((cs - t0) * SR)), min(n, int((ce + 1.2 - t0) * SR))
        if b <= a:
            continue
        lt = tt[a:b] - cs
        env = np.clip(lt / 0.9, 0, 1) * np.clip((cl + 1.2 - lt) / 1.4, 0, 1)
        sig = np.zeros(b - a, np.float32)
        for i, nm in enumerate(ch):
            f = hz(nm, 3 + (1 if i and NOTE[nm] < NOTE[ch[0]] else 0))
            for det in (-0.6, 0.6):
                ph = 2 * np.pi * (f + det) * lt
                sig += (np.sin(ph) + 0.30 * np.sin(2 * ph) + 0.12 * np.sin(3 * ph)) * pad_a / 3
        if bass_a:
            fb = hz(ch[0], 2)
            benv = np.exp(-lt * 0.9) * np.clip(lt / 0.02, 0, 1)
            sig += np.sin(2 * np.pi * fb * lt) * benv * bass_a * 1.4
            if deco == "pulse":
                for k in range(int(cl / 0.5)):
                    pt = lt - k * 0.5
                    m = pt > 0
                    sig[m] += np.sin(2 * np.pi * fb * pt[m]) * np.exp(-pt[m] * 9) * 0.25 * (0.6 + 0.4 * (k % 2 == 0))
        out[a:b] += sig * env
        # 장식 음
        if dens and deco in ("box", "pluck", "bell"):
            scale = ch + [ch[0], ch[1]]
            step = {"box": 0.5, "pluck": 0.25, "bell": 1.0}[deco]
            k = 0
            while k * step < cl:
                st = cs + k * step
                if rng.random() < dens and t0 <= st < t1:
                    nm = rng.choice(scale)
                    f = hz(nm, 5 if deco != "bell" else 4 + rng.choice([0, 1]))
                    L = int(SR * (2.4 if deco == "bell" else 1.2))
                    sa = int((st - t0) * SR)
                    seg = np.arange(min(L, n - sa)) / SR
                    if deco == "pluck":
                        e = np.exp(-seg * 7)
                        v = (np.sin(2 * np.pi * f * seg) + 0.4 * np.sin(4 * np.pi * f * seg)) * e * 0.10
                    elif deco == "bell":
                        e = np.exp(-seg * 1.6)
                        v = (np.sin(2 * np.pi * f * seg) + 0.5 * np.sin(2 * np.pi * f * 2.76 * seg) * np.exp(-seg * 3)) * e * 0.10
                    else:
                        e = np.exp(-seg * 3.2)
                        v = (np.sin(2 * np.pi * f * seg) + 0.25 * np.sin(6 * np.pi * f * seg)) * e * 0.11
                    out[sa:sa + len(v)] += v.astype(np.float32)
                k += 1
    return out


def reverb(x: np.ndarray, secs=2.2, wet=0.35, seed=1) -> np.ndarray:
    from scipy.signal import fftconvolve
    rng = np.random.default_rng(seed)
    L = int(SR * secs)
    ir = rng.standard_normal(L).astype(np.float32) * np.exp(-np.arange(L) / SR * (6.9 / secs))
    ir[: int(SR * 0.02)] = 0
    ir /= np.sqrt((ir ** 2).sum())
    y = fftconvolve(x, ir)[: len(x)].astype(np.float32)
    return x * (1 - wet) + y * wet * 2.5


def build_audio(scenes: list[Scene], total: float, path: str):
    n = int(total * SR) + SR
    music = np.zeros(n, np.float32)
    voice = np.zeros(n, np.float32)
    fx = np.zeros(n, np.float32)
    xf = int(1.2 * SR)
    for i, sc in enumerate(scenes):
        a0 = sc.t_start
        seg = synth_mood(sc.music, a0, a0 + sc.dur + 1.2, seed=i * 31 + 7)
        a = int(a0 * SR)
        # 장면 사이 겹침(앞 장면 꼬리 1.2초가 다음 장면과 섞임)
        fade = np.ones(len(seg), np.float32)
        fade[:xf] = np.linspace(0, 1, xf) if i else 1
        fade[-xf:] = np.linspace(1, 0, xf)
        music[a:a + len(seg)] += (seg * fade)[: n - a]
        for ln in sc.lines:
            if ln.audio is None:
                continue
            s = int((a0 + ln.t0) * SR)
            voice[s:s + len(ln.audio)] += ln.audio[: n - s]
        for (t, p, vol) in sc.sfx:
            tt = resolve_t(t, sc)
            pcm = _ffmpeg_pcm(A(p)) * vol
            s = int((a0 + tt) * SR)
            fx[s:s + len(pcm)] += pcm[: n - s]
    music = reverb(music, 2.6, 0.45)
    # 내레이션 밑에서 음악을 낮춘다
    env = (np.abs(voice) > 0.02).astype(np.float32)
    k = int(0.35 * SR)
    env = np.convolve(env, np.ones(k, np.float32) / k, mode="same")
    env = np.clip(env * 3, 0, 1)
    music *= (1 - 0.45 * env)
    m = music / max(1e-6, np.abs(music).max()) * 0.32
    v = reverb(voice, 0.5, 0.08, seed=3)
    mix = m + v * 0.95 + reverb(fx, 1.2, 0.2, seed=5) * 0.55
    mix = np.tanh(mix * 1.1) / np.tanh(1.1)
    mix = mix[: int(total * SR)]
    st = np.stack([mix, mix], 1)
    pcm16 = (np.clip(st, -1, 1) * 32000).astype("<i2")
    import wave
    with wave.open(path, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(pcm16.tobytes())


# ───────────────────────── AI 클립 ─────────────────────────
def clip_path(name: str) -> str:
    return os.path.join(CLIPS, name + ".mp4")


class ClipFrames:
    """클립을 화면 크기(W×H, 꽉 채워 자름)의 날 RGB로 한 번 풀어 두고 memmap으로 읽는다."""

    def __init__(self, path: str):
        st = os.stat(path)
        h = hashlib.sha1(f"{path}|{st.st_mtime_ns}|{st.st_size}|{W}x{H}".encode()).hexdigest()[:16]
        os.makedirs(CACHE, exist_ok=True)
        raw = os.path.join(CACHE, f"clip_{h}.rgb")
        if not os.path.exists(raw):
            tmp = f"{raw}.{os.getpid()}.part"
            subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", path, "-vf",
                            f"scale={W}:{H}:force_original_aspect_ratio=increase:flags=lanczos,crop={W}:{H}",
                            "-f", "rawvideo", "-pix_fmt", "rgb24", tmp], check=True)
            os.replace(tmp, raw)
        n = os.path.getsize(raw) // (W * H * 3)
        self.mm = np.memmap(raw, np.uint8, "r", shape=(n, H, W, 3))
        self.n = n
        r = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=r_frame_rate",
                            "-of", "csv=p=0", path], capture_output=True, text=True).stdout.strip()
        a, b = (r.split("/") + ["1"])[:2]
        self.fps = float(a) / float(b or 1)

    def frame(self, t: float, speed: float) -> Image.Image:
        """앞으로 갔다가 거꾸로 돌아오기를 되풀이한다(제자리 움직임이 끊기지 않게)."""
        if self.n < 2:
            return Image.fromarray(np.array(self.mm[0]))
        f = int(t * self.fps * speed)
        period = 2 * (self.n - 1)
        k = f % period
        return Image.fromarray(np.array(self.mm[k if k < self.n else period - k]))


def plates(scenes: list[Scene], video: str, outdir: str, size=(1280, 704)):
    """clip이 붙은 장면마다 AI에 넣을 바탕 그림(<이름>.png)과 프롬프트(<이름>.txt)를 뽑는다. 이미 있는 이름은 건너뛴다."""
    global MODE
    layout(scenes, video)
    os.makedirs(outdir, exist_ok=True)
    MODE = "plate"
    try:
        for i, sc in enumerate(scenes):
            if not sc.clip:
                continue
            name, prompt = sc.clip[0], sc.clip[1]
            png = os.path.join(outdir, name + ".png")
            if os.path.exists(png):
                continue
            t = resolve_t(sc.clip[2], sc) if len(sc.clip) > 2 else sc.dur * 0.6
            im = Renderer(sc, seed=i).frame(t)
            tw, th = size
            k = max(tw / W, th / H)
            im = im.resize((round(W * k), round(H * k)), Image.LANCZOS)
            l, tp = (im.width - tw) // 2, (im.height - th) // 2
            im.crop((l, tp, l + tw, tp + th)).save(png)
            with open(os.path.join(outdir, name + ".txt"), "w") as f:
                f.write(prompt)
            print("plate", png, flush=True)
    finally:
        MODE = "normal"


# ───────────────────────── 굽기 ─────────────────────────
_SCENES: list = []


def _render_scene(args):
    idx, path, scale = args
    sc = _SCENES[idx]
    r = Renderer(sc, seed=idx)
    nfr = int(round(sc.dur * FPS))
    ow, oh = int(W * scale) // 2 * 2, int(H * scale) // 2 * 2
    p = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{ow}x{oh}",
                          "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "slow", "-crf", "21",
                          "-pix_fmt", "yuv420p", path], stdin=subprocess.PIPE)
    for f in range(nfr):
        im = r.frame(f / FPS)
        if scale != 1:
            im = im.resize((ow, oh), Image.BILINEAR)
        p.stdin.write(im.tobytes())
    p.stdin.close()
    p.wait()
    return idx


def render(scenes: list[Scene], video: str, scale: float = 1.0, jobs: int = 6, only=None):
    global _SCENES
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(LINES, exist_ok=True)
    total = layout(scenes, video)
    _SCENES = scenes
    with open(os.path.join(LINES, f"{video}_lines.tsv"), "w") as f:
        f.write("id\tspeaker\tstart\ttext\n")
        for sc in scenes:
            for ln in sc.lines:
                f.write(f"{ln.id}\t{ln.who}\t{sc.t_start + ln.t0:.2f}\t{ln.text}\n")
    print(f"[{video}] {len(scenes)} scenes, {total:.1f}s", flush=True)
    name = video + ("_calm" if CALM else "")
    tmp = os.path.join(CACHE, name)
    os.makedirs(tmp, exist_ok=True)
    idxs = list(range(len(scenes))) if only is None else only
    tasks = [(i, os.path.join(tmp, f"s{i:02d}.mp4"), scale) for i in idxs]
    import multiprocessing as mp
    with mp.get_context("fork").Pool(jobs) as pool:
        for i in pool.imap_unordered(_render_scene, tasks):
            print(f"  scene {i} done", flush=True)
    wav = os.path.join(tmp, "audio.wav")
    build_audio(scenes, total, wav)
    lst = os.path.join(tmp, "list.txt")
    with open(lst, "w") as f:
        for i in range(len(scenes)):
            f.write(f"file 's{i:02d}.mp4'\n")
    out = os.path.join(OUT, f"{name}.mp4")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", lst, "-i", wav,
                    "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", out],
                   check=True)
    print("wrote", out)
    return out


def stills(scenes: list[Scene], video: str, path: str):
    """줄마다 한 장씩 뽑은 확인용 모음."""
    layout(scenes, video)
    shots = []
    for i, sc in enumerate(scenes):
        r = Renderer(sc, seed=i)
        ts = [ln.t0 + ln.dur * 0.6 for ln in sc.lines] or [sc.dur * 0.6]
        for t in ts:
            shots.append(r.frame(t).resize((480, 270)))
    cols = 5
    rows = (len(shots) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * 480, rows * 270))
    for k, s in enumerate(shots):
        sheet.paste(s, ((k % cols) * 480, (k // cols) * 270))
    sheet.save(path, quality=85)
    print("wrote", path, len(shots))


def main(scenes_fn, video: str):
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--stills", help="확인용 장면 모음 jpg 경로")
    ap.add_argument("--scale", type=float, default=1.0)
    ap.add_argument("--jobs", type=int, default=6)
    ap.add_argument("--calm", action="store_true", help="번쩍임·흔들림을 줄인 판(<영상>_calm.mp4)")
    ap.add_argument("--plates", help="AI 클립용 바탕 그림을 뽑을 폴더(ai_clips.py가 쓴다)")
    a = ap.parse_args()
    global CALM
    CALM = a.calm
    scenes = scenes_fn()
    if a.plates:
        plates(scenes, video, a.plates)
    elif a.stills:
        stills(scenes, video, a.stills)
    else:
        render(scenes, video, a.scale, a.jobs)
