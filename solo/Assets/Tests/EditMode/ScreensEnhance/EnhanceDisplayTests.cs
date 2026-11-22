using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Enhance;

namespace Wizard.Solo.Tests.ScreensEnhance
{
    /// <summary>08 요약서 10.4절 확률 표시(TV-084~096), 10.7절 결과 카드(TV-114~124, TV-131), 10.9절 이어서 강화(TV-127~130), 07 12.12절.</summary>
    public sealed class EnhanceDisplayTests
    {
        EnhanceConfig Cfg => TestContent.Db.Enhance;
        INameTable Names => TestContent.Names;
        EnhanceScreenTuning T => EnhanceScreenTuning.From(TestContent.Db);

        // ------------------------------------------------------------------ 10.4 확률

        [Test]
        public void S08_TV_084__Plus7RareHeroSameElementAura2Is82()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 7, 8, 2, 22, true, false);
            Assert.AreEqual(25, m.BasePct);
            Assert.AreEqual(22, m.SacrificeLinePct);
            Assert.AreEqual(5, m.SameElementPct);
            Assert.AreEqual(30, m.AuraPct);
            Assert.AreEqual(82, m.TotalPct);
            Assert.IsFalse(m.Capped);
        }

        [Test]
        public void S08_TV_085__DropToHintAndTag()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 7, 8, 2, 22, true, false);
            Assert.AreEqual(EnhanceHint.DropTo, m.Hint);
            Assert.AreEqual("실패하면: +6으로 하락", m.HintText(Names));
            Assert.IsTrue(m.DropPossibleTag);
        }

        [Test]
        public void S08_TV_086__BandOf82()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 7, 8, 2, 22, true, false);
            Assert.AreEqual(1230, m.Band.GreatBp);
            Assert.AreEqual(6970, m.Band.SuccessBp);
            Assert.AreEqual(450, m.Band.MutationBp);
            Assert.AreEqual(1350, m.Band.FailBp);
            Assert.AreEqual(10000, m.Band.Total);
            var b = ProbabilityDisplayModel.BandOf(1230, 8200, 8650);
            Assert.AreEqual(6970, b.SuccessBp);
        }

        [Test]
        public void S08_TV_087__PlusZeroIs100NoGreat()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 0, 0, 3, 70, true, false);
            Assert.AreEqual(100, m.TotalPct);
            Assert.AreEqual(0, m.Band.GreatBp);
            Assert.AreEqual(10000, m.Band.SuccessBp);
            Assert.IsFalse(m.Capped);
        }

        [Test]
        public void S08_TV_088__Plus1HeroCappedAt95()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 1, 1, 0, 14, false, false);
            Assert.AreEqual(104, m.RawPct);
            Assert.AreEqual(95, m.TotalPct);
            Assert.IsTrue(m.Capped);
        }

        [Test]
        public void S08_TV_089__Plus12SacrificeCapAndAura3()
        {
            // 08 10.4절은 같은 속성 +5를 상한(+40) 밖으로 읽어 93을 적었고, 강화 규칙의 주인 요약서 07(13절 M-1, TV-P-04)은
            // 상한을 등급+속성 합계에 걸어 88로 정했다(08 18.1 M-06 "규칙 요약서와 맞출 것"). 화면은 규칙과 같은 값을 보여야 하므로 07을 따른다.
            var m = ProbabilityDisplayModel.Compute(Cfg, 12, 12, 3, 50, true, false);
            Assert.AreEqual(3, m.BasePct);
            Assert.AreEqual(40, m.SacrificeLinePct + m.SameElementPct, "제물 줄 둘의 합은 상한 40");
            Assert.AreEqual(45, m.AuraPct);
            Assert.AreEqual(88, m.TotalPct);
        }

        [Test]
        public void S08_TV_090__Plus3NoDropLowPlus4DropTo3()
        {
            var a = ProbabilityDisplayModel.Compute(Cfg, 3, 3, 0, 0, false, false);
            Assert.IsFalse(a.DropPossible);
            Assert.AreEqual(EnhanceHint.NoDropLow, a.Hint);
            var b = ProbabilityDisplayModel.Compute(Cfg, 4, 4, 0, 0, false, false);
            Assert.IsTrue(b.DropPossible);
            Assert.AreEqual(EnhanceHint.DropTo, b.Hint);
            Assert.AreEqual(3, b.HintValue);
        }

        [Test]
        public void S08_TV_091__AtFloorCharmFolded()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 7, 9, 0, 0, false, false);
            Assert.AreEqual(EnhanceHint.AtFloor, m.Hint);
            Assert.AreEqual("바닥이다(역대 최고 +9)", m.HintText(Names));
            Assert.IsTrue(m.CharmSlotFolded);
            Assert.AreEqual("지금은 필요 없다", Names.Get(EnhanceNameKeys.CharmNotNeeded));
        }

        [Test]
        public void S08_TV_092__CharmProtectsNoTag()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 8, 8, 0, 0, false, true);
            Assert.AreEqual(EnhanceHint.CharmProtects, m.Hint);
            Assert.AreEqual("부적이 막아 준다", m.HintText(Names));
            Assert.IsFalse(m.DropPossibleTag);
        }

        [Test]
        public void S08_TV_093__GoldCost()
        {
            Assert.AreEqual(100, ProbabilityDisplayModel.Compute(Cfg, 0, 0, 0, 0, false, false).CostGold);
            Assert.AreEqual(6400, ProbabilityDisplayModel.Compute(Cfg, 7, 7, 0, 0, false, false).CostGold);
            Assert.AreEqual(16900, ProbabilityDisplayModel.Compute(Cfg, 12, 12, 0, 0, false, false).CostGold);
        }

        [Test]
        public void S08_TV_094__StoneForTarget()
        {
            Assert.AreEqual(Ids.Item.StoneLow, ProbabilityDisplayModel.Compute(Cfg, 4, 4, 0, 0, false, false).StoneId);
            Assert.AreEqual(Ids.Item.StoneMid, ProbabilityDisplayModel.Compute(Cfg, 5, 5, 0, 0, false, false).StoneId);
            Assert.AreEqual(Ids.Item.StoneMid, ProbabilityDisplayModel.Compute(Cfg, 8, 8, 0, 0, false, false).StoneId);
            Assert.AreEqual(Ids.Item.StoneHigh, ProbabilityDisplayModel.Compute(Cfg, 9, 9, 0, 0, false, false).StoneId);
            Assert.AreEqual(Ids.Item.StoneHigh, ProbabilityDisplayModel.Compute(Cfg, 12, 12, 0, 0, false, false).StoneId);
        }

        [Test]
        public void S08_TV_095__Gold6399AtPlus8DisabledShakeGold()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 7, 7, 0, 0, false, false);
            m.SetHoldings(6399, 5);
            Assert.IsFalse(m.CanAfford);
            Assert.AreEqual(CostShortage.Gold, m.Shortage);
        }

        [Test]
        public void S08_TV_096__LastProjectileCannotBePlaced()
        {
            var bolt = TestContent.Db.Block(Ids.Block.MagicBolt);
            Assert.IsTrue(bolt.IsProjectile);
            Assert.IsTrue(ProbabilityDisplayModel.CannotPlace(bolt, 1, 0));
            Assert.IsFalse(ProbabilityDisplayModel.CannotPlace(bolt, 2, 0));
            Assert.IsTrue(ProbabilityDisplayModel.CannotPlace(bolt, 2, 1), "이미 하나를 올렸으면 남은 하나는 못 올린다");
            Assert.AreEqual("놓을 수 없음", Names.Get(EnhanceNameKeys.CannotPlace));
            Assert.AreEqual("마지막 투사체 블록은 올릴 수 없다", Names.Get(EnhanceNameKeys.LastProjectileTip));
        }

        [Test]
        public void S08_10_4__MaxStageHasNoAttempt()
        {
            var m = ProbabilityDisplayModel.Compute(Cfg, 13, 13, 0, 0, false, false);
            Assert.IsTrue(m.AtMax);
            Assert.AreEqual(ErrorCode.MaxStage, m.Blocked);
            Assert.IsFalse(m.DropPossibleTag);
        }

        [Test]
        public void S08_10_4__FromPreviewSplitsSameElementLine()
        {
            var b = new ProbBreakdown { BasePct = 25, SacrificePct = 27, SameElementPct = 5, AuraPct = 30, RawPct = 82, SuccessPct = 82, SuccessBp = 8200, GreatBp = 1230, MutationEndBp = 8650, DropPossible = true, CostGold = 6400, StoneId = Ids.Item.StoneMid, StoneCount = 1 };
            var m = ProbabilityDisplayModel.FromPreview(Cfg, b, 7, 8, true, false);
            Assert.AreEqual(22, m.SacrificeLinePct);
            Assert.AreEqual(5, m.SameElementPct);
            Assert.AreEqual(450, m.Band.MutationBp);
        }

        // ------------------------------------------------------------------ 10.7 결과 카드

        static EnhanceResult R(EnhanceOutcome o, int before, int after, int auraBefore = 0, int auraAfter = 0) =>
            G23Results.Make(o, before, after, auraBefore, auraAfter);

        static bool Has(List<CardLine> lines, CardLineKind k) => lines.Exists(l => l.Kind == k);
        static string Text(List<CardLine> lines, CardLineKind k) { var l = lines.Find(x => x.Kind == k); return l.Text; }

        [Test]
        public void S08_TV_114__OfflineNoServerNoticeLine()
        {
            var r = R(EnhanceOutcome.Success, 9, 10);
            r.ServerNotice = "plus10";
            Assert.IsFalse(Has(EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Offline, 1, 3), CardLineKind.ServerNotice));
            Assert.IsTrue(Has(EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Server, 1, 3), CardLineKind.ServerNotice));
        }

        [Test]
        public void S08_TV_115__Plus9NoServerNotice()
        {
            var r = R(EnhanceOutcome.Success, 8, 9);
            Assert.IsFalse(Has(EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Server, 1, 3), CardLineKind.ServerNotice));
        }

        [Test]
        public void S08_TV_117__GreatWithFullSlotsSaysSlotFull()
        {
            var r = R(EnhanceOutcome.GreatSuccess, 5, 7);
            r.SlotAlreadyFull = true;
            var lines = EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Offline, 1, 3);
            Assert.AreEqual("슬롯은 이미 다 자랐다", Text(lines, CardLineKind.Slot));
            var m = G23Results.Model(T);
            m.Begin(r, 0, RitualStyle.Full);
            var cues = new List<EnhanceCue>();
            while (m.Phase != EnhancePhase.ResultCard) { m.Tick(); m.DrainCues(cues); }
            Assert.IsFalse(cues.Exists(c => c.EventId == "ui.enhance.slot_extend"), "슬롯 확장 구간을 건너뛴다");
        }

        [Test]
        public void S07_TV_B_19__GreatSlotExtendedLine()
        {
            var r = R(EnhanceOutcome.GreatSuccess, 8, 10);
            r.SlotExpanded = true;
            r.Wand.SlotsExtra = 3;
            var lines = EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Offline, 1, 3);
            Assert.AreEqual("연결 슬롯 +1 (3/3)", Text(lines, CardLineKind.Slot));
        }

        [Test]
        public void S08_TV_119__FailDropTraceBannerHidden()
        {
            var r = R(EnhanceOutcome.FailDrop, 7, 6, 0, 1);
            r.Wand.ShowScars = false;
            var lines = EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Offline, 1, 3);
            Assert.AreEqual("흔적이 남았다 · 지금은 비공개", Text(lines, CardLineKind.Trace));
            Assert.AreEqual("원래 그런 거다.", Text(lines, CardLineKind.Master));
            Assert.AreEqual("기운 0 → 1", Text(lines, CardLineKind.Aura));
        }

        [Test]
        public void S08_TV_120__CharmBlockedLineAuraUnchanged()
        {
            var r = R(EnhanceOutcome.FailCharmBlocked, 9, 9, 1, 1);
            r.CharmConsumed = true;
            var lines = EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Offline, 1, 3);
            Assert.AreEqual("부적 1개가 대신 탔다.", Text(lines, CardLineKind.Charm));
            Assert.IsFalse(Has(lines, CardLineKind.Aura), "기운 1 그대로");
        }

        [Test]
        public void S08_TV_121__FailNoDropAtFloorLine()
        {
            var r = R(EnhanceOutcome.FailNoDrop, 7, 7, 0, 1);
            r.Wand.FloorPeak = 9;
            r.Wand.RecordPeak = 9;
            var lines = EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Offline, 1, 3);
            Assert.AreEqual("더는 안 내려간다 (역대 최고 +9)", Text(lines, CardLineKind.Master));
            Assert.AreEqual("기운 0 → 1", Text(lines, CardLineKind.Aura));
            var low = EnhanceResultCardModel.Build(Names, Cfg, R(EnhanceOutcome.FailNoDrop, 2, 2, 0, 1), null, ProfileKind.Offline, 1, 3);
            Assert.AreEqual("내려가지는 않았다.", Text(low, CardLineKind.Master));
        }

        [Test]
        public void S08_TV_122__MutationPushOutLine()
        {
            string line = EnhanceResultCardModel.PushOutLine(Names, Ids.Mutation.TwistedBarrel);
            Assert.AreEqual("가장 오래된 뒤틀린 총열이 밀려난다", line);
            Assert.IsNull(EnhanceResultCardModel.PushOutLine(Names, null));
        }

        [Test]
        public void S08_TV_124__AuraAtMaxStaysThreeNoLine()
        {
            var lines = EnhanceResultCardModel.Build(Names, Cfg, R(EnhanceOutcome.FailDrop, 8, 7, 3, 3), null, ProfileKind.Offline, 1, 3);
            Assert.IsFalse(Has(lines, CardLineKind.Aura));
        }

        [Test]
        public void S08_TV_131__RollLineOneDecimal()
        {
            var r = R(EnhanceOutcome.Success, 7, 8);
            r.RollBp = 3720;
            r.Breakdown = new ProbBreakdown { SuccessPct = 82 };
            Assert.AreEqual("뽑힌 값 37.2 / 82.0 이하면 성공", EnhanceResultCardModel.RollLine(Names, r, T.RollDecimals));
            Assert.AreEqual("81.9", EnhanceResultCardModel.RollNumber(8199, 1), "버림 — 미만 판정을 넘어 보이지 않게");
        }

        [Test]
        public void S08_D_66__CharacterLineHiddenWithoutValue()
        {
            var r = R(EnhanceOutcome.Success, 2, 3);
            Assert.IsFalse(Names.Has(EnhanceNameKeys.CharLine(EnhanceOutcome.Success)), "값은 아직 정해지지 않았다(N-05)");
            Assert.IsFalse(Has(EnhanceResultCardModel.Build(Names, Cfg, r, null, ProfileKind.Offline, 1, 3), CardLineKind.Character));
            var withLine = new G23Names(Names).Set("str.enhance.char_line.success", "line");
            var lines = EnhanceResultCardModel.Build(withLine, Cfg, r, null, ProfileKind.Offline, 1, 3);
            Assert.AreEqual("line", Text(lines, CardLineKind.Character));
            Assert.AreEqual("str.enhance.char_line.great_success", EnhanceNameKeys.CharLine(EnhanceOutcome.GreatSuccess));
            Assert.AreEqual("str.enhance.char_line.fail_charm_blocked", EnhanceNameKeys.CharLine(EnhanceOutcome.FailCharmBlocked));
            Assert.AreEqual("str.enhance.char_line.fail_no_drop", EnhanceNameKeys.CharLine(EnhanceOutcome.FailNoDrop));
            Assert.AreEqual("str.enhance.char_line.fail_drop", EnhanceNameKeys.CharLine(EnhanceOutcome.FailDrop));
            Assert.AreEqual("str.enhance.char_line.mutation", EnhanceNameKeys.CharLine(EnhanceOutcome.MutationOffered));
        }

        [Test]
        public void S08_10_7__MilestoneBandText()
        {
            string band = EnhanceResultCardModel.MilestoneBand(Names, Cfg, 5, 9, 10);
            Assert.AreEqual("달 없는 밤의 → 꽤 위대한 그림자", band);
            Assert.AreEqual("엄청엄청 위대한 제자 5", EnhanceNameKeys.StageTitle(Names, Cfg, 5, 13), "D-27: 이름 자리는 캐릭터 표시 이름");
        }

        [Test]
        public void S08_TV_144__ScarCount()
        {
            Assert.AreEqual(2, EnhanceResultPlan.ScarCount(5, 7, T.ScarMax));
            Assert.AreEqual(1, EnhanceResultPlan.ScarCount(5, 6, T.ScarMax));
            Assert.AreEqual(0, EnhanceResultPlan.ScarCount(7, 7, T.ScarMax));
        }

        // ------------------------------------------------------------------ 10.9 이어서 강화

        [Test]
        public void S08_TV_127__ContinueStopsAtTarget()
        {
            Assert.AreEqual(ContinueStop.TargetReached, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.Success, 9, 10), 10, true));
            Assert.AreEqual(ContinueStop.None, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.Success, 8, 9), 10, true));
        }

        [Test]
        public void S08_TV_128__ContinueStopsOnGreatMutationMilestone()
        {
            Assert.AreEqual(ContinueStop.GreatSuccess, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.GreatSuccess, 5, 7), 12, true));
            Assert.AreEqual(ContinueStop.MutationOffered, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.MutationOffered, 5, 5), 12, true));
            var ms = R(EnhanceOutcome.Success, 6, 7);
            ms.MilestoneStage = 7;
            Assert.AreEqual(ContinueStop.Milestone, ContinuousEnhanceModel.StopAfter(ms, 12, true));
        }

        [Test]
        public void S08_TV_129__ContinueStopsWhenSameBlockGone()
        {
            var p = new ProfileData();
            p.Item(Ids.Block.Fireball).Qty = 1;
            var template = new List<string> { Ids.Block.Fireball, Ids.Block.Fireball };
            var picked = ContinuousEnhanceModel.PickSacrifices(TestContent.Db, p, template);
            Assert.IsNull(picked);
            Assert.AreEqual(ContinueStop.SacrificeShort, ContinuousEnhanceModel.StopBefore(picked, ErrorCode.None));
            Assert.AreEqual(ContinueStop.CannotAttempt, ContinuousEnhanceModel.StopBefore(new List<string>(), ErrorCode.NotEnoughGold));
        }

        [Test]
        public void S07_TV_Q_06__GreatAtPlus5WithTarget7Stops()
        {
            Assert.AreEqual(ContinueStop.GreatSuccess, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.GreatSuccess, 5, 7), 7, true));
        }

        [Test]
        public void S07_TV_Q_07__Plus3To4StopsAtMilestone()
        {
            var r = R(EnhanceOutcome.Success, 3, 4);
            r.MilestoneStage = 4;
            Assert.AreEqual(ContinueStop.Milestone, ContinuousEnhanceModel.StopAfter(r, 10, true));
        }

        [Test]
        public void S07_TV_Q_08__TwoSameElementCommonsForThreeSlotTemplateStops()
        {
            // 같은 (등급, 속성) 블록이 서고에 2개뿐인데 구성은 3개.
            var db = TestContent.Db;
            BlockDef a = null;
            foreach (var b in db.Blocks) if (b.Rarity == Ids.Rarity.Common) { a = b; break; }
            Assert.IsNotNull(a);
            var p = new ProfileData();
            p.Item(a.Id).Qty = 2;   // 그 (등급, 속성)의 블록은 서고에 이것 2개뿐
            var template = new List<string> { a.Id, a.Id, a.Id };
            Assert.IsNull(ContinuousEnhanceModel.PickSacrifices(db, p, template));
            CollectionAssert.AreEqual(new[] { a.Id, a.Id }, ContinuousEnhanceModel.PickSacrifices(db, p, new List<string> { a.Id, a.Id }));
        }

        [Test]
        public void S07_TV_Q_09__StopOnDrop()
        {
            Assert.AreEqual(ContinueStop.Dropped, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.FailDrop, 8, 7, 0, 1), 12, true));
            Assert.AreEqual(ContinueStop.None, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.FailDrop, 8, 7, 0, 1), 12, false));
            Assert.IsTrue(T.AutoStopOnDropDefault, "하락에서 멈춤 기본 켬 [임시]");
        }

        [Test]
        public void S07_TV_Q_10__OneResultPerAttempt()
        {
            var c = new ContinuousEnhanceModel(true);
            c.Start(new List<string>(), false, 10);
            c.Count(R(EnhanceOutcome.Success, 1, 2));
            Assert.AreEqual(1, c.Attempts, "결과 하나 = 시도 하나");
        }

        [Test]
        public void S08_TV_130__TallyText()
        {
            var c = new ContinuousEnhanceModel(true);
            c.Start(new List<string>(), false, 13);
            long[] gold = { 6400, 6400, 4900, 4900, 3600, 3600, 3600, 2500, 2500, 2500, 1600, 2300 };
            for (int i = 0; i < 12; i++)
            {
                var o = i < 5 ? EnhanceOutcome.Success : i == 5 ? EnhanceOutcome.MutationOffered : i == 6 ? EnhanceOutcome.FailCharmBlocked : EnhanceOutcome.FailDrop;
                var r = R(o, 5, 5);
                r.Breakdown = new ProbBreakdown { CostGold = gold[i] };
                c.Count(r);
            }
            Assert.AreEqual(44800, c.GoldSpent);
            Assert.AreEqual("12번째 시도 · 성공 5 · 실패 7 · 쓴 골드 44,800", c.TallyText(Names));
        }

        [Test]
        public void S07_5_4__NoCharmOnNoDropAttempt()
        {
            Assert.IsFalse(ContinuousEnhanceModel.CharmFor(true, false));
            Assert.IsTrue(ContinuousEnhanceModel.CharmFor(true, true));
        }
    }
}
