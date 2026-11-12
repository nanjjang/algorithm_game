using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // SanctionNotice 를 열 때 주는 값: 막힌 기능(이름 표 열쇠), 남은 기간(초, null 이면 기한 없음), 사유(이름 표 열쇠)
    public sealed class SanctionNoticeRequest
    {
        public SanctionNoticeRequest(IReadOnlyList<string> blockedFeatureKeys, long? remainingSeconds, string reasonKey,
            Action<DialogButtonRole>? onChosen = null)
        {
            BlockedFeatureKeys = blockedFeatureKeys ?? throw new ArgumentNullException(nameof(blockedFeatureKeys));
            if (remainingSeconds.HasValue && remainingSeconds.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingSeconds), remainingSeconds, "must not be negative");
            }
            RemainingSeconds = remainingSeconds;
            ReasonKey = reasonKey ?? throw new ArgumentNullException(nameof(reasonKey));
            OnChosen = onChosen;
        }

        public IReadOnlyList<string> BlockedFeatureKeys { get; }
        public long? RemainingSeconds { get; }
        public string ReasonKey { get; }
        public Action<DialogButtonRole>? OnChosen { get; }
    }
}
