using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // ConfirmDialog 를 열 때 ScreenArgs.Payload 로 준다(spec 13 1.5: 물음 한 줄, 설명 두 줄, [예] [아니오]).
    // Caution 이면 [예]가 조심 버튼(호박색 테 + 경고 삼각)이다
    public sealed class ConfirmRequest
    {
        public const int MaxDetailLines = 2;

        private static readonly string[] NoDetails = new string[0];

        public ConfirmRequest(string questionKey, IReadOnlyList<string>? detailKeys = null, bool caution = false,
            Action<DialogButtonRole>? onChosen = null)
        {
            QuestionKey = questionKey ?? throw new ArgumentNullException(nameof(questionKey));
            DetailKeys = detailKeys ?? NoDetails;
            if (DetailKeys.Count > MaxDetailLines)
            {
                throw new ArgumentException("at most " + MaxDetailLines + " detail lines", nameof(detailKeys));
            }
            Caution = caution;
            OnChosen = onChosen;
        }

        public string QuestionKey { get; }
        public IReadOnlyList<string> DetailKeys { get; }
        public bool Caution { get; }
        public Action<DialogButtonRole>? OnChosen { get; }
    }
}
