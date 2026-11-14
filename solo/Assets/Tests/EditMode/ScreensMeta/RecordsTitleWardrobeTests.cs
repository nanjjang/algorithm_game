using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Meta;

namespace Wizard.Solo.Tests.ScreensMeta
{
    /// <summary>기록(08 11.1절), 칭호(01 10.5절), 옷장(08 11.2절) 모델. 기대값은 요약서에서.</summary>
    public class RecordsTitleWardrobeTests
    {
        // 08 13.2절: Mirror = (bestDamagePct ≥ 50) ? Cracked.
        const int CrackedPercent = 50;

        // ---------------------------------------------------------------- 기록 · 복수 기록

        [Test]
        public void S08_TV_167__NoRevengeSlotBeforeFirstMeeting()
        {
            var p = MetaProfiles.Five();
            p.Flags.PhantomMet = false;
            var v = RevengeTracker.Build(p, CrackedPercent);
            Assert.AreEqual(RevengeRecordKind.None, RecordsModel.RevengeKind(RecordsModel.AllTab, v));
        }

        [Test]
        public void S08_11_1__PursuingShowsTrackerOnlyOnAllTab()
        {
            var p = MetaProfiles.Five();
            p.Flags.PhantomMet = true;
            var v = RevengeTracker.Build(p, CrackedPercent);
            Assert.AreEqual(RevengeRecordKind.Tracker, RecordsModel.RevengeKind(RecordsModel.AllTab, v));
            for (int tab = 1; tab <= 5; tab++) Assert.AreEqual(RevengeRecordKind.None, RecordsModel.RevengeKind(tab, v));
        }

        [Test]
        public void S08_11_1__DoneShowsDoneLineWithDateAndRuns()
        {
            var p = MetaProfiles.Five();
            p.Flags.PhantomMet = true;
            p.Flags.RevengeDoneAt = "2026-10-30T21:15:00";
            p.Revenge.RunsSinceFirstMeet = 7;
            var v = RevengeTracker.Build(p, CrackedPercent);
            Assert.AreEqual(RevengeRecordKind.Done, RecordsModel.RevengeKind(RecordsModel.AllTab, v));
            Assert.AreEqual(MirrorMark.Broken, v.Mirror);
            string line = TestContent.Names.Format(MetaKeys.RevengeDoneLine, "date", RecordsModel.DatePart(v.DoneDate), "n", v.RunsToComplete);
            Assert.AreEqual("복수 완료 · 2026-10-30 · 첫 만남에서 7판째", line);
        }

        [Test]
        public void S08_TV_234__TrackerHasNoDentLineWithoutAttempts()
        {
            var p = MetaProfiles.Five();
            p.Flags.PhantomMet = true;
            var v = RevengeTracker.Build(p, CrackedPercent);
            Assert.IsFalse(v.HasDent);
            Assert.AreEqual(2, RecordsModel.TrackerLines(v));
        }

        [Test]
        public void S08_TV_235__DentLineAndCrackedMirror()
        {
            var p = MetaProfiles.Five();
            p.Flags.PhantomMet = true;
            p.Revenge.PhantomBestDent = 62;
            p.Revenge.PhantomTries = 3;
            var v = RevengeTracker.Build(p, CrackedPercent);
            Assert.AreEqual(3, RecordsModel.TrackerLines(v));
            Assert.AreEqual("허상에게 낸 가장 큰 흠집 62% · 도전 3번",
                TestContent.Names.Format(MetaKeys.RevengeDent, "percent", v.DentPercent, "tries", v.Tries));
            Assert.AreEqual(MirrorMark.Cracked, v.Mirror);
            Assert.AreEqual(CrackedPercent, TestContent.Db.Const.RevengeCrackedPercent, "const.json revenge_cracked_percent = 08 13.2절의 50");
        }

