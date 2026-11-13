using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Assets
{
    // 콘텐츠 id → UI 아이콘 변형의 규칙 한 줄(spec 14 5.2절 "콘텐츠 id → UI 아이콘 변형 대응" [제안]).
    // 예: block.fireball → I-B1·I-B2 의 block_fireball. 열쇠 바꿈(element.ice → frost)은 KeyOverrides 에 있다.
    // 규칙은 data/presentation/asset_aliases.json 의 ui_icons 에서 온다
    public sealed class UiIconRule
    {
        public UiIconRule(string contentPrefix, IReadOnlyList<string> icons, string variantPrefix, IReadOnlyDictionary<string, string> keyOverrides)
        {
            ContentPrefix = contentPrefix;
            Icons = icons;
            VariantPrefix = variantPrefix;
            KeyOverrides = keyOverrides;
        }

        // "block." — 이 접두어로 시작하는 콘텐츠 id 에 쓴다
        public string ContentPrefix { get; }

        // 크기별 아이콘 ID(큰 것부터 매니페스트 표 순서)
        public IReadOnlyList<string> Icons { get; }

        public string VariantPrefix { get; }

        // 콘텐츠 열쇠 → 그림 열쇠(element: ice → frost, dark → shadow)
        public IReadOnlyDictionary<string, string> KeyOverrides { get; }

        public bool Matches(string contentId) =>
            contentId.Length > ContentPrefix.Length && contentId.StartsWith(ContentPrefix, StringComparison.Ordinal);

        // block.fireball → block_fireball
        public string VariantFor(string contentId)
        {
            string key = contentId.Substring(ContentPrefix.Length);
            if (KeyOverrides.TryGetValue(key, out string? mapped))
            {
                key = mapped;
            }
            return VariantPrefix + key;
        }
    }
}
