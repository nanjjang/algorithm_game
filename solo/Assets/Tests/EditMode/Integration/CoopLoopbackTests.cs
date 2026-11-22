using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Net;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 협동 2단계(11-online.md 4절): 한 프로세스 안에서 서버(CoopHost)와 손님(CoopClient) 여럿을 루프백 통로로 붙여 판을 돈다.
    /// 손님은 꼭두각시 세계만 보고 입력을 보낸다. 서버만 Step한다.
    /// </summary>
    public class CoopLoopbackTests
    {
        IntegrationKit _k;
        CoopHost _host;
        readonly List<CoopClient> _clients = new List<CoopClient>();
        readonly List<bool[]> _keys = new List<bool[]>();

        [SetUp] public void SetUp() { _k = new IntegrationKit(); }
        [TearDown] public void TearDown() { _k.Dispose(); _clients.Clear(); _keys.Clear(); }

        RunConfig Member(int character)
        {
            var wand = _k.Profile.Characters[character - 1].EquippedWandId;
            var r = _k.Ctx.Wands.BuildRunConfig(character, wand, 1, 1UL);
            Assert.IsTrue(r.Ok, "BuildRunConfig " + character + ": " + r.Error);
            return r.Value;
        }

        void Connect(int count)
        {
            _host = new CoopHost(_k.Db);
            for (int i = 0; i < count; i++)
            {
                LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
                _host.Accept(serverEnd);
                var c = new CoopClient(_k.Db);
                c.Connect(clientEnd, Member(i + 1));
                _clients.Add(c);
                _keys.Add(new bool[5]);
            }
            _host.Tick();
            foreach (var c in _clients) c.Pump();
        }

        /// <summary>한 틱: 손님 입력 보내기 → 서버 틱 → 손님 받기.</summary>
        void Frame(bool god)
        {
            foreach (var c in _clients) c.Flush();
            if (god && _host.World != null) foreach (var p in _host.World.Players) if (p.Active) p.InvulnUntilTick = _host.World.Tick + 2;
            _host.Tick();
            foreach (var c in _clients) c.Pump();
        }

        void Key(int ci, LogicalKey key, bool down)
        {
            int k = key == LogicalKey.Fire ? 4 : (int)key;
            if (_keys[ci][k] == down) return;
            _keys[ci][k] = down;
            _clients[ci].QueueInput(new InputEvent(key, down, 0));
        }

        /// <summary>꼭두각시 세계만 보고 움직이는 봇.</summary>
        void Bot(int ci)
        {
            var c = _clients[ci];
            var w = c.World;
            var p = w.Players[c.Slot];
            EnemyState best = null;
            float bd = float.MaxValue;
            foreach (var e in w.Enemies)
            {
                if (!e.Targetable) continue;
                float d = (e.Pos - p.Pos).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            Key(ci, LogicalKey.Fire, best != null);
            if (best == null) { for (int i = 0; i < 4; i++) Key(ci, (LogicalKey)i, false); return; }
            Vector2 v = best.Pos - p.Pos;
            bool h = Mathf.Abs(v.x) > Mathf.Abs(v.y);
            bool close = v.sqrMagnitude < 2.5f * 2.5f;
            bool blink = (w.Tick & 1) == 0;
            Key(ci, LogicalKey.Left, h && v.x < 0 && (!close || blink));
            Key(ci, LogicalKey.Right, h && v.x > 0 && (!close || blink));
            Key(ci, LogicalKey.Up, !h && v.y < 0 && (!close || blink));
            Key(ci, LogicalKey.Down, !h && v.y > 0 && (!close || blink));
        }

        /// <summary>그 손님이 자기 보상(전리품·비기·출발)을 고른다. 서버가 Slot을 다시 찍으므로 명령에 슬롯을 넣지 않는다.</summary>
        static void ResolveRewards(CoopClient c)
        {
            var cw = c.World;
            if (cw == null || c.Slot < 0 || cw.Tick % NetProtocol.SnapshotEveryTicks != 0) return;
            var r = cw.Run.Rewards[c.Slot];
            if (cw.Run.Phase == RunPhase.LootPick)
            {
                var loot = r.Loot;
                if (r.LootDone || loot == null) return;
                if (loot.OverflowToDiscard > 0) c.SendCommand(new RunCommand(RunCommandKind.LootDiscard, 100));
                else
                {
                    for (int i = 0; i < loot.Cards.Count && i < loot.PickCount; i++) c.SendCommand(new RunCommand(RunCommandKind.LootPick, i));
                    c.SendCommand(new RunCommand(RunCommandKind.LootConfirm));
                }
            }
            else if (cw.InCamp)
            {
                if (r.ArtOffer != null && r.ArtOffer.Candidates.Count > 0)
                    c.SendCommand(new RunCommand(RunCommandKind.ArtPick, 0, 0, r.ArtOffer.Candidates[0], 0));
                else if (!r.DepartReady) c.SendCommand(new RunCommand(RunCommandKind.CampDepart));
            }
        }

        bool Play(int maxTicks, System.Func<SimWorld, bool> until)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                var sw = _host.World;
                if (until(sw)) return true;
                if (sw.Run.Phase == RunPhase.RunEnded) return false;
                var cw0 = _clients[0].World;
                if (cw0.Run.Phase == RunPhase.LootPick || cw0.InCamp)
                {
                    for (int ci = 0; ci < _clients.Count; ci++) { Key(ci, LogicalKey.Fire, false); for (int i = 0; i < 4; i++) Key(ci, (LogicalKey)i, false); }
                    // 사람마다 자기 꼭두각시 세계의 자기 몫(Rewards[내 슬롯])을 보고 고른다. 명령은 스냅숏 주기마다 한 번만.
                    for (int ci = 0; ci < _clients.Count; ci++) ResolveRewards(_clients[ci]);
                }
                else for (int ci = 0; ci < _clients.Count; ci++) Bot(ci);
                Frame(god: true);
            }
            return until(_host.World);
        }

        // ------------------------------------------------------------ 테스트

        [Test]
        public void Loopback_ThreeClients_GetSlots_AndStartTogether()
        {
            Connect(3);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(CoopClientState.Lobby, _clients[i].State, "손님 " + i);
                Assert.AreEqual(i, _clients[i].Slot, "들어온 순서대로 슬롯");
                Assert.AreEqual(3, _clients[i].Lobby.Count, "로비 목록");
            }
            Assert.IsTrue(_host.StartRun(42UL));
            foreach (var c in _clients) c.Pump();
            for (int i = 0; i < 3; i++)
            {
                var c = _clients[i];
                Assert.AreEqual(CoopClientState.InRun, c.State);
                Assert.AreEqual(42UL, c.Config.Seed);
                Assert.AreEqual(2, c.Config.Party.Count);
                for (int s = 0; s < 3; s++)
                {
                    Assert.IsTrue(c.World.Players[s].Active, "손님 " + i + "의 세계에 슬롯 " + s);
                    Assert.AreEqual("char." + (s + 1), c.World.Players[s].CharId);
                }
                Assert.IsFalse(c.World.Players[3].Active);
            }
        }

        [Test]
        public void Loopback_FifthPlayer_IsRejected_Full()
        {
            Connect(4);
            LoopbackPipe.CreatePair(out var s, out var cEnd);
            _host.Accept(s);
            var extra = new CoopClient(_k.Db);
            extra.Connect(cEnd, Member(5));
            _host.Tick();
            extra.Pump();
            Assert.AreEqual(CoopClientState.Rejected, extra.State);
            Assert.AreEqual(RejectReason.Full, extra.RejectedBecause);
        }

        [Test]
        public void Snapshot_RoundTrip_IsLossless()
        {
            Connect(2);
            _host.StartRun(7UL);
            foreach (var c in _clients) c.Pump();
            Play(600, w => w.Run.Phase == RunPhase.WaveCombat && w.Enemies.Count > 0 && w.Projectiles.Count > 0);
            var a = new NetWriter();
            WorldSnapshot.Write(a, _host.World);
            byte[] first = a.ToArray();
            var puppet = _clients[1].World;
            WorldSnapshot.Apply(new NetReader(first), puppet);
            var b = new NetWriter();
            WorldSnapshot.Write(b, puppet);
            byte[] second = b.ToArray();
            TestContext.WriteLine("스냅숏 " + first.Length + "바이트, 적 " + _host.World.Enemies.Count + ", 내 탄 " + _host.World.Projectiles.Count);
            Assert.AreEqual(first.Length, second.Length, "다시 쓴 스냅숏 길이");
            CollectionAssert.AreEqual(first, second, "꼭두각시에 덮어쓴 뒤 다시 쓰면 같은 바이트여야 한다");
        }

        [Test]
        public void Loopback_TwoClients_ClearWave1_PuppetsMatchServer()
        {
            Connect(2);
            _host.StartRun(5UL);
            foreach (var c in _clients) c.Pump();
            Assert.IsTrue(Play(12000, w => w.Run.Phase == RunPhase.LootPick),
                "루프백 2인 웨이브 1 클리어(Phase = " + _host.World.Run.Phase + ", 적 " + _host.World.Enemies.Count + ")");
            TestContext.WriteLine("루프백 2인 웨이브 1 클리어 " + _host.World.Tick + "틱, 처치 " + _host.World.Run.TotalKills);

            // 스냅숏 틱에 맞춰 멈추면 꼭두각시가 서버와 같다.
            while (_host.World.Tick % NetProtocol.SnapshotEveryTicks != 0) Frame(true);
            foreach (var c in _clients)
            {
                Assert.AreEqual(_host.World.Tick, c.LastSnapshotTick);
                Assert.AreEqual(_host.World.Run.Phase, c.World.Run.Phase);
                Assert.AreEqual(_host.World.Run.TotalKills, c.World.Run.TotalKills);
                Assert.AreEqual(_host.World.Run.Score, c.World.Run.Score);
                for (int s = 0; s < 2; s++)
                {
                    Assert.AreEqual(_host.World.Players[s].Pos, c.World.Players[s].Pos, "슬롯 " + s + " 위치");
                    Assert.AreEqual(_host.World.Players[s].Hp, c.World.Players[s].Hp, "슬롯 " + s + " 체력");
                }
            }

            var evs = new List<SimEvent>();
            _clients[1].DrainEvents(evs);
            Assert.IsTrue(evs.Exists(e => e.Type == SimEventType.WaveCleared), "손님도 웨이브 클리어 이벤트를 받는다");

            Assert.IsTrue(Play(4000, w => w.Run.Phase == RunPhase.WaveCombat && w.Wave != null && w.Wave.Wave == 2),
                "두 사람이 각자 고른 명령(전리품·비기·야영 출발)이 서버에 닿아 웨이브 2 전투(Phase = " + _host.World.Run.Phase + ")");
        }

        [Test]
        public void Loopback_ClientLeavesMidRun_CountsAsDowned()
        {
            Connect(2);
            _host.StartRun(9UL);
            foreach (var c in _clients) c.Pump();
            Assert.IsTrue(Play(3000, w => w.Run.Phase == RunPhase.WaveCombat));
            _clients[1].Disconnect();
            Frame(true);
            Assert.IsTrue(_host.World.Players[1].Downed, "나간 손님의 캐릭터는 쓰러진 것으로 친다");
            Assert.AreNotEqual(RunPhase.RunEnded, _host.World.Run.Phase, "남은 사람이 있으면 판이 이어진다");
        }
    
        [Test]
        public void Loopback_FlowEvents_ArriveWithFreshState()
        {
            // 손님이 PhaseChanged(LootPick)를 받는 순간 꼭두각시도 이미 LootPick이고 전리품이 있어야 화면을 연다.
            Connect(2);
            _host.StartRun(5UL);
            foreach (var c in _clients) c.Pump();
            bool sawLoot = false, stale = false;
            for (int t = 0; t < 12000 && !sawLoot; t++)
            {
                var cw = _clients[1].World;
                if (cw.Run.Phase == RunPhase.WaveCombat || cw.Run.Phase == RunPhase.WavePrelude) for (int ci = 0; ci < _clients.Count; ci++) Bot(ci);
                Frame(true);
                var evs = new List<SimEvent>();
                _clients[1].DrainEvents(evs);
                foreach (var e in evs)
                {
                    if (e.Type != SimEventType.PhaseChanged || (RunPhase)e.A != RunPhase.LootPick) continue;
                    sawLoot = true;
                    if (cw.Run.Phase != RunPhase.LootPick || cw.Run.Rewards[_clients[1].Slot].Loot == null) stale = true;
                }
            }
            Assert.IsTrue(sawLoot, "전리품 단계에 닿는다");
            Assert.IsFalse(stale, "이벤트를 받을 때 꼭두각시 상태가 이미 전리품이어야 한다");
        }

        [Test]
        public void Loopback_EachPlayerPicksOwnLoot_WaitsForTeammate()
        {
            // 협동 보상은 사람마다: 손님 1이 자기 몫을 골라도 손님 0의 제시는 그대로이고, 둘 다 끝내야 야영으로 간다.
            Connect(2);
            _host.StartRun(5UL);
            foreach (var c in _clients) c.Pump();
            Assert.IsTrue(Play(12000, w => w.Run.Phase == RunPhase.LootPick), "웨이브 1 클리어");
            var hw = _host.World;
            Assert.IsNotNull(hw.Run.Rewards[0].Loot, "슬롯 0의 제시");
            Assert.IsNotNull(hw.Run.Rewards[1].Loot, "슬롯 1의 제시");
            Assert.AreNotSame(hw.Run.Rewards[0].Loot, hw.Run.Rewards[1].Loot, "제시는 사람마다 따로");

            for (int ci = 0; ci < _clients.Count; ci++) { Key(ci, LogicalKey.Fire, false); for (int i = 0; i < 4; i++) Key(ci, (LogicalKey)i, false); }
            var c1 = _clients[1];
            var loot1 = hw.Run.Rewards[c1.Slot].Loot;
            int stored0 = hw.Run.Rewards[_clients[0].Slot].Storage.Count;
            for (int i = 0; i < loot1.Cards.Count && i < loot1.PickCount; i++) c1.SendCommand(new RunCommand(RunCommandKind.LootPick, i));
            c1.SendCommand(new RunCommand(RunCommandKind.LootConfirm));
            for (int k = 0; k < NetProtocol.SnapshotEveryTicks * 2; k++) Frame(true);

            Assert.AreEqual(RunPhase.LootPick, hw.Run.Phase, "동료가 아직 고르는 중이면 전리품 단계에 머문다");
            Assert.IsTrue(hw.Run.Rewards[c1.Slot].LootDone, "손님 1은 끝냈다");
            Assert.IsFalse(hw.Run.Rewards[_clients[0].Slot].LootDone, "손님 0은 아직 고르는 중");
            Assert.AreEqual(stored0, hw.Run.Rewards[_clients[0].Slot].Storage.Count, "손님 1의 고르기가 손님 0의 보관함을 건드리지 않는다");
            Assert.IsTrue(c1.World.Run.Rewards[c1.Slot].LootDone, "손님 1의 꼭두각시도 기다림 상태를 안다");

            Assert.IsTrue(Play(4000, w => w.InCamp), "둘 다 고르면 야영으로");
        }

        [Test]
        public void Loopback_TeammateLeavesDuringLoot_OthersMoveOn()
        {
            Connect(2);
            _host.StartRun(5UL);
            foreach (var c in _clients) c.Pump();
            Assert.IsTrue(Play(12000, w => w.Run.Phase == RunPhase.LootPick), "웨이브 1 클리어");
            var c0 = _clients[0];
            var loot0 = _host.World.Run.Rewards[c0.Slot].Loot;
            for (int i = 0; i < loot0.Cards.Count && i < loot0.PickCount; i++) c0.SendCommand(new RunCommand(RunCommandKind.LootPick, i));
            c0.SendCommand(new RunCommand(RunCommandKind.LootConfirm));
            for (int k = 0; k < NetProtocol.SnapshotEveryTicks * 2; k++) Frame(true);
            Assert.AreEqual(RunPhase.LootPick, _host.World.Run.Phase, "손님 1을 기다린다");

            _clients[1].Disconnect();
            for (int k = 0; k < 4; k++) Frame(true);
            Assert.IsTrue(_host.World.Players[1].Left, "나간 사람은 Left");
            Assert.IsTrue(_host.World.InCamp, "나간 사람을 기다리지 않고 야영으로(Phase = " + _host.World.Run.Phase + ")");
        }
}
}
