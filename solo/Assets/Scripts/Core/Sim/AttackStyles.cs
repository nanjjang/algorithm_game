using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim
{
    /// <summary>
    /// 기본 공격 유형(캐릭터마다 셋, 핵 블록 block.core.*)의 목록·해금·판 시작 규칙. 순수 함수(상태 없음 — 콘텐츠마다 읽은 표를 붙잡아 둘 뿐).
    /// 데이터: Resources/DataSolo/AttackStyles.json. 정본 문서: Docs/12-attack-styles.md 1절.
    /// Run(판 시작의 코어 바꾸기), 원정 준비(카드 3장), 지팡이 서비스(판 설정에 프로필의 선택을 싣기)가 이 한 곳만 읽는다.
    /// </summary>
    public static class AttackStyles
    {
        public const string ExtraName = "AttackStyles";
        /// <summary>자리 글자(a = 처음부터, b, c).</summary>
        public const string SlotA = "a", SlotB = "b", SlotC = "c";

        sealed class Table
        {
            public readonly Dictionary<string, List<string>> ByCharacter = new Dictionary<string, List<string>>();
            public readonly List<string> ReplaceCores = new List<string>();
            public bool UnlockEnabled;
            public readonly Dictionary<string, int> UnlockMinBestWave = new Dictionary<string, int>();
            public bool TutorialUsesDefault;
            public bool ArtUpgradesEnabled;
            public JObject ArtUpgrades;
            public bool FinalsEnabled;
            public JObject Finals;
        }

        static readonly ConditionalWeakTable<IContentDb, Table> Cache = new ConditionalWeakTable<IContentDb, Table>();
        static readonly List<string> Empty = new List<string>();

        static Table Of(IContentDb db)
        {
            if (db == null) return null;
            Table t;
            if (Cache.TryGetValue(db, out t)) return t;
            t = Build(db.SoloExtra(ExtraName));
            Cache.Add(db, t);
            return t;
        }

        static Table Build(JObject jo)
        {
            var t = new Table();
            if (jo == null) return t;
            if (jo["characters"] is JObject chars)
                foreach (var prop in chars.Properties())
                {
                    if (prop.Name.StartsWith("_")) continue;
                    var list = new List<string>();
                    if (prop.Value is JArray arr)
                        for (int i = 0; i < arr.Count; i++)
                            if (arr[i].Type == JTokenType.String) list.Add((string)arr[i]);
                    t.ByCharacter[prop.Name] = list;
                }
            if (jo["replace_cores"] is JArray rc)
                for (int i = 0; i < rc.Count; i++)
                    if (rc[i].Type == JTokenType.String) t.ReplaceCores.Add((string)rc[i]);
            if (jo["unlock"] is JObject un)
            {
                t.UnlockEnabled = Bool(un, "enabled");
                if (un["min_best_wave"] is JObject mw)
                    foreach (var prop in mw.Properties())
                        if (prop.Value.Type == JTokenType.Integer) t.UnlockMinBestWave[prop.Name] = (int)prop.Value;
            }
            t.TutorialUsesDefault = Bool(jo, "tutorial_uses_default_style");
            if (jo["art_upgrades"] is JObject au)
            {
                t.ArtUpgrades = au;
                t.ArtUpgradesEnabled = Bool(au, "enabled");
            }
            if (jo["final_abilities"] is JObject fa)
            {
                t.Finals = fa;
                t.FinalsEnabled = Bool(fa, "enabled");
            }
            return t;
        }

        static bool Bool(JObject o, string key)
        {
            var v = o[key];
            return v != null && v.Type == JTokenType.Boolean && (bool)v;
        }

        // ------------------------------------------------------------ 목록

        /// <summary>그 캐릭터의 유형(핵 블록 id) A, B, C 순서. 표가 없으면 빈 목록.</summary>
        public static IReadOnlyList<string> StylesOf(IContentDb db, string charId)
        {
            var t = Of(db);
            List<string> list;
            if (t == null || charId == null || !t.ByCharacter.TryGetValue(charId, out list)) return Empty;
            return list;
        }

        /// <summary>그 캐릭터의 처음 유형(A). 없으면 null.</summary>
        public static string DefaultOf(IContentDb db, string charId)
        {
            var list = StylesOf(db, charId);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>그 캐릭터의 유형인가(다른 캐릭터의 유형·모르는 id면 false — 불변식: 캐릭터마다 다른 공격).</summary>
        public static bool IsStyleOf(IContentDb db, string charId, string styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return false;
            var list = StylesOf(db, charId);
            for (int i = 0; i < list.Count; i++) if (list[i] == styleId) return true;
            return false;
        }

        /// <summary>wanted가 그 캐릭터의 유형이면 그대로, 아니면 A(옛 저장·다른 캐릭터의 값 방어). 표가 없으면 null.</summary>
        public static string Resolve(IContentDb db, string charId, string wanted)
        {
            return IsStyleOf(db, charId, wanted) ? wanted : DefaultOf(db, charId);
        }

        /// <summary>그 유형의 자리 글자(a/b/c). 블록에 없으면 목록 순서로.</summary>
        public static string SlotOf(IContentDb db, string charId, string styleId)
        {
            var b = db != null && styleId != null ? db.Block(styleId) : null;
            if (b != null && b.Style != null && !string.IsNullOrEmpty(b.Style.Slot)) return b.Style.Slot;
            var list = StylesOf(db, charId);
            int i = -1;
            for (int k = 0; k < list.Count; k++) if (list[k] == styleId) { i = k; break; }
            return i == 0 ? SlotA : i == 1 ? SlotB : i == 2 ? SlotC : null;
        }

        // ------------------------------------------------------------ 해금 (손잡이: unlock.enabled, 기본 꺼짐)

        /// <summary>해금 조건을 쓰는가(꺼져 있으면 셋 다 처음부터).</summary>
        public static bool UnlockEnabled(IContentDb db) { var t = Of(db); return t != null && t.UnlockEnabled; }

        /// <summary>그 유형을 여는 "그 캐릭터로 넘긴 최고 웨이브"(0 = 처음부터). 해금이 꺼져 있으면 0.</summary>
        public static int UnlockWave(IContentDb db, string charId, string styleId)
        {
            var t = Of(db);
            if (t == null || !t.UnlockEnabled) return 0;
            string slot = SlotOf(db, charId, styleId);
            int n;
            return slot != null && t.UnlockMinBestWave.TryGetValue(slot, out n) && n > 0 ? n : 0;
        }

        /// <summary>그 캐릭터가 그 유형을 쓸 수 있나(그 캐릭터의 유형이고, 해금 조건이 꺼졌거나 최고 웨이브가 넘었다).</summary>
        public static bool IsUnlocked(IContentDb db, string charId, string styleId, int bestWave)
        {
            if (!IsStyleOf(db, charId, styleId)) return false;
            return bestWave >= UnlockWave(db, charId, styleId);
        }

        // ------------------------------------------------------------ 판 시작

        /// <summary>튜토리얼 판은 A 유형으로 도는가(손잡이 tutorial_uses_default_style).</summary>
        public static bool TutorialUsesDefault(IContentDb db) { var t = Of(db); return t != null && t.TutorialUsesDefault; }

        /// <summary>
        /// 이 판이 실제로 쓸 유형. 튜토리얼 판이고 손잡이가 켜져 있으면 그 캐릭터의 A, 아니면 cfg.AttackStyle(그 캐릭터의 유형일 때만).
        /// null = 바꾸지 않는다(출발 조립 그대로 — 명세의 마력탄).
        /// </summary>
        public static string EffectiveStyle(IContentDb db, RunConfig cfg)
        {
            if (cfg == null) return null;
            if (cfg.Mode == RunMode.Tutorial && TutorialUsesDefault(db)) return DefaultOf(db, cfg.CharId);
            return IsStyleOf(db, cfg.CharId, cfg.AttackStyle) ? cfg.AttackStyle : null;
        }

        /// <summary>코어가 유형으로 바뀌는 블록인가(replace_cores: 다섯 캐릭터 공통 시작 블록 마력탄).</summary>
        public static bool IsReplaceableCore(IContentDb db, string core)
        {
            var t = Of(db);
            return t != null && core != null && t.ReplaceCores.Contains(core);
        }

        /// <summary>
        /// 출발 조립의 코어를 유형의 핵 블록으로 바꾼 사본. 코어가 replace_cores(마력탄)일 때만 바꾼다 — 플레이어가 일부러 코어에 끼운
        /// 다른 투사체(화염구 등)는 그대로 둔다. 바꿀 것이 없으면 spec을 그대로 돌려준다.
        /// </summary>
        public static LoadoutSpec ApplyToLoadout(IContentDb db, LoadoutSpec spec, string styleId)
        {
            if (spec == null || string.IsNullOrEmpty(styleId)) return spec;
            if (db == null || db.Block(styleId) == null) return spec;
            if (!IsReplaceableCore(db, spec.Core)) return spec;
            var copy = spec.Clone();
            copy.Core = styleId;
            return copy;
        }

        // ------------------------------------------------------------ 고유 비기 강화 (손잡이: art_upgrades.enabled)

        public static bool ArtUpgradesEnabled(IContentDb db) { var t = Of(db); return t != null && t.ArtUpgradesEnabled; }

        /// <summary>그 고유 비기의 강화 수치 절(art_upgrades["art.kiln"] …). 꺼졌거나 없으면 null.</summary>
        public static JObject ArtUpgrade(IContentDb db, string artId)
        {
            var t = Of(db);
            if (t == null || !t.ArtUpgradesEnabled || t.ArtUpgrades == null || artId == null) return null;
            return t.ArtUpgrades[artId] as JObject;
        }

        // ------------------------------------------------------------ 파이널 어빌리티 (손잡이: final_abilities.enabled)

        public static bool FinalsEnabled(IContentDb db) { var t = Of(db); return t != null && t.FinalsEnabled; }

        /// <summary>final_abilities 절 전체(커맨드·간격·최소 결정). 꺼졌으면 null.</summary>
        public static JObject FinalConfig(IContentDb db)
        {
            var t = Of(db);
            return t != null && t.FinalsEnabled ? t.Finals : null;
        }

        /// <summary>파이널 어빌리티가 나가는 최소 결정 수(final_abilities.min_crystals, 없으면 3). 꺼졌으면 0.</summary>
        public static int FinalMinCrystals(IContentDb db)
        {
            var t = Of(db);
            if (t == null || !t.FinalsEnabled || t.Finals == null) return 0;
            var v = t.Finals["min_crystals"];
            return v != null && v.Type == JTokenType.Integer ? (int)v : 3;
        }

        /// <summary>그 기본 공격 유형(핵 블록 id)의 파이널 어빌리티 절(final_abilities["block.core.ember_mortar"] …). 꺼졌거나 없으면 null.</summary>
        public static JObject FinalAbility(IContentDb db, string styleId)
        {
            var t = Of(db);
            if (t == null || !t.FinalsEnabled || t.Finals == null || styleId == null) return null;
            return t.Finals[styleId] as JObject;
        }
    }
}
