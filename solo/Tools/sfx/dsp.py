"""
solo/Tools/sfx/dsp.py — 효과음 절차 합성 도구(numpy만 쓴다. scipy 없음).

모든 함수는 float64 모노 배열(44.1kHz)을 주고받는다. 난수는 호출자가 넘기는 numpy Generator만 쓴다
(같은 씨앗이면 같은 결과 — synth.py가 소리 이름마다 씨앗을 만든다).
"""
import numpy as np

SR = 44100


# ------------------------------------------------------------------ 기본

def n_of(d):
    return max(1, int(round(d * SR)))


def t_of(d):
    return np.arange(n_of(d)) / SR


def db(x):
    return 20.0 * np.log10(np.maximum(x, 1e-12))


def undb(d):
    return 10.0 ** (d / 20.0)


def fit(x, n):
    """길이를 n으로 자르거나 0으로 채운다."""
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])


def semis(s):
    return 2.0 ** (s / 12.0)


class Mix:
    """자리(초)에 소리를 더해 가는 버퍼."""

    def __init__(self):
        self.buf = np.zeros(0)

    def add(self, x, at=0.0, gain=1.0):
        if x is None or len(x) == 0:
            return self
        i = int(round(at * SR))
        need = i + len(x)
        if need > len(self.buf):
            self.buf = np.concatenate([self.buf, np.zeros(need - len(self.buf))])
        self.buf[i:need] += x * gain
        return self

    def out(self):
        return self.buf.copy()


# ------------------------------------------------------------------ 엔벌로프

def env_exp(dur, tau, attack=0.001):
    t = t_of(dur)
    e = np.exp(-t / max(tau, 1e-4))
    if attack > 0:
        a = np.minimum(1.0, t / attack)
        e = e * (0.5 - 0.5 * np.cos(np.pi * a))
    return e


def env_ar(dur, attack, release, shape=2.0):
    """attack까지 오르고 끝까지 power 곡선으로 내린다."""
    t = t_of(dur)
    a = np.minimum(1.0, t / max(attack, 1e-4))
    r = np.clip((dur - t) / max(release, 1e-4), 0.0, 1.0)
    return (0.5 - 0.5 * np.cos(np.pi * a)) * r ** shape


def env_points(dur, pts):
    """[(초, 값), ...] 사이를 곧게 잇는다."""
    t = t_of(dur)
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return np.interp(t, xs, ys)


def ramp(dur, a, b, curve="exp"):
    """dur 동안 a→b(주파수 스윕용). exp = 로그 간격."""
    t = np.linspace(0.0, 1.0, n_of(dur))
    if curve == "exp" and a > 0 and b > 0:
        return a * (b / a) ** t
    return a + (b - a) * t


# ------------------------------------------------------------------ 발진기

def phase_of(freq, n=None):
    f = np.asarray(freq, dtype=np.float64)
    if f.ndim == 0:
        f = np.full(n, float(f))
    return 2.0 * np.pi * np.cumsum(f) / SR


def sine(freq, dur, phase0=0.0):
    n = n_of(dur)
    f = freq if np.ndim(freq) else np.full(n, float(freq))
    f = fit(np.asarray(f, dtype=np.float64), n)
    return np.sin(phase_of(f) + phase0)


def saw(freq, dur, bright=12):
    """대역 제한 톱니(가산 합성, 배음 bright개, 나이퀴스트 위는 버린다)."""
    n = n_of(dur)
    f = freq if np.ndim(freq) else np.full(n, float(freq))
    f = fit(np.asarray(f, dtype=np.float64), n)
    ph = phase_of(f)
    y = np.zeros(n)
    for k in range(1, bright + 1):
        mask = (f * k) < SR * 0.45
        y += mask * np.sin(k * ph) / k
    return y * 0.6


def square(freq, dur, bright=9):
    n = n_of(dur)
    f = freq if np.ndim(freq) else np.full(n, float(freq))
    f = fit(np.asarray(f, dtype=np.float64), n)
    ph = phase_of(f)
    y = np.zeros(n)
    for k in range(1, 2 * bright, 2):
        mask = (f * k) < SR * 0.45
        y += mask * np.sin(k * ph) / k
    return y * 0.8


