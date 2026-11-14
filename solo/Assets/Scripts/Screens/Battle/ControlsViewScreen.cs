using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Battle
{
    /// <summary>
    /// 조작 보기(ScreenId.ControlsView, Modal, 가운데 960×640). 08 요약서 7절: 지금 키 배치 그대로의 조작표.
    /// 줄 9개(논리 키), 열 = 기능 이름 · 기본 키 · 보조 키. 일시정지 줄은 Esc(고정) + 재설정한 키.
    /// Esc·K·[닫기] = 닫기(일시정지 화면으로).
    /// </summary>
    public sealed class ControlsViewScreen : ScreenBase
    {
        public const float PanelW = 960f, PanelH = 640f;
        const float Pad = 32f, RowH = 44f, KeyCapPx = 32f, KeyGap = 8f;
        const float ActionColW = 320f, KeyColW = 260f;

        RectTransform _table;
        readonly List<GameObject> _rowObjects = new List<GameObject>();

        public override ScreenId Id => ScreenId.ControlsView;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        protected override void BuildContent(RectTransform content)
        {
            float px = (UiScale.RefWidth - PanelW) * 0.5f, py = (UiScale.RefHeight - PanelH) * 0.5f;
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Board, px, py, PanelW, PanelH).rectTransform;
            panel.name = "ControlsView";
            float th = UiTheme.LineHeight(UiFontId.F32);
            UiKit.LabelKey(Ctx, panel, ControlsViewModel.TitleKey, UiFontId.F32, Pad, 24f, PanelW - Pad * 2f, th, UiTheme.Chalk, TextAnchor.MiddleCenter);
            float y = 24f + th + 16f;
            float hh = UiTheme.LineHeight(UiFontId.F16);
            float colKey1 = Pad + ActionColW, colKey2 = colKey1 + KeyColW;
            UiKit.LabelKey(Ctx, panel, ControlsViewModel.PrimaryHeaderKey, UiFontId.F16, colKey1, y, KeyColW, hh, UiTheme.SlateBlue);
            UiKit.LabelKey(Ctx, panel, ControlsViewModel.SecondaryHeaderKey, UiFontId.F16, colKey2, y, KeyColW, hh, UiTheme.SlateBlue);
            y += hh + 8f;
            UiKit.Divider(Ctx, panel, Pad, y, PanelW - Pad * 2f);
            y += 8f;
            _table = UiKit.Node(panel, "Table", 0f, y, PanelW, RowH * ControlsViewModel.Order.Length);
            const float closeW = 240f, closeH = 56f;
            UiKit.Button(Ctx, panel, T("str.common.close"), (PanelW - closeW) * 0.5f, PanelH - closeH - 24f, closeW, closeH,
                () => Close(), UiButtonStyle.Medium, Focus);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Rebuild();
        }

        void Rebuild()
        {
            for (int i = 0; i < _rowObjects.Count; i++) if (_rowObjects[i] != null) Object.Destroy(_rowObjects[i]);
            _rowObjects.Clear();
            if (_table == null) return;
            var settings = Ctx != null && Ctx.Settings != null ? Ctx.Settings.Current : null;
            var rows = ControlsViewModel.Build(settings);
            float colKey1 = Pad + ActionColW, colKey2 = colKey1 + KeyColW;
            float lh = UiTheme.LineHeight(UiFontId.F24);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var row = UiKit.Node(_table, "Row." + r.Action, 0f, i * RowH, PanelW, RowH);
                _rowObjects.Add(row.gameObject);
                UiKit.LabelKey(Ctx, row, r.ActionNameKey, UiFontId.F24, Pad, (RowH - lh) * 0.5f, ActionColW - 16f, lh, UiTheme.Chalk);
                PlaceKeys(row, r.Primary, colKey1);
                PlaceKeys(row, r.Secondary, colKey2);
            }
        }

        void PlaceKeys(RectTransform row, Key[] keys, float x)
        {
            float y = (RowH - KeyCapPx) * 0.5f;
            if (keys == null || keys.Length == 0)
            {
                UiKit.Label(Ctx, row, UiKeyLabels.Of(Ctx != null ? Ctx.Names : null, Key.None), UiFontId.F24, x, y, KeyCapPx, KeyCapPx,
                    UiTheme.SlateBlue, TextAnchor.MiddleCenter);
                return;
            }
            for (int i = 0; i < keys.Length; i++)
            {
                // 글이 긴 키(Shift, Space …)는 마개 폭을 글에 맞춰 넓힌다.
                string label = UiKeyLabels.Of(Ctx != null ? Ctx.Names : null, keys[i]);
                if (label.Length <= 1)
                {
                    UiKit.KeyCap(Ctx, row, keys[i], x, y, KeyCapPx);
                    x += KeyCapPx + KeyGap;
                }
                else
                {
                    float w = Mathf.Max(KeyCapPx, 16f + label.Length * 14f);
                    var cap = UiKit.NineSlice(Ctx, row, "K-51", "keycap", null, x, y, w, KeyCapPx, UiTheme.WoodLight);
                    cap.name = "KeyCap." + keys[i];
                    UiKit.Label(Ctx, cap.rectTransform, label, UiFontId.F16, 0f, 0f, w, KeyCapPx, UiTheme.Chalk, TextAnchor.MiddleCenter);
                    x += w + KeyGap;
                }
            }
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            return base.OnMenuKey(key);
        }
    }
}
