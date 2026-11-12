using System;
using System.Collections.Generic;
using Wizard.Content;

namespace Wizard.DataBuild.Core
{
    // 팩 절(CONTRACTS 11.2) ↔ 원본 파일(ARCHITECTURE 2절 data/sim·data/rules). 스키마는 data/schema/<파일 이름>.schema.json.
    // 절 이름과 파일 이름은 같고 하나만 다르다: 절 31 run_limits ↔ data/rules/limits.json(ARCHITECTURE 2절 "limits(상한 식의 계수)").
    public static class SectionFiles
    {
        public const string SimDir = "sim";
        public const string RulesDir = "rules";

        // 절 id 오름차순
        public static IReadOnlyList<ContentSection> All { get; } = BuildAll();

        // 저장소의 data/ 기준 상대 경로(예: "sim/characters.json")
        public static string DataFile(ContentSection section)
        {
            string name = ContentSections.Name(section) ?? throw new ArgumentOutOfRangeException(nameof(section), "unknown section " + (ushort)section);
            string file = section == ContentSection.RunLimits ? "limits" : name;
            return (ContentSections.IsRules(section) ? RulesDir : SimDir) + "/" + file + ".json";
        }

        // data/ 기준 상대 경로 → 절. 절 목차에 없는 파일이면 false
        public static bool TryFromDataFile(string relative, out ContentSection section)
        {
            foreach (ContentSection s in All)
            {
                if (string.Equals(DataFile(s), relative, StringComparison.Ordinal))
                {
                    section = s;
                    return true;
                }
            }
            section = ContentSection.None;
            return false;
        }

        private static ContentSection[] BuildAll()
        {
            var list = new List<ContentSection>();
            foreach (ContentSection s in (ContentSection[])Enum.GetValues(typeof(ContentSection)))
            {
                if (ContentSections.IsKnown(s))
                {
                    list.Add(s);
                }
            }
            list.Sort((a, b) => ((ushort)a).CompareTo((ushort)b));
            return list.ToArray();
        }
    }
}
