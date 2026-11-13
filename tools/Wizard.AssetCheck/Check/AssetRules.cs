using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Wizard.AssetCheck.Check
{
    // tools/Wizard.AssetCheck/Rules/asset_rules.json: 반입 검사의 규칙 표(그믐 팔레트, 그리지 않기로 한 자리)
    public sealed class AssetRules
    {
        public const string RepoPath = "tools/Wizard.AssetCheck/Rules/asset_rules.json";

        private readonly HashSet<string> _notRequired;

        private AssetRules(PaletteRules palette, HashSet<string> notRequired)
        {
            Palette = palette;
            _notRequired = notRequired;
        }

        public PaletteRules Palette { get; }

        public IReadOnlyCollection<string> NotRequired => _notRequired;

        // 콘텐츠 id 규칙이 만들지만 명세가 그리지 않기로 한 자리(예: I-E2 의 무, spec 14 5.2절)
        public bool IsNotRequired(string slotId) => _notRequired.Contains(slotId);

        public static AssetRules Parse(string json)
        {
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                PaletteRules palette = PaletteRules.Parse(root.GetProperty("char5_palette"));
                var notRequired = new HashSet<string>(StringComparer.Ordinal);
                if (root.TryGetProperty("not_required", out JsonElement list))
                {
                    foreach (JsonElement e in list.EnumerateArray())
                    {
                        notRequired.Add(e.GetProperty("slot").GetString() ?? "");
                    }
                }
                return new AssetRules(palette, notRequired);
            }
        }
    }
}
