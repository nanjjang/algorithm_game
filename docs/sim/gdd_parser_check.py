# GDD 8절 판정기 규칙을 그대로 옮겨 입력열 표를 확인한다.
# 시각 단위: 1/15,360초 (틱 x 256 + 오프셋)
U = 15360
T = {0.07: 1075, 0.18: 2765, 0.2: 3072, 0.25: 3840, 0.3: 4608}
F, B, L, R = "앞", "뒤", "좌", "우"

COMMON = {
    (): ("마력 방출", 1),
    (F,): ("부채 전개", 1),
    (F, F): ("꿰뚫는 창", 2),
    (F, B): ("팔방 전개", 2),
    (F, B, B, F): ("만개", "all"),
    (B,): ("반동 사격", 1),
    (B, F): ("방열 폭발", 2),
    (L,): ("양익 사격", 1), (R,): ("양익 사격", 1),
    (L, L): ("잔상 걸음", 2), (R, R): ("잔상 걸음", 2),
}
UNIQUE = {
    "달구": {(B, B): "불가마"},
    "초롱": {(L, F): "벼락 질주", (R, F): "벼락 질주"},
    "갈피": {(F, L): "베껴 쓰기", (F, R): "베껴 쓰기"},
    "성에": {(B, L): "얼음 병풍", (B, R): "얼음 병풍"},
    "그믐": {(L, R): "그림자 손", (R, L): "그림자 손"},
}
JUST = {(F, F): ("뇌광창", 1)}
MANGAE = (F, B, B, F)
SIDE = (L, R)


def edge_gap(path, who):
    """path의 마지막 간선(path[-2] -> path[-1])의 기본 허용 간격."""
    if len(path) < 2:
        return None
    a, b = path[-2], path[-1]
    if path in (MANGAE[:3], MANGAE):
        return T[0.18]
    if a in SIDE and b == a:
        return T[0.25]
    if a == F and b == B:
        return T[0.2]
    if a in SIDE and b == F:
        return T[0.2]
    if a == F and b in SIDE:
        return T[0.2]
    if a in SIDE and b in SIDE and a != b:
        return T[0.2]
    if a == B and b == B and who == "달구" and path == (B, B):
        return T[0.2]
    return T[0.3]


class Parser:
    def __init__(self, who, unlocked=None, crystals=5, debt=0):
        self.who = who
        self.skills = dict(COMMON)
        self.unique = set()
        for p, n in UNIQUE[who].items():
            self.skills[p] = (n, 2)
            self.unique.add(p)
        self.nodes = {()}
        for p in self.skills:
            for i in range(1, len(p) + 1):
                self.nodes.add(p[:i])
        self.fail = {}
        for n in sorted(self.nodes, key=len):
            if not n:
                continue
            for k in range(1, len(n) + 1):
                if n[k:] in self.nodes:
                    self.fail[n] = n[k:]
                    break
        self.unlocked = unlocked  # None = 전부
        self.crystals, self.debt = crystals, debt
        self.reset()

    def reset(self):
        self.cur = ()
        self.times = []

    def is_unlocked(self, node):
        if node not in self.skills:
            return False
        return self.unlocked is None or self.skills[node][0] in self.unlocked

    def direction(self, d, t):
        if self.times and t - self.times[-1] > T[0.3]:
            self.reset()
        n = self.cur
        while n + (d,) not in self.nodes:
            if not n:
                break
            n = self.fail[n]
        self.cur = n + (d,) if n + (d,) in self.nodes else ()
        self.times = (self.times + [t])[-8:]

    def gaps_ok(self, node, t_k):
        """node의 깊이만큼의 최근 방향 입력으로 간격을 본다. (통과 여부, 늦은 입력 번호, 초과량)"""
        ts = self.times[-len(node):] if node else []
        if node == MANGAE:
            seq = ts + [t_k]
            for i in range(1, len(seq)):
                if seq[i] - seq[i - 1] > T[0.18]:
                    return False, i + 1, seq[i] - seq[i - 1] - T[0.18]
            return True, None, 0
        for i in range(1, len(ts)):
            if ts[i] - ts[i - 1] > edge_gap(node[: i + 1], self.who):
                return False, i + 1, 0
        return True, None, 0

    def pay(self, cost):
        """낼 수 있으면 내고 True. 그믐은 외상."""
        if cost == "all":
            if self.crystals < 3:
                return False
            self.crystals = 0
            return True
        if self.crystals >= cost:
            self.crystals -= cost
            return True
        if self.who == "그믐":
            need = cost - self.crystals
            if self.debt + need <= 2:
                self.crystals = 0
                self.debt += need
                return True
        return False

    def k(self, t):
        if self.times and t - self.times[-1] > T[0.3]:
            self.reset()
        n = self.cur
        just = False
        while True:
            if self.is_unlocked(n):
                ok, idx, over = self.gaps_ok(n, t)
                if n == MANGAE and not ok:
                    self.reset()
                    return "불발(만개 %d번째 입력이 %.3f초 늦음)" % (idx, over / U)
                if ok:
                    break
            n = self.fail[n]
        name, cost = self.skills[n]
        if n in JUST and self.times and 0 <= t - self.times[-1] <= T[0.07] \
                and (self.unlocked is None or JUST[n][0] in self.unlocked):
            name, cost = JUST[n]
            just = True
        res = None
        if self.pay(cost):
            res = name
        elif n in self.unique:
            pname, pcost = self.skills[n[:-1]]
            res = pname + "(부모 대체)" if self.pay(pcost) else "불발"
        else:
            res = "불발"
        self.reset()
        return res


