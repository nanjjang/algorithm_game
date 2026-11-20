using System;
using System.Collections.Generic;
using System.Text;

namespace Wizard.Solo.Sim.Spells
{
    /// <summary>
    /// 조립을 글자로 적고 읽는 시험용 도구. 10 요약서 9절(S02 14.5)의 입력 문법:
    /// <code>
    /// loadout := "core=" id "; links=" slot ("," slot)* [ "; char=" 1..5 ] [ "; ext=" 0..3 ] [ "; mut=" mut ("," mut)* ]
    /// slot    := id | "_"        // _ = 빈 칸
    /// </code>
    /// id는 정본 id("block.magic_bolt")이고 머리("block.", "mutation.")는 줄여 적어도 된다. 화면 글자가 아니다(개발용).
    /// </summary>
    public static class SpellLoadoutText
    {
        const string BlockPrefix = "block.";
        const string MutationPrefix = "mutation.";
        const string EmptySlot = "_";

        public sealed class Parsed
        {
            public string Core;
            public readonly List<string> Links = new List<string>();
            /// <summary>1~5. 적지 않았으면 0.</summary>
            public int Character;
            /// <summary>확장 칸 수. 적지 않았으면 0.</summary>
            public int ExtraSlots;
            public readonly List<string> Mutations = new List<string>();
        }

        /// <summary>글자를 읽는다. 문법에 안 맞으면 false(result는 읽은 데까지).</summary>
        public static bool TryParse(string text, out Parsed result)
        {
            result = new Parsed();
            if (string.IsNullOrEmpty(text)) return false;
            bool sawCore = false, sawLinks = false;
            string[] parts = text.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) return false;
                string key = part.Substring(0, eq).Trim();
                string value = part.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "core":
                        result.Core = SlotId(value, BlockPrefix);
                        sawCore = true;
                        break;
                    case "links":
                        if (value.Length > 0)
                        {
                            string[] slots = value.Split(',');
                            for (int k = 0; k < slots.Length; k++) result.Links.Add(SlotId(slots[k].Trim(), BlockPrefix));
                        }
                        sawLinks = true;
                        break;
                    case "char":
                        if (!int.TryParse(value, out result.Character) || result.Character < 1 || result.Character > Ids.Char.Count) return false;
                        break;
                    case "ext":
                        if (!int.TryParse(value, out result.ExtraSlots) || result.ExtraSlots < 0) return false;
                        break;
                    case "mut":
                        string[] muts = value.Split(',');
                        for (int k = 0; k < muts.Length; k++)
                        {
                            string m = SlotId(muts[k].Trim(), MutationPrefix);
                            if (m != null) result.Mutations.Add(m);
                        }
                        break;
                    default:
                        return false;
                }
            }
            return sawCore && sawLinks;
        }

        /// <summary>컴파일에 쓴 슬롯을 입력 문법으로 적는다(정본 id, 빈 칸은 "_").</summary>
        public static string Format(CompiledSpell spell)
        {
            if (spell == null || spell.SlotBlockIds == null || spell.SlotBlockIds.Length == 0) return string.Empty;
            var links = new string[spell.SlotBlockIds.Length - 1];
            Array.Copy(spell.SlotBlockIds, 1, links, 0, links.Length);
            return Format(spell.SlotBlockIds[0], links);
        }

        public static string Format(string core, IReadOnlyList<string> links)
        {
            var sb = new StringBuilder();
            sb.Append("core=").Append(string.IsNullOrEmpty(core) ? EmptySlot : core).Append("; links=");
            int n = links != null ? links.Count : 0;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(string.IsNullOrEmpty(links[i]) ? EmptySlot : links[i]);
            }
            return sb.ToString();
        }

        static string SlotId(string token, string prefix)
        {
            if (string.IsNullOrEmpty(token) || token == EmptySlot) return null;
            return token.StartsWith(prefix, StringComparison.Ordinal) ? token : prefix + token;
        }
    }
}
