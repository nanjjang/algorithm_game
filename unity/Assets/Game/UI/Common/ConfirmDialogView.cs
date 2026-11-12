#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Common.Dialogs;

namespace Wizard.Unity.UI.Common
{
    // ConfirmDialog(spec 13 1.5): 물음 한 줄(F-24), 설명 두 줄까지(F-16), [예] [아니오]. 글은 모델이 준 이름 표 열쇠다
    public sealed class ConfirmDialogView : DialogScreenView
    {
        private readonly List<UiLabel> _details = new List<UiLabel>();

        public ConfirmDialogView(ConfirmDialogModel model, UiViewContext context)
            : base(model, context)
        {
            Model = model;
        }

        public ConfirmDialogModel Model { get; }

        public UiLabel? Question { get; private set; }

        public IReadOnlyList<UiLabel> Details => _details;

        protected override void BuildBody(UiPanel panel, PixelRect area)
        {
            int y = area.Y;
            Question = new UiLabel(Context, panel, new PixelRect(area.X, y, area.Width, UiFonts.F24.LineHeightPx), UiFontId.F24, TextAnchor.MiddleCenter);
            Question.SetKey(Model.QuestionKey);
            y += UiFonts.F24.LineHeightPx + UiLayout.GridPx * 2;
            foreach (string key in Model.DetailKeys)
            {
                var line = new UiLabel(Context, panel, new PixelRect(area.X, y, area.Width, UiFonts.F16.LineHeightPx), UiFontId.F16, TextAnchor.MiddleCenter);
                line.SetKey(key);
                _details.Add(line);
                y += UiFonts.F16.LineHeightPx;
            }
        }
    }
}
