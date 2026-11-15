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

        public CoopLobbyModel(GameContext ctx) { _ctx = ctx; Account = OnlineAccount.Load(); }

        /// <summary>이 기기의 온라인 계정(없으면 null — 손님으로도 방에 들어갈 수 있지만 기록이 남지 않는다).</summary>
        public OnlineAccount Account { get; private set; }
        /// <summary>마지막으로 받은 서버 프로필(계정 만들 때, 또는 방에 들어갈 때 AuthOk).</summary>
        public ServerProfile Profile { get; private set; }

        System.Threading.Tasks.Task<OnlineService> _profileTask;
        /// <summary>서버가 이 기기의 로그인 표를 받지 않았다(로그아웃·만료) — 화면은 로그인 화면을 연다.</summary>
        public bool AuthRejected { get; private set; }
        public bool ProfileLoading => _profileTask != null;

        /// <summary>이 기기 계정 파일을 다시 읽는다(로그인 화면에서 돌아왔을 때).</summary>
        public void ReloadAccount()
        {
            Account = OnlineAccount.Load();
            AuthRejected = false;
            Profile = null;
        }

        /// <summary>서버 프로필을 따로 스레드로 받아 온다(위 띠의 이름·골드·레이팅). 끝나면 Tick에서 Profile이 바뀐다.</summary>
        public void RefreshProfile()
        {
            if (Account == null || _profileTask != null) return;
            var db = _ctx != null ? _ctx.Content : null;
            _profileTask = System.Threading.Tasks.Task.Run(() => OnlineService.Open(db));
        }

        void PollProfile()
        {
            if (_profileTask == null || !_profileTask.IsCompleted) return;
            var svc = _profileTask.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? _profileTask.Result : null;
            _profileTask = null;
            if (svc != null)
            {
                if (svc.Profile != null) Profile = svc.Profile;
                svc.Dispose();
            }
            else if (OnlineService.LastAuthFailed) AuthRejected = true;
        }

        /// <summary>이름으로 기기 계정을 만든다.</summary>
        public bool RegisterAccount(string name)
        {
            ErrorKey = null;
            if (Account != null) return true;
            var a = OnlineAccount.Register(_ctx != null ? _ctx.Content : null, name, out var err, out var prof);
            if (a == null)
            {
                ErrorKey = err == AuthError.NameTaken ? OnlineKeys.ErrorNameTaken : err == AuthError.BadName ? OnlineKeys.ErrorBadName : OnlineKeys.ErrorServer;
                return false;
            }
            Account = a;
            Profile = prof;
            return true;
        }

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

        /// <summary>온라인 출발 설정: 오프라인 프로필의 지팡이를 쓰지 않는다(11-online.md 6.8절). 서버가 서버 프로필의 단계로 다시 만든다.
        /// 기본 공격 유형만 오프라인에서 고른 것을 읽는다(쓰지 않는다).</summary>
        RunConfig MemberConfig(int character)
        {
            var p = _ctx != null ? _ctx.Profile : null;
            var rec = p != null ? p.Character(character) : null;
            return OnlineMember.Build(_ctx != null ? _ctx.Content : null, character, 0, rec != null ? rec.AttackStyle : null);
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
            PollProfile();
            if (Room != null && Room.Me != null && Room.Me.Profile != null) Profile = Room.Me.Profile;
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
        public const string MktCount = "str.online.mkt.count";
        public const string FrSideTitle = "str.online.fr.side_title";
        public const string FrSideNote = "str.online.fr.side_note";
        public const string FrRequestsHead = "str.online.fr.requests_head";
        public const string FrFriendsHead = "str.online.fr.friends_head";
        public const string FrNoRequests = "str.online.fr.no_requests";
        public const string FrNoFriends = "str.online.fr.no_friends";
        public const string FrRating = "str.online.fr.rating";
        public const string MktEach = "str.online.mkt.each";
        public const string MktSeller = "str.online.mkt.seller";
        public const string MktBuy = "str.online.mkt.buy";
        public const string MktMine = "str.online.mkt.mine";
        public const string MktOwn = "str.online.mkt.own";
        public const string MktLeft = "str.online.mkt.left";
        public const string MktUnlist = "str.online.mkt.unlist";
        public const string MktReceiptPrice = "str.online.mkt.receipt_price";
        public const string MktReceiptFee = "str.online.mkt.receipt_fee";
        public const string MktReceiptDeposit = "str.online.mkt.receipt_deposit";
        public const string MktReceiptGet = "str.online.mkt.receipt_get";
        public const string MktBrowseEmpty = "str.online.mkt.browse_empty";
        public const string MktMineEmpty = "str.online.mkt.mine_empty";
        public const string MktPickFirst = "str.online.mkt.pick_first";
        public const string ProfOddsGreat = "str.online.prof.odds_great";
        public const string ProfOddsSuccess = "str.online.prof.odds_success";
        public const string ProfOddsMutation = "str.online.prof.odds_mutation";
        public const string ProfOddsFail = "str.online.prof.odds_fail";
        public const string ProfDrop = "str.online.prof.drop";
        public const string ProfNoDrop = "str.online.prof.no_drop";
        public const string ProfCost = "str.online.prof.cost";
        public const string ProfSacrificeHead = "str.online.prof.sacrifice_head";
        public const string ProfSacrificeHint = "str.online.prof.sacrifice_hint";
        public const string ProfStorage = "str.online.prof.storage";
        public const string ProfStorageEmpty = "str.online.prof.storage_empty";
        public const string ProfMutations = "str.online.prof.mutations";
        public const string ProfNoMutations = "str.online.prof.no_mutations";
        public const string ProfWl = "str.online.prof.wl";
        public const string ProfStage = "str.online.prof.stage";
        public const string ProfYouRow = "str.online.prof.you_row";
        public const string ProfUnranked = "str.online.prof.unranked";
        public const string ProfCharm = "str.online.prof.charm";
        public const string HubDescCoop = "str.online.hub.desc.coop";
        public const string HubDescDuel = "str.online.hub.desc.duel";
        public const string HubDescPlaza = "str.online.hub.desc.plaza";
        public const string HubRecCoop = "str.online.hub.rec.coop";
        public const string HubRecDuel = "str.online.hub.rec.duel";
        public const string HubRecPlaza = "str.online.hub.rec.plaza";
        public const string HubCreate = "str.online.hub.create";
        public const string HubJoinHead = "str.online.hub.join_head";
        public const string HubLocal = "str.online.hub.local";
        public const string HubLocalNote = "str.online.hub.local_note";
        public const string HubPlazaEnter = "str.online.hub.plaza_enter";
        public const string HubCodeCaption = "str.online.hub.code_caption";
        public const string HubCopy = "str.online.hub.copy";
        public const string HubCopied = "str.online.hub.copied";
        public const string HubShare = "str.online.hub.share";
        public const string HubLocalRoom = "str.online.hub.local_room";
        public const string HubWaitingSlot = "str.online.hub.waiting_slot";
        public const string HubMembers = "str.online.hub.members";
        public const string HubLeader = "str.online.hub.leader";
        public const string HubYou = "str.online.hub.you";
        public const string HubStage = "str.online.hub.stage";
        public const string HubLoading = "str.online.hub.loading";
        public const string HubVersus = "str.online.hub.versus";
        public const string LoginTitle = "str.online.login.title";
        public const string LoginTabLogin = "str.online.login.tab_login";
        public const string LoginTabSignup = "str.online.login.tab_signup";
        public const string LoginEmail = "str.online.login.email";
        public const string LoginEmailHint = "str.online.login.email_hint";
        public const string LoginEmailNote = "str.online.login.email_note";
        public const string LoginPassword = "str.online.login.password";
        public const string LoginPasswordHint = "str.online.login.password_hint";
        public const string LoginPassword2 = "str.online.login.password2";
        public const string LoginPassword2Hint = "str.online.login.password2_hint";
        public const string LoginName = "str.online.login.name";
        public const string LoginNameHint = "str.online.login.name_hint";
        public const string LoginSubmit = "str.online.login.submit";
        public const string LoginSubmitSignup = "str.online.login.submit_signup";
        public const string LoginOr = "str.online.login.or";
        public const string LoginGoogle = "str.online.login.google";
        public const string LoginGoogleOff = "str.online.login.google_off";
        public const string LoginGoogleWait = "str.online.login.google_wait";
        public const string LoginForgot = "str.online.login.forgot";
        public const string LoginMailOffTag = "str.online.login.mail_off_tag";
        public const string LoginMailOff = "str.online.login.mail_off";
        public const string LoginBack = "str.online.login.back";
        public const string LoginWorking = "str.online.login.working";
        public const string LoginNeedName = "str.online.login.need_name";
        public const string LoginNeedNameSubmit = "str.online.login.need_name_submit";
        public const string LoginMismatch = "str.online.login.mismatch";
        public const string LoginNoServer = "str.online.login.no_server";
        public const string LoginSecureNote = "str.online.login.secure_note";
        public const string LoginSideTitle = "str.online.login.side_title";
        public const string LoginSide1 = "str.online.login.side_1";
        public const string LoginSide2 = "str.online.login.side_2";
        public const string LoginSide3 = "str.online.login.side_3";
        public const string LoginBrowserDone = "str.online.login.browser_done";
        public const string LoginBrowserClose = "str.online.login.browser_close";
        public const string Logout = "str.online.logout";
        public const string KindEmail = "str.online.kind.Email";
        public const string KindGoogle = "str.online.kind.Google";
        public const string KindDevice = "str.online.kind.Device";
        public const string LoginErrorPrefix = "str.online.login.error.";
        public const string KindPrefix = "str.online.kind.";
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
        public const string DoorCoop = "str.plaza.door.coop";
        public const string AccountLine = "str.online.account_line";
        public const string ProfileButton = "str.online.profile_button";
        public const string ProfileTitle = "str.online.profile.title";
        public const string ProfileWandLine = "str.online.profile.wand_line";
        public const string ProfileEnhance = "str.online.profile.enhance";
        public const string ProfileTop = "str.online.profile.top";
        public const string ProfileTopLine = "str.online.profile.top_line";
        public const string ProfileGuest = "str.online.profile.guest";
        public const string ProfileNote = "str.online.profile.note";
        public const string EnhanceGold = "str.online.enhance.gold";
        public const string EnhanceStones = "str.online.enhance.stones";
        public const string SacrificeLine = "str.online.sacrifice.line";
        public const string AssemblyButton = "str.online.assembly.button";
        public const string MutationRemoveHead = "str.online.mutation.remove_head";
        public const string MarketButton = "str.online.market.button";
        public const string MarketEmpty = "str.online.market.empty";
        public const string TopEmpty = "str.online.profile.top_empty";
        public const string MenuServerLine = "str.online.menu_server_line";
        public const string MarketHead = "str.online.market.head";
        public const string MarketBuyHead = "str.online.market.buy_head";
        public const string MarketSellHead = "str.online.market.sell_head";
        public const string MarketMineHead = "str.online.market.mine_head";
        public const string MarketLine = "str.online.market.line";
        public const string MarketMineLine = "str.online.market.mine_line";
        public const string MarketQty = "str.online.market.qty";
        public const string MarketPrice = "str.online.market.price";
        public const string MarketSell = "str.online.market.sell";
        public const string MarketRefresh = "str.online.market.refresh";
        public const string MarketPick = "str.online.market.pick";
        public const string MarketPickNone = "str.online.market.pick_none";
        public const string MarketResult = "str.online.market.result.";
        public const string FriendsButton = "str.online.friends.button";
        public const string FriendsTitle = "str.online.friends.title";
        public const string FriendsNameHint = "str.online.friends.name_hint";
        public const string FriendsAdd = "str.online.friends.add";
        public const string FriendsRequests = "str.online.friends.requests";
        public const string FriendsAccept = "str.online.friends.accept";
        public const string FriendsDecline = "str.online.friends.decline";
        public const string FriendsList = "str.online.friends.list";
        public const string FriendsRemove = "str.online.friends.remove";
        public const string FriendsRequestLine = "str.online.friends.request_line";
        public const string FriendsLine = "str.online.friends.line";
        public const string FriendsWhere = "str.online.friends.where.";
        public const string FriendsResult = "str.online.friends.result.";
        public const string WhisperFrom = "str.plaza.whisper.from";
        public const string WhisperNobody = "str.plaza.whisper.nobody";
        public const string MutationRemoved = "str.online.mutation.removed";
        public const string TradeHint = "str.plaza.trade.hint";
        public const string TradeInvite = "str.plaza.trade.invite";
        public const string TradeTitle = "str.plaza.trade.title";
        public const string TradeMine = "str.plaza.trade.mine";
        public const string TradeTheirs = "str.plaza.trade.theirs";
        public const string TradeGold = "str.plaza.trade.gold";
        public const string TradeConfirm = "str.plaza.trade.confirm";
        public const string TradeCancel = "str.plaza.trade.cancel";
        public const string TradeConfirmed = "str.plaza.trade.confirmed";
        public const string TradeResult = "str.plaza.trade.result.";
        public const string TradeInventory = "str.plaza.trade.inventory";
        public const string AssemblyTitle = "str.online.assembly.title";
        public const string AssemblyCore = "str.online.assembly.core";
        public const string AssemblyLink = "str.online.assembly.link";
        public const string AssemblyChips = "str.online.assembly.chips";
        public const string AssemblyClear = "str.online.assembly.clear";
        public const string AssemblySave = "str.online.assembly.save";
        public const string AssemblySaved = "str.online.assembly.saved";
        public const string AssemblyCompile = "str.online.assembly.compile";
        public const string AssemblyNotOwned = "str.online.assembly.not_owned";
        public const string AssemblyBad = "str.online.assembly.bad";
        public const string SacrificeClear = "str.online.sacrifice.clear";
        public const string SacrificeBad = "str.online.sacrifice.bad";
        public const string MutationAccept = "str.online.mutation.accept";
        public const string MutationDecline = "str.online.mutation.decline";
        public const string MutationList = "str.online.mutation.list";
        public const string MutationOfferLine = "str.online.mutation.offer";
        public const string MutationPush = "str.online.mutation.push";
        public const string ItemsLine = "str.online.items_line";
        public const string CharmOn = "str.online.charm_on";
        public const string CharmOff = "str.online.charm_off";
        public const string EnhanceMax = "str.online.enhance.max";
        public const string Outcome = "str.online.enhance.outcome.";
        public const string AccountGuest = "str.online.account_guest";
        public const string AccountCreate = "str.online.account_create";
        public const string AccountNameHint = "str.online.account_name_hint";
        public const string ErrorNameTaken = "str.online.error.name_taken";
        public const string ErrorBadName = "str.online.error.bad_name";
        public const string DoorDuel = "str.plaza.door.duel";
        public const string DoorPrompt = "str.plaza.door_prompt";
    }
}
