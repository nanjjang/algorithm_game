using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>실패 안내 한 줄(08 요약서 10.4절, 07 4.4절).</summary>
    public enum EnhanceHint { NoDropLow, AtFloor, DropTo, CharmProtects, MaxStage }

    /// <summary>[강화 J]가 모자라서 꺼졌을 때 흔들 쪽.</summary>
    [Flags]
    public enum CostShortage { None = 0, Gold = 1, Stone = 2 }

    /// <summary>결과 띠 4구간(실제 비율 bp). 합 = 10000.</summary>
    public struct ResultBand
    {
        public int GreatBp, SuccessBp, MutationBp, FailBp;
        public int Total => GreatBp + SuccessBp + MutationBp + FailBp;
    }

    /// <summary>
    /// 오른쪽 칸의 확률 표시(큰 숫자, 내역 네 줄, 상한 딱지, 결과 띠, 실패 안내, 비용, 하락 가능 꼬리표, 부적 칸 접힘).
    /// 값은 규칙과 같은 식으로 셈한다(07 4.2~4.5절). 화면은 IEnhanceService.Preview의 값을 그대로 넣어 만들고(FromPreview),
    /// 순수 식(Compute)은 같은 규칙을 시험에서 확인하는 데 쓴다. 다섯 지팡이가 같은 확률·비용을 쓴다 — 캐릭터는 같은 속성 판정에만 든다.
    /// </summary>
    public sealed class ProbabilityDisplayModel
    {
        public int Stage;
        public int Target;
        public int BasePct;
        /// <summary>"제물 합" 줄 = 제물 보너스 − 같은 속성(두 줄 합이 상한을 넘지 않게, 07 4.2절).</summary>
        public int SacrificeLinePct;
        /// <summary>"같은 속성" 줄(시도당 한 번).</summary>
        public int SameElementPct;
        public int AuraPct;
        public int RawPct;
        public int TotalPct;
        public bool Capped;
        public ResultBand Band;
        public bool DropPossible;
        public int FloorStage;
        public bool CharmSet;
        public EnhanceHint Hint;
        /// <summary>DropTo의 n(= stage − 1), AtFloor의 best(= floor_peak).</summary>
        public int HintValue;
        public bool DropPossibleTag => DropPossible && !CharmSet && Stage < MaxStage;
        /// <summary>부적 칸 접힘 + "지금은 필요 없다".</summary>
        public bool CharmSlotFolded => !DropPossible;
        public long CostGold;
        public string StoneId;
        public int StoneCount;
        public CostShortage Shortage;
        public bool CanAfford => Shortage == CostShortage.None;
        public ErrorCode Blocked;
        public int MaxStage;
        public bool AtMax => Stage >= MaxStage;

        // ------------------------------------------------------------------ 순수 식

        /// <summary>제물 점수(일반 4, 희귀 8, 영웅 14).</summary>
        public static int Score(EnhanceConfig cfg, string rarity) =>
            rarity == Ids.Rarity.Common ? cfg.Sacrifice.ScoreCommon
            : rarity == Ids.Rarity.Rare ? cfg.Sacrifice.ScoreRare
            : rarity == Ids.Rarity.Hero ? cfg.Sacrifice.ScoreHero : 0;

        /// <summary>제물 블록 목록의 (등급 합, 같은 속성 여부).</summary>
        public static void SacrificeParts(EnhanceConfig cfg, IContentDb db, IReadOnlyList<string> blocks, string wandElement,
            out int gradeSum, out bool sameElement)
        {
            gradeSum = 0;
            sameElement = false;
            if (blocks == null) return;
            for (int i = 0; i < blocks.Count; i++)
            {
                BlockDef b = db != null ? db.Block(blocks[i]) : null;
                if (b == null) continue;
                gradeSum += Score(cfg, b.Rarity);
                if (wandElement != null && b.Element == wandElement) sameElement = true;
            }
        }

        /// <summary>
        /// 확률 분해(08 10.4절 Compute, 07 4.2절). 제물 상한은 등급 합 + 같은 속성의 합계에 건다(07 13절 M-1: S8 해석).
        /// +1 도전은 100%이고 상한·대성공이 없다(TV-087).
        /// </summary>
        public static ProbabilityDisplayModel Compute(EnhanceConfig cfg, int stage, int floorPeak, int aura, int gradeSum, bool sameElement,
            bool charmSet)
        {
            var m = new ProbabilityDisplayModel { Stage = stage, Target = stage + 1, MaxStage = cfg.MaxStage, CharmSet = charmSet };
            if (stage >= cfg.MaxStage)
            {
                m.Hint = EnhanceHint.MaxStage;
                m.FloorStage = DisplayFloor(cfg, floorPeak);
                m.Blocked = ErrorCode.MaxStage;
                return m;
            }
            m.BasePct = cfg.BaseProb[stage];
            if (stage == 0)
            {
                m.RawPct = m.TotalPct = m.BasePct;
            }
            else
            {
                int elem = sameElement ? cfg.Sacrifice.SameElement : 0;
                int sac = Math.Min(cfg.Sacrifice.Cap, gradeSum + elem);
                m.SameElementPct = Math.Min(elem, sac);
                m.SacrificeLinePct = sac - m.SameElementPct;
                int a = aura < 0 ? 0 : (aura > cfg.AuraMax ? cfg.AuraMax : aura);
                m.AuraPct = cfg.AuraStepPct * a;
                m.RawPct = m.BasePct + sac + m.AuraPct;
                m.TotalPct = Math.Min(cfg.ProbCapPct, m.RawPct);
                m.Capped = m.RawPct >= cfg.ProbCapPct;
            }
            int p = m.TotalPct * 100;
            int great = stage == 0 ? 0 : p * cfg.GreatRatioPct / 100;
            int mutEnd = p + (10000 - p) * cfg.MutationRatioPct / 100;
            m.Band = BandOf(great, p, mutEnd);
            m.DropPossible = DropPossibleOf(cfg, stage, floorPeak);
            m.FloorStage = DisplayFloor(cfg, floorPeak);
            m.CostGold = cfg.CostCoeff * (long)(stage + 1) * (stage + 1);
            m.StoneId = stage <= cfg.StoneTierBounds[0] ? Ids.Item.StoneLow : stage <= cfg.StoneTierBounds[1] ? Ids.Item.StoneMid : Ids.Item.StoneHigh;
            m.StoneCount = cfg.StonesPerAttempt;
            SetHint(cfg, m, floorPeak);
            return m;
        }

        /// <summary>규칙 서비스가 만든 분해를 그대로 화면 값으로(같은 속성 줄만 따로 가른다).</summary>
        public static ProbabilityDisplayModel FromPreview(EnhanceConfig cfg, ProbBreakdown b, int stage, int floorPeak, bool sameElement,
            bool charmSet)
        {
            var m = new ProbabilityDisplayModel { Stage = stage, Target = stage + 1, MaxStage = cfg.MaxStage, CharmSet = charmSet };
            m.Blocked = b.Blocked;
            m.FloorStage = DisplayFloor(cfg, floorPeak);
            if (stage >= cfg.MaxStage) { m.Hint = EnhanceHint.MaxStage; return m; }
            m.BasePct = b.BasePct;
            m.SameElementPct = stage == 0 ? 0 : b.SameElementPct;
            m.SacrificeLinePct = b.SacrificePct - m.SameElementPct;
            m.AuraPct = b.AuraPct;
            m.RawPct = b.RawPct;
            m.TotalPct = b.SuccessPct;
            m.Capped = b.Capped;
            m.Band = BandOf(b.GreatBp, b.SuccessBp, b.MutationEndBp);
            m.DropPossible = b.DropPossible;
            m.CostGold = b.CostGold;
            m.StoneId = b.StoneId;
            m.StoneCount = b.StoneCount;
            SetHint(cfg, m, floorPeak);
            return m;
        }

        /// <summary>결과 띠: 대성공 [0, great) / 성공 [great, p) / 변이 [p, mutEnd) / 하락·하락 없음 [mutEnd, 10000)(TV-086).</summary>
        public static ResultBand BandOf(int greatBp, int successBp, int mutationEndBp) => new ResultBand
        {
            GreatBp = greatBp,
            SuccessBp = successBp - greatBp,
            MutationBp = mutationEndBp - successBp,
            FailBp = 10000 - mutationEndBp,
        };

        /// <summary>DropPossible = stage &gt; 3 && stage &gt; floor_peak − 2(07 4.4절 = 08 10.4절).</summary>
        public static bool DropPossibleOf(EnhanceConfig cfg, int stage, int floorPeak) =>
            stage > cfg.DropSafeMaxStage && stage - 1 >= floorPeak - cfg.FloorGap;

        /// <summary>표시 바닥 = max(3, floor_peak − 2)(07 13절 M-4).</summary>
        public static int DisplayFloor(EnhanceConfig cfg, int floorPeak) => Math.Max(cfg.DropSafeMaxStage, floorPeak - cfg.FloorGap);

        static void SetHint(EnhanceConfig cfg, ProbabilityDisplayModel m, int floorPeak)
        {
            if (m.Stage <= cfg.DropSafeMaxStage) { m.Hint = EnhanceHint.NoDropLow; m.HintValue = 0; }
            else if (!m.DropPossible) { m.Hint = EnhanceHint.AtFloor; m.HintValue = floorPeak; }
            else if (m.CharmSet) { m.Hint = EnhanceHint.CharmProtects; m.HintValue = 0; }
            else { m.Hint = EnhanceHint.DropTo; m.HintValue = m.Stage - 1; }
        }

        /// <summary>보유량으로 모자란 쪽을 정한다(TV-095: 골드 6,399, +8 도전 → 골드).</summary>
        public void SetHoldings(long gold, int stoneQty)
        {
            Shortage = CostShortage.None;
            if (AtMax) return;
            if (gold < CostGold) Shortage |= CostShortage.Gold;
            if (stoneQty < StoneCount) Shortage |= CostShortage.Stone;
        }

        /// <summary>실패 안내의 이름 표 열쇠.</summary>
        public string HintKey()
        {
            switch (Hint)
            {
                case EnhanceHint.NoDropLow: return EnhanceNameKeys.HintNoDropLow;
                case EnhanceHint.AtFloor: return EnhanceNameKeys.HintAtFloor;
                case EnhanceHint.CharmProtects: return EnhanceNameKeys.HintCharmProtects;
                case EnhanceHint.DropTo: return EnhanceNameKeys.HintDropTo;
                default: return EnhanceNameKeys.ErrorMaxStage;
            }
        }

        /// <summary>실패 안내 글(이름 표를 거친 것).</summary>
        public string HintText(INameTable names)
        {
            if (names == null) return HintKey();
            switch (Hint)
            {
                case EnhanceHint.AtFloor: return names.Format(EnhanceNameKeys.HintAtFloor, "best", HintValue);
                case EnhanceHint.DropTo: return names.Format(EnhanceNameKeys.HintDropTo, "n", HintValue);
                default: return names.Get(HintKey());
            }
        }

        /// <summary>제물 칸의 마지막 투사체 판정(TV-096): 서고+조립 합쳐 투사체가 placedProjectiles + 1개 이하이면 그 투사체는 더 올릴 수 없다.</summary>
        public static bool CannotPlace(BlockDef block, int projectileTotal, int placedProjectiles) =>
            block != null && block.IsProjectile && projectileTotal - placedProjectiles <= 1;
    }
}
