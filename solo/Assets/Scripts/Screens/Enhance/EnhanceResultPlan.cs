using System;
using System.Collections.Generic;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>강화 화면 상태(08 요약서 10.5절, 07 5.2절). WaitingLate는 서버 결과 늦음이라 솔로(오프라인)에서는 들어가지 않는다(TV-106).</summary>
    public enum EnhancePhase
    {
        Idle, Committing, Payment, Offering, Charge, Hold, WaitingLate, Judge,
        ResultPlay, MutationChoice, MutationAccept, MutationRejectReplay,
        Milestone, Plus13, ResultCard, ShortResult
    }

    /// <summary>시간표의 한 구간(상태 하나와 그 길이·사건).</summary>
    public sealed class EnhanceSegment
    {
        public EnhancePhase Phase;
        public int Length;
        /// <summary>결과 연출 종류(G23 result_events 열쇠). 의식 구간은 null.</summary>
        public string Kind;
        public readonly List<EnhanceCue> Cues = new List<EnhanceCue>();
    }

    /// <summary>
    /// 결과 꾸러미(EnhanceResult) → 판정 뒤 연출 구간들(08 요약서 10.7절). 연출은 결과를 바꾸지 않는다 — 여기는 결과를 읽기만 한다.
    /// </summary>
    public static class EnhanceResultPlan
    {
        public const string KindSuccess = "success", KindGreat = "great_success", KindFailDrop = "fail_drop",
            KindCharm = "fail_charm_blocked", KindNoDrop = "fail_no_drop", KindMutation = "mutation_offered",
            KindMutationAccepted = "mutation_accepted", KindShort = "short", KindShortGreat = "short_great",
            KindMilestoneFirst = "milestone_first", KindMilestoneAgain = "milestone_again", KindPlus13 = "plus13";

        public const string IfScarShed = "scar_shed", IfSlotExpanded = "slot_expanded", IfAuraGained = "aura_gained";

        public static string KindOf(EnhanceOutcome o)
        {
            switch (o)
            {
                case EnhanceOutcome.GreatSuccess: return KindGreat;
                case EnhanceOutcome.Success: return KindSuccess;
                case EnhanceOutcome.FailDrop: return KindFailDrop;
                case EnhanceOutcome.FailCharmBlocked: return KindCharm;
                case EnhanceOutcome.FailNoDrop: return KindNoDrop;
                case EnhanceOutcome.MutationOffered: return KindMutation;
                default: return KindMutationAccepted;
            }
        }

        public static bool IsFailKind(string kind) => kind == KindFailDrop || kind == KindCharm || kind == KindNoDrop;

        /// <summary>결과가 +13에 닿았나(Plus13 연출로 이어진다).</summary>
        public static bool ReachesPlus13(EnhanceResult r, int maxStage) =>
            r != null && r.StageAfter >= maxStage && r.StageBefore < maxStage &&
            (r.Outcome == EnhanceOutcome.Success || r.Outcome == EnhanceOutcome.GreatSuccess);

        /// <summary>+13 전체 연출(9초)은 역대 최고 갱신일 때만(TV-116).</summary>
        public static bool Plus13First(EnhanceResult r) => r != null && (r.RecordPeakUpdated || r.MilestoneFirstTime);

        /// <summary>마일스톤 덧붙임(+4·+7·+10; +13은 Plus13). 첫 도달 2.5초 / 다시 도달 0.8초(TV-111~113).</summary>
        public static int MilestoneTicks(EnhanceScreenTuning t, EnhanceResult r, int maxStage)
        {
            if (r == null || r.MilestoneStage <= 0 || r.MilestoneStage >= maxStage) return 0;
            if (r.Outcome != EnhanceOutcome.Success && r.Outcome != EnhanceOutcome.GreatSuccess) return 0;
            return EnhanceScreenTuning.Ticks(r.MilestoneFirstTime ? t.MilestoneFirstSeconds : t.MilestoneAgainSeconds);
        }

        /// <summary>Plus13 길이: 첫 도달 540틱 / 다시 도달 0.80~3.00(132틱).</summary>
        public static int Plus13Ticks(EnhanceScreenTuning t, bool first) =>
            first ? EnhanceScreenTuning.Ticks(t.Plus13FullSeconds)
                  : EnhanceScreenTuning.Ticks(t.Plus13RepeatToSeconds) - EnhanceScreenTuning.Ticks(t.Plus13RepeatFromSeconds);

        /// <summary>실패 0.50 뒤 침묵 차이(초): 결과 길이 표는 기준 강도(3단) 값이라 다른 강도는 이만큼 길어지거나 짧아진다(10.7절).</summary>
        public static double FailSilenceDelta(EnhanceScreenTuning t, int intensity) =>
            EnhanceScreenTuning.At(t.FailSilenceSeconds, intensity) - EnhanceScreenTuning.At(t.FailSilenceSeconds, t.ReferenceIntensity);

        /// <summary>결과 연출 길이(틱). 실패 셋은 강도에 따라 침묵이 바뀐다. 변이 제시는 카드가 뜰 때까지.</summary>
        public static int ResultTicks(EnhanceScreenTuning t, string kind, int intensity)
        {
            double s = kind == KindMutationAccepted ? t.MutationAcceptSeconds : t.ResultLength(kind);
            if (IsFailKind(kind)) s += FailSilenceDelta(t, intensity);
            return EnhanceScreenTuning.Ticks(s);
        }

        /// <summary>조건 판정(G23 result_events의 if).</summary>
        public static bool Condition(string cond, EnhanceResult r, int scarMax)
        {
            if (string.IsNullOrEmpty(cond)) return true;
            if (r == null) return false;
            switch (cond)
            {
                case IfSlotExpanded: return r.SlotExpanded;
                case IfAuraGained: return r.AuraAfter > r.AuraBefore;
                case IfScarShed: return ScarShed(r, scarMax);
                default: return false;
            }
        }

        /// <summary>흔적 조각 수 = min(상한, record_peak − stage)(07 4.9절, TV-144).</summary>
        public static int ScarCount(int stage, int recordPeak, int scarMax) => Math.Max(0, Math.Min(scarMax, recordPeak - stage));

        /// <summary>이번 성공으로 흔적 조각이 떨어져 나갔나(역대 최고를 새로 쓰지 않은 성공만 판정할 수 있다).</summary>
        public static bool ScarShed(EnhanceResult r, int scarMax)
        {
            if (r == null || r.Wand == null || r.RecordPeakUpdated) return false;
            if (r.StageAfter <= r.StageBefore) return false;
            int rp = r.Wand.RecordPeak;
            return ScarCount(r.StageAfter, rp, scarMax) < ScarCount(r.StageBefore, rp, scarMax);
        }

        /// <summary>
        /// 결과 연출 구간 하나를 만든다. fromSeconds = 그 시각부터(변이 거절의 0.50 이후 재생), cutSeconds &gt; 0 = 그 시각에서 끊는다(+13 끼어듦).
        /// </summary>
        public static EnhanceSegment Segment(EnhanceScreenTuning t, EnhancePhase phase, string kind, EnhanceResult r, int intensity,
            double fromSeconds = 0, double cutSeconds = 0)
        {
            var seg = new EnhanceSegment { Phase = phase, Kind = kind };
            bool fail = IsFailKind(kind);
            double delta = fail ? FailSilenceDelta(t, intensity) : 0;
            int full = ResultTicks(t, kind, intensity);
            int fromTicks = EnhanceScreenTuning.Ticks(fromSeconds);
            int cutTicks = cutSeconds > 0 ? EnhanceScreenTuning.Ticks(cutSeconds) : full;
            seg.Length = Math.Max(0, Math.Min(full, cutTicks) - fromTicks);
            foreach (var spec in t.Events(kind))
            {
                if (!Condition(spec.If, r, t.ScarMax)) continue;
                double at = spec.Seconds;
                if (fail && at > t.FailSilenceAfterSeconds) at += delta;
                int tick = EnhanceScreenTuning.Ticks(at);
                if (tick < fromTicks || (cutSeconds > 0 && tick >= cutTicks)) continue;
                seg.Cues.Add(ToCue(t, spec, tick - fromTicks, intensity));
            }
            seg.Cues.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            return seg;
        }

        /// <summary>+13 구간(첫 도달 = 전부, 다시 도달 = 0.80~3.00만).</summary>
        public static EnhanceSegment Plus13Segment(EnhanceScreenTuning t, bool first, int intensity)
        {
            var seg = new EnhanceSegment { Phase = EnhancePhase.Plus13, Kind = KindPlus13, Length = Plus13Ticks(t, first) };
            int from = first ? 0 : EnhanceScreenTuning.Ticks(t.Plus13RepeatFromSeconds);
            int to = first ? seg.Length : EnhanceScreenTuning.Ticks(t.Plus13RepeatToSeconds);
            foreach (var spec in t.Events(KindPlus13))
            {
                int tick = EnhanceScreenTuning.Ticks(spec.Seconds);
                if (tick < from || tick >= to) continue;
                seg.Cues.Add(ToCue(t, spec, tick - from, intensity));
            }
            seg.Cues.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            return seg;
        }

        /// <summary>+13에 닿는 결과의 결과 연출이 끊기는 시각(초).</summary>
        public static double Plus13Cut(EnhanceScreenTuning t, EnhanceResult r) =>
            r.Outcome == EnhanceOutcome.GreatSuccess
                // +12에서의 대성공은 숫자를 한 번만 찍고(TV-118), +11에서는 둘째 찍기(+13) 직전에 끊는다.
                ? (r.StageAfter - r.StageBefore < 2 ? t.Plus13CutGreatFrom12 : t.Plus13CutGreatFrom11)
                : t.Plus13CutSuccess;

        static EnhanceCue ToCue(EnhanceScreenTuning t, EnhanceCueSpec spec, int tick, int intensity)
        {
            string shake = spec.Shake;
            if (shake == EnhanceFx.ShakeByIntensity)
                shake = intensity >= t.SuccessShakeMediumMinIntensity ? EnhanceFx.ShakeMedium : EnhanceFx.ShakeSmall;
            return new EnhanceCue
            {
                Tick = tick,
                EventId = spec.EventId,
                Bus = spec.Bus == EnhanceRitual.BusWand ? SoundBus.Wand : SoundBus.Ui,
                Pitch = 1f,
                Fx = spec.Fx,
                FxTicks = EnhanceScreenTuning.Ticks(spec.FxSeconds),
                Alpha = spec.Alpha,
                Color = spec.Color,
                Shake = shake,
            };
        }
    }
}
