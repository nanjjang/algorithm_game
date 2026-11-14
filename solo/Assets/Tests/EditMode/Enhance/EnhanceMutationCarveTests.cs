using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Save;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>07 요약서 12.5절(변이), 12.7절(슬롯 새기기).</summary>
    public class EnhanceMutationCarveTests
    {
        EnhanceKit _kit;
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _kit = new EnhanceKit();
            _kit.Rich();
            _tempDir = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_tempDir != null && Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        static List<string> Ids2(List<MutationEntry> list)
        {
            var ids = new List<string>();
            foreach (MutationEntry m in list) ids.Add(m.Id);
            return ids;
        }

        // 후보 목록은 번호 순(1~6)에서 가진 것과 조건이 안 맞는 것을 뺀 것이다.
        int IndexInPool(WandData w, string mutationId)
        {
            List<string> pool = EnhanceRules.MutationPool(w.Mutations, EnhanceRules.CoreIsExplosive(_kit.Ctx.Content, w.Loadout));
            int i = pool.IndexOf(mutationId);
            Assert.GreaterOrEqual(i, 0, mutationId + " is not a candidate");
            return i;
        }

        // ------------------------------------------------------------ 12.5 변이

        [Test]
        public void S07_TV_M_01__ThirdMutationPushesOutTheOldest()
        {
            // char.1, 변이 [A, B] → C 받음 → [B, C], 밀려남 A
            WandData w = _kit.Set(9, 9, 0, character: 1);
            _kit.SetMutations(w, Ids.Mutation.TwistedBarrel, Ids.Mutation.HypersensitiveCircuit);
            EnhanceResult offer = _kit.Mutate(w, false, IndexInPool(w, Ids.Mutation.Gluttony));
            Assert.AreEqual(Ids.Mutation.Gluttony, offer.MutationCandidate);
            Assert.AreEqual(Ids.Mutation.TwistedBarrel, w.PendingOffer.WouldPushOut);
            EnhanceResult r = _kit.Resolve(w, true);
            CollectionAssert.AreEqual(new[] { Ids.Mutation.HypersensitiveCircuit, Ids.Mutation.Gluttony }, Ids2(w.Mutations));
            Assert.AreEqual(Ids.Mutation.TwistedBarrel, r.MutationPushedOut);
        }

        [Test]
        public void S07_TV_M_02__Character2HoldsThree()
        {
            WandData w = _kit.Set(9, 9, 0, character: 2);
            _kit.SetMutations(w, Ids.Mutation.TwistedBarrel, Ids.Mutation.HypersensitiveCircuit);
            EnhanceResult offer = _kit.Mutate(w, false, IndexInPool(w, Ids.Mutation.Gluttony));
            Assert.IsNull(offer.MutationPushedOut);
            EnhanceResult r = _kit.Resolve(w, true);
            CollectionAssert.AreEqual(
                new[] { Ids.Mutation.TwistedBarrel, Ids.Mutation.HypersensitiveCircuit, Ids.Mutation.Gluttony }, Ids2(w.Mutations));
            Assert.IsNull(r.MutationPushedOut);
        }

        [Test]
        public void S07_TV_M_03__Character2FourthPushesOutTheOldest()
        {
            WandData w = _kit.Set(9, 9, 0, character: 2);
            _kit.SetMutations(w, Ids.Mutation.TwistedBarrel, Ids.Mutation.HypersensitiveCircuit, Ids.Mutation.Gluttony);
            _kit.Mutate(w, false, IndexInPool(w, Ids.Mutation.MirrorImage));
            EnhanceResult r = _kit.Resolve(w, true);
            CollectionAssert.AreEqual(
                new[] { Ids.Mutation.HypersensitiveCircuit, Ids.Mutation.Gluttony, Ids.Mutation.MirrorImage }, Ids2(w.Mutations));
            Assert.AreEqual(Ids.Mutation.TwistedBarrel, r.MutationPushedOut);
        }

        [Test]
        public void S07_TV_M_04__NoUnstableCoreWhenCoreIsMagicBolt()
        {
            WandData w = _kit.Wand();
            Assert.AreEqual(Ids.Block.MagicBolt, w.Loadout.Core);
            Assert.IsFalse(EnhanceRules.CoreIsExplosive(_kit.Ctx.Content, w.Loadout));
            var rng = new Pcg32EnhanceRandom(new Pcg32(20261114));
            var seen = new HashSet<string>();
            for (int i = 0; i < 10000; i++)
            {
                string c = EnhanceRules.DrawCandidate(EnhanceRules.MutationPool(w.Mutations, false), rng);
                Assert.AreNotEqual(Ids.Mutation.UnstableCore, c);
                seen.Add(c);
            }
            Assert.AreEqual(5, seen.Count, "the other five all appear");
        }

        [Test]
        public void S07_TV_M_05__UnstableCoreAppearsWhenCoreIsFireball()
        {
            WandData w = _kit.Wand();
            w.Loadout.Core = Ids.Block.Fireball;
            Assert.IsTrue(EnhanceRules.CoreIsExplosive(_kit.Ctx.Content, w.Loadout));
            List<string> pool = EnhanceRules.MutationPool(w.Mutations, true);
            CollectionAssert.Contains(pool, Ids.Mutation.UnstableCore);
            Assert.AreEqual(6, pool.Count);

            // 서비스의 추첨에서도 나온다.
            w = _kit.Set(9, 9, 0);
            EnhanceResult offer = _kit.Mutate(w, false, IndexInPool(w, Ids.Mutation.UnstableCore));
            Assert.AreEqual(Ids.Mutation.UnstableCore, offer.MutationCandidate);
        }

        [Test]
        public void S07_TV_M_05__ExplodesFlagOnNonProjectileCoreDoesNotCount()
        {
            // 00 문서 D-30: flags에 explodes가 있는 "투사체"가 코어일 때만.
            var loadout = new LoadoutData { Core = Ids.Block.DoubleSize };
            Assert.IsFalse(EnhanceRules.CoreIsExplosive(_kit.Ctx.Content, loadout));
            Assert.IsFalse(EnhanceRules.CoreIsExplosive(_kit.Ctx.Content, new LoadoutData()));
        }

        [Test]
        public void S07_TV_M_06__PendingOfferBlocksEnhanceCarveAndRemove()
        {
            WandData w = _kit.Set(9, 9, 0);
            _kit.SetMutations(w, Ids.Mutation.TwistedBarrel);
            _kit.Mutate(w);
            Assert.IsNotNull(w.PendingOffer);
            Assert.AreEqual(ErrorCode.MutationPending, _kit.Service.Enhance(w.Id, null, false).Error);
            Assert.AreEqual(ErrorCode.MutationPending, _kit.Preview(w).Blocked);
            Assert.AreEqual(ErrorCode.MutationPending, _kit.Service.CarveSlot(w.Id).Error);
            Assert.AreEqual(ErrorCode.MutationPending, _kit.Service.RemoveMutation(w.Id, Ids.Mutation.TwistedBarrel).Error);
            // 다른 지팡이는 막히지 않는다.
            Assert.AreEqual(ErrorCode.None, _kit.Preview(_kit.Wand(2)).Blocked);
        }

        [Test]
        public void S07_TV_M_07__PendingOfferSurvivesRestart()
        {
            // 변이 제시 뒤 게임을 껐다 켜도 저장 파일에 pending_offer가 남아 강화 화면이 변이 카드부터 연다.
            _tempDir = Path.Combine(Path.GetTempPath(), "solo-enhance-" + Guid.NewGuid().ToString("N"));
            var store = new ProfileStore(_tempDir, () => NewProfile.Create(TestContent.Db, new EnhanceTestClock()), TestContent.Db.DataVersion);
            store.LoadOrCreate(out _, out _);
            var ctx = new GameContext { Content = TestContent.Db, Clock = new EnhanceTestClock(), Profiles = store };
            var rng = new ScriptedRandom();
            var service = new EnhanceService(ctx, rng);

            ProfileData p = store.Current;
            p.Currencies.Gold = 100_000;
            p.Item(Ids.Item.StoneHigh).Qty = 3;
            p.Item(Ids.Item.Charm).Qty = 1;
            WandData w = p.Wand(p.Character(1).EquippedWandId);
            w.Stage = 9; w.FloorPeak = 9; w.RecordPeak = 9; w.Aura = 1;
            ProbBreakdown b = service.Preview(w.Id, null, true);
            rng.Push(b.SuccessBp, 0);
            EnhanceResult offer = service.Enhance(w.Id, null, true).Value;
            Assert.AreEqual(EnhanceOutcome.MutationOffered, offer.Outcome);

            var reopened = new ProfileStore(_tempDir, () => NewProfile.Create(TestContent.Db, new EnhanceTestClock()), TestContent.Db.DataVersion);
            reopened.LoadOrCreate(out bool created, out bool restored);
            Assert.IsFalse(created);
            Assert.IsFalse(restored);
            WandData again = reopened.Current.Wand(w.Id);
            Assert.IsNotNull(again.PendingOffer);
            Assert.AreEqual(offer.AttemptId, again.PendingOffer.AttemptId);
            Assert.AreEqual(offer.MutationCandidate, again.PendingOffer.Candidate);
            Assert.AreEqual("FailCharmBlocked", again.PendingOffer.FallbackOutcome);
            Assert.AreEqual(9, again.PendingOffer.FallbackStageAfter);
            Assert.IsTrue(again.PendingOffer.FallbackCharmConsumed);
            Assert.AreEqual(1, again.PendingOffer.FallbackAuraAfter);
            Assert.AreEqual(100_000 - 10_000, reopened.Current.Currencies.Gold, "the cost was saved as spent");

            // 다시 켠 뒤에도 강화는 막혀 있고, 결정하면 풀린다. 뽑힌 값은 기록 꼬리에서 되살린다.
            var ctx2 = new GameContext { Content = TestContent.Db, Clock = new EnhanceTestClock(), Profiles = reopened };
            var service2 = new EnhanceService(ctx2, new ScriptedRandom());
            Assert.AreEqual(ErrorCode.MutationPending, service2.Enhance(again.Id, null, false).Error);
            Result<EnhanceResult> declined = service2.ResolveMutation(again.Id, again.PendingOffer.AttemptId, false);
            Assert.AreEqual(ErrorCode.None, declined.Error);
            Assert.AreEqual(EnhanceOutcome.FailCharmBlocked, declined.Value.Outcome);
            Assert.AreEqual(offer.RollBp, declined.Value.RollBp);
            Assert.AreEqual(0, reopened.Current.Item(Ids.Item.Charm).Qty);
            Assert.IsNull(again.PendingOffer);
        }

        [Test]
        public void S07_TV_M_08__RemoveOneMutationFor3000Gold()
        {
            WandData w = _kit.Set(8, 9, 2);
            _kit.SetSlots(w, 1);
            w.CarveBonus = 2;
            _kit.SetMutations(w, Ids.Mutation.TwistedBarrel, Ids.Mutation.Gluttony);
            _kit.Gold = 3000;
            int saves = _kit.Store.SaveCount;
            Result<WandData> r = _kit.Service.RemoveMutation(w.Id, Ids.Mutation.TwistedBarrel);
            Assert.AreEqual(ErrorCode.None, r.Error);
            CollectionAssert.AreEqual(new[] { Ids.Mutation.Gluttony }, Ids2(w.Mutations));
            Assert.AreEqual(0, _kit.Gold);
            Assert.AreEqual(8, w.Stage);
            Assert.AreEqual(2, w.Aura);
            Assert.AreEqual(1, w.SlotsExtra);
            Assert.AreEqual(2, w.CarveBonus);
            Assert.AreEqual(saves + 1, _kit.Store.SaveCount);
            _kit.AssertInvariants();
        }

        [Test]
        public void S07_TV_M_09__RemoveMutationNeeds3000Gold()
        {
            WandData w = _kit.Set(8, 9, 2);
            _kit.SetMutations(w, Ids.Mutation.TwistedBarrel, Ids.Mutation.Gluttony);
            _kit.Gold = 2999;
            Assert.AreEqual(ErrorCode.NotEnoughGold, _kit.Service.RemoveMutation(w.Id, Ids.Mutation.TwistedBarrel).Error);
            Assert.AreEqual(2, w.Mutations.Count);
            Assert.AreEqual(2999, _kit.Gold);
        }

        [Test]
        public void S07_4_7__NoCandidateLeftMeansPlainFail()
        {
            // 후보가 비면 일반 실패: char.2가 코어 마력탄으로 다섯 가운데 셋을 갖고 있어도 둘이 남으므로,
            // 후보를 전부 가진 꼴은 상한 때문에 생기지 않는다 — 규칙 함수로 직접 본다.
            var owned = new List<MutationEntry>();
            foreach (string id in Ids.Mutation.All) owned.Add(new MutationEntry { Id = id });
            Assert.AreEqual(0, EnhanceRules.MutationPool(owned, true).Count);
            Assert.IsNull(EnhanceRules.DrawCandidate(EnhanceRules.MutationPool(owned, true), new ScriptedRandom()));
        }

        [Test]
        public void S07_4_7__CandidateIsNeverOneAlreadyOwned()
        {
            WandData w = _kit.Set(9, 9, 0, character: 2);
            _kit.SetMutations(w, Ids.Mutation.Gluttony, Ids.Mutation.GlassWand);
            List<string> pool = EnhanceRules.MutationPool(w.Mutations, false);
            CollectionAssert.AreEqual(
                new[] { Ids.Mutation.TwistedBarrel, Ids.Mutation.HypersensitiveCircuit, Ids.Mutation.MirrorImage }, pool);
        }

        [Test]
        public void S07_4_7__ResolveErrors()
        {
            WandData w = _kit.Set(9, 9, 0);
            Assert.AreEqual(ErrorCode.NoPendingMutation, _kit.Service.ResolveMutation(w.Id, 1, true).Error);
            _kit.Mutate(w);
            Assert.AreEqual(ErrorCode.InvalidArgument, _kit.Service.ResolveMutation(w.Id, w.PendingOffer.AttemptId + 1, true).Error);
            Assert.AreEqual(ErrorCode.NotFound, _kit.Service.ResolveMutation(999, 1, true).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, _kit.Service.RemoveMutation(_kit.Wand(2).Id, Ids.Mutation.Gluttony).Error);
        }

        // ------------------------------------------------------------ 12.7 슬롯 새기기

        WandData CarveWand(int stage, int slots, int bonus = 0)
        {
            WandData w = _kit.Set(stage, stage, 1);
            _kit.SetSlots(w, slots);
            w.CarveBonus = bonus;
            return w;
        }

        [Test]
        public void S07_TV_S_01__Stage6IsNotEligible()
        {
            WandData w = CarveWand(6, 0);
            Assert.AreEqual(ErrorCode.NotEligible, _kit.Service.CarveSlot(w.Id).Error);
            Assert.AreEqual(ErrorCode.NotEligible, _kit.Service.PreviewCarve(w.Id).Blocked);
        }

        [Test]
        public void S07_TV_S_02__ThreeSlotsIsNotEligible()
        {
            WandData w = CarveWand(7, 3);
            Assert.AreEqual(ErrorCode.NotEligible, _kit.Service.CarveSlot(w.Id).Error);
        }

        [Test]
        public void S07_TV_S_03__FirstCarveCostsTwoHighStonesAnd20000GoldAt40Percent()
        {
            WandData w = CarveWand(7, 0);
            _kit.Gold = 20_000;
            _kit.Profile.Item(Ids.Item.StoneHigh).Qty = 2;
            ProbBreakdown b = _kit.Service.PreviewCarve(w.Id);
            Assert.AreEqual(ErrorCode.None, b.Blocked);
            Assert.AreEqual(20_000, b.CostGold);
            Assert.AreEqual(Ids.Item.StoneHigh, b.StoneId);
            Assert.AreEqual(2, b.StoneCount);
            Assert.AreEqual(40, b.SuccessPct);

            _kit.Rng.Push(3999);
            CarveResult r = _kit.Service.CarveSlot(w.Id).Value;
            Assert.IsTrue(r.Success);
            Assert.AreEqual(40, r.ChancePct);
            Assert.AreEqual(0, _kit.Gold);
            Assert.AreEqual(0, _kit.Qty(Ids.Item.StoneHigh));
            Assert.AreEqual(0, r.GoldAfter);

            Assert.AreEqual(ErrorCode.NotEnoughGold, _kit.Service.CarveSlot(w.Id).Error);
            _kit.Gold = 40_000;
            Assert.AreEqual(ErrorCode.NotEnoughStone, _kit.Service.CarveSlot(w.Id).Error);
        }

        [Test]
        public void S07_TV_S_04__SecondAndThirdSlotCost()
        {
            Assert.AreEqual(40_000, _kit.Service.PreviewCarve(CarveWand(7, 1).Id).CostGold);
            ProbBreakdown third = _kit.Service.PreviewCarve(CarveWand(7, 2).Id);
            Assert.AreEqual(60_000, third.CostGold);
            Assert.AreEqual(2, third.StoneCount);
            Assert.AreEqual(Ids.Item.StoneHigh, third.StoneId);
        }

        [Test]
        public void S07_TV_S_05__CarveChanceByBonus()
        {
            int[] expected = { 40, 60, 80, 100 };
            for (int bonus = 0; bonus < expected.Length; bonus++)
            {
                Assert.AreEqual(expected[bonus], EnhanceRules.CarvePct(_kit.Cfg, bonus), "carve_bonus " + bonus);
                Assert.AreEqual(expected[bonus], _kit.Service.PreviewCarve(CarveWand(7, 0, bonus).Id).SuccessPct);
            }
        }

        [Test]
        public void S07_TV_S_06__FailedCarveKeepsStageAndAuraAndAddsBonus()
        {
            WandData w = CarveWand(8, 1);
            long gold = _kit.Gold;
            int stones = _kit.Qty(Ids.Item.StoneHigh);
            _kit.Rng.Push(4000);                    // 40% → 4000 미만이어야 성공
            CarveResult r = _kit.Service.CarveSlot(w.Id).Value;
            Assert.IsFalse(r.Success);
            Assert.AreEqual(8, w.Stage);
            Assert.AreEqual(1, w.Aura);
            Assert.AreEqual(1, w.SlotsExtra);
            Assert.AreEqual(1, w.CarveBonus);
            Assert.AreEqual(1, r.CarveBonusAfter);
            Assert.AreEqual(gold - 40_000, _kit.Gold, "materials are gone");
            Assert.AreEqual(stones - 2, _kit.Qty(Ids.Item.StoneHigh));
            _kit.AssertInvariants();
        }

        [Test]
        public void S07_TV_S_07__SuccessfulCarveAddsSlotAndResetsBonus()
        {
            WandData w = CarveWand(8, 1, bonus: 2);
            int saves = _kit.Store.SaveCount;
            _kit.Rng.Push(7999);                    // 80%
            CarveResult r = _kit.Service.CarveSlot(w.Id).Value;
            Assert.IsTrue(r.Success);
            Assert.AreEqual(2, w.SlotsExtra);
            Assert.AreEqual(2, w.Loadout.Extra.Count);
            Assert.AreEqual(0, w.CarveBonus);
            Assert.AreEqual(2, r.SlotsExtraAfter);
            Assert.AreEqual(saves + 1, _kit.Store.SaveCount);
            _kit.AssertInvariants();
        }

        [Test]
        public void S07_TV_S_07__FourthTryAlwaysSucceeds()
        {
            WandData w = CarveWand(7, 0, bonus: 3);
            _kit.Rng.Push(9999);
            Assert.IsTrue(_kit.Service.CarveSlot(w.Id).Value.Success);
        }

        [Test]
        public void S07_TV_S_08__AttemptsForOneSlot()
        {
            // 1회 40%, 2회 36%, 3회 19.2%, 4회 4.8%, 평균 1.888
            double[] expected = { 0.40, 0.36, 0.192, 0.048 };
            double reach = 1;
            for (int k = 0; k < expected.Length; k++)
            {
                double p = EnhanceRules.CarvePct(_kit.Cfg, k) / 100.0;
                Assert.AreEqual(expected[k], reach * p, 1e-12, "attempt " + (k + 1));
                reach *= 1 - p;
            }
            Assert.AreEqual(0, reach, 1e-12);
            Assert.AreEqual(1.888, EnhanceRules.CarveExpectedAttempts(_kit.Cfg), 1e-9);
        }

        [Test]
        public void S07_TV_S_09__ExpectedCostZeroToThreeSlots()
        {
            double tries = EnhanceRules.CarveExpectedAttempts(_kit.Cfg);
            double gold = 0, stones = 0;
            for (int slots = 0; slots < 3; slots++)
            {
                gold += tries * EnhanceRules.CarveGold(_kit.Cfg, slots);
                stones += tries * _kit.Cfg.Carve.HighStones;
            }
            Assert.AreEqual(226_560, gold, 0.5);
            Assert.AreEqual(11.33, stones, 0.005);
        }

        [Test]
        public void S07_TV_S_10__ExpectedCostFirstSlot()
        {
            double tries = EnhanceRules.CarveExpectedAttempts(_kit.Cfg);
            Assert.AreEqual(37_760, tries * EnhanceRules.CarveGold(_kit.Cfg, 0), 0.5);
            Assert.AreEqual(3.78, tries * _kit.Cfg.Carve.HighStones, 0.005);
        }

        [Test]
        public void S07_TV_S_11__ExpectedCostTwoSlots()
        {
            double tries = EnhanceRules.CarveExpectedAttempts(_kit.Cfg);
            double gold = tries * (EnhanceRules.CarveGold(_kit.Cfg, 0) + EnhanceRules.CarveGold(_kit.Cfg, 1));
            Assert.AreEqual(113_280, gold, 0.5);
            Assert.AreEqual(7.55, 2 * tries * _kit.Cfg.Carve.HighStones, 0.005);
        }

        [Test]
        public void S07_4_8__SlotsSurviveADrop()
        {
            // 확장 칸은 평생이다: 하락해도 남는다.
            WandData w = CarveWand(8, 2);
            _kit.Fail(w);
            Assert.AreEqual(7, w.Stage);
            Assert.AreEqual(2, w.SlotsExtra);
            Assert.AreEqual(2, w.Loadout.Extra.Count);
        }
    }
}
