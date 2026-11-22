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
            public CoopBot Bot; // 서버 안 봇이면 그 봇(아니면 null)
            public long AccountId; // 계정으로 들어왔으면 그 id(아니면 0)
            public string AccountName;
            public bool Spectator; // 관전자: 슬롯 없이 상태만 받는다
        }

        /// <summary>자동 매치 방(결투 점수에 반영). 리그 제한을 걸고, 정해진 두 계정만 들어오고, 둘이 차면 서버가 시작한다.</summary>
        public bool Ranked;
        public int League;
        /// <summary>들어올 수 있는 계정(null = 누구나).</summary>
        public HashSet<long> Reserved;
        /// <summary>서버가 다시 만든 출발 설정에 마지막으로 거는 것(리그 제한).</summary>
        public Func<RunConfig, RunConfig> MemberFilter;
        /// <summary>자리가 다 차면 이 씨앗으로 바로 시작한다(0 = 방장이 시작).</summary>
        public ulong AutoStartSeed;
        /// <summary>방이 열린 뒤 지난 틱(방 서버가 센다).</summary>
        public int AgeTicks;
        public int SpectatorsMax = 6;
        readonly bool[] _leftDuringRun = new bool[SimConst.MaxPlayers];
        byte[] _startMsg;

        /// <summary>그 슬롯 사람이 판 도중에 끊겼나(자동 매치 이탈).</summary>
        public bool LeftDuringRun(int slot) => slot >= 0 && slot < _leftDuringRun.Length && _leftDuringRun[slot];

        /// <summary>그 슬롯의 출발 설정(없으면 null).</summary>
        public RunConfig MemberOfSlot(int slot)
        {
            foreach (var p in _peers) if (p.Slot == slot && p.Member != null) return p.Member;
            return null;
        }

        /// <summary>판에 들어온 사람 수(봇 포함, 끊긴 사람 포함 — 판이 끝난 뒤 세는 용도).</summary>
        public int MemberCount { get { int n = 0; foreach (var p in _peers) if (p.Slot >= 0 && p.Member != null) n++; return n; } }

        public int SpectatorCount { get { int n = 0; foreach (var p in _peers) if (p.Spectator && !p.Dropped) n++; return n; } }

        /// <summary>관전자를 들인다(가득이면 false). 판 중이면 판 설정과 지금 상태를 바로 보낸다.</summary>
        public bool AcceptSpectator(INetPipe pipe)
        {
            if (pipe == null || SpectatorCount >= SpectatorsMax) return false;
            var p = new Peer { Pipe = pipe, Spectator = true };
            _peers.Add(p);
            var msg = new NetWriter(NetMsg.Welcome);
            msg.Int(NetProtocol.Version);
            msg.Int(-1);
            pipe.Send(msg.ToArray());
            if (Started)
            {
                if (_startMsg != null) pipe.Send(_startMsg);
                pipe.Send(SnapshotMessage());
            }
            else SendLobby();
            return true;
        }

        /// <summary>방을 닫을 때 남은 연결(관전자 포함)을 끊는다.</summary>
        public void CloseAll()
        {
            foreach (var p in _peers) if (!p.Dropped) { p.Dropped = true; p.Pipe.Close(); }
        }

        readonly IContentDb _content;
        readonly Func<SimModuleSet> _modules;
        readonly List<Peer> _peers = new List<Peer>();
        readonly List<SimEvent> _evBuf = new List<SimEvent>();
        readonly int _maxPlayers;

        public SimWorld World { get; private set; }
        /// <summary>마지막으로 보낸 스냅숏 몸의 크기(압축 뒤, 바이트).</summary>
        public int LastSnapshotBytes { get; private set; }
        public bool Started => World != null;
        public int PlayerCount { get { int n = 0; foreach (var p in _peers) if (p.Slot >= 0 && !p.Dropped) n++; return n; } }

        /// <summary>modules = 세계마다 새 모듈 묶음(기본 SimModuleSet.CreateDefault).</summary>
        /// <summary>협동(최대 4) 또는 결투(2명). 결투는 두 사람이 다 있어야 시작한다.</summary>
        public readonly RoomMode Mode;
        /// <summary>방 서버가 준 코드(이 기기 방이면 null).</summary>
        public string Code;
        public int MaxPlayers => _maxPlayers;
        /// <summary>(계정 id, 캐릭터 1~5) → 서버 프로필의 지팡이 단계. 방 서버가 넣는다(없으면 0).</summary>
        public Func<long, int, int> StageLookup;
        /// <summary>(계정 id, 캐릭터) → 서버 지팡이의 변이 id들.</summary>
        public Func<long, int, List<string>> MutationLookup;
        /// <summary>(계정 id, 캐릭터) → 서버 지팡이 조립.</summary>
        public Func<long, int, LoadoutSpec> LoadoutLookup;
        /// <summary>판 전 마지막으로 누가 있었던 틱 수(빈 방 정리용, 방 서버가 센다).</summary>
        public int IdleTicks;

        public CoopHost(IContentDb content, Func<SimModuleSet> modules = null, int maxPlayers = SimConst.MaxPlayers, RoomMode mode = RoomMode.Coop)
        {
            Mode = mode;
            if (mode == RoomMode.Duel) maxPlayers = 2;
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

        /// <summary>서버 안에 동료 봇을 하나 세운다(판 전, 자리가 남을 때). 봇은 루프백 손님이라 사람과 같은 규칙으로 들어온다.</summary>
        public bool AddBot(RunConfig member)
        {
            if (Started || member == null || PlayerCount >= _maxPlayers) return false;
            LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
            var peer = new Peer { Pipe = serverEnd };
            var client = new CoopClient(_content, _modules);
            peer.Bot = new CoopBot(client);
            _peers.Add(peer);
            client.Connect(clientEnd, member);
            Pump();
            client.Pump();
            return true;
        }

        /// <summary>가장 나중에 넣은 봇을 뺀다(판 전).</summary>
        public bool RemoveBot()
        {
            if (Started) return false;
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                var p = _peers[i];
                if (p.Bot == null || p.Dropped) continue;
                p.Bot.Client.Disconnect();
                Drop(p);
                return true;
            }
            return false;
        }

        public int BotCount { get { int n = 0; foreach (var p in _peers) if (p.Bot != null && !p.Dropped) n++; return n; } }

        /// <summary>새 연결을 받는다. Hello가 오면 슬롯을 준다.</summary>
        public void Accept(INetPipe pipe, AccountRecord account = null)
        {
            if (pipe != null) _peers.Add(new Peer { Pipe = pipe, AccountId = account != null ? account.Id : 0, AccountName = account != null ? account.Name : null });
        }

        public bool HasAccount(long id)
        {
            if (id <= 0) return false;
            foreach (var p in _peers) if (p.AccountId == id && !p.Dropped) return true;
            return false;
        }

        /// <summary>그 슬롯 사람의 계정 id(봇·계정 없음·빈 자리 = 0).</summary>
        public long AccountOfSlot(int slot)
        {
            foreach (var p in _peers) if (p.Slot == slot && p.Bot == null) return p.AccountId;
            return 0;
        }

        /// <summary>로비의 판 시작(방장 단추). 들어온 사람이 없으면 false.</summary>
        public bool StartRun(ulong seed)
        {
            if (Started) return false;
            var members = new List<Peer>();
            foreach (var p in _peers) if (p.Slot >= 0 && !p.Dropped) members.Add(p);
            if (members.Count == 0) return false;
            if (Mode == RoomMode.Duel && members.Count != 2) return false;
            members.Sort((a, b) => a.Slot.CompareTo(b.Slot));

            var cfg = NetCodec.Clone(members[0].Member);
            cfg.Seed = seed;
            if (Mode == RoomMode.Duel)
            {
                cfg.Mode = RunMode.Duel;
                cfg.StartWave = 1;
            }
            else if (cfg.Mode == RunMode.Duel) cfg.Mode = RunMode.Endless;
            cfg.Party = new List<RunConfig>();
            for (int i = 1; i < members.Count; i++) cfg.Party.Add(NetCodec.Clone(members[i].Member));

            var w = new SimWorld(_content, _modules(), seed);
            World = w;
            w.StartRun(cfg);

            var msg = new NetWriter(NetMsg.Start);
            NetCodec.WriteConfig(msg, cfg);
            _startMsg = msg.ToArray();
            Broadcast(_startMsg);
            SendSnapshot();
            FlushEvents();
            return true;
        }

        /// <summary>한 틱: 받은 메시지 처리 → (판 중이면) Step → 이벤트·스냅숏 보내기.</summary>
        public void Tick()
        {
            // 서버 안 봇: 받은 것 처리 → 이번 틱 입력 보내기(사람 손님과 같은 길로 들어온다).
            foreach (var p in _peers) if (p.Bot != null && !p.Dropped) { p.Bot.Client.Pump(); p.Bot.Act(); }
            Pump();
            if (AutoStartSeed != 0 && !Started && PlayerCount >= _maxPlayers) StartRun(AutoStartSeed);
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
                    if (p.Spectator) continue; // 관전자가 보낸 것은 읽고 버린다
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
                case NetMsg.AddBot:
                    {
                        RunConfig cfg = null;
                        try { cfg = NetCodec.ReadConfig(r); } catch (Exception) { }
                        if (p.Slot >= 0 && p.Slot == LeaderSlot && cfg != null && !string.IsNullOrEmpty(cfg.CharId))
                            AddBot(OnlineMember.Build(_content, Ids.Char.NumberOf(cfg.CharId), 0, cfg.AttackStyle));
                    }
                    break;
                case NetMsg.RemoveBot:
                    if (p.Slot >= 0 && p.Slot == LeaderSlot) RemoveBot();
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
            // 출발 설정은 서버가 다시 만든다: 캐릭터·기본 공격 유형만 손님 것, 지팡이 단계는 서버 프로필(OnlineMember).
            int ch = Ids.Char.NumberOf(member.CharId);
            int stage = p.AccountId > 0 && StageLookup != null ? StageLookup(p.AccountId, ch) : 0;
            member = OnlineMember.Build(_content, ch, stage, member.AttackStyle, p.AccountId > 0 && LoadoutLookup != null ? LoadoutLookup(p.AccountId, ch) : null);
            if (p.AccountId > 0 && MutationLookup != null) member.Mutations = MutationLookup(p.AccountId, ch) ?? new List<string>();
            if (MemberFilter != null) member = MemberFilter(member) ?? member;
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
            if (p.Spectator) return;
            var w = World;
            if (w != null && p.Slot >= 0)
            {
                if (w.Run != null && w.Run.Phase != RunPhase.RunEnded) _leftDuringRun[p.Slot] = true;
                var pl = w.Players[p.Slot];
                w.InputQueues[p.Slot].Clear();
                if (w.Run.Config != null && w.Run.Config.Mode == RunMode.Duel) w.Mods.Run.ForfeitDuel(w, p.Slot);
                else if (pl.Active && !pl.Downed && w.Run.Phase != RunPhase.RunEnded)
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
            foreach (var p in _peers) if ((p.Slot >= 0 || p.Spectator) && !p.Dropped) p.Pipe.Send(data);
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
                msg.Bool(p.Bot != null);
                msg.Str(p.AccountName);
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

        void SendSnapshot() => Broadcast(SnapshotMessage());

        byte[] SnapshotMessage()
        {
            // 몸은 Deflate로 줄여 보낸다(판 상태 약 5KB → 1~2KB). 마지막 크기는 LastSnapshotBytes.
            var body = new NetWriter();
            WorldSnapshot.Write(body, World);
            byte[] packed = NetCodec.Deflate(body.ToArray());
            var msg = new NetWriter(NetMsg.Snapshot);
            msg.Bytes(packed);
            LastSnapshotBytes = packed.Length;
            return msg.ToArray();
        }
    }
}
