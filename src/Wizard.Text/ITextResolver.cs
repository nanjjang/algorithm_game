using System.Collections.Generic;

namespace Wizard.Text
{
    // CONTRACTS 5. 화면에 찍는 모든 문자열은 이것을 거친다. 이름 표(NameTable)와 TextMetrics 는 M6-TXT-01 이 만든다
    public interface ITextResolver
    {
        string Name(string nameKey);                                     // 없으면 열쇠 그대로
        string Format(string templateKey, IReadOnlyDictionary<string, string>? args = null);   // {char.5}{이/가} …
        string ResolveTemplate(string template, IReadOnlyDictionary<string, string>? args);
    }
}
