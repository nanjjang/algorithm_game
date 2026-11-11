using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Wizard.TestKit.Shared.Vectors
{
    // 명세 마크다운에서 테스트 벡터 id 를 모은다. 규칙은 전부 VectorConfig(tests/vectors.config.json)에 있다.
    public static class SpecVectorScanner
    {
        // 범위 칸(S06~S10)을 펼칠 때의 상한. 넘으면 오타로 본다
        private const int MaxRange = 100;

        public static SpecVectorScan ScanRepo(string repoRoot, VectorConfig config)
        {
            return ScanDirectory(RepoRoot.Resolve(repoRoot, config.SpecDir), config);
        }

        public static SpecVectorScan ScanDirectory(string specDir, VectorConfig config)
        {
            var files = new List<string>();
            foreach (string path in Directory.GetFiles(specDir))
            {
                string name = Path.GetFileName(path);
                if (config.SpecFile.IsMatch(name))
                {
                    files.Add(name);
                }
            }
            files.Sort(StringComparer.Ordinal);
            var inputs = new List<KeyValuePair<string, string>>();
            foreach (string name in files)
            {
                inputs.Add(new KeyValuePair<string, string>(name, File.ReadAllText(Path.Combine(specDir, name), Encoding.UTF8)));
            }
            return Scan(inputs, config);
        }

        // (파일 이름, 내용) 목록을 훑는다. 파일 이름에서 명세 번호를 얻는다.
        public static SpecVectorScan Scan(IReadOnlyList<KeyValuePair<string, string>> files, VectorConfig config)
        {
            var vectors = new List<SpecVector>();
            var problems = new List<string>();
            var seenSpecs = new List<string>();
            foreach (KeyValuePair<string, string> file in files)
            {
                Match fm = config.SpecFile.Match(file.Key);
                if (!fm.Success)
                {
                    problems.Add(file.Key + ": file name does not match specFile");
                    continue;
                }
                string spec = fm.Groups["spec"].Value;
                if (seenSpecs.Contains(spec))
                {
                    problems.Add(file.Key + ": spec " + spec + " has two files");
                    continue;
                }
                seenSpecs.Add(spec);
                Regex? idPattern = config.IdPatternFor(spec);
                if (idPattern == null)
                {
                    problems.Add(file.Key + ": spec " + spec + " has no id pattern in vectors.config.json");
                    continue;
                }
                int before = vectors.Count;
                ScanFile(file.Key, spec, file.Value, idPattern, config, vectors, problems);
                if (vectors.Count == before)
                {
                    problems.Add(file.Key + ": no test vectors found");
                }
            }
            foreach (string spec in config.Specs)
            {
                if (!seenSpecs.Contains(spec))
                {
                    problems.Add("spec " + spec + " is configured but no spec file was found");
                }
            }
            return new SpecVectorScan(vectors, problems);
        }

        private static void ScanFile(string file, string spec, string text, Regex idPattern, VectorConfig config,
                                     List<SpecVector> vectors, List<string> problems)
        {
            bool inVectors = false;
            string? chapterNo = null;
            string? section = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var defined = new List<SpecVector>();
            // 본문 괄호 속 id: 표·항목을 다 본 뒤에 정의인지 참조인지 가린다
            var prose = new List<ProseCandidate>();
            // 벡터 장에 나온 id 꼴 낱말: (낱말, 자리)
            var tokens = new List<KeyValuePair<string, string>>();
            // 벡터 장 밖에 나온 id 꼴 낱말: 참조라면 어딘가에 정의되어 있어야 한다
            var outsideTokens = new List<KeyValuePair<string, string>>();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNo = i + 1;
                string where = file + ":" + lineNo.ToString(CultureInfo.InvariantCulture);
                Match cm = config.Chapter.Match(line);
                if (cm.Success)
                {
                    inVectors = config.VectorChapter.IsMatch(line);
                    chapterNo = cm.Groups["ch"].Success ? cm.Groups["ch"].Value : null;
                    section = null;
                    continue;
                }
                if (!inVectors)
                {
                    // 벡터 장 밖: 굵은 항목이나 표 행이 이 명세의 id 꼴로 시작하면 그것도 벡터 정의다(spec 11 16절·17.5절의
                    // TV-11-162~166). 꼴이 다른 굵은 글씨·표, 번호 항목, 본문 괄호는 보통 글이다
                    foreach (Match tm in config.IdToken.Matches(line))
                    {
                        if (idPattern.IsMatch(tm.Value))
                        {
                            outsideTokens.Add(new KeyValuePair<string, string>(tm.Value, where));
                        }
                    }
                    Match osm = config.Subsection.Match(line);
                    if (osm.Success)
                    {
                        section = osm.Groups["sec"].Value;
                        continue;
                    }
                    var outside = new List<string>();
                    bool outsideProvisional = false;
                    Match om;
                    if ((om = config.TableRow.Match(line)).Success)
                    {
                        string cell = om.Groups["cell"].Value.Trim();
                        Match idm = config.TableId.Match(cell);
                        if (!idm.Success || !idPattern.IsMatch(idm.Groups["id"].Value))
                        {
                            continue;
                        }
                        outsideProvisional = idm.Groups["star"].Success;
                        if (!AddRange(idm.Groups["id"].Value, idm.Groups["to"], cell, where, outside, problems))
                        {
                            continue;
                        }
                    }
                    else if ((om = config.BoldItem.Match(line)).Success && idPattern.IsMatch(om.Groups["id"].Value.Trim()))
                    {
                        outside.Add(om.Groups["id"].Value.Trim());
                    }
                    else
                    {
                        continue;
                    }
                    // ### 소절이 없는 장(spec 11 16절)에서는 장 번호가 절이다
                    string? outsideSection = section ?? chapterNo;
                    if (outsideSection == null)
                    {
                        problems.Add(where + ": vector '" + string.Join(",", outside) + "' is outside a numbered chapter");
                        continue;
                    }
                    Define(outside, spec, outsideSection, outsideProvisional, false, file, lineNo, where, idPattern, ids, defined, problems);
                    continue;
                }
                foreach (Match tm in config.IdToken.Matches(line))
                {
                    if (idPattern.IsMatch(tm.Value))
                    {
                        tokens.Add(new KeyValuePair<string, string>(tm.Value, where));
                    }
                }
                Match sm = config.Subsection.Match(line);
                if (sm.Success)
                {
                    section = sm.Groups["sec"].Value;
                    continue;
                }

                var found = new List<string>();
                bool provisional = false;
                Match m;
                if ((m = config.TableRow.Match(line)).Success)
                {
                    string cell = m.Groups["cell"].Value.Trim();
                    if (config.TableIgnore.IsMatch(cell))
                    {
                        continue;
                    }
                    Match idm = config.TableId.Match(cell);
                    if (!idm.Success)
                    {
                        problems.Add(where + ": unrecognized table row in a vector section (first cell '" + cell + "')");
                        continue;
                    }
                    provisional = idm.Groups["star"].Success;
                    if (!AddRange(idm.Groups["id"].Value, idm.Groups["to"], cell, where, found, problems))
                    {
                        continue;
                    }
                }
                else if ((m = config.BoldItem.Match(line)).Success)
                {
                    found.Add(m.Groups["id"].Value.Trim());
                }
                else if ((m = config.NumberedItem.Match(line)).Success)
                {
                    found.Add(config.NumberedPrefix + m.Groups["num"].Value);
                }
                else
                {
                    // 본문 줄: 괄호 속에서 이 명세의 id 꼴인 것만 후보로 둔다. 꼴이 다른 괄호("(O2.6)", "(k=1.74)")는 벡터가 아니다
                    foreach (Match pm in config.ProseItem.Matches(line))
                    {
                        string from = pm.Groups["id"].Value;
                        if (!idPattern.IsMatch(from))
                        {
                            continue;
                        }
                        var range = new List<string>();
                        if (!AddRange(from, pm.Groups["to"], pm.Value, where, range, problems))
                        {
                            continue;
                        }
                        foreach (string id in range)
                        {
                            prose.Add(new ProseCandidate(id, section, lineNo, where));
                        }
                    }
                    continue;
                }

                if (section == null)
                {
                    problems.Add(where + ": vector '" + string.Join(",", found) + "' is outside a ### subsection");
                    continue;
                }
                Define(found, spec, section, provisional, true, file, lineNo, where, idPattern, ids, defined, problems);
            }

            // 표·항목이 정의하지 않은 본문 id 는 처음 나온 줄에서 정의된다. 나머지는 참조다
            foreach (ProseCandidate p in prose)
            {
                if (ids.Contains(p.Id))
                {
                    continue;
                }
                if (p.Section == null)
                {
                    problems.Add(p.Where + ": vector '" + p.Id + "' is outside a ### subsection");
                    continue;
                }
                ids.Add(p.Id);
                defined.Add(new SpecVector(spec, p.Section, p.Id, false, file, p.Line));
            }
            foreach (SpecVector v in defined)
            {
                if (v.Id.IndexOf('_') >= 0)
                {
                    problems.Add(v.File + ":" + v.Line.ToString(CultureInfo.InvariantCulture) + ": id '" + v.Id +
                                 "' contains '_' (test names map '-' to '_')");
                }
            }
            // 벡터 장 안의 id 꼴 낱말은 모두 이 명세의 벡터로 정의되어 있어야 한다. 아니면 규칙이 놓친 벡터이거나 잘못 적힌 참조다
            foreach (KeyValuePair<string, string> t in tokens)
            {
                if (!ids.Contains(t.Key))
                {
                    problems.Add(t.Value + ": id '" + t.Key + "' appears in the vector chapter but no rule defines it as a vector of spec " + spec);
                }
            }
            // 벡터 장 밖의 id 꼴 낱말도 정의된 벡터를 가리켜야 한다. 아니면 규칙이 놓친 정의(번호 항목·본문 꼴)이거나 없는 벡터를 가리키는 참조다
            foreach (KeyValuePair<string, string> t in outsideTokens)
            {
                if (!ids.Contains(t.Key))
                {
                    problems.Add(t.Value + ": id '" + t.Key + "' appears outside the vector chapter but no rule defines it as a vector of spec " + spec);
                }
            }
            defined.RemoveAll(v => v.Id.IndexOf('_') >= 0);
            vectors.AddRange(defined.OrderBy(v => v.Line));
        }

        // 표·항목 한 줄이 정의한 id 들을 검사하고 더한다
        private static void Define(List<string> found, string spec, string section, bool provisional, bool inVectorChapter,
                                   string file, int lineNo, string where, Regex idPattern, HashSet<string> ids,
                                   List<SpecVector> defined, List<string> problems)
        {
            foreach (string id in found)
            {
                if (!idPattern.IsMatch(id))
                {
                    problems.Add(where + ": id '" + id + "' does not match the spec " + spec + " pattern " + idPattern);
                    continue;
                }
                if (!ids.Add(id))
                {
                    problems.Add(where + ": id '" + id + "' appears twice in spec " + spec);
                    continue;
                }
                defined.Add(new SpecVector(spec, section, id, provisional, file, lineNo, inVectorChapter));
            }
        }

        private sealed class ProseCandidate
        {
            public ProseCandidate(string id, string? section, int line, string where)
            {
                Id = id;
                Section = section;
                Line = line;
                Where = where;
            }

            public string Id { get; }
            public string? Section { get; }
            public int Line { get; }
            public string Where { get; }
        }

        // from 하나, 또는 from~to 범위를 펼쳐 output 에 더한다. to 가 숫자뿐이면 from 의 끝 숫자만 바꾼 것이다("TV-E-09~14").
        private static bool AddRange(string from, Group to, string text, string where, List<string> output, List<string> problems)
        {
            if (!to.Success)
            {
                output.Add(from);
                return true;
            }
            string last = to.Value;
            if (IsDigits(last) && SplitTail(from, out string prefix, out _))
            {
                last = prefix + last;
            }
            if (!Expand(from, last, output))
            {
                problems.Add(where + ": range '" + text + "' cannot be expanded (same prefix and digit width, ascending, at most " +
                             MaxRange.ToString(CultureInfo.InvariantCulture) + ")");
                return false;
            }
            return true;
        }

        private static bool IsDigits(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }
            foreach (char c in s)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return true;
        }

        // "S06" ~ "S10" → S06, S07, S08, S09, S10. 앞머리가 같고 끝 숫자의 자릿수가 같아야 한다.
        private static bool Expand(string from, string to, List<string> output)
        {
            if (!SplitTail(from, out string p1, out string d1) || !SplitTail(to, out string p2, out string d2))
            {
                return false;
            }
            if (!string.Equals(p1, p2, StringComparison.Ordinal) || d1.Length != d2.Length)
            {
                return false;
            }
            int a = int.Parse(d1, NumberStyles.None, CultureInfo.InvariantCulture);
            int b = int.Parse(d2, NumberStyles.None, CultureInfo.InvariantCulture);
            if (b <= a || b - a + 1 > MaxRange)
            {
                return false;
            }
            string fmt = "D" + d1.Length.ToString(CultureInfo.InvariantCulture);
            for (int n = a; n <= b; n++)
            {
                output.Add(p1 + n.ToString(fmt, CultureInfo.InvariantCulture));
            }
            return true;
        }

        private static bool SplitTail(string id, out string prefix, out string digits)
        {
            int end = id.Length;
            int start = end;
            while (start > 0 && id[start - 1] >= '0' && id[start - 1] <= '9')
            {
                start--;
            }
            prefix = id.Substring(0, start);
            digits = id.Substring(start);
            return digits.Length > 0 && digits.Length <= 6;
        }
    }
}
