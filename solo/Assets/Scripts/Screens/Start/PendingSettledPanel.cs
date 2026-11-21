using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 자동 정산 알림 창(00 문서 D-68). 메인 메뉴 안의 덮개 창: 뒤를 먹색 60%로 덮고(마우스도 막는다) 쪽지 판에 제목·안내 한 줄·
    /// 받은 것 목록(넘치면 위아래로 굴린다)·[확인]. 줄은 PendingSettledModel이 만든다. 확인·K·Esc로 닫히고 다시 뜨지 않는다
    /// (인자는 메인 메뉴가 꺼낼 때 지운다). 새 ScreenId 없이 메인 메뉴가 키를 이 창에 먼저 넘긴다.
    /// </summary>
    public sealed class PendingSettledPanel
    {
        const float PanelW = 800f, Pad = 32f, BtnW = 200f, BtnH = 64f, RowH = 28f, ListMaxH = 448f, IconS = 24f;

        readonly GameContext _ctx;
        readonly RectTransform _parent;
        readonly UiFocusGroup _focus;
        RectTransform _root;
        UiScrollList _list;
        readonly List<RectTransform> _rows = new List<RectTransform>();
        int _scroll;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        /// <summary>마지막으로 그린 줄(테스트·디버그용).</summary>
        public IReadOnlyList<string> Lines => _lines;
        readonly List<string> _lines = new List<string>();

        public PendingSettledPanel(GameContext ctx, RectTransform parent, UiFocusGroup focus)
        {
            _ctx = ctx;
            _parent = parent;
            _focus = focus;
        }

        string T(string key) => _ctx != null && _ctx.Names != null ? _ctx.Names.Get(key) : key;

        public void Show(SettlementResult result)
        {
            Hide();
            var lines = PendingSettledModel.Lines(result);
            _lines.Clear();
            var names = _ctx != null ? _ctx.Names : null;
            for (int i = 0; i < lines.Count; i++) _lines.Add(PendingSettledModel.Text(lines[i], names));

            _root = UiKit.Node(_parent, "PendingSettled", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            _root.SetAsLastSibling();
            UiFullBleed.Fill(UiKit.Rect(_root, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.ModalDim, true, "Dim").rectTransform);

            int gainCount = lines.Count - 1;
            float h24 = UiTheme.LineHeight(UiFontId.F24), h16 = UiTheme.LineHeight(UiFontId.F16);
            float listH = Mathf.Min(ListMaxH, gainCount * (RowH + 4f));
            float panelH = Pad + h24 + 16f + h16 * 2f + 16f + h16 + 8f + listH + 24f + BtnH + Pad;
            var panel = UiKit.Panel(_ctx, _root, UiPanelKind.Paper, 0f, 0f, PanelW, panelH).rectTransform;
            UiKit.Center(panel, PanelW, panelH);
            panel.name = "PendingSettledPanel";

            float y = Pad;
            float w = PanelW - 2f * Pad;
            UiKit.Label(_ctx, panel, T(StartNameKeys.PendingTitle), UiFontId.F24, Pad, y, w, h24, UiTheme.PaperText);
            y += h24 + 16f;
            UiKit.Label(_ctx, panel, _lines[0], UiFontId.F16, Pad, y, w, h16 * 2f, UiTheme.PaperText, TextAnchor.UpperLeft, wrap: true);
            y += h16 * 2f + 16f;
            UiKit.Label(_ctx, panel, T(StartNameKeys.PendingReceived), UiFontId.F16, Pad, y, w, h16, UiTheme.PaperText);
            y += h16 + 8f;

            _list = UiKit.ScrollList(_ctx, panel, Pad, y, w, Mathf.Max(listH, RowH), null, 4f);
            _rows.Clear();
            for (int i = 1; i < lines.Count; i++)
            {
                var row = _list.AddRow(RowH);
                float tx = 0f;
                var icon = RowIcon(lines[i]);
                if (icon != null)
                {
                    UiKit.Image(row, icon, 0f, (RowH - IconS) * 0.5f, IconS, IconS, null, true);
                    tx = IconS + 8f;
                }
                UiKit.Label(_ctx, row, _lines[i], UiFontId.F16, tx, 0f, w - 24f - tx, RowH, UiTheme.PaperText);
                _rows.Add(row);
            }
            y += Mathf.Max(listH, RowH) + 24f;
            _focus.Clear();
            UiKit.Button(_ctx, panel, T(StartNameKeys.CommonOk), (PanelW - BtnW) * 0.5f, y, BtnW, BtnH, Hide, UiButtonStyle.Large, _focus);
            _scroll = 0;
        }

        Sprite RowIcon(PendingLine l)
        {
            var art = _ctx != null ? _ctx.Art : null;
            if (art == null) return null;
            switch (l.Kind)
            {
                case PendingLineKind.Gold: return art.Icon("ui:I-C2/gold", (int)IconS);
                case PendingLineKind.Material:
                case PendingLineKind.Block:
                case PendingLineKind.CrowBundle:
                case PendingLineKind.Art:
                    return string.IsNullOrEmpty(l.ContentId) ? null : art.Icon(l.ContentId, (int)IconS);
                default: return null;
            }
        }

        public void Hide()
        {
            if (_root == null) return;
            _focus.Clear();
            UnityEngine.Object.Destroy(_root.gameObject);
            _root = null;
            _list = null;
            _rows.Clear();
        }

        /// <summary>창이 떠 있는 동안의 메뉴 키(전부 먹는다). J = 확인, K = 닫기, 위아래 = 목록 굴리기.</summary>
        public bool HandleKey(MenuKey key)
        {
            if (!IsOpen) return false;
            switch (key)
            {
                case MenuKey.Cancel:
                    Hide();
                    return true;
                case MenuKey.Up:
                case MenuKey.Down:
                    if (_rows.Count > 0 && _list != null)
                    {
                        _scroll += key == MenuKey.Down ? 1 : -1;
                        if (_scroll < 0) _scroll = 0;
                        if (_scroll >= _rows.Count) _scroll = _rows.Count - 1;
                        _list.EnsureVisible(_rows[_scroll]);
                    }
                    return true;
                default:
                    _focus.HandleKey(key);
                    return true;
            }
        }
    }
}
