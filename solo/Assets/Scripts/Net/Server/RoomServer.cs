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
    public sealed partial class RoomServer
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
            Accounts = accounts ?? new AccountStore((string)null);
            _content = content;
            _modules = modules ?? SimModuleSet.CreateDefault;
            _rng = seed != 0 ? new Random(seed) : new Random();
            Plaza = new PlazaHost { Accounts = Accounts };
            // 온라인 규칙 수치(DataSolo/OnlineRules.json)와 콘텐츠(보유 IL 계산용)를 계정 저장소에 넘긴다.
            Accounts.Content = content;
            Accounts.Rules = OnlineRules.Load(content);
        }

        public CoopHost Room(string code) => code != null && _rooms.TryGetValue(code, out var h) ? h : null;

        public void Accept(INetPipe pipe)
        {
            if (pipe != null) _pending.Add(pipe);
        }

        public void Tick()
        {
            // 0. 끝난 계정 일(따로 스레드)의 결과를 받는다.
            RunJobs();
            // 1. 아직 길이 정해지지 않은 연결: 첫 메시지를 본다.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var pipe = _pending[i];
                if (!pipe.Connected) { _pending.RemoveAt(i); _authed.Remove(pipe); continue; }
                if (_jobs.ContainsKey(pipe)) continue; // 비밀번호 해시·Google 확인 중
                if (!pipe.TryReceive(out var data)) continue;
                if (TryAccountMessage(pipe, data)) continue; // 이메일·Google·서버 정보: 연결은 그대로(또는 끊음)
                if (TryAuth(pipe, data)) continue; // 계정 메시지면 연결은 그대로 기다린다
                if (TryService(pipe, data)) continue; // 강화·순위표: 답하고 연결은 그대로
                if (TryOnlineService(pipe, data)) continue; // 우편·차단·신고·시즌·대기열: 답하고 연결은 그대로
                _queue.RemoveAll(q => q.Pipe == pipe); // 방·광장으로 가면 대기열에서 빠진다
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
                h.AgeTicks++;
                // 잡힌 경기 방: 정한 시간 안에 둘 다 들어오지 않으면 닫는다(점수는 변하지 않는다).
                bool noShow = h.Ranked && !h.Started && h.AgeTicks > Accounts.Rules.MatchJoinSeconds * 60;
                if (h.Finished || noShow || (!h.Started && h.IdleTicks > EmptyRoomTicks)) _dead.Add(kv.Key);
            }
            foreach (var code in _dead)
            {
                Log?.Invoke("[room-server] room " + code + " closed");
                _rooms[code].CloseAll();
                _recorded.Remove(_rooms[code]);
                _rooms.Remove(code);
            }
            _dead.Clear();
            QueueTick();
            Plaza.Tick();
            if (++_expireTicks >= 60 * 60) { _expireTicks = 0; Accounts.ExpireListings(DateTime.UtcNow.Ticks); }
        }

        int _expireTicks;

        /// <summary>그 계정이 지금 어디 있나("plaza", "room", 접속 안 함 = "").</summary>
        public string WhereIs(long accountId)
        {
            if (Plaza.HasAccount(accountId)) return "plaza";
            foreach (var h in _rooms.Values) if (h.HasAccount(accountId)) return "room";
            return string.Empty;
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
                Fail(pipe, err);
                return true;
            }
            _authed[pipe] = rec;
            SendAuthOk(pipe, rec, null);
            return true;
        }

        readonly Random _dice = new Random();
        SimModuleSet _rules;

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
                var sac = new List<string>();
                try { var r = new NetReader(data); r.Byte(); ch = r.Int(); charm = r.Bool(); sac = r.StrList() ?? sac; } catch (Exception) { }
                _authed.TryGetValue(pipe, out var acct);
                var m = new NetWriter(NetMsg.EnhanceReply);
                Wizard.Solo.Meta.EnhanceOutcome outcome = Wizard.Solo.Meta.EnhanceOutcome.FailNoDrop;
                int before = acct != null ? Accounts.StageOf(acct.Id, ch) : 0;
                var res = acct == null ? AccountStore.EnhanceResult.NoAccount
                    : Accounts.Enhance(acct.Id, ch, _content, _enhanceRng, out outcome, charm, sac);
                m.Byte((byte)res);
                m.Byte((byte)outcome);
                m.Int(before);
                NetCodec.WriteProfile(m, acct != null ? acct.Profile : new ServerProfile());
                pipe.Send(m.ToArray());
                if (res == AccountStore.EnhanceResult.Ok) Log?.Invoke("[room-server] enhance " + acct.Id + " char " + ch + " " + before + " → " + Accounts.StageOf(acct.Id, ch) + " (" + outcome + ")");
                return true;
            }
            if (kind == NetMsg.SetLoadout)
            {
                int ch = 0;
                string core = null;
                List<string> links = null;
                try { var r = new NetReader(data); r.Byte(); ch = r.Int(); core = r.Str(); links = r.StrList(); } catch (Exception) { }
                _authed.TryGetValue(pipe, out var acct);
                if (_rules == null) _rules = _modules();
                var res = acct == null ? AccountStore.LoadoutResult.NoAccount : Accounts.SetLoadout(acct.Id, ch, core, links, _content, _rules);
                var m = new NetWriter(NetMsg.LoadoutReply);
                m.Byte((byte)res);
                NetCodec.WriteProfile(m, acct != null ? acct.Profile : new ServerProfile());
                pipe.Send(m.ToArray());
                return true;
            }
            if (kind == NetMsg.MarketBrowse || kind == NetMsg.MarketMine)
            {
                string item = null;
                try { var r = new NetReader(data); r.Byte(); if (kind == NetMsg.MarketBrowse) item = r.Str(); } catch (Exception) { }
                _authed.TryGetValue(pipe, out var acct);
                var list = kind == NetMsg.MarketBrowse ? Accounts.Browse(item, 60) : (acct != null ? Accounts.ListingsOf(acct.Id) : new List<MarketListing>());
                var m = new NetWriter(NetMsg.MarketList);
                m.Int(list.Count);
                foreach (var l in list) { m.Long(l.Id); m.Long(l.SellerId); m.Str(l.SellerName); m.Str(l.ItemId); m.Int(l.Qty); m.Long(l.Price); }
                pipe.Send(m.ToArray());
                return true;
            }
            if (kind == NetMsg.MarketSell || kind == NetMsg.MarketBuy || kind == NetMsg.MarketUnlist)
            {
                _authed.TryGetValue(pipe, out var acct);
                var res = AccountStore.MarketResult.NoAccount;
                try
                {
                    var r = new NetReader(data);
                    r.Byte();
                    if (acct != null)
                    {
                        if (kind == NetMsg.MarketSell) res = Accounts.List(acct.Id, r.Str(), r.Int(), r.Long(), DateTime.UtcNow.Ticks, out _);
                        else if (kind == NetMsg.MarketBuy) res = Accounts.Buy(acct.Id, r.Long());
                        else res = Accounts.Unlist(acct.Id, r.Long());
                    }
                }
                catch (Exception) { res = AccountStore.MarketResult.BadItem; }
                var m = new NetWriter(NetMsg.MarketReply);
                m.Byte((byte)res);
                NetCodec.WriteProfile(m, acct != null ? acct.Profile : new ServerProfile());
                pipe.Send(m.ToArray());
                return true;
            }
            if (kind == NetMsg.FriendListRequest)
            {
                _authed.TryGetValue(pipe, out var acct);
                var fr = acct != null ? Accounts.FriendsOf(acct.Id, WhereIs) : new List<FriendInfo>();
                var rq = acct != null ? Accounts.RequestsOf(acct.Id) : new List<FriendInfo>();
                var m = new NetWriter(NetMsg.FriendList);
                m.Int(fr.Count);
                foreach (var f in fr) { m.Long(f.Id); m.Str(f.Name); m.Int(f.Rating); m.Str(f.Where); }
                m.Int(rq.Count);
                foreach (var f in rq) { m.Long(f.Id); m.Str(f.Name); m.Int(f.Rating); }
                pipe.Send(m.ToArray());
                return true;
            }
            if (kind == NetMsg.FriendRequest || kind == NetMsg.FriendAnswer || kind == NetMsg.FriendRemove)
            {
                _authed.TryGetValue(pipe, out var acct);
                var res = AccountStore.FriendResult.NoAccount;
                try
                {
                    var r = new NetReader(data);
                    r.Byte();
                    if (acct != null)
                    {
                        if (kind == NetMsg.FriendRequest) res = Accounts.RequestFriend(acct.Id, r.Str());
                        else if (kind == NetMsg.FriendAnswer) { long from = r.Long(); res = r.Bool() ? Accounts.AcceptFriend(acct.Id, from) : Accounts.DeclineFriend(acct.Id, from); }
                        else res = Accounts.RemoveFriend(acct.Id, r.Long());
                    }
                }
                catch (Exception) { res = AccountStore.FriendResult.NotFound; }
                var m = new NetWriter(NetMsg.FriendReply);
                m.Byte((byte)res);
                pipe.Send(m.ToArray());
                return true;
            }
            if (kind == NetMsg.RemoveMutation)
            {
                int ch = 0;
                string mid = null;
                try { var r = new NetReader(data); r.Byte(); ch = r.Int(); mid = r.Str(); } catch (Exception) { }
                _authed.TryGetValue(pipe, out var acct);
                var res = acct == null ? AccountStore.EnhanceResult.NoAccount : Accounts.RemoveMutation(acct.Id, ch, mid, _content != null ? _content.Enhance : null);
                var m = new NetWriter(NetMsg.EnhanceReply);
                m.Byte((byte)res);
                m.Byte((byte)Wizard.Solo.Meta.EnhanceOutcome.FailNoDrop);
                m.Int(acct != null ? Accounts.StageOf(acct.Id, ch) : 0);
                NetCodec.WriteProfile(m, acct != null ? acct.Profile : new ServerProfile());
                pipe.Send(m.ToArray());
                return true;
            }
            if (kind == NetMsg.MutationResolve)
            {
                int ch = 0;
                bool accept = false;
                try { var r = new NetReader(data); r.Byte(); ch = r.Int(); accept = r.Bool(); } catch (Exception) { }
                _authed.TryGetValue(pipe, out var acct);
                Wizard.Solo.Meta.EnhanceOutcome outcome = Wizard.Solo.Meta.EnhanceOutcome.FailNoDrop;
                int before = acct != null ? Accounts.StageOf(acct.Id, ch) : 0;
                var res = acct == null ? AccountStore.EnhanceResult.NoAccount : Accounts.ResolveMutation(acct.Id, ch, _content, accept, out outcome);
                var m = new NetWriter(NetMsg.EnhanceReply);
                m.Byte((byte)res);
                m.Byte((byte)outcome);
                m.Int(before);
                NetCodec.WriteProfile(m, acct != null ? acct.Profile : new ServerProfile());
                pipe.Send(m.ToArray());
                return true;
            }
            return false;
        }

        sealed class DiceRandom : Wizard.Solo.Meta.Enhance.IEnhanceRandom
        {
            readonly Random _r;
            public DiceRandom(Random r) { _r = r; }
            public int Next(int maxExclusive) => _r.Next(maxExclusive);
        }
        Wizard.Solo.Meta.Enhance.IEnhanceRandom _enhanceRngField;
        /// <summary>강화 주사위(테스트는 바꿔 끼운다).</summary>
        public Wizard.Solo.Meta.Enhance.IEnhanceRandom EnhanceRng { set => _enhanceRngField = value; }
        Wizard.Solo.Meta.Enhance.IEnhanceRandom _enhanceRng => _enhanceRngField ?? (_enhanceRngField = new DiceRandom(_dice));

        /// <summary>판이 끝난 방의 결과를 서버 프로필에 한 번 적는다(계정이 있는 사람만).</summary>
        void RecordIfEnded(CoopHost h)
        {
            if (!h.Started || _recorded.Contains(h) || h.World.Run.Phase != RunPhase.RunEnded) return;
            _recorded.Add(h);
            var run = h.World.Run;
            long now = Now;
            int seconds = h.World.Tick / SimConst.TicksPerSecond;
            if (h.Mode == RoomMode.Duel && run.Duel != null)
            {
                long a = h.AccountOfSlot(0), b = h.AccountOfSlot(1);
                if (a > 0 && b > 0)
                {
                    long winner = run.Duel.MatchWinner < 0 ? 0 : h.AccountOfSlot(run.Duel.MatchWinner);
                    Accounts.AddPlayTime(a, seconds);
                    Accounts.AddPlayTime(b, seconds);
                    if (!h.Ranked) Accounts.RecordDuel(a, b, winner);
                    else
                    {
                        // 자동 매치(4.10절): 중도 이탈은 패배 + 이탈 기록, 양쪽이 다 끊겼으면 무효(점수가 변하지 않는다).
                        bool la = h.LeftDuringRun(0), lb = h.LeftDuringRun(1);
                        if (la && lb) Log?.Invoke("[room-server] match " + h.Code + " void (both left)");
                        else
                        {
                            bool rated = Accounts.RecordRankedDuel(a, b, winner, h.League, now);
                            if (la) Accounts.RecordLeave(a, now);
                            if (lb) Accounts.RecordLeave(b, now);
                            Log?.Invoke("[room-server] match " + h.Code + " winner " + winner + (rated ? " (rated)" : " (unrated)"));
                        }
                    }
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
                    var blocks = new List<string>();
                    foreach (var wb in run.Rewards[s].Storage) if (!wb.Borrowed && !string.IsNullOrEmpty(wb.BlockId)) blocks.Add(wb.BlockId);
                    Accounts.AddPlayTime(id, seconds);
                    Accounts.RecordCoop(id, run.LastClearedWave, run.Purse, mats, blocks);
                    // 판 기록과 순위표(판은 서버가 돌렸으므로 그대로 믿는다). 점수는 판의 점수(같이 한 사람은 같은 점수).
                    var member = h.MemberOfSlot(s);
                    if (member != null) Accounts.RecordRun(id, Ids.Char.NumberOf(member.CharId), member.WandStage, run.LastClearedWave, run.Score, run.Purse, h.MemberCount, now);
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
                        var h = new CoopHost(_content, _modules, SimConst.MaxPlayers, mode) { Code = NewCode(), StageLookup = Accounts.StageOf, MutationLookup = Accounts.MutationIds, LoadoutLookup = (aid, ch) => Accounts.LoadoutSpecOf(aid, ch, _content), SpectatorsMax = Accounts.Rules.SpectatorsMax };
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
                        // 잡힌 경기 방에는 그 두 계정만 들어온다.
                        if (h.Reserved != null && (acct == null || !h.Reserved.Contains(acct.Id) || h.HasAccount(acct.Id))) { Fail(pipe, RoomError.NotFound); return; }
                        SendInfo(pipe, h);
                        h.Accept(pipe, acct);
                        break;
                    }
                case NetMsg.SpectateRoom:
                    {
                        // 관전(4.6절): 결투 방만, 방마다 SpectatorsMax명까지. 판 중에도 들어올 수 있다.
                        string code = null;
                        try { code = Normalize(r.Str()); } catch (Exception) { }
                        var h = Room(code);
                        if (h == null || h.Mode != RoomMode.Duel) { Fail(pipe, RoomError.NotFound); return; }
                        if (h.SpectatorCount >= h.SpectatorsMax) { Fail(pipe, RoomError.Full); return; }
                        SendInfo(pipe, h);
                        h.AcceptSpectator(pipe);
                        break;
                    }
                case NetMsg.EnterPlaza:
                    Plaza.Accept(pipe, r, acct != null ? acct.Name : null, acct != null ? acct.Id : 0);
                    break;
                case NetMsg.Hello:
                    // 옛 손님(방 코드 없이 바로 Hello): 협동 방을 하나 만들어 준다.
                    {
                        var h = new CoopHost(_content, _modules) { Code = NewCode(), StageLookup = Accounts.StageOf, MutationLookup = Accounts.MutationIds, LoadoutLookup = (aid, ch) => Accounts.LoadoutSpecOf(aid, ch, _content) };
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
