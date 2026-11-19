import random, statistics as st

P = [100, 90, 75, 60, 50, 40, 32, 25, 18, 12, 8, 5, 3]


def gold(s):
    return 100 * (s + 1) ** 2


def tier(s):
    return 0 if s < 5 else (1 if s < 9 else 2)


def run(rng, sac=0, charm_from=99, floor_gap=2, target=13, milestones=(5, 7, 10, 13)):
    s = 0; peak = 0; aura = 0; n = 0; g = 0; mats = [0, 0, 0]; charms = 0; hit = {}
    while s < target:
        n += 1; g += gold(s); mats[tier(s)] += 1
        p = 100 if s == 0 else min(95, P[s] + 15 * aura + sac)
        r = rng.random() * 100
        if r < p:
            if s > 0 and r < p * 0.15:
                s = min(13, s + 2)
            else:
                s += 1
            aura = 0; peak = max(peak, s)
            for m in milestones:
                if s >= m and m not in hit:
                    hit[m] = (n, g, tuple(mats), charms)
        else:
            if rng.random() < 0.25:
                aura = min(3, aura + 1)  # mutation: stage kept
            else:
                drop = s > 3 and (s - 1) >= peak - floor_gap
                if drop and s >= charm_from:
                    charms += 1  # charm blocks the drop, no aura
                else:
                    if drop:
                        s -= 1
                    aura = min(3, aura + 1)
    return hit


def report(name, **kw):
    rng = random.Random(7); N = 20000
    res = [run(rng, **kw) for _ in range(N)]
    print('==', name)
    for m in (5, 7, 10, 13):
        a = [r[m][0] for r in res]; g = sorted(r[m][1] for r in res)
        mt = [sum(r[m][2][i] for r in res) / N for i in range(3)]
        ch = sum(r[m][3] for r in res) / N
        print(f' +{m}: attempts mean {st.mean(a):.0f} med {st.median(a):.0f} | gold mean {st.mean(g):,.0f} med {st.median(g):,.0f} p90 {g[int(N*.9)]:,.0f} | mats {mt[0]:.1f}/{mt[1]:.1f}/{mt[2]:.1f} charms {ch:.1f}')


report('no sacrifice, no charm')
report('sacrifice +15', sac=15)
report('sacrifice +30', sac=30)
report('charm from +10', charm_from=10)
report('sac15 + charm from +10', sac=15, charm_from=10)
report('no floor (pure -1), none', floor_gap=99)
report('no floor, sac15', floor_gap=99, sac=15)
