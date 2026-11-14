using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Screens.Battle;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using static Wizard.Solo.Tests.ScreensBattle.BattleTestWorlds;

namespace Wizard.Solo.Tests.ScreensBattle
{
    /// <summary>BattleFlowModel 전이표(00 문서 5.5절, 08 요약서 2절, D-21·D-23·D-49·D-58).</summary>
    public sealed class BattleFlowModelTests
    {
        BattleFlowModel _m;
        List<BattleFlowAction> _out;
        HashSet<ScreenId> _open;

        [SetUp]
        public void SetUp()
        {
            _m = new BattleFlowModel { DownedToSettlementSeconds = 2.0f }; // camp.json downed_to_settlement_seconds = 2.0 (D-21)
            _out = new List<BattleFlowAction>();
            _open = new HashSet<ScreenId> { ScreenId.Battle };
        }

        bool IsOpen(ScreenId id) => _open.Contains(id);

        void Feed(SimWorld w, SimEvent e) => _m.OnEvent(w, e, IsOpen, _out);

        bool Has(BattleFlowActionKind k)
        {
            for (int i = 0; i < _out.Count; i++) if (_out[i].Kind == k) return true;
            return false;
        }

        [Test]
        public void S00_FLOW01__PhaseChangedLootPickPushesLootPick()
        {
            var w = Make(RunPhase.LootPick);
            Feed(w, Phase(RunPhase.LootPick, RunPhase.WaveClearing));
            Assert.AreEqual(1, _out.Count);
            Assert.AreEqual(BattleFlowActionKind.PushLootPick, _out[0].Kind);
        }

        [Test]
        public void S00_FLOW02__LootPickAlreadyOpenIsNotPushedAgain()
        {
            _open.Add(ScreenId.LootPick);
            Feed(Make(RunPhase.LootPick), Phase(RunPhase.LootPick, RunPhase.WaveClearing));
            Assert.IsFalse(Has(BattleFlowActionKind.PushLootPick));
        }

        [Test]
        public void S00_FLOW03__LootPickEndsToCampClosesLootPickWithoutOpeningCampAtFireIdle()
        {
            _open.Add(ScreenId.LootPick);
            var w = Make(RunPhase.Camp);
            w.Run.CampSub = CampSubState.FireIdle;
            Feed(w, Phase(RunPhase.Camp, RunPhase.LootPick));
            Assert.IsTrue(Has(BattleFlowActionKind.CloseLootPick));
            Assert.IsFalse(Has(BattleFlowActionKind.PushCamp), "모닥불 옆 J 전에는 야영 화면을 열지 않는다(D-49)");
        }

        [Test]
        public void S00_FLOW04__CampChangedWithCampScreenPushesCamp()
        {
            var w = Make(RunPhase.Camp);
            w.Run.CampSub = CampSubState.CampScreen;
            Feed(w, Ev(SimEventType.CampChanged));
            Assert.AreEqual(1, _out.Count);
            Assert.AreEqual(BattleFlowActionKind.PushCamp, _out[0].Kind);

            _out.Clear();
            _open.Add(ScreenId.Camp);
            Feed(w, Ev(SimEventType.CampChanged));
            Assert.AreEqual(0, _out.Count, "이미 열린 야영 화면은 다시 열지 않는다");
        }

        [Test]
        public void S00_FLOW05__PhaseChangedStartCampWithCampScreenPushesCamp()
        {
            var w = Make(RunPhase.StartCamp);
            w.Run.CampSub = CampSubState.CampScreen;
            Feed(w, Phase(RunPhase.StartCamp, RunPhase.NotStarted));
            Assert.IsTrue(Has(BattleFlowActionKind.PushCamp));
        }

        [Test]
        public void S00_FLOW06__CampSubBackToFireIdleClosesCampAndArtTrie()
        {
            var w = Make(RunPhase.Camp);
            w.Run.CampSub = CampSubState.FireIdle;
            _open.Add(ScreenId.Camp);
            _open.Add(ScreenId.ArtTrie);
            Feed(w, Ev(SimEventType.CampChanged));
            Assert.IsTrue(Has(BattleFlowActionKind.CloseCamp));

            _out.Clear();
            _open.Remove(ScreenId.Camp);
            Feed(w, Ev(SimEventType.CampChanged));
            Assert.IsTrue(Has(BattleFlowActionKind.CloseCamp), "ArtTrie만 남아도 닫는다");
        }

        [Test]
        public void S00_FLOW07__DepartLeavingCampClosesCamp()
        {
            var w = Make(RunPhase.WavePrelude);
            w.Run.CampSub = CampSubState.CampScreen; // 단계가 야영을 벗어났다
            _open.Add(ScreenId.Camp);
            Feed(w, Phase(RunPhase.WavePrelude, RunPhase.Camp));
            Assert.IsTrue(Has(BattleFlowActionKind.CloseCamp));
            Assert.IsFalse(Has(BattleFlowActionKind.PushCamp));
        }

        [Test]
        public void S00_FLOW08__CampChangedWhileCampScreenOpenDoesNothing()
        {
            var w = Make(RunPhase.Camp);
            w.Run.CampSub = CampSubState.CampScreen;
            _open.Add(ScreenId.Camp);
            Feed(w, Ev(SimEventType.CampChanged)); // 회복·조립
            Assert.AreEqual(0, _out.Count);
        }

