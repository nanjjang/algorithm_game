using System.Collections.Generic;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Meta
{
    /// <summary>
    /// 키 배치 표(02 요약서 3.1절, 08 요약서 12.3절 TV-171·TV-172). 값은 UnityEngine.InputSystem.Key의 정수값, 0 = 없음.
    /// 칸 번호 = LogicalKey 번호(0~8). Pause 칸은 기본·보조 둘 다 0이다 — Esc는 메뉴 길의 고정 키라 넣지 않는다(00 문서 D-11·D-60).
    /// Default = 기본 W/A/S/D/J/K/LeftShift/Space + 보조 ↑/←/↓/→/Z/X/C/V.
    /// Alternate = 기본 ↑/←/↓/→/Z/X/C/V, 보조 없음(대체 배치를 고르면 WASD·J·K가 비게 된다).
    /// 설정 저장소(F4), 전투 입력원(F5의 KeyBindings), 메뉴 입력(F3), 키 재설정 화면(G24)이 전부 이 표 하나를 읽는다(공용 계약).
    /// 표를 다른 곳에 다시 적지 않는다. Key 열거형의 번호만 쓰고 Input System의 다른 것에는 기대지 않는다.
    /// </summary>
    public static class KeyLayouts
    {
        /// <summary>논리 키 수(LogicalKey 0~8).</summary>
        public const int KeyCount = SimConst.LogicalKeyCount;

        static readonly int[] DefaultPrimaryTable =
        {
            (int)Key.W, (int)Key.A, (int)Key.S, (int)Key.D,
            (int)Key.J, (int)Key.K, (int)Key.LeftShift, (int)Key.Space, 0
        };
        static readonly int[] DefaultSecondaryTable =
        {
            (int)Key.UpArrow, (int)Key.LeftArrow, (int)Key.DownArrow, (int)Key.RightArrow,
            (int)Key.Z, (int)Key.X, (int)Key.C, (int)Key.V, 0
        };
        static readonly int[] AlternatePrimaryTable =
        {
            (int)Key.UpArrow, (int)Key.LeftArrow, (int)Key.DownArrow, (int)Key.RightArrow,
            (int)Key.Z, (int)Key.X, (int)Key.C, (int)Key.V, 0
        };
        static readonly int[] AlternateSecondaryTable = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        public static IReadOnlyList<int> DefaultPrimary => DefaultPrimaryTable;
        public static IReadOnlyList<int> DefaultSecondary => DefaultSecondaryTable;
        public static IReadOnlyList<int> AlternatePrimary => AlternatePrimaryTable;
        public static IReadOnlyList<int> AlternateSecondary => AlternateSecondaryTable;

        /// <summary>배치 이름(OptionValues.Default / Alternate)의 기본 키 줄. Custom이나 모르는 이름이면 null.</summary>
        public static IReadOnlyList<int> Primary(string layout) =>
            layout == OptionValues.Default ? DefaultPrimaryTable : layout == OptionValues.Alternate ? AlternatePrimaryTable : null;

        public static IReadOnlyList<int> Secondary(string layout) =>
            layout == OptionValues.Default ? DefaultSecondaryTable : layout == OptionValues.Alternate ? AlternateSecondaryTable : null;

        /// <summary>정해진 배치(Default / Alternate)를 설정에 넣는다(key_layout 옵션도 같이). Custom이면 아무것도 하지 않고 false.</summary>
        public static bool Apply(SettingsData s, string layout)
        {
            if (!ApplyTables(s, layout)) return false;
            SettingsOptions.Set(s, OptionKeys.KeyLayout, layout);
            return true;
        }

        /// <summary>표의 키 줄만 넣는다(key_layout 옵션은 건드리지 않는다).</summary>
        static bool ApplyTables(SettingsData s, string layout)
        {
            var p = Primary(layout);
            var q = Secondary(layout);
            if (s == null || p == null || q == null) return false;
            s.KeyPrimary = new List<int>(p);
            s.KeySecondary = new List<int>(q);
            return true;
        }

        /// <summary>
        /// 저장된 키 줄을 맞춘다: key_layout이 default·alternate면 그 표의 값으로, custom이면 길이만 9칸으로(모자라면 0).
        /// </summary>
        public static void Normalize(SettingsData s)
        {
            if (s == null) return;
            string layout = SettingsOptions.Str(s, OptionKeys.KeyLayout);
            if (ApplyTables(s, layout)) return;
            if (s.KeyPrimary == null) s.KeyPrimary = new List<int>();
            if (s.KeySecondary == null) s.KeySecondary = new List<int>();
            Fit(s.KeyPrimary);
            Fit(s.KeySecondary);
        }

        static void Fit(List<int> keys)
        {
            while (keys.Count < KeyCount) keys.Add(0);
            if (keys.Count > KeyCount) keys.RemoveRange(KeyCount, keys.Count - KeyCount);
        }

        /// <summary>그 논리 키의 (기본, 보조) 물리 키. 없으면 0.</summary>
        public static int PrimaryOf(SettingsData s, LogicalKey key) => At(s?.KeyPrimary, (int)key);
        public static int SecondaryOf(SettingsData s, LogicalKey key) => At(s?.KeySecondary, (int)key);

        static int At(List<int> keys, int i) => keys != null && i >= 0 && i < keys.Count ? keys[i] : 0;
    }
}