def fm(fc, ratio, index, dur):
    """2연산자 FM. index는 상수 또는 배열(시간에 따른 밝기)."""
    n = n_of(dur)
    f = fc if np.ndim(fc) else np.full(n, float(fc))
    f = fit(np.asarray(f, dtype=np.float64), n)
    idx = index if np.ndim(index) else np.full(n, float(index))
    idx = fit(np.asarray(idx, dtype=np.float64), n)
    mod = np.sin(phase_of(f * ratio))
    return np.sin(phase_of(f) + idx * mod)


def noise(r, dur):
    return r.standard_normal(n_of(dur))


def modal(freqs, amps, taus, dur, r=None, attack=0.0005):
    """감쇠하는 사인의 합(종·쇠·유리). taus = 각 부분음의 감쇠 시간."""
    t = t_of(dur)
    y = np.zeros(len(t))
    for i, (f, a, tau) in enumerate(zip(freqs, amps, taus)):
        if f >= SR * 0.45:
            continue
        ph = r.uniform(0, 2 * np.pi) if r is not None else 0.0
        y += a * np.sin(2 * np.pi * f * t + ph) * np.exp(-t / tau)
    if attack > 0:
        y *= np.minimum(1.0, t / attack)
    return y


# ------------------------------------------------------------------ 필터(주파수 영역)

def _butter_mag(f, fc, order, kind):
    fc = max(fc, 1.0)
    if kind == "lp":
        return 1.0 / np.sqrt(1.0 + (f / fc) ** (2 * order))
    ratio = np.where(f > 0, fc / np.maximum(f, 1e-9), 1e9)
    return 1.0 / np.sqrt(1.0 + ratio ** (2 * order))


def filt(x, lo=None, hi=None, order=2):
    """정적 필터(영위상). lo = 고역 통과 차단, hi = 저역 통과 차단."""
    if len(x) == 0:
        return x
    n = len(x)
    m = 1
    while m < n * 2:
        m *= 2
    X = np.fft.rfft(x, m)
    f = np.fft.rfftfreq(m, 1.0 / SR)
    H = np.ones_like(f)
    if lo is not None:
        H *= _butter_mag(f, lo, order, "hp")
    if hi is not None:
        H *= _butter_mag(f, hi, order, "lp")
    return np.fft.irfft(X * H, m)[:n]


def peak_bp(x, f0, q=4.0):
    """f0 둘레 공명(로그 주파수에서 종 모양)."""
    n = len(x)
    m = 1
    while m < n * 2:
        m *= 2
    X = np.fft.rfft(x, m)
    f = np.fft.rfftfreq(m, 1.0 / SR)
    lf = np.log2(np.maximum(f, 1.0) / f0)
    H = np.exp(-0.5 * (lf * q) ** 2)
    return np.fft.irfft(X * H, m)[:n]


def formant(x, freqs, qs, gains):
    y = np.zeros(len(x))
    for f0, q, g in zip(freqs, qs, gains):
        y += peak_bp(x, f0, q) * g
    return y


def sweep(x, lo_track=None, hi_track=None, order=2, nfft=512):
    """시간에 따라 움직이는 필터(STFT, sqrt-Hann 50% 겹침). track = 길이 len(x)의 주파수 배열 또는 None."""
    n = len(x)
    hop = nfft // 2
    win = np.sqrt(np.hanning(nfft + 1)[:-1])
    pad = np.concatenate([np.zeros(nfft), x, np.zeros(nfft)])
    frames = (len(pad) - nfft) // hop + 1
    out = np.zeros(len(pad))
    f = np.fft.rfftfreq(nfft, 1.0 / SR)
    for k in range(frames):
        s = k * hop
        seg = pad[s:s + nfft] * win
        c = min(n - 1, max(0, s + hop - nfft))
        H = np.ones_like(f)
        if lo_track is not None:
            H *= _butter_mag(f, lo_track[c], order, "hp")
        if hi_track is not None:
            H *= _butter_mag(f, hi_track[c], order, "lp")
        out[s:s + nfft] += np.fft.irfft(np.fft.rfft(seg) * H, nfft) * win
    return out[nfft:nfft + n]


