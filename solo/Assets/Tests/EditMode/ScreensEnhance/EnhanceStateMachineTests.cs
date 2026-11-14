using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Enhance;

namespace Wizard.Solo.Tests.ScreensEnhance
{
    /// <summary>08 요약서 10.5절 전이표·10.7절 결과별 길이·10.8절 건너뛰기(TV-104~124 중 솔로분, 07 12.12 TV-Q-01~03).</summary>
    public sealed class EnhanceStateMachineTests
    {
        EnhanceScreenTuning T => EnhanceScreenTuning.From(TestContent.Db);

        static EnhanceResult Success(int before, int after, bool recordUpdated = true, int milestone = 0, bool firstTime = false)
        {
            var r = G23Results.Make(EnhanceOutcome.Success, before, after);
            r.RecordPeakUpdated = recordUpdated;
            r.MilestoneStage = milestone;
            r.MilestoneFirstTime = firstTime;
            return r;
        }

        /// <summary>결과 연출(ResultPlay) 구간의 길이를 잰다.</summary>
        int ResultPlayTicks(EnhanceResult r, int sacrifices = 1)
        {
            var m = G23Results.Model(T);
            m.Begin(r, sacrifices, RitualStyle.Full);
            G23Results.RunUntil(m, EnhancePhase.ResultPlay);
            Assert.AreEqual(EnhancePhase.ResultPlay, m.Phase);
            int n = 0;
            while (m.Phase == EnhancePhase.ResultPlay && n < 5000) { m.Tick(); n++; }
            return n;
        }

        [Test]
        public void S08_10_5__TransitionRowsForSuccess()
        {
            var m = G23Results.Model(T);
            Assert.AreEqual(EnhancePhase.Idle, m.Phase);
            Assert.AreEqual(EnhanceJResult.None, m.PressJ(), "대기의 J는 화면이 넣기·강화로 처리한다");
            m.Begin(Success(7, 8), 2, RitualStyle.Full);
            G23Results.RunUntilRest(m);
            CollectionAssert.AreEqual(new[]
            {
                EnhancePhase.Committing, EnhancePhase.Payment, EnhancePhase.Offering, EnhancePhase.Charge, EnhancePhase.Hold,
                EnhancePhase.Judge, EnhancePhase.ResultPlay, EnhancePhase.ResultCard
            }, m.History);
            Assert.AreEqual(EnhanceJResult.Retry, m.PressJ(), "결과 카드의 J = 같은 구성으로 다시 시도");
            m.Dismiss();
            Assert.AreEqual(EnhancePhase.Idle, m.Phase);
        }

