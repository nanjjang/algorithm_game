using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // OpsNotice 를 열 때 주는 값: 점검·시즌 일정의 줄(이름 표 열쇠)과 한 번에 보이는 줄 수
    public sealed class OpsNoticeRequest
    {
        public OpsNoticeRequest(IReadOnlyList<string> lineKeys, int visibleLines, Action<DialogButtonRole>? onChosen = null)
        {
            LineKeys = lineKeys ?? throw new ArgumentNullException(nameof(lineKeys));
            if (visibleLines < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleLines), visibleLines, "at least one visible line");
            }
            VisibleLines = visibleLines;
            OnChosen = onChosen;
        }

        public IReadOnlyList<string> LineKeys { get; }
        public int VisibleLines { get; }
        public Action<DialogButtonRole>? OnChosen { get; }
    }
}
