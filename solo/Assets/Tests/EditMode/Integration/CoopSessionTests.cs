using NUnit.Framework;
using Wizard.Solo.App;
using Wizard.Solo.Screens.Online;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>협동 방 규칙 + 세션 온라인 길(11-online.md 4절 2단계): 방 → 봇 → 시작 → 세션이 꼭두각시를 그리며 서버를 돌린다.</summary>
    public class CoopSessionTests
    {
        IntegrationKit _k;
        SessionCore _session;

        [SetUp]
        public void SetUp()
        {
            _k = new IntegrationKit();
            _session = new SessionCore(_k.Ctx);
            _k.Ctx.Session = _session;
        }

        [TearDown] public void TearDown() { _session.EndRun(); _k.Dispose(); }

        [Test]
        public void Lobby_CreateRoom_AddBots_Start_SessionRunsOnline()
        {
            var m = new CoopLobbyModel(_k.Ctx);
            Assert.IsFalse(m.HasRoom);
            Assert.IsTrue(m.CreateRoom(), "방 만들기: " + m.ErrorKey);
            Assert.AreSame(m.Room, _k.Ctx.Online);
            Assert.IsTrue(m.AddBot());
            Assert.IsTrue(m.AddBot());
            m.Tick();
            Assert.AreEqual(3, m.Members().Count, "나 + 봇 둘");
            Assert.IsTrue(m.Start(11UL), "시작: " + m.ErrorKey);

            Assert.IsTrue(_session.IsOnline);
            var w = _session.World;
            Assert.AreSame(m.Room.World, w, "세션이 꼭두각시 세계를 연다");
            Assert.AreEqual(0, w.ViewSlot);
            Assert.AreEqual(3, CountActive(w));

            _session.Paused = true;
            Assert.IsFalse(_session.Paused, "온라인 판은 멈추지 않는다");

            int serverStart = m.Room.Host.World.Tick;
            for (int i = 0; i < 120; i++) _session.Frame(1f / 60f);
            Assert.Greater(m.Room.Host.World.Tick, serverStart + 100, "세션 프레임이 서버를 돌린다");
            Assert.LessOrEqual(m.Room.Host.World.Tick - w.Tick, NetTickSlack, "꼭두각시가 서버를 따라온다");

            // 꼭두각시에 넣은 화면 명령은 서버로 간다(꼭두각시에는 남지 않는다).
            w.Enqueue(new RunCommand(RunCommandKind.GiveUp));
            _session.Frame(1f / 60f);
            Assert.AreEqual(0, w.Commands.Count);
            for (int i = 0; i < 6; i++) _session.Frame(1f / 60f);
            Assert.AreEqual(RunPhase.RunEnded, m.Room.Host.World.Run.Phase, "포기 명령이 서버에 닿는다");
            Assert.IsNull(_session.LastSummary, "온라인 판은 오프라인 요약·정산을 만들지 않는다");

            _session.EndRun();
            Assert.IsFalse(_session.IsOnline);
            Assert.IsFalse(m.Room.Connected);
            m.Enter(ScreenArgs.From(ScreenId.Battle).With(ScreenArgKeys.CoopResultWave, 0).With(ScreenArgKeys.CoopResultScore, 0));
            Assert.IsFalse(m.HasRoom);
            Assert.IsNull(_k.Ctx.Online);
        }

        const int NetTickSlack = 6;

        static int CountActive(SimWorld w)
        {
            int n = 0;
            foreach (var p in w.Players) if (p.Active) n++;
            return n;
        }
    }
}
