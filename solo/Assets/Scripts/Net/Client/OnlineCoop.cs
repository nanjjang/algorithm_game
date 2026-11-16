using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 협동 방 하나의 연결 묶음(세션이 보는 <see cref="IOnlineLink"/>). 2단계: 이 기기가 방장 서버(<see cref="Host"/>)를 같이 돌리고
    /// 나(<see cref="Me"/>)와 동료 봇이 루프백으로 붙는다. 3단계에서 Host 없이 원격 서버에 붙는 길을 더한다(Me의 통로만 바뀐다).
    /// </summary>
    public sealed class OnlineCoop : IOnlineLink
    {
        public CoopHost Host { get; private set; }
        public CoopClient Me { get; private set; }
        readonly IContentDb _content;


        OnlineCoop(IContentDb content) { _content = content; }

        /// <summary>이 기기에 방을 만들고 내가 슬롯 0으로 들어간다.</summary>
        public static OnlineCoop HostLocal(IContentDb content, RunConfig me, RoomMode mode = RoomMode.Coop)
        {
            var o = new OnlineCoop(content);
            o.Host = new CoopHost(content, null, SimConst.MaxPlayers, mode);
            LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
            o.Host.Accept(serverEnd);
            o.Me = new CoopClient(content);
            o.Me.Connect(clientEnd, me);
            o.Pump();
            return o;
        }

        /// <summary>방 서버에 새 방을 만든다(코드는 Me.RoomCode). 서버에 못 붙으면 null.</summary>
        public static OnlineCoop CreateRoom(IContentDb content, RoomMode mode, RunConfig me)
        {
            var pipe = OnlineServer.ConnectAuthed(content);
            if (pipe == null) return null;
            var o = new OnlineCoop(content);
            o.Me = new CoopClient(content);
            o.Me.ConnectCreate(pipe, mode, me);
            return o;
        }

        /// <summary>방 서버의 코드 방에 들어간다. 서버에 못 붙으면 null(코드가 틀리면 Me.State = Rejected, RoomErrorCode).</summary>
        public static OnlineCoop JoinCode(IContentDb content, string code, RunConfig me)
        {
            var pipe = OnlineServer.ConnectAuthed(content);
            if (pipe == null) return null;
            var o = new OnlineCoop(content);
            o.Me = new CoopClient(content);
            o.Me.ConnectJoin(pipe, RoomServer.Normalize(code), me);
            return o;
        }

        /// <summary>방 서버의 결투 방을 관전한다(11-online.md 6.14절). 서버에 못 붙으면 null.</summary>
        public static OnlineCoop Spectate(IContentDb content, string code)
        {
            var pipe = OnlineServer.ConnectAuthed(content);
            if (pipe == null) return null;
            var o = new OnlineCoop(content);
            o.Me = new CoopClient(content);
            o.Me.ConnectSpectate(pipe, RoomServer.Normalize(code));
            return o;
        }

        public string RoomCode => Me != null ? Me.RoomCode : null;
        public RoomMode Mode => Host != null ? Host.Mode : (Me != null ? Me.Mode : RoomMode.Coop);

        /// <summary>원격 서버(전용 서버)에 붙는다. 실패하면 null.</summary>
        public static OnlineCoop JoinRemote(IContentDb content, string host, int port, RunConfig me)
        {
            var pipe = TcpPipe.Connect(host, port);
            if (pipe == null) return null;
            var o = new OnlineCoop(content);
            o.Me = new CoopClient(content);
            o.Me.Connect(pipe, me);
            return o;
        }

        /// <summary>이 기기가 방장 서버를 같이 돌리나(봇을 더할 수 있다).</summary>
        public bool IsLocalHost => Host != null;

        /// <summary>내가 방장인가(로비의 가장 낮은 슬롯).</summary>
        public bool IsLeader
        {
            get
            {
                if (Me == null || Me.Slot < 0) return false;
                foreach (var m in Me.Lobby) if (m.Slot < Me.Slot) return false;
                return true;
            }
        }

        /// <summary>동료 봇을 하나 더한다. 이 기기 방이면 서버에 바로, 원격 방이면 서버에 청한다(방장만). 봇은 서버 안에서 돈다.</summary>
        public bool AddBot(RunConfig member)
        {
            if (Host != null)
            {
                bool ok = Host.AddBot(member);
                Pump();
                return ok;
            }
            if (!IsLeader || Me == null) return false;
            Me.RequestAddBot(member);
            return true;
        }

        /// <summary>마지막 봇을 뺀다.</summary>
        public void RemoveBot()
        {
            if (Host != null) { Host.RemoveBot(); Pump(); return; }
            if (IsLeader) Me?.RequestRemoveBot();
        }

        /// <summary>로비의 봇 수(로비 목록 기준).</summary>
        public int BotCount { get { int n = 0; if (Me != null) foreach (var m in Me.Lobby) if (m.IsBot) n++; return n; } }
        /// <summary>로비의 사람·봇 수.</summary>
        public int MemberCount => Me != null ? Me.Lobby.Count : 0;

        // ------------------------------------------------------------ 이 기기 방을 다른 기기에 열기

        TcpPipeListener _listener;
        public bool OpenToNetwork => _listener != null;
        public int NetworkPort => _listener != null ? _listener.Port : 0;

        /// <summary>이 기기 방에 다른 기기가 TCP로 들어오게 연다(전용 서버 없이 방장 기기가 서버 노릇).</summary>
        public bool OpenNetwork(int port)
        {
            if (Host == null) return false;
            if (_listener != null) return true;
            try { _listener = new TcpPipeListener(port, bindAll: true); }
            catch (System.Exception) { _listener = null; return false; }
            return true;
        }

        void AcceptNetwork()
        {
            if (_listener == null || Host == null) return;
            TcpPipe p;
            while ((p = _listener.Poll()) != null) Host.Accept(p);
        }

        /// <summary>로비 동안 프레임마다: 메시지만 주고받는다(판은 아직 없다).</summary>
        public void Pump()
        {
            AcceptNetwork();
            Host?.Tick();
            Me?.Pump();
            Host?.Tick();
            Me?.Pump();
        }

        /// <summary>방장의 시작 단추. 이 기기 서버면 바로 열고(true면 세션 StartOnline), 원격이면 서버에 청한다
        /// (판은 Start 메시지가 오면 열린다 — <see cref="World"/>가 생기는 것을 본다).</summary>
        public bool StartRun(ulong seed)
        {
            if (Host == null)
            {
                Me?.RequestStart(seed);
                return false;
            }
            if (!Host.StartRun(seed)) return false;
            Me.Pump();
            return Me.World != null;
        }

        // ------------------------------------------------------------ IOnlineLink

        public SimWorld World => Me != null ? Me.World : null;
        public int Slot => Me != null ? Me.Slot : 0;
        public bool Connected => Me != null && (Me.State == CoopClientState.Connecting || Me.State == CoopClientState.Lobby || Me.State == CoopClientState.InRun);

        public void Send(List<InputEvent> inputs, List<RunCommand> commands)
        {
            if (Me == null) return;
            for (int i = 0; i < inputs.Count; i++) Me.QueueInput(inputs[i]);
            for (int i = 0; i < commands.Count; i++) Me.SendCommand(commands[i]);
        }

        public void Step()
        {
            if (Me == null) return;
            Me.FramesSinceSnapshot++;
            Me.Flush();
            AcceptNetwork();
            Host?.Tick();
            Me.Pump();
        }

        public void DrainEvents(List<SimEvent> into) => Me?.DrainEvents(into);

        public float InterpAlpha => Me != null ? Me.InterpAlpha : 1f;

        public void Leave()
        {
            Me?.Disconnect();
            _listener?.Stop();
            _listener = null;
            Host = null;
        }
    }

    /// <summary>
    /// 방장 기기 안의 동료 봇(2단계 확인용). 자기 꼭두각시 세계만 보고 가장 가까운 적에게 걸어가며 J를 누른다.
    /// 전리품·야영은 고르지 않는다(방장이 고른다).
    /// </summary>
    public sealed class CoopBot
    {
        public readonly CoopClient Client;
        readonly bool[] _keys = new bool[5];

        public CoopBot(CoopClient client) { Client = client; }

        void Key(LogicalKey key, bool down)
        {
            int k = key == LogicalKey.Fire ? 4 : (int)key;
            if (_keys[k] == down) return;
            _keys[k] = down;
            Client.QueueInput(new InputEvent(key, down, 0));
        }

        void ReleaseAll()
        {
            Key(LogicalKey.Fire, false);
            for (int i = 0; i < 4; i++) Key((LogicalKey)i, false);
        }

        /// <summary>한 틱의 입력을 정해 보낸다.</summary>
        public void Act()
        {
            var w = Client.World;
            if (w == null || Client.Slot < 0) return;
            var p = w.Players[Client.Slot];
            bool duel = w.Run != null && w.Run.Config != null && w.Run.Config.Mode == RunMode.Duel;
            bool fight = w.Run != null && (w.Run.Phase == RunPhase.WavePrelude || w.Run.Phase == RunPhase.WaveCombat || w.Run.Phase == RunPhase.DuelFight);
            if (!fight || !p.Active || p.Downed) { ReleaseAll(); Client.Flush(); return; }
            // 결투면 상대 사람, 아니면 가장 가까운 적을 노린다.
            bool has = false;
            Vector2 target = default;
            if (duel)
            {
                var o = w.Players[Client.Slot == 0 ? 1 : 0];
                if (o.Active && !o.Downed) { has = true; target = o.Pos; }
            }
            else
            {
                float bd = float.MaxValue;
                foreach (var e in w.Enemies)
                {
                    if (!e.Targetable) continue;
                    float d = (e.Pos - p.Pos).sqrMagnitude;
                    if (d < bd) { bd = d; target = e.Pos; has = true; }
                }
            }
            Key(LogicalKey.Fire, has);
            if (!has) { for (int i = 0; i < 4; i++) Key((LogicalKey)i, false); Client.Flush(); return; }
            Vector2 v = target - p.Pos;
            bool h = Mathf.Abs(v.x) > Mathf.Abs(v.y);
            bool close = v.sqrMagnitude < 2.5f * 2.5f;
            bool blink = (w.Tick & 1) == 0;
            Key(LogicalKey.Left, h && v.x < 0 && (!close || blink));
            Key(LogicalKey.Right, h && v.x > 0 && (!close || blink));
            Key(LogicalKey.Up, !h && v.y < 0 && (!close || blink));
            Key(LogicalKey.Down, !h && v.y > 0 && (!close || blink));
            Client.Flush();
        }
    }
}
