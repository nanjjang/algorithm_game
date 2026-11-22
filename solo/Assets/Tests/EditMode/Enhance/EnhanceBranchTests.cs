using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>07 요약서 12.4절(결과 분기), 4.6절 분기 표의 모든 줄, 12.11절(오프라인 알림 없음).</summary>
    public class EnhanceBranchTests
    {
        EnhanceKit _kit;

        [SetUp]
        public void SetUp()
        {
            _kit = new EnhanceKit();
            _kit.Rich();
        }

        WandData At(int stage, int floorPeak, int aura, int charms = 0, int slots = 0)
        {
            WandData w = _kit.Set(stage, floorPeak, aura);
            _kit.SetSlots(w, slots);
            _kit.Profile.Item(Ids.Item.Charm).Qty = charms;
            return w;
        }

        int Scars(WandData w) => _kit.Service.ScarCount(w);

        // ------------------------------------------------------------ 12.4 결과 분기

        [Test]
        public void S07_TV_B_01__Stage3FailDoesNotDrop()
        {
            WandData w = At(3, 3, 0);
            EnhanceResult r = _kit.Fail(w);
            Assert.AreEqual(EnhanceOutcome.FailNoDrop, r.Outcome);
            Assert.AreEqual(3, w.Stage);
            Assert.AreEqual(1, w.Aura);
            Assert.AreEqual(0, w.DropCount);
        }

        [Test]
        public void S07_TV_B_02__Stage4FailDropsAndLeavesOneScar()
        {
            WandData w = At(4, 4, 0);
            EnhanceResult r = _kit.Fail(w);
            Assert.AreEqual(EnhanceOutcome.FailDrop, r.Outcome);
            Assert.AreEqual(3, w.Stage);
            Assert.AreEqual(4, w.FloorPeak);
            Assert.AreEqual(1, w.DropCount);
            Assert.AreEqual(1, w.Aura);
            Assert.AreEqual(1, Scars(w));
            Assert.IsTrue(r.ScarAdded);
            Assert.AreEqual(4, r.StageBefore);
            Assert.AreEqual(3, r.StageAfter);
        }

        [Test]
        public void S07_TV_B_03__Stage7Peak8FailDropsTo6()
        {
            WandData w = At(7, 8, 2);
            Assert.AreEqual(FailHint.DropTo, _kit.Service.HintFor(w.Id, false));
            EnhanceResult r = _kit.Fail(w);
            Assert.AreEqual(EnhanceOutcome.FailDrop, r.Outcome);
            Assert.AreEqual(6, w.Stage);
        }

        [Test]
        public void S07_TV_B_04__Stage6Peak8IsAtFloor()
        {
            WandData w = At(6, 8, 0);
            Assert.AreEqual(FailHint.AtFloor, _kit.Service.HintFor(w.Id, false));
            EnhanceResult r = _kit.Fail(w);
            Assert.AreEqual(EnhanceOutcome.FailNoDrop, r.Outcome);
            Assert.AreEqual(6, w.Stage);
            Assert.AreEqual(1, w.Aura);
            Assert.AreEqual(0, w.DropCount);
        }

        [Test]
        public void S07_TV_B_05__CharmOnAttemptThatCannotDropIsRefused()
        {
            WandData w = At(6, 8, 0, charms: 1);
            Assert.AreEqual(ErrorCode.CharmNotNeeded, _kit.Service.Enhance(w.Id, null, true).Error);
            Assert.AreEqual(ErrorCode.CharmNotNeeded, _kit.Preview(w, true).Blocked);
            Assert.AreEqual(1, _kit.Qty(Ids.Item.Charm));
            Assert.AreEqual(6, w.Stage);
        }

        [Test]
        public void S07_TV_B_06__CharmBlocksDropAndAuraStays()
        {
            WandData w = At(9, 9, 1, charms: 1);
            EnhanceResult r = _kit.Fail(w, useCharm: true);
            Assert.AreEqual(EnhanceOutcome.FailCharmBlocked, r.Outcome);
            Assert.AreEqual(9, w.Stage);
            Assert.AreEqual(1, w.Aura);
            Assert.AreEqual(0, _kit.Qty(Ids.Item.Charm));
            Assert.IsTrue(r.CharmConsumed);
            Assert.AreEqual(0, r.CharmAfter);
            Assert.AreEqual(0, w.DropCount);
            Assert.IsFalse(r.ScarAdded);
        }

        [Test]
        public void S07_TV_B_07__SuccessKeepsTheCharm()
        {
            WandData w = At(9, 9, 1, charms: 1);
            EnhanceResult r = _kit.Succeed(w, useCharm: true);
            Assert.AreEqual(EnhanceOutcome.Success, r.Outcome);
            Assert.AreEqual(10, w.Stage);
            Assert.AreEqual(0, w.Aura);
            Assert.AreEqual(1, _kit.Qty(Ids.Item.Charm));
            Assert.IsFalse(r.CharmConsumed);
        }

        [Test]
        public void S07_TV_B_08__AcceptedMutationKeepsStageAndCharm()
        {
            WandData w = At(9, 9, 1, charms: 1);
            EnhanceResult offer = _kit.Mutate(w, useCharm: true);
            Assert.AreEqual(EnhanceOutcome.MutationOffered, offer.Outcome);
            Assert.AreEqual(9, offer.StageAfter);
            EnhanceResult r = _kit.Resolve(w, accept: true);
            Assert.AreEqual(EnhanceOutcome.MutationAccepted, r.Outcome);
            Assert.AreEqual(9, w.Stage);
            Assert.AreEqual(2, w.Aura);
            Assert.AreEqual(1, _kit.Qty(Ids.Item.Charm));
            Assert.AreEqual(1, w.Mutations.Count);
            Assert.IsNull(w.PendingOffer);
        }

        [Test]
        public void S07_TV_B_09__DeclinedMutationWithCharmEqualsCharmBlocked()
        {
            WandData w = At(9, 9, 1, charms: 1);
            EnhanceResult offer = _kit.Mutate(w, useCharm: true);
            Assert.IsTrue(offer.HasFallback);
            Assert.AreEqual(EnhanceOutcome.FailCharmBlocked, offer.Fallback.Outcome);
            Assert.AreEqual(1, _kit.Qty(Ids.Item.Charm), "the charm is spent only when the offer is declined");
            EnhanceResult r = _kit.Resolve(w, accept: false);
            Assert.AreEqual(EnhanceOutcome.FailCharmBlocked, r.Outcome);
            Assert.AreEqual(9, w.Stage);
            Assert.AreEqual(1, w.Aura);
            Assert.AreEqual(0, _kit.Qty(Ids.Item.Charm));
            Assert.AreEqual(0, w.Mutations.Count);
        }

        [Test]
        public void S07_TV_B_10__DeclinedMutationWithoutCharmDrops()
        {
            WandData w = At(9, 9, 1);
            EnhanceResult offer = _kit.Mutate(w);
            Assert.AreEqual(EnhanceOutcome.FailDrop, offer.Fallback.Outcome);
            Assert.AreEqual(8, offer.Fallback.StageAfter);
            Assert.AreEqual(2, offer.Fallback.AuraAfter);
            EnhanceResult r = _kit.Resolve(w, accept: false);
            Assert.AreEqual(EnhanceOutcome.FailDrop, r.Outcome);
            Assert.AreEqual(8, w.Stage);
            Assert.AreEqual(2, w.Aura);
            Assert.AreEqual(1, w.DropCount);
            Assert.IsTrue(r.ScarAdded);
        }

        [Test]
        public void S07_TV_B_11__GreatSuccessAt12StopsAt13()
        {
            WandData w = At(12, 12, 0, slots: 0);
            EnhanceResult r = _kit.Great(w);
            Assert.AreEqual(EnhanceOutcome.GreatSuccess, r.Outcome);
            Assert.AreEqual(13, w.Stage);
            Assert.AreEqual(1, w.SlotsExtra);
            Assert.AreEqual(1, w.Loadout.Extra.Count);
            Assert.AreEqual(0, w.Aura);
            Assert.IsNull(r.ServerNotice, "offline: no server notice (TV-N-05)");
            Assert.AreEqual(13, r.MilestoneStage);
            Assert.IsTrue(r.MilestoneFirstTime);
        }

        [Test]
        public void S07_TV_B_12__GreatSuccessAt11ReachesThirdSlot()
        {
            WandData w = At(11, 11, 0, slots: 2);
            _kit.Great(w);
            Assert.AreEqual(13, w.Stage);
            Assert.AreEqual(3, w.SlotsExtra);
        }

        [Test]
        public void S07_TV_B_13__GreatSuccessWithFullSlotsReportsAlreadyFull()
        {
            WandData w = At(5, 5, 0, slots: 3);
            EnhanceResult r = _kit.Great(w);
            Assert.AreEqual(7, w.Stage);
            Assert.AreEqual(3, w.SlotsExtra);
            Assert.IsTrue(r.SlotAlreadyFull);
            Assert.IsFalse(r.SlotExpanded);
        }

        [Test]
        public void S07_TV_B_14__NoGreatSuccessAtStageZero()
        {
            WandData w = At(0, 0, 0);
            Assert.AreEqual(0, _kit.Preview(w).GreatBp);
            _kit.Rng.Push(0);
            EnhanceResult r = _kit.Service.Enhance(w.Id, null, false).Value;
            Assert.AreEqual(EnhanceOutcome.Success, r.Outcome);
            Assert.AreEqual(1, w.Stage);
            Assert.AreEqual(0, w.SlotsExtra);
        }

        [Test]
        public void S07_TV_B_15__AuraStopsAtThree()
        {
            WandData w = At(3, 3, 3);
            _kit.Fail(w);
            Assert.AreEqual(3, w.Aura);
        }

        [Test]
        public void S07_TV_B_16__SuccessBelowPeakKeepsOneScar()
        {
            WandData w = At(6, 8, 2);
            EnhanceResult r = _kit.Succeed(w);
            Assert.AreEqual(7, w.Stage);
            Assert.AreEqual(8, w.FloorPeak);
            Assert.AreEqual(0, w.Aura);
            Assert.AreEqual(1, Scars(w));
            Assert.IsFalse(r.RecordPeakUpdated);
        }

        [Test]
        public void S07_TV_B_17__ClimbingBackToPeakClearsScarsWithoutNewRecord()
        {
            WandData w = At(7, 8, 0);
            Assert.AreEqual(8, w.RecordPeak);
            EnhanceResult r = _kit.Succeed(w);
            Assert.AreEqual(8, w.Stage);
            Assert.AreEqual(0, Scars(w));
            Assert.IsFalse(r.RecordPeakUpdated);
        }

        [Test]
        public void S07_TV_B_18__GreatSuccessFrom8SetsBothPeaksTo10()
        {
            WandData w = At(8, 8, 0);
            EnhanceResult r = _kit.Great(w);
            Assert.AreEqual(10, w.Stage);
            Assert.AreEqual(10, w.FloorPeak);
            Assert.AreEqual(10, w.RecordPeak);
            Assert.IsTrue(r.RecordPeakUpdated);
            Assert.IsNull(r.ServerNotice, "offline: no server notice");
            Assert.AreEqual(10, r.MilestoneStage);
        }

        [Test]
        public void S07_TV_B_19__GreatSuccessAddsThirdSlot()
        {
            WandData w = At(8, 8, 0, slots: 2);
            EnhanceResult r = _kit.Great(w);
            Assert.AreEqual(10, w.Stage);
            Assert.AreEqual(3, w.SlotsExtra);
            Assert.AreEqual(3, w.Loadout.Extra.Count);
            Assert.IsTrue(r.SlotExpanded);
            Assert.IsFalse(r.SlotAlreadyFull);
        }

        [Test]
        public void S07_TV_B_20__ThirdFailStopsAtFloorPeakMinusTwo()
        {
            WandData w = At(6, 6, 0);
            Assert.AreEqual(EnhanceOutcome.FailDrop, _kit.Fail(w).Outcome);
            Assert.AreEqual(EnhanceOutcome.FailDrop, _kit.Fail(w).Outcome);
            Assert.AreEqual(4, w.Stage);
            Assert.AreEqual(6, w.FloorPeak);
            EnhanceResult third = _kit.Fail(w);
            Assert.AreEqual(EnhanceOutcome.FailNoDrop, third.Outcome);
            Assert.AreEqual(4, w.Stage);
            Assert.AreEqual(2, w.DropCount);
            Assert.AreEqual(2, Scars(w));
            Assert.IsFalse(third.ScarAdded);
        }

        // ------------------------------------------------------------ 4.6절 분기 표의 줄(위 벡터가 닿지 않는 칸)

        [Test]
        public void S07_4_6__SuccessRow()
        {
            WandData w = At(5, 5, 2, charms: 1);
            EnhanceResult r = _kit.Succeed(w, useCharm: true);
            Assert.AreEqual(6, r.StageAfter);
            Assert.AreEqual(2, r.AuraBefore);
            Assert.AreEqual(0, r.AuraAfter);
            Assert.AreEqual(0, w.SlotsExtra);
            Assert.AreEqual(1, _kit.Qty(Ids.Item.Charm));
            Assert.AreEqual(6, w.FloorPeak);
            Assert.AreEqual(6, w.RecordPeak);
            Assert.IsTrue(r.RecordPeakUpdated);
            Assert.IsFalse(r.HasFallback);
        }

        [Test]
        public void S07_4_6__GreatSuccessRowKeepsCharmAndResetsAura()
        {
            WandData w = At(5, 5, 3, charms: 1);
            EnhanceResult r = _kit.Great(w, useCharm: true);
            Assert.AreEqual(7, w.Stage);
            Assert.AreEqual(0, w.Aura);
            Assert.AreEqual(1, w.SlotsExtra);
            Assert.IsTrue(r.SlotExpanded);
            Assert.AreEqual(1, _kit.Qty(Ids.Item.Charm));
            Assert.IsFalse(r.CharmConsumed);
        }

        [Test]
        public void S07_4_6__FailDropRowWithoutCharm()
        {
            WandData w = At(9, 9, 1);
            EnhanceResult r = _kit.Fail(w);
            Assert.AreEqual(EnhanceOutcome.FailDrop, r.Outcome);
            Assert.AreEqual(8, w.Stage);
            Assert.AreEqual(2, w.Aura);
            Assert.AreEqual(1, w.DropCount);
            Assert.AreEqual(9, w.FloorPeak);
            Assert.AreEqual(9, w.RecordPeak);
            Assert.IsFalse(r.CharmConsumed);
        }

        [Test]
        public void S07_4_6__FailNoDropRowDoesNotSpendCharmAtLowStage()
        {
            // +3 이하: 부적 칸이 접혀 부적 없이 시도한다. 기운만 오른다.
            WandData w = At(2, 2, 0, charms: 2);
            EnhanceResult r = _kit.Fail(w);
            Assert.AreEqual(EnhanceOutcome.FailNoDrop, r.Outcome);
            Assert.AreEqual(2, w.Stage);
            Assert.AreEqual(1, w.Aura);
            Assert.AreEqual(2, _kit.Qty(Ids.Item.Charm));
        }

        [Test]
        public void S07_4_6__MutationAcceptedRowRaisesAuraAndPushesOldest()
        {
            WandData w = At(9, 9, 3, charms: 1);
            _kit.SetMutations(w, Ids.Mutation.TwistedBarrel, Ids.Mutation.Gluttony);
            EnhanceResult offer = _kit.Mutate(w, useCharm: true);
            Assert.AreEqual(Ids.Mutation.TwistedBarrel, offer.MutationPushedOut);
            EnhanceResult r = _kit.Resolve(w, accept: true);
            Assert.AreEqual(3, w.Aura, "aura is capped at 3");
            Assert.AreEqual(Ids.Mutation.TwistedBarrel, r.MutationPushedOut);
            Assert.AreEqual(2, w.Mutations.Count);
            Assert.AreEqual(1, _kit.Qty(Ids.Item.Charm), "a charm does not block a mutation");
        }

        [Test]
        public void S07_4_6__MutationDeclinedRowAtFloorIsFailNoDrop()
        {
            WandData w = At(6, 8, 0);
            EnhanceResult offer = _kit.Mutate(w);
            Assert.AreEqual(EnhanceOutcome.FailNoDrop, offer.Fallback.Outcome);
            EnhanceResult r = _kit.Resolve(w, accept: false);
            Assert.AreEqual(EnhanceOutcome.FailNoDrop, r.Outcome);
            Assert.AreEqual(6, w.Stage);
            Assert.AreEqual(1, w.Aura);
        }

        [Test]
        public void S07_4_6__AuraIsPerWand()
        {
            WandData a = At(3, 3, 0);
            WandData b = _kit.Set(3, 3, 0, character: 2);
            _kit.Fail(a);
            Assert.AreEqual(1, a.Aura);
            Assert.AreEqual(0, b.Aura, "aura does not move to another wand");
        }

        [Test]
        public void S07_4_6__SameRulesForAllFiveWands()
        {
            // 다섯 지팡이가 같은 확률·비용·실패 규칙(불변식): 같은 상태면 분해가 같다.
            ProbBreakdown first = default;
            for (int c = 1; c <= 5; c++)
            {
                WandData w = _kit.Set(8, 9, 2, character: c);
                ProbBreakdown b = _kit.Preview(w);
                if (c == 1) { first = b; continue; }
                Assert.AreEqual(first.SuccessPct, b.SuccessPct, "char " + c);
                Assert.AreEqual(first.GreatBp, b.GreatBp, "char " + c);
                Assert.AreEqual(first.MutationEndBp, b.MutationEndBp, "char " + c);
                Assert.AreEqual(first.CostGold, b.CostGold, "char " + c);
                Assert.AreEqual(first.StoneId, b.StoneId, "char " + c);
                Assert.AreEqual(first.DropPossible, b.DropPossible, "char " + c);
                Assert.AreEqual(first.FloorStage, b.FloorStage, "char " + c);
            }
        }

        // ------------------------------------------------------------ 4.1절 전제

        [Test]
        public void S07_4_1__MaxStageIsRefused()
        {
            WandData w = At(13, 13, 0);
            Assert.AreEqual(ErrorCode.MaxStage, _kit.Service.Enhance(w.Id, null, false).Error);
            Assert.AreEqual(ErrorCode.MaxStage, _kit.Preview(w).Blocked);
        }

        [Test]
        public void S07_4_1__CharmWantedButNoneOwned()
        {
            WandData w = At(9, 9, 0, charms: 0);
            Assert.AreEqual(ErrorCode.NotEnoughCharm, _kit.Service.Enhance(w.Id, null, true).Error);
        }

        [Test]
        public void S07_4_1__UnknownWandIsNotFound()
        {
            Assert.AreEqual(ErrorCode.NotFound, _kit.Service.Enhance(999, null, false).Error);
            Assert.AreEqual(ErrorCode.NotFound, _kit.Service.Preview(999, null, false).Blocked);
        }

        [Test]
        public void S07_4_1__CharmHeldForAnotherWandsPendingOfferIsNotFree()
        {
            // 한 자루가 변이 결정을 기다리며 부적 1개를 맡아 두었으면(거절하면 탄다) 다른 자루가 그 부적을 쓰지 못한다.
            WandData a = At(9, 9, 0, charms: 1);
            _kit.Mutate(a, useCharm: true);
            WandData b = _kit.Set(9, 9, 0, character: 2);
            Assert.AreEqual(ErrorCode.NotEnoughCharm, _kit.Service.Enhance(b.Id, null, true).Error);
            _kit.Resolve(a, accept: true);
            Assert.AreEqual(ErrorCode.None, _kit.Preview(b, true).Blocked);
        }

        // ------------------------------------------------------------ 4.11절 마일스톤, 4.12절 꾸러미

        [Test]
        public void S07_4_11__MilestoneFirstTimeOnlyWithNewRecord()
        {
            WandData w = At(3, 3, 0);
            EnhanceResult first = _kit.Succeed(w);
            Assert.AreEqual(4, first.MilestoneStage);
            Assert.IsTrue(first.MilestoneFirstTime);
            _kit.Fail(w);                         // +4 → +3
            EnhanceResult again = _kit.Succeed(w);
            Assert.AreEqual(4, again.MilestoneStage);
            Assert.IsFalse(again.MilestoneFirstTime, "reached before: short banner only");
        }

        [Test]
        public void S07_4_11__GreatSuccessSkippingMilestoneCountsIt()
        {
            WandData w = At(3, 3, 0);
            EnhanceResult r = _kit.Great(w);      // +3 → +5, 건넌 +4도 도달
            Assert.AreEqual(5, w.Stage);
            Assert.AreEqual(4, r.MilestoneStage);
            Assert.IsTrue(r.MilestoneFirstTime);
        }

        [Test]
        public void S07_4_11__HighestMilestoneWhenSeveral()
        {
            Assert.AreEqual(7, EnhanceRules.MilestoneReached(_kit.Cfg, 3, 7));
            Assert.AreEqual(0, EnhanceRules.MilestoneReached(_kit.Cfg, 4, 6));
            Assert.AreEqual(13, EnhanceRules.MilestoneReached(_kit.Cfg, 12, 13));
        }

        [Test]
        public void S07_4_11__TitleBands()
        {
            int[] band = { 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4 };
            for (int s = 0; s <= 13; s++) Assert.AreEqual(band[s], EnhanceRules.TitleBand(_kit.Cfg, s), "stage " + s);
            Assert.AreEqual("title.stage.2.b3", EnhanceRules.StageTitleId(_kit.Cfg, 2, 11));
        }

        [Test]
        public void S07_4_12__ResultBundleCarriesRollBandsAndBalances()
        {
            WandData w = At(7, 7, 0);
            long goldBefore = _kit.Gold;
            ProbBreakdown b = _kit.Preview(w);
            _kit.Rng.Push(2499);                  // 25% → 성공 구간 끝 2500의 바로 앞
            EnhanceResult r = _kit.Service.Enhance(w.Id, null, false).Value;
            Assert.AreEqual(EnhanceOutcome.Success, r.Outcome);
            Assert.AreEqual(2499, r.RollBp);
            Assert.AreEqual(b.SuccessBp, r.Breakdown.SuccessBp);
            Assert.AreEqual(2500, r.Breakdown.SuccessBp);
            Assert.AreEqual(6400, r.Breakdown.CostGold);
            Assert.AreEqual(goldBefore - 6400, r.GoldAfter);
            Assert.AreSame(w, r.Wand);
            Assert.AreEqual(_kit.Qty(Ids.Item.StoneMid), r.StoneMidAfter);
            Assert.AreEqual(_kit.Qty(Ids.Item.StoneLow), r.StoneLowAfter);
            Assert.AreEqual(_kit.Qty(Ids.Item.StoneHigh), r.StoneHighAfter);
        }

        [Test]
        public void S07_4_12__AttemptIdsIncreaseAndEachAttemptSavesAndLogs()
        {
            WandData w = At(0, 0, 0);
            long firstId = _kit.Profile.NextAttemptId;
            int saves = _kit.Store.SaveCount;
            EnhanceResult a = _kit.Succeed(w);
            EnhanceResult b = _kit.Succeed(w);
            Assert.AreEqual(firstId, a.AttemptId);
            Assert.AreEqual(firstId + 1, b.AttemptId);
            Assert.AreEqual(firstId + 2, _kit.Profile.NextAttemptId);
            Assert.AreEqual(saves + 2, _kit.Store.SaveCount);
            Assert.AreEqual(2, _kit.Profile.EnhanceLogTail.Count);
            Assert.AreEqual(b.AttemptId, (long)_kit.Profile.EnhanceLogTail[1]["attempt_id"]);
        }

        // ------------------------------------------------------------ 12.11 알림 (오프라인)

        [Test]
        public void S07_TV_N_05__OfflinePlus13HasNoServerNotice()
        {
            WandData w = At(12, 12, 0);
            EnhanceResult r = _kit.Succeed(w);
            Assert.AreEqual(13, w.Stage);
            Assert.IsNull(r.ServerNotice);
        }

        [Test]
        public void S07_TV_N_05__NoOutcomeEverCarriesANotice()
        {
            WandData w = At(9, 9, 0);
            Assert.IsNull(_kit.Succeed(w).ServerNotice);           // +9 → +10
            Assert.IsNull(_kit.Fail(w).ServerNotice);
            w = At(9, 9, 0);
            Assert.IsNull(_kit.Great(w).ServerNotice);             // +9 → +11
            w = At(9, 9, 0);
            Assert.IsNull(_kit.Mutate(w).ServerNotice);
            Assert.IsNull(_kit.Resolve(w, true).ServerNotice);
        }

        // ------------------------------------------------------------ 4.9절 흔적, 4.10절 배율

        [Test]
        public void S07_4_9__ScarCountAndVisibility()
        {
            WandData w = At(5, 7, 0);
            w.RecordPeak = 7;
            Assert.AreEqual(2, Scars(w));              // 08 TV-144: +5 최고 +7 → 2
            w.RecordPeak = 6; w.FloorPeak = 6;
            Assert.AreEqual(1, Scars(w));              // 최고 +6 → 1
            Assert.IsFalse(w.ShowScars, "default off");
            int saves = _kit.Store.SaveCount;
            Assert.IsTrue(_kit.Service.SetScarVisibility(w.Id, true).Value.ShowScars);
            Assert.AreEqual(saves + 1, _kit.Store.SaveCount);
            Assert.IsFalse(_kit.Wand(2).ShowScars, "per wand");
            Assert.AreEqual(ErrorCode.NotFound, _kit.Service.SetScarVisibility(999, true).Error);
        }

        [Test]
        public void S07_4_10__StageDamageMultiplierTable()
        {
            double[] table = { 1.0000, 1.1500, 1.3225, 1.5209, 1.7490, 2.0114, 2.3131, 2.6600, 3.0590, 3.5179, 4.0456, 4.6524, 5.3503, 6.1528 };
            for (int s = 0; s <= 13; s++) Assert.AreEqual(table[s], _kit.Service.StageDamageMul(s), 0.00006, "stage " + s);
        }
    }
}