def sweep_bp(x, center_track, q=2.0, nfft=512):
    """움직이는 띠 통과(가운데 주파수 배열)."""
    n = len(x)
    hop = nfft // 2
    win = np.sqrt(np.hanning(nfft + 1)[:-1])
    pad = np.concatenate([np.zeros(nfft), x, np.zeros(nfft)])
    frames = (len(pad) - nfft) // hop + 1
    out = np.zeros(len(pad))
    f = np.fft.rfftfreq(nfft, 1.0 / SR)
    lf0 = np.log2(np.maximum(f, 1.0))
    for k in range(frames):
        s = k * hop
        seg = pad[s:s + nfft] * win
        c = min(n - 1, max(0, s + hop - nfft))
        H = np.exp(-0.5 * ((lf0 - np.log2(center_track[c])) * q) ** 2)
        out[s:s + nfft] += np.fft.irfft(np.fft.rfft(seg) * H, nfft) * win
    return out[nfft:nfft + n]


def conv(x, h):
    n = len(x) + len(h) - 1
    m = 1
    while m < n:
        m *= 2
    return np.fft.irfft(np.fft.rfft(x, m) * np.fft.rfft(h, m), m)[:n]


# ------------------------------------------------------------------ 효과

def reverb(x, r, t60=0.5, wet=0.2, damp=5000.0, pre=0.01, tail=None):
    """합성 잔향(지수 감쇠 노이즈 IR + 이른 반사). 꼬리를 포함해 길어진 배열을 준다."""
    tail = t60 * 1.1 if tail is None else tail
    n = n_of(tail)
    t = np.arange(n) / SR
    ir = r.standard_normal(n) * np.exp(-6.91 * t / t60)
    ir = filt(ir, lo=150.0, hi=damp, order=1)
    # 높은 쪽이 더 빨리 죽게: 뒤쪽 절반을 한 번 더 깎는다
    ir = ir * (1.0 - 0.4 * np.minimum(1.0, t / t60)) + filt(ir, hi=damp * 0.4, order=1) * 0.4 * np.minimum(1.0, t / t60)
    p = n_of(pre)
    ir[:p] = 0.0
    for dt, g in ((0.013, 0.5), (0.021, 0.35), (0.034, 0.25)):
        i = n_of(pre + dt)
        if i < n:
            ir[i] += g * 6.0 / np.sqrt(n)
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
    w = conv(x, ir)
    dry = fit(x, len(w))
    return dry + w * wet


def delay(x, dt, fb=0.3, taps=3, damp=None):
    m = Mix()
    m.add(x)
    g = 1.0
    y = x
    for i in range(1, taps + 1):
        g *= fb
        if damp:
            y = filt(y, hi=damp, order=1)
        m.add(y, at=dt * i, gain=g)
    return m.out()


def drive(x, amount=2.0):
    return np.tanh(x * amount) / np.tanh(amount)


def crackle(r, dur, rate, lo=1500.0, hi=9000.0, decay=0.0015, amp_env=None):
    """드문 임펄스(불티·지직). rate = 초당 개수."""
    n = n_of(dur)
    y = np.zeros(n)
    k = r.poisson(rate * dur)
    if k > 0:
        pos = r.integers(0, n, k)
        amps = r.uniform(0.3, 1.0, k) * r.choice([-1.0, 1.0], k)
        y[pos] += amps
    L = n_of(decay * 6)
    kern = np.exp(-np.arange(L) / (decay * SR)) * r.standard_normal(L)
    y = conv(y, kern)[:n]
    y = filt(y, lo=lo, hi=hi, order=2)
    if amp_env is not None:
        y *= fit(amp_env, n)
    return y


def grains(r, dur, rate, glen=0.02, lo=2000.0, hi=7000.0, amp_env=None):
    """잘게 썬 노이즈 알갱이(바스락·자갈)."""
    n = n_of(dur)
    y = np.zeros(n)
    k = r.poisson(rate * dur)
    L = n_of(glen)
    w = np.hanning(L)
    for _ in range(k):
        i = r.integers(0, max(1, n - L))
        g = r.standard_normal(L) * w * r.uniform(0.2, 1.0)
        y[i:i + L] += g
    y = filt(y, lo=lo, hi=hi, order=2)
    if amp_env is not None:
        y *= fit(amp_env, n)
    return y