        [Test]
        public void S08_13_2__TrackerProgressNumeratorIsMin19()
        {
            var p = MetaProfiles.Five();
            p.Flags.PhantomMet = true;
            p.Character(2).BestWave = 14;
            var v = RevengeTracker.Build(p, CrackedPercent);
            Assert.AreEqual(14, v.Progress);
            Assert.AreEqual(19, v.ProgressMax);
            Assert.AreEqual(14f / 19f, RecordsModel.TrackerProgress01(v), 1e-6f);
            p.Character(3).BestWave = 33;
            Assert.AreEqual(19, RevengeTracker.Build(p, CrackedPercent).Progress);
        }

        [Test]
        public void S08_11_1__RunListNewestFirstAndFilteredByTab()
        {
            var p = MetaProfiles.Five();
            p.RunHistory.Add(MetaProfiles.Run("a", 1, 5));
            p.RunHistory.Add(MetaProfiles.Run("b", 2, 12));
            p.RunHistory.Add(MetaProfiles.Run("c", 1, 23));
            CollectionAssert.AreEqual(new[] { "c", "b", "a" }, RunIds(RecordsModel.Runs(p, RecordsModel.AllTab)));
            CollectionAssert.AreEqual(new[] { "c", "a" }, RunIds(RecordsModel.Runs(p, 1)));
            CollectionAssert.AreEqual(new[] { "b" }, RunIds(RecordsModel.Runs(p, 2)));
            Assert.IsEmpty(RecordsModel.Runs(p, 3));
        }

        [Test]
        public void S08_11_1__EmptyHistoryGivesEmptyList()
        {
            Assert.IsEmpty(RecordsModel.Runs(MetaProfiles.Five(), RecordsModel.AllTab));
            Assert.IsEmpty(RecordsModel.Runs(null, RecordsModel.AllTab));
        }

        [Test]
        public void S08_N_11__NoDuelRecordOrSeasonRowAndNoReplayColumn()
        {
            CollectionAssert.DoesNotContain(RecordsModel.SummaryRowKeys, "str.records.duel_record");
            CollectionAssert.DoesNotContain(RecordsModel.SummaryRowKeys, "str.records.season_result");
            CollectionAssert.Contains(RecordsModel.SummaryRowKeys, "str.records.titles");
            CollectionAssert.DoesNotContain(RecordsModel.ColumnKeys, "str.records.replay");
            CollectionAssert.AreEqual(new[]
            {
                "str.records.date", "str.records.character", "str.records.reached_wave", "str.records.start_wave", "str.records.score",
                "str.records.wand_stage", "str.records.loadout"
            }, RecordsModel.ColumnKeys);
        }

        [Test]
        public void S08_11_1__SixTabsAllPlusFiveCharacters()
        {
            Assert.AreEqual(6, RecordsModel.TabCount);
            Assert.AreEqual("str.records.tab.all", RecordsModel.TabKey(0));
            for (int i = 1; i <= 5; i++) Assert.AreEqual("char." + i, RecordsModel.TabKey(i));
        }

        [Test]
        public void S08_TV_161__ReachedWaveDayFormat()
        {
            // "3일째 웨이브 23"
            Assert.AreEqual(3, RecordsModel.DayOf(23));
            Assert.AreEqual("3일째 웨이브 23", TestContent.Names.Format(MetaKeys.FormatDayWave, "day", RecordsModel.DayOf(23), "wave", 23));
        }

        [Test]
        public void S08_11_1__SummaryBestAndTotals()
        {
            var p = MetaProfiles.Five();
            p.Character(1).BestScore = 1200; p.Character(1).BestWave = 12; p.Character(1).TotalWaves = 120;
            p.Character(2).BestScore = 3400; p.Character(2).BestWave = 9; p.Character(2).TotalWaves = 30;
            var th = new List<int> { 0, 100, 500, 2000 };
            var all = RecordsModel.Summary(p, 0, th, new List<int> { 4, 7, 10, 13 });
            Assert.AreEqual(3400, all.BestScore);
            Assert.AreEqual(12, all.BestWave);
            Assert.AreEqual(150, all.TotalWaves);
            Assert.AreEqual(0, all.WildStainStage, "전체 탭은 야생 물 단계를 그리지 않는다");
            var c1 = RecordsModel.Summary(p, 1, th, new List<int> { 4, 7, 10, 13 });
            Assert.AreEqual(1200, c1.BestScore);
            Assert.AreEqual(120, c1.TotalWaves);
            Assert.AreEqual(2, c1.WildStainStage);
        }

