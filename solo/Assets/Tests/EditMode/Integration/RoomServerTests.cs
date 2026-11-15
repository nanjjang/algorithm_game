using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Net;
using Wizard.Solo.Meta.Enhance;
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
            st.RecordCoop(rec.Id, 7, 70);
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

        T WithServerThread<T>(System.Func<T> f)
        {
            bool stop = false;
            var th = new Thread(() => { while (!stop) { Tick(); } });
            th.Start();
            try { return f(); }
            finally { stop = true; th.Join(); }
        }

        [Test]
        public void ServerWand_StageFromServerProfile_NotOfflineProfile()
        {
            var acct = RegisterWithServerThread("단계시험", out var err);
            Assert.IsNotNull(acct);
            _server.Accounts.Get(acct.Id).Profile.WandStages[0] = 4;
            int runsBefore = _k.Profile.RunsStarted;
            var model = new CoopLobbyModel(_k.Ctx);
            Assert.IsTrue(model.CreateOnline(), "온라인 방: " + model.ErrorKey);
            _rooms.Add(model.Room);
            Assert.AreEqual(runsBefore, _k.Profile.RunsStarted, "온라인 방은 오프라인 프로필에 쓰지 않는다");
            Assert.IsTrue(Until(300, () => model.Room.MemberCount == 1));
            model.Room.StartRun(1UL);
            Assert.IsTrue(Until(300, () => model.Room.World != null));
            var host = _server.Room(model.Room.RoomCode);
            Assert.AreEqual(4, host.World.Players[0].Wand.Stage, "서버 프로필의 단계로 선다");
            Assert.AreEqual(Ids.Block.MagicBolt, host.World.Run.Config.Loadout.Core);
        }

        [Test]
        public void ServerEnhance_GoldOnly_AndLeaderboard()
        {
            var acct = RegisterWithServerThread("강화시험", out var err);
            Assert.IsNotNull(acct);
            var svc = WithServerThread(() => OnlineService.Open(_k.Db));
            Assert.IsNotNull(svc);
            Assert.AreEqual("강화시험", svc.Profile.Name);
            var r0 = WithServerThread(() => svc.Enhance(1));
            Assert.AreEqual(AccountStore.EnhanceResult.NotEnoughGold, r0.Value.Result, "골드 0이면 못 한다");

            _server.Accounts.Get(acct.Id).Profile.Gold = 10000000;
            var r1 = WithServerThread(() => svc.Enhance(1));
            Assert.AreEqual(AccountStore.EnhanceResult.NotEnoughStones, r1.Value.Result, "강화석이 없으면 못 한다");
            foreach (var st in new[] { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh })
                _server.Accounts.Get(acct.Id).Profile.Add(st, 1000);
            int stageChanges = 0;
            for (int i = 0; i < 20; i++)
            {
                var r = WithServerThread(() => svc.Enhance(1));
                Assert.AreEqual(AccountStore.EnhanceResult.Ok, r.Value.Result);
                if (r.Value.Outcome == Wizard.Solo.Meta.EnhanceOutcome.MutationOffered)
                {
                    var rr = WithServerThread(() => svc.ResolveMutation(1, i % 2 == 0));
                    Assert.AreEqual(AccountStore.EnhanceResult.Ok, rr.Value.Result, "변이 답");
                    if (rr.Value.Profile.WandStages[0] != rr.Value.StageBefore) stageChanges++;
                    continue;
                }
                if (r.Value.Profile.WandStages[0] != r.Value.StageBefore) stageChanges++;
            }
            var prof = _server.Accounts.Get(acct.Id).Profile;
            Assert.Greater(stageChanges, 0, "20번 하면 단계가 한 번은 바뀐다");
            Assert.Less(prof.Gold, 10000000, "골드를 쓴다");
            Assert.AreEqual(20, prof.EnhanceTries);
            Assert.AreEqual(prof.WandStages[0], svc.Profile.WandStages[0], "답의 프로필이 서버와 같다");

            prof.DuelWins = 3;
            prof.Rating = 1100;
            var top = WithServerThread(() => svc.Leaderboard());
            Assert.IsNotNull(top);
            Assert.AreEqual("강화시험", top[0].Name);
            svc.Dispose();
        }

        [Test]
        public void AccountStore_Charm_BlocksDrop_AndCoopMaterials()
        {
            var st = new AccountStore(null);
            Assert.AreEqual(AccountStore.RegisterResult.Ok, st.Register("부적시험", new string('c', 40), out var rec));
            var cfg = _k.Db.Enhance;
            var p = rec.Profile;
            p.Gold = 100000000;
            foreach (var s in new[] { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh }) p.Add(s, 1000);
            // 떨어질 수 있는 단계로 올려 두고, 반드시 실패하는 주사위(9999)로 부적 없이/있이.
            p.WandStages[0] = cfg.DropSafeMaxStage + 2;
            p.PeakStages[0] = p.WandStages[0];
            int before = p.WandStages[0];
            p.Add(Ids.Item.Charm, 1);
            var fail = new FixedRng(9999);
            st.Enhance(rec.Id, 1, _k.Db, fail, out var o1, useCharm: true);
            Assert.AreEqual(before, p.WandStages[0], "부적이 떨어짐을 막는다: " + o1);
            Assert.AreEqual(0, p.Qty(Ids.Item.Charm), "막았으니 하나 썼다");
            st.Enhance(rec.Id, 1, _k.Db, fail, out var o2, useCharm: true);
            Assert.AreEqual(before - 1, p.WandStages[0], "부적이 없으면 떨어진다: " + o2);

            var mats = new List<Wizard.Solo.Meta.MaterialGain> { new Wizard.Solo.Meta.MaterialGain { ItemId = Ids.Item.StoneMid, Qty = 2 } };
            long gold = p.Gold;
            int mid = p.Qty(Ids.Item.StoneMid);
            st.RecordCoop(rec.Id, 5, 300, mats);
            Assert.AreEqual(gold + 300, p.Gold, "원정 지갑 골드");
            Assert.AreEqual(mid + 2, p.Qty(Ids.Item.StoneMid), "재료가 창고로");
        }

        sealed class FixedRng : Wizard.Solo.Meta.Enhance.IEnhanceRandom
        {
            readonly int _v;
            public FixedRng(int v) { _v = v; }
            public int Next(int maxExclusive) => System.Math.Min(_v, maxExclusive - 1);
        }

        /// <summary>주사위 값을 차례로 내준다(변이 구간 → 후보 뽑기).</summary>
        sealed class SeqRng : Wizard.Solo.Meta.Enhance.IEnhanceRandom
        {
            readonly int[] _v; int _i;
            public SeqRng(params int[] v) { _v = v; }
            public int Next(int maxExclusive) => System.Math.Min(_v[System.Math.Min(_i++, _v.Length - 1)], maxExclusive - 1);
        }

        [Test]
        public void ServerEnhance_Sacrifice_RaisesOdds_AndIsConsumed()
        {
            var st = new AccountStore(null);
            st.Register("제물시험", new string('s', 40), out var rec);
            var p = rec.Profile;
            p.Gold = 100000000;
            foreach (var s2 in new[] { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh }) p.Add(s2, 1000);
            var cfg = _k.Db.Enhance;
            p.WandStages[0] = 8;
            string block = null;
            foreach (var b in _k.Db.Blocks) { if (b.Id != Ids.Block.MagicBolt) { block = b.Id; break; } }
            Assert.IsNotNull(block);
            Assert.AreEqual(AccountStore.EnhanceResult.BadSacrifice, st.Enhance(rec.Id, 1, _k.Db, new FixedRng(0), out _, false, new[] { block }), "창고에 없는 블록은 못 쓴다");
            p.Add(block, 2);
            var def = _k.Db.Character(Ids.Char.Of(1));
            int plain = EnhanceRules.Breakdown(cfg, 8, 8, 0, EnhanceRules.SacrificeBonus(cfg, _k.Db, new List<string>(), def.Element)).SuccessPct;
            int withSac = EnhanceRules.Breakdown(cfg, 8, 8, 0, EnhanceRules.SacrificeBonus(cfg, _k.Db, new List<string> { block, block }, def.Element)).SuccessPct;
            Assert.Greater(withSac, plain, "제물이 확률을 올린다");
            Assert.AreEqual(AccountStore.EnhanceResult.Ok, st.Enhance(rec.Id, 1, _k.Db, new FixedRng(9999), out _, false, new[] { block, block }));
            Assert.AreEqual(0, p.Qty(block), "제물은 결과와 상관없이 사라진다");
        }

        [Test]
        public void ServerEnhance_Mutation_OfferAcceptDecline_AndRunUsesIt()
        {
            var st = new AccountStore(null);
            st.Register("변이시험", new string('m', 40), out var rec);
            var p = rec.Profile;
            p.Gold = 100000000;
            foreach (var s2 in new[] { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh }) p.Add(s2, 1000);
            var cfg = _k.Db.Enhance;
            p.WandStages[0] = 6;
            var def = _k.Db.Character(Ids.Char.Of(1));
            var bd = EnhanceRules.Breakdown(cfg, 6, 0, 0, EnhanceRules.SacrificeBonus(cfg, _k.Db, new List<string>(), def.Element));
            Assume.That(bd.MutationEndBp > bd.SuccessBp, "이 단계에 변이 구간이 있어야 한다");
            int roll = bd.SuccessBp; // 변이 구간의 첫 값
            Assert.AreEqual(AccountStore.EnhanceResult.Ok, st.Enhance(rec.Id, 1, _k.Db, new SeqRng(roll, 0), out var o));
            Assert.AreEqual(Wizard.Solo.Meta.EnhanceOutcome.MutationOffered, o);
            Assert.IsNotNull(p.OfferOf(1));
            Assert.AreEqual(6, p.WandStages[0], "받기 전엔 단계 그대로");
            Assert.AreEqual(AccountStore.EnhanceResult.MutationPending, st.Enhance(rec.Id, 1, _k.Db, new FixedRng(0), out _), "답하기 전엔 다시 못 한다");
            string cand = p.OfferOf(1).Candidate;
            Assert.AreEqual(AccountStore.EnhanceResult.Ok, st.ResolveMutation(rec.Id, 1, _k.Db, true, out var o2));
            Assert.AreEqual(Wizard.Solo.Meta.EnhanceOutcome.MutationAccepted, o2);
            CollectionAssert.Contains(st.MutationIds(rec.Id, 1), cand);
            Assert.AreEqual(1, p.Auras[0], "받으면 기운 +1");

            // 거절: 미리 계산한 실패
            st.Enhance(rec.Id, 1, _k.Db, new SeqRng(roll, 0), out var o3);
            if (o3 == Wizard.Solo.Meta.EnhanceOutcome.MutationOffered)
            {
                Assert.AreEqual(AccountStore.EnhanceResult.Ok, st.ResolveMutation(rec.Id, 1, _k.Db, false, out var o4));
                Assert.AreNotEqual(Wizard.Solo.Meta.EnhanceOutcome.MutationAccepted, o4);
                Assert.IsNull(p.OfferOf(1));
            }
        }

        [Test]
        public void ServerLoadout_SocketFromStorage_AndRunUsesIt()
        {
            var st = new AccountStore(null);
            st.Register("조립시험", new string('a', 40), out var rec);
            var p = rec.Profile;
            string proj = null, mod = null;
            foreach (var b in _k.Db.Blocks)
            {
                if (proj == null && b.IsProjectile && b.Id != Ids.Block.MagicBolt && b.Style == null) proj = b.Id;
                if (mod == null && !b.IsProjectile) mod = b.Id;
            }
            Assert.IsNotNull(proj);
            Assert.IsNotNull(mod);
            var rules = SimModuleSet.CreateDefault();
            int n = st.LinkSlots(rec.Id, 1, _k.Db);
            Assert.Greater(n, 0);
            Assert.AreEqual(AccountStore.LoadoutResult.NotOwned, st.SetLoadout(rec.Id, 1, proj, null, _k.Db, rules), "창고에 없으면 못 끼운다");
            p.Add(proj, 1);
            Assert.AreEqual(AccountStore.LoadoutResult.BadShape, st.SetLoadout(rec.Id, 1, mod, null, _k.Db, rules), "코어는 투사체만");
            Assert.AreEqual(AccountStore.LoadoutResult.Ok, st.SetLoadout(rec.Id, 1, proj, null, _k.Db, rules));
            Assert.AreEqual(0, p.Qty(proj), "끼우면 창고에서 빠진다");
            Assert.AreEqual(proj, st.LoadoutSpecOf(rec.Id, 1, _k.Db).Core);
            Assert.AreEqual(AccountStore.LoadoutResult.Ok, st.SetLoadout(rec.Id, 1, Ids.Block.MagicBolt, null, _k.Db, rules), "마력탄은 창고 없이");
            Assert.AreEqual(1, p.Qty(proj), "빼면 창고로 돌아온다");

            // 방에서: 서버 조립으로 선다.
            st.SetLoadout(rec.Id, 1, proj, null, _k.Db, rules);
            var host = new CoopHost(_k.Db) { StageLookup = st.StageOf, LoadoutLookup = (aid, ch) => st.LoadoutSpecOf(aid, ch, _k.Db) };
            LoopbackPipe.CreatePair(out var se, out var ce);
            host.Accept(se, rec);
            var c = new CoopClient(_k.Db);
            c.Connect(ce, OnlineMember.Build(_k.Db, 1, 0));
            host.Tick(); c.Pump();
            Assert.IsTrue(host.StartRun(3UL));
            Assert.AreEqual(proj, host.World.Run.Config.Loadout.Core, "판 출발 설정의 코어 = 서버 조립");
        }
}
}
