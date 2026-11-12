using System;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // spec 13 1.5절 SaveRestored: 오프라인 저장 파일이 깨져 직전 세대로 되돌린 사실, [확인]. Modal. Esc = [확인]
    public sealed class SaveRestoredModel : DialogScreen
    {
        public SaveRestoredModel(ScreenStack stack, InputModeTracker mode)
            : base(stack, mode)
        {
        }

        public override ScreenId Id => ScreenId.SaveRestored;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        public string MessageKey => CommonTextKeys.SaveRestoredMessage;

        public int GenerationsBack { get; private set; }

        protected override DialogButtonRole? CancelRole => DialogButtonRole.Ok;

        protected override Action<DialogButtonRole>? Setup(ScreenArgs args)
        {
            SaveRestoredRequest r = PayloadOf<SaveRestoredRequest>(args);
            GenerationsBack = r.GenerationsBack;
            AddButton(DialogButtonRole.Ok, CommonTextKeys.Ok);
            return r.OnChosen;
        }
    }
}
