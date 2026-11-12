#nullable enable
using System.Collections.Generic;
using Wizard.Text;

namespace Wizard.Unity.UI.Common
{
    // 화면에 찍는 글자의 출처(CLAUDE.md 5절, CONTRACTS 5절: 화면 글자는 이름 표에만 있다).
    // 위젯은 이름 표 열쇠를 받고 여기서 글자로 바꾼다. 이름 표(ITextResolver)를 받지 못했으면 열쇠 문자열을 그대로 찍는다.
    public sealed class UiTextSource
    {
        public UiTextSource(ITextResolver? resolver = null)
        {
            Resolver = resolver;
        }

        public ITextResolver? Resolver { get; }

        public bool HasTable => Resolver != null;

        public string Name(string nameKey) => Resolver == null ? nameKey : Resolver.Name(nameKey);

        public string Format(string templateKey, IReadOnlyDictionary<string, string>? args) =>
            Resolver == null ? templateKey : Resolver.Format(templateKey, args);
    }
}
