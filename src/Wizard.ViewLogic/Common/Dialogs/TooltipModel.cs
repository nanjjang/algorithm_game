using System;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // spec 13 1.5절 Tooltip. 최대 폭 400px. 넘치는 줄은 끝을 "…"로 자른다(spec 13 1.4). 아래 화면의 입력을 가로막지 않는 Panel 이다.
    // Esc 는 소비하지 않는다(스택이 툴팁을 닫는다)
    public sealed class TooltipModel : DialogScreen
    {
        public const int MaxWidthPx = 400;

        public TooltipModel(ScreenStack stack, InputModeTracker mode)
            : base(stack, mode)
        {
        }

        public override ScreenId Id => ScreenId.Tooltip;
        public override ScreenLayer Layer => ScreenLayer.Panel;
        public override bool BlocksInput => false;

        public TooltipRequest? Content { get; private set; }

        public string? TradableKey =>
            Content?.Tradable == null ? null : Content.Tradable.Value ? CommonTextKeys.TooltipTradable : CommonTextKeys.TooltipUntradable;

        // 이름 표에서 찾은 글 한 줄을 툴팁 폭에 맞춘다
        public static string FitLine(string text, int fontPx) => UiText.Fit(text, fontPx, MaxWidthPx);

        public override bool OnBack() => false;

        protected override Action<DialogButtonRole>? Setup(ScreenArgs args)
        {
            Content = PayloadOf<TooltipRequest>(args);
            return null;
        }
    }
}
