using System;
using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Waves
{
    /// <summary>구성 생성의 강제 값(테스트·도구용). null인 칸은 보통대로 뽑는다.</summary>
    public sealed class PlanOverrides
    {
        /// <summary>변덕을 이 목록으로 고정한다(추첨하지 않는다).</summary>
        public List<string> Whims;
        /// <summary>틀을 이 id로 고정한다(각본 웨이브에는 먹지 않는다).</summary>
        public string Frame;
        /// <summary>
        /// 각본·틀의 고정 개수 뒤에 더 넣을 고정 개수(솔로 조정: 보스 시연, G09.json boss_preview). 예산 안이고 종 상한을 지킨다.
        /// 시간대·첫 등장 웨이브는 보지 않는다(각본의 고정 개수와 같다).
        /// </summary>
        public List<SpeciesCountDef> ExtraFixed;
    }

    /// <summary>구성 생성의 중간 값(테스트가 벡터를 확인할 때 본다).</summary>
    public sealed class PlanTrace
    {
        public int BaseBudget;
        /// <summary>큰 무리 추가분(없으면 0).</summary>
        public int ExtraBudget;
        /// <summary>추가분으로 넣은 엔트리의 첫 색인과 수(WavePlan.Entries 기준).</summary>
        public int ExtraFirstEntry;
        public int ExtraEntryCount;
    }

    /// <summary>
    /// 웨이브 구성 생성(순수 함수: 씨앗·웨이브가 같으면 같은 구성). 06 요약서 3.2~3.4절.
    /// 난수는 판 씨앗에서 (WavePlan 흐름, 웨이브, 갈래)로 갈라 낸다 — 웨이브마다 독립이라 야영 화면이 미리 만들어도,
    /// 여러 번 만들어도 같은 결과가 나오고, 변덕은 구성 갈래와 무관하게 정해진다(TV-093, TV-104).
    /// </summary>
    public static class WavePlanner
    {
        /// <summary>WavePlan 흐름의 갈래 번호(자료구조 크기).</summary>
        const int SubComposition = 0, SubFrame = 1, SubWhims = 2, SubCount = 3;
        /// <summary>waves.json script[].rest 값: 혼합 단계 없이 남은 예산을 전부 채움 종으로.</summary>
        const string RestFill = "fill";

        static Pcg32 RngFor(ulong seed, int wave, int sub) => Pcg32.Derive(seed, RngStream.WavePlan, wave * SubCount + sub);

        // ------------------------------------------------------------ 시간대·가용·가중치

        /// <summary>웨이브의 시간대 id(waves.json tod_ranges. 표에 없으면 DayCycle.Tod).</summary>
        public static string TodOf(IContentDb db, int wave)
        {
            int slot = DayCycle.Slot(wave);
            var ranges = db != null && db.Waves != null ? db.Waves.TodRanges : null;
            if (ranges != null)
                for (int i = 0; i < ranges.Count; i++)
                    if (slot >= ranges[i].From && slot <= ranges[i].To) return ranges[i].Tod;
            return DayCycle.Tod(wave);
        }

        static float TodMultiplier(IContentDb db, string speciesId, string tod)
        {
            var def = db.Tod(tod);
            if (def == null || def.Weights == null) return 1f;
            for (int i = 0; i < def.Weights.Count; i++)
                if (def.Weights[i].Species == speciesId) return def.Weights[i].Weight;
            return 1f;
        }

        static ModifierDef LongNightDef(IContentDb db, IList<string> whims)
        {
            if (whims == null) return null;
            for (int i = 0; i < whims.Count; i++)
            {
                var m = db.Modifier(whims[i]);
                if (m != null && m.Weights != null && m.Weights.Count > 0) return m;
            }
            return null;
        }

        /// <summary>그 종이 이 웨이브에 나올 수 있나: 첫 등장 웨이브, 시간대, 시간대 배율 ≠ 0. 졸개·허수아비는 구성에 들지 않는다.</summary>
        public static bool IsAvailable(IContentDb db, EnemyDef e, int wave, string tod)
        {
            if (e == null || e.Minion || e.Cost <= 0 || e.Id == Ids.Enemy.Dummy) return false;
            if (wave < e.FirstWave) return false;
            if (e.Times == null || !e.Times.Contains(tod)) return false;
            var todDef = db.Tod(tod);
            if (todDef != null && !todDef.NormalEnemies) return false;
            return TodMultiplier(db, e.Id, tod) > 0f;
        }

        /// <summary>가중치(정수) = 기본 100 × 시간대 배율 × 변덕(긴 밤) 배율. 가용하지 않아도 값은 센다(0이면 안 나온다).</summary>
        public static int Weight(IContentDb db, WaveTuning tuning, string speciesId, string tod, IList<string> whims)
        {
            double wgt = (tuning ?? new WaveTuning()).SpeciesBaseWeight * (double)TodMultiplier(db, speciesId, tod);
            var ln = LongNightDef(db, whims);
            if (ln != null && (ln.OnlyTod == null || ln.OnlyTod == tod))
                for (int i = 0; i < ln.Weights.Count; i++)
                    if (ln.Weights[i].Species == speciesId) wgt *= ln.Weights[i].Weight;
            return (int)Math.Round(wgt, MidpointRounding.AwayFromZero);
        }

        /// <summary>한 웨이브 구성 전체의 종 상한(엘리트 포함). 0 = 상한 없음. 변덕(긴 밤)의 species_caps가 덮어쓴다.</summary>
        public static int SpeciesCap(IContentDb db, string speciesId, IList<string> whims)
        {
            if (whims != null)
                for (int i = 0; i < whims.Count; i++)
                {
                    var m = db.Modifier(whims[i]);
                    if (m == null || m.SpeciesCaps == null) continue;
                    for (int k = 0; k < m.SpeciesCaps.Count; k++)
                        if (m.SpeciesCaps[k].Species == speciesId) return m.SpeciesCaps[k].Cap;
                }
            var caps = db.Waves.SpeciesCap;
            if (caps != null)
                for (int i = 0; i < caps.Count; i++)
                    if (caps[i].Species == speciesId) return caps[i].Cap;
            var def = db.Enemy(speciesId);
            return def != null && def.SpeciesCap.HasValue ? def.SpeciesCap.Value : 0;
        }

        // ------------------------------------------------------------ 변덕·틀

        /// <summary>변덕 후보(데이터 순서). only_tod가 있는 변덕(긴 밤)은 그 시간대에만 든다.</summary>
        public static List<string> WhimPool(IContentDb db, string tod)
        {
            var pool = new List<string>();
            var mods = db.Modifiers;
            for (int i = 0; i < mods.Count; i++)
                if (mods[i].OnlyTod == null || mods[i].OnlyTod == tod) pool.Add(mods[i].Id);
            return pool;
        }

        /// <summary>그 웨이브의 변덕(중복 없이 균등). 보스 웨이브·20 이하는 없다. 구성 갈래와 무관한 난수로 뽑는다.</summary>
        public static List<string> PickWhims(IContentDb db, ulong seed, int wave)
        {
            var result = new List<string>();
            int n = WaveCurves.ModifierCount(db.Waves, wave);
            if (n <= 0) return result;
            var pool = WhimPool(db, TodOf(db, wave));
            var rng = RngFor(seed, wave, SubWhims);
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                int k = rng.Range(0, pool.Count);
                result.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return result;
        }

        static WaveScriptDef FindScript(IContentDb db, int wave)
        {
            var s = db.Waves.Script;
            if (s == null) return null;
            for (int i = 0; i < s.Count; i++) if (s[i].Wave == wave) return s[i];
            return null;
        }

        /// <summary>틀의 주 묶음에 가용한 종이 하나라도 있나(주 묶음이 없는 혼합은 항상 된다).</summary>
        public static bool FrameAvailable(IContentDb db, FrameDef frame, int wave)
        {
            if (frame == null) return false;
            if (frame.Group == null || frame.Group.Count == 0) return true;
            string tod = TodOf(db, wave);
            for (int i = 0; i < frame.Group.Count; i++)
                if (IsAvailable(db, db.Enemy(frame.Group[i]), wave, tod)) return true;
            return false;
        }

        /// <summary>
        /// 그 웨이브의 틀 id(보스 웨이브는 null). 각본 웨이브는 각본의 틀, 그 밖은 직전 일반 웨이브의 틀이 아닌 후보 중 균등.
        /// 직전 결과에 기대므로 처음부터 차례로 센다(TV-091, TV-092).
        /// </summary>
        public static string FrameOf(IContentDb db, ulong seed, int wave)
        {
            if (DayCycle.IsBossWave(wave)) return null;
            string prev = null;
            for (int v = 1; v <= wave; v++)
            {
                if (DayCycle.IsBossWave(v)) continue;
                var script = FindScript(db, v);
                prev = script != null ? script.Frame : PickFrame(db, seed, v, prev);
            }
            return prev;
        }

        static string PickFrame(IContentDb db, ulong seed, int wave, string prevFrame)
        {
            var frames = db.Frames;
            var cand = new List<string>();
            for (int i = 0; i < frames.Count; i++)
                if (frames[i].Id != prevFrame && FrameAvailable(db, frames[i], wave)) cand.Add(frames[i].Id);
            if (cand.Count == 0) return Ids.Frame.Mixed;
            return cand[RngFor(seed, wave, SubFrame).Range(0, cand.Count)];
        }

        // ------------------------------------------------------------ 구성

        /// <summary>
        /// 웨이브 구성을 만든다. bosses = 보스 고르기(null이거나 null을 돌려주면 bosses.json의 순환 순서).
        /// 06 요약서 3.2절의 순서 1~11.
        /// </summary>
        public static WavePlan Build(IContentDb db, WaveTuning tuning, BossModule bosses, ulong seed, int wave, bool phantomFirst,
            PlanOverrides overrides = null, PlanTrace trace = null)
        {
            if (wave < 1) wave = 1;
            tuning = tuning ?? new WaveTuning();
            var plan = new WavePlan { Wave = wave, Tod = TodOf(db, wave) };
            if (DayCycle.IsBossWave(wave))
            {
                // 보스 웨이브: 일반 적·엘리트·변덕·예산 없음(06 요약서 2.1절)
                plan.PhantomFirst = phantomFirst;
                plan.BossId = bosses != null ? bosses.SelectBoss(db, wave, phantomFirst) : null;
                if (plan.BossId == null) plan.BossId = DefaultBoss(db, wave, phantomFirst);
                return plan;
            }
            var b = new Builder(db, tuning, RngFor(seed, wave, SubComposition), plan);
            b.Run(seed, overrides, trace);
            return plan;
        }

        static string DefaultBoss(IContentDb db, int wave, bool phantomFirst)
        {
            if (phantomFirst) return Ids.Boss.MirrorPhantom;
            var list = db.Bosses;
            if (list == null || list.Count == 0) return Ids.Boss.KilnBoar;
            return list[DayCycle.BossRotationIndex(wave, list.Count)].Id;
        }

        /// <summary>함께 나오는 단위(낱마리 또는 박쥐 무리). 묶음 나누기는 단위째 옮긴다.</summary>
        struct Unit
        {
            public int First, Count, Cost;
            public bool Elite;
        }

        sealed class Builder
        {
            readonly IContentDb _db;
            readonly WaveTuning _tuning;
            readonly Pcg32 _rng;
            readonly WavePlan _plan;
            readonly WavesDef _waves;
            readonly List<EnemyDef> _avail = new List<EnemyDef>();
            readonly List<int> _weights = new List<int>();
            readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
            readonly List<Unit> _units = new List<Unit>();
            readonly List<EnemyDef> _candBuf = new List<EnemyDef>();
            readonly List<int> _candWeights = new List<int>();

            public Builder(IContentDb db, WaveTuning tuning, Pcg32 rng, WavePlan plan)
            {
                _db = db; _tuning = tuning; _rng = rng; _plan = plan; _waves = db.Waves;
            }

            public void Run(ulong seed, PlanOverrides ov, PlanTrace trace)
            {
                int wave = _plan.Wave;
                string tod = _plan.Tod;

                // 1. 예산
                int budget = _tuning.Budget(_waves, wave);
                int remaining = budget;

                // 2. 변덕(구성과 다른 갈래의 난수) → 긴 밤이면 가중치·상한에 반영
                _plan.Whims.AddRange(ov != null && ov.Whims != null ? ov.Whims : PickWhims(_db, seed, wave));
                ExtraBudgetDef extraDef = null;
                int extraElites = 0;
                for (int i = 0; i < _plan.Whims.Count; i++)
                {
                    var m = _db.Modifier(_plan.Whims[i]);
                    if (m == null) continue;
                    if (m.ExtraBudget != null) extraDef = m.ExtraBudget;
                    extraElites += m.ExtraElites;
                }
                int extra = extraDef != null ? budget * extraDef.Percent / WaveCurves.PercentOne : 0;

                // 틀: 각본 웨이브는 각본, 그 밖은 차례로 센 값
                var script = FindScript(_db, wave);
                FrameDef frame = null;
                if (script != null) _plan.Frame = script.Frame;
                else
                {
                    _plan.Frame = ov != null && ov.Frame != null ? ov.Frame : FrameOf(_db, seed, wave);
                    frame = _db.Frame(_plan.Frame);
                }

                // 가용 종과 가중치(데이터 순서 고정)
                var all = _db.Enemies;
                for (int i = 0; i < all.Count; i++)
                {
                    if (!IsAvailable(_db, all[i], wave, tod)) continue;
                    int wgt = Weight(_db, _tuning, all[i].Id, tod, _plan.Whims);
                    if (wgt <= 0) continue;
                    _avail.Add(all[i]);
                    _weights.Add(wgt);
                }

                // 3. 엘리트(예산 안): 비용 ×3이 남은 예산에 들고 종 상한 미만인 종에서 가중치 추첨
                int eliteCostMul = Math.Max(1, _waves.EliteCostMul);
                int elites = WaveCurves.EliteCount(_waves, wave);
                for (int i = 0; i < elites; i++)
                {
                    var e = Pick(null, false, remaining / eliteCostMul, true);
                    if (e == null) break;
                    AddSingle(e, true, false);
                    remaining -= e.Cost * eliteCostMul;
                }

                // 4. 고정 개수(각본 또는 틀). 박쥐는 "무리 하나"로 센다
                var fixedList = script != null ? script.Fixed : frame != null ? frame.Fixed : null;
                if (fixedList != null)
                    for (int i = 0; i < fixedList.Count; i++)
                    {
                        var def = _db.Enemy(fixedList[i].Species);
                        if (def == null || def.Cost <= 0) continue;
                        // 틀의 고정 종이 지금 시간대·웨이브에 없으면 빠지고 그 몫은 혼합으로 간다
                        if (script == null && !IsAvailable(_db, def, wave, tod)) continue;
                        int count = _rng.Range(fixedList[i].Min, fixedList[i].Max + 1);
                        for (int k = 0; k < count; k++)
                        {
                            if (def.Cost > remaining || !UnderCap(def)) break;
                            remaining -= AddUnit(def, remaining);
                        }
                    }

                // 4b. 솔로 조정: 보스 시연 고정 개수(PlanOverrides.ExtraFixed)
                var extraFixed = ov != null ? ov.ExtraFixed : null;
                if (extraFixed != null)
                    for (int i = 0; i < extraFixed.Count; i++)
                    {
                        var def = _db.Enemy(extraFixed[i].Species);
                        if (def == null || def.Cost <= 0) continue;
                        int count = _rng.Range(extraFixed[i].Min, extraFixed[i].Max + 1);
                        for (int k = 0; k < count; k++)
                        {
                            if (def.Cost > remaining || !UnderCap(def)) break;
                            remaining -= AddUnit(def, remaining);
                        }
                    }

                // 5. 주 묶음: 목표 = B × 몫 / 100(내림). 남은 예산을 넘지 않는다. 못 쓴 몫은 혼합으로 넘어간다
                var group = script != null ? script.Group : frame != null ? frame.Group : null;
                int share = script != null ? script.GroupShare : frame != null ? frame.GroupShare : 0;
                if (group != null && group.Count > 0 && share > 0)
                {
                    int left = Math.Min(budget * share / WaveCurves.PercentOne, remaining);
                    while (left > 0)
                    {
                        var e = Pick(group, true, left, false);
                        if (e == null) break;
                        int used = AddUnit(e, left);
                        left -= used;
                        remaining -= used;
                    }
                }

                // 6. 나머지(혼합): 주 묶음 밖의 가용 종 전체. rest = fill이면 건너뛴다
                bool fillOnly = script != null && script.Rest == RestFill;
                if (!fillOnly)
                    while (remaining > 0)
                    {
                        var e = Pick(group, false, remaining, false);
                        if (e == null) break;
                        remaining -= AddUnit(e, remaining);
                    }

                // 7. 남은 예산은 전부 채움 종(이끼 슬라임)으로 → 비용 합 = B
                var fill = _db.Enemy(_waves.FillSpecies);
                remaining -= Fill(fill, remaining);

                // 8. 큰 무리 추가분: 비용이 cost_max 이하인 가용 종으로, 남으면 채움 종
                if (trace != null)
                {
                    trace.BaseBudget = budget;
                    trace.ExtraBudget = extra;
                    trace.ExtraFirstEntry = _plan.Entries.Count;
                }
                if (extra > 0)
                {
                    int left = extra;
                    while (left > 0)
                    {
                        var e = Pick(null, false, Math.Min(left, extraDef.CostMax), false);
                        if (e == null) break;
                        left -= AddUnit(e, left);
                    }
                    if (fill != null && fill.Cost <= extraDef.CostMax) Fill(fill, left);
                }
                if (trace != null) trace.ExtraEntryCount = _plan.Entries.Count - trace.ExtraFirstEntry;

                // 9. 우두머리 행차: 엘리트 추가(예산 밖, 예산 조건 없음. 종 상한은 지킨다)
                for (int i = 0; i < extraElites; i++)
                {
                    var e = Pick(null, false, int.MaxValue, true);
                    if (e == null) break;
                    AddSingle(e, true, true);
                }

                _plan.Budget = budget + extra;

                // 11. 묶음 나누기
                AssignBundles();

                // 야영 미리보기: 나오는 종(중복 없이, 데이터 순서)
                for (int i = 0; i < all.Count; i++)
                    if (_counts.ContainsKey(all[i].Id)) _plan.PreviewSpecies.Add(all[i].Id);
            }

            bool UnderCap(EnemyDef e)
            {
                int cap = SpeciesCap(_db, e.Id, _plan.Whims);
                if (cap <= 0) return true;
                int n;
                _counts.TryGetValue(e.Id, out n);
                return n < cap;
            }

            /// <summary>가중치 추첨. group != null이면 inGroup에 따라 그 안/밖만. 비용 ≤ maxCost, 종 상한 미만. 없으면 null.</summary>
            EnemyDef Pick(List<string> group, bool inGroup, int maxCost, bool eliteOnly)
            {
                _candBuf.Clear();
                _candWeights.Clear();
                int total = 0;
                for (int i = 0; i < _avail.Count; i++)
                {
                    var e = _avail[i];
                    if (e.Cost > maxCost) continue;
                    if (group != null && group.Contains(e.Id) != inGroup) continue;
                    if (group == null && inGroup) continue;
                    if (eliteOnly && _db.Elite(e.Id) == null) continue;
                    if (!UnderCap(e)) continue;
                    _candBuf.Add(e);
                    _candWeights.Add(_weights[i]);
                    total += _weights[i];
                }
                if (total <= 0) return null;
                int r = _rng.Range(0, total);
                for (int i = 0; i < _candBuf.Count; i++)
                {
                    r -= _candWeights[i];
                    if (r < 0) return _candBuf[i];
                }
                return _candBuf[_candBuf.Count - 1];
            }

            void AddEntry(EnemyDef e, bool elite, bool outside)
            {
                _plan.Entries.Add(new WaveEntry { Species = e.Id, Elite = elite, OutsideBudget = outside });
                int n;
                _counts.TryGetValue(e.Id, out n);
                _counts[e.Id] = n + 1;
            }

            void AddSingle(EnemyDef e, bool elite, bool outside)
            {
                int cost = elite ? e.Cost * Math.Max(1, _waves.EliteCostMul) : e.Cost;
                _units.Add(new Unit { First = _plan.Entries.Count, Count = 1, Cost = cost, Elite = elite });
                AddEntry(e, elite, outside);
            }

            /// <summary>일반 개체 한 단위를 넣고 쓴 비용을 돌려준다. 무리로 나오는 종(박쥐)은 6~8마리,
            /// 남은 몫이 모자라면 남은 몫만큼(최소 1). 부르는 쪽이 cost ≤ left를 확인했다.</summary>
            int AddUnit(EnemyDef e, int left)
            {
                var pack = e.SpawnPack;
                if (pack == null || pack.Count < 2)
                {
                    AddSingle(e, false, false);
                    return e.Cost;
                }
                int k = _rng.Range(pack[0], pack[1] + 1);
                k = Math.Min(k, left / e.Cost);
                if (k < 1) k = 1;
                _units.Add(new Unit { First = _plan.Entries.Count, Count = k, Cost = k * e.Cost, Elite = false });
                for (int i = 0; i < k; i++) AddEntry(e, false, false);
                return k * e.Cost;
            }

            /// <summary>채움 종을 낱마리로 채운다(무리로 묶지 않는다). 쓴 비용을 돌려준다.</summary>
            int Fill(EnemyDef fill, int left)
            {
                if (fill == null || fill.Cost <= 0) return 0;
                int used = 0;
                while (left - used >= fill.Cost)
                {
                    AddSingle(fill, false, false);
                    used += fill.Cost;
                }
                return used;
            }

            /// <summary>
            /// 묶음 나누기(06 요약서 3.2절 11): 일반 단위를 섞은 뒤 묶음 비용 합이 ceil(총비용 ÷ 묶음 수)를 넘지 않게 앞에서부터 채운다.
            /// 박쥐 무리는 한 묶음에 함께. 엘리트는 마지막 묶음부터 거꾸로 하나씩.
            /// </summary>
            void AssignBundles()
            {
                int bundles = Math.Max(1, WaveCurves.BundleCount(_waves, _plan.Wave));
                _plan.BundleCount = bundles;
                var normal = new List<Unit>();
                var elite = new List<Unit>();
                int total = 0;
                for (int i = 0; i < _units.Count; i++)
                {
                    total += _units[i].Cost;
                    if (_units[i].Elite) elite.Add(_units[i]); else normal.Add(_units[i]);
                }
                for (int i = normal.Count - 1; i > 0; i--)
                {
                    int k = _rng.Range(0, i + 1);
                    var t = normal[i]; normal[i] = normal[k]; normal[k] = t;
                }
                int limit = (total + bundles - 1) / bundles;
                int bundle = 0, acc = 0;
                for (int i = 0; i < normal.Count; i++)
                {
                    var u = normal[i];
                    if (acc > 0 && acc + u.Cost > limit && bundle < bundles - 1) { bundle++; acc = 0; }
                    SetBundle(u, bundle);
                    acc += u.Cost;
                }
                for (int i = 0; i < elite.Count; i++)
                    SetBundle(elite[i], bundles - 1 - i % bundles);
            }

            void SetBundle(Unit u, int bundle)
            {
                for (int i = 0; i < u.Count; i++)
                {
                    var e = _plan.Entries[u.First + i];
                    e.Bundle = bundle;
                    _plan.Entries[u.First + i] = e;
                }
            }
        }
    }
}
