using System;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // spec 13 1.5절 RunSubmitPending(토스트): "지난 판을 서버에 내는 중", 버튼 없음. 입력을 받지 않는다. ScreenStack.DismissToast 로 닫는다
    public sealed class RunSubmitPendingModel : DialogScreen
    {
        public RunSubmitPendingModel(ScreenStack stack, InputModeTracker mode)
            : base(stack, mode)
        {
        }

        public override ScreenId Id => ScreenId.RunSubmitPending;
        public override ScreenLayer Layer => ScreenLayer.Toast;
        public override bool BlocksInput => false;

        public string MessageKey => CommonTextKeys.RunSubmitPendingMessage;

        protected override Action<DialogButtonRole>? Setup(ScreenArgs args) => null;
    }
}
