using System;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Save;

namespace Wizard.Solo.Tests.Save
{
    /// <summary>새 프로필 시작 상태(07 요약서 9.2절 = S8 8.4, 01 요약서 3.2절).</summary>
    public class NewProfileTests
    {
        static readonly DateTime Wed = new DateTime(2026, 11, 18, 15, 30, 0, DateTimeKind.Local);

        static ProfileData Make() => NewProfile.Create(new FakeContent(), new FixedClock(Wed));

        // S8 8.4: 캐릭터 5명 전원
        [Test]
        public void S08_8_4__FiveCharacters()
        {
            var p = Make();
            Assert.AreEqual(5, p.Characters.Count);
            for (int n = 1; n <= 5; n++) Assert.IsNotNull(p.Character(n), "char " + n);
        }

        // S8 8.4: 캐릭터마다 +0 한 자루(코어 마력탄), 각자 든 지팡이. 역대 최고 +0, 하락 0, 변이 0, 확장 0, 기운 0
        [Test]
        public void S08_8_4__OneStarterWandPerCharacterEquipped()
        {
            var p = Make();
            Assert.AreEqual(5, p.Wands.Count);
            for (int n = 1; n <= 5; n++)
            {
                var c = p.Character(n);
                var w = p.Wand(c.EquippedWandId);
                Assert.IsNotNull(w);
                Assert.AreEqual(n, w.Character);
                Assert.AreEqual(0, w.Stage);
                Assert.AreEqual(0, w.FloorPeak);
                Assert.AreEqual(0, w.RecordPeak);
                Assert.AreEqual(0, w.DropCount);
                Assert.AreEqual(0, w.Aura);
                Assert.AreEqual(0, w.SlotsExtra);
                Assert.AreEqual(0, w.CarveBonus);
                Assert.AreEqual(0, w.Mutations.Count);
                Assert.IsNull(w.PendingOffer);
                Assert.AreEqual("Starter", w.Origin);
                Assert.AreEqual(Ids.Block.MagicBolt, w.Loadout.Core);
                Assert.AreEqual(0, w.Loadout.Extra.Count);
                Assert.IsFalse(w.ShowScars);
            }
        }

        // 01 3.1절: 연결 칸 기본 4칸(제자 3 지팡이만 5칸), 빈 채
        [TestCase(1, 4)]
        [TestCase(2, 4)]
        [TestCase(3, 5)]
        [TestCase(4, 4)]
        [TestCase(5, 4)]
        public void S08_8_4__LinksAreEmptyBaseSlots(int character, int slots)
        {
            var p = Make();
            var w = p.Wand(p.Character(character).EquippedWandId);
            Assert.AreEqual(slots, w.Loadout.Links.Count);
            foreach (var l in w.Loadout.Links) Assert.IsNull(l);
        }

        // S8 8.4: 서고 마력탄 5개(전부 끼운 것), 여분 없음. 골드·강화석·부적 0
        [Test]
        public void S08_8_4__LibraryAndCurrencies()
        {
            var p = Make();
            Assert.AreEqual(5, p.Items[Ids.Block.MagicBolt].Qty);
            Assert.AreEqual(5, p.Items[Ids.Block.MagicBolt].QtySocketed);
            Assert.AreEqual(0, p.Currencies.Gold);
            foreach (var id in new[] { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh, Ids.Item.Charm })
            {
                Assert.IsTrue(p.Items.ContainsKey(id), id);
                Assert.AreEqual(0, p.Items[id].Qty, id);
                Assert.AreEqual(0, p.Items[id].QtyBound, id);
            }
            Assert.AreEqual(5, p.Items.Count);
            Assert.AreEqual(0, p.LibraryExpansions);
            Assert.AreEqual(0, p.ShopWeek.CharmBought);
        }

        // S8 8.4: 비기 시작 3종(마력 방출, 반동 사격, 고유 비기)
        [TestCase(1, "art.kiln")]
        [TestCase(2, "art.bolt_dash")]
        [TestCase(3, "art.copy_circle")]
        [TestCase(4, "art.ice_screen")]
        [TestCase(5, "art.shadow_hand")]
        public void S08_8_4__StartingArts(int character, string unique)
        {
            var c = Make().Character(character);
            CollectionAssert.AreEqual(new[] { Ids.Art.Burst, Ids.Art.Recoil, unique }, c.UnlockedArts);
        }

        // S8 8.4: 기록 — 최고 웨이브 0, 허상을 만난 적 없음(phantom_met = false)
        [Test]
        public void S08_8_4__RecordsAndFlags()
        {
            var p = Make();
            foreach (var c in p.Characters)
            {
                Assert.AreEqual(0, c.BestWave);
                Assert.AreEqual(0, c.BestScore);
                Assert.AreEqual(0, c.TotalWaves);
            }
            Assert.IsFalse(p.Flags.PhantomMet);
            Assert.IsNull(p.Flags.RevengeDoneAt);
            Assert.IsFalse(p.Flags.CrowBundleGiven);
            Assert.IsFalse(p.Flags.EnhanceTutorialShown);
            Assert.AreEqual(RevengeState.BeforeMeeting, p.RevengeState);
            Assert.AreEqual(0, p.RunHistory.Count);
            Assert.IsNull(p.RunCheckpoint);
            Assert.IsNull(p.LastSettledRunId);
        }

        [Test]
        public void NewProfile__IdsCountersAndMeta()
        {
            var p = Make();
            for (int i = 0; i < 5; i++) Assert.AreEqual(i + 1, p.Wands[i].Id);
            Assert.AreEqual(6, p.NextWandId);
            Assert.AreEqual(1, p.LastCharacter);
            Assert.AreEqual("Offline", p.ProfileKind);
            Assert.AreEqual(1, p.SchemaVersion);
            Assert.AreEqual("test-data-1", p.DataVersion);
            Assert.AreEqual("2026-11-16", p.ShopWeek.WeekStart);
            StringAssert.StartsWith("2026-11-18T15:30:00", p.CreatedAt);
        }
    }
}
