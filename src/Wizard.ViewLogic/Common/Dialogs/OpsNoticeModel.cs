using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // spec 13 1.5절 OpsNotice: 점검, 시즌 일정(스크롤), [닫기]. Modal. Esc = [닫기]
    public sealed class OpsNoticeModel : DialogScreen
    {
        private static readonly string[] NoLines = new string[0];

        public OpsNoticeModel(ScreenStack stack, InputModeTracker mode)
            : base(stack, mode)
        {
        }

        public override ScreenId Id => ScreenId.OpsNotice;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        public IReadOnlyList<string> LineKeys { get; private set; } = NoLines;
        public int VisibleLines { get; private set; } = 1;

        // 맨 위에 보이는 줄의 번호
        public int ScrollOffset { get; private set; }

        public int MaxScrollOffset => Math.Max(0, LineKeys.Count - VisibleLines);
        public bool CanScrollUp => ScrollOffset > 0;
        public bool CanScrollDown => ScrollOffset < MaxScrollOffset;

        // 끝에서 멈춘다
        public void ScrollBy(int lines) => ScrollOffset = Math.Max(0, Math.Min(MaxScrollOffset, ScrollOffset + lines));

        protected override DialogButtonRole? CancelRole => DialogButtonRole.Close;

        protected override Action<DialogButtonRole>? Setup(ScreenArgs args)
        {
            OpsNoticeRequest r = PayloadOf<OpsNoticeRequest>(args);
            LineKeys = r.LineKeys;
            VisibleLines = r.VisibleLines;
            ScrollOffset = 0;
            AddButton(DialogButtonRole.Close, CommonTextKeys.Close);
            return r.OnChosen;
        }
    }
}
