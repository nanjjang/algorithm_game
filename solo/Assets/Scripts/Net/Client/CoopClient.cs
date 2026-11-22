using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    public enum CoopClientState { Connecting, Lobby, InRun, Rejected, Disconnected }

    /// <summary>
    /// 협동 판의 클라이언트 쪽(11-online.md 3절). 판을 돌리지 않는다: 입력·명령을 서버에 보내고, 받은 스냅숏을 꼭두각시 세계
    /// (<see cref="World"/>, Step하지 않는다)에 덮어쓰고, 받은 표시 이벤트를 <see cref="DrainEvents"/>로 넘긴다.
    /// 꼭두각시 세계는 서버와 같은 판 설정으로 StartRun해 둔다 — 기획 정의, 조립에서 컴파일한 주문처럼 스냅숏이 나르지 않는 것을 제자리에 두기 위해서다.
    /// </summary>
    public sealed class CoopClient
    {
        public struct LobbyMember
        {
            public int Slot;
            public string CharId;
            public int WandStage;
            public bool IsBot;
            public string Name; // 계정 이름(없으면 null)
        }

        readonly IContentDb _content;
        readonly Func<SimModuleSet> _modules;
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly List<InputEvent> _outInputs = new List<InputEvent>();
        INetPipe _pipe;

        public CoopClientState State { get; private set; } = CoopClientState.Disconnected;
        public RejectReason RejectedBecause { get; private set; }
        /// <summary>서버가 준 내 슬롯(Welcome 전엔 −1).</summary>
        public int Slot { get; private set; } = -1;
        public SimWorld World { get; private set; }
        public RunConfig Config { get; private set; }
        public readonly List<LobbyMember> Lobby = new List<LobbyMember>();
        /// <summary>마지막으로 받은 스냅숏의 서버 틱(−1 = 아직 없음).</summary>
        public int LastSnapshotTick { get; private set; } = -1;
        public int SnapshotsReceived { get; private set; }
        /// <summary>마지막 스냅숏 뒤 흐른 60Hz 틱 수(OnlineCoop.Step이 센다).</summary>
        public int FramesSinceSnapshot;
        readonly SnapshotSmoother _smooth = new SnapshotSmoother();

        /// <summary>표시 보간 비(0~1): 앞 스냅숏 자리(PrevPos) → 지금 스냅숏 자리(Pos)를 스냅숏 간격 동안 잇는다.</summary>
        public float InterpAlpha => FramesSinceSnapshot >= NetProtocol.SnapshotEveryTicks ? 1f : (FramesSinceSnapshot + 1f) / NetProtocol.SnapshotEveryTicks;

        public CoopClient(IContentDb content, Func<SimModuleSet> modules = null)
        {
            _content = content;
            _modules = modules ?? SimModuleSet.CreateDefault;
        }

        /// <summary>서버에 붙는다. member = 내 출발 설정(캐릭터·지팡이·조립·기본 공격 유형).</summary>
        /// <summary>방 서버에 붙어 새 방을 만든다(코드는 RoomInfo로 온다).</summary>
        public void ConnectCreate(INetPipe pipe, RoomMode mode, RunConfig member)
        {
            var msg = new NetWriter(NetMsg.CreateRoom);
            msg.Byte((byte)mode);
            pipe.Send(msg.ToArray());
            Mode = mode;
            Connect(pipe, member);
        }

        /// <summary>방 서버에 붙어 코드의 방에 들어간다.</summary>
        public void ConnectJoin(INetPipe pipe, string code, RunConfig member)
        {
            var msg = new NetWriter(NetMsg.JoinRoom);
            msg.Str(code);
            pipe.Send(msg.ToArray());
            Connect(pipe, member);
        }

        /// <summary>방 코드(방 서버의 방이면, RoomInfo가 온 뒤).</summary>
        /// <summary>관전으로 들어왔다(입력·명령을 보내지 않는다. 화면은 슬롯 0의 눈으로 본다).</summary>
        public bool Spectator { get; private set; }

        /// <summary>그 방을 관전한다(11-online.md 6.14절). Welcome(슬롯 −1) 뒤 Lobby 또는 Start·Snapshot이 온다.</summary>
        public void ConnectSpectate(INetPipe pipe, string code)
        {
            _pipe = pipe;
            Spectator = true;
            State = CoopClientState.Connecting;
            var msg = new NetWriter(NetMsg.SpectateRoom);
            msg.Str(code);
            pipe.Send(msg.ToArray());
        }

        public string RoomCode { get; private set; }
        public RoomMode Mode { get; private set; }
        public int RoomMaxPlayers { get; private set; } = SimConst.MaxPlayers;
        public RoomError RoomErrorCode { get; private set; }
        /// <summary>계정으로 들어왔으면 서버 프로필(AuthOk).</summary>
        public ServerProfile Profile { get; private set; }
        public AuthError AuthFailed { get; private set; }

        public void Connect(INetPipe pipe, RunConfig member)
        {
            _pipe = pipe;
            State = CoopClientState.Connecting;
            var msg = new NetWriter(NetMsg.Hello);
            msg.Int(NetProtocol.Version);
            var m = NetCodec.Clone(member);
            m.Party = null;
            NetCodec.WriteConfig(msg, m);
            _pipe.Send(msg.ToArray());
        }

        public void Disconnect()
        {
            _pipe?.Close();
            State = CoopClientState.Disconnected;
        }

        // ------------------------------------------------------------ 보내기

        /// <summary>이번 프레임의 입력을 모아 둔다(<see cref="Flush"/>가 한 메시지로 보낸다).</summary>
        public void QueueInput(in InputEvent e)
        {
            if (State == CoopClientState.InRun && !Spectator) _outInputs.Add(e);
        }

        /// <summary>화면 명령(전리품·야영·포기 …)을 서버로. 꼭두각시 세계에는 넣지 않는다.</summary>
        public void SendCommand(in RunCommand c)
        {
            if (State != CoopClientState.InRun || _pipe == null || Spectator) return;
            var msg = new NetWriter(NetMsg.Command);
            NetCodec.WriteCommand(msg, c);
            _pipe.Send(msg.ToArray());
        }

        /// <summary>방장이면 서버에 판 시작을 청한다(방장이 아니면 서버가 무시한다).</summary>
        public void RequestStart(ulong seed)
        {
            if (State != CoopClientState.Lobby || _pipe == null) return;
            var msg = new NetWriter(NetMsg.StartRequest);
            msg.ULong(seed);
            _pipe.Send(msg.ToArray());
        }

        /// <summary>방장이면 서버에 봇을 청한다(member = 봇의 출발 설정).</summary>
        public void RequestAddBot(RunConfig member)
        {
            if (State != CoopClientState.Lobby || _pipe == null || member == null) return;
            var msg = new NetWriter(NetMsg.AddBot);
            var m = NetCodec.Clone(member);
            m.Party = null;
            NetCodec.WriteConfig(msg, m);
            _pipe.Send(msg.ToArray());
        }

        public void RequestRemoveBot()
        {
            if (State != CoopClientState.Lobby || _pipe == null) return;
            _pipe.Send(new NetWriter(NetMsg.RemoveBot).ToArray());
        }

        public void Flush()
        {
            if (_outInputs.Count == 0 || _pipe == null) return;
            var msg = new NetWriter(NetMsg.Input);
            msg.Int(_outInputs.Count);
            for (int i = 0; i < _outInputs.Count; i++) NetCodec.WriteInput(msg, _outInputs[i]);
            _pipe.Send(msg.ToArray());
            _outInputs.Clear();
        }

        // ------------------------------------------------------------ 받기

        /// <summary>받은 메시지를 모두 처리한다. 프레임마다 부른다.</summary>
        public void Pump()
        {
            if (_pipe == null) return;
            while (_pipe.TryReceive(out var data)) Handle(data);
            if (!_pipe.Connected && State != CoopClientState.Rejected) State = CoopClientState.Disconnected;
        }

        /// <summary>받아 둔 표시 이벤트를 넘기고 비운다(뷰의 OnEvent에 그대로 준다).</summary>
        public void DrainEvents(List<SimEvent> into)
        {
            into.AddRange(_events);
            _events.Clear();
        }

        void Handle(byte[] data)
        {
            var r = new NetReader(data);
            switch ((NetMsg)r.Byte())
            {
                case NetMsg.Welcome:
                    r.Int();
                    Slot = r.Int();
                    if (Slot < 0) { Spectator = true; Slot = 0; }
                    State = CoopClientState.Lobby;
                    break;
                case NetMsg.AuthOk:
                    r.Long();
                    Profile = NetCodec.ReadProfile(r);
                    break;
                case NetMsg.AuthFail:
                    AuthFailed = (AuthError)r.Byte();
                    State = CoopClientState.Rejected;
                    break;
                case NetMsg.RoomInfo:
                    RoomCode = r.Str();
                    Mode = (RoomMode)r.Byte();
                    RoomMaxPlayers = r.Int();
                    break;
                case NetMsg.RoomError:
                    RoomErrorCode = (RoomError)r.Byte();
                    State = CoopClientState.Rejected;
                    break;
                case NetMsg.Reject:
                    RejectedBecause = (RejectReason)r.Byte();
                    State = CoopClientState.Rejected;
                    break;
                case NetMsg.Lobby:
                    {
                        Lobby.Clear();
                        int n = r.Int();
                        for (int i = 0; i < n; i++) Lobby.Add(new LobbyMember { Slot = r.Int(), CharId = r.Str(), WandStage = r.Int(), IsBot = r.Bool(), Name = r.Str() });
                    }
                    break;
                case NetMsg.Start:
                    {
                        Config = NetCodec.ReadConfig(r);
                        var w = new SimWorld(_content, _modules(), Config.Seed);
                        w.StartRun(NetCodec.Clone(Config));
                        w.Events.Clear(); // 서버가 판 시작 이벤트를 따로 보낸다
                        World = w;
                        State = CoopClientState.InRun;
                    }
                    break;
                case NetMsg.Snapshot:
                    if (World == null) return;
                    {
                        byte[] raw = NetCodec.Inflate(r.Bytes());
                        _smooth.Capture(World);
                        WorldSnapshot.Apply(new NetReader(raw), World);
                        _smooth.Restore(World);
                        FramesSinceSnapshot = 0;
                    }
                    LastSnapshotTick = World.Tick;
                    SnapshotsReceived++;
                    break;
                case NetMsg.Events:
                    {
                        r.Int();
                        int n = r.Int();
                        for (int i = 0; i < n; i++) _events.Add(NetCodec.ReadEvent(r));
                    }
                    break;
            }
        }
    }
}
