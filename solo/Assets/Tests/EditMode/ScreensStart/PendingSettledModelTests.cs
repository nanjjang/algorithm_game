using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Start;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.ScreensStart
{
    /// <summary>자동 정산 알림(00 문서 D-68·D-23)의 줄 목록.</summary>
    public class PendingSettledModelTests
    {
        static List<PendingLineKind> Kinds(List<PendingLine> lines)
        {
            var k = new List<PendingLineKind>();
            foreach (var l in lines) k.Add(l.Kind);
            return k;
        }

        // 계약 벡터: 결과가 빈 정산(골드 0·재료 없음)도 알림 한 줄은 나온다.
        [Test]
        public void S00_D_68__EmptySettlementStillHasMessageLine()
        {
            var lines = PendingSettledModel.Lines(new SettlementResult { FromCheckpoint = true });
            Assert.GreaterOrEqual(lines.Count, 1);
            Assert.AreEqual(PendingLineKind.Message, lines[0].Kind);
            CollectionAssert.AreEqual(new[] { PendingLineKind.Message, PendingLineKind.Nothing }, Kinds(lines));
            var text = PendingSettledModel.Render(new SettlementResult(), TestContent.Names);
            Assert.IsFalse(string.IsNullOrEmpty(text[0]));
            Assert.AreNotEqual("str.main_menu.pending_settled.message_no_wave", text[0], "열쇠가 이름 표에 있다");
        }

        // D-68: 받은 것 — 골드, 재료 목록, 까마귀 보따리, 새 칭호, 복수 보상, 새 비기(+ 자동으로 담은 블록, D-23).
        [Test]
        public void S00_D_68__LinesListEverythingReceived()
        {
            var r = new SettlementResult
            {
                FromCheckpoint = true,
                GoldGained = 1200,
                CrowBundle = true,
                RevengeReward = true,
                Record = new RunRecordData { ReachedWave = 15 },
            };
            r.Materials.Add(new MaterialGain { ItemId = "mat.stone_low", Qty = 2 });
            r.Materials.Add(new MaterialGain { ItemId = "mat.stone_mid", Qty = 1 });
            r.Materials.Add(new MaterialGain { ItemId = "mat.stone_low", Qty = 1 });
            r.Bagged.Add(new WildBlock { Uid = 1, BlockId = "block.fireball" });
            r.NewTitles.Add("title.revenge.mirror_breaker");
            r.NewArts.Add("art.burst");

            var lines = PendingSettledModel.Lines(r);
            CollectionAssert.AreEqual(new[]
            {
                PendingLineKind.Message, PendingLineKind.Gold, PendingLineKind.Material, PendingLineKind.Material, PendingLineKind.Block,
                PendingLineKind.CrowBundle, PendingLineKind.Title, PendingLineKind.RevengeReward, PendingLineKind.Art
            }, Kinds(lines));
            Assert.AreEqual(15, lines[0].N);
            Assert.AreEqual(1200, lines[1].N);
            Assert.AreEqual("mat.stone_low", lines[2].ContentId);
            Assert.AreEqual(3, lines[2].N, "같은 재료는 합친다");
            Assert.AreEqual("mat.stone_mid", lines[3].ContentId);

            var text = PendingSettledModel.Render(r, TestContent.Names);
            StringAssert.Contains("15", text[0]);
            StringAssert.Contains("1,200", text[1]);
            StringAssert.Contains(TestContent.Names.Get("mat.stone_low"), text[2]);
            StringAssert.Contains(TestContent.Names.Get("block.fireball"), text[4]);
            StringAssert.Contains(TestContent.Names.Get("title.revenge.mirror_breaker"), text[6]);
            StringAssert.Contains(TestContent.Names.Get("art.burst"), text[8]);
        }

        // 이 작업이 더한 이름 열쇠(G18 조각)가 이름 표에 다 있다(없으면 열쇠가 화면에 찍힌다).
        [Test]
        public void S00_9__G18NameKeysPresent()
        {
            foreach (var k in new[]
            {
                StartNameKeys.PendingTitle, StartNameKeys.PendingMessage, StartNameKeys.PendingMessageNoWave, StartNameKeys.PendingReceived,
                StartNameKeys.PendingGold, StartNameKeys.PendingMaterial, StartNameKeys.PendingBlock, StartNameKeys.PendingCrowBundle,
                StartNameKeys.PendingTitleLine, StartNameKeys.PendingRevengeReward, StartNameKeys.PendingArt, StartNameKeys.PendingNothing,
                StartNameKeys.MainMenuExitConfirm
            })
                Assert.IsTrue(TestContent.Names.Has(k), k);
        }
    }
}
