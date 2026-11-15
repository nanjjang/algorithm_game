using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.UI;
using UInputField = UnityEngine.UI.InputField;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 친구(ScreenId.OnlineFriends, 11-online.md 6.12절). 이름으로 친구 신청, 나에게 온 신청 [받기]/[거절], 친구 목록(어디 있는지:
    /// 광장·방·접속 안 함 — 누르면 끊기). 귓속말은 광장 채팅에서 "@이름 내용".
    /// </summary>
    public sealed class OnlineFriendsScreen : ScreenBase
    {
        const float BoardW = 1100f, BoardH = 860f, Pad = 28f, RowH = 40f, Gap = 6f;
        const int FriendRows = 12, RequestRows = 5;

        OnlineService _svc;
        UiLabel _status;
        UInputField _name;
        readonly List<UiLabel> _friendLines = new List<UiLabel>();
        readonly List<UiButton> _removeButtons = new List<UiButton>();
        readonly List<UiLabel> _reqLines = new List<UiLabel>();
        readonly List<UiButton> _accept = new List<UiButton>(), _decline = new List<UiButton>();
        List<FriendInfo> _friends = new List<FriendInfo>(), _requests = new List<FriendInfo>();

        public override ScreenId Id => ScreenId.OnlineFriends;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, BoardW, BoardH).rectTransform;
            UiKit.Center(board, BoardW, BoardH);
            float inner = BoardW - 2f * Pad, y = Pad;
            UiKit.Label(Ctx, board, T(OnlineKeys.FriendsTitle), UiFontId.F32, Pad, y, inner, 44f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            y += 52f;
            var bg = UiKit.NineSlice(Ctx, board, "K-33", "input", null, Pad, y, inner - 220f, 48f, UiTheme.Chalk);
            bg.raycastTarget = true;
            _name = bg.gameObject.AddComponent<UInputField>();
            var t = UiKit.Label(Ctx, bg.rectTransform, string.Empty, UiFontId.F16, 8f, 0f, inner - 236f, 48f, UiTheme.PaperText);
            _name.textComponent = t.GetComponent<UText>();
            _name.characterLimit = AccountStore.NameMax;
            var ph = UiKit.Label(Ctx, bg.rectTransform, T(OnlineKeys.FriendsNameHint), UiFontId.F16, 8f, 0f, inner - 236f, 48f, UiTheme.SlateBlue);
            _name.placeholder = ph.GetComponent<UText>();
            UiKit.Button(Ctx, board, T(OnlineKeys.FriendsAdd), BoardW - Pad - 200f, y, 200f, 48f, Add, UiButtonStyle.Primary, Focus);
            y += 60f;
            UiKit.Label(Ctx, board, T(OnlineKeys.FriendsRequests), UiFontId.F16, Pad, y, inner, 28f, UiTheme.SelectYellow);
            y += 30f;
            for (int i = 0; i < RequestRows; i++)
            {
                int k = i;
                _reqLines.Add(UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, inner - 300f, RowH, UiTheme.Chalk));
                _accept.Add(UiKit.Button(Ctx, board, T(OnlineKeys.FriendsAccept), BoardW - Pad - 290f, y, 140f, RowH, () => Answer(k, true), UiButtonStyle.Primary, Focus, null, UiFontId.F16));
                _decline.Add(UiKit.Button(Ctx, board, T(OnlineKeys.FriendsDecline), BoardW - Pad - 140f, y, 140f, RowH, () => Answer(k, false), UiButtonStyle.Medium, Focus, null, UiFontId.F16));
                y += RowH + Gap;
            }
            y += 8f;
            UiKit.Label(Ctx, board, T(OnlineKeys.FriendsList), UiFontId.F16, Pad, y, inner, 28f, UiTheme.SelectYellow);
            y += 30f;
            for (int i = 0; i < FriendRows; i++)
            {
                int k = i;
                _friendLines.Add(UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, inner - 160f, RowH - 4f, UiTheme.Chalk));
                _removeButtons.Add(UiKit.Button(Ctx, board, T(OnlineKeys.FriendsRemove), BoardW - Pad - 140f, y, 140f, RowH - 4f, () => Remove(k), UiButtonStyle.Small, Focus, null, UiFontId.F12));
                y += RowH;
            }
            _status = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, BoardH - Pad - 100f, inner - 220f, 40f, UiTheme.Amber, TextAnchor.MiddleLeft);
            UiKit.Button(Ctx, board, T(OnlineKeys.Leave), BoardW - Pad - 200f, BoardH - Pad - 56f, 200f, 56f, () => Close(), UiButtonStyle.Medium, Focus);
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

        void Reload()
        {
            _friends = new List<FriendInfo>();
            _requests = new List<FriendInfo>();
            if (_svc != null && _svc.Friends(out var f, out var r)) { _friends = f; _requests = r; }
            for (int i = 0; i < _reqLines.Count; i++)
            {
                bool on = i < _requests.Count;
                _reqLines[i].gameObject.SetActive(on);
                _accept[i].gameObject.SetActive(on);
                _decline[i].gameObject.SetActive(on);
                if (on) _reqLines[i].SetText(F(OnlineKeys.FriendsRequestLine, "name", _requests[i].Name, "rating", _requests[i].Rating));
            }
            for (int i = 0; i < _friendLines.Count; i++)
            {
                bool on = i < _friends.Count;
                _friendLines[i].gameObject.SetActive(on);
                _removeButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                var x = _friends[i];
                string where = T(OnlineKeys.FriendsWhere + (string.IsNullOrEmpty(x.Where) ? "off" : x.Where));
                _friendLines[i].SetText(F(OnlineKeys.FriendsLine, "name", x.Name, "rating", x.Rating, "where", where));
            }
        }

        void Show(AccountStore.FriendResult? r)
        {
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
