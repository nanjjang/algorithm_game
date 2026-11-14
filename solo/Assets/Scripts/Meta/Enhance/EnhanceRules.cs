using System;
using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Meta.Enhance
{
    /// <summary>강화석 등급(07 요약서 4.3절).</summary>
    public enum StoneTier { Low = 0, Mid = 1, High = 2 }

    /// <summary>난수 한 번이 떨어진 구간(07 요약서 4.5절).</summary>
    public enum JudgeBand { GreatSuccess, Success, Mutation, Fail }

    /// <summary>강화 버튼 위의 실패 안내 한 줄(07 요약서 4.4절, 08 요약서 10.4절).</summary>
    public enum FailHint { NoDropLow, AtFloor, DropTo, CharmProtects }

    /// <summary>제물 보너스의 내역(07 요약서 4.2절). 상한은 등급 합 + 같은 속성의 "합계"에 건다(07 13절 M-1).</summary>
    public struct SacrificeBreakdown
    {
        /// <summary>등급 점수 합(상한 전).</summary>
        public int GradeSum;
        /// <summary>같은 속성 보너스(시도당 한 번. 0 또는 same_element).</summary>
        public int SameElementPct;
        /// <summary>판정에 쓰는 값 = min(cap, GradeSum + SameElementPct).</summary>
        public int TotalPct;
        /// <summary>내역의 "제물 합" 줄 = TotalPct − SameElementPct(두 줄 합이 상한을 넘지 않게).</summary>
        public int GradeLinePct => TotalPct - SameElementPct;
        public int Count;
    }

    /// <summary>
    /// 강화의 순수 규칙(07 요약서 4절·6절). 프로필을 바꾸지 않는다. 수치는 전부 EnhanceConfig(solo/enhance.json)에서 온다.
    /// 다섯 지팡이가 같은 확률·비용·실패 규칙을 쓴다 — 어느 함수도 캐릭터를 받아 확률이나 비용을 가르지 않는다
    /// (캐릭터가 드는 곳은 제물의 같은 속성 판정과 변이 보유 상한뿐이다).
    /// </summary>
    public static partial class EnhanceRules
    {
        /// <summary>1% = 100bp.</summary>
        public const int BpPerPct = 100;
        /// <summary>판정 난수의 범위 [0, 10000).</summary>
        public const int BpFull = 10000;
        public const int PctFull = 100;

        // ------------------------------------------------------------------ 확률 (4.2절)

        /// <summary>현재 단계의 기본 확률(%). 범위 밖이면 0.</summary>
        public static int BasePct(EnhanceConfig cfg, int stage)
        {
            if (cfg == null || cfg.BaseProb == null || stage < 0 || stage >= cfg.BaseProb.Count) return 0;
            return cfg.BaseProb[stage];
        }

        /// <summary>등급 하나의 제물 점수. 모르는 등급은 0.</summary>
        public static int SacrificeScore(EnhanceConfig cfg, string rarity)
        {
            if (rarity == Ids.Rarity.Common) return cfg.Sacrifice.ScoreCommon;
            if (rarity == Ids.Rarity.Rare) return cfg.Sacrifice.ScoreRare;
            if (rarity == Ids.Rarity.Hero) return cfg.Sacrifice.ScoreHero;
            return 0;
        }

        /// <summary>등급 합과 같은 속성 여부로 제물 보너스를 만든다.</summary>
        public static SacrificeBreakdown SacrificeBonus(EnhanceConfig cfg, int gradeSum, bool sameElement, int count)
        {
            int elem = sameElement ? cfg.Sacrifice.SameElement : 0;
            return new SacrificeBreakdown
            {
                GradeSum = gradeSum,
                SameElementPct = elem,
                TotalPct = Math.Min(cfg.Sacrifice.Cap, gradeSum + elem),
                Count = count,
            };
        }

        /// <summary>제물 블록 id 목록의 보너스. 콘텐츠에 없는 id는 점수 0으로 센다(유효성은 서비스가 따로 본다).</summary>
        public static SacrificeBreakdown SacrificeBonus(EnhanceConfig cfg, IContentDb db, IReadOnlyList<string> blocks, string wandElement)
        {
            int sum = 0;
            bool same = false;
            int count = blocks != null ? blocks.Count : 0;
            for (int i = 0; i < count; i++)
            {
                BlockDef def = db != null ? db.Block(blocks[i]) : null;
                if (def == null) continue;
                sum += SacrificeScore(cfg, def.Rarity);
                if (wandElement != null && def.Element == wandElement) same = true;
            }
            return SacrificeBonus(cfg, sum, same, count);
        }

        public static int ClampAura(EnhanceConfig cfg, int aura) => aura < 0 ? 0 : (aura > cfg.AuraMax ? cfg.AuraMax : aura);

        public static int AuraPct(EnhanceConfig cfg, int aura) => cfg.AuraStepPct * ClampAura(cfg, aura);

        /// <summary>상한 전 합(기본 + 제물 + 기운). +1 도전은 기본값 그대로.</summary>
        public static int RawPct(EnhanceConfig cfg, int stage, int aura, int sacrificePct)
        {
            if (stage == 0) return BasePct(cfg, 0);
            return BasePct(cfg, stage) + sacrificePct + AuraPct(cfg, aura);
        }

        /// <summary>최종 성공 확률(%). +1 도전은 상한의 예외(100). 하한은 없다.</summary>
        public static int SuccessPct(EnhanceConfig cfg, int stage, int aura, int sacrificePct)
        {
            if (stage == 0) return BasePct(cfg, 0);
            return Math.Min(cfg.ProbCapPct, RawPct(cfg, stage, aura, sacrificePct));
        }

        /// <summary>"상한" 표시: +1 도전이 아니고 합이 상한에 닿았다.</summary>
        public static bool IsCapped(EnhanceConfig cfg, int stage, int aura, int sacrificePct) =>
            stage != 0 && RawPct(cfg, stage, aura, sacrificePct) >= cfg.ProbCapPct;

        // ------------------------------------------------------------------ 판정 구간 (4.5절)

        public static int SuccessBp(int successPct) => successPct * BpPerPct;

        /// <summary>대성공 구간의 끝 = 성공 확률의 great_ratio_pct. +1 도전은 0.</summary>
        public static int GreatBp(EnhanceConfig cfg, int stage, int successPct) =>
            stage == 0 ? 0 : successPct * BpPerPct * cfg.GreatRatioPct / PctFull;

        /// <summary>변이 구간의 끝 = 성공 구간 끝 + 실패의 mutation_ratio_pct.</summary>
        public static int MutationEndBp(EnhanceConfig cfg, int successPct)
        {
            int p = SuccessBp(successPct);
            return p + (BpFull - p) * cfg.MutationRatioPct / PctFull;
        }

        /// <summary>난수 한 번의 구간. 전부 "미만"으로 가른다(07 13절 M-3).</summary>
        public static JudgeBand Judge(int rollBp, int successBp, int greatBp, int mutationEndBp)
        {
            if (rollBp < greatBp) return JudgeBand.GreatSuccess;
            if (rollBp < successBp) return JudgeBand.Success;
            if (rollBp < mutationEndBp) return JudgeBand.Mutation;
            return JudgeBand.Fail;
        }

        // ------------------------------------------------------------------ 비용 (4.3절)

        /// <summary>골드 = cost_coeff × (현재 단계 + 1)².</summary>
        public static long Cost(EnhanceConfig cfg, int stage)
        {
            long n = stage + 1;
            return cfg.CostCoeff * n * n;
        }

        public static StoneTier StoneTierFor(EnhanceConfig cfg, int stage)
        {
            if (stage <= cfg.StoneTierBounds[0]) return StoneTier.Low;
            if (stage <= cfg.StoneTierBounds[1]) return StoneTier.Mid;
            return StoneTier.High;
        }

        public static string StoneId(StoneTier tier) =>
            tier == StoneTier.Low ? Ids.Item.StoneLow : tier == StoneTier.Mid ? Ids.Item.StoneMid : Ids.Item.StoneHigh;

        // ------------------------------------------------------------------ 하락 (4.4절)

        /// <summary>실패하면 내려갈 수 있는가: stage &gt; drop_safe_max_stage 이고 stage − 1 ≥ floor_peak − floor_gap.</summary>
        public static bool DropPossible(EnhanceConfig cfg, int stage, int floorPeak) =>
            stage > cfg.DropSafeMaxStage && stage - 1 >= floorPeak - cfg.FloorGap;

        /// <summary>화면에 적는 바닥 = max(drop_safe_max_stage, floor_peak − floor_gap).</summary>
        public static int DisplayFloor(EnhanceConfig cfg, int floorPeak) =>
            Math.Max(cfg.DropSafeMaxStage, floorPeak - cfg.FloorGap);

        public static FailHint HintFor(EnhanceConfig cfg, int stage, int floorPeak, bool useCharm)
        {
            if (stage <= cfg.DropSafeMaxStage) return FailHint.NoDropLow;
            if (!DropPossible(cfg, stage, floorPeak)) return FailHint.AtFloor;
            return useCharm ? FailHint.CharmProtects : FailHint.DropTo;
        }

        /// <summary>일반 실패의 결과(4.6절 표의 실패 세 줄). 변이 거절의 결과도 이것이다.</summary>
        public static FallbackResult ResolveFail(EnhanceConfig cfg, int stage, int floorPeak, int aura, bool useCharm)
        {
            int auraUp = Math.Min(cfg.AuraMax, aura + 1);
            if (!DropPossible(cfg, stage, floorPeak))
                return new FallbackResult { Outcome = EnhanceOutcome.FailNoDrop, StageAfter = stage, AuraAfter = auraUp };
            if (useCharm)
                return new FallbackResult { Outcome = EnhanceOutcome.FailCharmBlocked, StageAfter = stage, AuraAfter = aura, CharmConsumed = true };
            return new FallbackResult { Outcome = EnhanceOutcome.FailDrop, StageAfter = stage - 1, AuraAfter = auraUp };
        }

        // ------------------------------------------------------------------ 성공 (4.6절)

        public static int StageAfterSuccess(EnhanceConfig cfg, int stage) => Math.Min(cfg.MaxStage, stage + 1);

        /// <summary>대성공은 두 단계. 최고 단계에서 멈춘다.</summary>
        public static int StageAfterGreat(EnhanceConfig cfg, int stage) => Math.Min(cfg.MaxStage, stage + 2);

        /// <summary>대성공이 슬롯을 줄 수 있는 단계인가(great_slot_min_stage 손잡이).</summary>
        public static bool GreatGivesSlot(EnhanceConfig cfg, int stageBefore) => stageBefore >= cfg.GreatSlotMinStage;

        // ------------------------------------------------------------------ 흔적, 마일스톤, 칭호 (4.9·4.11절)

        public static int ScarCount(int stage, int recordPeak, int scarMax) =>
            Math.Max(0, Math.Min(scarMax, recordPeak - stage));

        /// <summary>before &lt; m ≤ after 인 마일스톤 가운데 가장 높은 것. 없으면 0.</summary>
        public static int MilestoneReached(EnhanceConfig cfg, int before, int after)
        {
            int best = 0;
            if (cfg.Milestones == null) return best;
            for (int i = 0; i < cfg.Milestones.Count; i++)
            {
                int m = cfg.Milestones[i];
                if (m > before && m <= after && m > best) best = m;
            }
            return best;
        }

        /// <summary>그 단계 뒤의 다음 마일스톤(기댓값 표의 기본 목표). 없으면 최고 단계.</summary>
        public static int NextMilestone(EnhanceConfig cfg, int stage)
        {
            int best = cfg.MaxStage;
            if (cfg.Milestones == null) return best;
            for (int i = 0; i < cfg.Milestones.Count; i++)
            {
                int m = cfg.Milestones[i];
                if (m > stage && m < best) best = m;
            }
            return best;
        }

        /// <summary>칭호 묶음 번호(b0~b4) = 그 단계 이하의 마일스톤 수.</summary>
        public static int TitleBand(EnhanceConfig cfg, int stage)
        {
            int band = 0;
            if (cfg.Milestones == null) return band;
            for (int i = 0; i < cfg.Milestones.Count; i++)
                if (cfg.Milestones[i] <= stage) band++;
            return band;
        }

        /// <summary>"title.stage.N.bK".</summary>
        public static string StageTitleId(EnhanceConfig cfg, int character, int stage) =>
            "title.stage." + character + ".b" + TitleBand(cfg, stage);

        // ------------------------------------------------------------------ 변이 (4.7절)

        /// <summary>변이 보유 상한: characters.json max_mutations, 없으면 enhance.json의 값.</summary>
        public static int MutationCap(EnhanceConfig cfg, IContentDb db, int character)
        {
            string charId = Ids.Char.Of(character);
            CharacterDef def = db != null ? db.Character(charId) : null;
            if (def != null && def.MaxMutations > 0) return def.MaxMutations;
            return cfg.MutationCapFor(charId);
        }

        /// <summary>출발 조립의 코어가 폭발하는 투사체인가(00 문서 D-30: 불안정 핵이 나오는 조건).</summary>
        public static bool CoreIsExplosive(IContentDb db, LoadoutData loadout)
        {
            if (db == null || loadout == null || string.IsNullOrEmpty(loadout.Core)) return false;
            BlockDef core = db.Block(loadout.Core);
            return core != null && core.IsProjectile && core.HasFlag(Ids.BlockFlag.Explodes);
        }

        /// <summary>변이 후보 목록(번호 순): 여섯 가운데 이미 가진 것을 빼고, 불안정 핵은 코어가 폭발 투사체일 때만.</summary>
        public static List<string> MutationPool(IReadOnlyList<MutationEntry> owned, bool coreIsExplosive)
        {
            var pool = new List<string>();
            foreach (string id in Ids.Mutation.All)
            {
                if (id == Ids.Mutation.UnstableCore && !coreIsExplosive) continue;
                bool has = false;
                if (owned != null)
                    for (int i = 0; i < owned.Count; i++)
                        if (owned[i] != null && owned[i].Id == id) { has = true; break; }
                if (!has) pool.Add(id);
            }
            return pool;
        }

        /// <summary>후보 하나를 균등하게 뽑는다. 목록이 비면 null(일반 실패로 처리한다).</summary>
        public static string DrawCandidate(List<string> pool, IEnhanceRandom rng)
        {
            if (pool == null || pool.Count == 0) return null;
            return pool[rng.Next(pool.Count)];
        }

        /// <summary>받으면 밀려날 변이(가득 찼을 때 가장 오래된 것). 없으면 null.</summary>
        public static string WouldPushOut(IReadOnlyList<MutationEntry> owned, int cap)
        {
            if (owned == null || owned.Count == 0 || owned.Count < cap) return null;
            return owned[0].Id;
        }

        /// <summary>변이를 받는다: 가득이면 가장 오래된 것(맨 앞)을 밀어내고 뒤에 붙인다. 밀려난 id를 돌려준다(없으면 null).</summary>
        public static string AcceptMutation(List<MutationEntry> owned, string candidate, int cap, long seq)
        {
            string pushed = null;
            while (owned.Count > 0 && owned.Count >= cap)
            {
                if (pushed == null) pushed = owned[0].Id;
                owned.RemoveAt(0);
            }
            owned.Add(new MutationEntry { Id = candidate, Seq = seq });
            return pushed;
        }

        // ------------------------------------------------------------------ 슬롯 새기기 (4.8절)

        public static bool CarveEligible(EnhanceConfig cfg, int stage, int slotsExtra) =>
            stage >= cfg.Carve.MinStage && slotsExtra < cfg.SlotExtraMax;

        /// <summary>상한 전 확률 = base_pct + step_pct × 누적 실패 수.</summary>
        public static int CarveRawPct(EnhanceConfig cfg, int carveBonus) =>
            cfg.Carve.BasePct + cfg.Carve.StepPct * Math.Max(0, carveBonus);

        public static int CarvePct(EnhanceConfig cfg, int carveBonus) => Math.Min(PctFull, CarveRawPct(cfg, carveBonus));

        /// <summary>골드 = gold_coeff × (지금 확장 칸 수 + 1).</summary>
        public static long CarveGold(EnhanceConfig cfg, int slotsExtra) => (long)cfg.Carve.GoldCoeff * (slotsExtra + 1);

        public static bool CarveSucceeds(EnhanceConfig cfg, int carveBonus, int rollBp) =>
            rollBp < CarvePct(cfg, carveBonus) * BpPerPct;

        /// <summary>칸 하나를 얻기까지의 평균 시도 수(실패마다 확률이 오른다). TV-S-08.</summary>
        public static double CarveExpectedAttempts(EnhanceConfig cfg)
        {
            double expected = 0, reach = 1;
            for (int bonus = 0; reach > 0; bonus++)
            {
                double p = CarvePct(cfg, bonus) / (double)PctFull;
                expected += reach;
                reach *= 1 - p;
                if (p <= 0) return double.PositiveInfinity;
            }
            return expected;
        }

        // ------------------------------------------------------------------ 전투 배율 (4.10절)

        /// <summary>강화 배율 = stage_damage_base ^ 단계(0~최고 단계로 자른다).</summary>
        public static float StageDamageMul(EnhanceConfig cfg, int stage)
        {
            int n = stage < 0 ? 0 : (stage > cfg.MaxStage ? cfg.MaxStage : stage);
            return (float)Math.Pow(cfg.StageDamageBase, n);
        }

        // ------------------------------------------------------------------ 시도 전 분해

        /// <summary>
        /// 화면 표시와 판정이 같이 쓰는 분해. Blocked는 채우지 않는다(서비스가 전제를 본 뒤 채운다).
        /// SacrificePct = 제물 보너스 전체(등급 + 같은 속성, 상한 뒤). 내역 두 줄은 SacrificeBreakdown으로 가른다.
        /// </summary>
        public static ProbBreakdown Breakdown(EnhanceConfig cfg, int stage, int floorPeak, int aura, SacrificeBreakdown sacrifice)
        {
            var b = new ProbBreakdown();
            if (stage < 0 || stage >= cfg.MaxStage)
            {
                b.FloorStage = DisplayFloor(cfg, floorPeak);
                return b;
            }
            bool first = stage == 0;
            b.BasePct = BasePct(cfg, stage);
            b.SacrificePct = first ? 0 : sacrifice.TotalPct;
            b.SameElementPct = first ? 0 : sacrifice.SameElementPct;
            b.AuraPct = first ? 0 : AuraPct(cfg, aura);
            b.RawPct = RawPct(cfg, stage, aura, sacrifice.TotalPct);
            b.SuccessPct = SuccessPct(cfg, stage, aura, sacrifice.TotalPct);
            b.Capped = IsCapped(cfg, stage, aura, sacrifice.TotalPct);
            b.SuccessBp = SuccessBp(b.SuccessPct);
            b.GreatBp = GreatBp(cfg, stage, b.SuccessPct);
            b.MutationEndBp = MutationEndBp(cfg, b.SuccessPct);
            b.DropPossible = DropPossible(cfg, stage, floorPeak);
            b.FloorStage = DisplayFloor(cfg, floorPeak);
            b.CostGold = Cost(cfg, stage);
            b.StoneId = StoneId(StoneTierFor(cfg, stage));
            b.StoneCount = cfg.StonesPerAttempt;
            return b;
        }

        // ------------------------------------------------------------------ 불변식 (3.1절)

        /// <summary>지팡이 개체의 불변식을 본다. 어긴 것이 없으면 null, 있으면 어긴 조건의 이름.</summary>
        public static string CheckInvariants(EnhanceConfig cfg, WandData w, int mutationCap)
        {
            if (w == null) return "wand";
            if (w.Stage < 0 || w.Stage > cfg.MaxStage) return "stage_range";
            if (w.FloorPeak < w.Stage) return "floor_peak_below_stage";
            if (w.FloorPeak > Math.Min(cfg.MaxStage, w.Stage + cfg.FloorGap)) return "floor_peak_above_gap";
            if (w.RecordPeak < w.Stage) return "record_peak_below_stage";
            if (w.Aura < 0 || w.Aura > cfg.AuraMax) return "aura_range";
            if (w.SlotsExtra < 0 || w.SlotsExtra > cfg.SlotExtraMax) return "slots_extra_range";
            if (w.CarveBonus < 0 || CarveRawPct(cfg, w.CarveBonus) > PctFull && CarveRawPct(cfg, w.CarveBonus - 1) >= PctFull) return "carve_bonus_range";
            if (w.DropCount < 0) return "drop_count_range";
            if (w.Mutations == null || w.Mutations.Count > mutationCap) return "mutation_count";
            for (int i = 0; i < w.Mutations.Count; i++)
                for (int j = i + 1; j < w.Mutations.Count; j++)
                    if (w.Mutations[i].Id == w.Mutations[j].Id) return "mutation_duplicate";
            if (w.Loadout == null || w.Loadout.Extra == null || w.Loadout.Extra.Count != w.SlotsExtra) return "loadout_extra_count";
            return null;
        }
    }
}
