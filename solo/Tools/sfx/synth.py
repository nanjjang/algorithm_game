"""
solo/Tools/sfx/synth.py — 공용(common)·제자 1(char.1) 효과음을 절차 합성해 WAV(44.1kHz 모노 16bit)로 쓴다.

    python -I Tools/sfx/synth.py               # 전부 다시 만든다(Assets/Resources/Audio/common, char.1)
    python -I Tools/sfx/synth.py --png         # + Logs/sfx/ 에 대표 소리의 파형·스펙트로그램 PNG
    python -I Tools/sfx/synth.py --only kill_1,ui_press --png
    python -I Tools/sfx/synth.py --report      # 길이·피크·단시간 음량 표만 찍는다

같은 SEED면 같은 바이트가 나온다(소리마다 씨앗 = crc32(SEED:폴더/이름)). .meta는 만들지 않는다(Unity가 만든다).
음량 기준은 README.md 참조(분류별 50ms 최대 RMS 목표 + 피크 −1 dBFS 이하).
"""
import argparse
import json
import os
import sys
import wave
import zlib

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp import *  # noqa: E402,F401,F403

SEED = "wizard-sfx-1"
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio")
LOGS = os.path.join(ROOT, "Logs", "sfx")

PEAK_MAX_DB = -1.0       # 피크 상한(dBFS)
LIMIT_KNEE_DB = -7.0     # 이 위는 부드럽게 눌러 피크 상한 안에 넣는다

# 분류 → 50ms 최대 RMS 목표(dBFS). 기존 제자 소리(char.2~5) 분석값(대개 −12 ~ −26, 대기 반복음 −30)에 맞춘다.
TARGET = {
    "ui": -25.0, "ui_main": -22.0,
    "fire": -17.0, "char_fire": -16.0,
    "hit": -16.0, "kill": -14.0, "big": -12.0, "player": -13.0,
    "heat": -18.0, "chime": -16.0, "art": -15.0,
    "tele": -16.0, "enemy": -18.0, "cry": -19.0, "boss": -13.0, "ambient": -22.0,
    "drop": -19.0, "wave": -17.0,
    "enh_small": -21.0, "enh": -16.0, "enh_big": -12.0, "theme": -14.0,
    "loop": -30.0,
}

SOUNDS = []


def snd(folder, name, cat, loop=False):
    def deco(fn):
        SOUNDS.append((folder, name, cat, fn, loop))
        return fn
    return deco


def rng(folder, name):
    return np.random.default_rng(zlib.crc32((SEED + ":" + folder + "/" + name).encode("utf-8")))


def st50(x, weighted=True):
    """50ms 창의 최대 RMS(dBFS). weighted = 120Hz 아래를 깎고 잰다(작은 스피커에서 안 들리는 초저역이 음량을 정하지 않게)."""
    if weighted:
        x = filt(x, lo=120.0, order=2)
    w = n_of(0.05)
    if len(x) <= w:
        return db(np.sqrt(np.mean(x ** 2)) + 1e-12)
    c = np.cumsum(np.concatenate([[0.0], x ** 2]))
    m = (c[w:] - c[:-w]) / w
    return 10 * np.log10(m.max() + 1e-12)


def soft_limit(x, ceiling):
    """knee 위의 크기만 tanh로 눌러 ceiling 안에 넣는다(모양을 크게 바꾸지 않는다)."""
    k = undb(LIMIT_KNEE_DB)
    a = np.abs(x)
    over = a > k
    y = x.copy()
    span = ceiling - k
    y[over] = np.sign(x[over]) * (k + span * np.tanh((a[over] - k) / span))
    return y


