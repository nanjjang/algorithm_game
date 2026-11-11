using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // taskgraph validate. docs/dev/tasks.schema.json 의 스키마와, tools/check-tasks.mjs 가 보는 것을 같은 순서·같은 문구로 본다.
    //   1. 필수 필드, id 겹침, 배열 꼴, status(todo | blocked-on-assets), 모델 이름, 빈 acceptance·owned_paths
    //   5. owner_agent 가 agents 에 있고 owner_model 이 그 에이전트의 default_model 과 같다
    //   2. deps 가 있는 작업을 가리키고 순환이 없다(위상정렬)
    //   3. 같은 parallel_group 안의 작업은 서로 의존이 없고(전이 포함) owned_paths 가 겹치지 않는다
    //   4. owned_paths 가 겹치는 두 작업은 deps 로(전이 포함) 순서가 정해져 있다
    //   6. milestones 의 task_count, blocked_on_assets, parallel_groups 가 tasks 에서 계산한 값과 같다. gate 가 그 마일스톤의 작업이다
    //   8. acceptance 가 벡터의 부분집합을 말로 가리키지 않는다
    //   7. vectors 선택자의 문법, 명세의 모든 테스트 벡터가 정확히 한 작업에 배정된다(미배정·이중 배정 0)
    public static class GraphValidator
    {
        private static readonly HashSet<string> InitialStatus = new HashSet<string>(StringComparer.Ordinal) { "todo", "blocked-on-assets" };
        private static readonly HashSet<string> Models = new HashSet<string>(StringComparer.Ordinal) { "fable", "opus", "sonnet", "haiku" };
        private static readonly Regex Vague = new Regex("(절|TV-[A-Z0-9]+|B군|S군|A군)\\s*(가운데|중)\\s[^.]*벡터", RegexOptions.CultureInvariant);
        private static readonly Regex Selector = new Regex("^S([0-9][0-9])(?:§([0-9]+\\.[0-9]+)|:([A-Za-z0-9-]+)(?:\\.\\.([A-Za-z0-9-]+))?)$", RegexOptions.CultureInvariant);

        // specsDir 가 null 이면 벡터 배정을 보지 않는다(--no-vectors). schemaPath 가 null 이면 스키마를 보지 않는다.
        public static ValidationReport Validate(TaskDoc doc, string? schemaPath, string? specsDir)
        {
            var errors = new List<string>();
            var summary = new List<string>();

            if (schemaPath != null)
            {
                IReadOnlyList<string> schemaErrors = SchemaCheck.Evaluate(schemaPath, doc.Root);
                errors.AddRange(schemaErrors);
                summary.Add(schemaErrors.Count == 0 ? "스키마: 통과" : $"스키마: 위반 {schemaErrors.Count}건");
            }
            else
            {
                summary.Add("스키마: 건너뜀");
            }

            IReadOnlyList<TaskDef> tasks = doc.Tasks;
            if (tasks.Count == 0)
            {
                errors.Add("tasks가 비어 있다");
            }

            // ---------- 1. 필드 ----------
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TaskDef t in tasks)
            {
                foreach (string k in TaskDef.RequiredFields)
                {
                    if (!t.Node.ContainsKey(k))
                    {
                        errors.Add($"{t.Id}: 필드 '{k}'가 없다");
                    }
                }
                if (!seen.Add(t.Id))
                {
                    errors.Add($"{t.Id}: id가 겹친다");
                }
                foreach (string k in TaskDef.ArrayFields)
                {
                    if (t.Node.TryGetPropertyValue(k, out JsonNode? v) && v is not JsonArray)
                    {
                        errors.Add($"{t.Id}: '{k}'는 배열이어야 한다");
                    }
                }
                if (!InitialStatus.Contains(t.Status))
                {
                    errors.Add($"{t.Id}: status '{RawOf(t.Node, "status")}' — tasks.json에는 todo 또는 blocked-on-assets만 둔다");
                }
                if (!Models.Contains(t.OwnerModel))
                {
                    errors.Add($"{t.Id}: owner_model '{RawOf(t.Node, "owner_model")}'");
                }
                if (!Models.Contains(t.ReviewerModel))
                {
                    errors.Add($"{t.Id}: reviewer_model '{RawOf(t.Node, "reviewer_model")}'");
                }
                if (t.Node["acceptance"] is JsonArray acc && acc.Count == 0)
                {
                    errors.Add($"{t.Id}: acceptance가 비어 있다");
                }
                if (t.Node["owned_paths"] is JsonArray own && own.Count == 0)
                {
                    errors.Add($"{t.Id}: owned_paths가 비어 있다");
                }
            }

            // ---------- 5. 에이전트와 모델 ----------
            var agents = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            if (doc.Root["agents"] is JsonArray agentArr)
            {
                foreach (JsonNode? a in agentArr)
                {
                    if (a is JsonObject ao && TaskDef.Str(ao, "name") is string name)
                    {
                        agents[name] = ao;
                    }
                }
            }
            foreach (TaskDef t in tasks)
            {
                if (!agents.TryGetValue(t.OwnerAgent, out JsonObject? a))
                {
                    errors.Add($"{t.Id}: owner_agent '{t.OwnerAgent}'가 agents에 없다");
                    continue;
                }
                string? dm = TaskDef.Str(a, "default_model");
                if (dm != t.OwnerModel)
                {
                    errors.Add($"{t.Id}: owner_model '{t.OwnerModel}' ≠ {t.OwnerAgent}의 default_model '{dm}' (모델별 변형 에이전트를 쓴다)");
                }
            }

            // ---------- 2. deps 와 위상정렬 ----------
            IReadOnlyDictionary<string, TaskDef> byId = doc.ById;
            foreach (TaskDef t in tasks)
            {
                foreach (string d in t.Deps)
                {
                    if (!byId.ContainsKey(d))
                    {
                        errors.Add($"{t.Id}: deps의 '{d}'가 없다");
                    }
                    if (d == t.Id)
                    {
                        errors.Add($"{t.Id}: 자기 자신에 의존한다");
                    }
                }
            }
            DepGraph graph = DepGraph.Build(tasks, byId);
            if (!graph.Acyclic)
            {
                errors.Add($"deps에 순환이 있다: {string.Join(", ", graph.InCycle)}");
            }

            // ---------- 3·4. 경로 겹침 ----------
            int overlapPairs = 0;
            for (int i = 0; i < tasks.Count; i++)
            {
                for (int j = i + 1; j < tasks.Count; j++)
                {
                    TaskDef a = tasks[i];
                    TaskDef b = tasks[j];
                    (string, string)? hit = null;
                    foreach (string pa in a.OwnedPaths)
                    {
                        foreach (string pb in b.OwnedPaths)
                        {
                            if (PathPattern.Overlap(pa, pb))
                            {
                                hit = (pa, pb);
                                break;
                            }
                        }
                        if (hit != null)
                        {
                            break;
                        }
                    }
                    bool sameGroup = a.ParallelGroup == b.ParallelGroup;
                    if (sameGroup && graph.Acyclic && graph.Ordered(a.Id, b.Id))
                    {
                        errors.Add($"묶음 {a.ParallelGroup}: {a.Id}와 {b.Id}가 서로 의존한다(같은 묶음은 의존이 없어야 한다)");
                    }
                    if (hit == null)
                    {
                        continue;
                    }
                    overlapPairs++;
                    if (sameGroup)
                    {
                        errors.Add($"묶음 {a.ParallelGroup}: {a.Id}와 {b.Id}의 owned_paths가 겹친다 — {hit.Value.Item1} ↔ {hit.Value.Item2}");
                    }
                    else if (graph.Acyclic && !graph.Ordered(a.Id, b.Id))
                    {
                        errors.Add($"{a.Id}와 {b.Id}의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다 — {hit.Value.Item1} ↔ {hit.Value.Item2}");
                    }
                }
            }

            // ---------- 6. 마일스톤 집계 ----------
            var ms = new List<(string Id, JsonObject Node)>();
            var msIds = new HashSet<string>(StringComparer.Ordinal);
            if (doc.Root["milestones"] is JsonArray msArr)
            {
                foreach (JsonNode? m in msArr)
                {
                    if (m is JsonObject mo && TaskDef.Str(mo, "id") is string mid)
                    {
                        if (msIds.Add(mid))
                        {
                            ms.Add((mid, mo));
                        }
                    }
                }
            }
            var calc = new Dictionary<string, (int Count, int Blocked, SortedSet<string> Groups)>(StringComparer.Ordinal);
            foreach (TaskDef t in tasks)
            {
                if (!msIds.Contains(t.Milestone))
                {
                    errors.Add($"{t.Id}: milestone '{t.Milestone}'가 milestones에 없다");
                    continue;
                }
                if (!calc.TryGetValue(t.Milestone, out var c))
                {
                    c = (0, 0, new SortedSet<string>(StringComparer.Ordinal));
                }
                c.Groups.Add(t.ParallelGroup);
                calc[t.Milestone] = (c.Count + 1, c.Blocked + (t.Status == "blocked-on-assets" ? 1 : 0), c.Groups);
            }
            foreach ((string id, JsonObject m) in ms)
            {
                if (!calc.TryGetValue(id, out var c))
                {
                    c = (0, 0, new SortedSet<string>(StringComparer.Ordinal));
                }
                if (IntOf(m, "task_count") != c.Count)
                {
                    errors.Add($"milestone {id}: task_count {RawOf(m, "task_count")} ≠ 계산값 {c.Count}");
                }
                if (IntOf(m, "blocked_on_assets") != c.Blocked)
                {
                    errors.Add($"milestone {id}: blocked_on_assets {RawOf(m, "blocked_on_assets")} ≠ 계산값 {c.Blocked}");
                }
                List<string> want = c.Groups.ToList();
                List<string> have = TaskDef.StrList(m, "parallel_groups").OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (string.Join("|", want) != string.Join("|", have))
                {
                    errors.Add($"milestone {id}: parallel_groups가 tasks와 다르다 — 있어야 할 것 [{string.Join(", ", want)}], 적힌 것 [{string.Join(", ", have)}]");
                }
                string? gate = TaskDef.Str(m, "gate");
                if (!string.IsNullOrEmpty(gate))
                {
                    if (!byId.TryGetValue(gate, out TaskDef? g))
                    {
                        errors.Add($"milestone {id}: gate '{gate}' 작업이 없다");
                    }
                    else if (g.Milestone != id)
                    {
                        errors.Add($"milestone {id}: gate '{gate}'의 milestone이 다르다");
                    }
                }
            }

            // ---------- 8. 모호한 벡터 표현 ----------
            foreach (TaskDef t in tasks)
            {
                foreach (string a in t.Acceptance)
                {
                    if (Vague.IsMatch(a))
                    {
                        string head = a.Length > 50 ? a.Substring(0, 50) : a;
                        errors.Add($"{t.Id}: acceptance가 벡터의 부분집합을 말로 가리킨다(\"{head}…\"). vectors에 선택자로 적는다");
                    }
                }
            }

            // ---------- 7. 벡터 배정 ----------
            string vectorSummary = "건너뜀";
            if (specsDir != null)
            {
                if (!System.IO.Directory.Exists(specsDir))
                {
                    errors.Add($"명세 폴더가 없다: {specsDir} (--no-vectors로 건너뛸 수 있다)");
                }
                else
                {
                    vectorSummary = CheckVectors(tasks, VectorCatalog.Extract(specsDir), errors);
                }
            }

            var groups = new HashSet<string>(tasks.Select(t => t.ParallelGroup), StringComparer.Ordinal);
            summary.Add($"작업 {tasks.Count}개, 묶음 {groups.Count}개, 마일스톤 {ms.Count}개");
            summary.Add($"위상정렬: {(graph.Acyclic ? "됨" : "순환 있음")} / 경로가 겹치는 작업 쌍 {overlapPairs}개(전부 deps로 순서가 있어야 한다)");
            summary.Add($"벡터: {vectorSummary}");
            return new ValidationReport(errors, summary);
        }

        private static string CheckVectors(IReadOnlyList<TaskDef> tasks, IReadOnlyList<SpecVector> all, List<string> errors)
        {
            var bySpec = new Dictionary<string, List<SpecVector>>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpecVector v in all)
            {
                if (!seen.Add(v.Key))
                {
                    errors.Add($"명세 {v.Spec}: 벡터 id '{v.Id}'가 두 번 나온다(§{v.Section})");
                    continue;
                }
                if (!bySpec.TryGetValue(v.Spec, out List<SpecVector>? l))
                {
                    l = new List<SpecVector>();
                    bySpec[v.Spec] = l;
                }
                l.Add(v);
            }
            var owner = new Dictionary<string, string>(StringComparer.Ordinal);
            void Claim(string task, SpecVector v)
            {
                string k = v.Key;
                if (owner.TryGetValue(k, out string? prev) && prev != task)
                {
                    errors.Add($"벡터 {k}(§{v.Section})가 두 작업에 배정되어 있다: {prev}, {task}");
                }
                else if (prev != null)
                {
                    errors.Add($"{task}: 벡터 {k}를 두 번 가리킨다");
                }
                else
                {
                    owner[k] = task;
                }
            }
            foreach (TaskDef t in tasks)
            {
                foreach ((string? value, string raw) in t.Vectors)
                {
                    Match m = value != null ? Selector.Match(value) : Match.Empty;
                    if (!m.Success)
                    {
                        errors.Add($"{t.Id}: vectors의 '{raw}' — 꼴이 틀렸다(S07§17.3 | S13:TV-051 | S13:TV-026..TV-028)");
                        continue;
                    }
                    string spec = m.Groups[1].Value;
                    List<SpecVector> list = bySpec.TryGetValue(spec, out List<SpecVector>? sl) ? sl : new List<SpecVector>();
                    if (m.Groups[2].Success)
                    {
                        List<SpecVector> hit = list.Where(v => v.Section == m.Groups[2].Value).ToList();
                        if (hit.Count == 0)
                        {
                            errors.Add($"{t.Id}: '{raw}' — 명세 {spec}에 그런 벡터 절이 없다");
                            continue;
                        }
                        foreach (SpecVector v in hit)
                        {
                            Claim(t.Id, v);
                        }
                    }
                    else
                    {
                        int i = list.FindIndex(v => v.Id == m.Groups[3].Value);
                        int j = m.Groups[4].Success ? list.FindIndex(v => v.Id == m.Groups[4].Value) : i;
                        if (i < 0 || j < 0)
                        {
                            errors.Add($"{t.Id}: '{raw}' — 명세 {spec}에 그런 벡터 id가 없다");
                            continue;
                        }
                        if (j < i)
                        {
                            errors.Add($"{t.Id}: '{raw}' — 범위가 거꾸로다");
                            continue;
                        }
                        if (list[i].Section != list[j].Section)
                        {
                            errors.Add($"{t.Id}: '{raw}' — 범위가 절을 넘는다(§{list[i].Section} → §{list[j].Section})");
                            continue;
                        }
                        for (int k = i; k <= j; k++)
                        {
                            Claim(t.Id, list[k]);
                        }
                    }
                }
            }
            var missing = all.Where(v => !owner.ContainsKey(v.Key)).ToList();
            var bySec = new List<(string Key, List<string> Ids)>();
            foreach (SpecVector v in missing)
            {
                string k = $"S{v.Spec}§{v.Section}";
                int at = bySec.FindIndex(x => x.Key == k);
                if (at < 0)
                {
                    bySec.Add((k, new List<string> { v.Id }));
                }
                else
                {
                    bySec[at].Ids.Add(v.Id);
                }
            }
            foreach ((string k, List<string> ids) in bySec)
            {
                string shown = ids.Count > 12 ? string.Join(", ", ids.Take(12)) + $" … ({ids.Count}개)" : string.Join(", ", ids);
                errors.Add($"미배정 벡터 {k}: {shown}");
            }
            return $"명세 벡터 {all.Count}개, 배정 {owner.Count}개, 미배정 {missing.Count}개";
        }

        private static long? IntOf(JsonObject o, string key)
        {
            return JsStyleJson.AsLong(o[key]);
        }

        private static string RawOf(JsonObject o, string key)
        {
            if (!o.TryGetPropertyValue(key, out JsonNode? v))
            {
                return "undefined";
            }
            if (v is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
            {
                return jv.GetValue<string>();
            }
            return v?.ToJsonString() ?? "null";
        }
    }
}
