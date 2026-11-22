using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Prep;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensPrep
{
    /// <summary>
    /// 원정 준비 모델(08 요약서 5절, 01 요약서 2.3·4.6절, 00 문서 8.1절 화면 인자 표).
    /// 기대값은 요약서에서 가져왔다. TV-038(봇 결투 입장 DuelEntry: 시작 웨이브 줄 없음·"준비" 버튼)은 솔로 제외라 옮기지 않았다 —
    /// 00 문서 D-51이 봇 상대 결투 연습을 넣지 않기로 했고 원정 준비에 그 입장 길이 없다.
    /// </summary>
    public sealed class ExpeditionPrepModelTests
    {
        FakeProfileStore _store;
        FakeWandService _wands;
        FakeSettlementService _settle;
        GameContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _ctx = PrepFixture.Make(out _store, out _wands, out _settle);
        }

        static long[] RowIds(IReadOnlyList<WandListEntry> rows)
        {
            var a = new long[rows.Count];
            for (int i = 0; i < rows.Count; i++) a[i] = rows[i].Wand.Id;
            return a;
        }

        // ------------------------------------------------------------------ 5.3 지팡이 목록

        [Test]
        public void S08_TV_029__StageDescHeldPinnedTop()
        {
            PrepFixture.AddWand(_store, 1, 11, 7, false);
            PrepFixture.AddWand(_store, 1, 12, 5, true);
            PrepFixture.AddWand(_store, 1, 13, 9, false);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            m.SetSort(WandSort.StageDesc);
            // [+5(든), +9, +7]
            CollectionAssert.AreEqual(new long[] { 12, 13, 11 }, RowIds(m.Rows));
            Assert.IsTrue(m.Rows[0].Equipped);
        }

        [Test]
        public void S08_TV_030__StageAscHeldPinnedTop()
        {
            PrepFixture.AddWand(_store, 1, 11, 7, false);
            PrepFixture.AddWand(_store, 1, 12, 5, true);
            PrepFixture.AddWand(_store, 1, 13, 9, false);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            m.SetSort(WandSort.StageAsc);
            // [+5(든), +7, +9]
            CollectionAssert.AreEqual(new long[] { 12, 11, 13 }, RowIds(m.Rows));
        }

        [Test]
        public void S08_5_3__SortRowsDirectHeldPinnedInEverySort()
        {
            var rows = new List<WandListEntry>
            {
                new WandListEntry { Wand = new WandData { Id = 1, Character = 1, Stage = 9, AcquiredAt = "2026-01-03T00:00:00Z" } },
                new WandListEntry { Wand = new WandData { Id = 2, Character = 1, Stage = 5, AcquiredAt = "2026-01-01T00:00:00Z" }, Equipped = true },
                new WandListEntry { Wand = new WandData { Id = 3, Character = 1, Stage = 7, AcquiredAt = "2026-01-02T00:00:00Z" } },
            };
            CollectionAssert.AreEqual(new long[] { 2, 1, 3 }, RowIds(WandListModel.SortRows(rows, WandSort.StageDesc)));
            CollectionAssert.AreEqual(new long[] { 2, 3, 1 }, RowIds(WandListModel.SortRows(rows, WandSort.StageAsc)));
            // 최근 얻은 순: 01-03(1) → 01-02(3)
            CollectionAssert.AreEqual(new long[] { 2, 1, 3 }, RowIds(WandListModel.SortRows(rows, WandSort.Recent)));
        }

        [Test]
        public void S08_5_3__TieBreakStageThenAcquiredDescThenIdAsc()
        {
            var rows = new List<WandListEntry>
            {
                new WandListEntry { Wand = new WandData { Id = 5, Character = 1, Stage = 3, AcquiredAt = "2026-01-01T00:00:00Z" } },
                new WandListEntry { Wand = new WandData { Id = 4, Character = 1, Stage = 3, AcquiredAt = "2026-01-01T00:00:00Z" } },
                new WandListEntry { Wand = new WandData { Id = 6, Character = 1, Stage = 3, AcquiredAt = "2026-02-01T00:00:00Z" } },
            };
            CollectionAssert.AreEqual(new long[] { 6, 4, 5 }, RowIds(WandListModel.SortRows(rows, WandSort.StageDesc)));
        }

        [Test]
        public void S08_5_3__EnterSelectsHeldWandAndHeldTag()
        {
            PrepFixture.AddWand(_store, 1, 11, 9, false);
            PrepFixture.AddWand(_store, 1, 12, 5, true);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.AreEqual(12, m.WandId);
            Assert.IsTrue(m.SelectedEntry.Equipped);
            Assert.IsFalse(m.CanSetHeld);
            Assert.AreEqual("든 지팡이", TestContent.Names.Get(PrepKeys.HeldTag));
        }

        [Test]
        public void S08_5_3__PickOtherWandEnablesSetHeldAndEquipCallsService()
        {
            PrepFixture.AddWand(_store, 1, 11, 9, false);
            PrepFixture.AddWand(_store, 1, 12, 5, true);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.IsTrue(m.SelectWand(11));
            Assert.IsTrue(m.CanSetHeld);
            Assert.AreEqual(ErrorCode.None, m.SetHeld());
            Assert.AreEqual(1, _wands.EquipCalls);
            Assert.AreEqual(11, _store.Data.Character(1).EquippedWandId);
            Assert.AreEqual(11, m.WandId);
            Assert.IsTrue(m.Rows[0].Equipped);
            Assert.AreEqual(11, m.Rows[0].Wand.Id);
        }

        [Test]
        public void S08_5_3__OnlyThatCharactersWands()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            PrepFixture.AddWand(_store, 2, 21, 0, true);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            CollectionAssert.AreEqual(new long[] { 11 }, RowIds(m.Rows));
            Assert.IsFalse(m.SelectWand(21));
        }

        // ------------------------------------------------------------------ 캐릭터 (D-56)

        [Test]
        public void S08_5_1__EnterUsesLastCharacter()
        {
            PrepFixture.AddWand(_store, 3, 31, 0, true);
            _store.Data.LastCharacter = 3;
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.AreEqual(3, m.Character);
            Assert.AreEqual(31, m.WandId);
        }

        [Test]
        public void S08_5_1__SelectCharacterWritesLastCharacterAndSaves()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            PrepFixture.AddWand(_store, 4, 41, 2, true);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            int before = _store.SaveCount;
            Assert.IsTrue(m.SelectCharacter(4));
            Assert.AreEqual(4, _store.Data.LastCharacter);
            Assert.AreEqual(before + 1, _store.SaveCount);
            Assert.AreEqual(41, m.WandId);
        }

        [Test]
        public void S08_5_1__AllFiveSelectableFromStartAndOutOfRangeRejected()
        {
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            for (int c = 1; c <= 5; c++) Assert.IsTrue(m.SelectCharacter(c), "char " + c);
            Assert.IsFalse(m.SelectCharacter(0));
            Assert.IsFalse(m.SelectCharacter(6));
            Assert.AreEqual(5, m.Character);
        }

        // ------------------------------------------------------------------ 5.4 CanDepart

        [Test]
        public void S08_TV_031__CoreEmptyBlocksDepart()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true, null);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var c = m.CanDepart();
            Assert.IsFalse(c.Ok);
            Assert.IsTrue(c.CoreEmpty, "[조립 바꾸기]로 커서");
            Assert.AreEqual(PrepKeys.BlockedCoreEmpty, c.ReasonKey);
            Assert.AreEqual("코어에 투사체 블록을 끼워야 출발할 수 있다", TestContent.Names.Get(c.ReasonKey));
            var r = m.Depart(1);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(0, _wands.BuildCalls, "막히면 BuildRunConfig를 부르지 않는다");
        }

        [Test]
        public void S08_TV_032__ShopWandEmptyLoadoutSameAsCoreEmpty()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            var shop = PrepFixture.AddWand(_store, 1, 12, 0, false, null);
            shop.Origin = "Shop";
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.IsTrue(m.CanDepart().Ok);
            m.SelectWand(12);
            var c = m.CanDepart();
            Assert.IsFalse(c.Ok);
            Assert.IsTrue(c.CoreEmpty);
            Assert.AreEqual(PrepKeys.BlockedCoreEmpty, c.ReasonKey);
        }

        [Test]
        public void S08_5_4__CoreEmptyDetectedEvenIfServiceFlagFalse()
        {
            // 서비스의 CoreEmpty가 false여도 조립의 코어를 직접 본다.
            _wands.ReportCoreEmpty = false;
            PrepFixture.AddWand(_store, 1, 11, 0, true, "");
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.IsTrue(m.CanDepart().CoreEmpty);
        }

        [Test]
        public void S08_5_4__OnExpeditionLockBlocks()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            _wands.LockedIds.Add(11);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var c = m.CanDepart();
            Assert.IsFalse(c.Ok);
            Assert.IsFalse(c.CoreEmpty);
            Assert.AreEqual(PrepKeys.BlockedOnExpedition, c.ReasonKey);
            Assert.AreEqual("원정 중인 지팡이다", TestContent.Names.Get(c.ReasonKey));
        }

        [Test]
        public void S08_5_4__CoreCheckedBeforeLock()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true, null);
            _wands.LockedIds.Add(11);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.AreEqual(PrepKeys.BlockedCoreEmpty, m.CanDepart().ReasonKey);
        }

        [Test]
        public void S08_5_4__OkWhenCoreFilledAndUnlocked()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var c = m.CanDepart();
            Assert.IsTrue(c.Ok);
            Assert.IsNull(c.ReasonKey);
        }

        // ------------------------------------------------------------------ 5.5 시작 웨이브 / 01 T080~T085

        ExpeditionPrepModel WithBest(int best)
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            _store.Data.Character(1).BestWave = best;
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            return m;
        }

        int[] FirstWaves(ExpeditionPrepModel m)
        {
            var a = new int[m.SkipOptions.Count];
            for (int i = 0; i < a.Length; i++) { m.SelectSkip(m.SkipOptions[i]); a[i] = m.StartWave; }
            return a;
        }

        [Test]
        public void S08_TV_033__Best34ShowsZeroTenTwenty()
        {
            var m = WithBest(34);
            Assert.AreEqual(34, _settle.LastBestWave);
            CollectionAssert.AreEqual(new[] { 0, 10, 20 }, m.SkipOptions);
            Assert.IsTrue(m.ShowStartWaveRow);
            CollectionAssert.AreEqual(new[] { 1, 11, 21 }, FirstWaves(m));
        }

        [Test]
        public void S08_TV_034__Best14HidesRow()
        {
            var m = WithBest(14);
            CollectionAssert.AreEqual(new[] { 0 }, m.SkipOptions);
            Assert.IsFalse(m.ShowStartWaveRow);
            Assert.AreEqual(1, m.StartWave);
        }

        [Test]
        public void S08_TV_035__Best15ShowsZeroTen()
        {
            var m = WithBest(15);
            CollectionAssert.AreEqual(new[] { 0, 10 }, m.SkipOptions);
            Assert.IsTrue(m.ShowStartWaveRow);
        }

        [Test]
        public void S08_TV_036__Best28And29()
        {
            CollectionAssert.AreEqual(new[] { 0, 10 }, WithBest(28).SkipOptions);
            SetUp();
            CollectionAssert.AreEqual(new[] { 0, 10, 20 }, WithBest(29).SkipOptions);
        }

        [Test] public void S01_T080__Best34() { CollectionAssert.AreEqual(new[] { 0, 10, 20 }, WithBest(34).SkipOptions); }
        [Test] public void S01_T081__Best14() { CollectionAssert.AreEqual(new[] { 0 }, WithBest(14).SkipOptions); }
        [Test] public void S01_T082__Best15() { CollectionAssert.AreEqual(new[] { 0, 10 }, WithBest(15).SkipOptions); }
        [Test] public void S01_T083__Best29() { CollectionAssert.AreEqual(new[] { 0, 10, 20 }, WithBest(29).SkipOptions); }

        /// <summary>T084: 최고 40에서 건너뜀 20은 허용된다. 시즌 최고·ranked_eligible_start(순위 미반영)는 솔로에 순위가 없어 옮기지 않았다.</summary>
        [Test]
        public void S01_T084__Best40Skip20AllowedDepartsAtWave21()
        {
            var m = WithBest(40);
            Assert.IsTrue(m.SelectSkip(20));
            var r = m.Depart(7);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(21, _wands.LastStartWave);
        }

        [Test]
        public void S01_T085__Skip15Rejected()
        {
            var m = WithBest(40);
            Assert.IsFalse(m.SelectSkip(15));
            Assert.AreEqual(0, m.Skip);
        }

        [Test]
        public void S08_5_5__SelectedSkipGivesFirstWaveSkipPlusOne()
        {
            var m = WithBest(34);
            Assert.IsTrue(m.SelectSkip(10));
            Assert.IsTrue(m.Depart(1).Ok);
            Assert.AreEqual(11, _wands.LastStartWave);
            Assert.IsTrue(m.StepSkip(1));
            Assert.AreEqual(20, m.Skip);
            Assert.IsFalse(m.StepSkip(1), "끝에서 멈춘다");
            Assert.IsTrue(m.StepSkip(-1));
            Assert.IsTrue(m.StepSkip(-1));
            Assert.IsFalse(m.StepSkip(-1));
            Assert.AreEqual(0, m.Skip);
        }

        [Test]
        public void S08_TV_037__SkippedScoreSums()
        {
            var m = WithBest(80);
            Assert.AreEqual(8398, m.SkippedScore(10));
            Assert.AreEqual(28859, m.SkippedScore(20));
            Assert.AreEqual(64764, m.SkippedScore(30));
            Assert.AreEqual(120409, m.SkippedScore(40));
            Assert.AreEqual(195829, m.SkippedScore(50));
            Assert.AreEqual(0, m.SkippedScore(0));
        }

        [Test]
        public void S08_5_5__ScoreNoteText()
        {
            string s = TestContent.Names.Format(PrepKeys.StartWaveScoreNote, "score", TestContent.Names.Number(8398));
            Assert.AreEqual("건너뛰면 점수 8,398을 받고 보상은 없다", s);
            Assert.AreEqual("웨이브 11에서 출발", TestContent.Names.Format(PrepKeys.FormatStartWave, "start", 11));
        }

        // ------------------------------------------------------------------ 출발 (계약 벡터: 00 8.1절)

        [Test]
        public void S00_8_1__DepartPassesIntCharacterWandAndSeed()
        {
            PrepFixture.AddWand(_store, 2, 21, 3, true);
            _store.Data.LastCharacter = 2;
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var r = m.Depart(123456789UL);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(1, _wands.BuildCalls);
            Assert.AreEqual(2, _wands.LastCharacter, "캐릭터는 int 1~5");
            Assert.AreEqual(21L, _wands.LastWandId);
            Assert.AreEqual(1, _wands.LastStartWave);
            Assert.AreEqual(123456789UL, _wands.LastSeed);
        }

        [Test]
        public void S00_8_1__DepartAfterTutorialGoesLoadingThenBattleWithRunConfig()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            _store.Data.Flags.TutorialDummyDone = true;
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var r = m.Depart(5);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(ScreenId.Loading, r.Next);
            Assert.AreEqual(ScreenId.ExpeditionPrep, r.NextArgs.Entry);
            Assert.IsTrue(r.NextArgs.Values[ScreenArgKeys.NextScreen] is ScreenId);
            Assert.AreEqual(ScreenId.Battle, r.NextArgs.Get(ScreenArgKeys.NextScreen, ScreenId.None));
            var battle = r.NextArgs.Get<ScreenArgs>(ScreenArgKeys.NextArgs);
            Assert.IsNotNull(battle);
            Assert.IsTrue(battle.Values[ScreenArgKeys.RunConfig] is RunConfig);
            var cfg = battle.Get<RunConfig>(ScreenArgKeys.RunConfig);
            Assert.AreSame(r.Config, cfg);
            Assert.AreEqual(RunMode.Endless, cfg.Mode);
        }

        [Test]
        public void S00_8_1__DepartBeforeTutorialGoesTutorialWithRunConfig()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            _store.Data.Flags.TutorialDummyDone = false;
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var r = m.Depart(5);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(ScreenId.Tutorial, r.Next);
            Assert.AreEqual(ScreenId.ExpeditionPrep, r.NextArgs.Entry);
            Assert.IsTrue(r.NextArgs.Values[ScreenArgKeys.RunConfig] is RunConfig);
            Assert.AreSame(r.Config, r.NextArgs.Get<RunConfig>(ScreenArgKeys.RunConfig));
        }

        [Test]
        public void S00_8_1__BuildRunConfigFailureShowsErrorKey()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            _wands.Fail = ErrorCode.InvalidLoadout;
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var r = m.Depart(5);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(ErrorCode.InvalidLoadout, r.Error);
            Assert.AreEqual("error.invalid_loadout", r.ErrorKey);
            Assert.AreEqual("조립이 올바르지 않다.", TestContent.Names.Get(r.ErrorKey));
        }

        [Test]
        public void S00_8_1__ErrorKeysSnakeCase()
        {
            Assert.AreEqual("error.core_empty", PrepErrorKeys.Of(ErrorCode.CoreEmpty));
            Assert.AreEqual("error.wand_locked", PrepErrorKeys.Of(ErrorCode.WandLocked));
            Assert.AreEqual("error.not_found", PrepErrorKeys.Of(ErrorCode.NotFound));
            Assert.IsNull(PrepErrorKeys.Of(ErrorCode.None));
        }

        [Test]
        public void S00_8_1__ChangeLoadoutArgs()
        {
            PrepFixture.AddWand(_store, 1, 11, 0, true);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            var a = m.ChangeLoadoutArgs();
            Assert.AreEqual(ScreenId.ExpeditionPrep, a.Entry);
            Assert.IsTrue(a.Values[ScreenArgKeys.CampMode] is CampMode);
            Assert.AreEqual(CampMode.LoadoutEdit, a.Get(ScreenArgKeys.CampMode, CampMode.Camp));
            Assert.IsTrue(a.Values[ScreenArgKeys.WandId] is long, "WandId는 long");
            Assert.AreEqual(11L, a.Get(ScreenArgKeys.WandId, 0L));
        }

        [Test]
        public void S00_8_1__ReloadAfterLoadoutEditKeepsWandAndUnblocks()
        {
            var w = PrepFixture.AddWand(_store, 1, 11, 0, true, null);
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.IsTrue(m.CanDepart().CoreEmpty);
            w.Loadout.Core = Ids.Block.MagicBolt;   // Camp(LoadoutEdit)가 SetLoadout으로 고쳤다
            m.Reload();
            Assert.AreEqual(11, m.WandId);
            Assert.IsTrue(m.CanDepart().Ok);
        }

        // ------------------------------------------------------------------ 복수 추적기 한 줄 (13.2절)

        [Test]
        public void S08_13_2__TrackerLineHiddenBeforeMeeting()
        {
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.IsFalse(m.ShowTrackerLine);
        }

        [Test]
        public void S08_TV_237__TrackerLineWhilePursuing()
        {
            _store.Data.Flags.PhantomMet = true;
            _store.Data.Character(2).BestWave = 14;
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.IsTrue(m.ShowTrackerLine);
            Assert.AreEqual(14, m.Tracker.Progress);
            Assert.AreEqual("복수 · 최고 웨이브 14 / 19", TestContent.Names.Format(PrepKeys.RevengeShort, "n", m.Tracker.Progress));
        }

        [Test]
        public void S08_13_2__TrackerLineHiddenAfterDone()
        {
            _store.Data.Flags.PhantomMet = true;
            _store.Data.Flags.RevengeDoneAt = "2026-11-01";
            var m = new ExpeditionPrepModel(_ctx);
            m.Enter();
            Assert.IsFalse(m.ShowTrackerLine);
        }

        // ------------------------------------------------------------------ 서비스 없음

        [Test]
        public void S08_5__NullServicesEmptyStateNoThrow()
        {
            var m = new ExpeditionPrepModel(new GameContext());
            Assert.DoesNotThrow(() => m.Enter());
            Assert.AreEqual(1, m.Character);
            Assert.AreEqual(0, m.Rows.Count);
            Assert.IsFalse(m.ShowStartWaveRow);
            Assert.IsFalse(m.ShowTrackerLine);
            Assert.IsFalse(m.CanDepart().Ok);
            Assert.IsFalse(m.Depart(1).Ok);
            Assert.IsNull(m.ChangeLoadoutArgs());
            Assert.AreEqual(ErrorCode.NotFound, m.SetHeld());
            Assert.DoesNotThrow(() => m.SelectCharacter(3));
            Assert.AreEqual(0, m.SkippedScore(10));
            Assert.DoesNotThrow(() => new ExpeditionPrepModel(null).Enter());
        }
    }
}