def finalize(x, cat, loop=False):
    x = np.asarray(x, dtype=np.float64)
    x = x - np.mean(x)
    if loop:
        # 반복음은 원형(주기) 필터로 — 끝과 처음이 그대로 이어진다
        X = np.fft.rfft(x)
        f = np.fft.rfftfreq(len(x), 1.0 / SR)
        X *= 1.0 / np.sqrt(1.0 + (28.0 / np.maximum(f, 1e-9)) ** 4)
        x = np.fft.irfft(X, len(x))
    else:
        x = filt(x, lo=28.0, order=2)             # 직류·초저역 제거
    if not loop:
        # 꼬리 자르기: 피크에서 −60dB 아래로 떨어진 뒤는 버린다(5ms 여유)
        a = np.abs(x)
        thr = a.max() * undb(-60)
        idx = np.nonzero(a > thr)[0]
        end = min(len(x), (idx[-1] if len(idx) else len(x) - 1) + n_of(0.005))
        x = x[:end]
        fi = min(n_of(0.0015), len(x) // 4)
        fo = min(n_of(0.02), max(1, len(x) // 8))
        if fi > 0:
            x[:fi] *= 0.5 - 0.5 * np.cos(np.pi * np.arange(fi) / fi)
        x[-fo:] *= 0.5 + 0.5 * np.cos(np.pi * np.arange(fo) / fo)
        x[-1] = 0.0
    g = undb(TARGET[cat] - st50(x))
    x = x * g
    ceiling = undb(PEAK_MAX_DB - 0.3)
    if np.max(np.abs(x)) > undb(LIMIT_KNEE_DB):
        x = soft_limit(x, ceiling)
    p = np.max(np.abs(x))
    if p > ceiling:
        x *= ceiling / p
    return x


def make_loop(fn, r, dur, xf=0.25):
    """dur + xf 만큼 만들고 끝을 앞에 겹쳐 이음매 없는 반복음으로."""
    x = fn(r, dur + xf)
    x = fit(x, n_of(dur + xf))
    n = n_of(dur)
    k = n_of(xf)
    w = np.sin(0.5 * np.pi * np.arange(k) / k)
    y = x[:n].copy()
    y[:k] = x[:k] * w + x[n:n + k] * np.cos(0.5 * np.pi * np.arange(k) / k)
    return y


def write_wav(path, x):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = np.clip(np.round(x * 32767.0), -32767, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


# ================================================================== UI (화면 소리)

def wood_tap(r, f0, dur, bright=1.0):
    """마른 나무 톡: 짧은 공명 둘 + 딸깍 노이즈."""
    y = modal([f0, f0 * 2.31, f0 * 3.9], [1.0, 0.5 * bright, 0.25 * bright], [0.012, 0.007, 0.004], dur, r)
    c = filt(noise(r, 0.004), lo=2500, hi=9000) * env_exp(0.004, 0.0008)
    y[:len(c)] += c * 0.5 * bright
    return y


@snd("common", "ui_press", "ui")
def _(r):
    return wood_tap(r, 820, 0.05)


@snd("common", "ui_release", "ui")
def _(r):
    return wood_tap(r, 1250, 0.04, bright=1.3) * 0.8


@snd("common", "ui_cursor_move", "ui")
def _(r):
    x = filt(noise(r, 0.03), lo=3500, hi=9000) * env_points(0.03, [(0, 0), (0.006, 1), (0.03, 0)])
    return x + wood_tap(r, 2400, 0.03) * 0.15


@snd("common", "ui_hover", "ui")
def _(r):
    x = filt(noise(r, 0.03), lo=4500, hi=11000) * env_points(0.03, [(0, 0), (0.01, 1), (0.03, 0)]) ** 2
    return x * 0.8


def page_flip(r, dur, rising):
    c = ramp(dur, 1800, 6000) if rising else ramp(dur, 6000, 1500)
    x = sweep_bp(noise(r, dur), c, q=1.2)
    e = env_points(dur, [(0, 0), (dur * (0.35 if rising else 0.15), 1), (dur, 0)]) ** 1.5
    flap = 1 + 0.35 * np.sin(2 * np.pi * 38 * t_of(dur))
    m = Mix().add(x * e * flap)
    m.add(wood_tap(r, 300 if not rising else 380, 0.05), at=dur * (0.75 if rising else 0.55), gain=0.6)
    return m.out()


@snd("common", "ui_screen_open", "ui_main")
def _(r):
    return page_flip(r, 0.15, True)


@snd("common", "ui_screen_close", "ui_main")
def _(r):
    return page_flip(r, 0.15, False)


def two_note(r, f1, f2, dur):
    m = Mix()
    h = dur * 0.45
    for i, f in enumerate((f1, f2)):
        x = modal([f, f * 2, f * 3.01], [1, 0.25, 0.08], [0.05, 0.03, 0.015], h + 0.03, r)
        m.add(x, at=i * h * 0.85)
    return fit(m.out(), n_of(dur))


@snd("common", "ui_toggle_on", "ui")
def _(r):
    return two_note(r, note_hz("E6"), note_hz("A6"), 0.08)


@snd("common", "ui_toggle_off", "ui")
def _(r):
    return two_note(r, note_hz("A6"), note_hz("E6"), 0.08)


def dull_thunk(r, f0, dur):
    y = thump(f0 * 1.5, f0, dur, 0.035)
    y += modal([f0 * 2.1, f0 * 3.3], [0.35, 0.15], [0.03, 0.02], dur, r)
    return filt(y + puff(r, dur, 200, 1500, 0.001, 0.01) * 0.3, hi=2200)


@snd("common", "ui_disabled", "ui")
def _(r):
    return dull_thunk(r, 170, 0.12)


@snd("common", "ui_error", "ui_main")
def _(r):
    m = Mix()
    m.add(dull_thunk(r, 150, 0.12))
    m.add(dull_thunk(r, 140, 0.1), at=0.07, gain=0.7)
    return m.out()


@snd("common", "ui_confirm", "ui_main")
def _(r):
    f = note_hz("G6")
    y = chime(f, 0.25, r, tau=0.12) + 0.4 * chime(f * 1.5, 0.25, r, tau=0.08)
    y = reverb(y, r, t60=0.25, wet=0.18)
    return fit(y, n_of(0.25))


@snd("common", "ui_tooltip", "ui")
def _(r):
    f = note_hz("C7")
    y = modal([f, f * 2.0], [1, 0.2], [0.02, 0.01], 0.05, r, attack=0.004)
    return y + filt(noise(r, 0.05), lo=6000) * env_exp(0.05, 0.01) * 0.2


# ================================================================== 발사 블록(C-01, 6종 × 3 변주)

BLOCKS = ["magic_bolt", "fireball", "chain_lightning", "boomerang", "ice_spike", "shadow_orb"]


def fire_block(r, block, v):
    p = [0.97, 1.0, 1.04][v] * r.uniform(0.985, 1.015)
    if block == "magic_bolt":
        d = 0.09
        f = ramp(d, 1500 * p, 620 * p)
        y = fm(f, 2.0, env_exp(d, 0.02) * 2.5, d) * env_exp(d, 0.03, 0.001)
        y += puff(r, d, 2500, 8000, 0.001, 0.012) * 0.35
        y += thump(260 * p, 140, d, 0.02) * 0.4
        return y
    if block == "fireball":
        d = 0.15
        y = whoosh(r, d, 700 * p, 2600 * p, q=1.0, peak_at=0.25) * 1.2
        y += thump(150 * p, 70, d, 0.04) * 0.8
        y += crackle(r, d, 220, lo=2000, hi=9000, amp_env=env_exp(d, 0.06)) * 0.6
        return drive(y, 1.5)
    if block == "chain_lightning":
        d = 0.12
        t = t_of(d)
        f = 140 * p * (1 + 0.6 * np.sign(np.sin(2 * np.pi * 37 * t)) * 0.3)
        buzz = square(f, d, bright=20) * (0.6 + 0.4 * (r.uniform(size=len(t)) > 0.7))
        buzz = filt(buzz, lo=500, hi=7000) * env_exp(d, 0.05, 0.001)
        z = crackle(r, d, 700, lo=3000, hi=12000, decay=0.0006, amp_env=env_exp(d, 0.05)) * 1.2
        return buzz * 0.5 + z + puff(r, d, 4000, 12000, 0.0005, 0.006) * 0.5
    if block == "boomerang":
        d = 0.14
        x = noise(r, d)
        c = 900 * p * (1 + 1.8 * np.sin(np.pi * np.linspace(0, 1, n_of(d))))
        y = sweep_bp(x, c, q=1.6) * np.sin(np.pi * np.linspace(0, 1, n_of(d))) ** 1.2
        y *= 1 + 0.5 * np.sin(2 * np.pi * 42 * t_of(d))
        y += sine(ramp(d, 420 * p, 520 * p), d) * env_ar(d, 0.03, 0.08) * 0.2
        return y
    if block == "ice_spike":
        d = 0.1
        f0 = 2900 * p
        y = modal([f0, f0 * 1.73, f0 * 2.61, f0 * 0.5], [1.0, 0.5, 0.3, 0.25], [0.05, 0.03, 0.018, 0.03], d, r)
        y += puff(r, d, 6000, 14000, 0.0003, 0.004) * 0.6
        return y
    if block == "shadow_orb":
        d = 0.15
        f = ramp(d, 210 * p, 115 * p)
        y = fm(f, 0.5, env_exp(d, 0.08) * 3.0, d) * env_ar(d, 0.012, 0.09)
        y += filt(noise(r, d), hi=900) * env_ar(d, 0.02, 0.1) * 0.5
        return filt(drive(y, 1.8), hi=3500)
    raise ValueError(block)


for _b in BLOCKS:
    for _v in range(3):
        snd("common", "fire_block_%s_%d" % (_b, _v + 1), "fire")(lambda r, b=_b, v=_v: fire_block(r, b, v))


# ================================================================== 적중·폭발·연쇄(H-01~H-04, H-13~H-15)

def hit_punch(r, p, dur=0.08):
    y = thump(190 * p, 70 * p, dur, 0.025) * 1.0
    y += puff(r, dur, 400, 3500, 0.0005, 0.014) * 1.1
    y += modal([420 * p, 690 * p], [0.5, 0.3], [0.02, 0.012], dur, r)
    c = filt(noise(r, 0.005), lo=2500, hi=10000) * env_exp(0.005, 0.001)
    y[:len(c)] += c * 0.5
    return drive(y, 1.6)


for _i, _p in enumerate([0.92, 1.0, 1.07, 1.14]):
    snd("common", "hit_common_%d" % (_i + 1), "hit")(lambda r, p=_p: hit_punch(r, p))


@snd("common", "weak_ping", "hit")
def _(r):
    f = 1900.0
    y = modal([f, f * 2.4, f * 3.9], [1, 0.4, 0.2], [0.05, 0.025, 0.012], 0.15, r)
    return y + hit_punch(r, 1.1, 0.15) * 0.6


@snd("common", "resist_hit", "hit")
def _(r):
    y = thump(120, 50, 0.15, 0.05) + puff(r, 0.15, 150, 700, 0.002, 0.03) * 0.6
    return filt(y, hi=900)


for _i, _f in enumerate([980.0, 1230.0]):
    snd("common", "armor_hit_%d" % (_i + 1), "hit")(
        lambda r, f=_f: metal(f, 0.12, r, tau=0.06) + thump(220, 90, 0.12, 0.02) * 0.5)


def explosion(r, dur, size):
    m = Mix()
    m.add(thump(95 * (1.15 - 0.3 * size), 32, dur, 0.12 + 0.2 * size), gain=1.0)
    n = noise(r, dur)
    hi = ramp(dur, 7000, 350)
    m.add(sweep(n, hi_track=hi, order=2) * env_exp(dur, 0.08 + 0.12 * size, 0.0008), gain=0.9)
    m.add(crackle(r, dur, 120 + 200 * size, lo=800, hi=6000, decay=0.003,
                  amp_env=env_points(dur, [(0, 0), (0.03, 1), (dur, 0)])), gain=0.5)
    y = drive(m.out(), 2.0)
    y = reverb(y, r, t60=0.35 + 0.4 * size, wet=0.25, damp=3500)
    return fit(y, n_of(dur))


snd("common", "explosion_small", "big")(lambda r: explosion(r, 0.4, 0.0))
snd("common", "explosion_large", "big")(lambda r: explosion(r, 0.7, 1.0))


def zap(r, dur, p):
    t = t_of(dur)
    f = (180 + 900 * r.uniform(size=len(t)) ** 3)
    f = filt(f, hi=300, order=1) * p
    b = square(np.maximum(f, 60), dur, bright=15)
    b = filt(b, lo=700, hi=9000) * env_exp(dur, 0.04, 0.001)
    gate = filt((r.uniform(size=len(t)) > 0.6).astype(float), hi=900, order=1)
    return b * (0.4 + gate) * 0.5 + crackle(r, dur, 900, lo=3000, hi=12000, decay=0.0005, amp_env=env_exp(dur, 0.04)) * 1.2


for _i, _p in enumerate([0.9, 1.0, 1.15]):
    snd("common", "chain_hop_%d" % (_i + 1), "hit")(lambda r, p=_p: zap(r, 0.1, p))


@snd("common", "affinity_found", "chime")
def _(r):
    m = Mix()
    for i, nme in enumerate(("E6", "B6")):
        f = note_hz(nme)
        m.add(chime(f, 0.3, r, tau=0.12), at=i * 0.1)
    return fit(reverb(m.out(), r, t60=0.3, wet=0.2), n_of(0.4))


# ================================================================== 처치(H-05~H-07): 음색 6묶음 × 2

def sparkle(r, dur, f_lo=5000, f_hi=9000, k=3, tau=0.03):
    m = Mix()
    for i in range(k):
        f = r.uniform(f_lo, f_hi)
        m.add(modal([f], [1], [tau], min(dur, tau * 5), r), at=r.uniform(0, dur * 0.4), gain=r.uniform(0.4, 1))
    return fit(m.out(), n_of(dur))


def kill_sound(r, group, v):
    p = 1.0 + 0.07 * v
    dur = 0.24 if group in ("soft", "small", "spark") else 0.3
    m = Mix()
    m.add(thump(130 * p, 45, dur, 0.06), gain=0.9)        # 저음 펀치(쾌감)
    if group == "soft":       # 이끼 슬라임·포자: 물컹한 "퐁"
        m.add(bubble(320 * p, 0.12, r, rise=2.8), gain=0.9)
        m.add(bubble(520 * p, 0.09, r, rise=2.2), at=0.03, gain=0.5)
        m.add(filt(noise(r, dur), lo=400, hi=3000) * env_exp(dur, 0.03), gain=0.4)
    elif group == "beast":    # 늑대·멧돼지·고슴도치: "퍼석"
        m.add(filt(noise(r, dur), lo=600, hi=5000) * env_exp(dur, 0.05, 0.0008), gain=0.9)
        m.add(grains(r, dur, 120, glen=0.008, lo=1500, hi=6000, amp_env=env_exp(dur, 0.08)), gain=0.7)
        m.add(thump(90 * p, 38, dur, 0.09), gain=0.6)
    elif group == "stone":    # 선돌 골렘: 무너지는 돌
        m.add(grains(r, dur, 160, glen=0.012, lo=300, hi=3500, amp_env=env_points(dur, [(0, 1), (dur, 0)])), gain=1.0)
        m.add(modal([210 * p, 330 * p], [1, 0.5], [0.05, 0.03], dur, r), gain=0.5)
    elif group == "plant":    # 버섯 주술사·가시 덩굴: 마른 잎·나무 바스락
        m.add(grains(r, dur, 220, glen=0.006, lo=2500, hi=9000, amp_env=env_exp(dur, 0.1)), gain=0.8)
        m.add(modal([430 * p, 980 * p], [1, 0.4], [0.03, 0.015], dur, r), gain=0.6)
    elif group == "spark":    # 등불 벌레·도깨비불: 불꽃 "팡"
        m.add(sine(ramp(0.15, 1600 * p, 400 * p), 0.15) * env_exp(0.15, 0.05), gain=0.5)
        m.add(crackle(r, dur, 500, lo=2500, hi=11000, amp_env=env_exp(dur, 0.08)), gain=0.8)
        m.add(puff(r, dur, 1000, 8000, 0.0005, 0.02), gain=0.5)
    elif group == "small":    # 박쥐·족제비·두꺼비: 가벼운 "뿅"
        m.add(sine(ramp(0.1, 500 * p, 1400 * p), 0.1) * env_exp(0.1, 0.035, 0.001), gain=0.7)
        m.add(puff(r, dur, 800, 5000, 0.0005, 0.02), gain=0.5)
    m.add(sparkle(r, dur, k=3), at=0.01, gain=0.18)        # 고음 반짝(쾌감)
    y = drive(m.out(), 1.4)
    y = reverb(y, r, t60=0.25, wet=0.12, damp=6000)
    return fit(y, n_of(dur))


KILL_GROUPS = ["soft", "beast", "stone", "plant", "spark", "small"]
for _gi, _g in enumerate(KILL_GROUPS):
    for _v in range(2):
        snd("common", "kill_%d" % (_gi * 2 + _v + 1), "kill")(lambda r, g=_g, v=_v: kill_sound(r, g, v))


def bright_pluck(f, dur, r):
    return modal([f, f * 2, f * 3, f * 4.01], [1, 0.5, 0.25, 0.12], [0.35, 0.2, 0.1, 0.06], dur, r, attack=0.001)


@snd("common", "multi_kill", "big")
def _(r):
    m = Mix()
    m.add(thump(110, 38, 0.5, 0.12, click=0.6, r=r), gain=1.0)
    m.add(filt(noise(r, 0.5), lo=500, hi=6000) * env_exp(0.5, 0.06), gain=0.5)
    for i, nme in enumerate(("C5", "E5", "G5", "C6")):
        m.add(bright_pluck(note_hz(nme), 0.45, r), at=0.015 * i, gain=0.25)
    m.add(sparkle(r, 0.4, 6000, 11000, k=6, tau=0.05), at=0.02, gain=0.25)
    return fit(reverb(drive(m.out(), 1.3), r, t60=0.5, wet=0.2), n_of(0.5))


@snd("common", "elite_kill", "big")
def _(r):
    m = Mix()
    m.add(thump(90, 30, 0.8, 0.22, click=0.8, r=r), gain=1.0)
    m.add(grains(r, 0.6, 140, glen=0.015, lo=300, hi=4000, amp_env=env_points(0.6, [(0, 1), (0.6, 0)])), at=0.03, gain=0.7)
    m.add(sweep(noise(r, 0.5), hi_track=ramp(0.5, 5000, 300)) * env_exp(0.5, 0.12), gain=0.6)
    for i, nme in enumerate(("G5", "C6", "E6", "G6")):
        m.add(chime(note_hz(nme), 0.5, r, tau=0.25), at=0.12 + 0.07 * i, gain=0.22)
    return fit(reverb(drive(m.out(), 1.5), r, t60=0.7, wet=0.25), n_of(0.8))


@snd("common", "boss_kill", "big")
def _(r):
    m = Mix()
    for i in range(3):
        m.add(explosion(r, 0.7, 0.6 + 0.2 * i), at=i * 0.22, gain=0.8 - 0.15 * i)
    for i, nme in enumerate(("C4", "G4", "C5", "E5", "G5")):
        m.add(tone(note_hz(nme), 0.9, "saw", attack=0.25, release=0.5) * 0.5, at=0.4, gain=0.12)
        m.add(chime(note_hz(nme) * 2, 0.8, r, tau=0.35), at=0.45 + 0.04 * i, gain=0.12)
    y = m.out()
    y = filt(y, hi=9000)
    return fit(reverb(y, r, t60=0.9, wet=0.25), n_of(1.4))


# ================================================================== 플레이어(H-08~H-12, N-12)

@snd("common", "player_hit", "player")
def _(r):
    m = Mix()
    m.add(thump(200, 70, 0.25, 0.05, click=0.9, r=r), gain=0.9)
    m.add(filt(noise(r, 0.08), lo=1800, hi=6000) * env_exp(0.08, 0.015, 0.0003), gain=0.9)
    # 또렷한 쇳소리 "징"(두 음이 살짝 어긋나 거슬리게) — 아래로 휜다
    f = ramp(0.22, 1450, 900)
    m.add((sine(f, 0.22) + 0.7 * sine(f * 1.06, 0.22)) * env_exp(0.22, 0.06, 0.001) * env_ar(0.22, 0.001, 0.08, 1.0), gain=0.35)
    return fit(drive(m.out(), 1.8), n_of(0.25))


@snd("common", "invuln_end", "chime")
def _(r):
    m = Mix()
    m.add(modal([2200], [1], [0.02], 0.05, r), gain=0.6)
    m.add(modal([2950, 5900], [1, 0.3], [0.04, 0.02], 0.12, r), at=0.06)
    return fit(m.out(), n_of(0.2))


@snd("common", "player_down", "player")
def _(r):
    d = 1.2
    m = Mix()
    f = ramp(0.9, 520, 95)
    m.add(filt(saw(f, 0.9, bright=10), hi=2500) * env_points(0.9, [(0, 0), (0.03, 1), (0.9, 0)]), gain=0.5)
    m.add(sine(f * 0.5, 0.9) * env_points(0.9, [(0, 0), (0.05, 1), (0.9, 0)]), gain=0.4)
    m.add(thump(120, 40, 0.4, 0.12), at=0.75, gain=0.8)
    # 지팡이 떨어짐(나무 딸깍 두 번)
    m.add(wood_tap(r, 520, 0.08), at=0.88, gain=0.7)
    m.add(wood_tap(r, 610, 0.06), at=0.98, gain=0.4)
    return fit(reverb(m.out(), r, t60=0.6, wet=0.2), n_of(d))


@snd("common", "player_rise", "chime")
def _(r):
    m = Mix()
    for i, nme in enumerate(("C5", "E5", "G5")):
        m.add(chime(note_hz(nme), 0.45, r, tau=0.22), at=i * 0.14)
    m.add(whoosh(r, 0.6, 600, 4000, q=1.2, peak_at=0.8) * 0.3)
    return fit(reverb(m.out(), r, t60=0.4, wet=0.2), n_of(0.8))


# ================================================================== 열·스침·결정(C-03~C-25)

@snd("common", "heat_imminent", "heat")
def _(r):
    d = 0.45
    f = ramp(d, 110, 175)
    y = filt(saw(f, d, bright=14), hi=1800) * env_ar(d, 0.12, 0.12, 1.0)
    y *= 1 + 0.3 * np.sin(2 * np.pi * 9 * t_of(d))
    return y


@snd("common", "heat_critical", "heat")
def _(r):
    d = 0.5
    t = t_of(d)
    y = sine(880, d) + 0.5 * sine(1320, d) + 0.25 * sine(1762, d)
    y *= env_ar(d, 0.06, 0.25, 1.0) * (1 + 0.15 * np.sin(2 * np.pi * 6 * t))
    return y


for _i, _f in enumerate([2500.0, 2800.0]):
    snd("common", "heat_crit_shot_%d" % (_i + 1), "heat")(
        lambda r, f=_f: modal([f, f * 1.51, f * 2.23], [1, 0.5, 0.3], [0.08, 0.05, 0.03], 0.2, r)
        + puff(r, 0.2, 5000, 12000, 0.0003, 0.005) * 0.4)


@snd("common", "heat_brink", "heat")
def _(r):
    m = Mix()
    for i in range(5):
        m.add(wood_tap(r, 1700 + 120 * (i % 2), 0.03, bright=1.5), at=i * 0.055, gain=0.9 - 0.08 * i)
    return fit(m.out(), n_of(0.3))


@snd("common", "heat_lock", "heat")
def _(r):
    m = Mix()
    m.add(metal(700, 0.12, r, tau=0.03), gain=0.8)
    m.add(metal(520, 0.15, r, tau=0.04), at=0.05, gain=0.9)
    m.add(steam(r, 0.4, attack=0.02, tau=0.12), at=0.06, gain=0.6)
    return fit(m.out(), n_of(0.45))


@snd("common", "heat_dry", "heat")
def _(r):
    return wood_tap(r, 1400, 0.12, bright=0.8)


@snd("common", "heat_unlock", "heat")
def _(r):
    return S(steam(r, 0.25, attack=0.005, tau=0.06, lo=3000), wood_tap(r, 900, 0.05) * 0.4)


@snd("common", "heat_graze_unlock", "heat")
def _(r):
    m = Mix()
    m.add(glass_shatter(r, 0.35, density=40), gain=0.7)
    m.add(chime(note_hz("C7"), 0.35, r, tau=0.12), gain=0.6)
    return fit(m.out(), n_of(0.4))


@snd("common", "graze_ding", "chime")
def _(r):
    f = note_hz("C7")
    y = chime(f, 0.25, r, tau=0.1) + filt(noise(r, 0.25), lo=6000) * env_exp(0.25, 0.008) * 0.4
    return y


@snd("common", "crystal_note", "chime")
def _(r):
    f = note_hz("C6")
    y = bell(f, 0.25, r, tau=0.18, bright=0.8)
    return fit(reverb(y, r, t60=0.2, wet=0.12), n_of(0.25))


@snd("common", "crystal_full", "chime")
def _(r):
    m = Mix()
    for i, nme in enumerate(("C6", "E6", "G6", "C7")):
        m.add(bell(note_hz(nme), 0.45, r, tau=0.25, bright=0.8), at=i * 0.03, gain=0.4)
    return fit(reverb(m.out(), r, t60=0.4, wet=0.2), n_of(0.5))


for _i in range(2):
    snd("common", "crystal_break_%d" % (_i + 1), "chime")(
        lambda r, i=_i: glass_shatter(r, 0.3, density=70 + 20 * i, lo=2500 + 400 * i) + thump(300, 150, 0.3, 0.02) * 0.2)


@snd("common", "debt_borrow", "chime")
def _(r):
    return bell(note_hz("G3"), 0.4, r, tau=0.2, bright=0.5) + thump(110, 70, 0.4, 0.08) * 0.5


@snd("common", "debt_repay", "chime")
def _(r):
    return wood_tap(r, 1500, 0.2, bright=1.2) + chime(note_hz("G6"), 0.2, r, tau=0.06) * 0.4


@snd("common", "starfall_land", "chime")
def _(r):
    m = Mix()
    f = ramp(0.3, 2600, 900)
    m.add(sine(f, 0.3) * env_points(0.3, [(0, 0), (0.1, 1), (0.3, 0.3)]), gain=0.25)
    m.add(thump(200, 80, 0.2, 0.04, click=0.5, r=r), at=0.28)
    m.add(sparkle(r, 0.2, k=4), at=0.28, gain=0.3)
    return fit(m.out(), n_of(0.5))


@snd("common", "starfall_pick", "chime")
def _(r):
    return fit(reverb(chime(note_hz("E7"), 0.3, r, tau=0.14), r, t60=0.25, wet=0.15), n_of(0.3))


def coin(r, f0, dur=0.15):
    return modal([f0, f0 * 1.34, f0 * 2.03, f0 * 2.71], [1, 0.7, 0.4, 0.3], [0.06, 0.05, 0.03, 0.02], dur, r)


@snd("common", "exchange", "chime")
def _(r):
    m = Mix()
    for i, nme in enumerate(("C6", "D6", "E6", "G6", "A6")):
        m.add(coin(r, note_hz(nme) * 2), at=i * 0.06, gain=0.6)
    return fit(m.out(), n_of(0.45))


@snd("common", "combo_break", "heat")
def _(r):
    d = 0.3
    y = sweep_bp(noise(r, d), ramp(d, 5000, 900), q=1.5) * env_points(d, [(0, 0), (0.04, 1), (d, 0)])
    return y


@snd("common", "slow_applied", "heat")
def _(r):
    d = 0.2
    return grains(r, d, 250, glen=0.004, lo=5000, hi=13000, amp_env=env_ar(d, 0.03, 0.15)) \
        + chime(3400, d, r, tau=0.04) * 0.15


@snd("common", "orb_pass", "heat")
def _(r):
    d = 0.2
    y = S(bubble(180, 0.14, r, rise=1.8), filt(noise(r, d), hi=600) * env_exp(d, 0.05) * 0.4)
    return filt(y, hi=1200)


# ================================================================== 비기(T-00~T-19 공용)

@snd("common", "art_cast", "art")
def _(r):
    d = 0.3
    m = Mix()
    m.add(sweep_bp(noise(r, d), ramp(d, 900, 7000), q=1.3) * env_points(d, [(0, 0), (0.26, 1), (0.3, 0)]) ** 2)
    for i, f in enumerate((1046, 1568, 2093)):
        m.add(sine(f, d) * env_points(d, [(0, 0), (0.26, 1), (0.3, 0)]) ** 3, gain=0.15)
    return m.out()


@snd("common", "art_fizzle", "art")
def _(r):
    m = Mix()
    m.add(wood_tap(r, 900, 0.06, 0.8))
    m.add(wood_tap(r, 820, 0.06, 0.6), at=0.09, gain=0.8)
    m.add(puff(r, 0.2, 2000, 6000, 0.01, 0.05), at=0.05, gain=0.15)
    return fit(m.out(), n_of(0.25))


@snd("common", "art_late", "art")
def _(r):
    return fit(dull_thunk(r, 140, 0.25), n_of(0.25))


@snd("common", "art_t01", "art")   # ① 마력 방출
def _(r):
    m = Mix()
    m.add(thump(120, 40, 0.6, 0.15, click=0.6, r=r))
    m.add(whoosh(r, 0.5, 400, 5000, q=0.9, peak_at=0.15), gain=1.0)
    m.add(fm(ramp(0.5, 300, 900), 1.5, env_exp(0.5, 0.15) * 4, 0.5) * env_exp(0.5, 0.15, 0.005), gain=0.3)
    m.add(sparkle(r, 0.5, k=6, tau=0.06), at=0.05, gain=0.25)
    return fit(reverb(drive(m.out(), 1.5), r, t60=0.5, wet=0.2), n_of(0.6))


@snd("common", "art_t02", "art")   # ② 반동탄
def _(r):
    m = Mix()
    m.add(thump(90, 35, 0.4, 0.1, click=1.0, r=r), gain=1.1)
    m.add(filt(noise(r, 0.4), lo=200, hi=3000) * env_exp(0.4, 0.06, 0.0005), gain=0.8)
    m.add(whoosh(r, 0.25, 3000, 600, q=1.2, peak_at=0.2), at=0.08, gain=0.6)
    return fit(reverb(drive(m.out(), 2.0), r, t60=0.4, wet=0.2), n_of(0.4))


@snd("common", "art_t03", "art")   # ③ 부채
def _(r):
    m = Mix()
    for i in range(5):
        m.add(whoosh(r, 0.16, 1200 + 300 * i, 3500 + 500 * i, q=1.4, peak_at=0.3), at=i * 0.04, gain=0.7)
    m.add(thump(160, 70, 0.2, 0.04), gain=0.5)
    return fit(m.out(), n_of(0.4))


@snd("common", "art_t04", "art")   # ④ 쌍날개
def _(r):
    m = Mix()
    for i in range(2):
        w = whoosh(r, 0.3, 700, 4200, q=1.2, peak_at=0.4)
        w *= 1 + 0.4 * np.sin(2 * np.pi * 28 * t_of(0.3))   # 날갯짓 떨림
        m.add(w, at=i * 0.09, gain=0.8)
    m.add(chime(note_hz("A6"), 0.4, r, tau=0.15), at=0.12, gain=0.2)
    return fit(m.out(), n_of(0.5))


@snd("common", "art_t05", "art")   # ⑤ 잔상
def _(r):
    w = whoosh(r, 0.14, 1500, 6000, q=1.5, peak_at=0.4)
    y = delay(w, 0.06, fb=0.45, taps=3, damp=3000)
    return fit(y, n_of(0.3))


@snd("common", "art_t06", "art")   # ⑥ 관통창
def _(r):
    m = Mix()
    m.add(sine(ramp(0.15, 600, 2400), 0.15) * env_ar(0.15, 0.1, 0.02, 1), gain=0.25)
    m.add(fm(ramp(0.35, 2600, 1800), 3.0, env_exp(0.35, 0.1) * 2, 0.35) * env_exp(0.35, 0.12, 0.001), at=0.13, gain=0.4)
    m.add(whoosh(r, 0.3, 4000, 1500, q=2.0, peak_at=0.1), at=0.13, gain=0.7)
    m.add(thump(150, 60, 0.3, 0.06, click=0.7, r=r), at=0.13, gain=0.7)
    return fit(reverb(m.out(), r, t60=0.4, wet=0.18), n_of(0.5))


@snd("common", "art_t07", "art")   # ⑦ 방열
def _(r):
    m = Mix()
    m.add(thump(80, 35, 0.6, 0.15, click=0.5, r=r))
    m.add(steam(r, 0.6, attack=0.01, tau=0.2, lo=1500, hi=10000), gain=1.0)
    m.add(filt(noise(r, 0.6), hi=600) * env_exp(0.6, 0.15), gain=0.5)
    return fit(m.out(), n_of(0.6))


@snd("common", "art_t08", "art")   # ⑧ 팔방
def _(r):
    m = Mix()
    for i in range(8):
        p = semis([0, 2, 4, 7, 9, 12, 14, 16][i])
        d = 0.09
        y = fm(ramp(d, 1300 * p, 650 * p), 2.0, env_exp(d, 0.02) * 2, d) * env_exp(d, 0.03, 0.001)
        m.add(y + puff(r, d, 2500, 8000, 0.0005, 0.01) * 0.3, at=i * 0.05, gain=0.6)
    return fit(m.out(), n_of(0.6))


@snd("common", "art_t09a", "art")  # ⑨ 저스트 신호 "칭—"
def _(r):
    f = note_hz("E7")
    y = chime(f, 0.4, r, tau=0.2) + 0.5 * chime(f * 1.5, 0.4, r, tau=0.12)
    return fit(reverb(y, r, t60=0.4, wet=0.2), n_of(0.4))


@snd("common", "art_t09b", "art")  # ⑨ 뇌광창
def _(r):
    m = Mix()
    m.add(zap(r, 0.25, 1.4), gain=1.0)
    m.add(filt(noise(r, 0.05), lo=1000) * env_exp(0.05, 0.01, 0.0002), gain=1.2)
    m.add(sweep(noise(r, 0.8), hi_track=ramp(0.8, 3000, 200)) * env_exp(0.8, 0.25, 0.01), at=0.05, gain=0.8)
    m.add(thump(70, 30, 0.8, 0.25), at=0.03, gain=0.8)
    return fit(drive(m.out(), 1.6), n_of(0.8))


@snd("common", "art_t10a", "art")  # ⑩ 결정 모으기(회오리)
def _(r):
    d = 0.6
    y = sweep_bp(noise(r, d), ramp(d, 500, 5000), q=2.0) * env_ar(d, 0.5, 0.06, 1)
    y *= 1 + 0.5 * np.sin(2 * np.pi * ramp(d, 6, 22, "lin").cumsum() / SR)
    y += sine(ramp(d, 300, 1200), d) * env_ar(d, 0.5, 0.06, 1) * 0.2
    return y


@snd("common", "art_t10b", "art")  # ⑩ 파동(음계의 근음 — 코드가 음높이를 올린다)
def _(r):
    m = Mix()
    m.add(bell(note_hz("C5"), 0.3, r, tau=0.2, bright=0.7), gain=0.7)
    m.add(whoosh(r, 0.25, 800, 3000, q=1.0, peak_at=0.2), gain=0.4)
    return fit(m.out(), n_of(0.3))


@snd("common", "art_t10c", "art")  # ⑩ 마지막 파동(화음 + 소거)
def _(r):
    m = Mix()
    for i, nme in enumerate(("C5", "E5", "G5", "C6", "E6")):
        m.add(bell(note_hz(nme), 0.8, r, tau=0.4, bright=0.7), at=0.01 * i, gain=0.3)
    m.add(thump(110, 40, 0.5, 0.12, click=0.5, r=r), gain=0.7)
    m.add(steam(r, 0.6, attack=0.02, tau=0.2, lo=3000), at=0.05, gain=0.3)
    return fit(reverb(m.out(), r, t60=0.6, wet=0.22), n_of(0.8))


@snd("common", "art_t18", "art")   # ② 돌진 끊음 "쨍"
def _(r):
    return glass_shatter(r, 0.3, density=50) + chime(3100, 0.3, r, tau=0.1) * 0.5


@snd("common", "art_t19", "art")   # ⑦ 탄 소거 "치익"
def _(r):
    return steam(r, 0.5, attack=0.01, tau=0.16, lo=2500)


# ================================================================== 예고·적·환경(N-01~N-13)

def warn_ping(r, f, dur):
    # 단2도로 어긋난 두 음 = 경고(거슬림)
    return (sine(f, dur) + 0.8 * sine(f * semis(1), dur)) * env_exp(dur, dur * 0.4, 0.004)


@snd("common", "tele_start_circle", "tele")
def _(r):
    d = 0.2
    return warn_ping(r, ramp(d, 600, 900), d) * 0.5 + whoosh(r, d, 1500, 3500, q=1.2, peak_at=0.3)


@snd("common", "tele_start_band", "tele")
def _(r):
    d = 0.2
    b = filt(saw(330, d, bright=16), lo=300, hi=4000) * env_ar(d, 0.01, 0.12, 1.5)
    return b * 0.5 + whoosh(r, d, 2500, 2500, q=0.8, peak_at=0.2) * 0.7


@snd("common", "tele_start_fan", "tele")
def _(r):
    d = 0.2
    return warn_ping(r, ramp(d, 950, 650), d) * 0.5 + whoosh(r, d, 1000, 5000, q=1.4, peak_at=0.6)


@snd("common", "tele_start_aim", "tele")
def _(r):
    d = 0.2
    y = (sine(2400, d) + 0.7 * sine(2400 * semis(1), d)) * env_ar(d, 0.005, 0.15, 1.0) * 0.4
    return y + filt(noise(r, d), lo=6000) * env_exp(d, 0.05) * 0.5


@snd("common", "tele_start_ring", "tele")
def _(r):
    d = 0.2
    t = t_of(d)
    y = sine(700, d) * sine(173, d) * env_exp(d, 0.08, 0.003)
    return y * 0.8 + whoosh(r, d, 3000, 1200, q=1.0, peak_at=0.3) * 0.6


@snd("common", "tele_fill", "tele")
def _(r):
    d = 1.0
    t = t_of(d)
    f = ramp(d, 220, 440)
    y = filt(saw(f, d, bright=12), hi=2400) + 0.6 * sine(f * semis(1) * 2, d)
    y *= (0.55 + 0.45 * np.sin(2 * np.pi * ramp(d, 6, 16, "lin").cumsum() / SR) ** 2)
    y *= env_points(d, [(0, 0.15), (0.97, 1.0), (1.0, 0.0)])
    return y


for _i, _s in enumerate([0.0, 0.5, 1.0]):
    snd("common", "tele_hit_%d" % (_i + 1), "tele")(
        lambda r, s=_s: fit(explosion(r, 0.3, s * 0.6) + thump(140, 50, 0.3, 0.05, click=0.8, r=r) * 0.6, n_of(0.3)))


for _i, _p in enumerate([1.0, 0.85]):
    snd("common", "enemy_shot_%d" % (_i + 1), "enemy")(
        lambda r, p=_p: fm(ramp(0.15, 900 * p, 300 * p), 1.0, env_exp(0.15, 0.04) * 3, 0.15) * env_exp(0.15, 0.05, 0.001)
        + puff(r, 0.15, 1500, 6000, 0.0005, 0.015) * 0.4)


def rustle(r, dur, rate):
    e = env_points(dur, [(0, 0), (dur * 0.15, 1), (dur * 0.6, 0.6), (dur, 0)])
    return grains(r, dur, rate, glen=0.012, lo=1800, hi=8000, amp_env=e) + filt(noise(r, dur), lo=2500, hi=7000) * e * 0.15


snd("common", "enemy_spawn_1", "enemy")(lambda r: rustle(r, 0.4, 160))
snd("common", "enemy_spawn_2", "enemy")(lambda r: rustle(r, 0.4, 260))


def growl(r, f0, dur, formants, rough=0.3, bend=0.85):
    t = t_of(dur)
    f = f0 * (1 + (bend - 1) * t / dur) * (1 + 0.02 * np.sin(2 * np.pi * 5 * t))
    src = saw(f, dur, bright=40) * (1 + rough * filt(noise(r, dur), hi=70, order=1) * 8)
    y = formant(src, formants, [3.5] * len(formants), [1.0, 0.6, 0.35][:len(formants)])
    return y * env_points(dur, [(0, 0), (dur * 0.12, 1), (dur * 0.7, 0.8), (dur, 0)])


def low_drum(r, f0=60.0, dur=0.8, gain=1.0):
    y = thump(f0 * 1.8, f0, dur, dur * 0.3, click=0.3, r=r)
    y += filt(noise(r, dur), hi=500) * env_exp(dur, 0.05) * 0.4
    return y * gain


BOSS_VOICE = [  # (울음 기본음, 포먼트) — 보스 6종 순서: 화덕 멧돼지, 허상, 포자 여왕, 폭풍 부엉이, 서리 염소, 밤 뱀
    (95, [500, 1100, 2400]), (160, [700, 1800, 3200]), (120, [400, 900, 2000]),
    (210, [900, 2000, 3500]), (140, [600, 1300, 2600]), (80, [350, 800, 1900]),
]
for _i, (_f, _fm) in enumerate(BOSS_VOICE):
    def _boss(r, f=_f, fm_=_fm, i=_i):
        m = Mix()
        m.add(low_drum(r, 55, 0.9), gain=1.0)
        m.add(low_drum(r, 50, 0.9), at=0.35, gain=0.8)
        m.add(growl(r, f, 1.0, fm_, rough=0.4 + 0.05 * i), at=0.25, gain=0.7)
        return fit(reverb(m.out(), r, t60=0.8, wet=0.25, damp=3000), n_of(1.5))
    snd("common", "boss_intro_%d" % (_i + 1), "boss")(_boss)


@snd("common", "rage", "boss")
def _(r):
    m = Mix()
    m.add(low_drum(r, 60, 0.5))
    m.add(steam(r, 0.45, attack=0.03, tau=0.15, lo=1500, hi=9000), at=0.05, gain=0.5)
    return fit(m.out(), n_of(0.5))


@snd("common", "final_rage", "boss")
def _(r):
    m = Mix()
    for i in range(4):
        m.add(low_drum(r, 55, 0.5), at=i * 0.3, gain=0.7 + 0.1 * i)
    m.add(growl(r, 110, 1.3, [450, 1000, 2300], rough=0.6, bend=1.2), at=0.1, gain=0.9)
    return fit(reverb(drive(m.out(), 1.4), r, t60=0.8, wet=0.25, damp=3000), n_of(1.5))


@snd("common", "tod_crow", "ambient")
def _(r):
    m = Mix()
    for i, a in enumerate((0.0, 0.42)):
        d = 0.3
        g = growl(r, 520 - 30 * i, d, [1300, 2500, 3600], rough=0.9, bend=0.75)
        m.add(filt(g, lo=600), at=a, gain=1.0 - 0.3 * i)
    return fit(reverb(m.out(), r, t60=0.9, wet=0.35, damp=4000), n_of(1.0))


@snd("common", "tod_insects", "ambient")
def _(r):
    d = 1.0
    m = Mix()
    for k in range(3):
        f = r.uniform(4200, 6200)
        rate = r.uniform(28, 45)
        t = t_of(d)
        gate = (np.sin(2 * np.pi * rate * t) > 0.2).astype(float)
        gate = filt(gate, hi=400, order=1)
        chirp = (np.sin(2 * np.pi * 1.6 * t + k) > -0.2).astype(float)
        m.add(sine(f, d) * gate * filt(chirp, hi=20, order=1), gain=0.3)
    y = m.out() * env_points(d, [(0, 0), (0.2, 1), (0.8, 1), (1.0, 0)])
    return y


@snd("common", "tod_bird", "ambient")
def _(r):
    m = Mix()
    at = 0.0
    for i in range(5):
        d = r.uniform(0.07, 0.12)
        f = ramp(d, r.uniform(2800, 3600), r.uniform(4200, 5200)) if i % 2 == 0 else ramp(d, 5000, 3000)
        m.add(sine(f, d) * env_ar(d, 0.01, 0.04, 1), at=at, gain=0.5)
        at += d + r.uniform(0.03, 0.09)
    return fit(reverb(m.out(), r, t60=0.6, wet=0.3), n_of(1.0))


@snd("common", "phantom_1", "boss")   # N-11 등장의 유리 울림
def _(r):
    m = Mix()
    for i, f in enumerate((880, 1318, 1975)):
        y = sine(f, 1.0) * env_ar(1.0, 0.25, 0.6, 1) * (1 + 0.1 * np.sin(2 * np.pi * (3 + i) * t_of(1.0)))
        m.add(y, gain=0.25)
    m.add(chime(1760, 1.0, r, tau=0.4), at=0.05, gain=0.3)
    return fit(reverb(m.out(), r, t60=1.0, wet=0.35), n_of(1.2))


@snd("common", "phantom_2", "boss")   # 거울 금 + 거꾸로 감기
def _(r):
    m = Mix()
    crack = glass_shatter(r, 0.3, density=30, lo=1800, hi=7000)
    m.add(crack)
    rev = reverb(chime(1500, 0.6, r, tau=0.3) + whoosh(r, 0.6, 1000, 4000), r, t60=0.5, wet=0.3)[::-1]
    m.add(fit(rev, n_of(0.7)) * env_points(0.7, [(0, 0), (0.6, 1), (0.7, 0)]), at=0.2, gain=0.6)
    return fit(m.out(), n_of(1.0))


@snd("common", "phantom_3", "boss")   # 되비침: 올라가는 울림 + 깨짐
def _(r):
    m = Mix()
    d = 0.8
    m.add(sine(ramp(d, 500, 1500), d) * env_ar(d, 0.7, 0.05, 1), gain=0.35)
    m.add(sweep_bp(noise(r, d), ramp(d, 800, 6000), q=2.0) * env_ar(d, 0.7, 0.05, 1), gain=0.5)
    m.add(glass_shatter(r, 0.5, density=80), at=0.78, gain=0.9)
    return fit(reverb(m.out(), r, t60=0.6, wet=0.25), n_of(1.3))


@snd("common", "revenge_1", "boss")   # 큰 유리 깨짐
def _(r):
    m = Mix()
    m.add(thump(120, 40, 0.5, 0.1, click=1.0, r=r))
    m.add(glass_shatter(r, 0.9, density=90, lo=1800, hi=11000, tau=0.12), gain=1.0)
    return fit(reverb(m.out(), r, t60=0.7, wet=0.25), n_of(1.0))


@snd("common", "revenge_2", "boss")   # 맑은 한 음
def _(r):
    f = note_hz("A5")
    return fit(reverb(bell(f, 1.0, r, tau=0.6, bright=0.8), r, t60=0.9, wet=0.3), n_of(1.0))


# ---- 적 종별 울음(동작 직전, 예고 시작과 같은 때) — monsters.md 성격대로

@snd("common", "cry_slime", "cry")
def _(r):
    m = Mix()
    for i in range(3):
        m.add(bubble(r.uniform(150, 260), 0.1, r, rise=1.6), at=i * 0.07, gain=0.7)
    m.add(filt(noise(r, 0.3), lo=200, hi=1500) * env_ar(0.3, 0.05, 0.2), gain=0.3)
    return fit(m.out(), n_of(0.3))


@snd("common", "cry_wolf", "cry")
def _(r):
    return growl(r, 130, 0.4, [500, 1200, 2500], rough=0.7, bend=1.05)


@snd("common", "cry_golem", "cry")
def _(r):
    d = 0.45
    y = grains(r, d, 120, glen=0.02, lo=150, hi=1500, amp_env=env_ar(d, 0.1, 0.2, 1))
    return y + filt(saw(55, d, bright=20), hi=400) * env_ar(d, 0.1, 0.25, 1) * 0.3


@snd("common", "cry_shaman", "cry")
def _(r):
    m = Mix()
    for i in range(3):
        m.add(puff(r, 0.15, 400, 2500, 0.01, 0.04), at=i * 0.09, gain=0.6)
    m.add(growl(r, 260, 0.35, [700, 1600], rough=0.2, bend=1.1), gain=0.3)
    return fit(m.out(), n_of(0.4))


@snd("common", "cry_bug", "cry")
def _(r):
    d = 0.35
    return steam(r, d, attack=0.05, tau=0.2, lo=4000) * 0.6 + crackle(r, d, 300, lo=3000, hi=10000, amp_env=env_ar(d, 0.05, 0.2, 1))


@snd("common", "cry_hog", "cry")
def _(r):
    d = 0.3
    return growl(r, 190, d, [600, 1500, 2800], rough=0.9, bend=1.25) * 0.8 + grains(r, d, 200, glen=0.004, lo=3000, hi=9000) * 0.4


@snd("common", "cry_weasel", "cry")
def _(r):
    m = Mix()
    for i in range(2):
        d = 0.08
        m.add(sine(ramp(d, 1800, 2500), d) * env_ar(d, 0.005, 0.05, 1), at=i * 0.1, gain=0.4)
    m.add(grains(r, 0.3, 180, glen=0.01, lo=500, hi=3000), gain=0.6)
    return fit(m.out(), n_of(0.3))


@snd("common", "cry_bat", "cry")
def _(r):
    m = Mix()
    for i in range(4):
        m.add(puff(r, 0.05, 300, 1800, 0.008, 0.015), at=i * 0.065, gain=0.8)
    m.add(sine(ramp(0.06, 4200, 3000), 0.06) * env_ar(0.06, 0.005, 0.04), at=0.05, gain=0.25)
    return fit(m.out(), n_of(0.3))


@snd("common", "cry_wisp", "cry")
def _(r):
    d = 0.45
    t = t_of(d)
    y = sine(1100 * (1 + 0.04 * np.sin(2 * np.pi * 7 * t)), d) * env_ar(d, 0.15, 0.25, 1) * 0.4
    return y + sweep_bp(noise(r, d), ramp(d, 3000, 1500), q=2) * env_ar(d, 0.1, 0.3, 1) * 0.6


@snd("common", "cry_vine", "cry")
def _(r):
    d = 0.4
    t = t_of(d)
    creak = saw(70 + 30 * np.sin(2 * np.pi * 3 * t), d, bright=30)
    creak = formant(creak, [800, 1700], [4, 4], [1, 0.5]) * env_ar(d, 0.05, 0.2, 1)
    return creak + grains(r, d, 120, glen=0.006, lo=2500, hi=8000) * 0.3


@snd("common", "cry_boar", "cry")
def _(r):
    m = Mix()
    m.add(growl(r, 150, 0.25, [550, 1250, 2600], rough=1.0, bend=0.8), gain=0.8)
    for i in range(2):
        m.add(grains(r, 0.12, 300, glen=0.01, lo=400, hi=3000), at=0.22 + i * 0.12, gain=0.6)
    return fit(m.out(), n_of(0.5))


@snd("common", "cry_toad", "cry")
def _(r):
    m = Mix()
    for i in range(2):
        d = 0.14
        m.add(growl(r, 95, d, [350, 900], rough=0.3, bend=1.0) * (1 + 0.6 * np.sin(2 * np.pi * 45 * t_of(d))), at=i * 0.18)
    return fit(m.out(), n_of(0.4))


# ---- 줍기·웨이브

@snd("common", "drop_gold", "drop")
def _(r):
    m = Mix()
    m.add(coin(r, 2600, 0.2))
    m.add(coin(r, 3300, 0.15), at=0.05, gain=0.6)
    return fit(m.out(), n_of(0.25))


@snd("common", "drop_shard", "drop")
def _(r):
    m = Mix()
    m.add(chime(note_hz("E6"), 0.3, r, tau=0.12))
    m.add(chime(note_hz("B6"), 0.25, r, tau=0.1), at=0.06, gain=0.6)
    m.add(sweep_bp(noise(r, 0.3), ramp(0.3, 2000, 7000), q=2) * env_ar(0.3, 0.1, 0.15, 1), gain=0.25)
    return fit(m.out(), n_of(0.35))


@snd("common", "drop_bundle", "drop")
def _(r):
    m = Mix()
    m.add(thump(160, 80, 0.15, 0.03), gain=0.6)
    m.add(rustle(r, 0.25, 200), gain=0.8)
    return fit(m.out(), n_of(0.3))


@snd("common", "wave_start", "wave")
def _(r):
    m = Mix()
    m.add(low_drum(r, 60, 0.6))
    m.add(low_drum(r, 65, 0.6), at=0.2, gain=0.7)
    horn = tone(note_hz("D4"), 0.6, "saw", attack=0.08, release=0.3)
    m.add(filt(horn, hi=1800) + filt(tone(note_hz("A4"), 0.6, "saw", attack=0.08, release=0.3), hi=1800) * 0.6, at=0.2, gain=0.25)
    return fit(reverb(m.out(), r, t60=0.6, wet=0.25), n_of(0.9))


@snd("common", "wave_clear", "wave")
def _(r):
    m = Mix()
    for i, nme in enumerate(("G5", "C6", "E6", "G6")):
        m.add(bright_pluck(note_hz(nme), 0.6, r), at=i * 0.08, gain=0.35)
    m.add(chime(note_hz("C7"), 0.6, r, tau=0.3), at=0.32, gain=0.3)
    return fit(reverb(m.out(), r, t60=0.6, wet=0.25), n_of(1.0))


# ================================================================== 강화 공용(E-01~E-36)

@snd("common", "enh_open", "enh")
def _(r):
    m = Mix()
    d = 0.3
    m.add(bell(note_hz("C4"), d, r, tau=0.3, bright=0.6), gain=0.5)
    m.add(sweep_bp(noise(r, d), ramp(d, 500, 3000), q=1.5) * env_ar(d, 0.05, 0.2, 1), gain=0.5)
    return fit(reverb(m.out(), r, t60=0.5, wet=0.3), n_of(0.3))


@snd("common", "enh_sacrifice_in", "enh_small")
def _(r):
    return wood_tap(r, 1100, 0.1, 1.3) + coin(r, 2200, 0.1) * 0.3


@snd("common", "enh_sacrifice_out", "enh_small")
def _(r):
    return wood_tap(r, 800, 0.1, 1.0) + puff(r, 0.1, 1500, 6000, 0.002, 0.02) * 0.3


@snd("common", "enh_prob", "enh_small")
def _(r):
    m = Mix()
    for i in range(6):
        m.add(wood_tap(r, 2000 + 60 * i, 0.025, 1.2), at=i * 0.035, gain=0.6 + 0.06 * i)
    return fit(m.out(), n_of(0.25))


@snd("common", "enh_charm", "enh_small")
def _(r):
    d = 0.2
    y = filt(noise(r, d), lo=1500, hi=7000) * env_points(d, [(0, 0), (0.02, 1), (0.08, 0.3), (d, 0)])
    return y + thump(250, 160, d, 0.02) * 0.4


@snd("common", "enh_press", "enh")
def _(r):
    m = Mix()
    m.add(thump(110, 55, 0.3, 0.07, click=0.5, r=r))
    m.add(wood_tap(r, 350, 0.1, 0.8), gain=0.6)
    return fit(m.out(), n_of(0.3))


@snd("common", "enh_pay", "enh_small")
def _(r):
    m = Mix()
    for i in range(4):
        m.add(coin(r, r.uniform(2400, 3400), 0.12), at=i * 0.035, gain=0.5)
    m.add(thump(200, 110, 0.12, 0.03), at=0.17, gain=0.7)     # 돌 놓임
    m.add(wood_tap(r, 700, 0.06), at=0.17, gain=0.5)
    return fit(m.out(), n_of(0.3))


@snd("common", "enh_offer", "enh")
def _(r):
    return fit(reverb(bell(note_hz("C5"), 0.3, r, tau=0.25, bright=0.75), r, t60=0.4, wet=0.2), n_of(0.3))


@snd("common", "enh_heartbeat", "enh")
def _(r):
    m = Mix()
    m.add(thump(75, 45, 0.25, 0.06), gain=1.0)
    m.add(thump(68, 42, 0.25, 0.05), at=0.18, gain=0.7)
    return filt(fit(m.out(), n_of(0.5)), hi=500)


@snd("common", "enh_success_common", "enh_big")
def _(r):
    m = Mix()
    for i, nme in enumerate(("C5", "E5", "G5", "C6")):
        m.add(bright_pluck(note_hz(nme), 0.6, r), at=0.01 * i, gain=0.3)
    m.add(bell(note_hz("C6"), 0.6, r, tau=0.4), at=0.02, gain=0.45)
    m.add(thump(120, 45, 0.3, 0.07, click=0.4, r=r), gain=0.6)
    return fit(reverb(m.out(), r, t60=0.6, wet=0.25), n_of(0.6))


def slam(r, up=0):
    m = Mix()
    m.add(thump(100 * semis(up), 40, 0.3, 0.08, click=1.0, r=r))
    m.add(filt(noise(r, 0.3), lo=200, hi=2500) * env_exp(0.3, 0.03, 0.0003), gain=0.7)
    m.add(wood_tap(r, 260 * semis(up), 0.1, 0.8), gain=0.6)
    return fit(drive(m.out(), 1.8), n_of(0.3))


snd("common", "enh_slam_1", "enh")(lambda r: slam(r, 0))
snd("common", "enh_slam_2", "enh")(lambda r: slam(r, 4))   # 대성공 두 번째 = 3도 위


@snd("common", "enh_fail_start", "enh")
def _(r):
    # 성공·실패를 드러내지 않는 긴장 들숨: 오르는 숨 + 낮은 심장 한 번 + 가는 금
    d = 0.4
    m = Mix()
    m.add(sweep_bp(noise(r, d), ramp(d, 600, 2500), q=1.5) * env_ar(d, 0.32, 0.06, 1), gain=0.6)
    m.add(thump(70, 45, 0.25, 0.06), at=0.12, gain=0.8)
    m.add(crackle(r, 0.25, 60, lo=2000, hi=7000, decay=0.002), at=0.1, gain=0.3)
    return fit(m.out(), n_of(d))


@snd("common", "enh_stage_down", "enh")
def _(r):
    m = Mix()
    for i, nme in enumerate(("E5", "C5")):
        m.add(tone(note_hz(nme), 0.28, "tri", attack=0.005, tau=0.12), at=i * 0.17, gain=0.7)
    return fit(reverb(m.out(), r, t60=0.4, wet=0.2), n_of(0.5))


for _i, _n in enumerate(("C5", "D5", "E5")):
    snd("common", "enh_ember_%d" % (_i + 1), "enh")(
        lambda r, n=_n: fit(tone(note_hz(n), 0.3, "sine", attack=0.01, tau=0.12) + crackle(r, 0.3, 40, amp_env=env_exp(0.3, 0.1)) * 0.3, n_of(0.3)))


for _i in range(3):
    def _spread(r, i=_i):
        d = 0.3
        c = ramp(d, 2500 * semis(4 * i), 6000 * semis(4 * i))
        y = sweep_bp(noise(r, d), c, q=3.0) * env_ar(d, 0.05, 0.2, 1)
        y += chime(note_hz("G6") * semis(4 * i), d, r, tau=0.1) * 0.3
        return y
    snd("common", "enh_gold_spread_%d" % (_i + 1), "enh")(_spread)


@snd("common", "enh_great_burst", "enh_big")
def _(r):
    m = Mix()
    m.add(glass_shatter(r, 0.6, density=90, tau=0.1), gain=0.8)
    m.add(thump(100, 35, 0.6, 0.15, click=1.0, r=r))
    for i, nme in enumerate(("D5", "F#5", "A5", "D6", "F#6")):
        m.add(bell(note_hz(nme), 0.7, r, tau=0.4, bright=0.8), at=0.05 + 0.02 * i, gain=0.22)
    return fit(reverb(m.out(), r, t60=0.7, wet=0.25), n_of(0.8))


@snd("common", "enh_charm_catch", "enh")
def _(r):
    d = 0.3
    y = filt(noise(r, d), lo=800, hi=6000) * (1 + 0.8 * np.sin(2 * np.pi * 26 * t_of(d))) * env_ar(d, 0.02, 0.2, 1)
    return y


@snd("common", "enh_charm_burn", "enh")
def _(r):
    d = 0.4
    return crackle(r, d, 350, lo=1500, hi=8000, amp_env=env_ar(d, 0.05, 0.25, 1)) + filt(noise(r, d), lo=2000) * env_ar(d, 0.05, 0.3, 1) * 0.2


@snd("common", "enh_no_drop", "enh")
def _(r):
    return S(steam(r, 0.15, attack=0.002, tau=0.03, lo=2000), wood_tap(r, 600, 0.05) * 0.4)


@snd("common", "enh_wand_drop", "enh")
def _(r):
    m = Mix()
    m.add(wood_tap(r, 420, 0.1), gain=1.0)
    m.add(wood_tap(r, 480, 0.08), at=0.12, gain=0.6)
    m.add(wood_tap(r, 450, 0.06), at=0.2, gain=0.3)
    m.add(thump(140, 70, 0.15, 0.03), gain=0.5)
    return fit(m.out(), n_of(0.35))


@snd("common", "enh_slot_extend", "enh")
def _(r):
    m = Mix()
    m.add(grains(r, 0.3, 150, glen=0.015, lo=200, hi=2500, amp_env=env_ar(0.3, 0.02, 0.2, 1)))
    m.add(chime(note_hz("A6"), 0.5, r, tau=0.25), at=0.2, gain=0.5)
    return fit(reverb(m.out(), r, t60=0.4, wet=0.2), n_of(0.7))


@snd("common", "enh_mutation_card", "enh")
def _(r):
    m = Mix()
    m.add(thump(65, 42, 0.3, 0.08), gain=1.0)                     # 분당 60 = 1초에 한 번
    m.add(thump(60, 40, 0.3, 0.07), at=0.2, gain=0.6)
    t = t_of(1.0)
    wild = fm(110, 1.5, 2 + np.sin(2 * np.pi * 0.7 * t), 1.0) * env_ar(1.0, 0.3, 0.4, 1)
    m.add(filt(wild, hi=1500), gain=0.25)
    return fit(m.out(), n_of(1.0))


@snd("common", "enh_mutation_choice_1", "enh")   # 받음: 스며드는 소리
def _(r):
    d = 0.3
    y = sweep_bp(noise(r, d), ramp(d, 3000, 500), q=2) * env_ar(d, 0.05, 0.2, 1)
    return y + sine(ramp(d, 400, 200), d) * env_ar(d, 0.05, 0.2) * 0.3


@snd("common", "enh_mutation_choice_2", "enh")   # 거절: 마른 잎 부서짐
def _(r):
    return grains(r, 0.3, 300, glen=0.005, lo=2000, hi=9000, amp_env=env_exp(0.3, 0.08))


def brass(f, dur, attack=0.03, release=0.15):
    y = saw(f, dur, bright=18)
    t = t_of(dur)
    cut = 900 + 2500 * np.minimum(1, t / max(attack * 3, 1e-3)) * np.exp(-t / 0.6)
    y = sweep(y, hi_track=cut, order=2)
    return y * env_points(dur, [(0, 0), (attack, 1), (max(attack, dur - release), 0.8), (dur, 0)])


@snd("common", "enh_fanfare", "enh_big")
def _(r):
    m = Mix()
    seq = [("G4", 0.0, 0.18), ("C5", 0.2, 0.18), ("E5", 0.4, 0.18), ("G5", 0.6, 0.75)]
    for nme, at, d in seq:
        f = note_hz(nme)
        m.add(brass(f, d) + 0.5 * brass(f * 0.5, d), at=at, gain=0.35)
    m.add(chord([note_hz("C5"), note_hz("E5")], 0.75, lambda f, d: brass(f, d)), at=0.6, gain=0.4)
    m.add(low_drum(r, 70, 0.5), at=0.6, gain=0.4)
    return fit(reverb(m.out(), r, t60=0.6, wet=0.22), n_of(1.5))


@snd("common", "enh_title_band", "enh")
def _(r):
    d = 0.8
    t = t_of(d)
    flap = 0.5 + 0.5 * np.abs(np.sin(2 * np.pi * ramp(d, 9, 4, "lin").cumsum() / SR))
    y = filt(noise(r, d), lo=400, hi=5000) * flap * env_ar(d, 0.05, 0.4, 1)
    return y


@snd("common", "enh_plus13_bell", "enh_big")
def _(r):
    m = Mix()
    m.add(bell(note_hz("C3"), 2.0, r, tau=1.4, bright=0.9), gain=0.8)
    m.add(bell(note_hz("C4"), 2.0, r, tau=1.0, bright=0.7), gain=0.35)
    m.add(thump(80, 40, 0.5, 0.15, click=0.3, r=r), gain=0.4)
    return fit(reverb(m.out(), r, t60=1.6, wet=0.3), n_of(2.0))


# ---- +13 속성 주제 선율(E-30, 4마디) — 1 화염, 2 번개, 3 비전, 4 냉기, 5 암흑
THEMES = {
    1: dict(scale=["D4", "E4", "F4", "G4", "A4", "C5", "D5"], tempo=0.3,
            notes=[(0, 1), (2, 1), (4, 2), (5, 1), (4, 1), (2, 2), (3, 1), (4, 1), (6, 2), (4, 2), (6, 4)], bass="D3"),
    2: dict(scale=["E5", "F#5", "G#5", "B5", "C#6", "E6"], tempo=0.25,
            notes=[(0, 1), (3, 1), (5, 1), (3, 1), (4, 2), (2, 2), (0, 1), (2, 1), (4, 1), (5, 1), (3, 4)], bass="E3"),
    3: dict(scale=["C5", "D5", "E5", "G5", "A5", "C6"], tempo=0.25,
            notes=[(0, 1), (1, 1), (2, 1), (4, 1), (3, 2), (2, 2), (4, 1), (5, 1), (3, 1), (1, 1), (0, 4)], bass="C3"),
    4: dict(scale=["A5", "B5", "C6", "E6", "F6", "A6"], tempo=0.3,
            notes=[(5, 2), (3, 1), (2, 1), (1, 2), (0, 2), (2, 1), (3, 1), (4, 2), (3, 4)], bass="A2"),
    5: dict(scale=["C4", "Eb4", "F4", "G4", "Bb4", "C5"], tempo=0.33,
            notes=[(0, 2), (1, 1), (3, 1), (2, 2), (1, 2), (0, 1), (4, 1), (3, 2), (5, 4)], bass="C2"),
}


def theme_inst(k, f, d, r):
    if k == 1:   # 모루 종 + 불티
        return metal(f, d, r, tau=0.35, ring=1.6) * 0.5 + bell(f, d, r, tau=0.5, bright=0.6) * 0.5
    if k == 2:   # 유리 종
        return chime(f, d, r, tau=0.45)
    if k == 3:   # 실로폰
        return modal([f, f * 3.93, f * 9.8], [1, 0.35, 0.1], [0.18, 0.06, 0.02], d, r)
    if k == 4:   # 유리 하프
        return modal([f, f * 2, f * 3, f * 4], [1, 0.4, 0.2, 0.1], [0.6, 0.35, 0.2, 0.1], d, r, attack=0.004)
    if k == 5:   # 낮은 종 + 합창
        t = t_of(d)
        choir = formant(saw(f * (1 + 0.004 * np.sin(2 * np.pi * 5 * t)), d, bright=24), [600, 1000, 2400], [3, 3, 3], [1, 0.5, 0.25])
        return bell(f, d, r, tau=0.7, bright=0.5) * 0.6 + choir * env_ar(d, 0.15, 0.3, 1) * 0.6
    raise ValueError(k)


for _k in range(1, 6):
    def _theme(r, k=_k):
        th = THEMES[k]
        beat = th["tempo"]
        m = Mix()
        at = 0.0
        for idx, ln in th["notes"]:
            f = note_hz(th["scale"][idx])
            d = beat * ln + 0.6
            m.add(theme_inst(k, f, d, r), at=at, gain=0.45)
            at += beat * ln
        total = at
        fb = note_hz(th["bass"])
        pad = filt(saw(fb, total + 0.4, bright=10), hi=700) * env_ar(total + 0.4, 0.4, 0.8, 1)
        m.add(pad, gain=0.18)
        if k == 1:
            m.add(crackle(r, total, 25, amp_env=env_ar(total, 0.5, 0.5, 1)), gain=0.15)
        y = reverb(m.out(), r, t60=1.2, wet=0.3)
        return fit(y, n_of(min(6.0, total + 1.2)))
    snd("common", "enh_plus13_theme_%d" % _k, "theme")(_theme)


@snd("common", "enh_quick_1", "enh")   # 빠른 강화: 성공 한 음
def _(r):
    return fit(S(bell(note_hz("G5"), 0.3, r, tau=0.22, bright=0.8), thump(120, 60, 0.2, 0.04) * 0.4), n_of(0.3))


@snd("common", "enh_quick_2", "enh")   # 실패 한 음
def _(r):
    return fit(S(tone(note_hz("C4"), 0.3, "tri", attack=0.005, tau=0.1), dull_thunk(r, 130, 0.2) * 0.5), n_of(0.3))


@snd("common", "enh_quick_3", "enh")   # 대성공 두 음
def _(r):
    m = Mix()
    m.add(bell(note_hz("G5"), 0.2, r, tau=0.15), gain=0.7)
    m.add(bell(note_hz("D6"), 0.25, r, tau=0.18), at=0.09, gain=0.7)
    return fit(m.out(), n_of(0.3))


@snd("common", "enh_quick_4", "enh")   # 변이 한 음
def _(r):
    return fit(fm(note_hz("A4"), 1.41, env_exp(0.3, 0.15) * 3, 0.3) * env_exp(0.3, 0.12, 0.003), n_of(0.3))


@snd("common", "enh_trace", "enh_small")
def _(r):
    d = 0.15
    return filt(noise(r, d), lo=500, hi=4000) * env_ar(d, 0.03, 0.1, 1.5)


# ---- 캐릭터 소리 표식(E-36): 제자 N = 2N−1(밝은 표식), 2N(낮은 표식)
def card_mark(r, k):
    ch, kind = (k - 1) // 2 + 1, (k - 1) % 2
    d = 0.3
    if ch == 1:
        return metal(1500 if kind == 0 else 800, d, r, tau=0.06) * 0.6 + steam(r, d, 0.01, 0.06) * (0.3 if kind == 0 else 0.6)
    if ch == 2:
        return chime(2400 if kind == 0 else 1400, d, r, tau=0.08) + crackle(r, d, 200 if kind == 0 else 60, amp_env=env_exp(d, 0.05)) * 0.6
    if ch == 3:
        return page_flip(r, 0.15, kind == 0) if kind == 0 else fit(slam(r, 0) * 0.7, n_of(d))
    if ch == 4:
        return S(chime(3000 if kind == 0 else 1700, d, r, tau=0.1), glass_shatter(r, 0.2, 30) * (0.4 if kind else 0.0))
    return S(bell(note_hz("G4") if kind == 0 else note_hz("C4"), d, r, tau=0.2, bright=0.5), bubble(400, 0.1, r) * (0.4 if kind else 0.0))


for _k in range(1, 11):
    snd("common", "enh_card_mark_%d" % _k, "enh_small")(lambda r, k=_k: fit(card_mark(r, k), n_of(0.3)))


# ================================================================== 제자 1(char.1): 화염 · 화덕 · 풀무 · 모루

def iron(r, f0, dur, tau):
    """둔한 무쇠(텅): 낮은 비조화 부분음 + 둔탁한 노이즈."""
    y = modal([f0, f0 * 1.58, f0 * 2.37, f0 * 3.3], [1, 0.6, 0.35, 0.2], [tau, tau * 0.7, tau * 0.45, tau * 0.3], dur, r)
    return y + filt(noise(r, dur), lo=300, hi=3000) * env_exp(dur, 0.008, 0.0004) * 0.6


def flame(r, dur, lo=500, hi=4000, attack=0.01, tau=0.1, roar=0.5):
    """불이 화악: 띠 노이즈(밝기가 내려간다) + 불티."""
    x = sweep(noise(r, dur), lo_track=np.full(n_of(dur), float(lo)), hi_track=ramp(dur, hi, hi * 0.35), order=2)
    e = env_exp(dur, tau, attack)
    y = x * e + filt(noise(r, dur), hi=400) * e * roar
    return y + crackle(r, dur, 200, lo=2500, hi=10000, amp_env=e) * 0.5


@snd("char.1", "fire_clank", "char_fire")          # +0 "텅"
def _(r):
    return iron(r, 330, 0.12, 0.04) + thump(140, 70, 0.12, 0.03) * 0.5


@snd("char.1", "fire_clank_hiss", "char_fire")     # +1~+2 "텅-칙"
def _(r):
    m = Mix()
    m.add(iron(r, 360, 0.12, 0.04) + thump(140, 70, 0.12, 0.03) * 0.5)
    m.add(steam(r, 0.08, 0.003, 0.025, lo=3500), at=0.06, gain=0.5)
    return fit(m.out(), n_of(0.18))


@snd("char.1", "fire_flare", "char_fire")          # +3 "화륵"
def _(r):
    m = Mix()
    m.add(flame(r, 0.2, 600, 5000, 0.01, 0.06, roar=0.3))
    m.add(iron(r, 400, 0.1, 0.03), gain=0.4)
    return fit(m.out(), n_of(0.2))


@snd("char.1", "fire_whoosh", "char_fire")         # +4~+6 "화악"
def _(r):
    m = Mix()
    m.add(flame(r, 0.25, 350, 4500, 0.015, 0.09, roar=0.6))
    m.add(thump(120, 55, 0.2, 0.05), gain=0.6)
    return drive(fit(m.out(), n_of(0.25)), 1.4)


@snd("char.1", "fire_thud", "char_fire")           # +7~+9 "퉁"(묵직한 쇳물)
def _(r):
    m = Mix()
    m.add(thump(95, 42, 0.3, 0.08, click=0.6, r=r))
    m.add(iron(r, 190, 0.25, 0.06), gain=0.5)
    m.add(flame(r, 0.25, 300, 3000, 0.02, 0.08), at=0.02, gain=0.6)
    return drive(fit(m.out(), n_of(0.3)), 1.5)


@snd("char.1", "fire_boom", "char_fire")           # +10~+12 "쿠웅"
def _(r):
    m = Mix()
    m.add(thump(80, 32, 0.4, 0.12, click=0.6, r=r))
    m.add(flame(r, 0.35, 250, 2500, 0.02, 0.12, roar=0.9), gain=0.8)
    m.add(wood_tap(r, 900, 0.05), gain=0.3)   # 뚜껑 "달각"
    return fit(reverb(drive(m.out(), 1.6), r, t60=0.3, wet=0.15), n_of(0.4))


@snd("char.1", "fire_boom_bell", "char_fire")      # +13 "쿠웅" + 맑은 종 한 음
def _(r):
    m = Mix()
    m.add(thump(78, 30, 0.45, 0.13, click=0.6, r=r))
    m.add(flame(r, 0.35, 250, 2500, 0.02, 0.12, roar=0.9), gain=0.7)
    m.add(bell(note_hz("G6"), 0.5, r, tau=0.3, bright=0.8), at=0.03, gain=0.35)
    return fit(reverb(drive(m.out(), 1.4), r, t60=0.4, wet=0.18), n_of(0.6))


@snd("char.1", "hit_ember", "hit")
def _(r):
    m = Mix()
    m.add(hit_punch(r, 1.0, 0.15))
    m.add(crackle(r, 0.15, 400, lo=2500, hi=10000, amp_env=env_exp(0.15, 0.05)), gain=0.6)
    return fit(m.out(), n_of(0.15))


@snd("char.1", "hit_molten", "hit")
def _(r):
    m = Mix()
    m.add(hit_punch(r, 0.85, 0.22))
    m.add(flame(r, 0.2, 400, 4000, 0.003, 0.05, roar=0.5), gain=0.6)
    m.add(steam(r, 0.15, 0.01, 0.05, lo=4000), at=0.05, gain=0.3)
    return fit(m.out(), n_of(0.22))


@snd("char.1", "hit_anvil", "hit")                 # 약점·임계 적중 "깡"
def _(r):
    return fit(metal(1150, 0.3, r, tau=0.12, ring=1.5) + hit_punch(r, 0.9, 0.3) * 0.6, n_of(0.3))


@snd("char.1", "crit_anvil", "heat")               # C-05 임계 사격 망치 "깡"
def _(r):
    return fit(metal(1350, 0.3, r, tau=0.1, ring=1.4) + crackle(r, 0.3, 200, amp_env=env_exp(0.3, 0.08)) * 0.4, n_of(0.3))


@snd("char.1", "lock_hiss", "heat")                # C-07 잠금 "철컥" + 큰 김
def _(r):
    m = Mix()
    m.add(metal(600, 0.12, r, tau=0.03), gain=0.7)
    m.add(steam(r, 0.6, attack=0.01, tau=0.2, lo=2000), at=0.04, gain=1.0)
    return fit(m.out(), n_of(0.6))


@snd("char.1", "boilover_burst", "art")            # C-20 끓어넘침 증기 터짐
def _(r):
    m = Mix()
    m.add(thump(90, 40, 0.4, 0.1, click=0.4, r=r), gain=0.7)
    m.add(steam(r, 0.6, attack=0.005, tau=0.18, lo=1200, hi=10000))
    for i in range(5):
        m.add(bubble(r.uniform(200, 400), 0.08, r, rise=2.0), at=r.uniform(0, 0.25), gain=0.25)
    return fit(m.out(), n_of(0.6))


@snd("char.1", "kiln_place", "art")                # T-11 불가마 까는 소리
def _(r):
    m = Mix()
    m.add(thump(85, 35, 0.4, 0.1, click=0.8, r=r))
    m.add(grains(r, 0.2, 200, glen=0.012, lo=300, hi=3000, amp_env=env_exp(0.2, 0.06)), gain=0.6)   # 벽돌
    m.add(flame(r, 0.45, 200, 3000, 0.06, 0.2, roar=1.0), at=0.05, gain=0.9)                         # 화악 붙음
    return fit(reverb(drive(m.out(), 1.4), r, t60=0.4, wet=0.18), n_of(0.5))


@snd("char.1", "kiln_hum", "art")                  # T-11 이어지는 가마 울림
def _(r):
    d = 1.0
    t = t_of(d)
    roar = filt(noise(r, d), lo=60, hi=500) * (1 + 0.3 * np.sin(2 * np.pi * 3.5 * t))
    hum = sine(70, d) * 0.4 + sine(140, d) * 0.15
    e = env_points(d, [(0, 0), (0.15, 1), (0.6, 0.8), (d, 0)])
    return (roar + hum) * e + crackle(r, d, 60, amp_env=e) * 0.4


@snd("char.1", "kiln_end", "art")                  # 가마가 식음
def _(r):
    m = Mix()
    m.add(steam(r, 0.45, 0.02, 0.15, lo=2500), gain=0.7)
    m.add(iron(r, 260, 0.2, 0.05), gain=0.4)
    return fit(m.out(), n_of(0.5))


# ---- 대기 반복음(2초, 이음매 없음). 강화 화면의 지팡이 대기 — 단계 묶음별

def _idle_sizzle(r, d):
    return sizzle(r, d, rate=120) * 0.6


def _idle_burn(r, d):
    t = t_of(d)
    roar = filt(noise(r, d), lo=80, hi=900) * (1 + 0.25 * np.sin(2 * np.pi * 1.5 * t))
    return roar + crackle(r, d, 50, lo=2000, hi=9000) * 0.6


def _bubbles(r, d, rate, f_lo, f_hi):
    m = Mix()
    for _ in range(int(rate * d)):
        m.add(bubble(r.uniform(f_lo, f_hi), r.uniform(0.04, 0.08), r, rise=r.uniform(1.5, 2.5)),
              at=r.uniform(0, d - 0.1), gain=r.uniform(0.3, 1.0))
    return fit(m.out(), n_of(d))


def _idle_bubble(r, d):
    return _bubbles(r, d, 5, 250, 500) + filt(noise(r, d), lo=80, hi=500) * 0.3


def _idle_boil(r, d):
    return _bubbles(r, d, 14, 200, 600) + filt(noise(r, d), lo=80, hi=700) * 0.4


def _idle_kettle(r, d):
    t = t_of(d)
    hum = filt(noise(r, d), lo=100, hi=900) * (0.8 + 0.2 * np.sin(2 * np.pi * 2 * t))
    return hum * 0.7 + sine(150, d) * 0.15 + _bubbles(r, d, 8, 200, 500) * 0.5


def _idle_whistle(r, d):
    t = t_of(d)
    w = sine(1850 * (1 + 0.006 * np.sin(2 * np.pi * 4 * t)), d) * (0.4 + 0.2 * np.sin(2 * np.pi * 0.5 * t))
    return _idle_kettle(r, d) + w * 0.12 + peak_bp(noise(r, d), 1850, 8) * 0.1


def _idle_hum(r, d):
    t = t_of(d)
    return (sine(65, d) * 0.5 + sine(130, d) * 0.2 + sine(195.5, d) * 0.08) * (1 + 0.15 * np.sin(2 * np.pi * 0.5 * t)) \
        + filt(noise(r, d), lo=60, hi=300) * 0.2


IDLE = [("idle_sizzle", _idle_sizzle), ("idle_burn", _idle_burn), ("idle_bubble", _idle_bubble),
        ("idle_boil", _idle_boil), ("idle_kettle", _idle_kettle), ("idle_whistle", _idle_whistle), ("idle_hum", _idle_hum)]
for _nm, _fn in IDLE:
    snd("char.1", _nm, "loop", loop=True)(lambda r, fn=_fn: make_loop(fn, r, 2.0))


@snd("char.1", "idle_spark_tick", "enh_small")     # 지팡이 바꿈: 불티 "틱"
def _(r):
    return fit(S(crackle(r, 0.12, 30, lo=2500, hi=10000), wood_tap(r, 2200, 0.05) * 0.6), n_of(0.12))


@snd("char.1", "idle_lid_clack", "enh_small")      # 뚜껑 "달각"
def _(r):
    m = Mix()
    m.add(metal(1700, 0.08, r, tau=0.02), gain=0.6)
    m.add(metal(1600, 0.08, r, tau=0.02), at=0.07, gain=0.4)
    return fit(m.out(), n_of(0.18))


@snd("char.1", "idle_steam_puff", "enh_small")     # 김 "쉬"
def _(r):
    return steam(r, 0.25, attack=0.03, tau=0.08, lo=3000)


# ---- 강화 화면

def bellows(r, dur):
    """풀무 "후욱" 세 번, 음이 한 단씩 오른다(E-09 충전 지속음)."""
    m = Mix()
    seg = dur / 3.0
    for i in range(3):
        d = seg * 0.95
        c = ramp(d, 300 * semis(4 * i), 1400 * semis(4 * i))
        w = sweep_bp(noise(r, d), c, q=1.4) * env_points(d, [(0, 0), (d * 0.55, 1), (d, 0)]) ** 1.3
        roar = filt(noise(r, d), lo=80, hi=500) * env_points(d, [(0, 0), (d * 0.6, 1), (d, 0.2)])
        glow = sine(110 * semis(4 * i), d) * env_points(d, [(0, 0), (d * 0.6, 1), (d, 0)]) * 0.15
        m.add(w + roar * 0.5 + glow, at=i * seg, gain=0.7 + 0.15 * i)
    return fit(m.out(), n_of(dur))


for _i, _d in enumerate([0.8, 1.2, 1.8, 2.4, 3.2]):
    snd("char.1", "enhance_bellows_%d" % (_i + 1), "enh")(lambda r, d=_d: bellows(r, d))


@snd("char.1", "enhance_success", "enh_big")       # 맑은 "깡!" + 담금질 "치익"
def _(r):
    m = Mix()
    m.add(metal(1250, 0.6, r, tau=0.22, ring=1.6), gain=0.9)
    m.add(thump(110, 45, 0.3, 0.07, click=0.6, r=r), gain=0.6)
    m.add(steam(r, 0.5, attack=0.02, tau=0.16, lo=2500), at=0.12, gain=0.55)
    m.add(crackle(r, 0.4, 250, amp_env=env_exp(0.4, 0.15)), at=0.02, gain=0.4)   # 불티 분수
    return fit(reverb(m.out(), r, t60=0.5, wet=0.2), n_of(0.8))


@snd("char.1", "enhance_fail", "enh")              # 둔한 "턱" + 긴 "치이익—"
def _(r):
    m = Mix()
    m.add(iron(r, 220, 0.2, 0.03) + thump(100, 45, 0.2, 0.05) * 0.7)
    m.add(steam(r, 0.8, attack=0.03, tau=0.35, lo=2200), at=0.08, gain=0.7)
    return fit(m.out(), n_of(0.9))


@snd("char.1", "enhance_iron_drop", "enh")         # 식은 조각이 떨어짐 "털썩"
def _(r):
    m = Mix()
    m.add(iron(r, 280, 0.15, 0.04))
    m.add(iron(r, 310, 0.1, 0.03), at=0.11, gain=0.5)
    m.add(grains(r, 0.25, 100, glen=0.01, lo=300, hi=2500, amp_env=env_exp(0.25, 0.08)), at=0.05, gain=0.5)
    return fit(m.out(), n_of(0.4))


@snd("char.1", "enhance_great_success", "enh_big")  # 식은 쇳물 껍질이 깨지며 "깡! 깡!" + 고리 "짤랑"
def _(r):
    m = Mix()
    m.add(grains(r, 0.3, 300, glen=0.008, lo=800, hi=6000, amp_env=env_exp(0.3, 0.1)), gain=0.8)
    m.add(metal(1250, 0.5, r, tau=0.2, ring=1.5), at=0.05, gain=0.8)
    m.add(metal(1250 * semis(4), 0.5, r, tau=0.2, ring=1.5), at=0.3, gain=0.8)
    m.add(thump(100, 40, 0.4, 0.1, click=0.6, r=r), at=0.05, gain=0.6)
    for i in range(3):
        m.add(coin(r, 3200 + 300 * i, 0.15), at=0.55 + 0.05 * i, gain=0.35)
    return fit(reverb(m.out(), r, t60=0.5, wet=0.2), n_of(0.9))


@snd("char.1", "enhance_charm_scorch", "enh")      # 종이 띠가 그을리며 버팀
def _(r):
    d = 0.5
    y = crackle(r, d, 300, lo=1500, hi=8000, amp_env=env_ar(d, 0.05, 0.3, 1))
    y += filt(noise(r, d), lo=600, hi=5000) * (1 + 0.6 * np.sin(2 * np.pi * 22 * t_of(d))) * env_ar(d, 0.02, 0.3, 1) * 0.4
    return y


@snd("char.1", "enhance_tongs", "enh")             # 하락 없음: 달궈진 쇠집게 "턱 + 짧은 칙"
def _(r):
    m = Mix()
    m.add(iron(r, 520, 0.12, 0.03))
    m.add(steam(r, 0.15, 0.003, 0.04, lo=3000), at=0.08, gain=0.6)
    return fit(m.out(), n_of(0.35))


@snd("char.1", "enhance_mutation", "enh")          # 변이: 끓어 넘치는 "부글부글"이 점점 빨라짐
def _(r):
    d = 1.0
    m = Mix()
    at = 0.0
    gap = 0.12
    while at < d - 0.1:
        m.add(bubble(r.uniform(180, 450), r.uniform(0.05, 0.09), r, rise=2.2), at=at, gain=0.4 + 0.5 * at)
        at += gap
        gap = max(0.025, gap * 0.88)
    m.add(filt(noise(r, d), lo=80, hi=600) * env_ar(d, 0.6, 0.1, 1), gain=0.5)
    return fit(m.out(), n_of(d))


@snd("char.1", "enhance_molten_pour", "enh")       # 제물 바치기 "주르륵"(근음 — 코드가 5음 음계로 올린다)
def _(r):
    d = 0.35
    m = Mix()
    m.add(_bubbles(r, d, 18, 300, 700) * env_ar(d, 0.03, 0.2, 1), gain=0.7)
    m.add(filt(noise(r, d), lo=400, hi=2500) * env_ar(d, 0.03, 0.2, 1), gain=0.4)
    m.add(bell(note_hz("C5"), d, r, tau=0.2, bright=0.5), gain=0.35)
    return fit(m.out(), n_of(d))


@snd("char.1", "enhance_ring_jingle", "enh")       # 슬롯 확장: 구리 고리 "짤랑"
def _(r):
    m = Mix()
    for i in range(4):
        m.add(coin(r, 2300 + 250 * i, 0.2), at=i * 0.05, gain=0.6)
    m.add(grains(r, 0.2, 100, glen=0.012, lo=200, hi=2000), gain=0.4)
    return fit(m.out(), n_of(0.45))


for _i, (_nm, _d, _top) in enumerate([("enhance_milestone_04", 1.0, 0), ("enhance_milestone_07", 1.2, 2), ("enhance_milestone_10", 1.4, 4)]):
    def _mile(r, d=_d, top=_top):
        m = Mix()
        seq = [("D5", 0.0), ("A5", 0.14), ("D6", 0.28)]
        for k, (nme, at) in enumerate(seq):
            m.add(metal(note_hz(nme) * semis(top), 0.6, r, tau=0.25, ring=1.5), at=at, gain=0.45)
        m.add(brass(note_hz("D4") * semis(top), d - 0.4) * 0.5, at=0.28, gain=0.35)
        m.add(steam(r, 0.5, 0.03, 0.15, lo=2500), at=0.35, gain=0.3)
        return fit(reverb(m.out(), r, t60=0.7, wet=0.22), n_of(d))
    snd("char.1", _nm, "enh_big")(_mile)


@snd("char.1", "enhance_stage13", "enh_big")       # +13: 긴 종 + 주전자 휘파람이 뚝 그치고 낮은 울림
def _(r):
    d = 2.0
    m = Mix()
    t = t_of(0.9)
    whistle = sine(1900 * (1 + 0.01 * np.sin(2 * np.pi * 5 * t)), 0.9) * env_points(0.9, [(0, 0), (0.6, 1), (0.85, 1), (0.9, 0)])
    m.add(whistle, gain=0.12)
    m.add(bell(note_hz("D4"), 1.6, r, tau=1.0, bright=0.8), at=0.0, gain=0.5)
    m.add(_idle_hum(r, 1.2) * env_points(1.2, [(0, 0), (0.2, 1), (1.2, 0)]), at=0.9, gain=0.7)
    return fit(reverb(m.out(), r, t60=1.2, wet=0.25), n_of(d))


# ================================================================== 쓰기·그림

def render(folder, name, cat, fn, loop):
    r = rng(folder, name)
    x = fn(r)
    if np.ndim(x) == 0 or len(x) == 0:
        raise RuntimeError(folder + "/" + name + " made no audio")
    return finalize(x, cat, loop=loop)


def colormap(v):
    """0~1 → RGB(검정 → 보라 → 주황 → 노랑)."""
    stops = np.array([[0, 0, 0], [60, 15, 90], [180, 40, 80], [250, 140, 30], [255, 240, 160]], float)
    p = np.clip(v, 0, 1) * (len(stops) - 1)
    i = np.minimum(len(stops) - 2, p.astype(int))
    f = (p - i)[..., None]
    return (stops[i] * (1 - f) + stops[i + 1] * f).astype(np.uint8)


def png(path, x, title):
    from PIL import Image, ImageDraw
    W, HW, HS = 900, 160, 220
    img = Image.new("RGB", (W, HW + HS + 20), (18, 18, 22))
    d = ImageDraw.Draw(img)
    n = len(x)
    cols = np.array_split(x, W)
    mid = 20 + HW // 2
    for i, c in enumerate(cols):
        if len(c) == 0:
            continue
        lo, hi = c.min(), c.max()
        clip = max(abs(lo), abs(hi)) >= undb(-1.0)
        d.line([(i, mid - hi * HW / 2), (i, mid - lo * HW / 2)], fill=(255, 60, 60) if clip else (120, 200, 255))
    for lvl in (-1.0, -6.0):
        y = HW / 2 * undb(lvl)
        d.line([(0, mid - y), (W, mid - y)], fill=(70, 70, 80))
        d.line([(0, mid + y), (W, mid + y)], fill=(70, 70, 80))
    nfft, hop = 1024, max(64, n // W)
    frames = max(1, (n - nfft) // hop + 1)
    win = np.hanning(nfft)
    spec = np.zeros((frames, nfft // 2 + 1))
    xp = np.concatenate([x, np.zeros(nfft)])
    for k in range(frames):
        spec[k] = np.abs(np.fft.rfft(xp[k * hop:k * hop + nfft] * win))
    s = db(spec + 1e-9)
    s = (s - (s.max() - 80)) / 80
    # 세로 = 로그 주파수 40Hz~20kHz
    f = np.fft.rfftfreq(nfft, 1.0 / SR)
    ys = np.geomspace(40, 20000, HS)[::-1]
    bins = np.searchsorted(f, ys).clip(0, len(f) - 1)
    im = colormap(s[:, bins].T)
    sp = Image.fromarray(im).resize((W, HS))
    img.paste(sp, (0, HW + 20))
    d.text((4, 2), "%s  %.3fs  peak %.1f dBFS  st50 %.1f dB" % (title, n / SR, db(np.max(np.abs(x))), st50(x)), fill=(230, 230, 230))
    img.save(path)


PNG_DEFAULT = ["kill_1", "kill_7", "explosion_large", "player_hit", "hit_common_1", "fire_block_fireball_1",
               "ui_press", "ui_confirm", "tele_fill", "enh_great_burst", "enh_plus13_theme_1", "boss_intro_1",
               "fire_boom_bell", "idle_boil", "enhance_success", "enhance_bellows_3", "multi_kill", "elite_kill"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--png", action="store_true")
    ap.add_argument("--report", action="store_true")
    a = ap.parse_args()
    only = set(s for s in a.only.split(",") if s)
    keys = [(s[0], s[1]) for s in SOUNDS]
    dup = sorted(set(k for k in keys if keys.count(k) > 1))
    if dup:
        raise SystemExit("duplicate names: %s" % dup)
    rows = []
    for folder, name, cat, fn, loop in SOUNDS:
        if only and name not in only:
            continue
        x = render(folder, name, cat, fn, loop)
        if not a.report:
            write_wav(os.path.join(AUDIO, folder, name + ".wav"), x)
        rows.append((folder, name, cat, len(x) / SR, db(np.max(np.abs(x))), st50(x), loop))
        if a.png and (name in only or (not only and name in PNG_DEFAULT)):
            os.makedirs(LOGS, exist_ok=True)
            png(os.path.join(LOGS, folder + "_" + name + ".png"), x, folder + "/" + name)
    # 제자 1 폴더 목록(char.2 audio.json과 같은 꼴)
    if not a.report and not only:
        man = {"sampleRate": SR, "channels": 1, "format": "PCM16", "generator": "Tools/sfx/synth.py", "seed": SEED, "assets": {}}
        for folder, name, cat, secs, pk, st, loop in rows:
            if folder == "char.1":
                man["assets"][name] = {"file": name + ".wav", "seconds": round(secs, 3), "loop": bool(loop), "peakDbfs": round(pk, 1)}
        with open(os.path.join(AUDIO, "char.1", "audio.json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(man, f, ensure_ascii=False, indent=2)
            f.write("\n")
    for row in rows:
        print("%-8s %-30s %-9s %6.3fs peak %6.1f st50 %6.1f%s" % (row[0], row[1], row[2], row[3], row[4], row[5], " loop" if row[6] else ""))
    print("%d sounds" % len(rows))


if __name__ == "__main__":
    main()
