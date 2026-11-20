using System.Collections.Generic;

namespace Wizard.Solo.Art
{
    /// <summary>
    /// 콘텐츠 id ↔ 그림 열쇠 대응(09 요약서 1·8절, data/presentation/asset_aliases.json과 같은 값).
    /// 그림 폴더 이름(작업용 로마자) ↔ char.N 대응은 **이 파일의 CharacterFolders 한 곳에만** 둔다(CLAUDE.md "여기서도 지키는 것").
    /// </summary>
    public static class ArtAliases
    {
        /// <summary>Resources 안 그림 뿌리(Assets/Resources/Art).</summary>
        public const string ResourceRoot = "Art/";

        /// <summary>char.1~char.5 순서의 그림 폴더 열쇠(players/·wands/·sprite-pack layers의 폴더 이름).</summary>
        static readonly string[] CharacterFolders = { "dalgu", "chorong", "galpi", "seonge", "geumeum" };

        /// <summary>"char.N" → 그림 폴더(없으면 null).</summary>
        public static string FolderOf(string charId)
        {
            int n = Ids.Char.NumberOf(charId);
            return n >= 1 && n <= CharacterFolders.Length ? CharacterFolders[n - 1] : null;
        }

        /// <summary>그림 폴더 → "char.N"(없으면 null).</summary>
        public static string CharOfFolder(string folder)
        {
            for (int i = 0; i < CharacterFolders.Length; i++)
                if (CharacterFolders[i] == folder) return Ids.Char.Of(i + 1);
            return null;
        }

        public static int CharacterCount => CharacterFolders.Length;

        /// <summary>경로 안의 "char.N" 마디를 그림 폴더 이름으로 바꾼다(보강 층 열쇠 "reactive/char.1/steam" → "reactive/dalgu/steam").</summary>
        public static string ReplaceCharSegments(string path)
        {
            if (path == null || path.IndexOf("char.", System.StringComparison.Ordinal) < 0) return path;
            var parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                var f = FolderOf(parts[i]);
                if (f != null) parts[i] = f;
            }
            return string.Join("/", parts);
        }

        /// <summary>element.* → 효과 접미사(fire, lightning, arcane, ice, dark, neutral).</summary>
        public static string ElementFxKey(string elementId)
        {
            if (elementId == null) return null;
            return elementId.StartsWith("element.", System.StringComparison.Ordinal) ? elementId.Substring("element.".Length) : elementId;
        }

        /// <summary>element.* → UI 아이콘 열쇠(ice → frost, dark → shadow, 09 1절).</summary>
        public static string ElementUiKey(string elementId)
        {
            var k = ElementFxKey(elementId);
            if (k == "ice") return "frost";
            if (k == "dark") return "shadow";
            return k;
        }

        /// <summary>tod.* → 월드 타일 시간대 변형(day, dusk, night). 보스 웨이브는 밤 팔레트(09 7.1절). null → day.</summary>
        public static string TodVariant(string todId)
        {
            switch (todId)
            {
                case Ids.Tod.Dusk: return "dusk";
                case Ids.Tod.Night: return "night";
                case Ids.Tod.Boss: return "night";
                default: return "day";
            }
        }

        /// <summary>
        /// 캐릭터 클립 이름 고르기: "&lt;clip&gt;_&lt;방향&gt;" → "&lt;clip&gt;_down" → "&lt;clip&gt;" 그대로 → "idle_&lt;방향&gt;" 순.
        /// 예 ("idle", Right) → idle_right, ("death", Up) → death_down, ("sit_down_down", Down) → sit_down_down.
        /// </summary>
        public static string ResolveCharacterClip(ICollection<string> available, string clip, Facing3 facing)
        {
            if (available == null || available.Count == 0) return null;
            string dir = FacingSuffix(facing);
            if (clip != null)
            {
                string a = clip + "_" + dir;
                if (available.Contains(a)) return a;
                string b = clip + "_down";
                if (available.Contains(b)) return b;
                if (available.Contains(clip)) return clip;
            }
            string idle = "idle_" + dir;
            if (available.Contains(idle)) return idle;
            return null;
        }

        public static string FacingSuffix(Facing3 f)
        {
            switch (f)
            {
                case Facing3.Right: return "right";
                case Facing3.Up: return "up";
                default: return "down";
            }
        }

        /// <summary>
        /// 몬스터 그림 열쇠: monsters/manifest.json에 있으면 그대로, "elite.&lt;바탕&gt;"(엘리트 id)이면 바탕 "enemy.&lt;바탕&gt;"(S14 3.7: 엘리트 = 바탕 그림 + 덧그림).
        /// </summary>
        public static string MonsterEntryKey(string monsterId, System.Func<string, bool> exists)
        {
            if (monsterId == null) return null;
            if (exists(monsterId)) return monsterId;
            if (monsterId.StartsWith("elite.", System.StringComparison.Ordinal))
            {
                string rest = monsterId.Substring("elite.".Length);
                int dot = rest.IndexOf('.');
                string baseKey = "enemy." + (dot >= 0 ? rest.Substring(0, dot) : rest);
                if (exists(baseKey)) return baseKey;
            }
            return null;
        }

