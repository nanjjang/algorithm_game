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
        readonly List<CoopBot> _bots = new List<CoopBot>();
        readonly IContentDb _content;

        public IReadOnlyList<CoopBot> Bots => _bots;

        OnlineCoop(IContentDb content) { _content = content; }

        /// <summary>이 기기에 방을 만들고 내가 슬롯 0으로 들어간다.</summary>
        public static OnlineCoop HostLocal(IContentDb content, RunConfig me)
        {
            var o = new OnlineCoop(content);
            o.Host = new CoopHost(content);
            LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
            o.Host.Accept(serverEnd);
            o.Me = new CoopClient(content);
            o.Me.Connect(clientEnd, me);
            o.Pump();
            return o;
        }

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

        /// <summary>동료 봇을 하나 더한다(방장 기기 안). 가득이면 false.</summary>
        public bool AddBot(RunConfig member)
        {
            if (Host == null || Host.Started || Host.PlayerCount >= SimConst.MaxPlayers) return false;
            LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
            Host.Accept(serverEnd);
            var c = new CoopClient(_content);
            c.Connect(clientEnd, member);
            _bots.Add(new CoopBot(c));
            Pump();
            return true;
        }

        /// <summary>마지막 봇을 뺀다.</summary>
        public void RemoveBot()
        {
            if (_bots.Count == 0 || Host == null || Host.Started) return;
            _bots[_bots.Count - 1].Client.Disconnect();
            _bots.RemoveAt(_bots.Count - 1);
            Pump();
        }

        /// <summary>로비 동안 프레임마다: 메시지만 주고받는다(판은 아직 없다).</summary>
        public void Pump()
        {
            Host?.Tick();
            Me?.Pump();
            for (int i = 0; i < _bots.Count; i++) _bots[i].Client.Pump();
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
            for (int i = 0; i < _bots.Count; i++) _bots[i].Client.Pump();
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
            Me.Flush();
            for (int i = 0; i < _bots.Count; i++) _bots[i].Act();
            Host?.Tick();
            Me.Pump();
            for (int i = 0; i < _bots.Count; i++) _bots[i].Client.Pump();
        }

        public void DrainEvents(List<SimEvent> into) => Me?.DrainEvents(into);

        public void Leave()
        {
            for (int i = 0; i < _bots.Count; i++) _bots[i].Client.Disconnect();
            _bots.Clear();
            Me?.Disconnect();
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
            bool fight = w.Run != null && (w.Run.Phase == RunPhase.WavePrelude || w.Run.Phase == RunPhase.WaveCombat);
            if (!fight || !p.Active || p.Downed) { ReleaseAll(); Client.Flush(); return; }
            EnemyState best = null;
            float bd = float.MaxValue;
            foreach (var e in w.Enemies)
            {
                if (!e.Targetable) continue;
                float d = (e.Pos - p.Pos).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            Key(LogicalKey.Fire, best != null);
            if (best == null) { for (int i = 0; i < 4; i++) Key((LogicalKey)i, false); Client.Flush(); return; }
            Vector2 v = best.Pos - p.Pos;
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