        static List<string> RunIds(List<RunRecordData> runs)
        {
            var l = new List<string>();
            foreach (var r in runs) l.Add(r.RunId);
            return l;
        }

        // ---------------------------------------------------------------- 칭호

        [Test]
        public void S01_10_5__StageTitleBands()
        {
            var ms = TitleModel.Milestones(TestContent.Db);
            // b0 +0~+3, b1 +4~+6, b2 +7~+9, b3 +10~+12, b4 +13
            int[] expected = { 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4 };
            for (int stage = 0; stage <= 13; stage++)
                Assert.AreEqual(expected[stage], TitleModel.Band(ms, stage), "+" + stage);
        }

        [Test]
        public void S01_10_5__AvailableTitlesIncludeStageTitlesFromHeldWands()
        {
            var p = MetaProfiles.Five(0, 4, 7, 10, 13);
            p.Titles.Owned.Add(Wizard.Solo.Ids.Title.FirstNight);
            var ms = new List<int> { 4, 7, 10, 13 };
            CollectionAssert.AreEqual(new[]
            {
                "title.record.first_night", "title.stage.1.b0", "title.stage.2.b1", "title.stage.3.b2", "title.stage.4.b3", "title.stage.5.b4"
            }, TitleModel.Available(p, ms));
        }

        [Test]
        public void S01_10_5__EquippedStageTitleFollowsStageDrop()
        {
            var store = new FakeProfileStore { Current = MetaProfiles.Five(5, 0, 0, 0, 0) };
            var ms = new List<int> { 4, 7, 10, 13 };
            Assert.IsTrue(TitleModel.Equip(store, "title.stage.1.b1", ms));
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual("title.stage.1.b1", store.Current.Titles.Equipped);
            store.Current.Wand(1).Stage = 3; // 단계가 떨어지면 칭호도 내려간다
            Assert.AreEqual("title.stage.1.b0", TitleModel.Resolve(store.Current, ms));
        }

        [Test]
        public void S08_11_1__EquipRejectsUnownedAndNoneClears()
        {
            var store = new FakeProfileStore { Current = MetaProfiles.Five() };
            var ms = new List<int> { 4, 7, 10, 13 };
            Assert.IsFalse(TitleModel.Equip(store, "title.record.ten_days", ms));
            Assert.AreEqual(0, store.Saves);
            Assert.IsTrue(TitleModel.Equip(store, "title.stage.3.b0", ms));
            Assert.IsTrue(TitleModel.Equip(store, null, ms));
            Assert.IsNull(store.Current.Titles.Equipped);
            Assert.AreEqual(2, store.Saves);
        }

        // ---------------------------------------------------------------- 옷장

        [Test]
        public void S08_TV_197__GridShowsOnlyOwnedNoLockedCells()
        {
            var p = MetaProfiles.Five();
            p.Cosmetics.Owned.Add("cosmetic.headwear.cracked_mirror_shard");
            p.Cosmetics.Owned.Add("cosmetic.cloak.master_lap_blanket");
            var head = WardrobeModel.Cells(p, "headwear");
            Assert.AreEqual(2, head.Count);
            Assert.IsTrue(head[0].IsOriginal);
            Assert.AreEqual("cosmetic.headwear.cracked_mirror_shard", head[1].Id);
            var trail = WardrobeModel.Cells(p, "trail");
            Assert.AreEqual(1, trail.Count, "가진 것이 없는 분류는 '원래 모습' 칸만(D-73 빈 격자)");
            Assert.IsTrue(trail[0].IsOriginal);
            Assert.AreEqual(12, WardrobeModel.Categories.Length);
        }

