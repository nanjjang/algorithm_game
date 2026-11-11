# online.md 1.6 / 2.6: does owning unlimited wands of one character (DECISIONS 1.22) shorten the time to the first +13?
# Extension of online_econ3.py (same drop tables, same enhancement rules, same income model). Run: python3 online_econ3_multi.py
# Part 1: time to the first +13 when gold is spread over several wands by different policies.
# Part 2: "slot fishing" - buying +0 wands and enhancing them through the cheap stages to keep the one with extra slots.
import os, sys, random, math, statistics as st
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from online_econ3 import P, cost, tier, invH, run_stats, pick_start, TM

PRICE = 500  # online.md 1.6: price of a new +0 wand


class Wand:
    __slots__ = ('s', 'peak', 'aura', 'slots')

    def __init__(self):
        self.s = 0; self.peak = 0; self.aura = 0; self.slots = 0


def attempt(rng, w, use_sac, charm=False):
    """One enhancement attempt, same branch rules as online_econ3.career(). Returns True if a charm was consumed."""
    s = w.s
    p = 100 if s == 0 else min(95, P[s]+15*w.aura+(15 if use_sac else 0))
    r = rng.random()*100
    if r < p:
        if s > 0 and r < p*0.15:
            w.s = min(13, s+2); w.slots = min(3, w.slots+1)
        else:
            w.s = s+1
        w.aura = 0; w.peak = max(w.peak, w.s)
        return False
    if rng.random() < 0.25:
        w.aura = min(3, w.aura+1)
        return False
    drop = s > 3 and (s-1) >= w.peak-2
    if drop and charm:
        return True
    if drop: w.s = s-1
    w.aura = min(3, w.aura+1)
    return False


def order(policy, wands, T, cur):
    """Wands in the order the policy wants to attempt them. The caller takes the first one that has a stone
    of its tier; if that wand is not affordable the player saves gold (no spending on other wands instead)."""
    by_lead = sorted(wands, key=lambda w: (-w.s, -w.aura))
    by_low = sorted(wands, key=lambda w: (w.s, -w.aura))
    if policy == 'single' or policy == 'leader':
        return by_lead[:1]
    if policy == 'even':                      # always the lowest wand: N wands kept level
        return by_low
    if policy == 'lift':                      # lift every wand to +T first (lowest first), then only the leader
        low = [w for w in by_low if w.s < T]
        return low + by_lead[:1] if low else by_lead[:1]
    if policy == 'aura':                      # among wands within 1 stage of the leader, the one with most aura
        near = [w for w in wands if w.s >= by_lead[0].s-1]
        return sorted(near, key=lambda w: (-w.aura, -w.s))
    if policy == 'switch':                    # rotate to the next wand after every failure
        k = cur % len(wands)
        return wands[k:] + wands[:k]
    raise ValueError(policy)


def career_multi(rng, k, policy='single', n=1, T=7, tmk='A', share=1.0, sac=True, max_h=6000):
    tm = TM[tmk]
    wands = [Wand()]
    gold = 0.0; st3 = [0, 0, 0]; blocks = 0; hours = 0.0; best = 0; first3 = True
    cur = 0
    while max(w.s for w in wands) < 13 and hours < max_h:
        top = max(w.s for w in wands)
        h = k*1.15**top*math.exp(rng.gauss(0, 0.15))
        reach = max(1, int(invH(h)))
        start = pick_start(best, min(w.s for w in wands), st3)
        if reach <= start: reach = start+1
        g, t, lo, mid, up, ch, bag, _ = run_stats(rng, start, reach, tm, first3 and start < 3 <= reach)
        if start < 3 <= reach: first3 = False
        best = max(best, reach)
        hours += t; gold += g*0.85*share
        st3[0] += lo; st3[1] += mid; st3[2] += up
        blocks += int(bag*0.7+rng.random())
        while len(wands) < n and gold >= PRICE:
            gold -= PRICE; wands.append(Wand())
        while True:
            w = next((x for x in order(policy, wands, T, cur) if st3[tier(x.s)] >= 1), None)
            if w is None or gold < cost(w.s): break
            use_sac = sac and w.s >= 3 and blocks >= 3
            gold -= cost(w.s); st3[tier(w.s)] -= 1
            if use_sac: blocks -= 3
            before = w.s
            attempt(rng, w, use_sac)
            if policy == 'switch' and w.s <= before: cur += 1
            if w.s >= 13: break
    lead = max(wands, key=lambda w: w.s)
    return hours, lead.slots


