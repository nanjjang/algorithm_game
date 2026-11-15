using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Wizard.Solo.Net;
using Wizard.Solo.Screens.Online;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>협동 3단계(11-online.md 4절): 진짜 TCP 소켓(127.0.0.1)으로 서버와 손님 둘을 붙인다. 전용 서버와 같은 흐름(방장의 StartRequest).</summary>
    public class CoopTcpTests
    {
        IntegrationKit _k;
        TcpPipeListener _listener;
        CoopHost _host;
        readonly List<OnlineCoop> _rooms = new List<OnlineCoop>();

        [SetUp]
        public void SetUp()
        {
            _k = new IntegrationKit();
            _listener = new TcpPipeListener(0, bindAll: false);
            _host = new CoopHost(_k.Db);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in _rooms) r.Leave();
            _rooms.Clear();
            _listener.Stop();
            _k.Dispose();
        }

        RunConfig Member(int character)
        {
            var wand = _k.Profile.Characters[character - 1].EquippedWandId;
            var r = _k.Ctx.Wands.BuildRunConfig(character, wand, 1, 1UL);
            Assert.IsTrue(r.Ok);
            return r.Value;
        }

        /// <summary>서버 한 틱(새 연결 받기 포함) + 손님 받기. TCP라 잠깐 기다린다.</summary>
        void ServerTick()
        {
            TcpPipe p;
            while ((p = _listener.Poll()) != null) _host.Accept(p);
            _host.Tick();
            Thread.Sleep(1);
            foreach (var r in _rooms) r.Me.Pump();
        }

        bool Until(int maxTicks, System.Func<bool> cond)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                if (cond()) return true;
                foreach (var r in _rooms) if (r.World != null) { r.Me.Flush(); }
                ServerTick();
            }
            return cond();
        }

        [Test]
        public void ParseAddress_HostAndPort()
        {
            Assert.IsTrue(CoopLobbyModel.ParseAddress("192.168.0.10:30000", out var h, out var p));
            Assert.AreEqual("192.168.0.10", h);
            Assert.AreEqual(30000, p);
            Assert.IsTrue(CoopLobbyModel.ParseAddress(" mini.local ", out h, out p));
            Assert.AreEqual("mini.local", h);
            Assert.AreEqual(CoopLobbyModel.DefaultPort, p);
            Assert.IsFalse(CoopLobbyModel.ParseAddress("", out h, out p));
            Assert.IsFalse(CoopLobbyModel.ParseAddress("a:99999", out h, out p));
        }

        [Test]
        public void Tcp_TwoClients_LeaderStarts_PlayTogether()
        {
            for (int i = 0; i < 2; i++)
            {
                var room = OnlineCoop.JoinRemote(_k.Db, "127.0.0.1", _listener.Port, Member(i + 1));
                Assert.IsNotNull(room, "TCP 연결 " + i);
                _rooms.Add(room);
                Assert.IsTrue(Until(200, () => room.Me.State == CoopClientState.Lobby), "Welcome " + i + " (state " + room.Me.State + ")");
            }
            Assert.IsTrue(Until(200, () => _rooms[1].Me.Lobby.Count == 2), "로비 목록이 둘");
            Assert.IsTrue(_rooms[0].IsLeader);
            Assert.IsFalse(_rooms[1].IsLeader);

            // 방장이 아닌 손님의 시작 요청은 무시된다.
            _rooms[1].StartRun(3UL);
            Until(30, () => false);
            Assert.IsFalse(_host.Started, "방장이 아니면 시작하지 못한다");

            _rooms[0].StartRun(3UL);
            Assert.IsTrue(Until(300, () => _rooms[0].World != null && _rooms[1].World != null), "둘 다 판이 열린다");
            Assert.AreEqual(3UL, _host.World.Seed);

            // 손님 1이 오른쪽으로 걷는다 → 서버의 슬롯 1이 움직이고, 손님 0의 꼭두각시에도 보인다.
            Assert.IsTrue(Until(600, () => _host.World.Run.Phase == RunPhase.WaveCombat || _host.World.Run.Phase == RunPhase.WavePrelude));
            var start = _host.World.Players[1].Pos;
            _rooms[1].Me.QueueInput(new InputEvent(LogicalKey.Right, true, 0));
            Until(60, () => false);
            Assert.Greater(_host.World.Players[1].Pos.x, start.x + 0.5f, "TCP로 보낸 입력이 서버의 슬롯 1을 움직인다");
            Assert.IsTrue(Until(200, () => _rooms[0].World.Players[1].Pos.x > start.x + 0.5f), "다른 손님의 화면에도 보인다");
            Assert.Greater(_rooms[0].Me.SnapshotsReceived, 10);
        }

        [Test]
        public void Tcp_JoinAfterStart_IsRejected()
        {
            var a = OnlineCoop.JoinRemote(_k.Db, "127.0.0.1", _listener.Port, Member(1));
            _rooms.Add(a);
            Assert.IsTrue(Until(200, () => a.Me.State == CoopClientState.Lobby));
            a.StartRun(1UL);
            Assert.IsTrue(Until(200, () => _host.Started));
            var late = OnlineCoop.JoinRemote(_k.Db, "127.0.0.1", _listener.Port, Member(2));
            _rooms.Add(late);
            Assert.IsTrue(Until(200, () => late.Me.State == CoopClientState.Rejected));
            Assert.AreEqual(RejectReason.AlreadyStarted, late.Me.RejectedBecause);
        }

        [Test]
        public void Tcp_AllLeave_HostFinished()
        {
            var a = OnlineCoop.JoinRemote(_k.Db, "127.0.0.1", _listener.Port, Member(1));
            _rooms.Add(a);
            Assert.IsTrue(Until(200, () => a.Me.State == CoopClientState.Lobby));
            a.StartRun(1UL);
            Assert.IsTrue(Until(200, () => _host.Started));
            a.Leave();
            Assert.IsTrue(Until(200, () => _host.Finished), "모두 나가면 방이 끝난다(전용 서버가 새 방을 연다)");
        }
    }
}
