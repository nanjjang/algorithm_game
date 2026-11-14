using NUnit.Framework;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Battle;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using static Wizard.Solo.Tests.ScreensBattle.BattleTestWorlds;

namespace Wizard.Solo.Tests.ScreensBattle
{
    /// <summary>계약 벡터: 가짜 IGameSession·IScreenManager로 Battle·Pause의 세션 호출을 본다(00 문서 4.1절, D-60).</summary>
    public sealed class BattleControllerTests
    {
        GameContext _ctx;
        BattleFakeSession _session;
        BattleFakeScreens _screens;
        BattleFakeSettlement _settlement;
        CountingArtModule _arts;
        MarkerRunModule _run;
        SimWorld _main;
        BattleController _c;

        [SetUp]
        public void SetUp()
        {
            _arts = new CountingArtModule();
            _run = new MarkerRunModule();
            var mods = SimModuleSet.Stubs();
            mods.Arts = _arts;
            mods.Run = _run;
            _main = Make(RunPhase.WaveCombat, "char.2", mods);
            _session = new BattleFakeSession { Main = _main };
            _screens = new BattleFakeScreens();
            _screens.Stack.Add(ScreenId.Battle);
            _settlement = new BattleFakeSettlement();
            _ctx = new GameContext { Session = _session, Screens = _screens, Settlement = _settlement, Names = new EchoNames() };
            _c = new BattleController(_ctx);
            _c.Flow.DownedToSettlementSeconds = 2.0f;
        }

        [Test]
        public void S00_4_1_ENTER01__EnterStartsRunAndSetsInputScreenExitClears()
        {
            var cfg = new RunConfig { Mode = RunMode.Endless, CharId = "char.2" };
            _c.Enter(ScreenArgs.From(ScreenId.Loading).With(ScreenArgKeys.RunConfig, cfg));
            Assert.AreEqual(1, _session.Started.Count);
            Assert.AreSame(cfg, _session.Started[0]);
            Assert.AreEqual(ScreenId.Battle, _session.InputScreen);
            Assert.AreEqual(1, _session.SimEventSubscribers);
            Assert.AreEqual(1, _session.PauseSubscribers);
            _c.Exit();
            Assert.AreEqual(ScreenId.None, _session.InputScreen);
            Assert.AreEqual(0, _session.SimEventSubscribers);
            Assert.AreEqual(0, _session.PauseSubscribers);
        }

        [Test]
        public void S00_4_1_PAUSE01__EscAndPauseRequestedSameFramePushPauseOnce()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            Assert.IsTrue(_c.RequestPause());   // Esc (OnBack)
            _session.RaisePause();              // 재설정한 일시정지 키(같은 프레임)
            Assert.AreEqual(1, _session.PausedSetTrueCount);
            Assert.AreEqual(1, _screens.CountPushed(ScreenId.Pause));
            Assert.IsTrue(_session.Paused);
        }

