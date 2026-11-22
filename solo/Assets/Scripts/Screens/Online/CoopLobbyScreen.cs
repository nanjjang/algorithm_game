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
    /// 온라인 들머리(ScreenId.CoopLobby, docs/ui-assets.md 4.5·4.7·4.21절, Docs/14-online-ui.md). 12칸 격자(OnlineUi.Span).
    ///  - 위 띠: 왼쪽 제목, 가운데 "내 판"(이름·계정 종류·골드·레이팅), 오른쪽 아이콘 단추 4개(기록·친구·장터·로그아웃).
    ///  - 방 밖: 모드 카드 3장(440×640)이 화면 가운데. 고른 카드만 밝고 위로 올라오며, 카드 아래쪽에 그 모드의 할 일이 들어 있다
    ///    (협동·결투: [방 만들고 코드 받기], 코드 칸 + [참가], [이 기기에서 봇과] / 광장: [광장으로 가기]). 따로 떠 있는 판이 없다.
    ///  - 방 안: 왼쪽 위 방 코드 쪽지, 그 아래 자리(협동 4·결투 2 + 가운데 "대"), 오른쪽 판: 모드·인원·결과·[시작]·봇·[나가기].
    ///    결투 방은 결투장 바닥(S-0901) 장면. 내 자리에는 판 전에 캐릭터를 바꾸는 ←·→ 단추가 있다.
    ///  - 판이 끝나면 Battle이 결과와 함께 이 화면으로 돌아오고, 같은 방(같은 코드·자리·봇)에 그대로 남는다.
    /// 다시 그리기는 보이는 상태(방·로비 판·프로필·오류 …)가 바뀐 프레임에만 한다(<see cref="ViewKey"/>).
    /// 받아 온 그림(Docs/15)이 있으면 그것을 쓴다. 계정이 없으면 로그인 화면을 먼저 연다.
    /// </summary>
    public sealed class CoopLobbyScreen : ScreenBase
    {
        const float CardW = 440f, CardH = 640f, CardGap = 32f, CardY = 152f, Lift = 16f;
        const float SeatW = 288f, SeatH = 440f;

        CoopLobbyModel _model;
        bool _askedLogin;

        RectTransform _sceneCamp, _sceneDuel, _sceneRoom;

        UiLabel _who, _gold, _rating, _kindTag;
        RectTransform _kindTagRoot;
        readonly List<UiButton> _nav = new List<UiButton>();

        RectTransform _outside, _roomActions;
        sealed class Card
        {
            public UiButton Button;
            public RectTransform Root, Actions;
            public CanvasGroup Fade;
            public UiLabel Record, Hint;
            public UImage SelectedFrame;
        }
        readonly Card[] _cards = new Card[3];
        UiButton _createOnline, _createLocal, _join, _plazaEnter;
        UInputField _codeInput;

        RectTransform _inside, _panel;
        UiLabel _codeLabel, _localLabel, _share, _roomTitle, _members, _roomDesc;
        UiButton _copy;
        RectTransform _benchRoot;
        readonly List<Seat> _seats = new List<Seat>();
        UImage _versus;
        UiButton _addBot, _removeBot, _start, _leave;
        UImage _resultBanner;

        UiLabel _status;
        float _copiedTimer;
        int _picked; // 0 협동, 1 결투, 2 광장

        sealed class Seat
        {
            public UiButton Prev, Next;
            public RectTransform Root, BotTag, StageRoot;
            public UImage Frame, EmptyFrame, Art, Pedestal, Ready, Leader, Hourglass;
            public UiLabel Name, Role, Stage, Waiting;
        }

        public override ScreenId Id => ScreenId.CoopLobby;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            _model = new CoopLobbyModel(Ctx);
            _sceneCamp = OnlineUi.Scene(Ctx, content, "S-0501", "camp_background", 0.4f, "bg_online_hub");
            if (OnlineUi.Online("bg_online_hub") == null)
                OnlineUi.AnimImg(Ctx, _sceneCamp, "S-0501", "campfire", 1236f - 64f, 780f - 100f, 128f, 128f);
            _sceneDuel = OnlineUi.Scene(Ctx, content, "S-0901", "duel_floor", 0.45f);
            // 협동 방 안(천막, bg_room). 받아 온 그림이 없으면 들머리 장면을 그대로 쓴다.
            if (OnlineUi.Online("bg_room") != null) _sceneRoom = OnlineUi.Scene(Ctx, content, null, null, 0.25f, "bg_room");
            BuildHeader(content);
            BuildOutside(content);
            BuildInside(content);
            _status = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, OnlineUi.Edge, 1000f, OnlineUi.Span(12), 40f, UiTheme.Amber, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_status);
        }

        void BuildHeader(RectTransform content)
        {
            OnlineUi.HeaderBand(Ctx, content, "I-F1", "square", T(OnlineKeys.Title), out var band);
            float px = 600f, pw = 720f;
            var plate = UiKit.Panel(Ctx, band, UiPanelKind.Recess, px, 12f, pw, 56f).rectTransform;
            OnlineUi.Img(Ctx, plate, "I-F3", "person", 16f, 16f, 24f, 24f);
            _who = UiKit.Label(Ctx, plate, string.Empty, UiFontId.F24, 48f, 0f, 240f, 56f, UiTheme.Chalk);
            _kindTag = OnlineUi.Tag(Ctx, plate, T(OnlineKeys.KindEmail), 296f, 16f, UiTheme.SlateBlue, 112f);
            _kindTagRoot = (RectTransform)_kindTag.transform.parent;
            _gold = OnlineUi.IconText(Ctx, plate, "I-C2", "gold", string.Empty, 432f, 0f, 150f, 56f, UiFontId.F24, UiTheme.Gold);
            _rating = OnlineUi.IconText(Ctx, plate, "I-L1", "league_sprout", string.Empty, 596f, 0f, 116f, 56f, UiFontId.F24, UiTheme.Chalk);
            string[] icons = { "record", "friends", "market", "square", "exit" };
            string[] labels = { OnlineKeys.NavRecords, OnlineKeys.NavFriends, OnlineKeys.NavMarket, OnlineKeys.NavSeason, OnlineKeys.NavLogout };
            System.Action[] acts = { OnProfile, () => OpenChild(ScreenId.OnlineFriends), () => OpenChild(ScreenId.OnlineMarket), OpenSeason, OnLogout };
            float bx = UiScale.RefWidth - OnlineUi.Edge - 5f * 96f - 4f * 8f;
            for (int i = 0; i < 5; i++)
            {
                var b = UiKit.Button(Ctx, band, null, bx + i * 104f, 4f, 96f, 72f, acts[i], UiButtonStyle.Plain, Focus);
                var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                OnlineUi.Img(Ctx, r, "I-F1", icons[i], 32f, 6f, 32f, 32f);
                UiKit.Label(Ctx, r, T(labels[i]), UiFontId.F16, 0f, 42f, 96f, 24f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                _nav.Add(b);
            }
        }

        static readonly string[] CardArt = { "mode_expedition", "mode_duel", "mode_square" };
        static readonly string[] CardTitle = { OnlineKeys.ModeCoop, OnlineKeys.ModeDuel, OnlineKeys.Plaza };
        static readonly string[] CardDesc = { OnlineKeys.HubDescCoop, OnlineKeys.HubDescDuel, OnlineKeys.HubDescPlaza };
        static readonly string[] CardIcon = { "expedition", "duel", "square" };

        static float CardX(int i) => (UiScale.RefWidth - (3f * CardW + 2f * CardGap)) * 0.5f + i * (CardW + CardGap);

        void BuildOutside(RectTransform content)
        {
            _outside = UiKit.Node(content, "Outside", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var c = new Card();
                c.Root = UiKit.Node(_outside, "Card" + i, CardX(i), CardY, CardW, CardH);
                c.Fade = c.Root.gameObject.AddComponent<CanvasGroup>();
                c.Button = UiKit.Button(Ctx, c.Root, null, 0f, 0f, CardW, CardH, () => PickCard(k), UiButtonStyle.Plain, Focus);
                var r = c.Button.ContentRoot != null ? c.Button.ContentRoot : c.Button.Rect;
                if (OnlineUi.OnlineImg(r, "frame_mode_card", 0f, 0f, CardW, CardH) == null)
                    UiKit.Panel(Ctx, r, UiPanelKind.Board, 0f, 0f, CardW, CardH).raycastTarget = false;
                c.SelectedFrame = OnlineUi.OnlineImg(r, "frame_mode_card_selected", 0f, 0f, CardW, CardH);
                OnlineUi.Img(Ctx, r, "S-0502", CardArt[i], (CardW - 368f) * 0.5f, 24f, 368f, 240f);
                OnlineUi.Img(Ctx, r, "I-F1", CardIcon[i], 36f, 284f, 32f, 32f);
                UiKit.Label(Ctx, r, T(CardTitle[i]), UiFontId.F32, 80f, 278f, CardW - 112f, 44f, UiTheme.Chalk);
                UiKit.Label(Ctx, r, T(CardDesc[i]), UiFontId.F16, 36f, 332f, CardW - 72f, 52f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
                c.Record = UiKit.Label(Ctx, r, string.Empty, UiFontId.F16, 36f, 388f, CardW - 72f, 28f, UiTheme.Chalk);
                UiKit.Divider(Ctx, r, 36f, 428f, CardW - 72f);
                c.Hint = UiKit.Label(Ctx, r, T(OnlineKeys.HubPickHint), UiFontId.F24, 36f, 500f, CardW - 72f, 64f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
                c.Actions = UiKit.Node(c.Root, "Actions", 0f, 444f, CardW, 180f);
                _cards[i] = c;
            }
            _roomActions = UiKit.Node(_outside, "RoomActions", 0f, 0f, CardW, 180f);
            float inner = CardW - 72f;
            _createOnline = UiKit.Button(Ctx, _roomActions, T(OnlineKeys.HubCreate), 36f, 0f, inner, 56f, OnCreateOnline, UiButtonStyle.Primary, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "plus"));
            _codeInput = OnlineUi.Field(Ctx, Focus, _roomActions, null, T(OnlineKeys.CodeHint), 36f, 68f, inner - 132f, RoomServer.CodeLength + 2, false, UiFontId.F24);
            _codeInput.characterValidation = UInputField.CharacterValidation.Alphanumeric;
            _join = UiKit.Button(Ctx, _roomActions, T(OnlineKeys.Join), 36f + inner - 120f, 68f, 120f, 48f, OnJoin, UiButtonStyle.Medium, Focus);
            _createLocal = UiKit.Button(Ctx, _roomActions, T(OnlineKeys.HubLocal), 36f, 128f, inner, 40f, OnCreateLocal, UiButtonStyle.Small, Focus, null, UiFontId.F16);
            _plazaEnter = UiKit.Button(Ctx, _cards[2].Actions, T(OnlineKeys.HubPlazaEnter), 36f, 0f, inner, 64f, OnPlaza, UiButtonStyle.Primary, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F1", "square"));
        }

        void BuildInside(RectTransform content)
        {
            _inside = UiKit.Node(content, "Inside", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            float leftW = OnlineUi.Span(9);
            var noteImg = OnlineUi.OnlineImg(_inside, "frame_code_note", OnlineUi.Edge, 112f, 480f, 176f);
            var note = noteImg != null ? noteImg.rectTransform : OnlineUi.Nine(Ctx, _inside, "S-2102", "room_note", OnlineUi.Edge, 112f, 480f, 176f, null, UiTheme.Paper).rectTransform;
            float nx = noteImg != null ? 56f : 32f; // 받아 온 쪽지는 모서리 장식이 넓다
            UiKit.Label(Ctx, note, T(OnlineKeys.HubCodeCaption), UiFontId.F16, nx, 28f, 300f, 24f, UiTheme.PaperText);
            _codeLabel = UiKit.Label(Ctx, note, string.Empty, UiFontId.F64, nx, 56f, 288f, 88f, UiTheme.PaperText, TextAnchor.MiddleLeft);
            _localLabel = UiKit.Label(Ctx, note, T(OnlineKeys.HubLocalRoom), UiFontId.F32, nx, 56f, 416f - (nx - 32f), 88f, UiTheme.PaperText, TextAnchor.MiddleLeft);
            _copy = UiKit.Button(Ctx, note, T(OnlineKeys.HubCopy), 344f, 76f, 112f, 48f, OnCopy, UiButtonStyle.Small, Focus, null, UiFontId.F16,
                OnlineUi.Online("icon_copy") ?? OnlineUi.Sprite(Ctx, "I-F2", "copy"));
            _share = UiKit.Label(Ctx, _inside, T(OnlineKeys.HubShare), UiFontId.F24, OnlineUi.Edge + 512f, 176f, leftW - 512f, 48f, UiTheme.Chalk);
            UiKit.DropShadow(_share);
            _benchRoot = UiKit.Node(_inside, "Bench", OnlineUi.Edge, 336f, leftW, SeatH);
            for (int i = 0; i < SimConst.MaxPlayers; i++) _seats.Add(BuildSeat(_benchRoot));
            _versus = OnlineUi.OnlineImg(_benchRoot, "vs_emblem", 0f, 0f, 256f, 256f)
                ?? OnlineUi.AnimImg(Ctx, _benchRoot, "S-0902", "versus_hangul", 0f, 0f, 256f, 160f, 6f, loop: false);

            float px = OnlineUi.ColX(9), pw = OnlineUi.Span(3);
            _panel = UiKit.Panel(Ctx, _inside, UiPanelKind.Board, px, 112f, pw, 496f).rectTransform;
            UiKit.Bevel(_panel);
            float P = 28f, inner = pw - 2f * P, y = 24f;
            _roomTitle = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F32, P, y, inner, 44f, UiTheme.Chalk);
            y += 48f;
            _members = OnlineUi.IconText(Ctx, _panel, "I-F3", "person", string.Empty, P, y, inner, 32f, UiFontId.F24);
            y += 40f;
            _roomDesc = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, P, y, inner, 72f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            y += 84f;
            // 결과 띠(S-1701 1200×120)는 판 밖, 자리 위 가운데에 크게.
            _resultBanner = UiKit.Image(_inside, null, OnlineUi.Edge + (OnlineUi.Span(9) - 960f) * 0.5f, 232f, 960f, 96f, Color.white, true);
            _start = UiKit.Button(Ctx, _panel, T(OnlineKeys.Start), P, y, inner, 64f, OnStart, UiButtonStyle.Primary, Focus, null, UiFontId.F32, OnlineUi.Sprite(Ctx, "I-F2", "play"));
            y += 80f;
            _addBot = UiKit.Button(Ctx, _panel, T(OnlineKeys.AddBot), P, y, inner, 48f, OnAddBot, UiButtonStyle.Medium, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "plus"));
            y += 56f;
            _removeBot = UiKit.Button(Ctx, _panel, T(OnlineKeys.RemoveBot), P, y, inner, 48f, OnRemoveBot, UiButtonStyle.Medium, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "minus"));
            y += 80f;
            _leave = UiKit.Button(Ctx, _panel, T(OnlineKeys.Leave), P, y, inner, 48f, OnLeave, UiButtonStyle.Medium, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "back"));
        }

        Seat BuildSeat(RectTransform parent)
        {
            var s = new Seat();
            s.Root = UiKit.Node(parent, "Seat", 0f, 0f, SeatW, SeatH);
            s.Frame = OnlineUi.OnlineImg(s.Root, "frame_seat", 0f, 0f, SeatW, SeatH) ?? UiKit.Panel(Ctx, s.Root, UiPanelKind.Board, 0f, 0f, SeatW, SeatH);
            s.EmptyFrame = OnlineUi.OnlineImg(s.Root, "frame_seat_empty", 0f, 0f, SeatW, SeatH);
            if (s.EmptyFrame == null)
            {
                s.EmptyFrame = UiKit.Panel(Ctx, s.Root, UiPanelKind.Recess, 0f, 0f, SeatW, SeatH);
                s.EmptyFrame.color = UiTheme.WithAlpha(Color.white, 0.45f);
            }
            s.Pedestal = OnlineUi.Img(Ctx, s.Root, "S-0503", "dirt_pedestal", (SeatW - 240f) * 0.5f, 220f, 240f, 72f);
            s.Art = UiKit.Image(s.Root, null, (SeatW - 192f) * 0.5f, 52f, 192f, 192f, Color.white, false);
            s.Leader = OnlineUi.Img(Ctx, s.Root, "S-2104", "room_leader", 20f, 20f, 32f, 32f);
            var tag = OnlineUi.Tag(Ctx, s.Root, T(OnlineKeys.Bot), SeatW - 76f, 24f, UiTheme.SlateBlue, 52f);
            s.BotTag = (RectTransform)tag.transform.parent;
            OnlineUi.OnlineImg(s.BotTag, "badge_bot", -36f, -4f, 32f, 32f); // 꼬리표(높이 24) 왼쪽에 톱니 표
            s.Name = UiKit.Label(Ctx, s.Root, string.Empty, UiFontId.F32, 16f, 300f, SeatW - 32f, 44f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            s.Role = UiKit.Label(Ctx, s.Root, string.Empty, UiFontId.F16, 16f, 344f, SeatW - 32f, 24f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            s.StageRoot = UiKit.Node(s.Root, "Stage", (SeatW - 140f) * 0.5f, 372f, 140f, 28f);
            s.Stage = OnlineUi.IconText(Ctx, s.StageRoot, "I-F5", "hammer", string.Empty, 0f, 0f, 140f, 28f, UiFontId.F16, UiTheme.Chalk, 16f);
            s.Ready = OnlineUi.OnlineImg(s.Root, "badge_ready", (SeatW - 96f) * 0.5f, 400f, 96f, 40f)
                ?? OnlineUi.Img(Ctx, s.Root, "S-2103", "ready_ready", (SeatW - 96f) * 0.5f, 404f, 96f, 32f);
            s.Hourglass = OnlineUi.OnlineImg(s.Root, "badge_waiting", (SeatW - 96f) * 0.5f, 176f, 96f, 40f)
                ?? OnlineUi.AnimImg(Ctx, s.Root, "S-2105", "waiting_hourglass", (SeatW - 48f) * 0.5f, 172f, 48f, 48f);
            s.Waiting = UiKit.Label(Ctx, s.Root, T(OnlineKeys.HubWaitingSlot), UiFontId.F24, 16f, 232f, SeatW - 32f, 36f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            // 내 자리의 캐릭터 바꾸기(판 전). 그림 양옆, 포커스 묶음에 들어간다.
            s.Prev = UiKit.Button(Ctx, s.Root, T(OnlineKeys.CharPrev), 8f, 116f, 44f, 64f, () => OnChangeCharacter(-1), UiButtonStyle.Small, Focus, null, UiFontId.F32);
            s.Next = UiKit.Button(Ctx, s.Root, T(OnlineKeys.CharNext), SeatW - 52f, 116f, 44f, 64f, () => OnChangeCharacter(1), UiButtonStyle.Small, Focus, null, UiFontId.F32);
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
            if (_copiedTimer > 0f) _copiedTimer -= Time.unscaledDeltaTime;
            // 매 프레임 다시 그리지 않는다: 보이는 상태가 바뀐 때만(로비 목록·프로필·오류·결과 …).
            var key = ViewKey();
            if (!key.Equals(_shown)) Refresh();
        }

        /// <summary>화면에 보이는 것을 정하는 값들. 같으면 Refresh를 건너뛴다(목록 만들기·글자 찾기·SetText를 하지 않는다).</summary>
        struct ViewState : System.IEquatable<ViewState>
        {
            public object Room, Profile, Account;
            public string Error;
            public int LobbyVersion, ResultWave, DuelWinner, Picked;
            public bool HasRoom, Waiting, Returning, Loading, Copied, CanChange, CanStart, CanAdd, CanRemove, Leader;

            public bool Equals(ViewState o) =>
                ReferenceEquals(Room, o.Room) && ReferenceEquals(Profile, o.Profile) && ReferenceEquals(Account, o.Account)
                && ReferenceEquals(Error, o.Error) && LobbyVersion == o.LobbyVersion && ResultWave == o.ResultWave
                && DuelWinner == o.DuelWinner && Picked == o.Picked && HasRoom == o.HasRoom && Waiting == o.Waiting
                && Returning == o.Returning && Loading == o.Loading && Copied == o.Copied && CanChange == o.CanChange
                && CanStart == o.CanStart && CanAdd == o.CanAdd && CanRemove == o.CanRemove && Leader == o.Leader;
            public override bool Equals(object obj) => obj is ViewState v && Equals(v);
            public override int GetHashCode() => LobbyVersion;
        }

        ViewState _shown;

        ViewState ViewKey()
        {
            bool room = _model.HasRoom;
            return new ViewState
            {
                Room = _model.Room, Profile = _model.Profile, Account = _model.Account, Error = _model.ErrorKey,
                LobbyVersion = _model.Room != null ? _model.Room.LobbyVersion : 0,
                ResultWave = _model.ResultWave, DuelWinner = _model.ResultDuelWinner, Picked = _picked,
                HasRoom = room, Waiting = _model.WaitingForStart, Returning = _model.ReturningToRoom, Loading = _model.ProfileLoading,
                Copied = _copiedTimer > 0f,
                CanChange = room && _model.CanChangeCharacter, CanStart = room && _model.CanStart,
                CanAdd = room && _model.CanAddBot, CanRemove = room && _model.CanRemoveBot, Leader = room && _model.Room.IsLeader,
            };
        }

        public override void OnExit()
        {
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        void GoBattle() => Screens?.Replace(ScreenId.Battle, ScreenArgs.From(ScreenId.CoopLobby));

        void Refresh()
        {
            _shown = ViewKey();
            bool room = _model.HasRoom;
            bool duelRoom = room && _model.Mode == RoomMode.Duel;
            _sceneDuel.gameObject.SetActive(duelRoom);
            bool coopRoom = room && !duelRoom && _sceneRoom != null;
            if (_sceneRoom != null) _sceneRoom.gameObject.SetActive(coopRoom);
            _sceneCamp.gameObject.SetActive(!duelRoom && !coopRoom);
            _outside.gameObject.SetActive(!room);
            _inside.gameObject.SetActive(room);
            RefreshHeader(room);
            if (room) RefreshInside(); else RefreshOutside();

            string status = string.Empty;
            if (_model.ErrorKey != null) status = T(_model.ErrorKey);
            else if (_model.WaitingForStart) status = T(OnlineKeys.Waiting);
            else if (_model.ReturningToRoom) status = T(OnlineKeys.Returning);
            else if (room && _model.Mode == RoomMode.Duel && _model.Room.MemberCount < 2) status = T(OnlineKeys.DuelNeedTwo);
            else if (_model.ResultWave >= 0) status = F(OnlineKeys.Result, "wave", _model.ResultWave, "score", N(_model.ResultScore));
            else if (_copiedTimer > 0f) status = T(OnlineKeys.HubCopied);
            _status.SetText(status);
            _status.Text.color = _model.ErrorKey != null ? UiTheme.Amber : UiTheme.Chalk;
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
            foreach (var b in _nav) b.Enabled = !room && acct != null;
        }

        void RefreshOutside()
        {
            var pr = _model.Profile;
            for (int i = 0; i < 3; i++)
            {
                var c = _cards[i];
                bool sel = i == _picked;
                c.Button.Picked = sel;
                c.Fade.alpha = sel ? 1f : 0.62f;
                UiKit.Place(c.Root, CardX(i), CardY - (sel ? Lift : 0f), CardW, CardH);
                c.Hint.gameObject.SetActive(!sel);
                c.Actions.gameObject.SetActive(sel);
                if (c.SelectedFrame != null) c.SelectedFrame.gameObject.SetActive(sel);
            }
            _cards[0].Record.SetText(pr != null ? F(OnlineKeys.HubRecCoop, "wave", pr.CoopBestWave, "runs", pr.CoopRuns) : T(OnlineKeys.HubLoading));
            _cards[1].Record.SetText(pr != null ? F(OnlineKeys.HubRecDuel, "rating", pr.Rating, "w", pr.DuelWins, "l", pr.DuelLosses) : T(OnlineKeys.HubLoading));
            _cards[2].Record.SetText(T(OnlineKeys.HubRecPlaza));
            bool plaza = _picked == 2;
            _roomActions.gameObject.SetActive(!plaza);
            if (!plaza && _roomActions.parent != _cards[_picked].Actions)
            {
                _roomActions.SetParent(_cards[_picked].Actions, false);
                UiKit.Place(_roomActions, 0f, 0f, CardW, 180f);
            }
            bool online = _model.Account != null;
            _createOnline.Enabled = online;
            _join.Enabled = online;
            _codeInput.interactable = online;
            _plazaEnter.Enabled = online;
        }

        void RefreshInside()
        {
            bool duel = _model.Mode == RoomMode.Duel;
            int max = _model.MaxMembers;
            var members = _model.Members();
            string code = _model.Code;
            _codeLabel.gameObject.SetActive(code != null);
            _localLabel.gameObject.SetActive(code == null);
            if (code != null) _codeLabel.SetText(code);
            _copy.gameObject.SetActive(code != null);
            _share.gameObject.SetActive(code != null);
            _roomTitle.SetText(T(duel ? OnlineKeys.ModeDuel : OnlineKeys.ModeCoop));
            _roomDesc.SetText(T(duel ? OnlineKeys.HubDescDuel : OnlineKeys.HubDescCoop));
            _members.SetText(F(OnlineKeys.HubMembers, "n", _model.Room.MemberCount, "max", max));

            float benchW = OnlineUi.Span(9);
            // 결투는 결투장 바닥의 가운데 선(x 960)을 사이에 두고 마주 선다.
            float gap = duel ? 256f : OnlineUi.Gutter;
            float total = max * SeatW + (max - 1) * gap;
            float x0 = duel ? UiScale.RefWidth * 0.5f - OnlineUi.Edge - total * 0.5f : (benchW - total) * 0.5f;
            _canChange = _model.CanChangeCharacter;
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                bool shown = i < max;
                s.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                UiKit.Place(s.Root, x0 + i * (SeatW + gap), 0f, SeatW, SeatH);
                CoopClient.LobbyMember? m = null;
                foreach (var mm in members) if (mm.Slot == i) { m = mm; break; }
                FillSeat(s, i, m);
            }
            _versus.gameObject.SetActive(duel);
            if (duel)
            {
                float vh = _versus.sprite != null && _versus.sprite.rect.width > 0f ? 256f * _versus.sprite.rect.height / _versus.sprite.rect.width : 160f;
                UiKit.Place(_versus.rectTransform, x0 + SeatW + (gap - 256f) * 0.5f, (SeatH - vh) * 0.5f, 256f, vh);
            }

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

        bool _canChange;

        void FillSeat(Seat s, int slot, CoopClient.LobbyMember? mm)
        {
            bool filled = mm.HasValue;
            var m = mm.GetValueOrDefault();
            bool mine = filled && !m.IsBot && _model.Room != null && _model.Room.Slot == slot && !_model.Watching;
            s.Prev.gameObject.SetActive(mine);
            s.Next.gameObject.SetActive(mine);
            s.Prev.Enabled = mine && _canChange;
            s.Next.Enabled = mine && _canChange;
            s.Frame.gameObject.SetActive(filled);
            s.EmptyFrame.gameObject.SetActive(!filled);
            s.Art.gameObject.SetActive(filled);
            s.Pedestal.gameObject.SetActive(filled);
            s.Name.gameObject.SetActive(filled);
            s.Role.gameObject.SetActive(filled);
            s.StageRoot.gameObject.SetActive(filled);
            s.Ready.gameObject.SetActive(filled);
            s.Hourglass.gameObject.SetActive(!filled);
            s.Waiting.gameObject.SetActive(!filled);
            s.Leader.gameObject.SetActive(filled && _model.RoleKey(slot) == OnlineKeys.Host);
            s.BotTag.gameObject.SetActive(filled && m.IsBot);
            if (!filled) return;
            var art = OnlineUi.CharacterArt(Ctx, m.CharId);
            s.Art.sprite = art;
            s.Art.color = art != null ? Color.white : UiTheme.WithAlpha(UiTheme.WoodLight, 0.6f);
            bool me = _model.Room != null && _model.Room.Slot == slot;
            s.Name.SetText(T(m.CharId));
            s.Name.Text.color = me ? UiTheme.SelectYellow : UiTheme.Chalk;
            string role = T(_model.RoleKey(slot));
            if (me) role = T(OnlineKeys.HubYou) + " · " + role;
            s.Role.SetText(role);
            s.Stage.SetText(F(OnlineKeys.HubStage, "stage", m.WandStage));
        }

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

        /// <summary>시즌 화면. 닫히며 "join:코드"(자동 매치가 잡혔다) 또는 "watch:코드"(관전)를 돌려주면 그 방으로 간다.</summary>
        void OpenSeason()
        {
            var a = ScreenArgs.From(ScreenId.CoopLobby);
            a.OnResult = r =>
            {
                _model.RefreshProfile();
                if (r is string s)
                {
                    if (s.StartsWith("join:", System.StringComparison.Ordinal)) _model.JoinMatch(s.Substring(5));
                    else if (s.StartsWith("watch:", System.StringComparison.Ordinal)) _model.Spectate(s.Substring(6));
                }
                Refresh();
            };
            Screens?.Push(ScreenId.OnlineSeason, a);
        }
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
        void OnChangeCharacter(int delta) { _model.ChangeCharacter(delta); Refresh(); }
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
