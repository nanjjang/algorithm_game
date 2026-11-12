using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // Tooltip 을 열 때 주는 값(spec 13 1.5: 물건 이름(등급색), 종류와 속성, 수치, 한 줄 설명, 거래 가능 여부).
    // 글은 전부 이름 표 열쇠, 등급색은 색 토큰. Tradable 이 null 이면 거래 줄을 보이지 않는다(물건이 아닌 툴팁)
    public sealed class TooltipRequest
    {
        private static readonly string[] NoLines = new string[0];

        public TooltipRequest(string nameKey, string? gradeColorToken = null, string? kindKey = null, string? elementKey = null,
            IReadOnlyList<string>? valueLineKeys = null, string? descriptionKey = null, bool? tradable = null)
        {
            NameKey = nameKey ?? throw new ArgumentNullException(nameof(nameKey));
            GradeColorToken = gradeColorToken;
            KindKey = kindKey;
            ElementKey = elementKey;
            ValueLineKeys = valueLineKeys ?? NoLines;
            DescriptionKey = descriptionKey;
            Tradable = tradable;
        }

        public string NameKey { get; }
        public string? GradeColorToken { get; }
        public string? KindKey { get; }
        public string? ElementKey { get; }
        public IReadOnlyList<string> ValueLineKeys { get; }
        public string? DescriptionKey { get; }
        public bool? Tradable { get; }
    }
}
