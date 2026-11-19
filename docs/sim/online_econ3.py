# online.md 2.3 / 2.6 re-run with monsters.md 6.1, 6.2.4, tables 6.3-A~D (re-read 2026-11-11 17:22 version).
import random, math, statistics as st, sys
P = [100, 90, 75, 60, 50, 40, 32, 25, 18, 12, 8, 5, 3]
def cost(s): return 100*(s+1)**2
def tier(s): return 0 if s < 5 else (1 if s < 9 else 2)
def H(w): return 1+0.12*(w-1) if w <= 12 else 2.32*1.06**(w-12)
def invH(h):
    if h <= 2.32: return max(1.0, 1+(h-1)/0.12)
    return 12+math.log(h/2.32)/math.log(1.06)
def S(w): return 1+0.1*(w-1)
def B(w): return 10+6*w if w <= 12 else min(200, 82+4*(w-12))
def gold_wave(w): return 20+200*S(w) if w % 10 == 0 else 2*B(w)+20
def elites(w):
    if w % 10 == 0 or w < 12: return 0
    return 1 if w < 20 else 2 if w < 30 else 3 if w < 40 else 4
def day(w): return (w+9)//10
BLOCKVAL = {1: 7.2, 2: 8.4, 3: 9.4}   # avg gold of one unreturned wild block by day (5/15/50 x grade odds)

# time models (minutes): A = online.md assumption, B = pace implied by monsters.md 6.6
TM = {
    'A': dict(wave=lambda w: min(2.2, 1.3+0.02*w), boss=2.5, gap=2.0),
    'B': dict(wave=lambda w: 1.1, boss=2.0, gap=2.0),
}
BAG_PER = 3; BAG_MAX = 12   # monsters.md 6.2.4: floor(directly cleared waves / 3), no base, max 12

