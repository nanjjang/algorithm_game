using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Arts;
using Wizard.Solo.Tests.Spells;

namespace Wizard.Solo.Tests.Arts
{
    /// <summary>
    /// 파이널 어빌리티(AttackStyles.json final_abilities, ArtSystem.Finals.cs): 결정 게이지가 가득일 때 커맨드 없는 K → 결정 전부를 쓰고
    /// 캐릭터 × 기본 공격 유형마다 다른 파이널. 덜 찼으면 마력 방출 그대로. 손잡이는 명세 콘텐츠에서 꺼져 있고 FunPass가 켠다.
    /// </summary>
    public sealed class FinalAbilityTests
    {
        static ArtKit Kit(string charId, int crystals, string core)
        {
            var keep = TestContent.Db;
            TestContent.Db = AttackStyleKit.Tuned;
            try
            {
                var kit = new ArtKit(charId, crystals);
                if (core != null) kit.P.Spell.Core = AttackStyleKit.Tuned.Block(core);
                return kit;
            }
            finally { TestContent.Db = keep; }
        }

        static string CharOf(int i) => "char." + (i + 1);

        [Test]
        public void Final__SpecContent_IsOff()
        {
            Assert.IsFalse(AttackStyles.FinalsEnabled(TestContent.Db));
            Assert.IsTrue(AttackStyles.FinalsEnabled(AttackStyleKit.Tuned));
        }

        [Test]
        public void Final__EveryStyleHasItsOwnFinal()
        {
            var ids = new HashSet<string>();
            for (int c = 0; c < AttackStyleKit.Styles.Length; c++)
                foreach (var style in AttackStyleKit.Styles[c])
                {
                    var spec = AttackStyles.FinalAbility(AttackStyleKit.Tuned, style);
                    Assert.IsNotNull(spec, style);
                    Assert.IsTrue(ids.Add((string)spec["id"]), "파이널 id가 겹친다: " + style);
                }
            Assert.AreEqual(15, ids.Count);
        }

        [Test]
        public void Final__DoubleK_FiresFinal_SpendsAll_AndHits()
        {
            for (int c = 0; c < AttackStyleKit.Styles.Length; c++)
                foreach (var style in AttackStyleKit.Styles[c])
                {
                    string ch = CharOf(c);
                    int cap = AttackStyleKit.Tuned.Character(ch).CrystalCap;
                    var kit = Kit(ch, cap, style);
                    var e = kit.AddEnemy(kit.P.Pos.x + 2.5f, kit.P.Pos.y);
                    kit.Seq("K, K+1500");
                    kit.Stage5(240);
                    string id = (string)AttackStyles.FinalAbility(AttackStyleKit.Tuned, style)["id"];
                    Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtFired, id), style + ": 파이널 발동");
                    Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFired, Ids.Art.Burst), style + ": 마력 방출은 안 나간다");
                    Assert.AreEqual(0, kit.P.Crystals, style + ": 결정 전부");
                    Assert.Greater(kit.Spells.Directs.Count + kit.Spells.Executes.Count, 0, style + ": 무언가 때린다");
                    Assert.IsEmpty(ArtSystem.StateOf(kit.P).Finals, style + ": 끝난다");
                }
        }

        [Test]
        public void Final__DoubleK_NeedsTwoCrystals_SingleKIsBurst()
        {
            int need = AttackStyles.FinalMinCrystals(AttackStyleKit.Tuned);
            Assert.AreEqual(2, need);
            var kit = Kit(Ids.Char.C1, need - 1, "block.core.ember_mortar");
            kit.Seq("K, K+1500");
            kit.Stage5(60);
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFired, "final.ember_mortar"), "결정이 모자라면 파이널 아님");
            // 한 번만 누르면 창이 지난 뒤 마력 방출
            var plain = Kit(Ids.Char.C1, need + 2, "block.core.ember_mortar");
            plain.Seq("K");
            Assert.AreEqual(0, plain.CountEvents(SimEventType.ArtFired), "두 번째 K를 잠깐 기다린다");
            plain.Stage5(30);
            Assert.AreEqual(1, plain.CountEvents(SimEventType.ArtFired, Ids.Art.Burst));
            Assert.AreEqual(0, plain.CountEvents(SimEventType.ArtFired, "final.ember_mortar"));
            // 딱 min개면 파이널
            var kit3 = Kit(Ids.Char.C1, need, "block.core.ember_mortar");
            kit3.Seq("K, K+1500");
            Assert.AreEqual(1, kit3.CountEvents(SimEventType.ArtFired, "final.ember_mortar"));
            Assert.AreEqual(0, kit3.P.Crystals);
        }

        [Test]
        public void Final__DifferentStylesOfOneCharacter_GiveDifferentFinals()
        {
            var seen = new HashSet<string>();
            foreach (var style in AttackStyleKit.Styles[0])
            {
                int cap = AttackStyleKit.Tuned.Character(Ids.Char.C1).CrystalCap;
                var kit = Kit(Ids.Char.C1, cap, style);
                kit.Seq("K, K+1500");
                var ev = kit.LastEvent(SimEventType.ArtFired);
                Assert.IsTrue(seen.Add(ev.Id), style);
            }
        }
    }
}
