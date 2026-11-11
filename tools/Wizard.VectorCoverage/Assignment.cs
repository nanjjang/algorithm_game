using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Wizard.TestKit.Shared.Vectors;

namespace Wizard.VectorCoverage
{
    // tasks.json 의 vectors 선택자를 명세 벡터로 펼친 결과(PIPELINE 7.1 G1: 모든 벡터는 정확히 한 작업에 배정된다).
    //   "S07§17.3"            명세 07 의 17.3절 벡터 전부
    //   "S13:TV-051"          벡터 하나
    //   "S13:TV-026..TV-028"  명세의 줄 순서로 TV-026 부터 TV-028 까지(같은 절 안이어야 한다)
    // 벡터 목록은 tests/vectors.config.json 규칙으로 모은 것(SpecVectorScanner)이다. 같은 꼴을 tools/check-tasks.mjs 도 읽는다.
    public sealed class Assignment
    {
        private static readonly Regex Selector = new Regex(
            "^S(?<spec>[0-9]{2})(?:§(?<sec>[0-9]+(?:\\.[0-9]+)?)|:(?<from>[A-Za-z0-9-]+)(?:\\.\\.(?<to>[A-Za-z0-9-]+))?)$",
            RegexOptions.CultureInvariant);

        private readonly Dictionary<string, string> _owner = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<SpecVector>> _byTask = new Dictionary<string, List<SpecVector>>(StringComparer.Ordinal);
        private readonly List<string> _problems = new List<string>();
        private readonly Dictionary<string, List<string>> _problemsByTask = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private Assignment()
        {
        }

        // 선택자의 꼴이 틀렸거나, 없는 벡터·절을 가리키거나, 두 작업이 같은 벡터를 가진 경우
        public IReadOnlyList<string> Problems => _problems;

        // 그 작업이 걸린 문제(자기 선택자의 문제, 다른 작업과의 이중 배정)
        public IReadOnlyList<string> ProblemsOf(string taskId) =>
            _problemsByTask.TryGetValue(taskId, out List<string>? list) ? list : (IReadOnlyList<string>)Array.Empty<string>();

        // "S01:T-H04" → 작업 id
        public string? OwnerOf(SpecVector v) => _owner.TryGetValue(v.Key, out string? id) ? id : null;

        // 작업의 벡터(선택자 순서, 중복 없음). 작업이 없거나 벡터가 없으면 빈 목록
        public IReadOnlyList<SpecVector> VectorsOf(string taskId) =>
            _byTask.TryGetValue(taskId, out List<SpecVector>? list) ? list : (IReadOnlyList<SpecVector>)Array.Empty<SpecVector>();

        public static Assignment Resolve(SpecVectorScan scan, IReadOnlyList<TaskEntry> tasks)
        {
            var a = new Assignment();
            var bySpec = new Dictionary<string, List<SpecVector>>(StringComparer.Ordinal);
            foreach (SpecVector v in scan.Vectors)
            {
                if (!bySpec.TryGetValue(v.Spec, out List<SpecVector>? list))
                {
                    list = new List<SpecVector>();
                    bySpec.Add(v.Spec, list);
                }
                list.Add(v);
            }
            foreach (TaskEntry t in tasks)
            {
                var mine = new List<SpecVector>();
                a._byTask[t.Id] = mine;
                foreach (string sel in t.Vectors)
                {
                    foreach (SpecVector v in a.Expand(t.Id, sel, bySpec))
                    {
                        a.Claim(t.Id, v, mine);
                    }
                }
            }
            return a;
        }

        private List<SpecVector> Expand(string task, string sel, Dictionary<string, List<SpecVector>> bySpec)
        {
            var hit = new List<SpecVector>();
            Match m = Selector.Match(sel);
            if (!m.Success)
            {
                Problem(new[] { task }, task + ": vectors 의 '" + sel + "' — 꼴이 틀렸다(S07§17.3 | S13:TV-051 | S13:TV-026..TV-028)");
                return hit;
            }
            string spec = m.Groups["spec"].Value;
            List<SpecVector> list = bySpec.TryGetValue(spec, out List<SpecVector>? l) ? l : new List<SpecVector>();
            if (m.Groups["sec"].Success)
            {
                string sec = m.Groups["sec"].Value;
                foreach (SpecVector v in list)
                {
                    if (string.Equals(v.Section, sec, StringComparison.Ordinal))
                    {
                        hit.Add(v);
                    }
                }
                if (hit.Count == 0)
                {
                    Problem(new[] { task }, task + ": '" + sel + "' — 명세 " + spec + " 에 벡터가 있는 " + sec + "절이 없다");
                }
                return hit;
            }
            string from = m.Groups["from"].Value;
            string to = m.Groups["to"].Success ? m.Groups["to"].Value : from;
            int i = IndexOf(list, from);
            int j = IndexOf(list, to);
            if (i < 0 || j < 0)
            {
                Problem(new[] { task }, task + ": '" + sel + "' — 명세 " + spec + " 에 벡터 " + (i < 0 ? from : to) + " 가 없다");
                return hit;
            }
            if (j < i)
            {
                Problem(new[] { task }, task + ": '" + sel + "' — 범위가 거꾸로다(명세의 줄 순서)");
                return hit;
            }
            if (!string.Equals(list[i].Section, list[j].Section, StringComparison.Ordinal))
            {
                Problem(new[] { task }, task + ": '" + sel + "' — 범위가 절을 넘는다(§" + list[i].Section + " → §" + list[j].Section + ")");
                return hit;
            }
            for (int k = i; k <= j; k++)
            {
                hit.Add(list[k]);
            }
            return hit;
        }

        private void Claim(string task, SpecVector v, List<SpecVector> mine)
        {
            if (_owner.TryGetValue(v.Key, out string? prev))
            {
                if (string.Equals(prev, task, StringComparison.Ordinal))
                {
                    Problem(new[] { task }, task + ": 벡터 " + v.Key + " 를 두 번 가리킨다");
                }
                else
                {
                    Problem(new[] { prev, task }, "벡터 " + v.Key + "(§" + v.Section + ")가 두 작업에 배정되어 있다: " + prev + ", " + task);
                }
                return;
            }
            _owner.Add(v.Key, task);
            mine.Add(v);
        }

        private void Problem(string[] taskIds, string message)
        {
            _problems.Add(message);
            foreach (string id in taskIds)
            {
                if (!_problemsByTask.TryGetValue(id, out List<string>? list))
                {
                    list = new List<string>();
                    _problemsByTask.Add(id, list);
                }
                list.Add(message);
            }
        }

        private static int IndexOf(List<SpecVector> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
