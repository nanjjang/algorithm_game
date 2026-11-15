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
    /// 온라인 방(ScreenId.CoopLobby, 11-online.md). 어몽어스식: [온라인 방 만들기]를 누르면 6자리 코드가 나오고,
    /// 친구는 코드를 넣고 [참가]. 방 종류는 협동(4명)·결투(1대1). [이 기기에서]는 인터넷 없이 봇과 하는 방.
    /// 규칙은 CoopLobbyModel. Esc = 방에서 나가 메인 메뉴로.
    /// </summary>
    public sealed class CoopLobbyScreen : ScreenBase
    {
        const float BoardW = 760f, BoardH = 820f, Pad = 32f, RowH = 44f, BtnH = 56f, Gap = 12f;

        CoopLobbyModel _model;
        readonly List<UiLabel> _rows = new List<UiLabel>();
        UiLabel _title, _status, _code;
        UiButton _modeCoop, _modeDuel, _plaza, _createOnline, _createLocal, _join, _addBot, _removeBot, _start, _leave;
        UInputField _codeInput;

        public override ScreenId Id => ScreenId.CoopLobby;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            _model = new CoopLobbyModel(Ctx);
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, BoardW, BoardH).rectTransform;
            UiKit.Center(board, BoardW, BoardH);
            board.name = "CoopLobbyBoard";
            float inner = BoardW - 2f * Pad;

            float y = Pad - 8f;
            float th = UiTheme.LineHeight(UiFontId.F32);
            _title = UiKit.Label(Ctx, board, T(OnlineKeys.Title), UiFontId.F32, Pad, y, inner, th, UiTheme.Chalk, TextAnchor.MiddleCenter);
            y += th + 8f;

            float w3 = (inner - 2f * Gap) / 3f;
            _modeCoop = UiKit.Button(Ctx, board, T(OnlineKeys.ModeCoop), Pad, y, w3, BtnH, () => SelectMode(RoomMode.Coop), UiButtonStyle.Medium, Focus);
            _modeDuel = UiKit.Button(Ctx, board, T(OnlineKeys.ModeDuel), Pad + w3 + Gap, y, w3, BtnH, () => SelectMode(RoomMode.Duel), UiButtonStyle.Medium, Focus);
            _plaza = UiKit.Button(Ctx, board, T(OnlineKeys.Plaza), Pad + 2f * (w3 + Gap), y, w3, BtnH, OnPlaza, UiButtonStyle.Medium, Focus);
            y += BtnH + Gap;

            float w2 = (inner - Gap) / 2f;
            _createOnline = UiKit.Button(Ctx, board, T(OnlineKeys.CreateOnline), Pad, y, w2, BtnH, OnCreateOnline, UiButtonStyle.Primary, Focus);
            _createLocal = UiKit.Button(Ctx, board, T(OnlineKeys.CreateLocal), Pad + w2 + Gap, y, w2, BtnH, OnCreateLocal, UiButtonStyle.Medium, Focus);
            y += BtnH + Gap;

            float joinW = 180f;
            _codeInput = BuildInput(board, Pad, y, inner - joinW - Gap, BtnH);
            _join = UiKit.Button(Ctx, board, T(OnlineKeys.Join), BoardW - Pad - joinW, y, joinW, BtnH, OnJoin, UiButtonStyle.Medium, Focus);
            y += BtnH + Gap;

            float ch = UiTheme.LineHeight(UiFontId.F48);
            _code = UiKit.Label(Ctx, board, string.Empty, UiFontId.F48, Pad, y, inner, ch, UiTheme.SelectYellow, TextAnchor.MiddleCenter);
            y += ch + 8f;

            var list = UiKit.Panel(Ctx, board, UiPanelKind.Recess, Pad, y, inner, RowH * SimConst.MaxPlayers + 16f).rectTransform;
            list.name = "CoopMembers";
            for (int i = 0; i < SimConst.MaxPlayers; i++)
                _rows.Add(UiKit.Label(Ctx, list, string.Empty, UiFontId.F24, 16f, 8f + RowH * i, inner - 32f, RowH, UiTheme.Chalk));
            y += RowH * SimConst.MaxPlayers + 16f + Gap;

            _addBot = UiKit.Button(Ctx, board, T(OnlineKeys.AddBot), Pad, y, w2, BtnH, OnAddBot, UiButtonStyle.Medium, Focus);
            _removeBot = UiKit.Button(Ctx, board, T(OnlineKeys.RemoveBot), Pad + w2 + Gap, y, w2, BtnH, OnRemoveBot, UiButtonStyle.Medium, Focus);
            y += BtnH + Gap;
            _start = UiKit.Button(Ctx, board, T(OnlineKeys.Start), Pad, y, w2, BtnH, OnStart, UiButtonStyle.Primary, Focus);
            _leave = UiKit.Button(Ctx, board, T(OnlineKeys.Leave), Pad + w2 + Gap, y, w2, BtnH, OnLeave, UiButtonStyle.Medium, Focus);
            y += BtnH + Gap;
            float lh = UiTheme.LineHeight(UiFontId.F16);
            _status = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, inner, lh * 2f, UiTheme.Amber, TextAnchor.UpperCenter, wrap: true);
        }

        UInputField BuildInput(RectTransform parent, float x, float y, float w, float h)
        {
            var bg = UiKit.NineSlice(Ctx, parent, "K-33", "input", null, x, y, w, h, UiTheme.Chalk);
            bg.raycastTarget = true;
            var field = bg.gameObject.AddComponent<UInputField>();
            var text = UiKit.Label(Ctx, bg.rectTransform, string.Empty, UiFontId.F24, 8f, 0f, w - 16f, h, UiTheme.PaperText, TextAnchor.MiddleCenter);
            var ut = text.GetComponent<UText>();
            ut.supportRichText = false;
            field.textComponent = ut;
            field.lineType = UInputField.LineType.SingleLine;
            field.characterLimit = RoomServer.CodeLength + 2;
            var ph = UiKit.Label(Ctx, bg.rectTransform, T(OnlineKeys.CodeHint), UiFontId.F16, 8f, 0f, w - 16f, h, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
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
            if (Screens != null && _codeInput != null) Screens.CaptureKeys = _codeInput.isFocused;
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
            bool duel = _model.Mode == RoomMode.Duel;
            _title.SetText(T(duel ? OnlineKeys.ModeDuel : OnlineKeys.ModeCoop));
            var members = _model.Members();
            for (int i = 0; i < _rows.Count; i++)
            {
                bool shown = i < _model.MaxMembers;
                _rows[i].gameObject.SetActive(shown);
                if (shown) _rows[i].SetText(MemberText(i, members));
            }
            bool room = _model.HasRoom;
            _modeCoop.Enabled = !room && _model.SelectedMode != RoomMode.Coop;
            _modeDuel.Enabled = !room && _model.SelectedMode != RoomMode.Duel;
            _plaza.Enabled = !room;
            _createOnline.Enabled = !room;
            _createLocal.Enabled = !room;
            _join.Enabled = !room;
            _codeInput.interactable = !room;
            _addBot.Enabled = _model.CanAddBot;
            _removeBot.Enabled = _model.CanRemoveBot;
            _start.Enabled = _model.CanStart;
            _code.SetText(_model.Code != null ? F(OnlineKeys.CodeLine, "code", _model.Code) : string.Empty);

            string status = string.Empty;
            if (_model.ErrorKey != null) status = T(_model.ErrorKey);
            else if (_model.WaitingForStart) status = T(OnlineKeys.Waiting);
            else if (room && duel && _model.Room.MemberCount < 2) status = T(OnlineKeys.DuelNeedTwo);
            else if (_model.ResultDuelWinner >= -1)
            {
                string head = _model.ResultDuelWinner < 0 ? T(OnlineKeys.DuelDraw) : T(_model.ResultDuelWinner == _model.ResultMySlot ? OnlineKeys.DuelWin : OnlineKeys.DuelLose);
                status = head + "  " + _model.ResultWins0 + " : " + _model.ResultWins1;
            }
            else if (_model.ResultWave >= 0) status = F(OnlineKeys.Result, "wave", _model.ResultWave, "score", N(_model.ResultScore));
            _status.SetText(status);
        }

        string MemberText(int slot, List<CoopClient.LobbyMember> members)
        {
            foreach (var m in members)
            {
                if (m.Slot != slot) continue;
                string who = T(m.CharId) + " · " + T(_model.RoleKey(slot));
                if (_model.Room != null && _model.Room.Slot == slot) who += " ★";
                return F(OnlineKeys.MemberLine, "slot", slot + 1, "who", who, "stage", m.WandStage);
            }
            return F(OnlineKeys.Empty, "slot", slot + 1);
        }

        void SelectMode(RoomMode m) { _model.SelectedMode = m; _model.ResultDuelWinner = -2; _model.ResultWave = -1; Refresh(); }
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
            _model.Leave();
            Close();
        }

        public override bool OnBack() { OnLeave(); return true; }
    }
}
