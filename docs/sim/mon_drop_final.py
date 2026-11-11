# monsters.md 6.6절의 기대값 표를 계산한다 (골드 6.1절, 재료·부적 표 6.3-B~D, 주화 6.4절, 귀환 가방 6.2.4절).
# 실행: python3 docs/sim/mon_drop_final.py
# 줄마다: 판 하나의 값 | 시간당 값. mats = [하급, 중급, 상급, 부적]. 시간당 판 수는 추정이다.
# 6절의 표를 바꾸면 이 스크립트의 mats(), gold(), coins(), run()을 같이 고치고 6.6절 표를 다시 적는다.
import math
def B(w): return 10+6*w if w<=12 else min(200, 82+4*(w-12))
def S(w): return 1+0.1*(w-1)
def elites(w):
    if w%10==0 or w<12: return 0
    return 1 if w<20 else 2 if w<30 else 3 if w<40 else 4
def gold(w): return 200*S(w)+20 if w%10==0 else 2*B(w)+20
def mats(w):
    d=math.ceil(w/10); lo=mid=hi=ch=0.0
    if w%10==5:
        if d==1: lo+=1
        elif d==2: lo+=1; mid+=1
        else: mid+=1; hi+=min(0.25,0.05*(d-2))
    if w%10==0:
        if d==1: lo+=1; mid+=0.5; ch+=0.02
        elif d==2: mid+=1; hi+=0.2; ch+=0.04
        else: mid+=1; hi+=1+min(0.5,0.1*(d-3)); ch+=min(0.10,0.02*d)
    e=elites(w)
    if e:
        if d==2: lo+=0.1*e; mid+=0.08*e
        elif d>=3: mid+=0.05*e; hi+=e*min(0.05,0.02+0.005*(d-3)); ch+=0.005*e
    return lo,mid,hi,ch
def coins(w):
    d=math.ceil(w/10); c=min(d,5); return c*5 if w%10==0 else c
def run(a,b):
    g=sum(gold(w) for w in range(a,b+1)); m=[sum(x) for x in zip(*[mats(w) for w in range(a,b+1)])]
    n=b-a+1; bag=min(12,n//3); hero=min(bag,n//10); c=sum(coins(w) for w in range(a,b+1))
    return g,m,bag,hero,c
for name,a,b,rph in [("입문 1-8",1,8,4),("보통 1-14",1,14,3.5),("숙련 1-30",1,30,1.5),("1-5",1,5,20),("1-10",1,10,12),("21-40",21,40,2.4),("1-36",1,36,1),("1-40",1,40,1),("1-60",1,60,1)]:
    g,m,bag,hero,c=run(a,b)
    print(name,"run: gold",round(g),"mats",[round(x,2) for x in m],"bag",bag,"hero",hero,"coin",c,"| /h: gold",round(g*rph),[round(x*rph,2) for x in m],"bag",round(bag*rph,1),"coin",round(c*rph))
