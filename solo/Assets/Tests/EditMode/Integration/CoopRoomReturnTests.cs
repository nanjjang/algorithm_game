using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.App;
using Wizard.Solo.Net;
using Wizard.Solo.Screens.Online;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 방에서 캐릭터 바꾸기(ChangeCharacter)와 판 끝 뒤 같은 방으로 돌아가기(BackToLobby), 판마다 한 번 적기(플레이 테스트 피드백 2026-11).
    /// 서버(CoopHost·RoomServer)와 손님은 같은 프로세스 통로(LoopbackPipe)로 붙는다.
    /// </summary>
    public class CoopRoomReturnTests
    {
        static RunConfig Member(int ch) => OnlineMember.Build(TestContent.Db, ch, 0);

        CoopHost _host;
        readonly List<CoopClient> _clients = new List<CoopClient>();

        [TearDown] public void TearDown() { _clients.Clear(); _host = null; }

        CoopClient Join(int ch)
        {
            LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
            _host.Accept(serverEnd);
            var c = new CoopClient(TestContent.Db);
            c.Connect(clientEnd, Member(ch));
            _clients.Add(c);
            Frames(2);
            return c;
        }

        void Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                foreach (var c in _clients) c.Flush();
                _host.Tick();
                foreach (var c in _clients) c.Pump();
            }
        }

        /// <summary>서버 세계에서 사람 모두를 쓰러뜨린다(체력 0과 같은 길: 전원이 쓰러지면 HpZero로 판이 끝난다).</summary>
        static void DownEveryone(SimWorld w)
        {
            foreach (var p in w.Players)
            {
                if (!p.Active || p.Downed) continue;
                w.Mods.Player.ForceDown(w, p, default(PlayerDamageSource));
                w.Mods.Run.OnPlayerDowned(w, p);
            }
        }

        static string SeatChar(CoopClient c, int slot)
        {
            foreach (var m in c.Lobby) if (m.Slot == slot) return m.CharId;
            return null;
        }

        // ------------------------------------------------------------ 캐릭터 바꾸기

        [Test]
        public void ChangeCharacter_BeforeStart_RebuildsMember_RejectedAfterStart()
        {
            _host = new CoopHost(TestContent.Db);
            var a = Join(1);
            var b = Join(2);
            Assert.AreEqual(Ids.Char.C1, SeatChar(b, 0));

            a.RequestChangeCharacter(Member(3));
            Frames(2);
            Assert.AreEqual(Ids.Char.C3, SeatChar(b, 0), "동료 로비에 바뀐 캐릭터가 보인다");
            Assert.AreEqual(Ids.Char.C3, SeatChar(a, 0));
            Assert.AreEqual(Ids.Char.C3, _host.MemberOfSlot(0).CharId, "서버가 출발 설정을 다시 만든다");
            Assert.AreEqual(0, a.Slot, "자리는 그대로");

            var bad = Member(2);
            bad.CharId = "char.9";
            a.RequestChangeCharacter(bad);
            Frames(2);
            Assert.AreEqual(Ids.Char.C3, _host.MemberOfSlot(0).CharId, "없는 캐릭터는 받지 않는다");

            // 날 메시지를 보내는 손님(손님 쪽 막음을 거치지 않고 서버 규칙만 본다). 판 전에 슬롯 2로 앉는다.
            LoopbackPipe.CreatePair(out var se, out var ce);
            _host.Accept(se);
            var hello = new NetWriter(NetMsg.Hello);
            hello.Int(NetProtocol.Version);
            NetCodec.WriteConfig(hello, Member(4));
            ce.Send(hello.ToArray());
            Frames(2);
            Assert.AreEqual(Ids.Char.C4, _host.MemberOfSlot(2).CharId);

            Assert.IsTrue(_host.StartRun(5UL));
            Frames(2);
            Assert.AreEqual(Ids.Char.C3, a.World.Players[0].CharId, "판은 바꾼 캐릭터로 연다");

            // 판 중에는 손님 쪽이 막고, 서버도 받지 않는다.
            a.RequestChangeCharacter(Member(4));
            var raw = new NetWriter(NetMsg.ChangeCharacter);
            NetCodec.WriteConfig(raw, Member(5));
            ce.Send(raw.ToArray());
            Frames(2);
            Assert.AreEqual(Ids.Char.C3, _host.MemberOfSlot(0).CharId, "판 중에는 바꾸지 않는다");
            Assert.AreEqual(Ids.Char.C4, _host.MemberOfSlot(2).CharId, "서버도 판 중의 바꾸기를 받지 않는다");
            Assert.AreEqual(Ids.Char.C2, _host.MemberOfSlot(1).CharId);
        }

        [Test]
        public void ChangeCharacter_LocalHostRoom_GoesThroughSamePath()
        {
            var room = OnlineCoop.HostLocal(TestContent.Db, Member(1));
            Assert.IsTrue(room.AddBot(Member(2)));
            Assert.IsTrue(room.ChangeCharacter(Member(5)));
            Assert.AreEqual(Ids.Char.C5, room.Host.MemberOfSlot(0).CharId);
            Assert.AreEqual(Ids.Char.C5, SeatChar(room.Me, 0));
            Assert.AreEqual(Ids.Char.C2, SeatChar(room.Me, 1), "봇은 그대로");
            room.Leave();
        }

        // ------------------------------------------------------------ 판 끝 → 같은 방

        [Test]
        public void RunEnded_ReturnsToSameLobby_SameSlotsAndBots_LeaderCanRestart()
        {
            _host = new CoopHost(TestContent.Db) { ReturnToLobbyTicks = 30 };
            var a = Join(1);
            var b = Join(2);
            Assert.IsTrue(_host.AddBot(Member(4)));
            Frames(2);
            Assert.AreEqual(3, a.Lobby.Count);
            Assert.IsTrue(_host.StartRun(9UL));
            Frames(3);
            Assert.AreEqual(1, a.RunNumber);

            // 나만 쓰러지면 판은 이어지고 연결도 그대로다(동료를 본다).
            var w = _host.World;
            w.Mods.Player.ForceDown(w, w.Players[1], default(PlayerDamageSource));
            w.Mods.Run.OnPlayerDowned(w, w.Players[1]);
            Frames(10);
            Assert.AreNotEqual(RunPhase.RunEnded, w.Run.Phase, "동료가 서 있으면 판이 이어진다");
            Assert.AreEqual(CoopClientState.InRun, b.State, "쓰러진 사람도 판에 남는다");
            Assert.IsNotNull(b.World);

            DownEveryone(w);
            Frames(2);
            Assert.AreEqual(RunPhase.RunEnded, w.Run.Phase);
            Assert.IsTrue(_host.Started, "판 끝 직후에는 결과를 적을 틈을 둔다");
            Assert.IsFalse(_host.Finished, "사람이 있는 방은 닫지 않는다");

            Frames(40);
            Assert.IsFalse(_host.Started, "같은 방(로비)으로 돌아왔다");
            foreach (var c in new[] { a, b })
            {
                Assert.AreEqual(CoopClientState.Lobby, c.State);
                Assert.IsNull(c.World);
                Assert.IsNull(c.Config);
                Assert.AreEqual(-1, c.LastSnapshotTick);
                Assert.AreEqual(0, c.SnapshotsReceived);
                Assert.AreEqual(3, c.Lobby.Count, "사람 둘 + 봇 하나 그대로");
            }
            Assert.AreEqual(0, a.Slot);
            Assert.AreEqual(1, b.Slot);
            Assert.AreEqual(Ids.Char.C2, SeatChar(a, 1));
            Assert.IsTrue(a.Lobby.Exists(m => m.IsBot && m.Slot == 2), "봇도 같은 자리");
            Assert.AreEqual(1, _host.BotCount);

            // 돌아온 방에서 캐릭터를 바꾸고 방장이 다시 시작한다.
            b.RequestChangeCharacter(Member(3));
            Frames(2);
            Assert.AreEqual(Ids.Char.C3, SeatChar(a, 1));
            a.RequestStart(10UL);
            Frames(3);
            Assert.IsTrue(_host.Started);
            Assert.AreEqual(2, _host.RunNumber);
            Assert.AreEqual(2, a.RunNumber, "새 판은 판 번호가 는다");
            Assert.AreEqual(2, b.RunNumber);
            Assert.IsNotNull(b.World);
            Assert.AreEqual(Ids.Char.C3, b.World.Players[1].CharId);
            Assert.AreEqual(3, CountActive(_host.World));
        }

        [Test]
        public void RunEnded_PeerWhoLeftDuringRun_IsDroppedFromReturnedLobby()
        {
            _host = new CoopHost(TestContent.Db) { ReturnToLobbyTicks = 10 };
            var a = Join(1);
            var b = Join(2);
            Assert.IsTrue(_host.StartRun(3UL));
            Frames(3);
            b.Disconnect();
            Frames(3);
            Assert.IsTrue(_host.LeftDuringRun(1));
            DownEveryone(_host.World);
            Frames(20);
            Assert.IsFalse(_host.Started);
            Assert.IsFalse(_host.LeftDuringRun(1), "이탈 표시는 판마다 새로");
            Assert.AreEqual(1, a.Lobby.Count, "나간 사람은 빠진다");
            Assert.AreEqual(0, a.Slot);
            Assert.AreEqual(CoopClientState.Lobby, a.State);
        }

        [Test]
        public void RunEnded_AllHumansLeft_DoesNotReturn_StaysFinished()
        {
            _host = new CoopHost(TestContent.Db) { ReturnToLobbyTicks = 10 };
            var a = Join(1);
            Assert.IsTrue(_host.AddBot(Member(2)));
            Frames(2);
            Assert.IsTrue(_host.StartRun(6UL));
            Frames(3);
            a.Disconnect(); // 사람이 다 나갔다(봇만 남았다)
            Frames(3);
            Assert.AreEqual(0, _host.HumanCount);
            Assert.IsTrue(_host.Finished, "봇만 남은 방은 끝난 것으로 본다");
            DownEveryone(_host.World);
            Frames(30);
            Assert.IsTrue(_host.Started, "사람이 없으면 로비로 돌아가지 않는다");
            Assert.IsTrue(_host.Finished, "방 서버가 닫는다");
        }

        [Test]
        public void DefaultReturnDelay_OutlastsDeathScreen()
        {
            // 손님의 쓰러짐 대기(2초)보다 길어야 방장이 다시 시작할 때 모두가 방 화면에 와 있다.
            Assert.Greater(new CoopHost(TestContent.Db).ReturnToLobbyTicks, 2 * SimConst.TicksPerSecond);
        }

        [Test]
        public void RankedRoom_DoesNotReturn_FinishesAfterDelay()
        {
            _host = new CoopHost(TestContent.Db, null, 2, RoomMode.Duel) { Ranked = true, ReturnToLobbyTicks = 10 };
            var a = Join(1);
            var b = Join(2);
            Assert.IsTrue(_host.StartRun(4UL));
            Frames(3);
            b.Disconnect(); // 결투는 나가면 진다 → 판 끝
            for (int i = 0; i < 600 && _host.World.Run.Phase != RunPhase.RunEnded; i++) Frames(1);
            Assert.AreEqual(RunPhase.RunEnded, _host.World.Run.Phase);
            Frames(20);
            Assert.IsTrue(_host.Started, "자동 매치 방은 로비로 돌아가지 않는다");
            Assert.IsTrue(_host.Finished, "방 서버가 닫는다");
            Assert.AreEqual(CoopClientState.InRun, a.State);
        }

        static int CountActive(SimWorld w)
        {
            int n = 0;
            foreach (var p in w.Players) if (p.Active) n++;
            return n;
        }
    }

    /// <summary>방 서버: 같은 방에서 판을 다시 열면 판마다 한 번씩 적고, 돌아온 로비는 다시 참가를 받는다.</summary>
    public class RoomServerReturnTests
    {
        AccountStore _st;
        RoomServer _server;

        sealed class Client { public LoopbackPipe Pipe; public long Id; public string Name; }

        [SetUp]
        public void SetUp()
        {
            _st = new AccountStore((string)null) { Content = TestContent.Db };
            _server = new RoomServer(TestContent.Db, null, 13, _st);
        }

        void Ticks(int n, params CoopClient[] cs)
        {
            for (int i = 0; i < n; i++)
            {
                foreach (var c in cs) c.Flush();
                _server.Tick();
                foreach (var c in cs) c.Pump();
            }
        }

        LoopbackPipe Authed(string name, bool register, long id = 0)
        {
            LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
            _server.Accept(serverEnd);
            var m = new NetWriter(register ? NetMsg.Register : NetMsg.Login);
            if (register) m.Str(name); else m.Long(id);
            m.Str(new string('k', 40) + name);
            clientEnd.Send(m.ToArray());
            Ticks(2);
            return clientEnd;
        }

        static long ReadAuthId(LoopbackPipe p)
        {
            while (p.TryReceive(out var d))
            {
                if (d.Length == 0 || (NetMsg)d[0] != NetMsg.AuthOk) continue;
                var r = new NetReader(d);
                r.Byte();
                return r.Long();
            }
            return 0;
        }

        Client Account(string name)
        {
            var p = Authed(name, true);
            long id = ReadAuthId(p);
            Assert.Greater(id, 0, "계정을 만든다");
            p.Close();
            return new Client { Id = id, Name = name };
        }

        CoopClient Create(Client c, int ch)
        {
            var cc = new CoopClient(TestContent.Db);
            cc.ConnectCreate(Authed(c.Name, false, c.Id), RoomMode.Coop, OnlineMember.Build(TestContent.Db, ch, 0));
            return cc;
        }

        CoopClient JoinRoom(Client c, string code, int ch)
        {
            var cc = new CoopClient(TestContent.Db);
            cc.ConnectJoin(Authed(c.Name, false, c.Id), code, OnlineMember.Build(TestContent.Db, ch, 0));
            return cc;
        }

        static void DownEveryone(SimWorld w)
        {
            foreach (var p in w.Players)
            {
                if (!p.Active || p.Downed) continue;
                w.Mods.Player.ForceDown(w, p, default(PlayerDamageSource));
                w.Mods.Run.OnPlayerDowned(w, p);
            }
        }

        [Test]
        public void SameRoom_TwoRuns_RecordedOncePerRun_AndAcceptsJoinAfterReturn()
        {
            var ua = Account("돌아옴일");
            var ub = Account("돌아옴이");
            var uc = Account("돌아옴삼");
            var a = Create(ua, 1);
            Ticks(3, a);
            Assert.IsNotNull(a.RoomCode);
            string code = a.RoomCode;
            var b = JoinRoom(ub, code, 2);
            Ticks(3, a, b);
            var room = _server.Room(code);
            Assert.AreEqual(2, room.PlayerCount);

            a.RequestStart(21UL);
            Ticks(3, a, b);
            Assert.IsTrue(room.Started);
            DownEveryone(room.World);
            Ticks(2, a, b);
            Assert.AreEqual(RunPhase.RunEnded, room.World.Run.Phase);
            Assert.AreEqual(1, _st.Get(ua.Id).Profile.CoopRuns, "판이 끝나면 적는다");
            Assert.AreEqual(1, _st.Get(ub.Id).Profile.CoopRuns);

            Ticks(room.ReturnToLobbyTicks + 10, a, b);
            Assert.IsFalse(room.Started, "같은 방으로 돌아왔다");
            Assert.AreSame(room, _server.Room(code), "방은 닫지 않는다(같은 코드)");
            Assert.AreEqual(1, _st.Get(ua.Id).Profile.CoopRuns, "한 판은 한 번만 적는다");
            Assert.AreEqual(CoopClientState.Lobby, a.State);
            Assert.AreEqual(CoopClientState.Lobby, b.State);

            // 돌아온 로비는 다시 참가를 받는다.
            var c = JoinRoom(uc, code, 3);
            Ticks(3, a, b, c);
            Assert.AreEqual(CoopClientState.Lobby, c.State, "로비로 돌아온 방에는 다시 들어올 수 있다");
            Assert.AreEqual(2, c.Slot);
            Assert.AreEqual(3, a.Lobby.Count);

            a.RequestStart(22UL);
            Ticks(3, a, b, c);
            Assert.IsTrue(room.Started);
            Assert.AreEqual(2, room.RunNumber);
            DownEveryone(room.World);
            Ticks(5, a, b, c);
            Assert.AreEqual(2, _st.Get(ua.Id).Profile.CoopRuns, "두 번째 판도 한 번 적는다");
            Assert.AreEqual(2, _st.Get(ub.Id).Profile.CoopRuns);
            Assert.AreEqual(1, _st.Get(uc.Id).Profile.CoopRuns);
            Ticks(room.ReturnToLobbyTicks + 10, a, b, c);
            Assert.AreEqual(2, _st.Get(ua.Id).Profile.CoopRuns);
            a.Disconnect(); b.Disconnect(); c.Disconnect();
        }
    }

    /// <summary>방 화면 규칙 + 세션: 판이 끝나도 방에 남고(DetachOnline), 같은 방에서 다시 시작한다. 캐릭터 바꾸기는 오프라인 프로필에 쓰지 않는다.</summary>
    public class CoopLobbyReturnTests
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
        public void Lobby_ChangeCharacter_DoesNotWriteOfflineProfile()
        {
            int before = _k.Profile.LastCharacter;
            var m = new CoopLobbyModel(_k.Ctx);
            Assert.IsTrue(m.CreateRoom(), "방 만들기: " + m.ErrorKey);
            m.Tick();
            int start = m.SeatCharacter;
            Assert.IsTrue(m.CanChangeCharacter);
            Assert.IsTrue(m.ChangeCharacter(1));
            m.Tick();
            Assert.AreEqual(start % Ids.Char.Count + 1, m.SeatCharacter, "다음 캐릭터");
            Assert.IsTrue(m.ChangeCharacter(-2));
            m.Tick();
            int expect = ((start - 1 + 1 - 2) % Ids.Char.Count + Ids.Char.Count) % Ids.Char.Count + 1;
            Assert.AreEqual(expect, m.SeatCharacter, "앞으로 돌면 끝에서 이어진다");
            Assert.AreEqual(before, _k.Profile.LastCharacter, "오프라인 프로필은 그대로");
            Assert.IsTrue(m.Start(3UL));
            Assert.IsFalse(m.CanChangeCharacter, "판 중에는 못 바꾼다");
            m.Leave();
        }

        [Test]
        public void RunEnds_DetachKeepsRoom_ReturnsToLobby_LeaderStartsAgain()
        {
            var m = new CoopLobbyModel(_k.Ctx);
            Assert.IsTrue(m.CreateRoom(), "방 만들기: " + m.ErrorKey);
            Assert.IsTrue(m.AddBot());
            m.Tick();
            Assert.IsTrue(m.Start(11UL), "시작: " + m.ErrorKey);
            var host = m.Room.Host;
            host.ReturnToLobbyTicks = 20;
            for (int i = 0; i < 30; i++) _session.Frame(1f / 60f);

            var hw = host.World;
            foreach (var p in hw.Players)
            {
                if (!p.Active || p.Downed) continue;
                hw.Mods.Player.ForceDown(hw, p, default(PlayerDamageSource));
                hw.Mods.Run.OnPlayerDowned(hw, p);
            }
            for (int i = 0; i < 4; i++) _session.Frame(1f / 60f);
            Assert.AreEqual(RunPhase.RunEnded, _session.MainWorld.Run.Phase, "꼭두각시도 판 끝");

            // Battle의 판 끝(GoToSettlement)과 같은 길: 판만 걷고 연결은 남긴다.
            _session.DetachOnline();
            Assert.IsFalse(_session.IsOnline);
            Assert.IsTrue(m.Room.Connected, "판이 끝나도 방에서 나가지 않는다");
            m.Enter(ScreenArgs.From(ScreenId.Battle).With(ScreenArgKeys.CoopResultWave, 0).With(ScreenArgKeys.CoopResultScore, 0));
            Assert.IsTrue(m.HasRoom);
            Assert.AreSame(m.Room, _k.Ctx.Online);
            Assert.AreEqual(0, m.ResultWave, "결과가 보인다");

            bool reentered = false;
            for (int i = 0; i < 60 && m.Room.World != null; i++) reentered |= m.Tick();
            Assert.IsFalse(reentered, "끝난 판의 세계를 새 판으로 보지 않는다");
            Assert.IsNull(m.Room.World, "서버가 같은 방으로 돌려보냈다");
            Assert.IsFalse(m.ReturningToRoom);
            Assert.AreEqual(2, m.Members().Count, "나 + 봇 그대로");
            Assert.IsTrue(m.CanStart, "방장이 다시 시작할 수 있다");

            // 화면을 새로 만들어도(새 모델) 열린 방을 다시 집는다.
            var fresh = new CoopLobbyModel(_k.Ctx);
            fresh.Enter(ScreenArgs.From(ScreenId.Battle));
            Assert.AreSame(m.Room, fresh.Room);
            Assert.IsFalse(fresh.Tick(), "로비에서 새 판으로 착각하지 않는다");

            Assert.IsTrue(m.Start(12UL), "다시 시작: " + m.ErrorKey);
            Assert.IsTrue(_session.IsOnline);
            Assert.AreSame(m.Room.World, _session.World);
            Assert.AreEqual(2, m.Room.RunNumber);
            Assert.AreNotEqual(RunPhase.RunEnded, _session.World.Run.Phase);

            // 판 중 나가기(Esc 확인 = EndRun)는 여전히 연결을 끊는다.
            _session.EndRun();
            Assert.IsFalse(m.Room.Connected);
        }
    }
}
