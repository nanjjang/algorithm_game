using System;
using System.Collections.Generic;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>의식 방식(08 요약서 10.8절, 14.2절).</summary>
    public enum RitualStyle
    {
        /// <summary>전체 의식(Payment → Offering → Charge → Hold → Judge → 결과 연출).</summary>
        Full = 0,
        /// <summary>빠른 강화: 충전 0.3초 + 짧은 결과 0.5초(시도 약 0.8초). 심장·숨 멈춤·카메라·어두워짐 없음.</summary>
        Fast = 1,
        /// <summary>연출 감도 최소: 결과 카드만(변이 카드는 그대로, +13 2초 잠금 없음 [임시 U-19]).</summary>
        CardOnly = 2
    }

    /// <summary>시간표의 한 시점(구간 시작 기준 틱). 소리 사건이 있으면 그 시점에 ctx.Sound.Play(EventId, Bus, 1, Pitch).</summary>
    public struct EnhanceCue
    {
        public int Tick;
        public string EventId;
        public SoundBus Bus;
        public float Pitch;
        public string Fx;
        public int FxTicks;
        public double Alpha;
        public string Color;
        public string Shake;

        public bool HasSound => !string.IsNullOrEmpty(EventId);
        public override string ToString() => Tick + ":" + (EventId ?? "-") + "/" + (Fx ?? "-");
    }

    /// <summary>
    /// 한 시도의 의식 구간(08 요약서 10.6절). 결과와 무관하게 도전 단계·제물 수·의식 방식만으로 정해진다 —
    /// 그래서 충전 길이와 소리는 결과에 따라 달라지지 않는다(10.5절 원칙).
    /// 틱은 누른 순간(시도 시작)을 0으로 센다.
    /// </summary>
    public sealed class RitualTimeline
    {
        public int Target;
        public int Intensity;
        public RitualStyle Style;
        public int SacrificeCount;
        public int PayStart, OfferStart, ChargeStart, HoldStart, JudgeTick;
        public int PayTicks => OfferStart - PayStart;
        public int OfferTicks => ChargeStart - OfferStart;
        public int ChargeTicks => HoldStart - ChargeStart;
        public int HoldTicks => JudgeTick - HoldStart;
        /// <summary>제물이 마법진에 빨려 드는 시각(①부터).</summary>
        public readonly List<int> OfferAt = new List<int>();
        /// <summary>판정 전 시간표(시도 시작 기준 틱, 오름차순).</summary>
        public readonly List<EnhanceCue> Cues = new List<EnhanceCue>();
    }

    /// <summary>의식 시간표의 순수 규칙(08 요약서 10.6·10.8절, 07 요약서 5.3·5.4절).</summary>
    public static class EnhanceRitual
    {
        public const string BusWand = "wand";

        /// <summary>도전 단계 → 강도 0..5(TV-103: 2/4/7/10/13 → 1/2/3/4/5, +1 → 0).</summary>
        public static int Intensity(EnhanceScreenTuning t, int target)
        {
            int n = 0;
            var bounds = t.IntensityMinTarget;
            for (int i = 0; i < bounds.Length; i++) if (target >= bounds[i]) n = i;
            return n;
        }

        /// <summary>충전 게이지(TV-101): 처음 40% 시간에 80%, 남은 60% 시간에 마지막 20%.</summary>
        public static double ChargeGauge(EnhanceScreenTuning t, double t01)
        {
            if (t01 <= 0) return 0;
            if (t01 >= 1) return 1;
            double kt = t.GaugeKneeTime, kv = t.GaugeKneeValue;
            if (t01 <= kt) return t01 / kt * kv;
            return kv + (t01 - kt) / (1 - kt) * (1 - kv);
        }

        /// <summary>제물 i(0부터)가 들어가는 틱(TV-102: 9, 16, 23, 31, 38).</summary>
        public static int OfferTick(EnhanceScreenTuning t, int i) =>
            EnhanceScreenTuning.Ticks(t.PaySeconds) + EnhanceScreenTuning.Ticks(i * t.OfferIntervalSeconds);

        /// <summary>UseFullRitual(target) = enhance_ritual == full || (target ≥ +10 && !enhance_fast_allow_high) (TV-125·126).</summary>
        public static bool UseFullRitual(EnhanceScreenTuning t, int target, bool fastOn, bool fastAllowHigh) =>
            !fastOn || (target >= t.FastFullRitualMinTarget && !fastAllowHigh);

        /// <summary>설정과 연출 감도로 이번 시도의 방식을 정한다. 연출 감도 최소(2)는 결과 카드만.</summary>
        public static RitualStyle StyleFor(EnhanceScreenTuning t, int target, bool fastOn, bool fastAllowHigh, int effectsLevel)
        {
            if (effectsLevel >= 2) return RitualStyle.CardOnly;
            return UseFullRitual(t, target, fastOn, fastAllowHigh) ? RitualStyle.Full : RitualStyle.Fast;
        }

        /// <summary>판정 시각(초)만 셈하는 도우미(TV-097~099).</summary>
        public static double JudgeSeconds(EnhanceScreenTuning t, int intensity, bool hasSacrifice)
        {
            double offer = hasSacrifice ? t.OfferWithSacrificeSeconds : t.OfferWithoutSacrificeSeconds;
            return t.PaySeconds + offer + EnhanceScreenTuning.At(t.ChargeSeconds, intensity) + EnhanceScreenTuning.At(t.HoldSeconds, intensity);
        }

        public static RitualTimeline Build(EnhanceScreenTuning t, int target, int sacrificeCount, RitualStyle style)
        {
            var tl = new RitualTimeline
            {
                Target = target,
                Intensity = Intensity(t, target),
                Style = style,
                SacrificeCount = sacrificeCount,
            };
            int i = tl.Intensity;
            tl.Cues.Add(new EnhanceCue { Tick = 0, EventId = EnhanceSoundIds.Press, Bus = SoundBus.Ui, Pitch = 1f });
            if (style == RitualStyle.CardOnly)
            {
                tl.PayStart = tl.OfferStart = tl.ChargeStart = tl.HoldStart = tl.JudgeTick = 0;
                return tl;
            }
            tl.Cues.Add(new EnhanceCue { Tick = 0, EventId = EnhanceSoundIds.Pay, Bus = SoundBus.Ui, Pitch = 1f, Fx = EnhanceFx.Pay });
            if (style == RitualStyle.Fast)
            {
                tl.PayStart = tl.OfferStart = tl.ChargeStart = 0;
                tl.HoldStart = tl.JudgeTick = EnhanceScreenTuning.Ticks(t.FastChargeSeconds);
                tl.Cues.Add(new EnhanceCue { Tick = 0, EventId = EnhanceSoundIds.ChargeBegin, Bus = SoundBus.Wand, Pitch = 1f, Fx = EnhanceFx.Charge });
                return tl;
            }

            tl.PayStart = 0;
            tl.OfferStart = EnhanceScreenTuning.Ticks(t.PaySeconds);
            bool hasSac = sacrificeCount > 0;
            tl.ChargeStart = tl.OfferStart + EnhanceScreenTuning.Ticks(hasSac ? t.OfferWithSacrificeSeconds : t.OfferWithoutSacrificeSeconds);
            tl.HoldStart = tl.ChargeStart + EnhanceScreenTuning.Ticks(EnhanceScreenTuning.At(t.ChargeSeconds, i));
            tl.JudgeTick = tl.HoldStart + EnhanceScreenTuning.Ticks(EnhanceScreenTuning.At(t.HoldSeconds, i));

            for (int k = 0; k < sacrificeCount; k++)
            {
                int at = OfferTick(t, k);
                tl.OfferAt.Add(at);
                // 5음 음계를 한 음씩 올린다(E-07 순번 1~5) — 재생기에는 같은 사건과 음높이로 넘긴다.
                tl.Cues.Add(new EnhanceCue { Tick = at, EventId = EnhanceSoundIds.Offer, Bus = SoundBus.Ui, Pitch = SemitonePitch(EnhanceScreenTuning.At(t.OfferSemitones, k)), Fx = EnhanceFx.Offer });
            }
            tl.Cues.Add(new EnhanceCue { Tick = tl.ChargeStart, EventId = EnhanceSoundIds.ChargeBegin, Bus = SoundBus.Wand, Pitch = 1f, Fx = EnhanceFx.Charge });

            // 심장 박동(E-08): 강도 1 이상만(TV-100: +1 도전은 심장 소리 없음). 분당 시작값 → 끝값으로 곧게 빨라지고 음높이도 오른다.
            int bpmEnd = EnhanceScreenTuning.At(t.HeartBpmEnd, i);
            int chargeTicks = tl.HoldStart - tl.ChargeStart;
            if (i > 0 && bpmEnd > 0 && chargeTicks > 0)
            {
                double prevBeats = 0;
                double total = chargeTicks / (double)EnhanceScreenTuning.TicksPerSecond;
                int semis = EnhanceScreenTuning.At(t.PitchSemitones, i);
                for (int k = 1; k < chargeTicks; k++)
                {
                    double s = k / (double)EnhanceScreenTuning.TicksPerSecond;
                    double beats = (t.HeartBpmStart * s + (bpmEnd - t.HeartBpmStart) * s * s / (2 * total)) / 60.0;
                    if (Math.Floor(beats) > Math.Floor(prevBeats))
                    {
                        double frac = s / total;
                        tl.Cues.Add(new EnhanceCue
                        {
                            Tick = tl.ChargeStart + k, EventId = EnhanceSoundIds.ChargeProgress, Bus = SoundBus.Wand,
                            Pitch = (float)Math.Pow(2.0, semis * frac / 12.0), Fx = EnhanceFx.Heartbeat,
                        });
                    }
                    prevBeats = beats;
                }
            }
            if (tl.JudgeTick > tl.HoldStart)
                tl.Cues.Add(new EnhanceCue { Tick = tl.HoldStart, EventId = EnhanceSoundIds.Hold, Bus = SoundBus.Ui, Pitch = 1f, Fx = EnhanceFx.Hold });
            tl.Cues.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            return tl;
        }

        /// <summary>반음 수 → 음높이 배율(평균율: 12반음 = 2배).</summary>
        public static float SemitonePitch(int semitones) => (float)Math.Pow(2.0, semitones / 12.0);
    }
}
