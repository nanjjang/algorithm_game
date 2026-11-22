using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>07 요약서 12.12절(건너뛰기·빠른·이어서 강화의 규칙 쪽)과 3.1절 불변식(매 시도 뒤 검사).</summary>
    public class EnhanceFlowAndInvariantTests
    {
        EnhanceKit _kit;
        EnhanceTuning _tuning;

        [SetUp]
        public void SetUp()
        {
            _kit = new EnhanceKit();
            _kit.Rich();
            _tuning = _kit.Service.Tuning;
        }

        // ------------------------------------------------------------ 12.12

        [Test]
        public void S07_TV_Q_01__SkipWithin12TicksIsIgnored()
        {
            Assert.IsFalse(EnhanceFlowRules.CanSkip(_tuning, 12, false, -1));
            Assert.IsFalse(EnhanceFlowRules.CanSkip(_tuning, 17, false, -1));
        }

        [Test]
        public void S07_TV_Q_02__SkipFrom18TicksGoesToShortResult()
        {
            Assert.IsTrue(EnhanceFlowRules.CanSkip(_tuning, 18, false, -1));
            Assert.IsTrue(EnhanceFlowRules.CanSkip(_tuning, 60, false, -1));
            Assert.AreEqual(30, _tuning.ShortResultTicks);   // 짧은 결과 0.5초
        }

        [Test]
        public void S07_TV_Q_03__MutationCardCannotBeSkipped()
        {
            Assert.IsFalse(EnhanceFlowRules.CanSkip(_tuning, 600, true, -1));
        }

        [Test]
        public void S07_5_4__FirstPlus13CannotBeSkippedForTwoSeconds()
        {
            Assert.IsFalse(EnhanceFlowRules.CanSkip(_tuning, 600, false, 0));
            Assert.IsFalse(EnhanceFlowRules.CanSkip(_tuning, 600, false, 119));
            Assert.IsTrue(EnhanceFlowRules.CanSkip(_tuning, 600, false, 120));
        }

        [Test]
        public void S07_TV_Q_04__FastModeStillRunsFullRitualFromStage9()
        {
            Assert.IsTrue(EnhanceFlowRules.UsesFullRitual(_tuning, true, 9, false));
            Assert.IsTrue(EnhanceFlowRules.UsesFullRitual(_tuning, true, 12, false));
            Assert.IsFalse(EnhanceFlowRules.UsesFullRitual(_tuning, true, 9, true), "the setting lifts it");
            Assert.IsFalse(EnhanceFlowRules.UsesFullRitual(_tuning, true, 8, false));
        }

        [Test]
        public void S07_TV_Q_05__FastModeAtStage5()
        {
            Assert.IsFalse(EnhanceFlowRules.UsesFullRitual(_tuning, true, 5, false));
            Assert.AreEqual(18, _tuning.FastChargeTicks);    // 충전 0.3초
            Assert.AreEqual(48, _tuning.FastAttemptTicks);   // 시도 약 0.8초
            Assert.IsTrue(EnhanceFlowRules.UsesFullRitual(_tuning, false, 5, false), "fast mode off");
        }

        [Test]
        public void S07_TV_Q_06__AutoContinueStopsOnGreatSuccess()
        {
            WandData w = _kit.Set(5, 5, 0);
            EnhanceResult r = _kit.Great(w);
            Assert.AreEqual(AutoStopReason.GreatSuccess, EnhanceFlowRules.StopAfter(r, 7, true));
        }

        [Test]
        public void S07_TV_Q_07__AutoContinueStopsOnMilestone()
        {
            WandData w = _kit.Set(3, 3, 0);
            EnhanceResult r = _kit.Succeed(w);
            Assert.AreEqual(AutoStopReason.Milestone, EnhanceFlowRules.StopAfter(r, 6, true));
        }

        [Test]
        public void S07_TV_Q_08__AutoContinueStopsWhenSacrificesRunShort()
        {
            // 구성은 같은 속성 일반 블록 3개인데 서고에 2개뿐 → 다음 시도는 멈춘다.
            _kit.Give(Ids.Block.Frost, 2);
            var template = new List<string> { Ids.Block.Frost, Ids.Block.Frost, Ids.Block.Frost };
            List<string> picked = EnhanceFlowRules.PickSacrifices(_kit.Ctx.Content, _kit.Profile, template);
            Assert.IsNull(picked);
            WandData w = _kit.Set(5, 5, 0);
            Assert.AreEqual(AutoStopReason.SacrificeShort, EnhanceFlowRules.StopBefore(_kit.Preview(w), picked));
        }

        [Test]
        public void S07_5_4__SameConfigurationFallsBackToSameRarityAndElement()
        {
            // 같은 block id 우선, 없으면 같은 (등급, 속성)의 다른 블록(U-21). 일반·비전 = 마력탄, 관통.
            _kit.Give(Ids.Block.Pierce, 1);
            _kit.Give(Ids.Block.MagicBolt, 1);
            var template = new List<string> { Ids.Block.Pierce, Ids.Block.Pierce };
            List<string> picked = EnhanceFlowRules.PickSacrifices(_kit.Ctx.Content, _kit.Profile, template);
            CollectionAssert.AreEqual(new[] { Ids.Block.Pierce, Ids.Block.MagicBolt }, picked);
            // 끼운 블록은 세지 않는다(새 프로필의 마력탄 5개는 전부 끼워 둔 것).
            _kit.Profile.Item(Ids.Block.MagicBolt).Qty -= 1;
            Assert.IsNull(EnhanceFlowRules.PickSacrifices(_kit.Ctx.Content, _kit.Profile, template));
            Assert.AreEqual(0, EnhanceFlowRules.PickSacrifices(_kit.Ctx.Content, _kit.Profile, null).Count);
        }

        [Test]
        public void S07_TV_Q_09__AutoContinueStopsOnDropWhenAsked()
        {
            WandData w = _kit.Set(5, 5, 0);
            EnhanceResult r = _kit.Fail(w);
            Assert.AreEqual(EnhanceOutcome.FailDrop, r.Outcome);
            Assert.AreEqual(AutoStopReason.Dropped, EnhanceFlowRules.StopAfter(r, 7, true));
            Assert.AreEqual(AutoStopReason.None, EnhanceFlowRules.StopAfter(r, 7, false));
            Assert.IsTrue(_tuning.AutoStopOnDropDefault);
        }

        [Test]
        public void S07_TV_Q_10__OneResultPerRequest()
        {
            // 시도마다 따로 판정한다: 호출 한 번 = 난수 한 번 = 결과 하나.
            WandData w = _kit.Set(5, 5, 0);
            long nextId = _kit.Profile.NextAttemptId;
            for (int i = 0; i < 4; i++)
            {
                int calls = _kit.Rng.Calls;
                EnhanceResult r = _kit.Fail(_kit.Set(5, 7, 0));
                Assert.AreEqual(calls + 1, _kit.Rng.Calls);
                Assert.AreEqual(nextId + i, r.AttemptId);
            }
            Assert.AreEqual(4, _kit.Profile.EnhanceLogTail.Count);
            Assert.IsNotNull(w);
        }

        [Test]
        public void S07_5_4__OtherStopConditions()
        {
            WandData w = _kit.Set(5, 5, 0);
            Assert.AreEqual(AutoStopReason.TargetReached, EnhanceFlowRules.StopAfter(_kit.Succeed(w), 6, true));
            w = _kit.Set(5, 5, 0);
            Assert.AreEqual(AutoStopReason.None, EnhanceFlowRules.StopAfter(_kit.Succeed(w), 7, true));
            w = _kit.Set(5, 5, 0);
            Assert.AreEqual(AutoStopReason.MutationOffered, EnhanceFlowRules.StopAfter(_kit.Mutate(w), 7, true));
            _kit.Resolve(w, false);

            // 골드·강화석·부적이 모자라면 시도 전에 멈춘다.
            w = _kit.Set(5, 5, 0);
            _kit.Gold = 0;
            Assert.AreEqual(AutoStopReason.CannotAttempt, EnhanceFlowRules.StopBefore(_kit.Preview(w), new List<string>()));
            _kit.Gold = 1_000_000;
            Assert.AreEqual(AutoStopReason.None, EnhanceFlowRules.StopBefore(_kit.Preview(w), new List<string>()));
            Assert.AreEqual(AutoStopReason.CannotAttempt, EnhanceFlowRules.StopBefore(_kit.Preview(w, true), new List<string>()), "charm wanted, none owned");

            // 하락 없는 시도로 바뀌면 그 시도엔 부적을 쓰지 않는다.
            Assert.IsFalse(EnhanceFlowRules.AutoUseCharm(true, false));
            Assert.IsTrue(EnhanceFlowRules.AutoUseCharm(true, true));
            Assert.IsFalse(EnhanceFlowRules.AutoUseCharm(false, true));
        }

        [Test]
        public void S07_5_4__TallyCountsEveryNonSuccessAsFailure()
        {
            // 실패 수에는 변이·부적이 막음·하락 없음이 든다 [임시]. 쓴 골드는 시도마다의 비용.
            var tally = new EnhanceTally();
            _kit.Profile.Item(Ids.Item.Charm).Qty = 1;
            WandData w = _kit.Set(5, 5, 0);
            tally.Add(_kit.Succeed(w), false);                   // 3,600
            tally.Add(_kit.Fail(w, useCharm: true), false);      // +6: 4,900, 부적이 막음
            EnhanceResult offer = _kit.Mutate(w);                // 4,900
            tally.Add(offer, false);
            tally.Add(_kit.Resolve(w, true), true);              // 결정은 시도로 세지 않는다
            tally.Add(_kit.Fail(_kit.Set(6, 8, 0)), false);      // 4,900, 하락 없음
            Assert.AreEqual(4, tally.Attempts);
            Assert.AreEqual(1, tally.Successes);
            Assert.AreEqual(3, tally.Failures);
            Assert.AreEqual(3600 + 4900 + 4900 + 4900, tally.GoldSpent);
        }

        // ------------------------------------------------------------ 3.1절 불변식: 무작위로 오래 돌리며 매번 본다

        [Test]
        public void S07_3_1__InvariantsHoldAfterEveryAttemptOfARandomWalk()
        {
            var kit = new EnhanceKit(new Pcg32EnhanceRandom(new Pcg32(7_2026_11_14)));
            var pick = new Pcg32(99);
            string[] blocks = { Ids.Block.Frost, Ids.Block.Homing, Ids.Block.SplitThree, Ids.Block.DoubleSize };
            var outcomes = new HashSet<EnhanceOutcome>();
            int carved = 0, removed = 0;

            for (int step = 0; step < 6000; step++)
            {
                ProfileData p = kit.Profile;
                p.Currencies.Gold = 1_000_000;
                foreach (string id in new[] { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh }) p.Item(id).Qty = 5;
                p.Item(Ids.Item.StoneMid).QtyBound = 2;
                p.Item(Ids.Item.Charm).Qty = 10;   // 결정 대기 중인 자루들이 맡아 둔 것보다 많게
                foreach (string id in blocks) p.Item(id).Qty = 5;

                WandData w = kit.Wand(1 + pick.Range(0, 5));
                if (pick.Range(0, 40) == 0) w.Loadout.Core = w.Loadout.Core == Ids.Block.Fireball ? Ids.Block.MagicBolt : Ids.Block.Fireball;

                if (w.PendingOffer != null)
                {
                    Result<EnhanceResult> resolved = kit.Service.ResolveMutation(w.Id, w.PendingOffer.AttemptId, pick.Range(0, 3) != 0);
                    Assert.AreEqual(ErrorCode.None, resolved.Error);
                    outcomes.Add(resolved.Value.Outcome);
                }
                else if (w.Stage >= kit.Cfg.MaxStage)
                {
                    // +13은 더 올릴 수 없다. 새기기를 해 보고, 다 자랐으면 새 지팡이 꼴로 되돌려 다시 걷는다.
                    Assert.AreEqual(ErrorCode.MaxStage, kit.Service.Enhance(w.Id, null, false).Error);
                    if (w.SlotsExtra < kit.Cfg.SlotExtraMax)
                    {
                        Assert.AreEqual(ErrorCode.None, kit.Service.CarveSlot(w.Id).Error);
                        carved++;
                    }
                    else
                    {
                        w.Stage = 0; w.FloorPeak = 0; w.RecordPeak = 0; w.Aura = 0; w.DropCount = 0; w.CarveBonus = 0;
                        kit.SetSlots(w, 0);
                        w.Mutations.Clear();
                    }
                }
                else if (w.Mutations.Count > 0 && pick.Range(0, 25) == 0)
                {
                    Assert.AreEqual(ErrorCode.None, kit.Service.RemoveMutation(w.Id, w.Mutations[pick.Range(0, w.Mutations.Count)].Id).Error);
                    removed++;
                }
                else if (pick.Range(0, 12) == 0 && EnhanceRules.CarveEligible(kit.Cfg, w.Stage, w.SlotsExtra))
                {
                    Assert.AreEqual(ErrorCode.None, kit.Service.CarveSlot(w.Id).Error);
                    carved++;
                }
                else
                {
                    var sac = new List<string>();
                    int n = pick.Range(0, 6);
                    for (int i = 0; i < n; i++) sac.Add(blocks[pick.Range(0, blocks.Length)]);
                    bool charm = pick.Range(0, 2) == 0 && EnhanceRules.DropPossible(kit.Cfg, w.Stage, w.FloorPeak);
                    int stageBefore = w.Stage, peakBefore = w.RecordPeak;
                    Result<EnhanceResult> r = kit.Service.Enhance(w.Id, sac, charm);
                    Assert.AreEqual(ErrorCode.None, r.Error, "step " + step);
                    outcomes.Add(r.Value.Outcome);
                    Assert.IsNull(r.Value.ServerNotice);
                    Assert.LessOrEqual(w.Stage - stageBefore, 2);
                    Assert.GreaterOrEqual(w.Stage - stageBefore, -1);
                    Assert.GreaterOrEqual(w.RecordPeak, peakBefore, "record_peak never goes down");
                    Assert.AreEqual(w.FloorPeak, w.RecordPeak, "solo: both peaks move together");
                }
                kit.AssertInvariants();
            }

            // 걷는 동안 모든 분기를 밟았다.
            foreach (EnhanceOutcome o in new[]
            {
                EnhanceOutcome.GreatSuccess, EnhanceOutcome.Success, EnhanceOutcome.FailDrop, EnhanceOutcome.FailCharmBlocked,
                EnhanceOutcome.FailNoDrop, EnhanceOutcome.MutationOffered, EnhanceOutcome.MutationAccepted
            })
                Assert.IsTrue(outcomes.Contains(o), "never saw " + o);
            Assert.Greater(carved, 0);
            Assert.Greater(removed, 0);
            Assert.LessOrEqual(kit.Profile.EnhanceLogTail.Count, kit.Service.Tuning.LogTailMax);
        }
    }
}
