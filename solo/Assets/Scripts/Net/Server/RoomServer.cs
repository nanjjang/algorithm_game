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
        /// <summary>계정 확인을 마친 연결 → 그 계정(다음 메시지로 길을 나눈다).</summary>
        readonly Dictionary<INetPipe, AccountRecord> _authed = new Dictionary<INetPipe, AccountRecord>();
        readonly HashSet<CoopHost> _recorded = new HashSet<CoopHost>();
        public AccountStore Accounts { get; }
        readonly Dictionary<string, CoopHost> _rooms = new Dictionary<string, CoopHost>();
        readonly List<string> _dead = new List<string>();
        readonly Random _rng;

        public PlazaHost Plaza { get; }
        public int RoomCount => _rooms.Count;
        public IEnumerable<CoopHost> Rooms => _rooms.Values;
        /// <summary>로그 한 줄(전용 서버가 Debug.Log로 찍는다).</summary>
        public Action<string> Log;

        public RoomServer(IContentDb content, Func<SimModuleSet> modules = null, int seed = 0, AccountStore accounts = null)
        {
            Accounts = accounts ?? new AccountStore(null);
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
                if (TryAuth(pipe, data)) continue; // 계정 메시지면 연결은 그대로 기다린다
                if (TryService(pipe, data)) continue; // 강화·순위표: 답하고 연결은 그대로
                _pending.RemoveAt(i);
                _authed.TryGetValue(pipe, out var acct);
                _authed.Remove(pipe);
                Route(pipe, data, acct);
            }
            // 2. 방·광장 한 틱.
            foreach (var kv in _rooms)
            {
                var h = kv.Value;
                h.Tick();
                RecordIfEnded(h);
                if (!h.Started) h.IdleTicks = h.PlayerCount > 0 ? 0 : h.IdleTicks + 1;
                if (h.Finished || (!h.Started && h.IdleTicks > EmptyRoomTicks)) _dead.Add(kv.Key);
            }
            foreach (var code in _dead)
            {
                Log?.Invoke("[room-server] room " + code + " closed");
                _recorded.Remove(_rooms[code]);
                _rooms.Remove(code);
            }
            _dead.Clear();
            Plaza.Tick();
        }

        /// <summary>Register·Login이면 처리하고 true(실패면 끊는다).</summary>
        bool TryAuth(INetPipe pipe, byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            var kind = (NetMsg)data[0];
            if (kind != NetMsg.Register && kind != NetMsg.Login) return false;
            AccountRecord rec = null;
            AuthError err = AuthError.WrongLogin;
            try
            {
                var r = new NetReader(data);
                r.Byte();
                if (kind == NetMsg.Register)
                {
                    string name = r.Str(), secret = r.Str();
                    var res = Accounts.Register(name, secret, out rec);
                    if (res == AccountStore.RegisterResult.BadName) err = AuthError.BadName;
                    else if (res == AccountStore.RegisterResult.NameTaken) err = AuthError.NameTaken;
                    else if (res == AccountStore.RegisterResult.BadSecret) err = AuthError.BadSecret;
                    if (rec != null) Log?.Invoke("[room-server] account " + rec.Id + " registered");
                }
                else
                {
                    long id = r.Long();
                    rec = Accounts.Login(id, r.Str());
                }
            }
            catch (Exception) { rec = null; }
            if (rec == null)
            {
                var f = new NetWriter(NetMsg.AuthFail);
                f.Byte((byte)err);
                pipe.Send(f.ToArray());
                pipe.Close();
                _pending.Remove(pipe);
                return true;
            }
            _authed[pipe] = rec;
            var ok = new NetWriter(NetMsg.AuthOk);
            ok.Long(rec.Id);
            NetCodec.WriteProfile(ok, rec.Profile);
            pipe.Send(ok.ToArray());
            return true;
        }

        readonly Random _dice = new Random();

        bool TryService(INetPipe pipe, byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            var kind = (NetMsg)data[0];
            if (kind == NetMsg.LeaderboardRequest)
            {
                var top = Accounts.Top(20);
                var m = new NetWriter(NetMsg.LeaderboardReply);
                m.Int(top.Count);
                foreach (var p in top) NetCodec.WriteProfile(m, p);
                pipe.Send(m.ToArray());
                return true;
            }
            if (kind == NetMsg.EnhanceRequest)
            {
                int ch = 0;
                bool charm = false;
                try { var r = new NetReader(data); r.Byte(); ch = r.Int(); charm = r.Bool(); } catch (Exception) { }
                _authed.TryGetValue(pipe, out var acct);
                var m = new NetWriter(NetMsg.EnhanceReply);
                Wizard.Solo.Meta.EnhanceOutcome outcome = Wizard.Solo.Meta.EnhanceOutcome.FailNoDrop;
                int before = acct != null ? Accounts.StageOf(acct.Id, ch) : 0;
                var res = acct == null ? AccountStore.EnhanceResult.NoAccount
                    : Accounts.Enhance(acct.Id, ch, _content != null ? _content.Enhance : null, _dice.Next(10000), out outcome, charm);
                m.Byte((byte)res);
                m.Byte((byte)outcome);
                m.Int(before);
                NetCodec.WriteProfile(m, acct != null ? acct.Profile : new ServerProfile());
                pipe.Send(m.ToArray());
                if (res == AccountStore.EnhanceResult.Ok) Log?.Invoke("[room-server] enhance " + acct.Id + " char " + ch + " " + before + " → " + Accounts.StageOf(acct.Id, ch) + " (" + outcome + ")");
                return true;
            }
            return false;
        }

        /// <summary>판이 끝난 방의 결과를 서버 프로필에 한 번 적는다(계정이 있는 사람만).</summary>
        void RecordIfEnded(CoopHost h)
        {
            if (!h.Started || _recorded.Contains(h) || h.World.Run.Phase != RunPhase.RunEnded) return;
            _recorded.Add(h);
            var run = h.World.Run;
            if (h.Mode == RoomMode.Duel && run.Duel != null)
            {
                long a = h.AccountOfSlot(0), b = h.AccountOfSlot(1);
                if (a > 0 && b > 0)
                {
                    long winner = run.Duel.MatchWinner < 0 ? 0 : h.AccountOfSlot(run.Duel.MatchWinner);
                    Accounts.RecordDuel(a, b, winner);
                }
            }
            else
            {
                for (int s = 0; s < SimConst.MaxPlayers; s++)
                {
                    long id = h.AccountOfSlot(s);
                    if (id <= 0) continue;
                    // 재료는 사람마다 따로 굴린다(오프라인 정산과 같은 표). 까마귀 보따리는 서버에 없다.
                    var table = _content != null && _content.Drops != null ? _content.Drops.MaterialTable : null;
                    var mats = table != null ? Wizard.Solo.Meta.Settlement.SettlementService.DrawMaterials(table, run.DropChances, false, bp => _dice.Next(10000) < bp) : null;
                    Accounts.RecordCoop(id, run.LastClearedWave, run.Purse, mats);
                }
            }
            Log?.Invoke("[room-server] room " + h.Code + " recorded");
        }

        void Route(INetPipe pipe, byte[] first, AccountRecord acct)
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
                        var h = new CoopHost(_content, _modules, SimConst.MaxPlayers, mode) { Code = NewCode(), StageLookup = Accounts.StageOf };
                        _rooms[h.Code] = h;
                        Log?.Invoke("[room-server] room " + h.Code + " created (" + mode + ")");
                        SendInfo(pipe, h);
                        h.Accept(pipe, acct);
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
                        h.Accept(pipe, acct);
                        break;
                    }
                case NetMsg.EnterPlaza:
                    Plaza.Accept(pipe, r, acct != null ? acct.Name : null);
                    break;
                case NetMsg.Hello:
                    // 옛 손님(방 코드 없이 바로 Hello): 협동 방을 하나 만들어 준다.
                    {
                        var h = new CoopHost(_content, _modules) { Code = NewCode(), StageLookup = Accounts.StageOf };
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