        [Test]
        public void S01_D21__RunEndedHpZeroShowsDeathLineThenSettlesAfterTwoSeconds()
        {
            var w = Make(RunPhase.RunEnded);
            Feed(w, Ev(SimEventType.RunEnded, (int)RunEndReason.HpZero));
            Assert.IsTrue(Has(BattleFlowActionKind.BeginSettlement));
            Assert.IsTrue(_m.DeathLineVisible);
            Assert.AreEqual(2.0f, _out[0].DelaySeconds, 1e-6f);
            Assert.IsFalse(_m.Advance(1.0f));
            Assert.IsFalse(_m.Advance(0.9f));
            Assert.IsTrue(_m.Advance(0.1f + 1e-4f));
            Assert.IsFalse(_m.Advance(1.0f), "정산 전이는 한 번만");
        }

        [Test]
        public void S08_7_GIVEUP01__RunEndedAbandonSettlesImmediately()
        {
            var w = Make(RunPhase.RunEnded);
            Feed(w, Ev(SimEventType.RunEnded, (int)RunEndReason.Abandon));
            Assert.IsTrue(Has(BattleFlowActionKind.BeginSettlement));
            Assert.IsFalse(_m.DeathLineVisible);
            Assert.AreEqual(0f, _out[0].DelaySeconds);
            Assert.IsTrue(_m.Advance(0f));
        }

        [Test]
        public void S08_7_GIVEUP02__SecondRunEndedIsIgnoredAndPhaseChangesAfterEndDoNothing()
        {
            var w = Make(RunPhase.RunEnded);
            Feed(w, Ev(SimEventType.RunEnded, (int)RunEndReason.Abandon));
            _out.Clear();
            Feed(w, Ev(SimEventType.RunEnded, (int)RunEndReason.Abandon));
            Feed(w, Phase(RunPhase.LootPick, RunPhase.WaveClearing));
            Assert.AreEqual(0, _out.Count);
        }

        [TestCase(5, true)]
        [TestCase(10, true)]
        [TestCase(15, true)]
        [TestCase(20, true)]
        [TestCase(1, false)]
        [TestCase(4, false)]
        [TestCase(6, false)]
        [TestCase(11, false)]
        public void S00_D23__CheckpointSavedOnMultiplesOfFive(int wave, bool saved)
        {
            Feed(Make(RunPhase.WaveClearing), Ev(SimEventType.WaveCleared, wave));
            Assert.AreEqual(saved, Has(BattleFlowActionKind.SaveCheckpoint));
            if (saved) Assert.AreEqual(wave, _out[0].Wave);
        }

        [Test]
        public void S00_D58__LongWaveClearingOpensLootPickOnlyOnPhaseChanged()
        {
            var w = Make(RunPhase.WaveClearing);
            Feed(w, Phase(RunPhase.WaveClearing, RunPhase.WaveCombat));
            Feed(w, Ev(SimEventType.RevengeKill));
            // 복수 처치 WaveClearing 264틱(revenge_clear_ticks) 동안 나오는 이벤트들
            for (int t = 0; t < 264; t++)
            {
                Feed(w, Ev(SimEventType.ScoreChanged, t));
                Feed(w, Ev(SimEventType.DropPicked, 1, slot: 0));
            }
            Feed(w, Ev(SimEventType.WaveCleared, 20));
            Assert.IsFalse(Has(BattleFlowActionKind.PushLootPick));
            w.Run.Phase = RunPhase.LootPick;
            Feed(w, Phase(RunPhase.LootPick, RunPhase.WaveClearing));
            Assert.IsTrue(Has(BattleFlowActionKind.PushLootPick));
        }

        [Test]
        public void S04_11_3__ArtUnlockedRecordsIntCharacterNumber()
        {
            var w = Make(RunPhase.Camp, "char.3");
            Feed(w, Ev(SimEventType.ArtUnlocked, slot: 0, id: "art.fan"));
            Assert.AreEqual(1, _out.Count);
            Assert.AreEqual(BattleFlowActionKind.RecordArtUnlock, _out[0].Kind);
            Assert.AreEqual(3, _out[0].Character);
            Assert.AreEqual("art.fan", _out[0].ArtId);
        }

        [Test]
        public void S00_D20__PauseBlockedDuringReflectionAndCollapse()
        {
            var w = Make(RunPhase.WaveCombat);
            Assert.IsTrue(_m.CanRequestPause(w, true, false));
            Assert.IsFalse(_m.CanRequestPause(w, true, true), "이미 일시정지");
            Assert.IsFalse(_m.CanRequestPause(w, false, false), "판 없음");
            Assert.IsFalse(_m.CanRequestPause(null, true, false));
            w.Players[0].InputLocked = true;
            Assert.IsFalse(_m.CanRequestPause(w, true, false), "되비침·쓰러짐(InputLocked)");
            w.Players[0].InputLocked = false;
            w.Run.Phase = RunPhase.PhantomFinale;
            Assert.IsFalse(_m.CanRequestPause(w, true, false));
            w.Run.Phase = RunPhase.PhantomDowned;
            Assert.IsFalse(_m.CanRequestPause(w, true, false));
            w.Run.Phase = RunPhase.RunEnded;
            Assert.IsFalse(_m.CanRequestPause(w, true, false), "판이 끝남");
            w.Run.Phase = RunPhase.Camp;
            Assert.IsTrue(_m.CanRequestPause(w, true, false), "야영 전장에서도 일시정지는 된다");
        }

        [Test]
        public void S00_D20__PauseBlockedAfterRunEndedEvent()
        {
            var w = Make(RunPhase.WaveCombat);
            Feed(w, Ev(SimEventType.RunEnded, (int)RunEndReason.HpZero));
            Assert.IsFalse(_m.CanRequestPause(w, true, false));
        }
    }
}
