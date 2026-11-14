using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Enhance;

namespace Wizard.Solo.Tests.ScreensEnhance
{
    /// <summary>08 요약서 10.6·10.8절 의식 시간표 벡터(TV-097~103, TV-125·126, 07 12.12 TV-Q-04·05).</summary>
    public sealed class EnhanceTimelineTests
    {
        EnhanceScreenTuning T => EnhanceScreenTuning.From(TestContent.Db);

        [Test]
        public void S08_TV_103__IntensityByTarget()
        {
            Assert.AreEqual(1, EnhanceRitual.Intensity(T, 2));
            Assert.AreEqual(2, EnhanceRitual.Intensity(T, 4));
            Assert.AreEqual(3, EnhanceRitual.Intensity(T, 7));
            Assert.AreEqual(4, EnhanceRitual.Intensity(T, 10));
            Assert.AreEqual(5, EnhanceRitual.Intensity(T, 13));
            Assert.AreEqual(0, EnhanceRitual.Intensity(T, 1));
            Assert.AreEqual(1, EnhanceRitual.Intensity(T, 3));
            Assert.AreEqual(2, EnhanceRitual.Intensity(T, 6));
            Assert.AreEqual(3, EnhanceRitual.Intensity(T, 9));
            Assert.AreEqual(4, EnhanceRitual.Intensity(T, 12));
        }

        [Test]
        public void S08_TV_097__Intensity3WithSacrificeSegments()
        {
            var tl = EnhanceRitual.Build(T, 8, 2, RitualStyle.Full);
            Assert.AreEqual(9, tl.OfferStart);
            Assert.AreEqual(45, tl.ChargeStart);
            Assert.AreEqual(153, tl.HoldStart);
            Assert.AreEqual(174, tl.JudgeTick);
        }

        [Test]
        public void S08_TV_098__Intensity3WithoutSacrificeJudgeAt150()
        {
            var tl = EnhanceRitual.Build(T, 8, 0, RitualStyle.Full);
            Assert.AreEqual(150, tl.JudgeTick);
            Assert.AreEqual(2.50, EnhanceRitual.JudgeSeconds(T, 3, false), 1e-9);
        }

        [Test]
        public void S08_TV_099__JudgeSecondsByIntensity()
        {
            Assert.AreEqual(1.70, EnhanceRitual.JudgeSeconds(T, 1, true), 1e-9);
            Assert.AreEqual(2.20, EnhanceRitual.JudgeSeconds(T, 2, true), 1e-9);
            Assert.AreEqual(3.65, EnhanceRitual.JudgeSeconds(T, 4, true), 1e-9);
            Assert.AreEqual(4.75, EnhanceRitual.JudgeSeconds(T, 5, true), 1e-9);
            Assert.AreEqual(102, EnhanceRitual.Build(T, 2, 1, RitualStyle.Full).JudgeTick);
            Assert.AreEqual(132, EnhanceRitual.Build(T, 5, 1, RitualStyle.Full).JudgeTick);
            Assert.AreEqual(219, EnhanceRitual.Build(T, 11, 1, RitualStyle.Full).JudgeTick);
            Assert.AreEqual(285, EnhanceRitual.Build(T, 13, 1, RitualStyle.Full).JudgeTick);
        }

        [Test]
        public void S08_TV_100__PlusOneChargeShortNoHoldNoHeartbeat()
        {
            var tl = EnhanceRitual.Build(T, 1, 0, RitualStyle.Full);
            Assert.AreEqual(24, tl.ChargeTicks);
            Assert.AreEqual(0, tl.HoldTicks);
            foreach (var c in tl.Cues)
            {
                Assert.AreNotEqual(EnhanceSoundIds.ChargeProgress, c.EventId, "심장 소리 사건이 없어야 한다");
                Assert.AreNotEqual(EnhanceSoundIds.Hold, c.EventId, "숨 멈춤이 없어야 한다");
            }
            // 결과는 Success만: +1 도전의 확률은 100%, 대성공 0.
            var m = ProbabilityDisplayModel.Compute(TestContent.Db.Enhance, 0, 0, 0, 0, false, false);
            Assert.AreEqual(10000, m.Band.SuccessBp);
            Assert.AreEqual(0, m.Band.GreatBp + m.Band.MutationBp + m.Band.FailBp);
        }

        [Test]
        public void S08_TV_101__ChargeGaugeCurve()
        {
            Assert.AreEqual(0.4, EnhanceRitual.ChargeGauge(T, 0.2), 1e-9);
            Assert.AreEqual(0.8, EnhanceRitual.ChargeGauge(T, 0.4), 1e-9);
            Assert.AreEqual(0.9, EnhanceRitual.ChargeGauge(T, 0.7), 1e-9);
            Assert.AreEqual(1.0, EnhanceRitual.ChargeGauge(T, 1.0), 1e-9);
        }

        [Test]
        public void S08_TV_102__OfferTicksForFiveSacrifices()
        {
            int[] want = { 9, 16, 23, 31, 38 };
            for (int i = 0; i < want.Length; i++) Assert.AreEqual(want[i], EnhanceRitual.OfferTick(T, i), "제물 " + i);
            var tl = EnhanceRitual.Build(T, 8, 5, RitualStyle.Full);
            CollectionAssert.AreEqual(want, tl.OfferAt);
        }

        [Test]
        public void S08_TV_125__FastOnFullRitualFromPlus10()
        {
            Assert.IsFalse(EnhanceRitual.UseFullRitual(T, 9, true, false));
            Assert.IsTrue(EnhanceRitual.UseFullRitual(T, 10, true, false));
        }

