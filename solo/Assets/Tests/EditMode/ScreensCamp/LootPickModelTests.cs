using NUnit.Framework;
using Wizard.Solo.Screens.Camp;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.ScreensCamp
{
    /// <summary>전리품 고르기 화면 규칙(08 요약서 8.1절, 06 요약서 4.4절). 기대값은 요약서 벡터.</summary>
    public class LootPickModelTests
    {
        static RunState Run(int cards, int picked, int purse)
        {
            var run = new RunState { Purse = purse };
            run.Loot = new LootOffer { Wave = 3, PickCount = 2 };
            for (int i = 0; i < cards; i++)
                run.Loot.Cards.Add(new LootCard { BlockId = Ids.Block.SplitTwo, Rarity = Ids.Rarity.Common, Picked = i < picked });
            return run;
        }

        [Test]
        public void S08_TV_060__ConfirmOffAtOnePickOnAtTwo_ThirdIgnored()
        {
            var m1 = LootPickModel.From(TestContent.Db, Run(4, 1, 0));
            Assert.IsFalse(m1.ConfirmEnabled, "1장이면 꺼짐");
            var m2 = LootPickModel.From(TestContent.Db, Run(4, 2, 0));
            Assert.IsTrue(m2.ConfirmEnabled, "2장이면 켜짐");
            RunCommand cmd;
            Assert.IsFalse(m2.ToggleCommand(3, out cmd), "3번째 고르기는 무시");
            Assert.IsTrue(m2.ToggleCommand(0, out cmd));
            Assert.AreEqual(RunCommandKind.LootUnpick, cmd.Kind, "고른 카드를 누르면 빼기");
            Assert.AreEqual(0, cmd.A);
            Assert.IsTrue(m1.ToggleCommand(2, out cmd));
            Assert.AreEqual(RunCommandKind.LootPick, cmd.Kind);
            Assert.AreEqual(2, cmd.A, "A = 카드 번호");
        }

        [Test]
        public void S08_TV_061__RerollDisabledAtPurse29_NotEnoughGold()
        {
            var m = LootPickModel.From(TestContent.Db, Run(4, 0, 29));
            Assert.IsFalse(m.RerollEnabled);
            Assert.AreEqual("error.not_enough_gold", m.RerollDisabledReason);
            Assert.AreEqual("골드가 모자란다.", TestContent.Names.Get(m.RerollDisabledReason));
        }

        [Test]
        public void S06_TV_192__RerollUnavailableBelow30()
        {
            Assert.AreEqual(30, LootPickModel.From(TestContent.Db, Run(4, 0, 0)).RerollCost);
            Assert.IsFalse(LootPickModel.From(TestContent.Db, Run(4, 0, 29)).RerollEnabled);
            Assert.IsTrue(LootPickModel.From(TestContent.Db, Run(4, 0, 30)).RerollEnabled);
        }

        [Test]
        public void S08_TV_062__RerollEnabledAtPurse100WithTwoPicked()
        {
            // 지갑 −30·카드 새로·고른 것 비움·확정 칸 유지는 시뮬레이션(Run)이 한다. 화면은 단추를 켜고 LootReroll만 보낸다.
            var m = LootPickModel.From(TestContent.Db, Run(4, 2, 100));
            Assert.IsTrue(m.RerollEnabled);
            Assert.AreEqual(100 - 30, m.Purse - m.RerollCost, "TV-062: 100 → 70");
        }

        [Test]
        public void S08_TV_063__Storage11PlusTwoNeedsOneDiscard_RareGives15()
        {
            Assert.AreEqual(1, LootPickModel.OverflowAfterConfirm(11, 2, false, TestContent.Db.Drops.StorageCap));
            Assert.AreEqual(0, LootPickModel.OverflowAfterConfirm(10, 2, false, TestContent.Db.Drops.StorageCap));
            Assert.AreEqual(15, LootPickModel.DiscardGold(TestContent.Db, new WildBlock { Rarity = Ids.Rarity.Rare }));
            Assert.AreEqual(5, LootPickModel.DiscardGold(TestContent.Db, new WildBlock { Rarity = Ids.Rarity.Common }));
            Assert.AreEqual(50, LootPickModel.DiscardGold(TestContent.Db, new WildBlock { Rarity = Ids.Rarity.Hero }));
            Assert.AreEqual(50, LootPickModel.DiscardGold(TestContent.Db, new WildBlock { Rarity = Ids.Rarity.Common, Borrowed = true }));
            var cmd = LootPickModel.DiscardCommand(3);
            Assert.AreEqual(RunCommandKind.LootDiscard, cmd.Kind);
            Assert.AreEqual(103, cmd.A, "버릴 자리 = 보관함 SlotRef 100 + i");

            var run = Run(4, 2, 0);
            run.Loot.OverflowToDiscard = 1;
            var m = LootPickModel.From(TestContent.Db, run);
            Assert.AreEqual(LootPickPhase.OverflowDiscard, m.Phase);
            Assert.IsFalse(m.ConfirmEnabled);
            Assert.IsFalse(m.RerollEnabled);
        }

        [Test]
        public void CardRowWidthIncludesAllCardsAndGaps()
        {
            Assert.AreEqual(1448f, LootPickModel.RowWidth(4));
            Assert.AreEqual(1816f, LootPickModel.RowWidth(5));
        }

        [Test]
        public void S08_TV_059__BorrowedCardShownWithTag()
        {
            var run = Run(4, 0, 0);
            run.Loot.BorrowedBlockId = Ids.Block.Homing;
            var m = LootPickModel.From(TestContent.Db, run);
            Assert.IsNotNull(m.BorrowedCard);
            Assert.IsTrue(m.BorrowedCard.Borrowed);
            Assert.AreEqual(Ids.Rarity.Hero, m.BorrowedCard.Rarity);
            Assert.AreEqual("빌림", TestContent.Names.Get("str.loot_pick.borrowed"));
        }

        [Test]
        public void S08_8_1__CardLinesOnlyWhatApplies()
        {
            var bolt = LootPickModel.Card(TestContent.Db, Ids.Block.MagicBolt, Ids.Rarity.Common, false);
            Assert.AreEqual("str.block.kind.projectile", bolt.KindKey);
            Assert.AreEqual("str.block.grade.common", bolt.GradeKey);
            Assert.AreEqual(3, bolt.StatLines.Count, "투사체: 열·피해·간격");
            var trig = LootPickModel.Card(TestContent.Db, Ids.Block.OnHitTrigger, Ids.Rarity.Rare, false);
            foreach (var l in trig.StatLines) Assert.AreNotEqual("str.block.damage", l.Key, "제어 블록에 피해 줄 없음");
        }
    }
}
