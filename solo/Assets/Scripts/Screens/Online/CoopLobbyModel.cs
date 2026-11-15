using System.Collections.Generic;
using Wizard.Solo.Net;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 협동 방(ScreenId.CoopLobby, 11-online.md)의 규칙. 엔진 없음.
    /// 2단계: 이 기기에 방을 만들고(방장 = 슬롯 0), 동료 봇을 0~3명 더해 시작한다. 내 캐릭터 = 마지막으로 고른 캐릭터의 든 지팡이(D-56과 같은 규칙).
    /// 판이 끝나면 Battle이 이 화면으로 돌아오며 결과(웨이브·점수)를 넘긴다. 오프라인 프로필에는 아무것도 쓰지 않는다.
    /// </summary>
    public sealed class CoopLobbyModel
    {
        readonly GameContext _ctx;

        public OnlineCoop Room { get; private set; }
        /// <summary>만들 방의 종류(화면의 협동/결투 단추). 방에 들어간 뒤에는 방의 종류(Room.Mode)를 본다.</summary>
        public RoomMode SelectedMode = RoomMode.Coop;
        public RoomMode Mode => Room != null ? Room.Mode : SelectedMode;
        public int MaxMembers => Mode == RoomMode.Duel ? 2 : SimConst.MaxPlayers;
        /// <summary>방 코드(서버 방, 받은 뒤). 이 기기 방이면 null.</summary>
        public string Code => Room != null ? Room.RoomCode : null;
        /// <summary>결투 결과(Battle이 넘긴다): 이긴 슬롯(−1 무승부, −2 없음), 내 슬롯, 승수.</summary>
        public int ResultDuelWinner = -2, ResultMySlot, ResultWins0, ResultWins1;
        public string ErrorKey { get; private set; }
        public int ResultWave = -1;
        public int ResultScore;

        public CoopLobbyModel(GameContext ctx) { _ctx = ctx; }

        public bool HasRoom => Room != null && Room.Connected;
        public bool CanAddBot => HasRoom && Room.IsLeader && Room.World == null && Room.MemberCount > 0 && Room.MemberCount < MaxMembers;
        public bool CanRemoveBot => HasRoom && Room.IsLeader && Room.World == null && Room.BotCount > 0;
        public bool CanOpenNetwork => HasRoom && Room.IsLocalHost && !Room.OpenToNetwork;
        public bool CanStart => HasRoom && Room.IsLeader && Room.World == null && !_startRequested
            && (Mode != RoomMode.Duel || Room.MemberCount == 2);
        /// <summary>원격 방에서 시작을 청했고 아직 판이 안 열렸다.</summary>
        bool _startRequested;

        /// <summary>원격 방에서 시작을 청하고 서버를 기다리는 중.</summary>
        public bool WaitingForStart => _startRequested && Room != null && Room.World == null;

        /// <summary>자리 줄의 역할 글자: 방장 / (이 기기 방이면) 봇 / 동료.</summary>
        public string RoleKey(int slot)
        {
            int leader = int.MaxValue;
            foreach (var m in Members()) if (m.Slot < leader) leader = m.Slot;
            if (slot == leader) return OnlineKeys.Host;
            foreach (var m in Members()) if (m.Slot == slot && m.IsBot) return OnlineKeys.Bot;
            return OnlineKeys.Member;
        }

        /// <summary>로비 목록(슬롯 순). 방이 없으면 빈 목록.</summary>
        public List<CoopClient.LobbyMember> Members()
        {
            var l = new List<CoopClient.LobbyMember>();
            if (Room != null && Room.Me != null) l.AddRange(Room.Me.Lobby);
            l.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return l;
        }

        int MyCharacter
        {
            get
            {
                var p = _ctx != null ? _ctx.Profile : null;
                return p != null && p.LastCharacter >= 1 ? p.LastCharacter : 1;
            }
        }

        RunConfig MemberConfig(int character)
        {
            var p = _ctx != null ? _ctx.Profile : null;
            if (_ctx == null || _ctx.Wands == null || p == null || p.Characters == null || character < 1 || character > p.Characters.Count) return null;
            var r = _ctx.Wands.BuildRunConfig(character, p.Characters[character - 1].EquippedWandId, 1, 1UL);
            if (!r.Ok || r.Value == null) { ErrorKey = OnlineKeys.ErrorWand; return null; }
            return r.Value;
        }

        /// <summary>온라인 서버에 방을 만든다(코드가 나온다).</summary>
        public bool CreateOnline()
        {
            ErrorKey = null;
            if (HasRoom) return true;
            var me = MemberConfig(MyCharacter);
            if (me == null) { ErrorKey = ErrorKey ?? OnlineKeys.ErrorWand; return false; }
            Room = OnlineCoop.CreateRoom(_ctx.Content, SelectedMode, me);
            if (Room == null) { ErrorKey = OnlineKeys.ErrorServer; return false; }
            if (_ctx != null) _ctx.Online = Room;
            return true;
        }

        /// <summary>코드로 온라인 방에 들어간다.</summary>
        public bool JoinCode(string code)
        {
            ErrorKey = null;
            if (HasRoom) return true;
            code = RoomServer.Normalize(code);
            if (string.IsNullOrEmpty(code) || code.Length != RoomServer.CodeLength) { ErrorKey = OnlineKeys.ErrorCode; return false; }
            var me = MemberConfig(MyCharacter);
            if (me == null) { ErrorKey = ErrorKey ?? OnlineKeys.ErrorWand; return false; }
            Room = OnlineCoop.JoinCode(_ctx.Content, code, me);
            if (Room == null) { ErrorKey = OnlineKeys.ErrorServer; return false; }
            if (_ctx != null) _ctx.Online = Room;
            return true;
        }

        /// <summary>이 기기에 방을 만든다(인터넷 없이 봇과 연습).</summary>
        public bool CreateRoom()
        {
            ErrorKey = null;
            if (HasRoom) return true;
            var me = MemberConfig(MyCharacter);
            if (me == null) { ErrorKey = ErrorKey ?? OnlineKeys.ErrorWand; return false; }
            Room = OnlineCoop.HostLocal(_ctx.Content, me, SelectedMode);
            if (!HasRoom) { ErrorKey = OnlineKeys.ErrorConnect; Room = null; return false; }
            if (_ctx != null) _ctx.Online = Room;
            return true;
        }

        /// <summary>원격 서버의 방에 들어간다(주소 "호스트:포트", 포트가 없으면 <see cref="DefaultPort"/>).</summary>
        public bool JoinRoom(string address)
        {
            ErrorKey = null;
            if (HasRoom) return true;
            if (!ParseAddress(address, out string host, out int port)) { ErrorKey = OnlineKeys.ErrorAddress; return false; }
            var me = MemberConfig(MyCharacter);
            if (me == null) { ErrorKey = ErrorKey ?? OnlineKeys.ErrorWand; return false; }
            Room = OnlineCoop.JoinRemote(_ctx.Content, host, port, me);
            if (Room == null) { ErrorKey = OnlineKeys.ErrorConnect; return false; }
            if (_ctx != null) _ctx.Online = Room;
            return true;
        }

        public const int DefaultPort = 27870;

        public static bool ParseAddress(string address, out string host, out int port)
        {
            host = null;
            port = DefaultPort;
            if (string.IsNullOrWhiteSpace(address)) return false;
            string a = address.Trim();
            int colon = a.LastIndexOf(':');
            if (colon > 0 && a.IndexOf(':') == colon)
            {
                if (!int.TryParse(a.Substring(colon + 1), out port) || port <= 0 || port > 65535) return false;
                a = a.Substring(0, colon);
            }
            host = a;
            return host.Length > 0;
        }

        /// <summary>동료 봇: 내 캐릭터 다음 번호부터 차례로(5 다음은 1).</summary>
        public bool AddBot()
        {
            if (!CanAddBot) return false;
            int n = Room.BotCount + 1;
            int c = (MyCharacter - 1 + n) % Wizard.Solo.Ids.Char.Count + 1;
            var cfg = MemberConfig(c);
            return cfg != null && Room.AddBot(cfg);
        }

        /// <summary>이 기기 방을 다른 기기에 연다(포트 = DefaultPort). 열린 뒤에는 NetworkAddresses를 안내한다.</summary>
        public bool OpenNetwork()
        {
            ErrorKey = null;
            if (!CanOpenNetwork) return false;
            if (!Room.OpenNetwork(DefaultPort)) { ErrorKey = OnlineKeys.ErrorPort; return false; }
            return true;
        }

        /// <summary>이 기기의 내부 IPv4 주소들(같은 와이파이의 다른 기기가 칠 주소).</summary>
        public static List<string> LocalAddresses()
        {
            var l = new List<string>();
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        var a = ua.Address;
                        if (a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || System.Net.IPAddress.IsLoopback(a)) continue;
                        string t = a.ToString();
                        if (t.StartsWith("169.254.")) continue;
                        if (!l.Contains(t)) l.Add(t);
                    }
                }
            }
            catch (System.Exception) { }
            return l;
        }

        public void RemoveBot() { if (CanRemoveBot) Room.RemoveBot(); }

        /// <summary>판 시작: 서버 시작 → 세션을 꼭두각시 세계로 연다. 성공이면 Battle로 가면 된다.</summary>
        public bool Start(ulong seed)
        {
            ErrorKey = null;
            if (!CanStart) return false;
            if (!(_ctx.Session is IOnlineSession os)) { ErrorKey = OnlineKeys.ErrorConnect; return false; }
            if (!Room.IsLocalHost)
            {
                Room.StartRun(seed);
                _startRequested = true;
                return false; // 서버의 Start를 기다린다(Tick이 연다)
            }
            if (!Room.StartRun(seed)) { ErrorKey = OnlineKeys.ErrorConnect; return false; }
            os.StartOnline(Room);
            return true;
        }

        /// <summary>방에서 나간다(판 밖에서).</summary>
        public void Leave()
        {
            Room?.Leave();
            Room = null;
            if (_ctx != null && _ctx.Online != null && !_ctx.Online.Connected) _ctx.Online = null;
        }

        /// <summary>프레임마다. 원격 방에서 판이 열렸으면(방장이 시작했다) 세션을 열고 true — 화면은 Battle로 간다.</summary>
        public bool Tick()
        {
            if (Room == null) return false;
            if (Room.World == null)
            {
                Room.Pump();
                if (!Room.Connected)
                {
                    ErrorKey = Room.Me.State != CoopClientState.Rejected ? OnlineKeys.ErrorConnect
                        : Room.Me.RoomErrorCode == RoomError.NotFound ? OnlineKeys.ErrorNoRoom
                        : Room.Me.RoomErrorCode == RoomError.Full ? OnlineKeys.ErrorFull
                        : Room.Me.RoomErrorCode == RoomError.Started ? OnlineKeys.ErrorStarted
                        : OnlineKeys.ErrorRejected;
                    Room = null;
                    _startRequested = false;
                    if (_ctx != null) _ctx.Online = null;
                    return false;
                }
            }
            if (Room != null && !Room.IsLocalHost && Room.World != null && _ctx.Session is IOnlineSession os && !os.IsOnline)
            {
                _startRequested = false;
                os.StartOnline(Room);
                return true;
            }
            return false;
        }

        /// <summary>화면에 들어올 때: 끝난 판의 연결은 걷고 결과를 받는다.</summary>
        public void Enter(ScreenArgs args)
        {
            ErrorKey = null;
            ResultWave = args != null && args.Values.ContainsKey(ScreenArgKeys.CoopResultWave) ? args.Get(ScreenArgKeys.CoopResultWave, 0) : -1;
            ResultScore = args != null ? args.Get(ScreenArgKeys.CoopResultScore, 0) : 0;
            ResultDuelWinner = args != null ? args.Get(ScreenArgKeys.DuelWinner, -2) : -2;
            ResultMySlot = args != null ? args.Get(ScreenArgKeys.DuelMySlot, 0) : 0;
            ResultWins0 = args != null ? args.Get(ScreenArgKeys.DuelWins0, 0) : 0;
            ResultWins1 = args != null ? args.Get(ScreenArgKeys.DuelWins1, 0) : 0;
            if (args != null && args.Values.ContainsKey(ScreenArgKeys.RoomMode)) SelectedMode = args.Get(ScreenArgKeys.RoomMode, RoomMode.Coop);
            _startRequested = false;
            if (Room != null && !Room.Connected) Room = null;
            if (_ctx != null && _ctx.Online != null && !_ctx.Online.Connected) _ctx.Online = null;
        }
    }

    /// <summary>협동 방 글자 열쇠(Resources/NamesSolo/Online.ko.json).</summary>
    public static class OnlineKeys
    {
        public const string MenuItem = "str.main_menu.item.coop";
        public const string Title = "str.coop.title";
        public const string Intro = "str.coop.intro";
        public const string Create = "str.coop.create";
        public const string AddBot = "str.coop.add_bot";
        public const string RemoveBot = "str.coop.remove_bot";
        public const string Start = "str.coop.start";
        public const string Leave = "str.coop.leave";
        public const string Empty = "str.coop.slot_empty";
        public const string MemberLine = "str.coop.member_line";
        public const string Host = "str.coop.host";
        public const string Bot = "str.coop.bot";
        public const string Result = "str.coop.result";
        public const string ErrorWand = "str.coop.error.wand";
        public const string ErrorConnect = "str.coop.error.connect";
        public const string ErrorAddress = "str.coop.error.address";
        public const string ErrorPort = "str.coop.error.port";
        public const string OpenNetwork = "str.coop.open_network";
        public const string OpenedAt = "str.coop.opened_at";
        public const string ErrorRejected = "str.coop.error.rejected";
        public const string Join = "str.coop.join";
        public const string AddressHint = "str.coop.address_hint";
        public const string Waiting = "str.coop.waiting";
        public const string Member = "str.coop.member";
        public const string ModeCoop = "str.coop.mode.coop";
        public const string ModeDuel = "str.coop.mode.duel";
        public const string CreateOnline = "str.coop.create_online";
        public const string CreateLocal = "str.coop.create_local";
        public const string CodeHint = "str.coop.code_hint";
        public const string CodeLine = "str.coop.code_line";
        public const string ErrorCode = "str.coop.error.code";
        public const string ErrorServer = "str.coop.error.server";
        public const string ErrorNoRoom = "str.coop.error.no_room";
        public const string ErrorFull = "str.coop.error.full";
        public const string ErrorStarted = "str.coop.error.started";
        public const string DuelWin = "str.duel.result.win";
        public const string DuelLose = "str.duel.result.lose";
        public const string DuelDraw = "str.duel.result.draw";
        public const string DuelNeedTwo = "str.duel.need_two";
        public const string MenuDuel = "str.main_menu.item.duel";
        public const string MenuPlaza = "str.main_menu.item.plaza";
        public const string Plaza = "str.coop.plaza";
        public const string ChatHint = "str.plaza.chat_hint";
        public const string ChatSend = "str.plaza.chat_send";
        public const string PlazaRooms = "str.plaza.rooms";
        public const string PlazaNameTitle = "str.plaza.name_title";
        public const string PlazaNameHint = "str.plaza.name_hint";
        public const string PlazaEnter = "str.plaza.enter";
        public const string PlazaCount = "str.plaza.count";
    }
}
