using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;
using UInputField = UnityEngine.UI.InputField;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 광장(ScreenId.Plaza, 11-online.md 6단계). 좀비고식: 온라인 서버의 광장 하나에 최대 40명이 들어와 걸어 다니고(방향키·WASD),
    /// Enter로 말하면 머리 위에 말풍선이 6초 뜨고, 숫자 1~4로 감정 표현. 오른쪽 아래 [방으로]는 온라인 방 화면(코드 만들기·참가).
    /// 이름은 처음 들어올 때 정한다(기기에 기억, 프로필에는 쓰지 않는다). 캐릭터 = 마지막으로 고른 캐릭터.
    /// </summary>
    public sealed class PlazaScreen : ScreenBase
    {
        const float Cell = 46f;          // 광장 한 칸 = 46px (40×22칸 → 1840×1012)
        const float OriginX = 40f, OriginY = 34f;
        const float AvatarPx = 96f;
        const string NamePref = "wizard.plaza_name";
        static readonly string[] EmoteGlyphs = { "", "!", "?", "♥", "♪", "…", "★", "zZ", "^^" };

        /// <summary>광장의 문(좀비고식): 그 칸 안에 서서 Space를 누르면 그 종류의 방 화면이 열린다. 칸 좌표(광장 40×22칸).</summary>
        public struct Door
        {
            public Rect Area;
            public RoomMode Mode;
            public string LabelKey;
        }

        public static readonly Door[] Doors =
        {
            new Door { Area = new Rect(1f, 8f, 3f, 6f), Mode = RoomMode.Coop, LabelKey = OnlineKeys.DoorCoop },
            new Door { Area = new Rect(36f, 8f, 3f, 6f), Mode = RoomMode.Duel, LabelKey = OnlineKeys.DoorDuel },
        };

        /// <summary>그 자리가 들어 있는 문(없으면 −1).</summary>
        public static int DoorAt(Vector2 pos)
        {
            for (int i = 0; i < Doors.Length; i++) if (Doors[i].Area.Contains(pos)) return i;
            return -1;
        }

        UiLabel _doorPrompt, _tradeLine;
        PlazaTradePanel _trade;

        sealed class AvatarView
        {
            public RectTransform Root;
            public UImage Body;
            public UiLabel Name, Bubble, Emote;
            public RectTransform BubbleRoot;
            public float AnimT;
        }

        readonly PlazaClient _client = new PlazaClient();
        readonly Dictionary<int, AvatarView> _views = new Dictionary<int, AvatarView>();
        readonly List<int> _gone = new List<int>();
        RectTransform _field;
        UInputField _chat, _nameInput;
        UiLabel _status, _count;
        RectTransform _nameBoard;
        bool _entered;
        int _lastDir = -2;

        public override ScreenId Id => ScreenId.Plaza;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.Night, true, "Background");
            Sprite bg = null;
            try { bg = Ctx != null && Ctx.Art != null ? Ctx.Art.Background("S-0501") : null; } catch (System.Exception) { }
            _field = UiKit.Node(content, "PlazaField", OriginX, OriginY, PlazaHost.Width * Cell, PlazaHost.Height * Cell);
            if (bg != null) UiKit.Image(_field, bg, 0f, 0f, PlazaHost.Width * Cell, PlazaHost.Height * Cell, new Color(1f, 1f, 1f, 0.55f), false);
            else UiKit.Rect(_field, 0f, 0f, PlazaHost.Width * Cell, PlazaHost.Height * Cell, UiTheme.Wood, false, "Ground");
            // 가운데 분수(그림 대신 단순 도형).
            UiKit.Rect(_field, (PlazaHost.Width * 0.5f - 1.5f) * Cell, (PlazaHost.Height * 0.5f - 1f) * Cell, 3f * Cell, 2f * Cell,
                UiTheme.WithAlpha(UiTheme.ArcaneTeal, 0.35f), false, "Fountain");

            foreach (var d in Doors)
            {
                var door = UiKit.Rect(_field, d.Area.x * Cell, d.Area.y * Cell, d.Area.width * Cell, d.Area.height * Cell,
                    UiTheme.WithAlpha(d.Mode == RoomMode.Duel ? UiTheme.Amber : UiTheme.Good, 0.35f), false, "Door");
                UiKit.Label(Ctx, door.rectTransform, T(d.LabelKey), UiFontId.F16, -40f, -28f, d.Area.width * Cell + 80f, 24f,
                    UiTheme.Chalk, TextAnchor.MiddleCenter, outline: true);
            }
            float lh = UiTheme.LineHeight(UiFontId.F16);
            _doorPrompt = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, (UiScale.RefWidth - 900f) * 0.5f, UiScale.RefHeight - 140f, 900f, 40f,
                UiTheme.SelectYellow, TextAnchor.MiddleCenter, outline: true);
            _count = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, 48f, 40f, 600f, lh, UiTheme.Chalk);
            _status = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, 48f, 40f + lh, 900f, lh, UiTheme.Amber);

            float by = UiScale.RefHeight - 72f;
            _chat = BuildInput(content, 48f, by, 900f, 52f, OnlineKeys.ChatHint, 60);
            UiKit.Button(Ctx, content, T(OnlineKeys.ChatSend), 960f, by, 160f, 52f, SendChat, UiButtonStyle.Medium, Focus);
            UiKit.Button(Ctx, content, T(OnlineKeys.PlazaRooms), UiScale.RefWidth - 48f - 220f, by, 220f, 52f, GoRooms, UiButtonStyle.Primary, Focus);
            UiKit.Button(Ctx, content, T(OnlineKeys.Leave), UiScale.RefWidth - 48f - 220f - 12f - 160f, by, 160f, 52f, Leave, UiButtonStyle.Medium, Focus);

            _tradeLine = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, (UiScale.RefWidth - 1200f) * 0.5f, 110f, 1200f, 40f,
                UiTheme.SelectYellow, TextAnchor.MiddleCenter, outline: true);
            _trade = new PlazaTradePanel(Ctx, content, Focus, _client, T);

            // 이름 정하기 판(처음 한 번).
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, 560f, 260f).rectTransform;
            UiKit.Center(board, 560f, 260f);
            _nameBoard = board;
            UiKit.Label(Ctx, board, T(OnlineKeys.PlazaNameTitle), UiFontId.F24, 32f, 28f, 496f, 40f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _nameInput = BuildInput(board, 32f, 92f, 496f, 56f, OnlineKeys.PlazaNameHint, PlazaHost.NameMaxChars);
            UiKit.Button(Ctx, board, T(OnlineKeys.PlazaEnter), 180f, 172f, 200f, 56f, Enter, UiButtonStyle.Primary, Focus);
        }

        UInputField BuildInput(RectTransform parent, float x, float y, float w, float h, string hintKey, int limit)
        {
            var bg = UiKit.NineSlice(Ctx, parent, "K-33", "input", null, x, y, w, h, UiTheme.Chalk);
            bg.raycastTarget = true;
            var field = bg.gameObject.AddComponent<UInputField>();
            var text = UiKit.Label(Ctx, bg.rectTransform, string.Empty, UiFontId.F16, 10f, 0f, w - 20f, h, UiTheme.PaperText);
            var ut = text.GetComponent<UText>();
            ut.supportRichText = false;
            field.textComponent = ut;
            field.lineType = UInputField.LineType.SingleLine;
            field.characterLimit = limit;
            var ph = UiKit.Label(Ctx, bg.rectTransform, T(hintKey), UiFontId.F16, 10f, 0f, w - 20f, h, UiTheme.SlateBlue);
            field.placeholder = ph.GetComponent<UText>();
            return field;
        }

        string MyCharId
        {
            get
            {
                var p = Ctx != null ? Ctx.Profile : null;
                return Wizard.Solo.Ids.Char.Of(p != null && p.LastCharacter >= 1 ? p.LastCharacter : 1);
            }
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _entered = false;
            string saved = null;
            try { saved = PlayerPrefs.GetString(NamePref, null); } catch (System.Exception) { }
            _nameInput.text = string.IsNullOrEmpty(saved) ? T(MyCharId) : saved;
            _nameBoard.gameObject.SetActive(true);
            _status.SetText(string.Empty);
            _nameInput.ActivateInputField();
            // 온라인 계정이 있으면 그 이름으로 바로 들어간다.
            var acct = OnlineAccount.Load();
            if (acct != null) { _nameInput.text = acct.Name; Enter(); }
        }

        void Enter()
        {
            string name = PlazaHost.Clean(_nameInput.text, PlazaHost.NameMaxChars);
            if (string.IsNullOrEmpty(name)) return;
            try { PlayerPrefs.SetString(NamePref, name); PlayerPrefs.Save(); } catch (System.Exception) { }
            var pipe = OnlineServer.ConnectAuthed(Ctx != null ? Ctx.Content : null);
            if (pipe == null) { _status.SetText(T(OnlineKeys.ErrorServer)); return; }
            _client.Connect(pipe, name, MyCharId);
            _entered = true;
            _nameBoard.gameObject.SetActive(false);
            _status.SetText(string.Empty);
        }

        public override void OnExit()
        {
            _client.Leave();
            foreach (var v in _views.Values) if (v.Root != null) Object.Destroy(v.Root.gameObject);
            _views.Clear();
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        void Leave() { Close(); }
        void GoRooms() { Screens?.Replace(ScreenId.CoopLobby, ScreenArgs.From(ScreenId.Plaza)); }

        void SendChat()
        {
            if (!_entered) return;
            _client.Say(_chat.text);
            _chat.text = string.Empty;
            _chat.DeactivateInputField();
        }

        public override bool OnBack()
        {
            if (_chat.isFocused) { _chat.DeactivateInputField(); return true; }
            Close();
            return true;
        }

        public override void Tick(float dt)
        {
            bool typing = (_chat != null && _chat.isFocused) || (_nameInput != null && _nameInput.isFocused) || (_trade != null && _trade.Typing);
            if (Screens != null) Screens.CaptureKeys = typing;
            var kb = Keyboard.current;
            if (_nameBoard.gameObject.activeSelf)
            {
                if (kb != null && kb.enterKey.wasPressedThisFrame) Enter();
                return;
            }
            if (!_entered) return;

            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame)
                {
                    if (_chat.isFocused) SendChat();
                    else _chat.ActivateInputField();
                }
                int dir = -1;
                if (!typing)
                {
                    bool up = kb.upArrowKey.isPressed || kb.wKey.isPressed, down = kb.downArrowKey.isPressed || kb.sKey.isPressed;
                    bool left = kb.leftArrowKey.isPressed || kb.aKey.isPressed, right = kb.rightArrowKey.isPressed || kb.dKey.isPressed;
                    dir = DirOf(up && !down, down && !up, left && !right, right && !left);
                    if (kb.digit1Key.wasPressedThisFrame) _client.Emote(1);
                    if (kb.digit2Key.wasPressedThisFrame) _client.Emote(2);
                    if (kb.digit3Key.wasPressedThisFrame) _client.Emote(3);
                    if (kb.digit4Key.wasPressedThisFrame) _client.Emote(4);
                }
                bool trading = _trade.Visible;
                if (trading) dir = -1; // 거래 판이 열려 있으면 서 있는다
                if (dir != _lastDir) { _client.Move(dir); _lastDir = dir; }

                // 거래: T = 가장 가까운 사람에게 청하기, 청이 오면 Y/N.
                if (!typing && !trading)
                {
                    if (kb.tKey.wasPressedThisFrame) { int near = NearestOther(); if (near > 0) _client.RequestTrade(near); }
                    if (_client.InviteFrom > 0 && kb.yKey.wasPressedThisFrame) _client.AnswerTrade(true);
                    if (_client.InviteFrom > 0 && kb.nKey.wasPressedThisFrame) _client.AnswerTrade(false);
                }

                // 문 위에 서 있으면 안내, Space면 그 방 화면으로.
                int door = -1;
                if (_client.People.TryGetValue(_client.MyId, out var me)) door = DoorAt(me.Pos);
                _doorPrompt.SetText(door >= 0 ? F(OnlineKeys.DoorPrompt, "door", T(Doors[door].LabelKey)) : string.Empty);
                if (door >= 0 && !typing && kb.spaceKey.wasPressedThisFrame)
                {
                    Screens?.Replace(ScreenId.CoopLobby, ScreenArgs.From(ScreenId.Plaza).With(ScreenArgKeys.RoomMode, Doors[door].Mode));
                    return;
                }
            }

            _client.Update(dt);
            if (!_client.Connected)
            {
                _status.SetText(T(_client.Rejected ? OnlineKeys.ErrorFull : OnlineKeys.ErrorConnect));
                _entered = false;
                _nameBoard.gameObject.SetActive(true);
                return;
            }
            Render(dt);
            bool open = _trade.Render();
            string line = _client.InviteFrom > 0 ? T(OnlineKeys.TradeInvite).Replace("{name}", _client.InviteName ?? "?")
                : (!open && _trade.LastResult != null ? _trade.LastResult : (open ? string.Empty : T(OnlineKeys.TradeHint)));
            _tradeLine.SetText(line);
        }

        /// <summary>내 곁(거래 거리 안)의 가장 가까운 다른 사람 id(없으면 −1).</summary>
        int NearestOther()
        {
            if (!_client.People.TryGetValue(_client.MyId, out var me)) return -1;
            int best = -1;
            float bd = PlazaHost.TradeRange;
            foreach (var kv in _client.People)
            {
                if (kv.Key == _client.MyId) continue;
                float d = (kv.Value.Pos - me.Pos).magnitude;
                if (d <= bd) { bd = d; best = kv.Key; }
            }
            return best;
        }

        public static int DirOf(bool up, bool down, bool left, bool right)
        {
            if (up && right) return 1;
            if (down && right) return 3;
            if (down && left) return 5;
            if (up && left) return 7;
            if (up) return 0;
            if (right) return 2;
            if (down) return 4;
            if (left) return 6;
            return -1;
        }

        void Render(float dt)
        {
            _count.SetText(F(OnlineKeys.PlazaCount, "n", _client.People.Count, "max", PlazaHost.MaxAvatars));
            _gone.Clear();
            _gone.AddRange(_views.Keys);
            // 아래쪽 사람이 앞에 그려지게 y 순으로 둔다.
            var order = new List<PlazaClient.Shown>(_client.People.Values);
            order.Sort((a, b) => a.Pos.y.CompareTo(b.Pos.y));
            foreach (var s in order)
            {
                var a = s.Now;
                _gone.Remove(a.Id);
                if (!_views.TryGetValue(a.Id, out var v)) { v = MakeView(a); _views[a.Id] = v; }
                v.Root.SetAsLastSibling();
                Vector2 pos = s.Pos;
                v.Root.anchoredPosition = new Vector2(pos.x * Cell - AvatarPx * 0.5f, -(pos.y * Cell - AvatarPx));
                bool moving = a.MoveDir >= 0;
                v.AnimT += dt;
                Facing3 f = a.FaceDir == 0 || a.FaceDir == 1 || a.FaceDir == 7 ? Facing3.Up : (a.FaceDir == 4 || a.FaceDir == 3 || a.FaceDir == 5 ? Facing3.Down : Facing3.Right);
                bool flip = a.FaceDir >= 5 && a.FaceDir <= 7 && f == Facing3.Right || a.FaceDir == 6;
                SpriteAnim clip = null;
                try { clip = Ctx != null && Ctx.Art != null ? Ctx.Art.CharacterClip(a.CharId, moving ? "walk" : "idle", f) : null; } catch (System.Exception) { }
                var sp = clip != null ? clip.At(v.AnimT) : null;
                if (sp != null) { v.Body.sprite = sp; v.Body.color = Color.white; }
                v.Body.rectTransform.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
                v.Name.SetText(a.Id == _client.MyId ? "★ " + a.Name : a.Name);
                bool hasBubble = !string.IsNullOrEmpty(a.Bubble);
                v.BubbleRoot.gameObject.SetActive(hasBubble);
                if (hasBubble) v.Bubble.SetText(a.Bubble);
                v.Emote.SetText(a.Emote > 0 && a.Emote < EmoteGlyphs.Length ? EmoteGlyphs[a.Emote] : string.Empty);
            }
            foreach (var id in _gone)
            {
                if (_views.TryGetValue(id, out var v) && v.Root != null) Object.Destroy(v.Root.gameObject);
                _views.Remove(id);
            }
        }

        AvatarView MakeView(PlazaAvatar a)
        {
            var root = UiKit.Node(_field, "Avatar." + a.Id, 0f, 0f, AvatarPx, AvatarPx + 24f);
            var v = new AvatarView { Root = root };
            UiKit.Rect(root, AvatarPx * 0.25f, AvatarPx - 10f, AvatarPx * 0.5f, 12f, UiTheme.Shadow, false, "Shadow");
            v.Body = UiKit.Image(root, null, 0f, 0f, AvatarPx, AvatarPx, UiTheme.WithAlpha(UiTheme.Chalk, 0.0f), true);
            v.Body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            v.Name = UiKit.Label(Ctx, root, a.Name, UiFontId.F12, -40f, AvatarPx, AvatarPx + 80f, 20f,
                a.Id == _client.MyId ? UiTheme.SelectYellow : UiTheme.Chalk, TextAnchor.MiddleCenter, outline: true);
            var bubble = UiKit.Panel(Ctx, root, UiPanelKind.Board, -80f, -52f, AvatarPx + 160f, 46f).rectTransform;
            v.BubbleRoot = bubble;
            v.Bubble = UiKit.Label(Ctx, bubble, string.Empty, UiFontId.F12, 8f, 4f, AvatarPx + 144f, 38f, UiTheme.PaperText, TextAnchor.MiddleCenter, wrap: true);
            v.Emote = UiKit.Label(Ctx, root, string.Empty, UiFontId.F24, AvatarPx - 20f, -8f, 48f, 32f, UiTheme.SelectYellow, TextAnchor.MiddleCenter, outline: true);
            return v;
        }
    }
}
