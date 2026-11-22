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
        /// <summary>감정 표현 번호 1~8 → I-F10 그림.</summary>
        static readonly string[] EmoteIcons = { "greet", "clap", "bow", "dance", "sit", "taunt", "raise_wand", "despair" };

        /// <summary>광장의 문(좀비고식): 그 칸 안에 서서 Space를 누르면 그 종류의 방 화면이 열린다. 칸 좌표(광장 40×22칸).</summary>
        public struct Door
        {
            public Rect Area;
            public RoomMode Mode;
            public string LabelKey;
        }

        /// <summary>등불 자리(광장 땅 좌표 px, 기둥 밑동). 모닥불 둘레 길의 네 모서리.</summary>
        static readonly Vector2[] LampSpots = { new Vector2(560f, 330f), new Vector2(1280f, 330f), new Vector2(560f, 760f), new Vector2(1280f, 760f) };

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
        int _whispersSeen;
        string _lastWhisper;
        float _whisperTime;

        sealed class AvatarView
        {
            public RectTransform Root;
            public UImage Body;
            public UiLabel Name, Bubble;
            public RectTransform BubbleRoot, Plate, Tail;
            public UnityEngine.UI.Image EmoteIcon;
            public string ShownName, ShownBubble;
            public int ShownEmote;
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
            bg = OnlineUi.Online("plaza_ground");
            bool customGround = bg != null;
            if (bg == null) { try { bg = Ctx != null && Ctx.Art != null ? Ctx.Art.Background("S-0501") : null; } catch (System.Exception) { } }
            _field = UiKit.Node(content, "PlazaField", OriginX, OriginY, PlazaHost.Width * Cell, PlazaHost.Height * Cell);
            if (bg != null) UiKit.Image(_field, bg, 0f, 0f, PlazaHost.Width * Cell, PlazaHost.Height * Cell, customGround ? Color.white : new Color(0.85f, 0.85f, 0.9f, 1f), false);
            else UiKit.Rect(_field, 0f, 0f, PlazaHost.Width * Cell, PlazaHost.Height * Cell, UiTheme.Wood, false, "Ground");

            // 문: 협동 = 한뎃벌로 나가는 문(facility.field_gate), 결투 = 결투 게시판(facility.duel_board). 위에 시설 팻말(S-2007).
            foreach (var d in Doors)
            {
                string prop = d.Mode == RoomMode.Duel ? "facility.duel_board" : "facility.field_gate";
                // 받아 온 땅에는 두 문이 이미 그려져 있다 — 그 위에 문 그림을 또 얹지 않는다.
                Sprite ps = customGround ? null : OnlineUi.Online(d.Mode == RoomMode.Duel ? "plaza_gate_duel" : "plaza_gate_field");
                if (ps == null && !customGround) try { var anim = Ctx != null && Ctx.Art != null ? Ctx.Art.World(prop) : null; ps = anim != null && anim.Frames != null && anim.Frames.Length > 0 ? anim.Frames[0] : null; }
                catch (System.Exception) { ps = null; }
                float dw = d.Area.width * Cell, dh = d.Area.height * Cell;
                var area = UiKit.Node(_field, "Door", d.Area.x * Cell, d.Area.y * Cell, dw, dh);
                // 들어서는 자리: 흐린 분필 원.
                if (!customGround) UiKit.Rect(area, 0f, dh - 24f, dw, 24f, UiTheme.WithAlpha(UiTheme.Chalk, 0.12f), false, "Mat");
                if (ps != null)
                {
                    float pw = ps.rect.width * 2f, ph = ps.rect.height * 2f;
                    if (pw > dw + 64f) { float k = (dw + 64f) / pw; pw *= k; ph *= k; }
                    UiKit.Image(area, ps, (dw - pw) * 0.5f, dh - ph - 8f, pw, ph, Color.white, true);
                }
                else if (!customGround) UiKit.Rect(area, 0f, 0f, dw, dh, UiTheme.WithAlpha(UiTheme.WoodLight, 0.5f), false, "DoorFallback");
                float sw = Mathf.Max(dw + 40f, UiTextFit.WidthPx(T(d.LabelKey), 16) + 40f);
                var sign = UiKit.NineSlice(Ctx, area, "S-2007", "facility_sign", null, (dw - sw) * 0.5f, -48f, sw, 40f, UiTheme.Paper).rectTransform;
                UiKit.Label(Ctx, sign, T(d.LabelKey), UiFontId.F16, 0f, 0f, sw, 40f, UiTheme.Chalk, TextAnchor.MiddleCenter, outline: true);
            }
            // 받아 온 땅(plaza_ground)에는 노점·게시판·빨랫줄이 그려져 있다. 모닥불 둘레 길에 등불(plaza_lamp)만 세운다.
            var lamp = customGround ? OnlineUi.Online("plaza_lamp") : null;
            if (lamp != null)
                foreach (var lp in LampSpots)
                    UiKit.Image(_field, lamp, lp.x - 36f, lp.y - 168f, 72f, 168f, Color.white, true).name = "plaza_lamp";
            // 위 띠(얇게): 광장 인원과 조작 안내.
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, 34f, UiTheme.WithAlpha(UiTheme.Ink, 0.7f), false, "TopStrip");
            float lh = UiTheme.LineHeight(UiFontId.F16);
            _doorPrompt = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, (UiScale.RefWidth - 900f) * 0.5f, UiScale.RefHeight - 140f, 900f, 40f,
                UiTheme.SelectYellow, TextAnchor.MiddleCenter, outline: true);
            var cnt = OnlineUi.IconText(Ctx, content, "I-F8", "talk", string.Empty, 48f, 5f, 900f, 24f, UiFontId.F16, UiTheme.Chalk, 16f);
            _count = cnt;
            _status = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, 48f, 40f, 900f, lh, UiTheme.Amber, TextAnchor.MiddleLeft, outline: true);

            float by = UiScale.RefHeight - 72f;
            UiKit.Rect(content, 0f, by - 14f, UiScale.RefWidth, UiScale.RefHeight - by + 14f, UiTheme.WithAlpha(UiTheme.Ink, 0.72f), false, "BottomStrip");
            OnlineUi.OnlineImg(content, "frame_chatbox", 16f, by - 24f, 1136f, 96f);
            _chat = BuildInput(content, 48f, by, 900f, 52f, OnlineKeys.ChatHint, 60);
            UiKit.Button(Ctx, content, T(OnlineKeys.ChatSend), 960f, by, 160f, 52f, SendChat, UiButtonStyle.Medium);
            UiKit.Button(Ctx, content, T(OnlineKeys.PlazaRooms), UiScale.RefWidth - 48f - 220f, by, 220f, 52f, GoRooms, UiButtonStyle.Primary);
            UiKit.Button(Ctx, content, T(OnlineKeys.Leave), UiScale.RefWidth - 48f - 220f - 12f - 160f, by, 160f, 52f, Leave, UiButtonStyle.Medium);

            _tradeLine = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, (UiScale.RefWidth - 1200f) * 0.5f, 110f, 1200f, 40f,
                UiTheme.SelectYellow, TextAnchor.MiddleCenter, outline: true);
            _trade = new PlazaTradePanel(Ctx, content, null, _client, T);

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
            _lastOrder.Clear();
            _shownCount = -1;
            _shownDoor = -2;
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        void Leave() { Close(); }
        void GoRooms() { Screens?.Replace(ScreenId.CoopLobby, ScreenArgs.From(ScreenId.Plaza)); }

        void SendChat()
        {
            if (!_entered) return;
            if (!_client.TryWhisperCommand(_chat.text)) _client.Say(_chat.text);
            _chat.text = string.Empty;
            _chat.DeactivateInputField();
        }

        /// <summary>광장 안에서는 키보드가 걷기·감정·거래 키만 맡는다. 아래 단추·거래 판은 마우스로만 누른다(방향키·Space가 단추 커서를 옮기거나 누르지 않게).</summary>
        public override bool OnMenuKey(MenuKey key)
        {
            if (_entered) return true;
            return base.OnMenuKey(key);
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
                if (door != _shownDoor)
                {
                    // 문이 바뀔 때만 글을 만든다(프레임마다 문구 조립·할당을 하지 않게)
                    _shownDoor = door;
                    _doorPrompt.SetText(door >= 0 ? F(OnlineKeys.DoorPrompt, "door", T(Doors[door].LabelKey)) : string.Empty);
                }
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
            if (_client.Whispers.Count > _whispersSeen)
            {
                _whispersSeen = _client.Whispers.Count;
                var w = _client.Whispers[_client.Whispers.Count - 1];
                _lastWhisper = w.Key == null ? F(OnlineKeys.WhisperNobody, "name", w.Value) : F(OnlineKeys.WhisperFrom, "name", w.Key, "text", w.Value);
                _whisperTime = 8f;
            }
            if (_whisperTime > 0f) _whisperTime -= dt;
            string line = _whisperTime > 0f ? _lastWhisper : _client.InviteFrom > 0 ? T(OnlineKeys.TradeInvite).Replace("{name}", _client.InviteName ?? "?")
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

        // 프레임마다 새 목록·람다를 만들지 않게 다시 쓴다
        readonly List<PlazaClient.Shown> _order = new List<PlazaClient.Shown>();
        readonly List<int> _lastOrder = new List<int>();
        static readonly System.Comparison<PlazaClient.Shown> ByY = (a, b) => a.Pos.y.CompareTo(b.Pos.y);
        int _shownCount = -1;
        int _shownDoor = -2;

        void Render(float dt)
        {
            int people = _client.People.Count;
            if (people != _shownCount)
            {
                _shownCount = people;
                _count.SetText(F(OnlineKeys.PlazaCount, "n", people, "max", PlazaHost.MaxAvatars));
            }
            _gone.Clear();
            _gone.AddRange(_views.Keys);
            // 아래쪽 사람이 앞에 그려지게 y 순으로 둔다.
            _order.Clear();
            _order.AddRange(_client.People.Values);
            _order.Sort(ByY);
            // 앞뒤 순서가 지난 프레임과 같으면 형제 순서를 다시 매기지 않는다(매 프레임 SetAsLastSibling은 캔버스 전체를 다시 묶게 한다).
            bool reorder = _order.Count != _lastOrder.Count;
            for (int i = 0; i < _order.Count && !reorder; i++)
                if (_order[i].Now.Id != _lastOrder[i] || !_views.ContainsKey(_lastOrder[i])) reorder = true;
            if (reorder)
            {
                _lastOrder.Clear();
                for (int i = 0; i < _order.Count; i++) _lastOrder.Add(_order[i].Now.Id);
            }
            foreach (var s in _order)
            {
                var a = s.Now;
                _gone.Remove(a.Id);
                if (!_views.TryGetValue(a.Id, out var v)) { v = MakeView(a); _views[a.Id] = v; }
                if (reorder) v.Root.SetAsLastSibling();
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
                var scale = new Vector3(flip ? -1f : 1f, 1f, 1f);
                if (v.Body.rectTransform.localScale != scale) v.Body.rectTransform.localScale = scale;
                if (v.ShownName != a.Name)
                {
                    v.ShownName = a.Name;
                    v.Name.SetText(a.Name);
                    float nw = Mathf.Max(48f, UiTextFit.WidthPx(a.Name, 12) + 20f);
                    UiKit.Place(v.Plate, (AvatarPx - nw) * 0.5f, AvatarPx + 2f, nw, 22f);
                    UiKit.Place(v.Name.Rect, 0f, 0f, nw, 22f);
                }
                bool hasBubble = !string.IsNullOrEmpty(a.Bubble);
                if (v.BubbleRoot.gameObject.activeSelf != hasBubble) v.BubbleRoot.gameObject.SetActive(hasBubble);
                if (hasBubble && v.ShownBubble != a.Bubble)
                {
                    v.ShownBubble = a.Bubble;
                    v.Bubble.SetText(a.Bubble);
                    // 한 줄 20자, 최대 폭 336(ui-assets 4.20). 짧은 말은 풍선도 좁게.
                    float bw = Mathf.Clamp(UiTextFit.WidthPx(a.Bubble, 16) + 28f, 72f, 336f);
                    int lines = Mathf.Clamp(Mathf.CeilToInt((UiTextFit.WidthPx(a.Bubble, 16) + 1f) / 308f), 1, 4);
                    float bh = 20f + lines * 22f;
                    UiKit.Place(v.BubbleRoot, (AvatarPx - bw) * 0.5f, -bh - 14f, bw, bh);
                    UiKit.Place(v.Bubble.Rect, 14f, 8f, bw - 28f, bh - 16f);
                    UiKit.Place(v.Tail, bw * 0.5f - 12f, bh - 2f, 24f, 16f);
                }
                int e = a.Emote;
                bool emote = e > 0 && e <= EmoteIcons.Length;
                if (v.EmoteIcon.gameObject.activeSelf != emote) v.EmoteIcon.gameObject.SetActive(emote);
                if (emote && v.ShownEmote != e)
                {
                    v.ShownEmote = e;
                    var es = OnlineUi.Sprite(Ctx, "I-F10", EmoteIcons[e - 1]);
                    v.EmoteIcon.sprite = es;
                }
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
            // 뒤집기(왼쪽 보기)를 가운데 축으로 하려고 축을 가운데로 옮기고, 자리는 그대로 둔다.
            v.Body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            v.Body.rectTransform.anchoredPosition = new Vector2(AvatarPx * 0.5f, -AvatarPx * 0.5f);
            // 닉네임 판(S-2001): 나 = 분필 흰 테, 남 = 먹색. 폭은 글자에 맞춘다(Render에서).
            bool me = a.Id == _client.MyId;
            v.Plate = UiKit.NineSlice(Ctx, root, "S-2001", me ? "nameplate_self" : "nameplate_other", null, 0f, AvatarPx + 2f, AvatarPx, 22f,
                UiTheme.WithAlpha(UiTheme.Ink, 0.7f)).rectTransform;
            v.Name = UiKit.Label(Ctx, v.Plate, a.Name, UiFontId.F12, 0f, 0f, AvatarPx, 22f, me ? UiTheme.SelectYellow : UiTheme.Chalk, TextAnchor.MiddleCenter);
            // 말풍선(S-2005 주변 + 꼬리).
            var bubble = UiKit.NineSlice(Ctx, root, "S-2005", "bubble_nearby", null, -80f, -60f, AvatarPx + 160f, 46f, UiTheme.Paper).rectTransform;
            v.BubbleRoot = bubble;
            v.Tail = OnlineUi.Img(Ctx, bubble, "S-2005", "bubble_tail_nearby", 0f, 0f, 24f, 16f).rectTransform;
            v.Bubble = UiKit.Label(Ctx, bubble, string.Empty, UiFontId.F16, 14f, 8f, AvatarPx + 132f, 30f, UiTheme.PaperText, TextAnchor.MiddleCenter, wrap: true);
            v.EmoteIcon = UiKit.Image(root, null, AvatarPx - 24f, -24f, 48f, 48f, Color.white, true);
            v.EmoteIcon.gameObject.SetActive(false);
            return v;
        }
    }
}
