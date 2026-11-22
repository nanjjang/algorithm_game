using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Screens.Camp;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.ScreensCamp
{
    /// <summary>
    /// 전리품 카드의 새 정보: 효과 한 줄(BlockDescriber — 블록 데이터 + 이름 표), 별 줄, 끼웠을 때 미리보기(SpellPreview 한 길), 건너뛰기·다시 뽑기 값.
    /// </summary>
    public class LootCardInfoTests
    {
        [Test]
        public void Describer__EveryBlockHasOneLine_AllSlotsFilled()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            var stars = BlockDescriber.Stars(db);
            Assert.AreEqual(3, stars.Max);
            foreach (var rarity in new[] { Ids.Rarity.Common, Ids.Rarity.Rare, Ids.Rarity.Hero })
                foreach (var id in db.BlocksOfRarity(rarity))
                {
                    var def = db.Block(id);
                    var line = BlockDescriber.Describe(def);
                    Assert.IsNotNull(line.Key, id + ": 설명 없음");
                    Assert.IsTrue(names.Has(line.Key), id + ": 이름 표에 " + line.Key + " 없음");
                    string text = line.Format(names);
                    StringAssert.DoesNotContain("{", text, id + ": 채우지 못한 자리 — " + text);
                    var scope = BlockDescriber.ScopeRule(def);
                    Assert.IsTrue(names.Has(scope.Key), id + ": " + scope.Key);
                    var star = BlockDescriber.StarLine(def, 1, stars);
                    Assert.IsNotNull(star.Key, id + ": 별 줄 없음");
                    StringAssert.DoesNotContain("{", star.Format(names), id);
                }
        }

        [Test]
        public void Describer__NumbersComeFromData()
        {
            var db = TestContent.Db;
            var names = TestContent.Names;
            // 화염구 params.explosion_radius 2, 관통 pierce_count 2·damage_bonus 0.1, 잠식 15% 3초
            StringAssert.Contains("2", BlockDescriber.Describe(db.Block(Ids.Block.Fireball)).Format(names));
            string pierce = BlockDescriber.Describe(db.Block(Ids.Block.Pierce)).Format(names);
            StringAssert.Contains("2", pierce);
            StringAssert.Contains("10%", pierce);
            string corr = BlockDescriber.Describe(db.Block(Ids.Block.Corrosion)).Format(names);
            StringAssert.Contains("15%", corr);
            StringAssert.Contains("3", corr);
            // 마력탄 ★2: 피해 +40%, 열 −20%
            string star = BlockDescriber.StarLine(db.Block(Ids.Block.MagicBolt), 2, BlockDescriber.Stars(db)).Format(names);
            StringAssert.Contains("40%", star);
            StringAssert.Contains("20%", star);
            // 2갈래 ★1: 열 배수 1.48
            StringAssert.Contains("1.48", BlockDescriber.StarLine(db.Block(Ids.Block.SplitTwo), 1, BlockDescriber.Stars(db)).Format(names));
        }

        static PlayerState Player(SimModuleSet rules, string core, params string[] links)
        {
            var db = TestContent.Db;
            var ch = db.Character(Ids.Char.C1);
            var p = new PlayerState { Slot = 0, Active = true, CharId = Ids.Char.C1, Def = ch, MaxHp = 800, Hp = 500 };
            p.Derived = rules.Traits.ComputeDerived(db, ch, 0, new List<string>());
            p.Loadout = new RunLoadout { Core = new SlotItem { BlockId = core }, Links = new SlotItem[4] };
            for (int i = 0; i < links.Length; i++) p.Loadout.Links[i] = new SlotItem { BlockId = links[i] };
            return p;
        }

        static RunState Offer(params LootCard[] cards)
        {
            var run = new RunState { Purse = 100 };
            run.Loot = new LootOffer { Wave = 3, PickCount = 2, RerollCost = 45, Rerolls = 1, SkipGold = 16, SkipHealCells = 1 };
            foreach (var c in cards) run.Loot.Cards.Add(c);
            return run;
        }

        [Test]
        public void Preview__InsertIntoFirstEmptyLink_SplitDoublesShots()
        {
            var rules = SimModuleSet.CreateDefault();
            var p = Player(rules, Ids.Block.MagicBolt);
            var run = Offer(new LootCard { BlockId = Ids.Block.SplitTwo, Rarity = Ids.Rarity.Common },
                            new LootCard { BlockId = Ids.Block.Pierce, Rarity = Ids.Rarity.Common });
            var m = LootPickModel.From(TestContent.Db, run, rules, p);
            var split = m.Cards[0];
            Assert.AreEqual(LootPreviewKind.Insert, split.PreviewKind);
            Assert.AreEqual(1, split.PreviewSlot, "첫 빈 연결 칸");
            Assert.AreEqual(1, split.ShotsBefore);
            Assert.AreEqual(2, split.ShotsAfter, "2갈래: 탄 1 → 2");
            Assert.AreEqual(4f, split.HeatBefore, 1e-4f);
            Assert.AreEqual(6.4f, split.HeatAfter, 1e-4f, "열 4 × 1.6");
            Assert.Greater(split.DpsPct, 0f);
            var pierce = m.Cards[1];
            Assert.AreEqual(7f, pierce.HeatAfter, 1e-4f, "4 + 3");
            Assert.AreEqual(1, pierce.ShotsAfter);
            // 다시 뽑기·건너뛰기 값은 제시의 값 그대로, 다음 값은 +15
            Assert.AreEqual(45, m.RerollCost);
            Assert.AreEqual(60, m.NextRerollCost);
            Assert.AreEqual(16, m.SkipGold);
            Assert.IsTrue(m.SkipHealEnabled, "체력 500/800");
            Assert.IsTrue(m.SkipGoldEnabled);
            var cmd = LootPickModel.SkipCommand(true, 0);
            Assert.AreEqual(RunCommandKind.LootSkip, cmd.Kind);
            Assert.AreEqual(1, cmd.A);
        }

        [Test]
        public void Preview__StarOnEquippedBlock_AndReplaceWhenFull()
        {
            var rules = SimModuleSet.CreateDefault();
            var p = Player(rules, Ids.Block.MagicBolt, Ids.Block.WallBounce, Ids.Block.Frost, Ids.Block.WallBounce, Ids.Block.Frost);
            var run = Offer(new LootCard { BlockId = Ids.Block.MagicBolt, Rarity = Ids.Rarity.Common, StarTarget = 1 },
                            new LootCard { BlockId = Ids.Block.SplitThree, Rarity = Ids.Rarity.Rare });
            var m = LootPickModel.From(TestContent.Db, run, rules, p);
            var star = m.Cards[0];
            Assert.AreEqual(LootPreviewKind.Star, star.PreviewKind);
            Assert.AreEqual(0, star.PreviewSlot);
            Assert.Less(star.HeatAfter, star.HeatBefore, "별이 열을 깎는다");
            Assert.Greater(star.DpsPct, 15f, "피해 +20%");
            Assert.IsNotNull(star.StarLine.Key);

            var split = m.Cards[1];
            Assert.AreEqual(LootPreviewKind.Replace, split.PreviewKind, "빈 칸이 없으면 바꿀 칸");
            Assert.That(split.PreviewSlot, Is.InRange(1, 4));
            Assert.IsNotNull(split.ReplacedId);
            Assert.AreEqual(3, split.ShotsAfter);
        }

        [Test]
        public void Preview__NoRules_NoPreview()
        {
            var run = Offer(new LootCard { BlockId = Ids.Block.Fireball, Rarity = Ids.Rarity.Rare });
            var m = LootPickModel.From(TestContent.Db, run);
            Assert.AreEqual(LootPreviewKind.None, m.Cards[0].PreviewKind);
            Assert.IsNotNull(m.Cards[0].Desc.Key, "설명은 미리보기 없이도 있다");
        }
    }
}
