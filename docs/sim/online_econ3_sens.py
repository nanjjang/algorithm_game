import random, statistics as st, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import online_econ3 as m
orig = m.run_stats
def make(fm, fu):
    def rs(rng, start, reach, tm, first3):
        g,t,lo,mid,up,ch,bag,c = orig(rng,start,reach,tm,first3)
        mid2 = sum(1 for _ in range(mid) if rng.random()<fm); up2 = sum(1 for _ in range(up) if rng.random()<fu)
        return g,t,lo,mid2,up2,ch,bag,c
    return rs
for fm,fu in ((1,1),(0.2,1),(0.1,0.33),(0.1,0.2)):
    m.run_stats = make(fm,fu)
    print('mid x',fm,'up x',fu); m.rep('normal sac all',1.74); m.rep('normal sac+charm',1.74,charm=True)
