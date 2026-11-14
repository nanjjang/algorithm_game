using NUnit.Framework;
using Wizard.Solo.App;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.App
{
    /// <summary>
    /// 실행기의 순수 진행부(00 문서 4절·4.1절)를 스텁 모듈 세계로 확인한다.
    /// 스텁 PlayerModule은 입력 줄을 꺼내지 않으므로 w.InputQueues[0]에 세계가 받은 입력이 그대로 쌓인다.
    /// </summary>
    public class SessionCoreTests
    {
        static RunConfig Cfg(RunMode mode = RunMode.Endless) => new RunConfig { Mode = mode, Seed = 7 };

        [Test]
        public void S01_T_F19__PauseResetsCommandOncePerActivePlayer()
        {
            var rig = new SessionRig { ActiveSlots = new[] { 0, 2 } };
            rig.Session.StartRun(Cfg());
            rig.Frames(3);
            var arts = rig.Arts;

            rig.Session.Paused = true;
            Assert.AreEqual(1, arts.Count(0, CommandResetReason.Pause));
            Assert.AreEqual(1, arts.Count(2, CommandResetReason.Pause));
            Assert.AreEqual(0, arts.Count(1, CommandResetReason.Pause), "inactive slot untouched");
            Assert.AreEqual(2, arts.Resets.Count);

            rig.Session.Paused = true; // 같은 값 = 진입이 아니다
            Assert.AreEqual(2, arts.Resets.Count);

            rig.Session.Paused = false;
            Assert.AreEqual(2, arts.Resets.Count, "unpause does not reset the command parser");
            rig.Session.Paused = true;
            Assert.AreEqual(2, arts.Count(0, CommandResetReason.Pause));
        }

        [Test]
        public void Paused_NoTicks_PausedSecondsAccumulate_ResetOnStartRun()
        {
            var rig = new SessionRig();
            rig.Session.StartRun(Cfg());
            rig.Frames(5);
            var w = rig.Session.World;
            Assert.AreEqual(5, w.Tick);
            rig.Session.Paused = true;
            for (int i = 0; i < 30; i++) rig.Session.Frame(0.1f);
            Assert.AreEqual(5, w.Tick, "pause stops ticks");
            Assert.AreEqual(3f, rig.Session.PausedSeconds, 1e-3f);
            Assert.AreEqual(0f, rig.Host.LastDt, "render dt is 0 while paused");
            rig.Session.Paused = false;
            rig.Frames(2);
            Assert.AreEqual(7, w.Tick);
            Assert.AreEqual(3f, rig.Session.PausedSeconds, 1e-3f);

            rig.Session.StartRun(Cfg());
            Assert.AreEqual(0f, rig.Session.PausedSeconds);
            Assert.IsFalse(rig.Session.Paused);
        }

        [Test]
        public void GateClosed_InputDoesNotReachWorld_OpenAttaches()
        {
            var rig = new SessionRig();
            rig.Session.InputScreen = ScreenId.Battle;
            rig.Screens.TopId = ScreenId.LootPick;
            rig.Session.StartRun(Cfg());
            var w = rig.Session.World;
            rig.Bot.Press(1, LogicalKey.Right).Press(2, LogicalKey.Fire);
            rig.Frames(6);
            Assert.AreEqual(0, w.InputQueues[0].Count, "closed gate: nothing enters the world");
            Assert.AreEqual(6, w.Tick, "ticks keep running behind a screen");

            rig.Screens.TopId = ScreenId.Battle;
            rig.Frames(1);
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Right, true), "held state key pressed on attach");
            Assert.AreEqual(0, SessionRig.CountKey(w, LogicalKey.Fire, true), "held J swallowed on attach");

            // J를 뗐다 다시 누르면 들어간다.
            int t = w.Tick;
            rig.Bot.Release(t, LogicalKey.Fire).Press(t + 1, LogicalKey.Fire);
            rig.Frames(3);
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Fire, true));
            Assert.AreEqual(0, SessionRig.CountKey(w, LogicalKey.Fire, false), "release of swallowed J is silent");
        }

        [Test]
        public void GateClosing_ReleasesHeldKeysIntoWorld()
        {
            var rig = new SessionRig();
            rig.Session.InputScreen = ScreenId.Battle;
            rig.Screens.TopId = ScreenId.Battle;
            rig.Session.StartRun(Cfg());
            var w = rig.Session.World;
            rig.Bot.Press(1, LogicalKey.Up).Press(1, LogicalKey.Stance);
            rig.Frames(3);
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Up, true));
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Stance, true));

            rig.Screens.TopId = ScreenId.Camp;
            rig.Frames(1);
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Up, false));
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Stance, false));
            Assert.IsFalse(rig.Session.GateOpen);

            // 닫힌 동안의 변화는 버린다.
            int before = w.InputQueues[0].Count;
            rig.Bot.Press(w.Tick, LogicalKey.Left).Release(w.Tick + 1, LogicalKey.Left);
            rig.Frames(4);
            Assert.AreEqual(before, w.InputQueues[0].Count);
        }

        [Test]
        public void InputScreenNoneOrNoScreens_GateAlwaysOpen()
        {
            var rig = new SessionRig(withScreens: false);
            rig.Session.InputScreen = ScreenId.Battle;
            rig.Session.StartRun(Cfg());
            rig.Bot.Press(1, LogicalKey.Down);
            rig.Frames(3);
            Assert.AreEqual(1, SessionRig.CountKey(rig.Session.World, LogicalKey.Down, true));

            var rig2 = new SessionRig();
            rig2.Screens.TopId = ScreenId.Camp;
            rig2.Session.InputScreen = ScreenId.None;
            rig2.Session.StartRun(Cfg());
            rig2.Bot.Press(1, LogicalKey.Down);
            rig2.Frames(3);
            Assert.AreEqual(1, SessionRig.CountKey(rig2.Session.World, LogicalKey.Down, true));
        }

        [Test]
        public void PauseKey_NotInWorld_RaisesPauseRequested()
        {
            var rig = new SessionRig();
            int requests = 0;
            rig.Session.PauseRequested += () => requests++;
            rig.Session.StartRun(Cfg());
            var w = rig.Session.World;
            rig.Bot.Press(2, LogicalKey.Pause).Release(4, LogicalKey.Pause).Press(3, LogicalKey.Right);
            rig.Frames(6);
            Assert.AreEqual(1, requests);
            Assert.AreEqual(0, SessionRig.CountKey(w, LogicalKey.Pause, true));
            Assert.AreEqual(0, SessionRig.CountKey(w, LogicalKey.Pause, false));
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Right, true));
            Assert.IsFalse(rig.Session.Paused, "runner does not pause or open screens by itself");
        }

        [Test]
        public void PauseKey_GateClosed_NoRequest()
        {
            var rig = new SessionRig();
            int requests = 0;
            rig.Session.PauseRequested += () => requests++;
            rig.Session.InputScreen = ScreenId.Battle;
            rig.Screens.TopId = ScreenId.Pause;
            rig.Session.StartRun(Cfg());
            rig.Bot.Press(2, LogicalKey.Pause);
            rig.Frames(5);
            Assert.AreEqual(0, requests);
        }

        [Test]
        public void Unpause_ReleasedKeyUp_NewJSwallowed()
        {
            var rig = new SessionRig();
            rig.Session.StartRun(Cfg());
            var w = rig.Session.World;
            rig.Bot.Press(1, LogicalKey.Left);
            rig.Frames(3);
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Left, true));

            rig.Session.Paused = true;
            rig.Bot.Now(LogicalKey.Left, false).Now(LogicalKey.Fire, true);
            rig.Frames(10);
            rig.Session.Paused = false;
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Left, false), "released during pause → up at unpause");
            var q = w.InputQueues[0];
            Assert.AreEqual((long)w.Tick, q[q.Count - 1].TimeU / SimConst.UnitsPerTick, "up event belongs to the resuming tick");
            rig.Frames(3);
            Assert.AreEqual(0, SessionRig.CountKey(w, LogicalKey.Fire, true), "J pressed during pause is swallowed");
        }

        [Test]
        public void SubRun_MainFrozen_WorldAndMainWorldCorrect()
        {
            var rig = new SessionRig();
            rig.Session.StartRun(Cfg());
            var main = rig.Session.World;
            rig.Frames(10);
            Assert.AreEqual(10, main.Tick);
            Assert.AreEqual(1, rig.Host.Builds);

            rig.Session.PushSubRun(Cfg(RunMode.Practice));
            var sub = rig.Session.World;
            Assert.AreNotSame(main, sub);
            Assert.AreSame(main, rig.Session.MainWorld);
            Assert.IsTrue(rig.Session.HasSubRun);
            Assert.AreSame(sub, rig.Host.LastBuilt);
            Assert.AreEqual(1, rig.Host.Disposes);

            rig.Frames(5);
            Assert.AreEqual(10, main.Tick, "main world is frozen while the sub run is up");
            Assert.AreEqual(5, sub.Tick);

            rig.Session.PopSubRun();
            Assert.IsFalse(rig.Session.HasSubRun);
            Assert.AreSame(main, rig.Session.World);
            Assert.AreSame(main, rig.Session.MainWorld);
            Assert.AreSame(main, rig.Host.LastBuilt, "layers rebuilt on the main world");
            Assert.AreEqual(2, rig.Host.Disposes);
            rig.Frames(2);
            Assert.AreEqual(12, main.Tick);
        }

        [Test]
        public void SubRun_InputGoesToSub_MainHeldKeysReleasedThenReattached()
        {
            var rig = new SessionRig();
            rig.Session.StartRun(Cfg());
            var main = rig.Session.World;
            rig.Bot.Press(1, LogicalKey.Right);
            rig.Frames(3);
            Assert.AreEqual(1, SessionRig.CountKey(main, LogicalKey.Right, true));

            rig.Session.PushSubRun(Cfg(RunMode.Practice));
            var sub = rig.Session.World;
            Assert.AreEqual(1, SessionRig.CountKey(main, LogicalKey.Right, false), "main gets the release when the sub run opens");
            rig.Frames(2);
            Assert.AreEqual(1, SessionRig.CountKey(sub, LogicalKey.Right, true), "held direction carries into the sub run");

            rig.Session.PopSubRun();
            rig.Frames(1);
            Assert.AreEqual(2, SessionRig.CountKey(main, LogicalKey.Right, true), "re-attached to main after pop");
        }

        [Test]
        public void RunEnded_RaisedOnce_WithSummary_SubRunDoesNotRaise()
        {
            var rig = new SessionRig { EndAtTick = 3 };
            int ended = 0;
            RunSummary got = null;
            rig.Session.RunEnded += s => { ended++; got = s; };
            rig.Session.StartRun(Cfg());
            rig.Frames(10);
            Assert.AreEqual(1, ended);
            Assert.IsNotNull(got);
            Assert.AreSame(got, rig.Session.LastSummary);
            Assert.AreEqual(1, rig.RunsMade[0].SummaryBuilds);

            // 보조 판의 판 끝은 본판에 닿지 않는다.
            var rig2 = new SessionRig();
            int ended2 = 0;
            rig2.Session.RunEnded += s => ended2++;
            rig2.Session.StartRun(Cfg());
            rig2.Frames(2);
            rig2.EndAtTick = 1;
            rig2.Session.PushSubRun(Cfg(RunMode.Practice));
            rig2.Frames(5);
            Assert.AreEqual(RunPhase.RunEnded, rig2.Session.World.Run.Phase);
            Assert.AreEqual(0, ended2);
            Assert.IsNull(rig2.Session.LastSummary);
        }

        [Test]
        public void EventsDispatchedEveryTick_ToHostAndSimEventRaised()
        {
            var rig = new SessionRig();
            int raised = 0;
            SimWorld from = null;
            rig.Session.SimEventRaised += (w, e) => { raised++; from = w; };
            rig.Session.StartRun(Cfg());
            rig.Frames(4);
            Assert.AreEqual(4, raised);
            Assert.AreEqual(4, rig.Host.Events);
            Assert.AreSame(rig.Session.World, from);
            Assert.AreEqual(0, rig.Session.World.Events.Count, "drained");
        }

        [Test]
        public void HandlerEndsRunMidFrame_StopsStepping()
        {
            var rig = new SessionRig();
            rig.Session.SimEventRaised += (w, e) => { if (w.Tick >= 2) rig.Session.EndRun(); };
            rig.Session.StartRun(Cfg());
            var w0 = rig.Session.World;
            rig.Session.Frame(0.25f);
            Assert.IsFalse(rig.Session.HasRun);
            Assert.AreEqual(2, w0.Tick, "no steps after EndRun inside the handler");
            rig.Frames(3); // 판이 없어도 죽지 않는다
        }

        [Test]
        public void StartRun_RaisesRunStarted_EndRunClears()
        {
            var rig = new SessionRig();
            SimWorld started = null;
            rig.Session.RunStarted += w => started = w;
            rig.Session.StartRun(Cfg());
            Assert.AreSame(rig.Session.World, started);
            Assert.IsTrue(rig.Session.HasRun);
            rig.Session.PushSubRun(Cfg(RunMode.Practice));
            rig.Session.EndRun();
            Assert.IsFalse(rig.Session.HasRun);
            Assert.IsFalse(rig.Session.HasSubRun);
            Assert.IsNull(rig.Session.World);
            Assert.IsNull(rig.Session.MainWorld);
        }

        [Test]
        public void NullInput_NoCrash()
        {
            var ctx = new GameContext();
            var s = new SessionCore(ctx, () => new SimWorld(null, SimModuleSet.Stubs()));
            s.StartRun(Cfg());
            s.Frame(1f / 60f);
            s.Paused = true;
            s.Paused = false;
            s.PushSubRun(Cfg());
            s.Frame(1f / 60f);
            s.PopSubRun();
            s.SetFocused(false);
            s.Frame(1f / 60f);
            s.EndRun();
            Assert.AreEqual(0f, s.PausedSeconds);
        }

        [Test]
        public void FocusLost_ReleasesKeys_FocusBackResyncs()
        {
            var rig = new SessionRig();
            rig.Session.StartRun(Cfg());
            var w = rig.Session.World;
            rig.Bot.Press(1, LogicalKey.Up);
            rig.Frames(3);
            rig.Session.SetFocused(false);
            rig.Frames(1);
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Up, false));
            rig.Session.SetFocused(true);
            rig.Frames(1);
            Assert.AreEqual(2, SessionRig.CountKey(w, LogicalKey.Up, true));
        }

        [Test]
        public void SetInputOverride_SwitchesSource()
        {
            var rig = new SessionRig();
            rig.Session.StartRun(Cfg());
            var w = rig.Session.World;
            rig.Bot.Press(1, LogicalKey.Up);
            rig.Frames(3);
            var other = new BotInputSource();
            rig.Session.SetInputOverride(other);
            Assert.AreSame(other, rig.Session.Input);
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Up, false), "old source released");
            other.Press(w.Tick + 1, LogicalKey.Left);
            rig.Frames(3);
            Assert.AreEqual(1, SessionRig.CountKey(w, LogicalKey.Left, true));
            rig.Session.SetInputOverride(null);
            Assert.AreSame(rig.Bot, rig.Session.Input);
        }
    }
}