def norm(x):
    p = np.max(np.abs(x)) if len(x) else 0.0
    return x / p if p > 0 else x


# ------------------------------------------------------------------ 자주 쓰는 소리 조각

def thump(f0, f1, dur, tau, click=0.0, r=None):
    """저음 펀치(음높이가 뚝 떨어지는 사인 + 선택적 딸깍)."""
    t = t_of(dur)
    f = f1 + (f0 - f1) * np.exp(-t / (tau * 0.35))
    # 작은 스피커에서도 들리게 배음을 조금 세운다(tanh) — 순수 사인 저음은 "퍽"이 아니라 "웅"으로 들린다.
    y = np.tanh(2.2 * np.sin(phase_of(f))) / np.tanh(2.2) * np.exp(-t / tau) * 0.6
    y *= np.minimum(1.0, t / 0.0008)
    if click > 0 and r is not None:
        c = filt(noise(r, 0.006), lo=1500, hi=8000) * env_exp(0.006, 0.0015)
        y = fit(y, len(y))
        y[:len(c)] += c * click
    return y


def puff(r, dur, lo, hi, attack, tau):
    return filt(noise(r, dur), lo=lo, hi=hi) * env_exp(dur, tau, attack)


def whoosh(r, dur, f_from, f_to, q=1.5, peak_at=0.5):
    """띠 노이즈가 휙 지나간다(가운데 주파수 스윕 + 가운데가 가장 큰 엔벌로프)."""
    x = noise(r, dur)
    c = ramp(dur, f_from, f_to)
    y = sweep_bp(x, c, q=q)
    e = env_points(dur, [(0, 0), (dur * peak_at, 1), (dur, 0)])
    return y * e ** 1.5


def bell(f0, dur, r, bright=1.0, tau=1.0, ratios=None, amps=None):
    """종(부분음 비율 = 교회 종 근사)."""
    ratios = ratios or [0.5, 1.0, 1.19, 1.5, 2.0, 2.51, 2.66, 3.01, 4.17, 5.43]
    amps = amps or [0.35, 1.0, 0.55, 0.4, 0.5, 0.25, 0.2, 0.18, 0.12, 0.08]
    taus = [tau * (1.6 if k == 0 else 1.0 / (0.7 + 0.5 * k ** 0.8)) for k in range(len(ratios))]
    amps = [a * (bright ** min(k, 6) if k > 1 else a) for k, a in enumerate(amps)]
    return modal([f0 * q for q in ratios], amps, taus, dur, r)


def chime(f0, dur, r, tau=0.4):
    """맑은 유리·쇠 종(위 부분음이 비조화)."""
    return modal([f0, f0 * 2.76, f0 * 5.40, f0 * 8.93], [1.0, 0.45, 0.22, 0.1],
                 [tau, tau * 0.45, tau * 0.25, tau * 0.12], dur, r)


def metal(f0, dur, r, tau=0.25, ring=1.0):
    """쇠를 친 소리(모루·철판). 부분음이 촘촘하고 비조화."""
    ratios = [1.0, 1.47, 2.09, 2.56, 3.39, 4.1, 5.2, 6.3]
    amps = [1.0, 0.7, 0.6, 0.45, 0.35, 0.25, 0.2, 0.15]
    taus = [tau * ring, tau * 0.8 * ring, tau * 0.6, tau * 0.5, tau * 0.4, tau * 0.3, tau * 0.22, tau * 0.18]
    jit = [1.0 + r.uniform(-0.01, 0.01) for _ in ratios]
    y = modal([f0 * q * j for q, j in zip(ratios, jit)], amps, taus, dur, r)
    c = filt(noise(r, 0.01), lo=2000, hi=12000) * env_exp(0.01, 0.002)
    y[:len(c)] += c * 0.8
    return y


