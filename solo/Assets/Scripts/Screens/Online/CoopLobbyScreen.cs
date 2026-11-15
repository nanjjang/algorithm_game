using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;
using UInputField = UnityEngine.UI.InputField;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 온라인 들머리(ScreenId.CoopLobby, docs/ui-assets.md 4.5·4.7·4.21절, 11-online.md). 야영지 장면 위:
    ///  - 방 밖: 왼쪽에 모드 카드 3장(협동·결투·광장, S-0502 그림 400×520), 오른쪽 528 폭 판에 고른 모드의 할 일
    ///    ([방 만들고 코드 받기], 코드로 참가, [이 기기에서 봇과]). 위 띠에 내 이름·종류·골드·레이팅과 기록·친구·장터·로그아웃.
    ///  - 방 안: 왼쪽은 "벤치" — 핀에 꽂힌 쪽지(S-2102)에 큰 방 코드, 자리마다 캐릭터 그림(192) + 흙 받침(S-0503) + 준비 표시(S-2103)
    ///    + 방장(S-2104)·봇 꼬리표, 빈자리는 모래시계(S-2105). 결투는 두 자리 사이에 VS(S-0902). 오른쪽 판에 [시작]·봇·나가기·결과.
    /// 계정이 없거나 로그인 표가 지워졌으면 로그인 화면(OnlineLogin)을 먼저 연다 — 돌아와서도 없으면 메인 메뉴로.
    /// 규칙은 CoopLobbyModel. Esc = 방에서 나가 메인 메뉴로.
    /// </summary>
    public sealed class CoopLobbyScreen : ScreenBase
    {
        const float CardX = 48f, CardY = 128f, CardW = 400f, CardH = 520f, CardGap = 24f;
        const float PanelX = 1344f, PanelY = 128f, PanelW = 528f, PanelH = 760f, PanelPad = 32f;
        const float BenchX = 48f, BenchY = 128f, BenchW = 1272f, BenchH = 760f;

        CoopLobbyModel _model;
        bool _askedLogin;

        // 위 띠
        UiLabel _who, _gold, _rating;
        RectTransform _kindTagRoot;
        UiLabel _kindTag;
        UiButton _profile, _friends, _market, _logout;

        // 방 밖
        RectTransform _outside;
        readonly UiButton[] _cards = new UiButton[3];
        readonly UiLabel[] _cardRecords = new UiLabel[3];
        UiLabel _panelTitle, _panelDesc, _localNote, _joinHead;
        UiButton _createOnline, _createLocal, _join, _plazaEnter;
        UInputField _codeInput;
        RectTransform _roomActions;

        // 방 안
        RectTransform _inside;
        UiLabel _codeLabel, _codeCaption, _share, _roomTitle, _members, _roomDesc;
        UiButton _copy;
        RectTransform _benchRoot;
        readonly List<Seat> _seats = new List<Seat>();
        UImage _versus;
        UiButton _addBot, _removeBot, _start, _leave;
        UImage _resultBanner;

        UiLabel _status;

        sealed class Seat
        {
            public RectTransform Root;
            public UImage Board, Art, Pedestal, Ready, Leader, Hourglass;
            public UiLabel Name, Role, Stage, Waiting;
            public RectTransform BotTag, StageRoot;
        }

        public override ScreenId Id => ScreenId.CoopLobby;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            _model = new CoopLobbyModel(Ctx);
            OnlineUi.Scene(Ctx, content, "S-0501", "camp_background", 0.45f);
            OnlineUi.AnimImg(Ctx, content, "S-0501", "campfire", 1236f - 64f, 780f - 100f, 128f, 128f);
            BuildHeader(content);
            BuildOutside(content);
            BuildInside(content);
            _status = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, CardX, 952f, 1824f, 48f, UiTheme.Amber, TextAnchor.MiddleLeft);
            UiKit.DropShadow(_status);
        }

        void BuildHeader(RectTransform content)
        {
            OnlineUi.HeaderBand(Ctx, content, "I-F1", "square", T(OnlineKeys.Title), out var band);
            float x = 380f;
            _who = UiKit.Label(Ctx, band, string.Empty, UiFontId.F24, x, 0f, 300f, 80f, UiTheme.Chalk, TextAnchor.MiddleLeft);
            _kindTag = OnlineUi.Tag(Ctx, band, T(OnlineKeys.KindEmail), x + 300f, 28f, UiTheme.SlateBlue, 120f);
            _kindTagRoot = (RectTransform)_kindTag.transform.parent;
            _gold = OnlineUi.IconText(Ctx, band, "I-C2", "gold", string.Empty, x + 440f, 0f, 180f, 80f, UiFontId.F24, UiTheme.Gold);
            _rating = OnlineUi.IconText(Ctx, band, "I-L1", "league_sprout", string.Empty, x + 630f, 0f, 170f, 80f, UiFontId.F24, UiTheme.Chalk);
            float bw = 176f, bx = UiScale.RefWidth - OnlineUi.Edge - 4f * bw - 3f * 8f;
            _profile = UiKit.Button(Ctx, band, T(OnlineKeys.ProfileButton), bx, 16f, bw, 48f, OnProfile, UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F1", "record"));
            _friends = UiKit.Button(Ctx, band, T(OnlineKeys.FriendsButton), bx + (bw + 8f), 16f, bw, 48f, () => OpenChild(ScreenId.OnlineFriends), UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F1", "friends"));
            _market = UiKit.Button(Ctx, band, T(OnlineKeys.MarketButton), bx + 2f * (bw + 8f), 16f, bw, 48f, () => OpenChild(ScreenId.OnlineMarket), UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F1", "market"));
            _logout = UiKit.Button(Ctx, band, T(OnlineKeys.Logout), bx + 3f * (bw + 8f), 16f, bw, 48f, OnLogout, UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F1", "exit"));
        }

        static readonly string[] CardArt = { "mode_expedition", "mode_duel", "mode_square" };
        static readonly string[] CardTitle = { OnlineKeys.ModeCoop, OnlineKeys.ModeDuel, OnlineKeys.Plaza };
        static readonly string[] CardDesc = { OnlineKeys.HubDescCoop, OnlineKeys.HubDescDuel, OnlineKeys.HubDescPlaza };
        static readonly string[] CardIcon = { "expedition", "duel", "square" };

        int _picked; // 0 협동, 1 결투, 2 광장

        void BuildOutside(RectTransform content)
        {
            _outside = UiKit.Node(content, "Outside", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                float x = CardX + i * (CardW + CardGap);
                var b = UiKit.Button(Ctx, _outside, null, x, CardY, CardW, CardH, () => PickCard(k), UiButtonStyle.Plain, Focus);
                var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                UiKit.Panel(Ctx, r, UiPanelKind.Board, 0f, 0f, CardW, CardH).raycastTarget = false;
                OnlineUi.Img(Ctx, r, "S-0502", CardArt[i], 16f, 16f, 368f, 240f);
                OnlineUi.Img(Ctx, r, "I-F1", CardIcon[i], 24f, 276f, 32f, 32f);
                UiKit.Label(Ctx, r, T(CardTitle[i]), UiFontId.F32, 64f, 270f, CardW - 80f, 44f, UiTheme.Chalk, TextAnchor.MiddleLeft);
                UiKit.Label(Ctx, r, T(CardDesc[i]), UiFontId.F16, 24f, 324f, CardW - 48f, 72f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
                UiKit.Divider(Ctx, r, 24f, 420f, CardW - 48f);
                _cardRecords[i] = UiKit.Label(Ctx, r, string.Empty, UiFontId.F16, 24f, 436f, CardW - 48f, 32f, UiTheme.Chalk, TextAnchor.MiddleLeft);
                _cards[i] = b;
            }

            var panel = UiKit.Panel(Ctx, _outside, UiPanelKind.Board, PanelX, PanelY, PanelW, PanelH).rectTransform;
            UiKit.Bevel(panel);
            float inner = PanelW - 2f * PanelPad, y = PanelPad;
            _panelTitle = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F32, PanelPad, y, inner, 44f, UiTheme.Chalk, TextAnchor.MiddleLeft);
            y += 52f;
            _panelDesc = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F16, PanelPad, y, inner, 72f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            y += 88f;
            _roomActions = UiKit.Node(panel, "RoomActions", 0f, y, PanelW, PanelH - y);
            float ry = 0f;
            _createOnline = UiKit.Button(Ctx, _roomActions, T(OnlineKeys.HubCreate), PanelPad, ry, inner, 64f, OnCreateOnline, UiButtonStyle.Primary, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "plus"));
            ry += 96f;
            _joinHead = UiKit.Label(Ctx, _roomActions, T(OnlineKeys.HubJoinHead), UiFontId.F16, PanelPad, ry, inner, 24f, UiTheme.SlateBlue);
            ry += 28f;
            _codeInput = OnlineUi.Field(Ctx, Focus, _roomActions, null, T(OnlineKeys.CodeHint), PanelPad, ry, inner - 152f, RoomServer.CodeLength + 2, false, UiFontId.F32);
            _codeInput.characterValidation = UInputField.CharacterValidation.Alphanumeric;
            _join = UiKit.Button(Ctx, _roomActions, T(OnlineKeys.Join), PanelPad + inner - 140f, ry, 140f, 48f, OnJoin, UiButtonStyle.Medium, Focus);
            ry += 80f;
            UiKit.Divider(Ctx, _roomActions, PanelPad, ry, inner);
            ry += 24f;
            _createLocal = UiKit.Button(Ctx, _roomActions, T(OnlineKeys.HubLocal), PanelPad, ry, inner, 48f, OnCreateLocal, UiButtonStyle.Medium, Focus);
            ry += 52f;
            _localNote = UiKit.Label(Ctx, _roomActions, T(OnlineKeys.HubLocalNote), UiFontId.F12, PanelPad, ry, inner, 20f, UiTheme.SlateBlue);
            _plazaEnter = UiKit.Button(Ctx, panel, T(OnlineKeys.HubPlazaEnter), PanelPad, y, inner, 64f, OnPlaza, UiButtonStyle.Primary, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F1", "square"));
        }

        void BuildInside(RectTransform content)
        {
            _inside = UiKit.Node(content, "Inside", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            // 핀에 꽂힌 쪽지: 방 코드.
            var note = OnlineUi.Nine(Ctx, _inside, "S-2102", "room_note", BenchX, BenchY, 440f, 152f, null, UiTheme.Paper).rectTransform;
            _codeCaption = UiKit.Label(Ctx, note, T(OnlineKeys.HubCodeCaption), UiFontId.F16, 28f, 20f, 300f, 24f, UiTheme.PaperText);
            _codeLabel = UiKit.Label(Ctx, note, string.Empty, UiFontId.F48, 28f, 48f, 300f, 64f, UiTheme.PaperText, TextAnchor.MiddleLeft);
            _copy = UiKit.Button(Ctx, note, T(OnlineKeys.HubCopy), 316f, 56f, 104f, 48f, OnCopy, UiButtonStyle.Small, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "copy"));
            _share = UiKit.Label(Ctx, _inside, T(OnlineKeys.HubShare), UiFontId.F24, BenchX + 472f, BenchY + 56f, 800f, 40f, UiTheme.Chalk);
            UiKit.DropShadow(_share);
            _benchRoot = UiKit.Node(_inside, "Bench", BenchX, BenchY + 184f, BenchW, BenchH - 184f);
            for (int i = 0; i < SimConst.MaxPlayers; i++) _seats.Add(BuildSeat(_benchRoot));
            _versus = OnlineUi.AnimImg(Ctx, _benchRoot, "S-0902", "versus_hangul", 0f, 0f, 256f, 160f, 6f, loop: false);

            var panel = UiKit.Panel(Ctx, _inside, UiPanelKind.Board, PanelX, PanelY, PanelW, PanelH).rectTransform;
            UiKit.Bevel(panel);
            float inner = PanelW - 2f * PanelPad, y = PanelPad;
            _roomTitle = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F32, PanelPad, y, inner, 44f, UiTheme.Chalk);
            _members = OnlineUi.IconText(Ctx, panel, "I-F3", "person", string.Empty, PanelPad, y + 52f, inner, 32f, UiFontId.F24);
            _roomDesc = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F16, PanelPad, y + 96f, inner, 56f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            y += 164f;
            _resultBanner = UiKit.Image(panel, null, PanelPad - 8f, y, inner + 16f, 64f, Color.white, true);
            y += 88f;
            _start = UiKit.Button(Ctx, panel, T(OnlineKeys.Start), PanelPad, y, inner, 64f, OnStart, UiButtonStyle.Primary, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "play"));
            y += 88f;
            float half = (inner - 12f) / 2f;
            _addBot = UiKit.Button(Ctx, panel, T(OnlineKeys.AddBot), PanelPad, y, half, 48f, OnAddBot, UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "plus"));
            _removeBot = UiKit.Button(Ctx, panel, T(OnlineKeys.RemoveBot), PanelPad + half + 12f, y, half, 48f, OnRemoveBot, UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "minus"));
            _leave = UiKit.Button(Ctx, panel, T(OnlineKeys.Leave), PanelPad, PanelH - PanelPad - 48f, inner, 48f, OnLeave, UiButtonStyle.Medium, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "back"));
        }

        Seat BuildSeat(RectTransform parent)
        {
            var s = new Seat();
            s.Root = UiKit.Node(parent, "Seat", 0f, 0f, 288f, 560f);
            s.Board = UiKit.Panel(Ctx, s.Root, UiPanelKind.Recess, 0f, 0f, 288f, 560f);
            s.Board.color = UiTheme.WithAlpha(Color.white, 0.85f);
            s.Pedestal = OnlineUi.Img(Ctx, s.Root, "S-0503", "dirt_pedestal", 24f, 232f, 240f, 72f);
            s.Art = UiKit.Image(s.Root, null, 48f, 64f, 192f, 192f, Color.white, false);
            s.Leader = OnlineUi.Img(Ctx, s.Root, "S-2104", "room_leader", 16f, 16f, 32f, 32f);
            s.Name = UiKit.Label(Ctx, s.Root, string.Empty, UiFontId.F24, 16f, 316f, 256f, 36f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            s.Role = UiKit.Label(Ctx, s.Root, string.Empty, UiFontId.F16, 16f, 352f, 256f, 24f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            s.StageRoot = UiKit.Node(s.Root, "Stage", 84f, 384f, 160f, 28f);
            s.Stage = OnlineUi.IconText(Ctx, s.StageRoot, "I-F5", "hammer", string.Empty, 0f, 0f, 160f, 28f, UiFontId.F16, UiTheme.Chalk, 16f);
            s.Ready = OnlineUi.Img(Ctx, s.Root, "S-2103", "ready_ready", 96f, 440f, 96f, 32f);
            var tag = OnlineUi.Tag(Ctx, s.Root, T(OnlineKeys.Bot), 220f, 20f, UiTheme.SlateBlue, 52f);
            s.BotTag = (RectTransform)tag.transform.parent;
            s.Hourglass = OnlineUi.AnimImg(Ctx, s.Root, "S-2105", "waiting_hourglass", 120f, 180f, 48f, 48f);
            s.Waiting = UiKit.Label(Ctx, s.Root, T(OnlineKeys.HubWaitingSlot), UiFontId.F16, 16f, 240f, 256f, 28f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            return s;
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _model.Enter(args);
            _model.RefreshProfile();
            _picked = _model.Mode == RoomMode.Duel ? 1 : 0;
            _askedLogin = false;
            Refresh();
            Focus.SetCursor(0, false);
        }

        public override void Tick(float dt)
        {
            if (Screens != null && _codeInput != null) Screens.CaptureKeys = _codeInput.isFocused;
            if (_model.Tick()) { GoBattle(); return; }
            if (!_askedLogin && !_model.HasRoom && (_model.Account == null || _model.AuthRejected))
            {
                _askedLogin = true;
                if (_model.AuthRejected) OnlineAccount.Forget();
                var a = ScreenArgs.From(ScreenId.CoopLobby);
                a.OnResult = r =>
                {
                    _model.ReloadAccount();
                    if (_model.Account == null) { Close(); return; }
                    _askedLogin = false;
                    _model.RefreshProfile();
                };
                Screens?.Push(ScreenId.OnlineLogin, a);
                return;
            }
            Refresh();
        }

        public override void OnExit()
        {
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        void GoBattle() => Screens?.Replace(ScreenId.Battle, ScreenArgs.From(ScreenId.CoopLobby));

        void Refresh()
        {
            bool room = _model.HasRoom;
            _outside.gameObject.SetActive(!room);
            _inside.gameObject.SetActive(room);
            RefreshHeader(room);
            if (room) RefreshInside(); else RefreshOutside();

            string status = string.Empty;
            if (_model.ErrorKey != null) status = T(_model.ErrorKey);
            else if (_model.WaitingForStart) status = T(OnlineKeys.Waiting);
            else if (room && _model.Mode == RoomMode.Duel && _model.Room.MemberCount < 2) status = T(OnlineKeys.DuelNeedTwo);
            else if (_model.ResultWave >= 0) status = F(OnlineKeys.Result, "wave", _model.ResultWave, "score", N(_model.ResultScore));
            else if (_copiedTimer > 0f) status = T(OnlineKeys.HubCopied);
            _status.SetText(status);
            _status.Text.color = _model.ErrorKey != null ? UiTheme.Amber : UiTheme.Chalk;
            if (_copiedTimer > 0f) _copiedTimer -= Time.unscaledDeltaTime;
        }

        void RefreshHeader(bool room)
        {
            var acct = _model.Account;
            var pr = _model.Profile;
            _who.SetText(pr != null ? pr.Name : acct != null ? acct.Name ?? string.Empty : string.Empty);
            _kindTagRoot.gameObject.SetActive(acct != null);
            if (acct != null) _kindTag.SetText(T(OnlineKeys.KindPrefix + acct.Kind));
            _gold.SetText(pr != null ? N(pr.Gold) : _model.ProfileLoading ? "…" : "-");
            _rating.SetText(pr != null ? pr.Rating.ToString() : "-");
            _profile.Enabled = !room && acct != null;
            _friends.Enabled = !room && acct != null;
            _market.Enabled = !room && acct != null;
            _logout.Enabled = !room && acct != null;
        }

        void RefreshOutside()
        {
            var pr = _model.Profile;
            for (int i = 0; i < 3; i++)
            {
                _cards[i].Picked = i == _picked;
                _cards[i].On = i == _picked;
            }
            _cardRecords[0].SetText(pr != null ? F(OnlineKeys.HubRecCoop, "wave", pr.CoopBestWave, "runs", pr.CoopRuns) : T(OnlineKeys.HubLoading));
            _cardRecords[1].SetText(pr != null ? F(OnlineKeys.HubRecDuel, "rating", pr.Rating, "w", pr.DuelWins, "l", pr.DuelLosses) : T(OnlineKeys.HubLoading));
            _cardRecords[2].SetText(T(OnlineKeys.HubRecPlaza));
            _panelTitle.SetText(T(CardTitle[_picked]));
            _panelDesc.SetText(T(CardDesc[_picked]));
            bool plaza = _picked == 2;
            _roomActions.gameObject.SetActive(!plaza);
            _plazaEnter.gameObject.SetActive(plaza);
            bool online = _model.Account != null;
            _createOnline.Enabled = online;
            _join.Enabled = online;
            _codeInput.interactable = online;
            _plazaEnter.Enabled = online;
        }

        static string RoleKey(CoopLobbyModel m, int slot) => m.RoleKey(slot);

        void RefreshInside()
        {
            bool duel = _model.Mode == RoomMode.Duel;
            int max = _model.MaxMembers;
            var members = _model.Members();
            string code = _model.Code;
            _codeLabel.SetText(code ?? T(OnlineKeys.HubLocalRoom));
            _codeLabel.Text.fontSize = code != null ? (int)UiFontId.F48 : (int)UiFontId.F24;
            _copy.gameObject.SetActive(code != null);
            _share.gameObject.SetActive(code != null);
            _roomTitle.SetText(T(duel ? OnlineKeys.ModeDuel : OnlineKeys.ModeCoop));
            _roomDesc.SetText(T(duel ? OnlineKeys.HubDescDuel : OnlineKeys.HubDescCoop));
            _members.SetText(F(OnlineKeys.HubMembers, "n", _model.Room.MemberCount, "max", max));

            // 자리 배치: 협동 4자리(288, 간격 24), 결투 2자리 + 가운데 VS.
            float seatW = 288f, gap = duel ? 360f : 24f;
            float total = max * seatW + (max - 1) * gap;
            float x0 = (BenchW - total) * 0.5f;
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                bool shown = i < max;
                s.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                UiKit.Place(s.Root, x0 + i * (seatW + gap), 0f, seatW, 560f);
                CoopClient.LobbyMember? m = null;
                foreach (var mm in members) if (mm.Slot == i) { m = mm; break; }
                FillSeat(s, i, m);
            }
            _versus.gameObject.SetActive(duel);
            if (duel) UiKit.Place(_versus.rectTransform, x0 + seatW + (gap - 256f) * 0.5f, 200f, 256f, 160f);

            _addBot.Enabled = _model.CanAddBot;
            _removeBot.Enabled = _model.CanRemoveBot;
            _start.Enabled = _model.CanStart;
            _start.gameObject.SetActive(_model.Room.IsLeader);

            string banner = null;
            if (_model.ResultDuelWinner >= -1)
                banner = _model.ResultDuelWinner < 0 ? "result_banner_draw" : _model.ResultDuelWinner == _model.ResultMySlot ? "result_banner_victory" : "result_banner_defeat";
            var sp = banner != null ? OnlineUi.Sprite(Ctx, "S-1701", banner) : null;
            _resultBanner.gameObject.SetActive(sp != null);
            if (sp != null) _resultBanner.sprite = sp;
        }

        void FillSeat(Seat s, int slot, CoopClient.LobbyMember? mm)
        {
            bool filled = mm.HasValue;
            var m = mm.GetValueOrDefault();
            s.Art.gameObject.SetActive(filled);
            s.Pedestal.gameObject.SetActive(filled);
            s.Name.gameObject.SetActive(filled);
            s.Role.gameObject.SetActive(filled);
            s.StageRoot.gameObject.SetActive(filled);
            s.Ready.gameObject.SetActive(filled);
            s.Hourglass.gameObject.SetActive(!filled);
            s.Waiting.gameObject.SetActive(!filled);
            s.Board.color = UiTheme.WithAlpha(Color.white, filled ? 0.9f : 0.45f);
            bool leader = filled && RoleKey(_model, slot) == OnlineKeys.Host;
            s.Leader.gameObject.SetActive(leader);
            bool bot = filled && m.IsBot;
            s.BotTag.gameObject.SetActive(bot);
            if (!filled) return;
            var art = OnlineUi.CharacterArt(Ctx, m.CharId);
            s.Art.sprite = art;
            s.Art.color = art != null ? Color.white : UiTheme.WithAlpha(UiTheme.WoodLight, 0.6f);
            bool me = _model.Room != null && _model.Room.Slot == slot;
            s.Name.SetText(T(m.CharId));
            s.Name.Text.color = me ? UiTheme.SelectYellow : UiTheme.Chalk;
            string role = T(RoleKey(_model, slot));
            if (me) role = T(OnlineKeys.HubYou) + " · " + role;
            s.Role.SetText(role);
            s.Stage.SetText(F(OnlineKeys.HubStage, "stage", m.WandStage));
        }

        float _copiedTimer;

        void PickCard(int k)
        {
            _picked = k;
            if (k == 0) SelectMode(RoomMode.Coop);
            else if (k == 1) SelectMode(RoomMode.Duel);
            Refresh();
        }

        void SelectMode(RoomMode m)
        {
            _model.SelectedMode = m;
            _picked = m == RoomMode.Duel ? 1 : 0;
            _model.ResultDuelWinner = -2;
            _model.ResultWave = -1;
            Refresh();
        }

        void OpenChild(ScreenId id) { Screens?.Push(id, ScreenArgs.From(ScreenId.CoopLobby)); }
        void OnProfile() { OpenChild(ScreenId.OnlineProfile); }
        void OnCopy()
        {
            if (_model.Code == null) return;
            GUIUtility.systemCopyBuffer = _model.Code;
            _copiedTimer = 2f;
        }

        void OnLogout()
        {
            if (_model.HasRoom) return;
            // 이 기기 파일은 바로 지우고, 서버의 로그인 표 지우기는 따로 스레드로(서버가 없어도 막히지 않게).
            var acct = OnlineAccount.Load();
            OnlineAccount.Forget();
            OnlineAuth.Logout(Ctx != null ? Ctx.Content : null, acct);
            _model.ReloadAccount();
            _askedLogin = false;
        }

        void OnCreateOnline() { _model.CreateOnline(); Refresh(); }
        void OnCreateLocal() { _model.CreateRoom(); Refresh(); }
        void OnJoin() { _model.JoinCode(_codeInput.text); Refresh(); }
        void OnAddBot() { _model.AddBot(); Refresh(); }
        void OnRemoveBot() { _model.RemoveBot(); Refresh(); }
        void OnPlaza() { Screens?.Replace(ScreenId.Plaza, ScreenArgs.From(ScreenId.CoopLobby)); }

        void OnStart()
        {
            ulong seed = (ulong)System.DateTime.UtcNow.Ticks;
            if (!_model.Start(seed)) { Refresh(); return; }
            GoBattle();
        }

        void OnLeave()
        {
            if (_model.HasRoom) { _model.Leave(); Refresh(); return; }
            Close();
        }

        public override bool OnBack()
        {
            if (_model.HasRoom) { _model.Leave(); Refresh(); return true; }
            Close();
            return true;
        }
    }
}
