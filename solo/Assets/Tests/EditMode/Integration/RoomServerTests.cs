using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Net;
using Wizard.Solo.Screens.Online;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>방 서버(코드로 참가)와 광장: 진짜 TCP(127.0.0.1)로 RoomServer에 붙는다.</summary>
    public class RoomServerTests
    {
        IntegrationKit _k;
        TcpPipeListener _listener;
        RoomServer _server;
        readonly List<OnlineCoop> _rooms = new List<OnlineCoop>();
        readonly List<PlazaClient> _plaza = new List<PlazaClient>();

        [SetUp]
        public void SetUp()
        {
            _k = new IntegrationKit();
            _listener = new TcpPipeListener(0, bindAll: false);
            _server = new RoomServer(_k.Db, null, 7);
            OnlineServer.Override = new List<string> { "127.0.0.1:" + _listener.Port };
            _acctFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wizard_acct_" + System.Guid.NewGuid().ToString("N") + ".json");
            OnlineAccount.FilePath = _acctFile;
        }

        string _acctFile;

        /// <summary>계정 만들기는 응답을 기다리며 막히므로, 서버는 다른 스레드에서 돌린다(테스트에서만).</summary>
        OnlineAccount RegisterWithServerThread(string name, out AuthError? err)
        {
            bool stop = false;
            var th = new Thread(() => { while (!stop) { Tick(); } });
            th.Start();
            try { return OnlineAccount.Register(_k.Db, name, out err, out _); }
            finally { stop = true; th.Join(); }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in _rooms) r.Leave();
            foreach (var p in _plaza) p.Leave();
            _rooms.Clear();
            _plaza.Clear();
            OnlineServer.Override = null;
            OnlineAccount.FilePath = null;
            try { System.IO.File.Delete(_acctFile); } catch (System.Exception) { }
            _listener.Stop();
            _k.Dispose();
        }

        RunConfig Member(int c)
        {
            var r = _k.Ctx.Wands.BuildRunConfig(c, _k.Profile.Characters[c - 1].EquippedWandId, 1, 1UL);
            Assert.IsTrue(r.Ok);
            return r.Value;
        }

        void Tick()
        {
            TcpPipe p;
            while ((p = _listener.Poll()) != null) _server.Accept(p);
            _server.Tick();
            Thread.Sleep(1);
            foreach (var r in _rooms) { r.Me.Flush(); r.Me.Pump(); }
            foreach (var c in _plaza) c.Update(1f / 60f);
        }

        bool Until(int n, System.Func<bool> cond)
        {
            for (int i = 0; i < n; i++) { if (cond()) return true; Tick(); }
            return cond();
        }

        [Test]
        public void CreateRoom_GetsCode_FriendJoinsByCode()
        {
            var a = OnlineCoop.CreateRoom(_k.Db, RoomMode.Coop, Member(1));
            Assert.IsNotNull(a);
            _rooms.Add(a);
            Assert.IsTrue(Until(300, () => a.RoomCode != null && a.Me.State == CoopClientState.Lobby), "코드를 받는다");
            Assert.AreEqual(RoomServer.CodeLength, a.RoomCode.Length);
            foreach (char ch in a.RoomCode) StringAssert.Contains(ch.ToString(), RoomServer.CodeAlphabet);

            var b = OnlineCoop.JoinCode(_k.Db, a.RoomCode.ToLowerInvariant(), Member(2));
            _rooms.Add(b);
            Assert.IsTrue(Until(300, () => b.Me.State == CoopClientState.Lobby && a.MemberCount == 2), "소문자로 쳐도 들어간다");
            Assert.AreEqual(a.RoomCode, b.RoomCode);
            Assert.AreEqual(1, b.Slot);
            Assert.AreEqual(1, _server.RoomCount);

            a.StartRun(3UL);
            Assert.IsTrue(Until(300, () => a.World != null && b.World != null), "방장이 시작하면 둘 다 판");
        }

        [Test]
        public void WrongCode_IsRejected_NotFound()
        {
            var b = OnlineCoop.JoinCode(_k.Db, "ZZZZZZ", Member(2));
            _rooms.Add(b);
            Assert.IsTrue(Until(300, () => b.Me.State == CoopClientState.Rejected));
            Assert.AreEqual(RoomError.NotFound, b.Me.RoomErrorCode);
        }

        [Test]
        public void TwoRooms_AreSeparate()
        {
            var a = OnlineCoop.CreateRoom(_k.Db, RoomMode.Coop, Member(1));
            var c = OnlineCoop.CreateRoom(_k.Db, RoomMode.Coop, Member(3));
            _rooms.Add(a); _rooms.Add(c);
            Assert.IsTrue(Until(300, () => a.RoomCode != null && c.RoomCode != null && a.MemberCount == 1 && c.MemberCount == 1));
            Assert.AreNotEqual(a.RoomCode, c.RoomCode);
            Assert.AreEqual(2, _server.RoomCount);
            Assert.AreEqual(0, a.Slot);
            Assert.AreEqual(0, c.Slot);
        }

        [Test]
        public void DuelRoom_TwoMax_NeedsTwoToStart_PlaysDuel()
        {
            var a = OnlineCoop.CreateRoom(_k.Db, RoomMode.Duel, Member(1));
            _rooms.Add(a);
            Assert.IsTrue(Until(300, () => a.RoomCode != null && a.MemberCount == 1));
            Assert.AreEqual(RoomMode.Duel, a.Mode);
            a.StartRun(1UL);
            Until(30, () => false);
            Assert.IsNull(a.World, "혼자서는 결투를 시작하지 못한다");

            var b = OnlineCoop.JoinCode(_k.Db, a.RoomCode, Member(2));
            _rooms.Add(b);
            Assert.IsTrue(Until(300, () => a.MemberCount == 2));
            var c = OnlineCoop.JoinCode(_k.Db, a.RoomCode, Member(3));
            _rooms.Add(c);
            Assert.IsTrue(Until(300, () => c.Me.State == CoopClientState.Rejected), "결투 방은 2명까지");
            Assert.AreEqual(RoomError.Full, c.Me.RoomErrorCode);

            a.StartRun(1UL);
            Assert.IsTrue(Until(300, () => a.World != null && b.World != null));
            Assert.AreEqual(RunMode.Duel, a.World.Run.Config.Mode);
            Assert.IsTrue(Until(400, () => b.World.Run.Phase == RunPhase.DuelFight), "손님 화면도 결투 싸움 단계");
            Assert.IsNotNull(b.World.Run.Duel, "결투 상태가 스냅숏으로 온다");
        }

        [Test]
        public void EmptyRoom_IsClosedAfterEveryoneLeaves()
        {
            var a = OnlineCoop.CreateRoom(_k.Db, RoomMode.Coop, Member(1));
            _rooms.Add(a);
            Assert.IsTrue(Until(300, () => a.MemberCount == 1));
            a.Leave();
            Assert.IsTrue(Until(RoomServer.EmptyRoomTicks + 300, () => _server.RoomCount == 0), "빈 방은 지운다");
        }

        [Test]
        public void Plaza_TwoPeople_SeeEachOther_MoveAndChat()
        {
            var p1 = new PlazaClient();
            var p2 = new PlazaClient();
            _plaza.Add(p1); _plaza.Add(p2);
            p1.Connect(OnlineServer.Connect(_k.Db), "마법사A", "char.1");
            p2.Connect(OnlineServer.Connect(_k.Db), "마법사B", "char.2");
            Assert.IsTrue(Until(300, () => p1.MyId > 0 && p2.MyId > 0 && p1.People.Count == 2 && p2.People.Count == 2), "서로 보인다");
            Vector2 start = p2.People[p1.MyId].Now.Pos;
            p1.Move(2); // 오른쪽
            Until(60, () => false);
            p1.Move(-1);
            Until(12, () => false);
            Assert.Greater(p2.People[p1.MyId].Now.Pos.x, start.x + 1f, "A가 오른쪽으로 걸은 것이 B에게 보인다");
            p1.Say("안녕\n하세요");
            Assert.IsTrue(Until(60, () => p2.People[p1.MyId].Now.Bubble != null));
            Assert.AreEqual("안녕하세요", p2.People[p1.MyId].Now.Bubble, "줄바꿈은 빠진다");
            p2.Emote(3);
            Assert.IsTrue(Until(60, () => p1.People[p2.MyId].Now.Emote == 3));
            p2.Leave();
            Assert.IsTrue(Until(120, () => p1.People.Count == 1), "나간 사람은 사라진다");
        }

        [Test]
        public void Plaza_DirOf_EightDirections()
        {
            Assert.AreEqual(-1, PlazaScreen.DirOf(false, false, false, false));
            Assert.AreEqual(0, PlazaScreen.DirOf(true, false, false, false));
            Assert.AreEqual(1, PlazaScreen.DirOf(true, false, false, true));
            Assert.AreEqual(6, PlazaScreen.DirOf(false, false, true, false));
            Assert.AreEqual(5, PlazaScreen.DirOf(false, true, true, false));
        }
    
        [Test]
        public void Account_Register_Login_DuelResultRecorded()
        {
            var acct = RegisterWithServerThread("마법사갑", out var err);
            Assert.IsNotNull(acct, "계정 만들기: " + err);
            Assert.AreEqual(acct.Id, OnlineAccount.Load().Id, "기기에 저장");
            Assert.IsNull(RegisterWithServerThread("마법사갑", out err), "같은 이름은 안 된다");
            Assert.AreEqual(AuthError.NameTaken, err);

            // 두 번째 계정은 다른 기기라 치고 파일을 바꿔 만든다.
            string first = _acctFile;
            OnlineAccount.FilePath = first + ".b";
            var acctB = RegisterWithServerThread("마법사을", out err);
            Assert.IsNotNull(acctB);

            OnlineAccount.FilePath = first;
            var a = OnlineCoop.CreateRoom(_k.Db, RoomMode.Duel, Member(1));
            _rooms.Add(a);
            Assert.IsTrue(Until(300, () => a.RoomCode != null && a.Me.Profile != null), "로그인하면 서버 프로필이 온다");
            Assert.AreEqual("마법사갑", a.Me.Profile.Name);
            Assert.AreEqual(1000, a.Me.Profile.Rating);

            OnlineAccount.FilePath = first + ".b";
            var b = OnlineCoop.JoinCode(_k.Db, a.RoomCode, Member(2));
            _rooms.Add(b);
            Assert.IsTrue(Until(300, () => a.MemberCount == 2));
            Assert.IsTrue(a.Me.Lobby.Exists(m => m.Name == "마법사을"), "로비에 계정 이름");

            a.StartRun(5UL);
            Assert.IsTrue(Until(300, () => a.World != null));
            // 슬롯 1(을)이 나가면 결투는 갑의 승리로 끝나고 기록된다.
            b.Leave();
            Assert.IsTrue(Until(600, () => _server.Accounts.Get(acct.Id).Profile.DuelWins == 1), "승리 기록");
            var pa = _server.Accounts.Get(acct.Id).Profile;
            var pb = _server.Accounts.Get(acctB.Id).Profile;
            Assert.AreEqual(1, pb.DuelLosses);
            Assert.Greater(pa.Rating, 1000);
            Assert.Less(pb.Rating, 1000);
            Assert.AreEqual(AccountStore.DuelWinGold, pa.Gold);
            try { System.IO.File.Delete(first + ".b"); } catch (System.Exception) { }
        }

        [Test]
        public void Account_WrongSecret_IsRejected()
        {
            var acct = RegisterWithServerThread("마법사병", out var err);
            Assert.IsNotNull(acct);
            System.IO.File.WriteAllText(_acctFile, "{\"Id\":" + acct.Id + ",\"Name\":\"x\",\"Secret\":\"" + new string('A', 44) + "\"}");
            var a = OnlineCoop.CreateRoom(_k.Db, RoomMode.Coop, Member(1));
            _rooms.Add(a);
            Assert.IsTrue(Until(300, () => a.Me.State == CoopClientState.Rejected), "비밀이 틀리면 못 들어간다");
            Assert.AreEqual(AuthError.WrongLogin, a.Me.AuthFailed);
        }

        [Test]
        public void AccountStore_PersistsToFile()
        {
            string path = _acctFile + ".store";
            var st = new AccountStore(path);
            Assert.AreEqual(AccountStore.RegisterResult.Ok, st.Register("저장시험", new string('k', 40), out var rec));
            st.RecordCoop(rec.Id, 7);
            var again = new AccountStore(path);
            Assert.AreEqual(1, again.Count);
            Assert.IsNotNull(again.Login(rec.Id, new string('k', 40)));
            Assert.IsNull(again.Login(rec.Id, new string('j', 40)));
            Assert.AreEqual(7, again.Get(rec.Id).Profile.CoopBestWave);
            Assert.AreEqual(70, again.Get(rec.Id).Profile.Gold);
            try { System.IO.File.Delete(path); } catch (System.Exception) { }
        }

        [Test]
        public void Plaza_DoorAt()
        {
            Assert.AreEqual(-1, PlazaScreen.DoorAt(new Vector2(20f, 11f)));
            Assert.AreEqual(RoomMode.Coop, PlazaScreen.Doors[PlazaScreen.DoorAt(new Vector2(2f, 10f))].Mode);
            Assert.AreEqual(RoomMode.Duel, PlazaScreen.Doors[PlazaScreen.DoorAt(new Vector2(37f, 10f))].Mode);
        }
}
}
