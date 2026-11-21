using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 투사체 블록 → 탄 그림(effects manifest의 "묶음/열쇠") 대응(09 요약서 8절, presentation/asset_aliases.json block_projectile_fx).
    /// 그림 없는 투사체(부메랑)는 G16.json shots.block_fx_fallback(D-41: mana_bolt 대체). 투사체가 아닌 블록(변형·제어)은 탄 몸을 바꾸지 않아 null.
    /// </summary>
    public sealed class ShotSpriteTable
    {
        readonly Dictionary<string, string> _map = new Dictionary<string, string>();

        public static ShotSpriteTable Parse(string assetAliasesJson, EffectsTuning tuning)
        {
            var t = new ShotSpriteTable();
            if (!string.IsNullOrEmpty(assetAliasesJson))
            {
                var root = JObject.Parse(assetAliasesJson);
                var o = root["block_projectile_fx"] as JObject;
                if (o != null)
                    foreach (var p in o.Properties())
                        if (p.Value.Type == JTokenType.String && !p.Name.StartsWith("_", System.StringComparison.Ordinal))
                            t._map[p.Name] = p.Value.Value<string>();
            }
            if (tuning != null)
                foreach (var kv in tuning.StringMap("shots", "block_fx_fallback"))
                    if (!t._map.ContainsKey(kv.Key)) t._map[kv.Key] = kv.Value;
            return t;
        }

        /// <summary>"player/mana_bolt" 꼴. 없으면 null.</summary>
        public string PathFor(string blockId)
        {
            string s;
            return blockId != null && _map.TryGetValue(blockId, out s) ? s : null;
        }

        /// <summary>그림 묶음과 열쇠로 나눈다("player/mana_bolt" → player, mana_bolt). 실패면 false.</summary>
        public bool TryGet(string blockId, out string group, out string key)
        {
            group = key = null;
            string p = PathFor(blockId);
            if (p == null) return false;
            int slash = p.IndexOf('/');
            if (slash <= 0 || slash >= p.Length - 1) return false;
            group = p.Substring(0, slash);
            key = p.Substring(slash + 1);
            return true;
        }

        public int Count => _map.Count;
    }
}
