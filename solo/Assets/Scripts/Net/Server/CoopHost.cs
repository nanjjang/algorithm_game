using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 협동 판의 서버 쪽(11-online.md 1·3절). 판을 돌리는 유일한 곳이다: 손님의 입력·명령을 자기 슬롯 큐에 넣고, 60틱으로 Step하고,
    /// 스냅숏(20Hz)과 표시 이벤트(매 틱)를 보낸다. 시계는 바깥(헤드리스 서버의 고정 틱 또는 테스트)이 <see cref="Tick"/>을 불러 준다.
    /// 슬롯 = 들어온 순서(0부터). 판 전체 설정(모드·씨앗·시작 웨이브)은 슬롯 0의 Hello 설정을 쓴다.
    /// </summary>
    public sealed class CoopHost
    {
        sealed class Peer
        {
            public INetPipe Pipe;
            public int Slot = -1;
            public RunConfig Member;
            public bool Dropped;
        }

        readonly IContentDb _content;
        readonly Func<SimModuleSet> _modules;
        readonly List<Peer> _peers = new List<Peer>();
        readonly List<SimEvent> _evBuf = new List<SimEvent>();
        readonly int _maxPlayers;

        public SimWorld World { get; private set; }
        public bool Started => World != null;
        public int PlayerCount { get { int n = 0; foreach (var p in _peers) if (p.Slot >= 0 && !p.Dropped) n++; return n; } }

        /// <summary>modules = 세계마다 새 모듈 묶음(기본 SimModuleSet.CreateDefault).</summary>
        public CoopHost(IContentDb content, Func<SimModuleSet> modules = null, int maxPlayers = SimConst.MaxPlayers)
        {
            _content = content;
            _modules = modules ?? SimModuleSet.CreateDefault;
            _maxPlayers = Math.Max(1, Math.Min(maxPlayers, SimConst.MaxPlayers));
        }

        /// <summary>방장 = 남아 있는 가장 낮은 슬롯(없으면 −1).</summary>
        public int LeaderSlot
        {
            get
            {
                int best = -1;
                foreach (var p in _peers) if (p.Slot >= 0 && !p.Dropped && (best < 0 || p.Slot < best)) best = p.Slot;
                return best;
            }
        }

        /// <summary>판이 끝났고 남은 손님이 없다(전용 서버가 방을 새로 연다).</summary>
        public bool Finished
        {
            get
            {
                if (!Started) return false;
                if (World.Run.Phase != RunPhase.RunEnded && PlayerCount > 0) return false;
                return PlayerCount == 0;
            }
        }

        /// <summary>새 연결을 받는다. Hello가 오면 슬롯을 준다.</summary>
        public void Accept(INetPipe pipe)
        {
            if (pipe != null) _peers.Add(new Peer { Pipe = pipe });
        }

        /// <summary>로비의 판 시작(방장 단추). 들어온 사람이 없으면 false.</summary>
        public bool StartRun(ulong seed)
        {
            if (Started) return false;
            var members = new List<Peer>();
            foreach (var p in _peers) if (p.Slot >= 0 && !p.Dropped) members.Add(p);
            if (members.Count == 0) return false;
            members.Sort((a, b) => a.Slot.CompareTo(b.Slot));

            var cfg = NetCodec.Clone(members[0].Member);
            cfg.Seed = seed;
            cfg.Party = new List<RunConfig>();
            for (int i = 1; i < members.Count; i++) cfg.Party.Add(NetCodec.Clone(members[i].Member));

            var w = new SimWorld(_content, _modules(), seed);
            World = w;
            w.StartRun(cfg);

            var msg = new NetWriter(NetMsg.Start);
            NetCodec.WriteConfig(msg, cfg);
            Broadcast(msg.ToArray());
            SendSnapshot();
            FlushEvents();
            return true;
        }

        /// <summary>한 틱: 받은 메시지 처리 → (판 중이면) Step → 이벤트·스냅숏 보내기.</summary>
        public void Tick()
        {
            Pump();
            var w = World;
            if (w == null) return;
            if (w.Run != null && w.Run.Phase == RunPhase.RunEnded) { FlushEvents(); return; }
            w.Step();
            // 판 흐름이 바뀐 틱(전리품·야영·판 끝 …)은 스냅숏을 이벤트보다 먼저 보낸다: 손님 화면은 이벤트를 받는 순간의
            // 꼭두각시 상태(Run.Phase, CampSub, Loot)를 보고 화면을 열기 때문이다.
            _evBuf.Clear();
            w.DrainEvents(_evBuf);
            bool flow = false;
            for (int i = 0; i < _evBuf.Count && !flow; i++) flow = ChangesFlow(_evBuf[i].Type);
            if (flow || w.Tick % NetProtocol.SnapshotEveryTicks == 0) SendSnapshot();
            SendEvents(w.Tick);
        }

        static bool ChangesFlow(SimEventType t) =>
            t == SimEventType.PhaseChanged || t == SimEventType.CampChanged || t == SimEventType.RunEnded
            || t == SimEventType.WaveCleared || t == SimEventType.PlayerRevived || t == SimEventType.PlayerDowned || t == SimEventType.PlayerRose;

        // ------------------------------------------------------------ 받기

        void Pump()
        {
            for (int i = 0; i < _peers.Count; i++)
            {
                var p = _peers[i];
                if (p.Dropped) continue;
                if (!p.Pipe.Connected) { Drop(p); continue; }
                while (p.Pipe.TryReceive(out var data))
                {
                    try { Handle(p, data); }
                    catch (Exception) { Drop(p); break; }
                }
            }
        }

        void Handle(Peer p, byte[] data)
        {
            var r = new NetReader(data);
            var kind = (NetMsg)r.Byte();
            switch (kind)
            {
                case NetMsg.Hello:
                    OnHello(p, r);
                    break;
                case NetMsg.Input:
                    if (p.Slot < 0 || World == null) return;
                    {
                        int n = r.Int();
                        // 손님의 시계는 믿지 않는다: 받은 순서를 지키며 지금 틱의 시각으로 다시 찍는다.
                        long now = SimConst.TickStartU(World.Tick);
                        for (int i = 0; i < n; i++)
                        {
                            var e = NetCodec.ReadInput(r);
                            if (e.Key == LogicalKey.Pause) continue; // 일시정지는 온라인에 없다
                            World.EnqueueInput(p.Slot, new InputEvent(e.Key, e.Down, now));
                        }
                    }
                    break;
                case NetMsg.StartRequest:
                    {
                        ulong seed = r.ULong();
                        if (p.Slot >= 0 && p.Slot == LeaderSlot && !Started) StartRun(seed);
                    }
                    break;
                case NetMsg.Command:
                    if (p.Slot < 0 || World == null) return;
                    {
                        var c = NetCodec.ReadCommand(r);
                        c.Slot = p.Slot;
                        World.Enqueue(c);
                    }
                    break;
            }
        }

        void OnHello(Peer p, NetReader r)
        {
            int version = r.Int();
            if (version != NetProtocol.Version) { Reject(p, RejectReason.Version); return; }
            if (Started) { Reject(p, RejectReason.AlreadyStarted); return; }
            if (PlayerCount >= _maxPlayers) { Reject(p, RejectReason.Full); return; }
            RunConfig member;
            try { member = NetCodec.ReadConfig(r); }
            catch (Exception) { Reject(p, RejectReason.BadHello); return; }
            if (member == null || string.IsNullOrEmpty(member.CharId)) { Reject(p, RejectReason.BadHello); return; }
            member.Party = null;
            p.Member = member;
            p.Slot = FreeSlot();
            var msg = new NetWriter(NetMsg.Welcome);
            msg.Int(NetProtocol.Version);
            msg.Int(p.Slot);
            p.Pipe.Send(msg.ToArray());
            SendLobby();
        }

        int FreeSlot()
        {
            for (int s = 0; s < _maxPlayers; s++)
            {
                bool used = false;
                foreach (var q in _peers) if (q.Slot == s && !q.Dropped) used = true;
                if (!used) return s;
            }
            return -1;
        }

        void Reject(Peer p, RejectReason why)
        {
            var msg = new NetWriter(NetMsg.Reject);
            msg.Byte((byte)why);
            p.Pipe.Send(msg.ToArray());
            p.Pipe.Close();
            p.Dropped = true;
        }

        /// <summary>끊긴 손님: 판 중이면 그 캐릭터는 쓰러진 것으로 친다(전원이 쓰러지면 판이 끝난다). 판 전이면 로비에서 뺀다.</summary>
        void Drop(Peer p)
        {
            if (p.Dropped) return;
            p.Dropped = true;
            p.Pipe.Close();
            var w = World;
            if (w != null && p.Slot >= 0)
            {
                var pl = w.Players[p.Slot];
                w.InputQueues[p.Slot].Clear();
                if (pl.Active && !pl.Downed && w.Run.Phase != RunPhase.RunEnded)
                {
                    w.Mods.Player.ForceDown(w, pl, default(PlayerDamageSource));
                    w.Mods.Run.OnPlayerDowned(w, pl);
                }
            }
            else SendLobby();
        }

        // ------------------------------------------------------------ 보내기

        void Broadcast(byte[] data)
        {
            foreach (var p in _peers) if (p.Slot >= 0 && !p.Dropped) p.Pipe.Send(data);
        }

        void SendLobby()
        {
            var msg = new NetWriter(NetMsg.Lobby);
            int n = 0;
            foreach (var p in _peers) if (p.Slot >= 0 && !p.Dropped) n++;
            msg.Int(n);
            foreach (var p in _peers)
            {
                if (p.Slot < 0 || p.Dropped) continue;
                msg.Int(p.Slot);
                msg.Str(p.Member.CharId);
                msg.Int(p.Member.WandStage);
            }
            Broadcast(msg.ToArray());
        }

        void FlushEvents()
        {
            _evBuf.Clear();
            World.DrainEvents(_evBuf);
            SendEvents(World.Tick);
        }

        void SendEvents(int tick)
        {
            if (_evBuf.Count == 0) return;
            var msg = new NetWriter(NetMsg.Events);
            msg.Int(tick);
            msg.Int(_evBuf.Count);
            for (int i = 0; i < _evBuf.Count; i++) NetCodec.WriteEvent(msg, _evBuf[i]);
            Broadcast(msg.ToArray());
            _evBuf.Clear();
        }

        void SendSnapshot()
        {
            var msg = new NetWriter(NetMsg.Snapshot);
            WorldSnapshot.Write(msg, World);
            Broadcast(msg.ToArray());
        }
    }
}
