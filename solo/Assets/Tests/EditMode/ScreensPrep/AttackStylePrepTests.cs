using System.IO;
using System.Text;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Save;
using Wizard.Solo.Screens.Prep;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.ScreensPrep
{
    /// <summary>
    /// 원정 준비의 기본 공격 고르기(Docs/12-attack-styles.md 1절): 카드 3장, 캐릭터별 마지막 선택을 프로필(characters[].attack_style)에
    /// 저장·불러오기, 출발 판 설정에 싣기, 해금 손잡이(기본 꺼짐 — 켜면 웨이브 5·10).
    /// </summary>
    public sealed class AttackStylePrepTests
    {
        FakeProfileStore _store;
        FakeWandService _wands;
        FakeSettlementService _settle;
        GameContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _ctx = PrepFixture.Make(out _store, out _wands, out _settle);
            for (int c = 1; c <= 5; c++) PrepFixture.AddWand(_store, c, 100 + c, 0, true);
        }

        static string[] Styles(int c) => Spells.AttackStyleKit.Styles[c - 1];

        [Test]
        public void AS_Prep__ThreeCardsPerCharacter_DefaultIsA_AllOpen()
        {
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var cards = m.StyleCards();
            Assert.AreEqual(3, cards.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(Styles(1)[i], cards[i].Id);
                Assert.IsTrue(cards[i].Unlocked, "해금 손잡이 기본 꺼짐 = 셋 다 처음부터");
                Assert.AreEqual(0, cards[i].UnlockWave);
                Assert.Greater(cards[i].TagKeys.Count, 0, "특징 꼬리표");
                Assert.IsTrue(TestContent.Names.Has(cards[i].DescKey));
                Assert.IsTrue(TestContent.Names.Has(cards[i].SlotKey));
                Assert.AreEqual(Ids.Element.Fire, cards[i].Element);
            }
            Assert.AreEqual(Styles(1)[0], m.SelectedStyle);
            Assert.IsTrue(cards[0].Selected);
            Assert.IsFalse(cards[1].Selected);
        }

        [Test]
        public void AS_Prep__SelectionSavedPerCharacter_AndRestored()
        {
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            int saves = _store.SaveCount;
            Assert.IsTrue(m.SelectStyle(Styles(1)[1]));
            Assert.AreEqual(Styles(1)[1], _store.Data.Character(1).AttackStyle);
            Assert.Greater(_store.SaveCount, saves, "고르면 바로 저장");
            Assert.IsFalse(m.SelectStyle(Styles(1)[1]), "같은 것을 다시 고르면 바뀐 것 없음");
            Assert.IsFalse(m.SelectStyle(Styles(2)[0]), "다른 캐릭터의 유형은 못 고른다");
            Assert.AreEqual(Styles(1)[1], m.SelectedStyle);

            m.SelectCharacter(2);
            Assert.AreEqual(Styles(2)[0], m.SelectedStyle, "캐릭터마다 따로");
            m.SelectStyle(Styles(2)[2]);
            m.SelectCharacter(1);
            Assert.AreEqual(Styles(1)[1], m.SelectedStyle);

            var again = new ExpeditionPrepModel(_ctx);
            again.Enter();
            Assert.AreEqual(Styles(1)[1], again.SelectedStyle, "다시 들어와도 마지막 선택");
            again.SelectCharacter(2);
            Assert.AreEqual(Styles(2)[2], again.SelectedStyle);
        }

        [Test]
        public void AS_Prep__DepartCarriesTheStyleInRunConfig()
        {
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            m.SelectStyle(Styles(1)[2]);
            var r = m.Depart(42UL);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(Styles(1)[2], r.Config.AttackStyle);

            _store.Data.Character(1).AttackStyle = null;
            var fresh = new ExpeditionPrepModel(_ctx);
            fresh.Enter();
            var r2 = fresh.Depart(43UL);
            Assert.AreEqual(Styles(1)[0], r2.Config.AttackStyle, "고른 적 없으면 A");
            Assert.AreEqual(Styles(1)[0], _store.Data.Character(1).AttackStyle, "출발하면 기록에 남는다(연습·튜토리얼이 읽는다)");
        }

        [Test]
        public void AS_Prep__ForeignOrUnknownSavedValueFallsBackToA()
        {
            _store.Data.Character(1).AttackStyle = Styles(3)[1];
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.AreEqual(Styles(1)[0], m.SelectedStyle);
            _store.Data.Character(1).AttackStyle = "block.core.nope";
            m.Enter();
            Assert.AreEqual(Styles(1)[0], m.SelectedStyle);
        }

        [Test]
        public void AS_Prep__UnlockKnob_BAtWave5_CAtWave10()
        {
            _ctx.Content = WithUnlockEnabled();
            _store.Data.Character(1).AttackStyle = Styles(1)[2];
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.AreEqual(Styles(1)[0], m.SelectedStyle, "잠긴 선택은 A로");
            var cards = m.StyleCards();
            Assert.IsTrue(cards[0].Unlocked);
            Assert.IsFalse(cards[1].Unlocked);
            Assert.AreEqual(5, cards[1].UnlockWave);
            Assert.AreEqual(10, cards[2].UnlockWave);
            Assert.IsFalse(m.SelectStyle(Styles(1)[1]));

            _store.Data.Character(1).BestWave = 5;
            m.Enter();
            Assert.IsTrue(m.SelectStyle(Styles(1)[1]));
            Assert.IsFalse(m.SelectStyle(Styles(1)[2]));
            _store.Data.Character(1).BestWave = 10;
            m.Enter();
            Assert.IsTrue(m.SelectStyle(Styles(1)[2]));
        }

        [Test]
        public void AS_Profile__AttackStyleRoundTripsAndOldSavesReadNull()
        {
            var p = new ProfileData();
            p.Characters.Add(new CharacterRecord { Id = 1, AttackStyle = Styles(1)[1] });
            string json = SaveJson.Serialize(p);
            StringAssert.Contains("\"attack_style\"", json);
            var back = SaveJson.Deserialize<ProfileData>(json);
            Assert.AreEqual(Styles(1)[1], back.Character(1).AttackStyle);
            var old = SaveJson.Deserialize<ProfileData>("{\"characters\":[{\"id\":2,\"best_wave\":3}]}");
            Assert.IsNull(old.Character(2).AttackStyle, "옛 저장에는 없다 → A");
        }

        /// <summary>AttackStyles.json의 unlock.enabled만 켠 콘텐츠(나머지는 실제 파일 그대로).</summary>
        static IContentDb WithUnlockEnabled()
        {
            string data = TestContent.DataDir, extra = TestContent.SoloExtraDir;
            return ContentDb.Load(
                rel => { string f = Path.Combine(data, rel + ".json"); return File.Exists(f) ? File.ReadAllText(f, Encoding.UTF8) : null; },
                name =>
                {
                    string f = Path.Combine(extra, name + ".json");
                    if (!File.Exists(f)) return null;
                    string text = File.ReadAllText(f, Encoding.UTF8);
                    if (name == AttackStyles.ExtraName)
                    {
                        var jo = Newtonsoft.Json.Linq.JObject.Parse(text);
                        jo["unlock"]["enabled"] = true;
                        text = jo.ToString();
                    }
                    return text;
                },
                msg => { });
        }
    }
}
