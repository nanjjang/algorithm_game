using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>
    /// 칭호 고르기(ScreenId.TitlePicker, 08 11.1절). 가운데 720×640 창: 얻은 칭호 중 닉네임 앞에 붙일 것 하나.
    /// 첫 줄 "칭호 없음"(.none), 그다음 TitleModel.Available(기록·손맛·복수 + 강화 단계 칭호). 붙인 것은 확정 표시(Picked).
    /// 고르면 titles.equipped를 저장하고 닫는다. Esc·K = 그대로 닫기.
    /// </summary>
    public sealed class TitlePickerScreen : ScreenBase
    {
        const float W = 720f, H = 640f, Pad = 32f, RowH = 56f, BtnW = 200f, BtnH = 56f;

        RectTransform _board;
        UiScrollList _list;
        UiButton _close;
        readonly List<UiButton> _rows = new List<UiButton>();

        public override ScreenId Id => ScreenId.TitlePicker;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        protected override void BuildContent(RectTransform content)
        {
            _board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, W, H).rectTransform;
            UiKit.Center(_board, W, H);
            _board.name = "TitlePickerBoard";
            float th = UiTheme.LineHeight(UiFontId.F32);
            UiKit.Label(Ctx, _board, T(MetaKeys.TitlePickerTitle), UiFontId.F32, Pad, Pad - 8f, W - 2f * Pad, th, UiTheme.Chalk,
                TextAnchor.MiddleCenter);
            float listY = Pad + th + 16f;
            _list = UiKit.ScrollList(Ctx, _board, Pad, listY, W - 2f * Pad, H - listY - Pad - BtnH - 16f, Focus, 4f);
            _close = UiKit.Button(Ctx, _board, T(MetaKeys.CommonClose), (W - BtnW) * 0.5f, H - Pad - BtnH, BtnW, BtnH, () => Close(),
                UiButtonStyle.Medium, null);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Rebuild();
        }

        void Rebuild()
        {
            Focus.Clear();
            MetaUi.ClearChildren(_list.Content);
            _list.Clear();
            _rows.Clear();
            var p = Ctx != null ? Ctx.Profile : null;
            var ms = TitleModel.Milestones(Ctx != null ? Ctx.Content : null);
            string equipped = TitleModel.Resolve(p, ms);
            var ids = new List<string> { null };
            ids.AddRange(TitleModel.Available(p, ms));
            float w = W - 2f * Pad - 16f;
            int cursor = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                var row = _list.AddRow(RowH, "Title" + i);
                string text = id == null ? T(MetaKeys.TitlePickerNone) : T(id);
                var b = UiKit.Button(Ctx, row, text, 0f, 0f, w, RowH, () => Pick(id), UiButtonStyle.Plain, Focus, null, UiFontId.F24);
                b.Picked = id == equipped;
                if (b.Picked) cursor = i;
                _rows.Add(b);
            }
            Focus.Add(_close);
            Focus.SetCursor(cursor, false);
        }

        void Pick(string id)
        {
            var ms = TitleModel.Milestones(Ctx != null ? Ctx.Content : null);
            if (TitleModel.Equip(Ctx != null ? Ctx.Profiles : null, id, ms)) Close(id);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            return base.OnMenuKey(key);
        }
    }
}
