using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // spec 13 1.5절 SanctionNotice: 막힌 기능, 기간, 사유, [확인]. Modal. Esc = [확인]
    public sealed class SanctionNoticeModel : DialogScreen
    {
        private static readonly string[] NoFeatures = new string[0];

        public SanctionNoticeModel(ScreenStack stack, InputModeTracker mode)
            : base(stack, mode)
        {
        }

        public override ScreenId Id => ScreenId.SanctionNotice;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        public IReadOnlyList<string> BlockedFeatureKeys { get; private set; } = NoFeatures;
        public long? RemainingSeconds { get; private set; }
        public string ReasonKey { get; private set; } = string.Empty;

        // 기한이 없으면 "기한 없음" 열쇠, 있으면 null(남은 초를 숫자로 보인다)
        public string? PermanentKey => RemainingSeconds.HasValue ? null : CommonTextKeys.SanctionPermanent;

        protected override DialogButtonRole? CancelRole => DialogButtonRole.Ok;

        protected override Action<DialogButtonRole>? Setup(ScreenArgs args)
        {
            SanctionNoticeRequest r = PayloadOf<SanctionNoticeRequest>(args);
            BlockedFeatureKeys = r.BlockedFeatureKeys;
            RemainingSeconds = r.RemainingSeconds;
            ReasonKey = r.ReasonKey;
            AddButton(DialogButtonRole.Ok, CommonTextKeys.Ok);
            return r.OnChosen;
        }
    }
}
