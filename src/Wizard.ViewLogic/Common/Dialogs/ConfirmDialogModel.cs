using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // spec 13 1.5절 ConfirmDialog. Modal. Esc = [아니오].
    // 처음 커서: 조심할 일이면 [아니오], 아니면 [예] [임시: 명세가 정하지 않았다. 조심할 일을 J 한 번에 하지 않게 한다]
    public sealed class ConfirmDialogModel : DialogScreen
    {
        private static readonly string[] NoDetails = new string[0];

        public ConfirmDialogModel(ScreenStack stack, InputModeTracker mode)
            : base(stack, mode)
        {
        }

        public override ScreenId Id => ScreenId.ConfirmDialog;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        public string QuestionKey { get; private set; } = string.Empty;
        public IReadOnlyList<string> DetailKeys { get; private set; } = NoDetails;
        public bool Caution { get; private set; }

        protected override DialogButtonRole? CancelRole => DialogButtonRole.No;

        protected override Action<DialogButtonRole>? Setup(ScreenArgs args)
        {
            ConfirmRequest r = PayloadOf<ConfirmRequest>(args);
            QuestionKey = r.QuestionKey;
            DetailKeys = r.DetailKeys;
            Caution = r.Caution;
            AddButton(DialogButtonRole.Yes, CommonTextKeys.Yes, r.Caution ? ButtonTone.Caution : ButtonTone.Normal);
            AddButton(DialogButtonRole.No, CommonTextKeys.No);
            return r.OnChosen;
        }

        protected override DialogButton? DefaultButton() => FindButton(Caution ? DialogButtonRole.No : DialogButtonRole.Yes);
    }
}
