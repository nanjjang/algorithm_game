using UnityEngine.InputSystem;
using Wizard.Solo.Meta;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 논리 키 9개 × 물리 키 둘(기본·보조)의 배치. 물리 키는 Input System의 Key(물리 자리, 한글 입력기와 무관).
    /// 설정(SettingsData.KeyPrimary/KeySecondary)에 값이 있으면 그것을, 비어 있으면(0·목록 없음) key_layout의 표를 쓴다.
    /// 표의 정본은 Core의 Meta.KeyLayouts 하나다(02 요약서 3.1절, 08 요약서 12.3절): default = 기본 W/A/S/D/J/K/Shift/Space + 보조 ↑/←/↓/→/Z/X/C/V
    /// (W와 ↑ 둘 다 위), alternate = 기본 ↑/←/↓/→/Z/X/C/V, 보조 없음. Pause는 둘 다 없음(Esc는 메뉴 길의 고정 키).
    /// Esc는 어떤 칸에 있어도 버린다(전투 입력원은 Esc를 내지 않는다 — 00 문서 4.1절, D-11).
    /// </summary>
    public sealed class KeyBindings
    {
        public readonly Key[] Primary = new Key[SimConst.LogicalKeyCount];
        public readonly Key[] Secondary = new Key[SimConst.LogicalKeyCount];

        /// <summary>default 배치의 기본 칸 키(W/A/S/D/J/K/LeftShift/Space, Pause는 None).</summary>
        public static Key DefaultKey(LogicalKey k) => (Key)KeyLayouts.DefaultPrimary[(int)k];
        /// <summary>alternate 배치의 기본 칸 키(↑/←/↓/→/Z/X/C/V, Pause는 None).</summary>
        public static Key AlternateKey(LogicalKey k) => (Key)KeyLayouts.AlternatePrimary[(int)k];

        /// <summary>default 배치(기본 칸 = W/A/S/D…, 보조 칸 = ↑/←/↓/→…).</summary>
        public static KeyBindings CreateDefault() => FromTable(OptionValues.Default);

        /// <summary>alternate 배치(기본 칸 = ↑/←/↓/→…, 보조 칸 없음).</summary>
        public static KeyBindings CreateAlternate() => FromTable(OptionValues.Alternate);

        static KeyBindings FromTable(string layout)
        {
            var b = new KeyBindings();
            var p = KeyLayouts.Primary(layout);
            var q = KeyLayouts.Secondary(layout);
            for (int i = 0; i < SimConst.LogicalKeyCount; i++) { b.Primary[i] = (Key)p[i]; b.Secondary[i] = (Key)q[i]; }
            return b;
        }

        /// <summary>설정에서 읽는다. null이면 default 배치. 칸마다 값이 0(없음)이거나 목록이 짧으면 배치 표의 값으로 채운다
        /// — 단 key_layout이 custom이면 비운 칸은 정말 비운 것으로 본다.</summary>
        public static KeyBindings FromSettings(SettingsData s)
        {
            string layout = s != null ? SettingsOptions.Str(s, OptionKeys.KeyLayout) : OptionValues.Default;
            var b = layout == OptionValues.Alternate ? CreateAlternate() : CreateDefault();
            if (s == null) return b;
            bool custom = layout == OptionValues.Custom;
            bool hasAny = HasAny(s.KeyPrimary) || HasAny(s.KeySecondary);
            if (!hasAny) return b;
            for (int i = 0; i < SimConst.LogicalKeyCount; i++)
            {
                Key p = Read(s.KeyPrimary, i), q = Read(s.KeySecondary, i);
                if (custom) { b.Primary[i] = p; b.Secondary[i] = q; }
                else
                {
                    if (p != Key.None) b.Primary[i] = p;
                    if (q != Key.None) b.Secondary[i] = q;
                }
            }
            b.StripEscape();
            return b;
        }

        static bool HasAny(System.Collections.Generic.List<int> list)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] != 0) return true;
            return false;
        }

        static Key Read(System.Collections.Generic.List<int> list, int i)
        {
            if (list == null || i >= list.Count) return Key.None;
            int v = list[i];
            if (v <= 0 || !System.Enum.IsDefined(typeof(Key), v)) return Key.None;
            return (Key)v;
        }

        void StripEscape()
        {
            for (int i = 0; i < SimConst.LogicalKeyCount; i++)
            {
                if (Primary[i] == Key.Escape) Primary[i] = Key.None;
                if (Secondary[i] == Key.Escape) Secondary[i] = Key.None;
            }
        }
    }
}
