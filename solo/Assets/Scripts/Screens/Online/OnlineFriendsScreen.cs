using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;
using UInputField = UnityEngine.UI.InputField;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 친구(ScreenId.OnlineFriends, 11-online.md 6.12절, docs/ui-assets.md 4.22절). 야영지 장면을 어둡게 두고 오른쪽에 서랍 판(640):
    /// 이름 칸 + [친구 신청], "받은 신청 n"(줄마다 [받기] [거절]), "친구 n"(상태 아이콘 I-F4 — 광장·방·접속 안 함, 이름, 레이팅, 있는 곳, [끊기]).
    /// 접속한 친구가 위로 온다(서버가 정렬). 왼쪽엔 까마귀 우편 시설 그림과 귓속말 안내.
    /// </summary>
    public sealed class OnlineFriendsScreen : ScreenBase
    {
        const float DrawerX = 1232f, DrawerY = 104f, DrawerW = 640f, DrawerH = 928f, P = 28f, RowH = 64f;
        const int RequestRows = 4, FriendRows = 8;

        OnlineService _svc;
        UiLabel _status, _reqHead, _frHead, _noReq, _noFr;
        UImage _noFrArt;
        UInputField _name;
        RectTransform _reqRoot, _frRoot;
        sealed class Row
        {
            public RectTransform Root;
            public UImage Status;
            public UiLabel Name, Sub, Where;
            public UiButton A, B;
        }
        readonly List<Row> _reqRows = new List<Row>(), _frRows = new List<Row>();
        List<FriendInfo> _friends = new List<FriendInfo>(), _requests = new List<FriendInfo>();

        public override ScreenId Id => ScreenId.OnlineFriends;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            OnlineUi.Scene(Ctx, content, "S-0501", "camp_background", 0.6f);
            OnlineUi.HeaderBand(Ctx, content, "I-F1", "friends", T(OnlineKeys.FriendsTitle), out var band);
            UiKit.Button(Ctx, band, T(OnlineKeys.Leave), UiScale.RefWidth - OnlineUi.Edge - 176f, 16f, 176f, 48f, () => Close(), UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "back"));

            // 왼쪽: 까마귀 우편 시설.
            Sprite crow = null;
            try { var a = Ctx != null && Ctx.Art != null ? Ctx.Art.World("facility.crow_mail") : null; crow = a != null && a.Frames != null && a.Frames.Length > 0 ? a.Frames[0] : null; }
            catch (System.Exception) { crow = null; }
            if (crow != null)
            {
                float w = crow.rect.width * 3f, h = crow.rect.height * 3f;
                UiKit.Image(content, crow, 160f + (640f - w) * 0.5f, 560f - h, w, h, Color.white, true);
            }
            var t = UiKit.Label(Ctx, content, T(OnlineKeys.FrSideTitle), UiFontId.F32, 160f, 600f, 900f, 48f, UiTheme.Chalk);
            UiKit.DropShadow(t);
            var n = UiKit.Label(Ctx, content, T(OnlineKeys.FrSideNote), UiFontId.F16, 160f, 656f, 900f, 48f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            UiKit.DropShadow(n);

            // 오른쪽 서랍.
            var drawer = OnlineUi.OnlineImg(content, "frame_drawer", DrawerX, DrawerY, DrawerW, DrawerH);
            var d = (drawer ?? UiKit.Panel(Ctx, content, UiPanelKind.Board, DrawerX, DrawerY, DrawerW, DrawerH)).rectTransform;
            if (drawer == null) UiKit.Bevel(d);
            float inner = DrawerW - 2f * P, y = 24f;
            _name = OnlineUi.Field(Ctx, Focus, d, null, T(OnlineKeys.FriendsNameHint), P, y, inner - 196f, AccountStore.NameMax);
            UiKit.Button(Ctx, d, T(OnlineKeys.FriendsAdd), P + inner - 184f, y, 184f, 48f, Add, UiButtonStyle.Primary, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F3", "friend_add"));
            y += 72f;
            // 구역 머리는 어두운 제목 띠(K-04) 위 분필 글자: 서랍 그림 위쪽의 밝은 편지 칸 위에 노랑 글자를 바로 얹으면 1.2:1로 묻혔다(플레이 시험).
            _reqHead = UiKit.SectionHeader(Ctx, d, string.Empty, P, y, inner, 32f);
            y += 36f;
            _reqRoot = UiKit.Node(d, "Requests", P, y, inner, RequestRows * (RowH + 6f));
            for (int i = 0; i < RequestRows; i++) _reqRows.Add(BuildRow(_reqRoot, i, true));
            // 빈 줄 안내도 줄과 같은 안쪽 판(K-02) 위에 둔다(그림 위 회청 3.8:1 → 판 위 7.8:1).
            var noReqPlate = UiKit.Panel(Ctx, _reqRoot, UiPanelKind.Recess, 0f, 0f, inner, 40f);
            noReqPlate.raycastTarget = false;
            _noReq = UiKit.Label(Ctx, noReqPlate.rectTransform, T(OnlineKeys.FrNoRequests), UiFontId.F16, 16f, 0f, inner - 32f, 40f, UiKit.TextColor(UiPanelKind.Recess, UiTextRole.Muted));
            y += RequestRows * (RowH + 6f) + 12f;
            UiKit.Divider(Ctx, d, P, y, inner);
            y += 12f;
            _frHead = UiKit.SectionHeader(Ctx, d, string.Empty, P, y, inner, 32f);
            y += 36f;
            _frRoot = UiKit.Node(d, "Friends", P, y, inner, FriendRows * (RowH + 6f));
            for (int i = 0; i < FriendRows; i++) _frRows.Add(BuildRow(_frRoot, i, false));
            _noFrArt = OnlineUi.OnlineImg(_frRoot, "empty_friends", (inner - 192f) * 0.5f, 24f, 192f, 192f);
            _noFr = UiKit.Label(Ctx, _frRoot, T(OnlineKeys.FrNoFriends), UiFontId.F24, 8f, _noFrArt != null ? 232f : 24f, inner - 16f, 72f, UiTheme.SlateBlue, TextAnchor.UpperCenter, wrap: true);
            _status = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 160f, 960f, 1000f, 40f, UiTheme.Amber);
            UiKit.DropShadow(_status);
        }

        Row BuildRow(RectTransform parent, int i, bool request)
        {
            float w = DrawerW - 2f * P;
            var r = new Row { Root = UiKit.Node(parent, "Row" + i, 0f, i * (RowH + 6f), w, RowH) };
            UiKit.Panel(Ctx, r.Root, UiPanelKind.Recess, 0f, 0f, w, RowH);
            r.Status = UiKit.Image(r.Root, null, 16f, (RowH - 32f) * 0.5f, 32f, 32f, Color.white, true);
            r.Name = UiKit.Label(Ctx, r.Root, string.Empty, UiFontId.F24, 60f, 4f, 260f, 32f, UiTheme.Chalk);
            r.Sub = UiKit.Label(Ctx, r.Root, string.Empty, UiFontId.F16, 60f, 36f, 150f, 24f, UiTheme.SlateBlue);
            r.Where = UiKit.Label(Ctx, r.Root, string.Empty, UiFontId.F16, 212f, 36f, 200f, 24f, UiTheme.SlateBlue);
            int k = i;
            if (request)
            {
                r.A = UiKit.Button(Ctx, r.Root, T(OnlineKeys.FriendsAccept), w - 236f, 12f, 112f, 40f, () => Answer(k, true), UiButtonStyle.Primary, Focus, null, UiFontId.F16);
                r.B = UiKit.Button(Ctx, r.Root, T(OnlineKeys.FriendsDecline), w - 116f, 12f, 104f, 40f, () => Answer(k, false), UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            }
            else
                r.A = UiKit.Button(Ctx, r.Root, T(OnlineKeys.FriendsRemove), w - 132f, 14f, 120f, 36f, () => Remove(k), UiButtonStyle.Small, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "trash"));
            return r;
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _status.SetText(string.Empty);
            _svc?.Dispose();
            _svc = OnlineService.Open(Ctx != null ? Ctx.Content : null);
            if (_svc == null) _status.SetText(T(OnlineKeys.ErrorServer));
            Reload();
            Focus.SetCursor(0, false);
        }

        public override void OnExit()
        {
            _svc?.Dispose();
            _svc = null;
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        public override void Tick(float dt) { if (Screens != null) Screens.CaptureKeys = _name.isFocused; }

        static string StatusIcon(string where) => string.IsNullOrEmpty(where) ? "offline" : where == "plaza" ? "online" : "duel";

        void Reload()
        {
            _friends = new List<FriendInfo>();
            _requests = new List<FriendInfo>();
            if (_svc != null && _svc.Friends(out var f, out var r)) { _friends = f; _requests = r; }
            _reqHead.SetText(F(OnlineKeys.FrRequestsHead, "n", _requests.Count));
            _frHead.SetText(F(OnlineKeys.FrFriendsHead, "n", _friends.Count));
            _noReq.transform.parent.gameObject.SetActive(_requests.Count == 0);
            _noFr.gameObject.SetActive(_friends.Count == 0);
            if (_noFrArt != null) _noFrArt.gameObject.SetActive(_friends.Count == 0);
            for (int i = 0; i < _reqRows.Count; i++)
            {
                var row = _reqRows[i];
                bool on = i < _requests.Count;
                row.Root.gameObject.SetActive(on);
                if (!on) continue;
                row.Status.sprite = OnlineUi.Sprite(Ctx, "I-F3", "person");
                row.Name.SetText(_requests[i].Name);
                row.Sub.SetText(F(OnlineKeys.FrRating, "rating", _requests[i].Rating));
                row.Where.SetText(string.Empty);
            }
            for (int i = 0; i < _frRows.Count; i++)
            {
                var row = _frRows[i];
                bool on = i < _friends.Count;
                row.Root.gameObject.SetActive(on);
                if (!on) continue;
                var x = _friends[i];
                bool online = !string.IsNullOrEmpty(x.Where);
                row.Status.sprite = OnlineUi.Sprite(Ctx, "I-F4", StatusIcon(x.Where));
                row.Name.SetText(x.Name);
                row.Name.Text.color = online ? UiTheme.Chalk : UiTheme.SlateBlue;
                row.Sub.SetText(F(OnlineKeys.FrRating, "rating", x.Rating));
                row.Where.SetText(T(OnlineKeys.FriendsWhere + (online ? x.Where : "off")));
                row.Where.Text.color = online ? UiTheme.Good : UiTheme.SlateBlue;
            }
        }

        void Show(AccountStore.FriendResult? r)
        {
            _status.Text.color = r == AccountStore.FriendResult.Ok || r == AccountStore.FriendResult.Requested ? UiTheme.Good : UiTheme.Amber;
            _status.SetText(r == null ? T(OnlineKeys.ErrorServer) : T(OnlineKeys.FriendsResult + r.Value));
        }

        void Add()
        {
            if (_svc == null) return;
            Show(_svc.RequestFriend(_name.text));
            _name.text = string.Empty;
            Reload();
        }

        void Answer(int k, bool yes)
        {
            if (_svc == null || k >= _requests.Count) return;
            Show(_svc.AnswerFriend(_requests[k].Id, yes));
            Reload();
        }

        void Remove(int k)
        {
            if (_svc == null || k >= _friends.Count) return;
            Show(_svc.RemoveFriend(_friends[k].Id));
            Reload();
        }
    }
}