def rep(name, k=1.74, N=1500, **kw):
    rng = random.Random(11)
    res = [career_multi(rng, k, **kw) for _ in range(N)]
    v = sorted(r[0] for r in res)
    sl = [r[1] for r in res]
    print(f' {name:44s} +13: {st.mean(v):6.1f}/{v[N//2]:6.1f}/{v[int(N*.9)]:6.1f} h | slots(0..3)% {[round(sl.count(i)/N*100) for i in range(4)]}')


def fish(T, sac, N=200000):
    """Grow a fresh +0 wand to at least +T. Returns mean gold, mean attempts per stone tier, P(slots>=k)."""
    rng = random.Random(5); gold = 0.0; att = [0, 0, 0]; cnt = [0, 0, 0, 0]
    for _ in range(N):
        w = Wand()
        while w.s < T:
            gold += cost(w.s); att[tier(w.s)] += 1
            attempt(rng, w, sac and w.s >= 3)
        cnt[w.s and w.slots] += 1
    g = gold/N
    ge = [sum(cnt[k:])/N for k in range(4)]
    return g, [a/N for a in att], ge


if __name__ == '__main__':
    print('== Part 1. hours to the first +13 (normal player k=1.74, sacrifice +15, all gold, time model A): mean/median/p90')
    rep('1 wand (baseline, = online_econ3)', policy='single', n=1)
    for n in (2, 3, 5, 10):
        rep(f'{n} wands, leader only (others idle)', policy='leader', n=n)
    for n in (2, 3, 5):
        rep(f'{n} wands, kept level (lowest first)', policy='even', n=n)
    for n, T in ((3, 5), (3, 7), (5, 7), (10, 7), (3, 10)):
        rep(f'{n} wands, all to +{T} first, then leader', policy='lift', n=n, T=T)
    for n in (2, 3):
        rep(f'{n} wands, most aura among top two stages', policy='aura', n=n)
        rep(f'{n} wands, switch wand after every failure', policy='switch', n=n)
    print('== same, time model B (monsters.md 6.6 pace)')
    rep('1 wand (baseline)', policy='single', n=1, tmk='B')
    rep('3 wands, kept level', policy='even', n=3, tmk='B')
    rep('3 wands, all to +7 first, then leader', policy='lift', n=3, T=7, tmk='B')

    print('== Part 2. slot fishing: one fresh +0 wand grown to +T (sacrifice +15 from +4 attempts on)')
    eng = {1: 20000*1.9, 2: (20000+40000)*1.9, 3: (20000+40000+60000)*1.9}   # online.md 1.5, mean 1.9 tries per slot
    for T in (4, 5, 7):
        g, att, ge = fish(T, True)
        print(f' to +{T}: gold {g:7.0f} (+{PRICE} wand), stones lo/mid/up {att[0]:.1f}/{att[1]:.1f}/{att[2]:.1f}, P(slots>=1/2/3) {ge[1]*100:.1f}/{ge[2]*100:.1f}/{ge[3]*100:.2f}%')
        for kk in (1, 2, 3):
            if ge[kk] > 0:
                per = (g+PRICE)/ge[kk]
                print(f'    a +{T} wand with >={kk} slots by fishing: {per:9.0f} gold, {att[0]/ge[kk]:6.1f} lower stones, {1/ge[kk]:5.1f} wands'
                      f' | carving {kk} slot(s) instead: {eng[kk]:7.0f} gold + {3.8*kk:.1f} upper stones')
