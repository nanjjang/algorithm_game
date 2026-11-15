using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 방 서버(11-online.md 3단계 뒤, "코드로 참가"). 서버 하나가 방 여러 개(협동·결투)와 광장 하나를 돌린다.
    /// 새 연결의 첫 메시지로 길을 나눈다:
    ///   CreateRoom(mode) → 6자리 코드의 새 방, RoomInfo 응답 → 그 방(CoopHost)에 연결을 넘긴다(뒤이은 Hello는 방이 받는다).
    ///   JoinRoom(code)   → 있고, 판 전이고, 자리가 있으면 RoomInfo → 넘긴다. 아니면 RoomError 뒤 끊는다.
    ///   EnterPlaza       → 광장(PlazaHost)에 넘긴다.
    /// 시계는 바깥(DedicatedServer·테스트)이 Tick으로 준다(60Hz). 끝난 방, 아무도 없는 방은 지운다.
    /// </summary>
    public sealed class RoomServer
    {
        /// <summary>헷갈리는 글자(0·O·1·I·L)를 뺀 코드 글자.</summary>
        public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const int CodeLength = 6;
        public const int MaxRooms = 200;
        /// <summary>판 전 빈 방을 지우기까지(틱). 만든 사람이 Hello를 보내기 전의 틈을 넉넉히 준다.</summary>
        public const int EmptyRoomTicks = 60 * 10;

        readonly IContentDb _content;
        readonly Func<SimModuleSet> _modules;
        readonly List<INetPipe> _pending = new List<INetPipe>();
        readonly Dictionary<string, CoopHost> _rooms = new Dictionary<string, CoopHost>();
        readonly List<string> _dead = new List<string>();
        readonly Random _rng;

        public PlazaHost Plaza { get; }
        public int RoomCount => _rooms.Count;
        public IEnumerable<CoopHost> Rooms => _rooms.Values;
        /// <summary>로그 한 줄(전용 서버가 Debug.Log로 찍는다).</summary>
        public Action<string> Log;

        public RoomServer(IContentDb content, Func<SimModuleSet> modules = null, int seed = 0)
        {
            _content = content;
            _modules = modules ?? SimModuleSet.CreateDefault;
            _rng = seed != 0 ? new Random(seed) : new Random();
            Plaza = new PlazaHost();
        }

        public CoopHost Room(string code) => code != null && _rooms.TryGetValue(code, out var h) ? h : null;

        public void Accept(INetPipe pipe)
        {
            if (pipe != null) _pending.Add(pipe);
        }

        public void Tick()
        {
            // 1. 아직 길이 정해지지 않은 연결: 첫 메시지를 본다.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var pipe = _pending[i];
                if (!pipe.Connected) { _pending.RemoveAt(i); continue; }
                if (!pipe.TryReceive(out var data)) continue;
                _pending.RemoveAt(i);
                Route(pipe, data);
            }
            // 2. 방·광장 한 틱.
            foreach (var kv in _rooms)
            {
                var h = kv.Value;
                h.Tick();
                if (!h.Started) h.IdleTicks = h.PlayerCount > 0 ? 0 : h.IdleTicks + 1;
                if (h.Finished || (!h.Started && h.IdleTicks > EmptyRoomTicks)) _dead.Add(kv.Key);
            }
            foreach (var code in _dead)
            {
                Log?.Invoke("[room-server] room " + code + " closed");
                _rooms.Remove(code);
            }
            _dead.Clear();
            Plaza.Tick();
        }

        void Route(INetPipe pipe, byte[] first)
        {
            NetReader r;
            NetMsg kind;
            try { r = new NetReader(first); kind = (NetMsg)r.Byte(); }
            catch (Exception) { Fail(pipe, RoomError.BadRequest); return; }
            switch (kind)
            {
                case NetMsg.CreateRoom:
                    {
                        if (_rooms.Count >= MaxRooms) { Fail(pipe, RoomError.ServerFull); return; }
                        RoomMode mode;
                        try { mode = (RoomMode)r.Byte(); } catch (Exception) { mode = RoomMode.Coop; }
                        if (mode != RoomMode.Coop && mode != RoomMode.Duel) mode = RoomMode.Coop;
                        var h = new CoopHost(_content, _modules, SimConst.MaxPlayers, mode) { Code = NewCode() };
                        _rooms[h.Code] = h;
                        Log?.Invoke("[room-server] room " + h.Code + " created (" + mode + ")");
                        SendInfo(pipe, h);
                        h.Accept(pipe);
                        break;
                    }
                case NetMsg.JoinRoom:
                    {
                        string code = null;
                        try { code = Normalize(r.Str()); } catch (Exception) { }
                        var h = Room(code);
                        if (h == null) { Fail(pipe, RoomError.NotFound); return; }
                        if (h.Started) { Fail(pipe, RoomError.Started); return; }
                        if (h.PlayerCount >= h.MaxPlayers) { Fail(pipe, RoomError.Full); return; }
                        SendInfo(pipe, h);
                        h.Accept(pipe);
                        break;
                    }
                case NetMsg.EnterPlaza:
                    Plaza.Accept(pipe, r);
                    break;
                case NetMsg.Hello:
                    // 옛 손님(방 코드 없이 바로 Hello): 협동 방을 하나 만들어 준다.
                    {
                        var h = new CoopHost(_content, _modules) { Code = NewCode() };
                        _rooms[h.Code] = h;
                        var replay = new ReplayPipe(pipe, first);
                        SendInfo(pipe, h);
                        h.Accept(replay);
                        break;
                    }
                default:
                    Fail(pipe, RoomError.BadRequest);
                    break;
            }
        }

        static void SendInfo(INetPipe pipe, CoopHost h)
        {
            var m = new NetWriter(NetMsg.RoomInfo);
            m.Str(h.Code);
            m.Byte((byte)h.Mode);
            m.Int(h.MaxPlayers);
            pipe.Send(m.ToArray());
        }

        static void Fail(INetPipe pipe, RoomError why)
        {
            var m = new NetWriter(NetMsg.RoomError);
            m.Byte((byte)why);
            pipe.Send(m.ToArray());
            pipe.Close();
        }

        string NewCode()
        {
            var c = new char[CodeLength];
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                for (int i = 0; i < c.Length; i++) c[i] = CodeAlphabet[_rng.Next(CodeAlphabet.Length)];
                var s = new string(c);
                if (!_rooms.ContainsKey(s)) return s;
            }
            throw new InvalidOperationException("no free room code");
        }

        /// <summary>대소문자·띄어쓰기를 무시한다("abc 234" → "ABC234").</summary>
        public static string Normalize(string code)
        {
            if (code == null) return null;
            var sb = new System.Text.StringBuilder(code.Length);
            foreach (char ch in code) if (!char.IsWhiteSpace(ch) && ch != '-') sb.Append(char.ToUpperInvariant(ch));
            return sb.ToString();
        }

        /// <summary>이미 꺼낸 첫 메시지를 한 번 더 내주는 통로(옛 손님의 Hello를 방에 넘길 때).</summary>
        sealed class ReplayPipe : INetPipe
        {
            readonly INetPipe _inner;
            byte[] _first;
            public ReplayPipe(INetPipe inner, byte[] first) { _inner = inner; _first = first; }
            public bool Connected => _inner.Connected;
            public void Send(byte[] message) => _inner.Send(message);
            public bool TryReceive(out byte[] message)
            {
                if (_first != null) { message = _first; _first = null; return true; }
                return _inner.TryReceive(out message);
            }
            public void Close() => _inner.Close();
        }
    }
}
