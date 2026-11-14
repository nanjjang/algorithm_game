using System;
using System.Collections.Generic;
using System.Globalization;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>결과 카드 한 줄의 뜻(그리는 쪽이 꼴을 고른다).</summary>
    public enum CardLineKind { Title, Stage, Roll, Slot, Charm, Aura, Trace, Master, Character, ServerNotice, Milestone }

    public struct CardLine
    {
        public CardLineKind Kind;
        public string Text;
        public CardLine(CardLineKind k, string t) { Kind = k; Text = t; }
    }

    /// <summary>
    /// 결과 카드(08 요약서 10.7절). 뽑힌 값 / 성공 기준, 단계 변화, 슬롯 +1 또는 "슬롯은 이미 다 자랐다", 부적 소모, 기운, 흔적,
    /// 느루 줄, 캐릭터 한마디(열쇠 str.enhance.char_line.&lt;outcome&gt; — 값이 없으면 줄을 숨긴다, D-66),
    /// 서버 알림 줄(오프라인은 없다, TV-114). 글은 전부 이름 표를 거친다.
    /// </summary>
    public static class EnhanceResultCardModel
    {
        /// <summary>뽑힌 값 글(TV-131: roll 3720, p 82 → "37.2 / 82.0"). 소수 자리는 G23 roll_decimals, 뽑힌 값은 버림(미만 판정을 넘어 보이지 않게).</summary>
        public static string RollNumber(int rollBp, int decimals)
        {
            if (decimals < 0) decimals = 0;
            double scale = Math.Pow(10, decimals);
            double v = Math.Floor(rollBp / 100.0 * scale + 1e-9) / scale;
            return v.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        public static string PctNumber(int pct, int decimals) =>
            ((double)pct).ToString("F" + (decimals < 0 ? 0 : decimals), CultureInfo.InvariantCulture);

        public static string RollLine(INameTable names, EnhanceResult r, int decimals) =>
            names.Format(EnhanceNameKeys.LineRoll, "roll", RollNumber(r.RollBp, decimals), "p", PctNumber(r.Breakdown.SuccessPct, decimals));

        /// <summary>카드 줄들. attempt = 시도 결과, shown = 지금 보이는 결과(변이 결정이 있으면 그것). kind = 프로필 종류.</summary>
        public static List<CardLine> Build(INameTable names, EnhanceConfig cfg, EnhanceResult attempt, EnhanceResult shown,
            ProfileKind kind, int decimals, int slotMax)
        {
            var lines = new List<CardLine>();
            var r = shown ?? attempt;
            if (r == null || names == null) return lines;
            EnhanceOutcome o = r.Outcome;
            lines.Add(new CardLine(CardLineKind.Title, names.Get(EnhanceNameKeys.ResultTitle(o))));
            lines.Add(new CardLine(CardLineKind.Stage, names.Format(EnhanceNameKeys.LineStage,
                "from", EnhanceNameKeys.Stage(names, r.StageBefore), "to", EnhanceNameKeys.Stage(names, r.StageAfter))));
            var rollSrc = attempt != null && attempt.RollBp >= 0 ? attempt : (r.RollBp >= 0 ? r : null);
            if (rollSrc != null) lines.Add(new CardLine(CardLineKind.Roll, RollLine(names, rollSrc, decimals)));

            if (r.SlotExpanded)
            {
                int n = r.Wand != null ? r.Wand.SlotsExtra : 0;
                lines.Add(new CardLine(CardLineKind.Slot, names.Format(EnhanceNameKeys.LineSlotExtended, "n", n, "max", slotMax)));
            }
            else if (r.SlotAlreadyFull)
                lines.Add(new CardLine(CardLineKind.Slot, names.Get(EnhanceNameKeys.LineSlotFull)));

            if (r.CharmConsumed) lines.Add(new CardLine(CardLineKind.Charm, names.Get(EnhanceNameKeys.LineFailCharm)));
            if (r.AuraAfter != r.AuraBefore)
                lines.Add(new CardLine(CardLineKind.Aura, names.Format(EnhanceNameKeys.LineAura, "from", r.AuraBefore, "to", r.AuraAfter)));

            if (o == EnhanceOutcome.FailDrop)
            {
                lines.Add(new CardLine(CardLineKind.Master, names.Get(EnhanceNameKeys.LineFailDrop)));
                bool pub = r.Wand != null && r.Wand.ShowScars;
                lines.Add(new CardLine(CardLineKind.Trace, TraceBanner(names, pub)));
            }
            else if (o == EnhanceOutcome.FailNoDrop)
            {
                bool atFloor = cfg != null && r.StageBefore > cfg.DropSafeMaxStage;
                int best = r.Wand != null ? r.Wand.FloorPeak : r.StageBefore;
                lines.Add(new CardLine(CardLineKind.Master, atFloor
                    ? names.Format(EnhanceNameKeys.LineFailFloor, "best", best)
                    : names.Get(EnhanceNameKeys.LineFailNoDrop)));
            }

            string ch = EnhanceNameKeys.IfPresent(names, EnhanceNameKeys.CharLine(o == EnhanceOutcome.MutationAccepted ? EnhanceOutcome.MutationOffered : o));
            if (ch != null) lines.Add(new CardLine(CardLineKind.Character, ch));

            // 서버 전체 알림 줄은 서버 프로필에서 알림이 나갔을 때만(오프라인은 받지도 내지도 않는다).
            if (kind == ProfileKind.Server && !string.IsNullOrEmpty(r.ServerNotice))
                lines.Add(new CardLine(CardLineKind.ServerNotice, names.Get(EnhanceNameKeys.LineServerNotified)));
            return lines;
        }

        /// <summary>"흔적이 남았다 · 지금은 {state}"(TV-119).</summary>
        public static string TraceBanner(INameTable names, bool isPublic) =>
            names.Format(EnhanceNameKeys.TraceBanner, "state", names.Get(isPublic ? EnhanceNameKeys.TraceStatePublic : EnhanceNameKeys.TraceStateHidden));

        /// <summary>칭호 띠 "{from} → {to}".</summary>
        public static string MilestoneBand(INameTable names, EnhanceConfig cfg, int character, int from, int to) =>
            names.Format(EnhanceNameKeys.Milestone, "from", EnhanceNameKeys.StageTitle(names, cfg, character, from),
                "to", EnhanceNameKeys.StageTitle(names, cfg, character, to));

        /// <summary>변이 카드의 밀려남 줄(TV-122). 밀려날 것이 없으면 null.</summary>
        public static string PushOutLine(INameTable names, string pushedOut) =>
            string.IsNullOrEmpty(pushedOut) ? null : names.Format(EnhanceNameKeys.MutationPushOut, "mutation", names.Get(pushedOut));

        /// <summary>대기 중인 변이 제시 → 화면이 받는 꾸러미 꼴(결정 대기 지팡이를 열면 카드부터, TV-M-07).</summary>
        public static EnhanceResult FromOffer(WandData w)
        {
            if (w == null || w.PendingOffer == null) return null;
            var o = w.PendingOffer;
            return new EnhanceResult
            {
                AttemptId = o.AttemptId,
                Outcome = EnhanceOutcome.MutationOffered,
                StageBefore = w.Stage,
                StageAfter = w.Stage,
                AuraBefore = w.Aura,
                AuraAfter = w.Aura,
                MutationCandidate = o.Candidate,
                MutationPushedOut = o.WouldPushOut,
                HasFallback = true,
                Fallback = new FallbackResult
                {
                    Outcome = o.FallbackOutcome == EnhanceOutcome.FailDrop.ToString() ? EnhanceOutcome.FailDrop
                        : o.FallbackOutcome == EnhanceOutcome.FailCharmBlocked.ToString() ? EnhanceOutcome.FailCharmBlocked : EnhanceOutcome.FailNoDrop,
                    StageAfter = o.FallbackStageAfter,
                    CharmConsumed = o.FallbackCharmConsumed,
                    AuraAfter = o.FallbackAuraAfter,
                },
                RollBp = -1,
                Wand = w,
            };
        }
    }
}