def run_stats(rng, start, reach, tm, first3):
    """directly cleared waves start+1..reach. returns gold, hours, lo, mid, up, charm, bag, coins"""
    g = 0.0; t = tm['gap']; lo = mid = up = ch = 0; coins = 0
    for w in range(start+1, reach+1):
        d = day(w)
        g += gold_wave(w)
        if w % 10 == 0:
            t += tm['boss']; coins += 5*min(d, 5)
            if d == 1:
                lo += 1
                if rng.random() < 0.50: mid += 1
                if rng.random() < 0.02: ch += 1
            elif d == 2:
                mid += 1
                if rng.random() < 0.20: up += 1
                if rng.random() < 0.04: ch += 1
            else:
                mid += 1; up += 1
                if rng.random() < min(0.50, 0.10*(d-3)): up += 1
                if rng.random() < min(0.10, 0.02*d): ch += 1
        else:
            t += tm['wave'](w); coins += min(d, 5)
            if w % 10 == 5:
                if d == 1: lo += 1
                elif d == 2: lo += 1; mid += 1
                else:
                    mid += 1
                    if rng.random() < min(0.25, 0.05*(d-2)): up += 1
            for _ in range(elites(w)):
                if d == 2:
                    if rng.random() < 0.10: lo += 1
                    if rng.random() < 0.08: mid += 1
                elif d >= 3:
                    if rng.random() < 0.05: mid += 1
                    if rng.random() < min(0.05, 0.02+0.005*(d-3)): up += 1
                    if rng.random() < 0.005: ch += 1
        if w == 3 and first3: lo += 3
    n = reach-start
    bag = min(BAG_MAX, n//BAG_PER)
    # 2 cards picked per wave; what is not carried home turns into gold
    g += max(0, 2*n-bag)*BLOCKVAL[min(3, day(max(1, reach)))]
    return g, t/60.0, lo, mid, up, ch, bag, coins

def pick_start(best, s, st3):
    mx = (int(0.7*best)//10)*10 if best >= 15 else 0
    if s < 5 and st3[0] == 0: return min(mx, 10)      # lower stones only drop on day 1-2
    return mx

def career(rng, k, tmk='A', share=1.0, sac=True, charm=False, free_stones=False, max_h=4000, smart=True):
    tm = TM[tmk]
    s = 0; peak = 0; aura = 0; gold = 0.0; st3 = [0, 0, 0]; charms = 0; blocks = 0
    hours = 0.0; best = 0; hit = {}; slots = 0; weekbuy = 0; weekmark = 0.0; att = 0; first3 = True
    got = [0, 0, 0]; wait_stone = 0.0; wait_gold = 0.0
    while s < 13 and hours < max_h:
        h = k*1.15**s*math.exp(rng.gauss(0, 0.15))
        reach = max(1, int(invH(h)))
        start = pick_start(best, s, st3) if smart else ((int(0.7*best)//10)*10 if best >= 15 else 0)
        if reach <= start: reach = start+1
        g, t, lo, mid, up, ch, bag, _ = run_stats(rng, start, reach, tm, first3 and start < 3 <= reach)
        if start < 3 <= reach: first3 = False
        best = max(best, reach)
        hours += t; gold += g*0.85*share
        st3[0] += lo; st3[1] += mid; st3[2] += up; charms += ch
        got[0] += lo; got[1] += mid; got[2] += up
        blocks += int(bag*0.7+rng.random())
        if hours-weekmark >= 10: weekmark = hours; weekbuy = 0
        while s < 13 and gold >= cost(s) and (free_stones or st3[tier(s)] >= 1):
            use_sac = sac and s >= 3 and blocks >= 3
            if charm and s >= 10 and charms == 0 and weekbuy < 3 and gold >= cost(s)+10000:
                gold -= 10000; charms += 1; weekbuy += 1
            gold -= cost(s); att += 1
            if not free_stones: st3[tier(s)] -= 1
            if use_sac: blocks -= 3
            p = 100 if s == 0 else min(95, P[s]+15*aura+(15 if use_sac else 0))
            r = rng.random()*100
            if r < p:
                if s > 0 and r < p*0.15:
                    s = min(13, s+2); slots = min(3, slots+1)
                else: s += 1
                aura = 0; peak = max(peak, s)
                for m in (5, 7, 10, 13):
                    if s >= m and m not in hit: hit[m] = hours
            else:
                if rng.random() < 0.25: aura = min(3, aura+1)
                else:
                    drop = s > 3 and (s-1) >= peak-2
                    if drop and charm and s >= 10 and charms > 0: charms -= 1
                    else:
                        if drop: s -= 1
                        aura = min(3, aura+1)
        if s < 13:
            if gold >= cost(s) and not free_stones and st3[tier(s)] < 1: wait_stone += t
            elif gold < cost(s): wait_gold += t
    return hit, slots, att, best, got, st3, wait_stone, hours, blocks

def rep(name, k, **kw):
    rng = random.Random(11); N = 1500
    res = [career(rng, k, **kw) for _ in range(N)]
    out = f' {name:40s}'
    for m in (5, 7, 10, 13):
        v = sorted(r[0].get(m, 4000) for r in res)
        out += f' +{m}: {st.mean(v):6.1f}/{v[N//2]:6.1f}/{v[int(N*.9)]:6.1f}'
    sl = [r[1] for r in res]
    out += f' | slots {[round(sl.count(i)/N*100) for i in range(4)]} att {st.mean(r[2] for r in res):.0f}'
    out += f' stoneWait {st.mean(r[6] for r in res):.1f}h leftover(lo/mid/up) {st.mean(r[5][0] for r in res):.0f}/{st.mean(r[5][1] for r in res):.0f}/{st.mean(r[5][2] for r in res):.0f} blocksLeft {st.mean(r[8] for r in res):.0f}'
    print(out)

def static(tmk):
    tm = TM[tmk]
    print(f'== static income, time model {tmk}')
    for name, start, reach in (('1->8', 0, 8), ('1->9', 0, 9), ('1->10', 0, 10), ('1->14', 0, 14), ('10->24', 10, 24), ('20->30', 20, 30), ('20->40', 20, 40), ('1->30', 0, 30)):
        rng = random.Random(1); n = 4000; acc = [0]*8
        for _ in range(n):
            r = run_stats(rng, start, reach, tm, False)
            for i in range(8): acc[i] += r[i]/n
        g, t, lo, mid, up, ch, bag, coins = acc
        print(f' {name:8s} gold/run {g:6.0f} min {t*60:5.1f} | /h: netgold {g*0.85/t:6.0f} lo {lo/t:4.1f} mid {mid/t:4.1f} up {up/t:4.2f} charm {ch/t:4.2f} bag {bag/t:4.1f} coins {coins/t:4.0f}')
    print(f'== normal player by stage (k=1.74), time model {tmk}')
    for s in (0, 3, 5, 7, 9, 10, 11, 12, 13):
        reach = int(invH(1.74*1.15**s)); start = (int(0.7*reach)//10)*10 if reach >= 15 else 0
        rng = random.Random(3); n = 3000; acc = [0]*8
        for _ in range(n):
            r = run_stats(rng, start, reach, tm, False)
            for i in range(8): acc[i] += r[i]
        g, t, lo, mid, up, ch, bag, coins = acc
        print(f' +{s}: reach {reach} start {start} run {t/n*60:.0f}min netgold/h {g*0.85/t:.0f} lo/h {lo/t:.1f} mid/h {mid/t:.1f} up/h {up/t:.2f} charm/h {ch/t:.2f} bag/h {bag/t:.1f}')

if __name__ == '__main__':
    for tmk in ('A', 'B'):
        static(tmk)
        print(f'== career hours mean/median/p90, time model {tmk}')
        rep('normal sac all', 1.74, tmk=tmk)
        rep('normal sac 70%', 1.74, tmk=tmk, share=0.7)
        rep('normal sac+charm all', 1.74, tmk=tmk, charm=True)
        rep('normal sac+charm 70%', 1.74, tmk=tmk, charm=True, share=0.7)
        rep('normal nosac all', 1.74, tmk=tmk, sac=False)
        rep('normal sac all, stones unlimited', 1.74, tmk=tmk, free_stones=True)
        rep('normal sac all, always max start', 1.74, tmk=tmk, smart=False)
        rep('beginner sac all', 1.42, tmk=tmk)
        rep('skilled sac all', 2.62, tmk=tmk)
    print('sum gold w1..30 (kills+clear only)', sum(gold_wave(w) for w in range(1, 31)))