        [Test]
        public void S08_11_2__WearSavesAndMarksWorn()
        {
            var store = new FakeProfileStore { Current = MetaProfiles.Five() };
            store.Current.Cosmetics.Owned.Add(Wizard.Solo.Ids.Cosmetic.CrackedMirrorShard);
            Assert.IsFalse(WardrobeModel.Wear(store, "headwear", "cosmetic.headwear.not_owned"));
            Assert.AreEqual(0, store.Saves);
            Assert.IsTrue(WardrobeModel.Wear(store, "headwear", Wizard.Solo.Ids.Cosmetic.CrackedMirrorShard));
            Assert.AreEqual(1, store.Saves);
            var cells = WardrobeModel.Cells(store.Current, "headwear");
            Assert.IsFalse(cells[0].Worn);
            Assert.IsTrue(cells[1].Worn);
            Assert.IsTrue(WardrobeModel.Wear(store, "headwear", null));
            Assert.IsNull(WardrobeModel.Worn(store.Current, "headwear"));
            Assert.AreEqual(2, store.Saves);
        }

        [Test]
        public void S09_2_4__CrackedMirrorShardIsBoundFromRecord()
        {
            Assert.IsTrue(WardrobeModel.IsBound("cosmetic.headwear.cracked_mirror_shard"));
            Assert.AreEqual("str.wardrobe.source.record", WardrobeModel.SourceKey("cosmetic.headwear.cracked_mirror_shard"));
            Assert.IsFalse(WardrobeModel.IsBound("cosmetic.cloak.master_lap_blanket"));
        }

        [Test]
        public void S01_10_5__WildStainStageBoundaries()
        {
            var th = WardrobeModel.StainThresholds(TestContent.Db);
            CollectionAssert.AreEqual(new[] { 0, 100, 500, 2000 }, th);
            Assert.AreEqual(1, WardrobeModel.StainStage(0, th));
            Assert.AreEqual(1, WardrobeModel.StainStage(99, th));
            Assert.AreEqual(2, WardrobeModel.StainStage(100, th));
            Assert.AreEqual(2, WardrobeModel.StainStage(499, th));
            Assert.AreEqual(3, WardrobeModel.StainStage(500, th));
            Assert.AreEqual(3, WardrobeModel.StainStage(1999, th));
            Assert.AreEqual(4, WardrobeModel.StainStage(2000, th));
        }

        [Test]
        public void S01_10_5__WildStainNeverGoesBackButCanWearLower()
        {
            var store = new FakeProfileStore { Current = MetaProfiles.Five() };
            var rec = store.Current.Character(2);
            rec.TotalWaves = 50;
            rec.WildStainStageMax = 3;
            rec.WildStainWorn = 3;
            var th = new List<int> { 0, 100, 500, 2000 };
            Assert.AreEqual(3, WardrobeModel.StainMax(rec, th));
            Assert.IsTrue(WardrobeModel.PickStain(store, 2, 1, th), "원래 모습으로 입기");
            Assert.AreEqual(1, rec.WildStainWorn);
            Assert.AreEqual(3, rec.WildStainStageMax);
            Assert.IsFalse(WardrobeModel.PickStain(store, 2, 4, th));
            Assert.AreEqual(1, store.Saves);
        }

        [Test]
        public void S08_D_73__MissingClipFallsBackToStanding()
        {
            System.Func<string, Facing3, bool> has = (c, f) => c != "sit" || f == Facing3.Down;
            Assert.AreEqual("sit", WardrobeModel.ResolveClip(WardrobePose.Sit, Facing3.Down, has));
            Assert.AreEqual("idle", WardrobeModel.ResolveClip(WardrobePose.Sit, Facing3.Right, has));
            Assert.AreEqual("walk", WardrobeModel.ResolveClip(WardrobePose.Walk, Facing3.Up, has));
            Assert.AreEqual(Facing3.Right, WardrobeModel.NextFacing(Facing3.Down));
            Assert.AreEqual(Facing3.Up, WardrobeModel.NextFacing(Facing3.Right));
            Assert.AreEqual(Facing3.Down, WardrobeModel.NextFacing(Facing3.Up));
            Assert.AreEqual(WardrobePose.Idle, WardrobeModel.Toggle(WardrobePose.Walk, WardrobePose.Walk));
        }
    }
}