def glass_shatter(r, dur, density=60, lo=2500.0, hi=11000.0, tau=0.08):
    """유리가 깨지며 흩어짐(무작위 높은 핑 + 깨지는 노이즈)."""
    m = Mix()
    burst = filt(noise(r, dur), lo=lo * 0.8, hi=hi * 1.2) * env_exp(dur, 0.03, 0.0005)
    m.add(burst, gain=0.8)
    k = max(4, int(density * dur))
    for i in range(k):
        at = (r.uniform(0, 1) ** 2.2) * dur * 0.85
        f = r.uniform(lo, hi)
        d = min(dur - at, tau * 3)
        if d <= 0.005:
            continue
        p = modal([f, f * r.uniform(1.4, 2.6)], [1.0, 0.4], [tau * r.uniform(0.4, 1.2), tau * 0.3], d, r)
        m.add(p, at=at, gain=r.uniform(0.15, 0.5) * (1 - at / dur) ** 0.7)
    return fit(m.out(), n_of(dur))


def steam(r, dur, attack=0.01, tau=0.2, lo=2500.0, hi=11000.0, hiss_env=None):
    x = filt(noise(r, dur), lo=lo, hi=hi, order=2)
    e = env_exp(dur, tau, attack) if hiss_env is None else fit(hiss_env, n_of(dur))
    # 김은 거칠게 일렁인다
    wob = 1.0 + 0.25 * filt(noise(r, dur), hi=30.0, order=1) / 0.05
    return x * e * np.clip(wob, 0.3, 1.7)


def sizzle(r, dur, rate=400.0, env=None):
    base = filt(noise(r, dur), lo=3000, hi=12000) * 0.25
    cr = crackle(r, dur, rate, lo=2500, hi=10000, decay=0.0008)
    y = base + cr
    if env is not None:
        y *= fit(env, n_of(dur))
    return y


def bubble(f0, dur, r, rise=2.2):
    """거품 하나(음높이가 위로 휙)."""
    t = t_of(dur)
    f = f0 * (1 + (rise - 1) * (1 - np.exp(-t / (dur * 0.35))))
    return np.sin(phase_of(f)) * env_exp(dur, dur * 0.3, 0.001)


def tone(freq, dur, kind="sine", attack=0.005, tau=None, release=None, r=None, vib=0.0, vib_rate=5.5):
    """악기 한 음."""
    t = t_of(dur)
    f = np.full(len(t), float(freq)) if not np.ndim(freq) else fit(np.asarray(freq, float), len(t))
    if vib > 0:
        f = f * (1 + vib * np.sin(2 * np.pi * vib_rate * t) * np.minimum(1, t / 0.15))
    if kind == "sine":
        y = np.sin(phase_of(f))
    elif kind == "saw":
        y = saw(f, dur)
    elif kind == "square":
        y = square(f, dur)
    elif kind == "tri":
        y = np.zeros(len(t))
        ph = phase_of(f)
        for k in range(1, 12, 2):
            y += ((-1) ** ((k - 1) // 2)) * np.sin(k * ph) / (k * k)
        y *= 0.8
    else:
        raise ValueError(kind)
    e = np.minimum(1.0, t / max(attack, 1e-4))
    if tau is not None:
        e = e * np.exp(-t / tau)
    if release is not None:
        e = e * np.clip((dur - t) / release, 0, 1)
    return y * e


def chord(freqs, dur, inst, gains=None, spread=0.0, r=None):
    m = Mix()
    for i, f in enumerate(freqs):
        g = gains[i] if gains else 1.0 / len(freqs)
        m.add(inst(f, dur - spread * i), at=spread * i, gain=g)
    return fit(m.out(), n_of(dur))


def note_hz(name):
    """'C4', 'F#5', 'Bb3' → Hz (A4 = 440)."""
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    k = names[name[0]]
    i = 1
    if len(name) > 2 or (len(name) == 2 and name[1] in "#b"):
        if name[1] == "#":
            k += 1
            i = 2
        elif name[1] == "b":
            k -= 1
            i = 2
    octv = int(name[i:])
    return 440.0 * 2 ** ((k + 12 * (octv - 4)) / 12.0)


def S(*xs):
    """길이가 다른 소리들을 0으로 채워 더한다."""
    xs = [np.asarray(x, dtype=np.float64) for x in xs if np.ndim(x)]
    n = max(len(x) for x in xs)
    y = np.zeros(n)
    for x in xs:
        y[:len(x)] += x
    return y
