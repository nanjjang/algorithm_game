using System;
using System.Text;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Records;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>재화 경고(다음 시도 비용 대비). 강화 규칙은 그대로 — 무엇을 미리 알려 줄지만 정한다.</summary>
    public enum FundsWarning
    {
        None = 0,
        /// <summary>이번 시도는 되지만 같은 비용을 한 번 더 낼 골드가 남지 않는다.</summary>
        LastGold = 1,
        /// <summary>이번 시도는 되지만 같은 강화석을 한 번 더 낼 만큼 남지 않는다.</summary>
        LastStone = 2,
        /// <summary>지금 골드가 모자라다.</summary>
        ShortGold = 3,
        /// <summary>지금 강화석이 모자라다.</summary>
        ShortStone = 4
    }

    /// <summary>
    /// 강화 화면의 기록 칸·경고 줄 규칙(엔진 없음). 기록은 지팡이 개체에 저장된 WandEnhanceStats(강화 서비스가 시도마다 적는다)를 읽기만 한다.
    /// 다섯 지팡이의 확률·비용·실패 규칙이 같다는 고정 문구, 하락 뒤 회복 안내(확률은 매번 독립, 바닥은 지켜진다)도 여기서 고른다.
    /// 글자는 이름 표 str.enhance.record.*.
    /// </summary>
    public static class EnhanceRecordModel
    {
        public const string P = "str.enhance.record.";
        public const string TitleKey = P + "title", SummaryKey = P + "summary", StageLineKey = P + "stage_line", StageNoneKey = P + "stage_none",
            SameRulesKey = P + "same_rules", TableTitleKey = P + "table_title", TableRowKey = P + "table_row", EmptyKey = P + "empty",
            WarnLastGoldKey = P + "warn.last_gold", WarnLastStoneKey = P + "warn.last_stone", WarnShortGoldKey = P + "warn.short_gold",
            WarnShortStoneKey = P + "warn.short_stone", AfterDropKey = P + "after_drop", AfterDropAuraKey = P + "after_drop_aura";

        /// <summary>
        /// 경고: 모자라면 Short*, 이번이 마지막이면 Last*(골드 먼저). 최대 단계·비용 0이면 None.
        /// 다음 시도 비용은 같은 단계의 비용으로 본다(성공하면 비용이 오르므로 실제로는 더 빨리 모자랄 수 있다 — 문구가 "같은 단계"라고 밝힌다).
        /// </summary>
        public static FundsWarning Funds(long gold, int stoneHave, long costGold, int stoneCost, bool atMax)
        {
            if (atMax) return FundsWarning.None;
            if (costGold > 0 && gold < costGold) return FundsWarning.ShortGold;
            if (stoneCost > 0 && stoneHave < stoneCost) return FundsWarning.ShortStone;
            if (costGold > 0 && gold - costGold < costGold) return FundsWarning.LastGold;
            if (stoneCost > 0 && stoneHave - stoneCost < stoneCost) return FundsWarning.LastStone;
            return FundsWarning.None;
        }

        public static string FundsKey(FundsWarning w)
        {
            switch (w)
            {
                case FundsWarning.LastGold: return WarnLastGoldKey;
                case FundsWarning.LastStone: return WarnLastStoneKey;
                case FundsWarning.ShortGold: return WarnShortGoldKey;
                case FundsWarning.ShortStone: return WarnShortStoneKey;
                default: return null;
            }
        }

        /// <summary>하락 뒤 회복 안내를 보일까: 마지막 결과가 하락이었고(변이를 거절해 하락한 것 포함) 결과를 다 본 뒤.</summary>
        public static bool ShowAfterDrop(EnhanceResult last, bool settled) =>
            settled && last != null && last.Outcome == EnhanceOutcome.FailDrop;

        public static string SummaryText(INameTable n, WandEnhanceStats s)
        {
            if (s == null || s.Attempts <= 0) return n != null ? n.Get(EmptyKey) : EmptyKey;
            if (n == null) return SummaryKey;
            return n.Format(SummaryKey, "n", n.Number(s.Attempts), "success", n.Number(s.Successes), "great", n.Number(s.GreatSuccesses),
                "keep", n.Number(s.Keeps), "drop", n.Number(s.Drops), "mutation", n.Number(s.Mutations));
        }

        /// <summary>지금 도전 단계의 "표기 평균 n% · 실제 a/b (c%)" 한 줄.</summary>
        public static string StageText(INameTable n, WandEnhanceStats s, int target)
        {
            StageEnhanceStats st = s != null ? s.Find(target) : null;
            if (n == null) return st == null ? StageNoneKey : StageLineKey;
            string stage = EnhanceNameKeys.Stage(n, target);
            if (st == null || st.Attempts <= 0) return n.Format(StageNoneKey, "stage", stage);
            return n.Format(StageLineKey, "stage", stage, "shown", Pct(EnhanceRecordBook.AverageShownPct(st)),
                "ok", n.Number(st.Successes), "n", n.Number(st.Attempts), "actual", Pct(EnhanceRecordBook.ActualPct(st)));
        }

        /// <summary>단계별 표(툴팁): 시도한 단계만, 낮은 단계부터.</summary>
        public static string TableText(INameTable n, WandEnhanceStats s, int maxStage)
        {
            if (n == null) return TableTitleKey;
            var sb = new StringBuilder(n.Get(TableTitleKey));
            bool any = false;
            for (int t = 1; t <= maxStage; t++)
            {
                StageEnhanceStats st = s != null ? s.Find(t) : null;
                if (st == null || st.Attempts <= 0) continue;
                any = true;
                sb.Append('\n').Append(n.Format(TableRowKey, "stage", EnhanceNameKeys.Stage(n, t), "shown", Pct(EnhanceRecordBook.AverageShownPct(st)),
                    "ok", n.Number(st.Successes), "n", n.Number(st.Attempts), "actual", Pct(EnhanceRecordBook.ActualPct(st))));
            }
            if (!any) sb.Append('\n').Append(n.Get(EmptyKey));
            return sb.ToString();
        }

        /// <summary>소수 첫째 자리까지(정수면 정수로).</summary>
        public static string Pct(double v)
        {
            if (v < 0) return "0";
            double r = Math.Round(v, 1, MidpointRounding.AwayFromZero);
            return Math.Abs(r - Math.Round(r)) < 1e-9
                ? ((long)Math.Round(r)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : r.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