def run(who, seq, **kw):
    """seq: [(키, 직전 입력과의 간격 초)]"""
    p = Parser(who, **kw)
    t = 100000
    out = None
    for key, gap in seq:
        t += round(gap * U)
        if key == "K":
            out = p.k(t)
        else:
            p.direction(key, t)
    return out, p.crystals, p.debt


g = 0.15
rows = [
    ("앞,앞,앞,K 전원", run("갈피", [(F, 0), (F, g), (F, g), ("K", g)])[0], "꿰뚫는 창"),
    ("앞,앞,옆,K 갈피", run("갈피", [(F, 0), (F, g), (L, g), ("K", g)])[0], "베껴 쓰기"),
    ("뒤,뒤,앞,K 달구", run("달구", [(B, 0), (B, g), (F, g), ("K", g)])[0], "방열 폭발"),
    ("좌,우,우,K 그믐", run("그믐", [(L, 0), (R, g), (R, g), ("K", g)])[0], "잔상 걸음"),
    ("앞,뒤,뒤,K 달구", run("달구", [(F, 0), (B, g), (B, g), ("K", g)])[0], "불가마"),
    ("앞,뒤,뒤,K 그 밖", run("성에", [(F, 0), (B, g), (B, g), ("K", g)])[0], "반동 사격"),
    ("뒤,뒤,K 결정1 달구", run("달구", [(B, 0), (B, g), ("K", g)], crystals=1)[0], "반동 사격(부모 대체)"),
    ("좌,우,K 0.2 안 그믐", run("그믐", [(L, 0), (R, 0.19), ("K", g)])[0], "그림자 손"),
    ("좌,우,K 0.2 밖 그믐", run("그믐", [(L, 0), (R, 0.22), ("K", g)])[0], "양익 사격"),
    ("옆,앞,K 0.2 초과 초롱", run("초롱", [(L, 0), (F, 0.22), ("K", g)])[0], "부채 전개"),
    ("좌,우,K 결정0 빚1 그믐", run("그믐", [(L, 0), (R, g), ("K", g)], crystals=0, debt=1), ("양익 사격(부모 대체)", 0, 2)),
    # 만개
    ("만개 전부 0.18 안", run("달구", [(F, 0), (B, 0.17), (B, 0.17), (F, 0.17), ("K", 0.17)])[0], "만개"),
    ("앞,뒤 0.17 → 팔방", run("달구", [(F, 0), (B, 0.17), ("K", g)])[0], "팔방 전개"),
    ("앞,뒤 0.19 → 팔방(0.2 안)", run("달구", [(F, 0), (B, 0.19), ("K", g)])[0], "팔방 전개"),
    ("앞,뒤 0.22 → 반동", run("달구", [(F, 0), (B, 0.22), ("K", g)])[0], "반동 사격"),
    ("만개 해금, 앞→뒤 0.19", run("달구", [(F, 0), (B, 0.19), (B, 0.17), (F, 0.17), ("K", 0.17)]), None),
    ("만개 해금, 앞→K 0.25", run("달구", [(F, 0), (B, 0.17), (B, 0.17), (F, 0.17), ("K", 0.25)]), None),
    ("만개 미해금", run("달구", [(F, 0), (B, 0.17), (B, 0.17), (F, 0.17), ("K", 0.17)],
                   unlocked={"마력 방출", "반동 사격", "불가마", "부채 전개", "방열 폭발", "팔방 전개"})[0], "방열 폭발"),
    ("만개 미해금·방열 미해금", run("달구", [(F, 0), (B, 0.17), (B, 0.17), (F, 0.17), ("K", 0.17)],
                         unlocked={"마력 방출", "반동 사격", "불가마", "부채 전개"})[0], "부채 전개"),
    ("만개 결정 2개", run("달구", [(F, 0), (B, 0.17), (B, 0.17), (F, 0.17), ("K", 0.17)], crystals=2)[0], "불발"),
    ("앞,앞,섬", run("달구", [(F, 0), (F, g), ("K", 0.06)])[0], "뇌광창"),
    ("앞,앞,K 늦음", run("달구", [(F, 0), (F, g), ("K", 0.09)])[0], "꿰뚫는 창"),
]
bad = 0
for name, got, want in rows:
    flag = "" if want is None or got == want else "  <-- 다름"
    bad += bool(flag)
    print(name, "=>", got, "| 기대", want, flag)
print("불일치", bad)

# "전원"과 "그 밖" 행은 다섯 캐릭터로 다시 돌린다
ALL = ["달구", "초롱", "갈피", "성에", "그믐"]
bad2 = 0
for w in ALL:
    chk = [
        (run(w, [(F, 0), (F, g), (F, g), ("K", g)])[0], "꿰뚫는 창"),
        (run(w, [(F, 0), (B, g), (B, g), ("K", g)])[0], "불가마" if w == "달구" else "반동 사격"),
        (run(w, [(F, 0), (B, 0.17), (B, 0.17), (F, 0.17), ("K", 0.17)], crystals=4)[0], "만개"),
        (run(w, [(F, 0), (B, 0.19), (B, 0.17), (F, 0.17), ("K", 0.17)])[0][:2], "불발"),
        (run(w, [(F, 0), (B, 0.19), ("K", g)])[0], "팔방 전개"),
        (run(w, [(F, 0), (B, 0.22), ("K", g)])[0], "반동 사격"),
        (run(w, [(F, 0), (B, 0.17), (B, 0.17), (F, 0.17), ("K", 0.17)],
             unlocked={"마력 방출", "반동 사격", "부채 전개", "방열 폭발", "팔방 전개"} | set(UNIQUE[w].values()))[0], "방열 폭발"),
    ]
    for got, want in chk:
        if got != want:
            bad2 += 1
            print(w, got, want)
print("전원 행 불일치", bad2)