        [Test]
        public void S08_10_5__ResultCardRetryBeginsAgain()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(7, 8), 0, RitualStyle.Full);
            G23Results.RunUntilRest(m);
            Assert.IsTrue(m.AcceptsSetup);
            Assert.IsTrue(m.Begin(G23Results.Make(EnhanceOutcome.FailDrop, 8, 7, 0, 1), 0, RitualStyle.Full));
            Assert.AreEqual(EnhancePhase.Payment, m.Phase);
        }

        [Test]
        public void S08_10_5__MutationOfferedGoesToChoiceThenAcceptToCard()
        {
            var m = G23Results.Model(T);
            m.Begin(G23Results.Make(EnhanceOutcome.MutationOffered, 7, 7), 0, RitualStyle.Full);
            G23Results.RunUntilRest(m);
            Assert.AreEqual(EnhancePhase.MutationChoice, m.Phase);
            var decision = G23Results.Make(EnhanceOutcome.MutationAccepted, 7, 7, 0, 1);
            Assert.IsTrue(m.Accept(decision));
            Assert.AreEqual(EnhancePhase.MutationAccept, m.Phase);
            G23Results.RunUntilRest(m);
            Assert.AreEqual(EnhancePhase.ResultCard, m.Phase);
            Assert.AreSame(decision, m.Shown);
        }

        [Test]
        public void S08_TV_106__OfflineNeverEntersWaitingLate()
        {
            foreach (var r in new[] { Success(7, 8), G23Results.Make(EnhanceOutcome.FailDrop, 7, 6, 0, 1), G23Results.Make(EnhanceOutcome.FailNoDrop, 2, 2, 0, 1) })
            {
                var m = G23Results.Model(T);
                m.Begin(r, 1, RitualStyle.Full);
                G23Results.RunUntilRest(m);
                CollectionAssert.DoesNotContain(m.History, EnhancePhase.WaitingLate);
            }
        }

        [Test]
        public void S08_TV_107__JIgnoredAtPoint2Seconds()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(7, 8), 1, RitualStyle.Full);
            for (int i = 0; i < 12; i++) m.Tick();
            Assert.AreEqual(EnhanceJResult.Ignored, m.PressJ());
            Assert.AreNotEqual(EnhancePhase.ShortResult, m.Phase);
        }

        [Test]
        public void S08_TV_108__JAtPoint5SkipsToShortResult()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(7, 8), 1, RitualStyle.Full);
            for (int i = 0; i < 30; i++) m.Tick();
            Assert.AreEqual(EnhanceJResult.Skipped, m.PressJ());
            Assert.AreEqual(EnhancePhase.ShortResult, m.Phase);
            int n = G23Results.RunUntilRest(m);
            Assert.AreEqual(30, n, "짧은 형태 0.5초 = 30틱");
            Assert.AreEqual(EnhancePhase.ResultCard, m.Phase);
        }

        [Test]
        public void S07_TV_Q_01__JAt12TicksIgnored()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(5, 6), 0, RitualStyle.Full);
            for (int i = 0; i < 12; i++) m.Tick();
            Assert.AreEqual(EnhanceJResult.Ignored, m.PressJ());
        }

        [Test]
        public void S07_TV_Q_02__JAfter18TicksSkips()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(5, 6), 0, RitualStyle.Full);
            for (int i = 0; i < 18; i++) m.Tick();
            Assert.AreEqual(EnhanceJResult.Skipped, m.PressJ());
            Assert.AreEqual(EnhancePhase.ShortResult, m.Phase);
        }

        [Test]
        public void S08_TV_109__JDuringMutationChoiceIgnored()
        {
            var m = G23Results.Model(T);
            m.Begin(G23Results.Make(EnhanceOutcome.MutationOffered, 7, 7), 0, RitualStyle.Full);
            G23Results.RunUntilRest(m);
            Assert.AreEqual(EnhancePhase.MutationChoice, m.Phase);
            Assert.AreEqual(EnhanceJResult.Ignored, m.PressJ());
            for (int i = 0; i < 600; i++) m.Tick();
            Assert.AreEqual(EnhancePhase.MutationChoice, m.Phase, "제한 시간 없음");
        }

        [Test]
        public void S07_TV_Q_03__SkipDuringRitualOfMutationStillShowsCard()
        {
            var m = G23Results.Model(T);
            m.Begin(G23Results.Make(EnhanceOutcome.MutationOffered, 7, 7), 0, RitualStyle.Full);
            for (int i = 0; i < 40; i++) m.Tick();
            Assert.AreEqual(EnhanceJResult.Skipped, m.PressJ());
            G23Results.RunUntilRest(m);
            Assert.AreEqual(EnhancePhase.MutationChoice, m.Phase, "변이 카드는 건너뛸 수 없다");
        }

        [Test]
        public void S08_TV_110__Plus13FirstReachLocksFirstTwoSeconds()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(12, 13, true, 13, true), 1, RitualStyle.Full);
            G23Results.RunUntil(m, EnhancePhase.Plus13);
            Assert.AreEqual(EnhancePhase.Plus13, m.Phase);
            for (int i = 0; i < 90; i++) m.Tick();
            Assert.AreEqual(EnhanceJResult.Ignored, m.PressJ(), "1.5초 J 무시");
            Assert.AreEqual(EnhancePhase.Plus13, m.Phase);
            for (int i = 0; i < 60; i++) m.Tick();
            Assert.AreEqual(EnhanceJResult.Skipped, m.PressJ(), "2.5초 J 넘김");
            Assert.AreEqual(EnhancePhase.ResultCard, m.Phase);
        }

        [Test]
        public void S08_TV_110__SkipBeforeJudgeOnPlus13FirstGoesToLockedPlus13()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(12, 13, true, 13, true), 1, RitualStyle.Full);
            for (int i = 0; i < 60; i++) m.Tick();
            Assert.AreEqual(EnhanceJResult.Skipped, m.PressJ());
            Assert.AreEqual(EnhancePhase.Plus13, m.Phase);
            Assert.AreEqual(EnhanceJResult.Ignored, m.PressJ());
        }

        [Test]
        public void S08_TV_111__MilestoneFirstReach150Ticks()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(6, 7, true, 7, true), 1, RitualStyle.Full);
            G23Results.RunUntil(m, EnhancePhase.Milestone);
            Assert.AreEqual(EnhancePhase.Milestone, m.Phase);
            Assert.AreEqual(150, m.PhaseLength);
        }

        [Test]
        public void S08_TV_112__MilestoneAgain48Ticks()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(6, 7, false, 7, false), 1, RitualStyle.Full);
            G23Results.RunUntil(m, EnhancePhase.Milestone);
            Assert.AreEqual(48, m.PhaseLength);
        }

        [Test]
        public void S08_TV_113__NoMilestoneAtPlus5()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(4, 5, true, 0, false), 1, RitualStyle.Full);
            G23Results.RunUntilRest(m);
            CollectionAssert.DoesNotContain(m.History, EnhancePhase.Milestone);
        }

        [Test]
        public void S08_TV_116__Plus13FullNineSecondsRepeatTwoPointTwo()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(12, 13, true, 13, true), 1, RitualStyle.Full);
            G23Results.RunUntil(m, EnhancePhase.Plus13);
            Assert.AreEqual(540, m.PhaseLength, "역대 최고 갱신 = 9초 전체");
            var m2 = G23Results.Model(T);
            m2.Begin(Success(12, 13, false, 13, false), 1, RitualStyle.Full);
            G23Results.RunUntil(m2, EnhancePhase.Plus13);
            Assert.AreEqual(132, m2.PhaseLength, "다시 도달 = 0.80~3.00(2.2초)");
            CollectionAssert.DoesNotContain(m2.History, EnhancePhase.Milestone);
        }

        [Test]
        public void S08_TV_118__GreatAtPlus12SlamsOnceThenPlus13()
        {
            var r = G23Results.Make(EnhanceOutcome.GreatSuccess, 12, 13);
            r.RecordPeakUpdated = true;
            r.MilestoneStage = 13;
            r.MilestoneFirstTime = true;
            var m = G23Results.Model(T);
            m.Begin(r, 0, RitualStyle.Full);
            G23Results.RunUntil(m, EnhancePhase.ResultPlay);
            Assert.AreEqual(72, m.PhaseLength, "첫 내려찍기(1.20) 직전에 +13으로 끊긴다");
            var cues = new List<EnhanceCue>();
            int slams = 0;
            while (m.Phase == EnhancePhase.ResultPlay) { m.Tick(); cues.Clear(); m.DrainCues(cues); foreach (var c in cues) if (c.Fx == EnhanceFx.SlamGold && m.Phase == EnhancePhase.ResultPlay) slams++; }
            Assert.AreEqual(0, slams, "대성공 연출 안의 두 번 찍기가 없다");
            Assert.AreEqual(EnhancePhase.Plus13, m.Phase);
        }

        [Test]
        public void S08_10_7__ResultLengthsAtIntensity3()
        {
            Assert.AreEqual(120, ResultPlayTicks(Success(7, 8, false)));
            Assert.AreEqual(216, ResultPlayTicks(G23Results.Make(EnhanceOutcome.GreatSuccess, 7, 9)));
            Assert.AreEqual(132, ResultPlayTicks(G23Results.Make(EnhanceOutcome.FailDrop, 7, 6, 0, 1)));
            Assert.AreEqual(144, ResultPlayTicks(G23Results.Make(EnhanceOutcome.FailCharmBlocked, 7, 7, 1, 1)));
            Assert.AreEqual(96, ResultPlayTicks(G23Results.Make(EnhanceOutcome.FailNoDrop, 7, 7, 0, 1)));
        }

        [Test]
        public void S08_10_7__FailGetsLongerNotBiggerWithIntensity()
        {
            // 0.50 뒤 침묵: 1단 0.2초(기준 3단 0.4초보다 0.2초 짧다), 4단 0.5초(0.1초 길다).
            Assert.AreEqual(84, ResultPlayTicks(G23Results.Make(EnhanceOutcome.FailNoDrop, 1, 1, 0, 1)));
            Assert.AreEqual(138, ResultPlayTicks(G23Results.Make(EnhanceOutcome.FailDrop, 9, 8, 0, 1)));
        }

        [Test]
        public void S08_10_5__GreatSuccessStartsLikeFailure()
        {
            var great = FirstResultCue(G23Results.Make(EnhanceOutcome.GreatSuccess, 7, 9));
            var fail = FirstResultCue(G23Results.Make(EnhanceOutcome.FailDrop, 7, 6, 0, 1));
            Assert.AreEqual(fail, great, "대성공의 첫 소리 = 실패의 첫 소리");
        }

        string FirstResultCue(EnhanceResult r)
        {
            var m = G23Results.Model(T);
            m.Begin(r, 0, RitualStyle.Full);
            var buf = new List<EnhanceCue>();
            while (m.Phase != EnhancePhase.ResultPlay) { m.Tick(); buf.Clear(); m.DrainCues(buf); }
            foreach (var c in buf) if (c.HasSound) return c.EventId;
            return null;
        }

        [Test]
        public void S08_TV_123__RejectReplaysFallbackFromHalfSecond()
        {
            var m = G23Results.Model(T);
            m.Begin(G23Results.Make(EnhanceOutcome.MutationOffered, 7, 7), 0, RitualStyle.Full);
            G23Results.RunUntilRest(m);
            m.DrainCues(new List<EnhanceCue>());   // 의식·변이 제시까지의 사건은 버린다
            var fb = G23Results.Make(EnhanceOutcome.FailDrop, 7, 6, 1, 2);
            Assert.IsTrue(m.Reject(fb));
            Assert.AreEqual(EnhancePhase.MutationRejectReplay, m.Phase);
            Assert.AreEqual(102, m.PhaseLength, "FailDrop 2.2초의 0.50 이후 = 1.7초");
            var cues = new List<EnhanceCue>();
            m.DrainCues(cues);
            Assert.AreEqual(EnhanceSoundIds.MutationReject, cues[0].EventId);
            bool stageDown = false;
            while (m.Phase == EnhancePhase.MutationRejectReplay)
            {
                m.Tick();
                cues.Clear();
                m.DrainCues(cues);
                foreach (var c in cues) if (c.EventId == "ui.enhance.stage_down") { stageDown = true; Assert.AreEqual(30, m.PhaseTick); }
            }
            Assert.IsTrue(stageDown, "단계 −1 연출");
            Assert.AreEqual(EnhancePhase.ResultCard, m.Phase);
            Assert.AreEqual(6, m.Shown.StageAfter);
            Assert.AreEqual(2, m.Shown.AuraAfter, "기운 +1");
        }

        [Test]
        public void S08_Contract__ResultDecidedBeforeRitualSkipKeepsSameResult()
        {
            var r = G23Results.Make(EnhanceOutcome.FailDrop, 7, 6, 0, 1);
            var m = G23Results.Model(T);
            m.Begin(r, 0, RitualStyle.Full);
            Assert.AreSame(r, m.Result, "Begin이 받은 결과가 곧 이번 시도의 결과");
            for (int i = 0; i < 40; i++) m.Tick();
            m.PressJ();
            G23Results.RunUntilRest(m);
            Assert.AreSame(r, m.Shown);
            Assert.AreEqual(EnhanceOutcome.FailDrop, m.Shown.Outcome);
            Assert.AreEqual(6, m.Shown.StageAfter);
        }

        [Test]
        public void S08_Contract__PendingOfferOpensMutationCardFirst()
        {
            var w = new WandData { Id = 3, Character = 2, Stage = 6, Aura = 1, PendingOffer = new MutationOffer
            {
                AttemptId = 9, Candidate = Ids.Mutation.Gluttony, FallbackOutcome = "FailDrop", FallbackStageAfter = 5, FallbackAuraAfter = 2
            } };
            var offer = EnhanceResultCardModel.FromOffer(w);
            Assert.AreEqual(EnhanceOutcome.MutationOffered, offer.Outcome);
            Assert.AreEqual(9, offer.AttemptId);
            Assert.AreEqual(EnhanceOutcome.FailDrop, offer.Fallback.Outcome);
            var m = G23Results.Model(T);
            m.ResumePendingOffer(offer);
            Assert.AreEqual(EnhancePhase.MutationChoice, m.Phase);
        }

        [Test]
        public void S08_Contract__ResultCardEmitsCharacterLineSound()
        {
            var m = G23Results.Model(T);
            m.Begin(Success(2, 3, false), 0, RitualStyle.Full);
            var all = new List<EnhanceCue>();
            while (m.Phase != EnhancePhase.ResultCard) { m.Tick(); m.DrainCues(all); }
            Assert.IsTrue(all.Exists(c => c.EventId == EnhanceSoundIds.ResultCard), "E-36 결과 카드 사건");
        }
    }
}
