using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // 명세의 테스트 벡터 목록. tools/check-tasks.mjs 의 extractVectors 를 그대로 옮겼다.
    //   "## N. …테스트 벡터…" 아래 "### N.M" 절에서
    //   표의 첫 칸(| TV-001 |, | T-H04 |, | B01 |), "- **TV-11-017** …", "71. given …"(→ N71)
    // 정규식의 \d 는 [0-9], \b 는 JS 처럼 ASCII 단어 경계로 맞췄다(.NET 은 한글도 단어 문자로 본다).
    public static class VectorCatalog
    {
        private static readonly Regex SpecFile = new Regex("^[0-9][0-9]-.*\\.md$", RegexOptions.CultureInvariant);
        private static readonly Regex H2 = new Regex("^## ([0-9]+)\\. (.*)", RegexOptions.CultureInvariant);
        private static readonly Regex H3 = new Regex("^### ([0-9]+\\.[0-9]+)(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
        private static readonly Regex TableId = new Regex("^\\|\\s*`?([A-Z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*)`?\\s*\\|", RegexOptions.CultureInvariant);
        private static readonly Regex BoldId = new Regex("^- \\*\\*([A-Z][A-Za-z0-9-]*[0-9][A-Za-z0-9-]*)\\*\\*", RegexOptions.CultureInvariant);
        private static readonly Regex NumberedId = new Regex("^([0-9]+)\\. ", RegexOptions.CultureInvariant);
        private static readonly Regex HasDigit = new Regex("[0-9]", RegexOptions.CultureInvariant);

        public static IReadOnlyList<SpecVector> Extract(string dir)
        {
            var list = new List<SpecVector>();
            string[] files = Directory.GetFiles(dir)
                .Select(Path.GetFileName)
                .Where(f => f != null && SpecFile.IsMatch(f))
                .Select(f => f!)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();
            foreach (string f in files)
            {
                string spec = f.Substring(0, 2);
                bool inVec = false;
                string sec = "";
                foreach (string line in File.ReadAllText(Path.Combine(dir, f), Encoding.UTF8).Split('\n'))
                {
                    Match m = H2.Match(line);
                    if (m.Success)
                    {
                        inVec = m.Groups[2].Value.Contains("테스트 벡터", StringComparison.Ordinal);
                        sec = "";
                        continue;
                    }
                    if (!inVec)
                    {
                        continue;
                    }
                    m = H3.Match(line);
                    if (m.Success)
                    {
                        sec = m.Groups[1].Value;
                        continue;
                    }
                    if (sec.Length == 0)
                    {
                        continue;
                    }
                    string? id = null;
                    if ((m = TableId.Match(line)).Success && HasDigit.IsMatch(m.Groups[1].Value) && m.Groups[1].Value != "ID")
                    {
                        id = m.Groups[1].Value;
                    }
                    else if ((m = BoldId.Match(line)).Success)
                    {
                        id = m.Groups[1].Value;
                    }
                    else if ((m = NumberedId.Match(line)).Success)
                    {
                        id = "N" + m.Groups[1].Value;
                    }
                    if (id != null)
                    {
                        list.Add(new SpecVector(spec, sec, id));
                    }
                }
            }
            return list;
        }
    }
}
