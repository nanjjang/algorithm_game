using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta.Enhance;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>
    /// 경제 시뮬레이션 모형(07 요약서 12.8절): docs/sim/online_econ3.py, online_econ3_multi.py, online_econ3_sens.py를 그대로 옮겼다.
    /// 강화 쪽(확률, 대성공 비율, 변이 비율, 비용, 강화석 등급, 하락 바닥, 부적·새 지팡이 값)은 규칙 함수와 데이터에서 읽고,
    /// 수입 쪽(웨이브 골드, 드랍 표, 판 길이, 도달 웨이브)은 그 스크립트의 모형 상수 그대로다(모형의 가정이지 게임 수치가 아니다).
    /// 난수는 Pcg32라 원본(파이썬 메르센 트위스터)과 표본이 다르다 — 그래서 명세의 허용 오차로 견준다.
    /// </summary>
    sealed class EconRandom
    {
        readonly Pcg32 _rng;
        public EconRandom(ulong seed) { _rng = new Pcg32(seed); }
        /// <summary>[0, 1).</summary>
        public double Next() => _rng.NextU32() / 4294967296.0;
        public double Gauss(double mu, double sigma)
        {
            double u1 = 1.0 - Next(), u2 = Next();
            return mu + sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }

    struct RunStats
    {
        public double Gold, Hours;
        public int Lo, Mid, Up, Charm, Bag, Coins;
    }

    sealed class CareerResult
    {
        /// <summary>+5, +7, +10, +13에 처음 닿은 시간(못 닿았으면 상한).</summary>
        public Dictionary<int, double> Hit = new Dictionary<int, double>();
        public int Slots, Attempts;
        public double Hours;
        public double HitOr(int stage, double fallback) => Hit.TryGetValue(stage, out double h) ? h : fallback;
    }

    sealed class EconWand
    {
        public int S, Peak, Aura, Slots;
    }

    enum MultiPolicy { Single, Leader, Even, Lift, Switch }

    struct Stat3
    {
        public double Mean, Median, P90;
        public static Stat3 Of(List<double> values)
        {
            var v = new List<double>(values);
            v.Sort();
            double sum = 0;
            foreach (double x in v) sum += x;
            int n = v.Count;
            return new Stat3 { Mean = sum / n, Median = v[n / 2], P90 = v[(int)(n * 0.9)] };
        }
    }

    sealed class EconModel
    {
        public const int Careers = 1500;          // 명세: 1,500명
        public const double MaxHours = 4000, MaxHoursMulti = 6000;
        const int BagPer = 3, BagMax = 12;        // 직접 넘긴 웨이브 3개마다 가방 1칸, 최대 12
        const double NetShare = 0.85;             // 판 중 지출을 뺀 순수입 비율(모형)
        const int SacrificeBlocks = 3, SacrificePct = 15, SacrificeFromStage = 3;
        const double WeekHours = 10;              // 모형: 플레이 10시간 = 상점의 한 주

        readonly EnhanceConfig _cfg;
        readonly EconomyConfig _econ;
        /// <summary>'A' = 기본 판 길이, 'B' = 빠른 쪽.</summary>
        public readonly char TimeModel;
        /// <summary>민감도(TV-E-60): 중급·상급 드랍을 이 비율로 솎는다.</summary>
        public double MidKeep = 1, UpKeep = 1;

        public EconModel(IContentDb db, char timeModel)
        {
            _cfg = db.Enhance;
            _econ = db.Economy;
            TimeModel = timeModel;
        }

        // ---------------------------------------------------------------- 수입 모형(online_econ3.py)

        public static double InvH(double h)
        {
            if (h <= 2.32) return Math.Max(1.0, 1 + (h - 1) / 0.12);
            return 12 + Math.Log(h / 2.32) / Math.Log(1.06);
        }
        static double S(int w) => 1 + 0.1 * (w - 1);
        static double B(int w) => w <= 12 ? 10 + 6 * w : Math.Min(200, 82 + 4 * (w - 12));
        public static double GoldWave(int w) => w % 10 == 0 ? 20 + 200 * S(w) : 2 * B(w) + 20;
        static int Elites(int w)
        {
            if (w % 10 == 0 || w < 12) return 0;
            return w < 20 ? 1 : w < 30 ? 2 : w < 40 ? 3 : 4;
        }
        static int Day(int w) => (w + 9) / 10;
        static double BlockValue(int day) => day <= 1 ? 7.2 : day == 2 ? 8.4 : 9.4;

        double WaveMinutes(int w) => TimeModel == 'A' ? Math.Min(2.2, 1.3 + 0.02 * w) : 1.1;
        double BossMinutes => TimeModel == 'A' ? 2.5 : 2.0;
        const double GapMinutes = 2.0;

        public RunStats Run(EconRandom rng, int start, int reach, bool first3)
        {
            double g = 0, t = GapMinutes;
            int lo = 0, mid = 0, up = 0, ch = 0, coins = 0;
            for (int w = start + 1; w <= reach; w++)
            {
                int d = Day(w);
                g += GoldWave(w);
                if (w % 10 == 0)
                {
                    t += BossMinutes; coins += 5 * Math.Min(d, 5);
                    if (d == 1)
                    {
                        lo += 1;
                        if (rng.Next() < 0.50) mid += 1;
                        if (rng.Next() < 0.02) ch += 1;
                    }
                    else if (d == 2)
                    {
                        mid += 1;
                        if (rng.Next() < 0.20) up += 1;
                        if (rng.Next() < 0.04) ch += 1;
                    }
                    else
                    {
                        mid += 1; up += 1;
                        if (rng.Next() < Math.Min(0.50, 0.10 * (d - 3))) up += 1;
                        if (rng.Next() < Math.Min(0.10, 0.02 * d)) ch += 1;
                    }
                }
                else
                {
                    t += WaveMinutes(w); coins += Math.Min(d, 5);
                    if (w % 10 == 5)
                    {
                        if (d == 1) lo += 1;
                        else if (d == 2) { lo += 1; mid += 1; }
                        else
                        {
                            mid += 1;
                            if (rng.Next() < Math.Min(0.25, 0.05 * (d - 2))) up += 1;
                        }
                    }
                    int elites = Elites(w);
                    for (int e = 0; e < elites; e++)
                    {
                        if (d == 2)
                        {
                            if (rng.Next() < 0.10) lo += 1;
                            if (rng.Next() < 0.08) mid += 1;
                        }
                        else if (d >= 3)
                        {
                            if (rng.Next() < 0.05) mid += 1;
                            if (rng.Next() < Math.Min(0.05, 0.02 + 0.005 * (d - 3))) up += 1;
                            if (rng.Next() < 0.005) ch += 1;
                        }
                    }
                }
                if (w == 3 && first3) lo += 3;
            }
            int n = reach - start;
            int bag = Math.Min(BagMax, n / BagPer);
            // 웨이브마다 카드 2장을 고르고, 가져가지 못한 것은 골드가 된다.
            g += Math.Max(0, 2 * n - bag) * BlockValue(Math.Min(3, Day(Math.Max(1, reach))));

            if (MidKeep < 1 || UpKeep < 1)
            {
                int mid2 = 0, up2 = 0;
                for (int i = 0; i < mid; i++) if (rng.Next() < MidKeep) mid2++;
                for (int i = 0; i < up; i++) if (rng.Next() < UpKeep) up2++;
                mid = mid2; up = up2;
            }
            return new RunStats { Gold = g, Hours = t / 60.0, Lo = lo, Mid = mid, Up = up, Charm = ch, Bag = bag, Coins = coins };
        }

        static int MaxStart(int best) => best >= 15 ? (int)(0.7 * best) / 10 * 10 : 0;

        static int PickStart(int best, int s, int[] stones)
        {
            int mx = MaxStart(best);
            if (s < 5 && stones[0] == 0) return Math.Min(mx, 10);   // 하급 강화석은 1~2일째에만 나온다
            return mx;
        }

        // ---------------------------------------------------------------- 강화 한 번(규칙 함수 그대로)

        long Cost(int s) => EnhanceRules.Cost(_cfg, s);
        int Tier(int s) => (int)EnhanceRules.StoneTierFor(_cfg, s);
        double ReachPower(double k, int stage, EconRandom rng) =>
            k * Math.Pow(_cfg.StageDamageBase, stage) * Math.Exp(rng.Gauss(0, 0.15));

        /// <summary>한 번 도전한다. 부적을 썼으면 true.</summary>
        bool Attempt(EconRandom rng, EconWand w, bool useSac, bool charm)
        {
            int s = w.S;
            int p = EnhanceRules.SuccessPct(_cfg, s, w.Aura, useSac ? SacrificePct : 0);
            double r = rng.Next() * 100;
            if (r < p)
            {
                if (s > 0 && r < p * _cfg.GreatRatioPct / 100.0)
                {
                    w.S = EnhanceRules.StageAfterGreat(_cfg, s);
                    w.Slots = Math.Min(_cfg.SlotExtraMax, w.Slots + 1);
                }
                else w.S = EnhanceRules.StageAfterSuccess(_cfg, s);
                w.Aura = 0;
                w.Peak = Math.Max(w.Peak, w.S);
                return false;
            }
            if (rng.Next() < _cfg.MutationRatioPct / 100.0)
            {
                w.Aura = Math.Min(_cfg.AuraMax, w.Aura + 1);
                return false;
            }
            bool drop = EnhanceRules.DropPossible(_cfg, s, w.Peak);
            if (drop && charm) return true;
            if (drop) w.S = s - 1;
            w.Aura = Math.Min(_cfg.AuraMax, w.Aura + 1);
            return false;
        }

        // ---------------------------------------------------------------- 한 자루(online_econ3.career)

        public CareerResult Career(EconRandom rng, double k, double share = 1.0, bool sac = true, bool charm = false,
            bool freeStones = false, bool smart = true)
        {
            var wand = new EconWand();
            double gold = 0, hours = 0, weekMark = 0;
            var stones = new int[3];
            int charms = 0, blocks = 0, best = 0, weekBuy = 0;
            bool first3 = true;
            var res = new CareerResult();
            int[] marks = { 5, 7, 10, 13 };

            while (wand.S < _cfg.MaxStage && hours < MaxHours)
            {
                int reach = Math.Max(1, (int)InvH(ReachPower(k, wand.S, rng)));
                int start = smart ? PickStart(best, wand.S, stones) : MaxStart(best);
                if (reach <= start) reach = start + 1;
                bool crossesThree = start < 3 && 3 <= reach;
                RunStats run = Run(rng, start, reach, first3 && crossesThree);
                if (crossesThree) first3 = false;
                best = Math.Max(best, reach);
                hours += run.Hours; gold += run.Gold * NetShare * share;
                stones[0] += run.Lo; stones[1] += run.Mid; stones[2] += run.Up; charms += run.Charm;
                blocks += (int)(run.Bag * 0.7 + rng.Next());
                if (hours - weekMark >= WeekHours) { weekMark = hours; weekBuy = 0; }

                while (wand.S < _cfg.MaxStage && gold >= Cost(wand.S) && (freeStones || stones[Tier(wand.S)] >= 1))
                {
                    int s = wand.S;
                    bool useSac = sac && s >= SacrificeFromStage && blocks >= SacrificeBlocks;
                    bool charmStage = charm && s >= 10;
                    if (charmStage && charms == 0 && weekBuy < _econ.ShopCharmWeeklyLimit && gold >= Cost(s) + _econ.ShopCharmGold)
                    {
                        gold -= _econ.ShopCharmGold; charms += 1; weekBuy += 1;
                    }
                    gold -= Cost(s); res.Attempts += 1;
                    if (!freeStones) stones[Tier(s)] -= 1;
                    if (useSac) blocks -= SacrificeBlocks;
                    if (Attempt(rng, wand, useSac, charmStage && charms > 0)) charms -= 1;
                    foreach (int m in marks)
                        if (wand.S >= m && !res.Hit.ContainsKey(m)) res.Hit[m] = hours;
                }
            }
            res.Slots = wand.Slots;
            res.Hours = hours;
            return res;
        }

        // ---------------------------------------------------------------- 여러 자루(online_econ3_multi.career_multi)

        static List<EconWand> StableSort(List<EconWand> wands, Comparison<EconWand> cmp)
        {
            var sorted = new List<EconWand>(wands);
            for (int i = 1; i < sorted.Count; i++)   // 삽입 정렬(안정)
            {
                EconWand x = sorted[i];
                int j = i - 1;
                while (j >= 0 && cmp(sorted[j], x) > 0) { sorted[j + 1] = sorted[j]; j--; }
                sorted[j + 1] = x;
            }
            return sorted;
        }

        static int ByLead(EconWand a, EconWand b) => a.S != b.S ? b.S.CompareTo(a.S) : b.Aura.CompareTo(a.Aura);
        static int ByLow(EconWand a, EconWand b) => a.S != b.S ? a.S.CompareTo(b.S) : b.Aura.CompareTo(a.Aura);

        static List<EconWand> Order(MultiPolicy policy, List<EconWand> wands, int target, int cur)
        {
            List<EconWand> byLead = StableSort(wands, ByLead);
            switch (policy)
            {
                case MultiPolicy.Single:
                case MultiPolicy.Leader:
                    return new List<EconWand> { byLead[0] };
                case MultiPolicy.Even:
                    return StableSort(wands, ByLow);
                case MultiPolicy.Lift:
                {
                    var low = new List<EconWand>();
                    foreach (EconWand w in StableSort(wands, ByLow)) if (w.S < target) low.Add(w);
                    low.Add(byLead[0]);
                    return low;
                }
                default:   // Switch: 실패할 때마다 다음 지팡이로
                {
                    int k = cur % wands.Count;
                    var rotated = new List<EconWand>();
                    for (int i = 0; i < wands.Count; i++) rotated.Add(wands[(k + i) % wands.Count]);
                    return rotated;
                }
            }
        }

        public CareerResult CareerMulti(EconRandom rng, double k, MultiPolicy policy, int n, int target = 7, double share = 1.0, bool sac = true)
        {
            var wands = new List<EconWand> { new EconWand() };
            double gold = 0, hours = 0;
            var stones = new int[3];
            int blocks = 0, best = 0, cur = 0;
            bool first3 = true;
            long price = _econ.ShopWandPlus0Gold;

            while (Top(wands) < _cfg.MaxStage && hours < MaxHoursMulti)
            {
                int reach = Math.Max(1, (int)InvH(ReachPower(k, Top(wands), rng)));
                int start = PickStart(best, Lowest(wands), stones);
                if (reach <= start) reach = start + 1;
                bool crossesThree = start < 3 && 3 <= reach;
                RunStats run = Run(rng, start, reach, first3 && crossesThree);
                if (crossesThree) first3 = false;
                best = Math.Max(best, reach);
                hours += run.Hours; gold += run.Gold * NetShare * share;
                stones[0] += run.Lo; stones[1] += run.Mid; stones[2] += run.Up;
                blocks += (int)(run.Bag * 0.7 + rng.Next());
                while (wands.Count < n && gold >= price) { gold -= price; wands.Add(new EconWand()); }

                while (true)
                {
                    EconWand w = null;
                    foreach (EconWand x in Order(policy, wands, target, cur))
                        if (stones[Tier(x.S)] >= 1) { w = x; break; }
                    if (w == null || gold < Cost(w.S)) break;
                    bool useSac = sac && w.S >= SacrificeFromStage && blocks >= SacrificeBlocks;
                    gold -= Cost(w.S); stones[Tier(w.S)] -= 1;
                    if (useSac) blocks -= SacrificeBlocks;
                    int before = w.S;
                    Attempt(rng, w, useSac, false);
                    if (policy == MultiPolicy.Switch && w.S <= before) cur += 1;
                    if (w.S >= _cfg.MaxStage) break;
                }
            }
            EconWand lead = wands[0];
            foreach (EconWand w in wands) if (w.S > lead.S) lead = w;
            return new CareerResult { Hours = hours, Slots = lead.Slots };
        }

        static int Top(List<EconWand> wands) { int m = 0; foreach (EconWand w in wands) m = Math.Max(m, w.S); return m; }
        static int Lowest(List<EconWand> wands) { int m = int.MaxValue; foreach (EconWand w in wands) m = Math.Min(m, w.S); return m; }

        // ---------------------------------------------------------------- 슬롯 낚시(online_econ3_multi.fish)

        public sealed class FishResult
        {
            public double Gold;
            public double[] StonesPerWand = new double[3];
            /// <summary>확장 칸이 k개 이상인 비율(k = 0..3).</summary>
            public double[] AtLeast = new double[4];
        }

        /// <summary>새 +0 지팡이를 +target 이상까지 올린다(제물 +15는 +4 도전부터).</summary>
        public FishResult Fish(int target, int wandCount, ulong seed)
        {
            var rng = new EconRandom(seed);
            double gold = 0;
            var attempts = new long[3];
            var count = new int[4];
            for (int i = 0; i < wandCount; i++)
            {
                var w = new EconWand();
                while (w.S < target)
                {
                    gold += Cost(w.S); attempts[Tier(w.S)] += 1;
                    Attempt(rng, w, w.S >= SacrificeFromStage, false);
                }
                count[w.Slots] += 1;
            }
            var res = new FishResult { Gold = gold / wandCount };
            for (int t = 0; t < 3; t++) res.StonesPerWand[t] = attempts[t] / (double)wandCount;
            for (int k = 0; k < 4; k++)
            {
                int sum = 0;
                for (int j = k; j < 4; j++) sum += count[j];
                res.AtLeast[k] = sum / (double)wandCount;
            }
            return res;
        }

        // ---------------------------------------------------------------- 시간당 수입(online_econ3.static)

        public struct Income
        {
            public double GoldPerRun, Minutes, NetGoldPerHour, LoPerHour, MidPerHour, UpPerHour, CharmPerHour;
        }

        public Income StaticIncome(int start, int reach, int runs, ulong seed)
        {
            var rng = new EconRandom(seed);
            double g = 0, t = 0, lo = 0, mid = 0, up = 0, ch = 0;
            for (int i = 0; i < runs; i++)
            {
                RunStats r = Run(rng, start, reach, false);
                g += r.Gold; t += r.Hours; lo += r.Lo; mid += r.Mid; up += r.Up; ch += r.Charm;
            }
            return new Income
            {
                GoldPerRun = g / runs, Minutes = t / runs * 60, NetGoldPerHour = g * NetShare / t,
                LoPerHour = lo / t, MidPerHour = mid / t, UpPerHour = up / t, CharmPerHour = ch / t,
            };
        }

        /// <summary>보통 플레이어(k)의 그 단계에서의 도달 웨이브와 시작 웨이브(건너뛴 수).</summary>
        public void ReachAndStart(double k, int stage, out int reach, out int start)
        {
            reach = (int)InvH(k * Math.Pow(_cfg.StageDamageBase, stage));
            start = MaxStart(reach);
        }
    }
}