        [Test]
        public void S00_4_1_PAUSE02__PauseRequestedAloneOpensPause()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _session.RaisePause();
            Assert.AreEqual(1, _screens.CountPushed(ScreenId.Pause));
            Assert.AreEqual(ScreenId.Pause, _screens.Top);
        }

        [Test]
        public void S00_4_1_PAUSE03__ScreenDoesNotCallArtsOnPauseEntry()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _c.RequestPause();
            Assert.AreEqual(0, _arts.ResetCalls, "커맨드 초기화는 세션(Paused set)이 한다");
        }

        [Test]
        public void S00_D20__EscIgnoredDuringFirstEncounter()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _main.Players[0].InputLocked = true;
            _main.Run.Phase = RunPhase.PhantomFinale;
            var screen = new BattleScreen();
            Assert.IsTrue(screen.OnBack(), "OnBack은 늘 소비한다");
            Assert.IsFalse(_c.RequestPause());
            _session.RaisePause();
            Assert.AreEqual(0, _session.PausedSetTrueCount);
            Assert.AreEqual(0, _screens.CountPushed(ScreenId.Pause));
        }

        [Test]
        public void S00_4_1_PAUSE04__PauseIgnoredWhenAnotherScreenIsOnTop()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _screens.Stack.Add(ScreenId.LootPick);
            Assert.IsFalse(_c.RequestPause());
            Assert.AreEqual(0, _session.PausedSetTrueCount);
        }

        [Test]
        public void S08_7_PAUSE05__EscInPauseResumesAndPops()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _c.RequestPause();
            var p = new PauseController(_ctx, () => _screens.Pop());
            p.Resume(); // Esc = [계속]
            Assert.IsFalse(_session.Paused);
            Assert.AreEqual(1, _session.PausedSetFalseCount);
            Assert.AreEqual(1, _screens.PopCount);
            Assert.AreEqual(ScreenId.Battle, _screens.Top);
        }

        [Test]
        public void S08_7_GIVEUP03__GiveUpConfirmEnqueuesGiveUpThenSettles()
        {
            _session.Summary = new RunSummary { RunId = "run.last" };
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _c.RequestPause();
            var p = new PauseController(_ctx, () => _screens.Pop());
            p.Activate(PauseItem.GiveUp);
            Assert.AreEqual(1, _screens.ConfirmCount);
            Assert.AreEqual("str.pause.give_up_confirm", _screens.LastConfirmBody);
            Assert.IsTrue(_screens.LastConfirmCaution);
            Assert.AreEqual(0, _main.Commands.Count, "확인 전에는 명령을 넣지 않는다");
            _screens.LastConfirm(true);
            Assert.AreEqual(1, _main.Commands.Count);
            Assert.AreEqual(RunCommandKind.GiveUp, _main.Commands[0].Kind);
            Assert.IsFalse(_session.Paused);
            Assert.AreEqual(ScreenId.Battle, _screens.Top);

            // 다음 틱: Run이 RunEnded(Abandon)를 낸다 → 정산
            _main.Run.Phase = RunPhase.RunEnded;
            _session.Raise(_main, Ev(SimEventType.RunEnded, (int)RunEndReason.Abandon));
            _c.Tick(0.016f);
            Assert.AreEqual(1, _screens.Replaced.Count);
            Assert.AreEqual(ScreenId.Settlement, _screens.Replaced[0].Key);
            Assert.AreSame(_session.Summary, _screens.Replaced[0].Value.Get<RunSummary>(ScreenArgKeys.RunSummary));
        }

        [Test]
        public void S08_7_GIVEUP04__ToMenuIsSameAsGiveUpAndNoClosesNothing()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _c.RequestPause();
            var p = new PauseController(_ctx, () => _screens.Pop());
            p.Activate(PauseItem.ToMenu);
            Assert.AreEqual(1, _screens.ConfirmCount);
            Assert.AreEqual("str.pause.give_up_confirm", _screens.LastConfirmBody);
            _screens.LastConfirm(false);
            Assert.AreEqual(0, _main.Commands.Count);
            Assert.IsTrue(_session.Paused);
            Assert.AreEqual(ScreenId.Pause, _screens.Top);
            p.Activate(PauseItem.ToMenu);
            _screens.LastConfirm(true);
            Assert.AreEqual(RunCommandKind.GiveUp, _main.Commands[0].Kind);
        }

        [Test]
        public void S01_D21__HpZeroSettlesAfterDownedSeconds()
        {
            _session.Summary = new RunSummary { RunId = "run.dead" };
            bool death = false;
            _c.DeathLineChanged = on => death = on;
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _session.Raise(_main, Ev(SimEventType.RunEnded, (int)RunEndReason.HpZero));
            Assert.IsTrue(death);
            _c.Tick(1.5f);
            Assert.AreEqual(0, _screens.Replaced.Count);
            _c.Tick(0.6f);
            Assert.AreEqual(1, _screens.Replaced.Count);
            Assert.AreEqual(ScreenId.Settlement, _screens.Replaced[0].Key);
        }

        [Test]
        public void S00_4_1_SUB01__SubRunEventsCauseNoTransitions()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            var sub = Make(RunPhase.Practice);
            _session.Sub = sub;
            _session.Raise(sub, Phase(RunPhase.LootPick, RunPhase.WaveClearing));
            _session.Raise(sub, Ev(SimEventType.RunEnded, (int)RunEndReason.Abandon));
            _session.Raise(sub, Ev(SimEventType.WaveCleared, 5));
            _session.Raise(sub, Ev(SimEventType.ArtUnlocked, slot: 0, id: "art.fan"));
            _c.Tick(10f);
            Assert.AreEqual(0, _screens.Pushed.Count);
            Assert.AreEqual(0, _screens.Replaced.Count);
            Assert.AreEqual(0, _settlement.Checkpoints.Count);
            Assert.AreEqual(0, _settlement.ArtUnlocks.Count);
            Assert.IsFalse(_c.Flow.Ended);
        }

        [Test]
        public void S04_11_3__ArtUnlockedCallsRecordArtUnlockWithIntCharacter()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _session.Raise(_main, Ev(SimEventType.ArtUnlocked, slot: 0, id: "art.wings"));
            Assert.AreEqual(1, _settlement.ArtUnlocks.Count);
            Assert.AreEqual(2, _settlement.ArtUnlocks[0].Key);
            Assert.AreEqual("art.wings", _settlement.ArtUnlocks[0].Value);
        }

        [Test]
        public void S00_D23__CheckpointSavesBuildSummaryAtWaveTen()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _main.Run.Wave = 10;
            _session.Raise(_main, Ev(SimEventType.WaveCleared, 9));
            Assert.AreEqual(0, _settlement.Checkpoints.Count);
            _session.Raise(_main, Ev(SimEventType.WaveCleared, 10));
            Assert.AreEqual(1, _settlement.Checkpoints.Count);
            Assert.AreEqual("run.test", _settlement.Checkpoints[0].RunId);
            Assert.AreEqual(1, _run.BuildCalls);
        }

        [Test]
        public void S00_5_5_CAMP01__CampOpenedWithCampModeAndClosedOnDepart()
        {
            _c.Enter(ScreenArgs.From(ScreenId.Loading));
            _main.Run.Phase = RunPhase.LootPick;
            _session.Raise(_main, Phase(RunPhase.LootPick, RunPhase.WaveClearing));
            Assert.AreEqual(ScreenId.LootPick, _screens.Top);

            _main.Run.Phase = RunPhase.Camp;
            _main.Run.CampSub = CampSubState.FireIdle;
            _session.Raise(_main, Phase(RunPhase.Camp, RunPhase.LootPick));
            Assert.AreEqual(ScreenId.Battle, _screens.Top, "LootPick 단계가 끝나면 PopTo(Battle)");
            Assert.AreEqual(0, _screens.CountPushed(ScreenId.Camp));

            _main.Run.CampSub = CampSubState.CampScreen; // Run이 모닥불 옆 J를 판정했다
            _session.Raise(_main, Ev(SimEventType.CampChanged));
            Assert.AreEqual(ScreenId.Camp, _screens.Top);
            var campArgs = _screens.Pushed[_screens.Pushed.Count - 1].Value;
            Assert.AreEqual(CampMode.Camp, campArgs.Get(ScreenArgKeys.CampMode, CampMode.Library));
            Assert.AreEqual(0, _main.Commands.Count, "화면은 CampOpen 명령을 넣지 않는다");

            _screens.Stack.Add(ScreenId.ArtTrie);
            _main.Run.Phase = RunPhase.WavePrelude;
            _session.Raise(_main, Phase(RunPhase.WavePrelude, RunPhase.Camp));
            Assert.AreEqual(ScreenId.Battle, _screens.Top);
            Assert.IsFalse(_screens.IsOpen(ScreenId.Camp));
            Assert.IsFalse(_screens.IsOpen(ScreenId.ArtTrie));
        }

        [Test]
        public void S08_7_PAUSE06__PauseMenuHasFiveItemsInOrder()
        {
            var items = PauseMenuModel.Items;
            CollectionAssert.AreEqual(new[] { PauseItem.Resume, PauseItem.Settings, PauseItem.Controls, PauseItem.GiveUp, PauseItem.ToMenu }, items);
            Assert.AreEqual("str.pause.resume", PauseMenuModel.LabelKey(PauseItem.Resume));
            Assert.AreEqual("str.pause.settings", PauseMenuModel.LabelKey(PauseItem.Settings));
            Assert.AreEqual("str.pause.controls", PauseMenuModel.LabelKey(PauseItem.Controls));
            Assert.AreEqual("str.pause.give_up", PauseMenuModel.LabelKey(PauseItem.GiveUp));
            Assert.AreEqual("str.pause.to_menu", PauseMenuModel.LabelKey(PauseItem.ToMenu));
            Assert.IsTrue(PauseMenuModel.IsCaution(PauseItem.GiveUp));
            Assert.IsFalse(PauseMenuModel.IsCaution(PauseItem.Resume));
            Assert.IsTrue(PauseMenuModel.NeedsConfirm(PauseItem.GiveUp));
            Assert.IsTrue(PauseMenuModel.NeedsConfirm(PauseItem.ToMenu));
            Assert.IsFalse(PauseMenuModel.NeedsConfirm(PauseItem.Settings));
        }

        [Test]
        public void S08_7_PAUSE07__SettingsAndControlsPushTheirScreens()
        {
            var p = new PauseController(_ctx, () => _screens.Pop());
            p.Activate(PauseItem.Settings);
            p.Activate(PauseItem.Controls);
            Assert.AreEqual(1, _screens.CountPushed(ScreenId.Settings));
            Assert.AreEqual(1, _screens.CountPushed(ScreenId.ControlsView));
        }

        [Test]
        public void S08_7_SUMMARY01__PauseTotalUsesPausedSeconds()
        {
            var names = new EchoNames();
            Assert.AreEqual("str.pause.pause_total(time=str.format.seconds(n=42))", PauseSummaryModel.PauseTotalText(names, 42.9f));
            Assert.AreEqual("str.pause.pause_total(time=str.pause.time.min_sec(m=1,s=15))", PauseSummaryModel.PauseTotalText(names, 75.4f));
            Assert.AreEqual("str.pause.pause_total(time=str.format.seconds(n=0))", PauseSummaryModel.PauseTotalText(names, 0f));
        }

        [Test]
        public void S08_7_SUMMARY02__SummaryReadsCharacterWandLoadoutScore()
        {
            _main.Run.Score = 1234;
            var p = _main.Players[0];
            p.Wand.Stage = 7;
            p.Loadout.Core = new SlotItem { BlockId = "block.fireball" };
            p.Loadout.Links = new[] { new SlotItem { BlockId = "block.magic_bolt" }, new SlotItem() };
            var s = PauseSummaryModel.Build(_main, 61f);
            Assert.AreEqual("char.2", s.CharId);
            Assert.AreEqual(2, s.CharNumber);
            Assert.AreEqual(7, s.WandStage);
            Assert.AreEqual("block.fireball", s.CoreBlockId);
            CollectionAssert.AreEqual(new[] { "block.magic_bolt", null }, s.LinkBlockIds);
            Assert.AreEqual(1234, s.Score);
            Assert.AreEqual(61, s.PausedWholeSeconds);
        }
    }

    /// <summary>조작 보기 표(08 요약서 7절 "지금 키 배치 그대로", 02 요약서 3.1절, D-11).</summary>
    public sealed class ControlsViewModelTests
    {
        [Test]
        public void S08_7_CONTROLS01__DefaultLayoutRows()
        {
            var s = new SettingsData();
            KeyLayouts.Apply(s, OptionValues.Default);
            var rows = ControlsViewModel.Build(s);
            Assert.AreEqual(9, rows.Count);
            Assert.AreEqual("str.key_rebind.action.move_up", rows[0].ActionNameKey);
            CollectionAssert.AreEqual(new[] { Key.W }, rows[0].Primary);
            CollectionAssert.AreEqual(new[] { Key.UpArrow }, rows[0].Secondary);
            Assert.AreEqual(LogicalKey.Fire, rows[4].Action);
            CollectionAssert.AreEqual(new[] { Key.J }, rows[4].Primary);
            CollectionAssert.AreEqual(new[] { Key.Z }, rows[4].Secondary);
            Assert.AreEqual("str.key_rebind.action.art", rows[5].ActionNameKey);
            CollectionAssert.AreEqual(new[] { Key.K }, rows[5].Primary);
            CollectionAssert.AreEqual(new[] { Key.LeftShift }, rows[6].Primary);
            CollectionAssert.AreEqual(new[] { Key.Space }, rows[7].Primary);
            var pause = rows[8];
            Assert.AreEqual("str.key_rebind.action.pause", pause.ActionNameKey);
            Assert.IsTrue(pause.FixedEsc);
            CollectionAssert.AreEqual(new[] { Key.Escape }, pause.Primary);
            Assert.AreEqual(0, pause.Secondary.Length);
        }

        [Test]
        public void S08_7_CONTROLS02__AlternateLayoutHasNoSecondary()
        {
            var s = new SettingsData();
            KeyLayouts.Apply(s, OptionValues.Alternate);
            var rows = ControlsViewModel.Build(s);
            CollectionAssert.AreEqual(new[] { Key.UpArrow }, rows[0].Primary);
            Assert.AreEqual(0, rows[0].Secondary.Length);
            CollectionAssert.AreEqual(new[] { Key.Z }, rows[4].Primary);
            CollectionAssert.AreEqual(new[] { Key.V }, rows[7].Primary);
        }

        [Test]
        public void S08_7_CONTROLS03__CustomRebindShowsCurrentKeysAndPauseKeyAfterEsc()
        {
            var s = new SettingsData();
            KeyLayouts.Apply(s, OptionValues.Default);
            s.KeyPrimary[(int)LogicalKey.Fire] = (int)Key.L;
            s.KeyPrimary[(int)LogicalKey.Pause] = (int)Key.P;
            var rows = ControlsViewModel.Build(s);
            CollectionAssert.AreEqual(new[] { Key.L }, rows[4].Primary);
            CollectionAssert.AreEqual(new[] { Key.Escape }, rows[8].Primary);
            CollectionAssert.AreEqual(new[] { Key.P }, rows[8].Secondary);
        }

        [Test]
        public void S08_7_CONTROLS04__NoSettingsFallsBackToDefaultTable()
        {
            var rows = ControlsViewModel.Build(null);
            CollectionAssert.AreEqual(new[] { Key.W }, rows[0].Primary);
            CollectionAssert.AreEqual(new[] { Key.Escape }, rows[8].Primary);
        }
    }
}
