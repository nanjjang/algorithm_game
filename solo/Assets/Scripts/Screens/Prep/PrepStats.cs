using System;
using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Screens.Prep
{
    /// <summary>능력치 값의 꼴(화면이 글자를 만들 때 고른다).</summary>
    public enum PrepStatUnit { Count = 0, Decimal = 1, Seconds = 2 }

    /// <summary>능력치 판 한 줄(08 요약서 5.2절: 아이콘 + 이름 + 값 + 막대).</summary>
    public struct PrepStatRow
    {
        /// <summary>str.stat.* 열쇠.</summary>
        public string LabelKey;
        /// <summary>UI I-S1 아이콘 변형(= 열쇠의 끝 마디).</summary>
        public string IconVariant;
        public float Value;
        /// <summary>막대 비율: 값 ÷ 다섯 캐릭터 중 가장 큰 값.</summary>
        public float Bar01;
        public PrepStatUnit Unit;
    }

    /// <summary>
    /// 능력치 11줄(08 요약서 5.2절 순서, 02 요약서 8.1절 라벨). 값은 characters.json 그대로(02 요약서 8.2절).
    /// 막대는 다섯 명 사이의 비교용이라 그 줄의 최댓값을 1로 본다.
    /// </summary>
    public static class PrepStats
    {
        struct Spec
        {
            public string Key;
            public PrepStatUnit Unit;
            public Func<CharacterDef, float> Get;
        }

        static readonly Spec[] Specs =
        {
            new Spec { Key = "health", Unit = PrepStatUnit.Count, Get = c => c.MaxHpCells },
            new Spec { Key = "move_speed", Unit = PrepStatUnit.Decimal, Get = c => c.WalkSpeed },
            new Spec { Key = "stance_speed", Unit = PrepStatUnit.Decimal, Get = c => c.StanceSpeed },
            new Spec { Key = "crystal_cap", Unit = PrepStatUnit.Count, Get = c => c.CrystalCap },
            new Spec { Key = "combo_per_crystal", Unit = PrepStatUnit.Count, Get = c => c.ComboPerCrystal },
            new Spec { Key = "cooling", Unit = PrepStatUnit.Decimal, Get = c => c.CoolPerSecond },
            new Spec { Key = "heat_threshold", Unit = PrepStatUnit.Decimal, Get = c => c.CritStart },
            new Spec { Key = "lock_time", Unit = PrepStatUnit.Seconds, Get = c => c.LockSeconds },
            new Spec { Key = "graze", Unit = PrepStatUnit.Decimal, Get = c => c.GrazeRadius },
            new Spec { Key = "link_slots", Unit = PrepStatUnit.Count, Get = c => c.BaseLinkSlots },
            new Spec { Key = "crystal_loss", Unit = PrepStatUnit.Count, Get = c => c.CrystalLossOnHit },
        };

        public const string LabelPrefix = "str.stat.";
        public static int RowCount => Specs.Length;

        /// <summary>그 캐릭터의 11줄. db나 정의가 없으면 빈 목록.</summary>
        public static List<PrepStatRow> Build(IContentDb db, string charId)
        {
            var rows = new List<PrepStatRow>(Specs.Length);
            var def = db != null ? db.Character(charId) : null;
            if (def == null) return rows;
            var all = db.Characters;
            for (int i = 0; i < Specs.Length; i++)
            {
                var s = Specs[i];
                float v = s.Get(def);
                float max = 0f;
                if (all != null)
                    for (int k = 0; k < all.Count; k++)
                        if (all[k] != null) max = Math.Max(max, s.Get(all[k]));
                if (max < v) max = v;
                rows.Add(new PrepStatRow
                {
                    LabelKey = LabelPrefix + s.Key,
                    IconVariant = s.Key,
                    Value = v,
                    Bar01 = max > 0f ? v / max : 0f,
                    Unit = s.Unit,
                });
            }
            return rows;
        }

        /// <summary>숫자 글자(문화권 무관, 소수 둘째 자리까지 — 3.76, 4.5, 45).</summary>
        public static string FormatNumber(float v, PrepStatUnit unit)
        {
            if (unit == PrepStatUnit.Count) return ((int)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Math.Round(v, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
