using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Wizard.Ci
{
    // 단계에 붙는 검사 스크립트 하나(PIPELINE 4.6). 머리말(앞 HeaderLines 줄)의 주석으로 정한다.
    //   # ci-stage: fast|full|integration     어느 단계에서 도는가(필수. 없으면 단계에 붙지 않는 보조 스크립트다)
    //   # ci-timeout: <초>                     이 검사의 시간 제한(없으면 기본값)
    // 모으는 곳: tools/ci/*.sh 와 tools/ci/lib/*.sh(이 작업이 만든 기본 검사). ci.sh 자신은 빼고, 파일 이름순으로 돈다.
    // 뒤 작업은 ci.sh 를 고치지 않고 tools/ci/ 에 스크립트 파일을 더해서 검사를 늘린다.
    public sealed class StepScript
    {
        public const int HeaderLines = 12;
        public static readonly string[] Stages = { "fast", "full", "integration" };
        public static readonly string[] Folders = { "tools/ci", "tools/ci/lib" };

        private static readonly Regex StageLine = new Regex("^#\\s*ci-stage:\\s*(?<v>\\S*)\\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex TimeoutLine = new Regex("^#\\s*ci-timeout:\\s*(?<v>\\S*)\\s*$", RegexOptions.CultureInvariant);

        public StepScript(string path, string relativePath, string stage, int? timeoutSeconds)
        {
            Path = path;
            RelativePath = relativePath;
            Stage = stage;
            TimeoutSeconds = timeoutSeconds;
        }

        public string Path { get; }

        // 저장소 기준. 예: "tools/ci/build.sh"
        public string RelativePath { get; }

        // 파일 이름에서 .sh 를 뗀 것. 결과 파일 이름이다. 예: "build"
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

        public string Stage { get; }

        public int? TimeoutSeconds { get; }

        public static int StageRank(string stage) => Array.IndexOf(Stages, stage) + 1;

        // 단계의 검사 목록(이름순). 머리말의 단계 이름이 틀렸거나 같은 이름이 두 폴더에 있으면 실패를 알린다.
        public static List<StepScript> Collect(string root)
        {
            var list = new List<StepScript>();
            var problems = new List<string>();
            foreach (string folder in Folders)
            {
                string dir = RepoPaths.Combine(root, folder);
                if (!Directory.Exists(dir))
                {
                    continue;
                }
                foreach (string file in Directory.GetFiles(dir, "*.sh"))
                {
                    string rel = RepoPaths.Relative(root, file);
                    if (rel == "tools/ci/ci.sh")
                    {
                        continue;
                    }
                    StepScript? s = Read(file, rel, problems);
                    if (s != null)
                    {
                        list.Add(s);
                    }
                }
            }
            list.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(System.IO.Path.GetFileName(a.Path), System.IO.Path.GetFileName(b.Path));
                return c != 0 ? c : string.CompareOrdinal(a.RelativePath, b.RelativePath);
            });
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Name == list[i - 1].Name)
                {
                    problems.Add(list[i - 1].RelativePath + " 와 " + list[i].RelativePath + " 의 이름이 같다(결과 파일 artifacts/ci/<단계>/" + list[i].Name + ".json 이 겹친다)");
                }
            }
            if (problems.Count > 0)
            {
                throw new CiException("검사 스크립트 머리말 문제: " + string.Join("; ", problems));
            }
            return list;
        }

        public static StepScript? Read(string file, string rel, List<string> problems)
        {
            string? stage = null;
            int? timeout = null;
            int n = 0;
            foreach (string line in File.ReadLines(file, Encoding.UTF8))
            {
                if (++n > HeaderLines)
                {
                    break;
                }
                Match m = StageLine.Match(line);
                if (m.Success)
                {
                    stage = m.Groups["v"].Value;
                    if (StageRank(stage) == 0)
                    {
                        problems.Add(rel + ": '# ci-stage: " + stage + "' — 단계는 fast|full|integration 중 하나다");
                        return null;
                    }
                    continue;
                }
                m = TimeoutLine.Match(line);
                if (m.Success)
                {
                    if (!int.TryParse(m.Groups["v"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int t) || t <= 0)
                    {
                        problems.Add(rel + ": '# ci-timeout: " + m.Groups["v"].Value + "' — 양의 정수(초)여야 한다");
                        return null;
                    }
                    timeout = t;
                }
            }
            return stage == null ? null : new StepScript(file, rel, stage, timeout);
        }

        // 이름("build", "build.sh", "lib/meta-check.sh", "tools/ci/build.sh")으로 하나를 찾는다. 단계 머리말이 없는 스크립트도 찾는다.
        public static StepScript Find(string root, string name)
        {
            var candidates = new List<string>();
            string n = name.EndsWith(".sh", StringComparison.Ordinal) ? name : name + ".sh";
            if (n.StartsWith("tools/ci/", StringComparison.Ordinal))
            {
                candidates.Add(n);
            }
            else
            {
                candidates.Add("tools/ci/" + n);
                candidates.Add("tools/ci/lib/" + n);
            }
            foreach (string rel in candidates)
            {
                string file = RepoPaths.Combine(root, rel);
                if (rel != "tools/ci/ci.sh" && File.Exists(file))
                {
                    var problems = new List<string>();
                    StepScript? s = Read(file, rel, problems);
                    if (problems.Count > 0)
                    {
                        throw new CiException(string.Join("; ", problems));
                    }
                    return s ?? new StepScript(file, rel, "run", null);
                }
            }
            throw new CiException("검사 스크립트가 없다: " + name + " (tools/ci/ 와 tools/ci/lib/ 에서 찾았다)");
        }
    }
}