        [Test]
        public void S08_TV_126__FastAllowHighUnlocksPlus10()
        {
            Assert.IsFalse(EnhanceRitual.UseFullRitual(T, 10, true, true));
        }

        [Test]
        public void S07_TV_Q_04__FastStage9StillFullRitual()
        {
            Assert.AreEqual(RitualStyle.Full, EnhanceRitual.StyleFor(T, 9 + 1, true, false, 0));
        }

        [Test]
        public void S07_TV_Q_05__FastStage5ChargePoint3AttemptPoint8()
        {
            var t = T;
            Assert.AreEqual(RitualStyle.Fast, EnhanceRitual.StyleFor(t, 6, true, false, 0));
            var tl = EnhanceRitual.Build(t, 6, 2, RitualStyle.Fast);
            Assert.AreEqual(18, tl.ChargeTicks);
            var m = G23Results.Model(t);
            m.Begin(G23Results.Make(EnhanceOutcome.Success, 5, 6), 2, RitualStyle.Fast);
            int ticks = G23Results.RunUntilRest(m);
            Assert.AreEqual(EnhancePhase.ResultCard, m.Phase);
            Assert.AreEqual(48, ticks, "시도 1회 약 0.8초(48틱)");
            CollectionAssert.DoesNotContain(m.History, EnhancePhase.Hold, "빠른 강화엔 숨 멈춤이 없다");
            CollectionAssert.Contains(m.History, EnhancePhase.ShortResult);
        }

        [Test]
        public void S08_Contract__EnhanceRitualSettingFastGivesShortForm()
        {
            var s = new SettingsData();
            SettingsOptions.Set(s, OptionKeys.EnhanceRitual, OptionValues.Fast);
            bool fast = SettingsOptions.Str(s, OptionKeys.EnhanceRitual) == OptionValues.Fast;
            Assert.AreEqual(RitualStyle.Fast, EnhanceRitual.StyleFor(T, 3, fast, SettingsOptions.Bool(s, OptionKeys.EnhanceFastAllowHigh), SettingsOptions.EffectsLevel(s)));
            var m = G23Results.Model(T);
            m.Begin(G23Results.Make(EnhanceOutcome.FailNoDrop, 2, 2, 0, 1), 0, RitualStyle.Fast);
            G23Results.RunUntilRest(m);
            CollectionAssert.Contains(m.History, EnhancePhase.ShortResult, "짧은 형태");
            CollectionAssert.DoesNotContain(m.History, EnhancePhase.ResultPlay);
        }

        [Test]
        public void S08_14_2__MinimalEffectsShowsResultCardOnly()
        {
            Assert.AreEqual(RitualStyle.CardOnly, EnhanceRitual.StyleFor(T, 8, false, false, 2));
            var m = G23Results.Model(T);
            m.Begin(G23Results.Make(EnhanceOutcome.Success, 7, 8), 0, RitualStyle.CardOnly);
            Assert.AreEqual(EnhancePhase.ResultCard, m.Phase);
            var mm = G23Results.Model(T);
            mm.Begin(G23Results.Make(EnhanceOutcome.MutationOffered, 7, 7), 0, RitualStyle.CardOnly);
            Assert.AreEqual(EnhancePhase.MutationChoice, mm.Phase, "변이 카드는 그대로");
        }

        [Test]
        public void S08_Contract__ChargeSoundCuesIndependentOfResult()
        {
            var outcomes = new[]
            {
                G23Results.Make(EnhanceOutcome.Success, 7, 8),
                G23Results.Make(EnhanceOutcome.GreatSuccess, 7, 9),
                G23Results.Make(EnhanceOutcome.FailDrop, 7, 6, 0, 1),
                G23Results.Make(EnhanceOutcome.FailCharmBlocked, 7, 7),
                G23Results.Make(EnhanceOutcome.FailNoDrop, 7, 7, 0, 1),
                G23Results.Make(EnhanceOutcome.MutationOffered, 7, 7),
            };
            List<string> first = null;
            foreach (var r in outcomes)
            {
                var seq = RitualCues(r, out int judgeTick);
                Assert.AreEqual(174, judgeTick, "판정 시각은 결과와 무관하게 174틱");
                if (first == null) first = seq;
                else CollectionAssert.AreEqual(first, seq, "결과 " + r.Outcome + "의 충전 구간 사건이 달라졌다");
            }
            CollectionAssert.Contains(first, "0:" + EnhanceSoundIds.Press);
            CollectionAssert.Contains(first, "0:" + EnhanceSoundIds.Pay);
            CollectionAssert.Contains(first, "45:" + EnhanceSoundIds.ChargeBegin);
            CollectionAssert.Contains(first, "153:" + EnhanceSoundIds.Hold);
        }

        /// <summary>판정 전까지 나온 사건 "시도 틱:사건 id" 목록.</summary>
        List<string> RitualCues(EnhanceResult r, out int judgeTick)
        {
            var m = G23Results.Model(T);
            m.Begin(r, 3, RitualStyle.Full);
            var seq = new List<string>();
            var buf = new List<EnhanceCue>();
            m.DrainCues(buf);
            foreach (var c in buf) seq.Add("0:" + c.EventId);
            int guard = 0;
            while (guard++ < 1000)
            {
                m.Tick();
                buf.Clear();
                m.DrainCues(buf);
                if (m.Phase == EnhancePhase.ResultPlay) break;
                foreach (var c in buf) seq.Add(m.AttemptTick + ":" + c.EventId);
            }
            judgeTick = m.AttemptTick;
            return seq;
        }
    }
}