        /// <summary>아이콘 대응 한 줄: 콘텐츠 id 앞부분 → UI id 후보와 변형 이름 만들기.</summary>
        public sealed class IconRule
        {
            public string Prefix;
            public string[] UiIds;
            public string VariantPrefix;
            /// <summary>id 전체가 같을 때만(접두사 규칙이 아니다).</summary>
            public bool Exact;
            /// <summary>Exact일 때의 변형 이름.</summary>
            public string ExactVariant;
        }

        /// <summary>09 요약서 8절 표(I-* 아이콘). 크기는 후보 중 sizePx에 가장 가까운 것을 고른다(ArtLibrary).</summary>
        public static readonly IconRule[] IconRules =
        {
            new IconRule { Prefix = "block.", UiIds = new[] { "I-B1", "I-B2" }, VariantPrefix = "block_" },
            new IconRule { Prefix = "art.", UiIds = new[] { "I-T1", "I-T2" }, VariantPrefix = "" },
            new IconRule { Prefix = "element.", UiIds = new[] { "I-E1", "I-E2", "I-E3" }, VariantPrefix = "element_" },
            new IconRule { Prefix = "passive.", UiIds = new[] { "I-S2" }, VariantPrefix = "" },
            new IconRule { Prefix = "wand_trait.", UiIds = new[] { "I-S3" }, VariantPrefix = "" },
            new IconRule { Prefix = "currency.", UiIds = new[] { "I-C1", "I-C2", "I-C3" }, VariantPrefix = "" },
            new IconRule { Prefix = "mat.", UiIds = new[] { "I-M1", "I-M2" }, VariantPrefix = "" },
            new IconRule { Prefix = Ids.Item.Charm, Exact = true, UiIds = new[] { "I-M3" }, ExactVariant = "safeguard_charm" },
            new IconRule { Prefix = Ids.Item.MaterialBundle, Exact = true, UiIds = new[] { "I-M4" }, ExactVariant = "material_bundle" },
            new IconRule { Prefix = Ids.Item.CrowBundle, Exact = true, UiIds = new[] { "I-M4" }, ExactVariant = "crow_bundle" },
            new IconRule { Prefix = Ids.Item.WandBox, Exact = true, UiIds = new[] { "I-M6" }, ExactVariant = "wand_box" },
            new IconRule { Prefix = Ids.Item.LibraryExpansion, Exact = true, UiIds = new[] { "I-M9" }, ExactVariant = "library_expansion" },
            new IconRule { Prefix = "emote.", UiIds = new[] { "I-F10" }, VariantPrefix = "" },
            new IconRule { Prefix = "cosmetic_cat.", UiIds = new[] { "I-M7" }, VariantPrefix = "cosmetic_" },
            new IconRule { Prefix = "cosmetic.", UiIds = new[] { "I-M7" }, VariantPrefix = "cosmetic_" },
        };

        /// <summary>콘텐츠 id → (UI id 후보, 변형). 규칙이 없으면 false.</summary>
        public static bool TryIconRule(string contentId, out string[] uiIds, out string variant)
        {
            uiIds = null;
            variant = null;
            if (contentId == null) return false;
            for (int i = 0; i < IconRules.Length; i++)
            {
                var r = IconRules[i];
                if (r.Exact)
                {
                    if (contentId != r.Prefix) continue;
                    uiIds = r.UiIds;
                    variant = r.ExactVariant;
                    return true;
                }
                if (!contentId.StartsWith(r.Prefix, System.StringComparison.Ordinal)) continue;
                string key = contentId.Substring(r.Prefix.Length);
                if (r.Prefix == "element.") key = ElementUiKey(contentId);
                if (r.Prefix == "cosmetic.")
                {
                    // cosmetic.<분류>.<낱개> → 분류 아이콘(I-M8 낱개가 없어 I-M7로 대신, 09 8절)
                    int dot = key.IndexOf('.');
                    if (dot >= 0) key = key.Substring(0, dot);
                }
                uiIds = r.UiIds;
                variant = r.VariantPrefix + key;
                return true;
            }
            return false;
        }

        /// <summary>표시하지 않는 아이콘(별빛 = 유료 상품 0개, 어떤 화면에도 내지 않는다. S14 3.11) → 대체 그림.</summary>
        public static bool IsHiddenIcon(string contentId) => contentId == "currency.starlight";

        /// <summary>변이 아이콘: native-64 mutation_icons 칸 0~5 = Ids.Mutation.All 순서(D-39). 없으면 −1.</summary>
        public static int MutationIconIndex(string mutationId)
        {
            var all = Ids.Mutation.All;
            for (int i = 0; i < all.Length; i++) if (all[i] == mutationId) return i;
            return -1;
        }

        /// <summary>Art 기준 상대 경로(확장자 포함) → Resources.Load 경로("Art/…", 확장자 뺌).</summary>
        public static string ToResourcePath(string artRelativePath)
        {
            if (artRelativePath == null) return null;
            string p = artRelativePath.Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            int dot = p.LastIndexOf('.');
            if (dot > slash) p = p.Substring(0, dot);
            return ResourceRoot + p;
        }

        /// <summary>"a/b/../c" 꼴을 정리한다(UI external 경로 "../backgrounds/…").</summary>
        public static string Normalize(string path)
        {
            if (path == null) return null;
            var parts = path.Replace('\\', '/').Split('/');
            var stack = new List<string>();
            foreach (var p in parts)
            {
                if (p.Length == 0 || p == ".") continue;
                if (p == "..") { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); continue; }
                stack.Add(p);
            }
            return string.Join("/", stack);
        }
    }
}
