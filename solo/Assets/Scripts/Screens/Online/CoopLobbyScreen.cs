using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.UI;
using Wizard.Solo.Art;
using UInputField = UnityEngine.UI.InputField;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 협동 방(ScreenId.CoopLobby, 11-online.md). 판 720×700: 제목, 안내, 서버 주소 칸 + [참가], 자리 네 줄, 단추 줄, 결과·오류 한 줄.
    /// 규칙은 CoopLobbyModel. Esc = 방에서 나가 메인 메뉴로.
    /// </summary>
    public sealed class CoopLobbyScreen : ScreenBase
    {
        const float BoardW = 720f, BoardH = 700f, Pad = 32f, RowH = 44f, BtnH = 56f;

        CoopLobbyModel _model;
        readonly List<UiLabel> _rows = new List<UiLabel>();
        UiLabel _status;
        UiButton _create, _join, _addBot, _removeBot, _start, _leave;
        UInputField _address;

        public override ScreenId Id => ScreenId.CoopLobby;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            _model = new CoopLobbyModel(Ctx);
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, BoardW, BoardH).rectTransform;
            UiKit.Center(board, BoardW, BoardH);
            board.name = "CoopLobbyBoard";

            float y = Pad - 8f;
            float th = UiTheme.LineHeight(UiFontId.F32);
            UiKit.Label(Ctx, board, T(OnlineKeys.Title), UiFontId.F32, Pad, y, BoardW - 2f * Pad, th, UiTheme.Chalk, TextAnchor.MiddleCenter);
            y += th + 8f;
            float lh = UiTheme.LineHeight(UiFontId.F16);
            UiKit.Label(Ctx, board, T(OnlineKeys.Intro), UiFontId.F16, Pad, y, BoardW - 2f * Pad, lh * 2f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            y += lh * 2f + 16f;

            float joinW = 180f;
            _address = BuildInput(board, Pad, y, BoardW - 2f * Pad - joinW - 12f, BtnH);
            _join = UiKit.Button(Ctx, board, T(OnlineKeys.Join), BoardW - Pad - joinW, y, joinW, BtnH, OnJoin, UiButtonStyle.Medium, Focus);
            y += BtnH + 16f;

            var list = UiKit.Panel(Ctx, board, UiPanelKind.Recess, Pad, y, BoardW - 2f * Pad, RowH * SimConst.MaxPlayers + 16f).rectTransform;
            list.name = "CoopMembers";
            for (int i = 0; i < SimConst.MaxPlayers; i++)
                _rows.Add(UiKit.Label(Ctx, list, string.Empty, UiFontId.F24, 16f, 8f + RowH * i, BoardW - 2f * Pad - 32f, RowH, UiTheme.Chalk));
            y += RowH * SimConst.MaxPlayers + 32f;

            float bw = (BoardW - 2f * Pad - 24f) / 3f;
            _create = UiKit.Button(Ctx, board, T(OnlineKeys.Create), Pad, y, bw, BtnH, OnCreate, UiButtonStyle.Medium, Focus);
            _addBot = UiKit.Button(Ctx, board, T(OnlineKeys.AddBot), Pad + bw + 12f, y, bw, BtnH, OnAddBot, UiButtonStyle.Medium, Focus);
            _removeBot = UiKit.Button(Ctx, board, T(OnlineKeys.RemoveBot), Pad + (bw + 12f) * 2f, y, bw, BtnH, OnRemoveBot, UiButtonStyle.Medium, Focus);
            y += BtnH + 12f;
            float bw2 = (BoardW - 2f * Pad - 12f) / 2f;
            _start = UiKit.Button(Ctx, board, T(OnlineKeys.Start), Pad, y, bw2, BtnH, OnStart, UiButtonStyle.Primary, Focus);
            _leave = UiKit.Button(Ctx, board, T(OnlineKeys.Leave), Pad + bw2 + 12f, y, bw2, BtnH, OnLeave, UiButtonStyle.Medium, Focus);
            y += BtnH + 12f;
            _status = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, BoardW - 2f * Pad, lh, UiTheme.Amber, TextAnchor.MiddleCenter);
        }

        UInputField BuildInput(RectTransform parent, float x, float y, float w, float h)
        {
            var bg = UiKit.NineSlice(Ctx, parent, "K-33", "input", null, x, y, w, h, UiTheme.Chalk);
            bg.raycastTarget = true;
            var field = bg.gameObject.AddComponent<UInputField>();
            var text = UiKit.Label(Ctx, bg.rectTransform, string.Empty, UiFontId.F16, 8f, 0f, w - 16f, h, UiTheme.PaperText);
            var ut = text.GetComponent<UText>();
            ut.supportRichText = false;
            field.textComponent = ut;
            field.lineType = UInputField.LineType.SingleLine;
            var ph = UiKit.Label(Ctx, bg.rectTransform, T(OnlineKeys.AddressHint), UiFontId.F16, 8f, 0f, w - 16f, h, UiTheme.SlateBlue);
            field.placeholder = ph.GetComponent<UText>();
            return field;
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _model.Enter(args);
            Refresh();
            Focus.SetCursor(0, false);
        }

        public override void Tick(float dt)
        {
            // 주소 칸에 글을 치는 동안 메뉴 키가 새지 않게 잡아 둔다(WandReleaseConfirm과 같은 방식).
            if (Screens != null && _address != null) Screens.CaptureKeys = _address.isFocused;
            if (_model.Tick()) { GoBattle(); return; }
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
            var members = _model.Members();
            for (int i = 0; i < _rows.Count; i++)
            {
                CoopMemberText(i, members, out string text);
                _rows[i].SetText(text);
            }
            _create.Enabled = !_model.HasRoom;
            _join.Enabled = !_model.HasRoom;
            _address.interactable = !_model.HasRoom;
            _addBot.Enabled = _model.CanAddBot;
            _removeBot.Enabled = _model.CanRemoveBot;
            _start.Enabled = _model.CanStart;
            string status = string.Empty;
            if (_model.ErrorKey != null) status = T(_model.ErrorKey);
            else if (_model.WaitingForStart) status = T(OnlineKeys.Waiting);
            else if (_model.ResultWave >= 0) status = F(OnlineKeys.Result, "wave", _model.ResultWave, "score", N(_model.ResultScore));
            _status.SetText(status);
        }

        void CoopMemberText(int slot, List<Wizard.Solo.Net.CoopClient.LobbyMember> members, out string text)
        {
            foreach (var m in members)
            {
                if (m.Slot != slot) continue;
                string who = T(m.CharId);
                who += " · " + T(_model.RoleKey(slot));
                text = F(OnlineKeys.MemberLine, "slot", slot + 1, "who", who, "stage", m.WandStage);
                return;
            }
            text = F(OnlineKeys.Empty, "slot", slot + 1);
        }

        void OnCreate() { _model.CreateRoom(); Refresh(); }
        void OnJoin() { _model.JoinRoom(_address.text); Refresh(); }
        void OnAddBot() { _model.AddBot(); Refresh(); }
        void OnRemoveBot() { _model.RemoveBot(); Refresh(); }

        void OnStart()
        {
            ulong seed = (ulong)System.DateTime.UtcNow.Ticks;
            if (!_model.Start(seed)) { Refresh(); return; }
            GoBattle();
        }

        void OnLeave()
        {
            _model.Leave();
            Close();
        }

        public override bool OnBack() { OnLeave(); return true; }
    }
}
