using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // taskgraph add: 계약 변경(CCR-<번호>)과 결함(FIX-<번호>) 작업을 tasks.json 에 더한다(PIPELINE 3.7절).
    //   - id 가 "CCR" 이나 "FIX" 이면 다음 번호를 붙인다. 그 밖에는 CCR-<번호> | FIX-<번호> 꼴이고 겹치지 않아야 한다.
    //     다음 번호는 이 문서와 usedMax(다른 브랜치·작업 트리에서 쓰인 최댓값. Cli 가 next-ids 와 같은 길로 읽어 넘긴다. T-65 6번)의
    //     최댓값 + 1 이다. usedMax 를 넘기지 않으면 이 문서만 본다. 이 함수는 git 을 부르지 않는다.
    //   - 검사의 병합 기록(history)도 Cli 가 읽어 넘긴다(T-65 5번). 넘기지 않으면 전부 병합 전으로 본다.
    //   - status 가 없으면 todo, vectors·contracts 가 없으면 빈 배열. 열쇠는 tasks.json 의 순서로 맞춘다.
    //   - 같은 마일스톤의 마지막 작업 뒤에 넣고, 그 마일스톤의 task_count, blocked_on_assets, parallel_groups 를 고친다.
    //   - 더한 뒤의 문서 전체를 validate 와 같은 검사에 통과시켜야 쓴다. 하나라도 틀리면 아무것도 쓰지 않는다.
    //   - 다른 작업의 줄은 바꾸지 않는다(JSON.stringify(…, null, 1) 꼴 그대로 다시 쓴다).
    public static class TaskAdder
    {
        public static readonly Regex IdPattern = new Regex("^(CCR|FIX)-([0-9]+)$", RegexOptions.CultureInvariant);

        private static readonly string[] KeyOrder =
        {
            "id", "milestone", "title", "module", "owner_agent", "owner_model", "reviewer_model",
            "deps", "spec_refs", "contracts", "owned_paths", "acceptance", "vectors", "parallel_group", "status", "human_check",
        };

        public static AddResult Prepare(string currentText, string sourceName, string inputJson, string? schemaPath, string? specsDir,
            MergeHistory? history = null, IReadOnlyDictionary<string, int>? usedMax = null)
        {
            TaskDoc doc = TaskDoc.Parse(currentText, sourceName);
            JsonObject input;
            try
            {
                input = JsonNode.Parse(inputJson) as JsonObject
                    ?? throw new TaskGraphException("더할 작업은 JSON 객체여야 한다", TaskGraphException.UsageExitCode);
            }
            catch (JsonException e)
            {
                throw new TaskGraphException($"더할 작업이 JSON 이 아니다 — {e.Message}", TaskGraphException.UsageExitCode);
            }

            string id = TaskDef.Str(input, "id") ?? throw new TaskGraphException("더할 작업에 id 가 없다(CCR, FIX, CCR-<번호>, FIX-<번호>)", TaskGraphException.UsageExitCode);
            if (id == "CCR" || id == "FIX")
            {
                id = NextId(doc, id, usedMax);
            }
            else if (!IdPattern.IsMatch(id))
            {
                throw new TaskGraphException($"add 는 CCR·FIX 작업만 더한다: id '{id}' (CCR-<번호> | FIX-<번호>)", TaskGraphException.UsageExitCode);
            }
            if (doc.ById.ContainsKey(id))
            {
                throw new TaskGraphException($"{id}: 이미 있는 id 다");
            }
            input["id"] = id;
            if (!input.ContainsKey("status"))
            {
                input["status"] = "todo";
            }
            if (!input.ContainsKey("vectors"))
            {
                input["vectors"] = new JsonArray();
            }
            if (!input.ContainsKey("contracts"))
            {
                input["contracts"] = new JsonArray();
            }
            JsonObject task = Reorder(input);

            if (doc.Root["tasks"] is not JsonArray tasks)
            {
                throw new TaskGraphException($"{sourceName}: tasks 배열이 없다");
            }
            string milestone = TaskDef.Str(task, "milestone") ?? "";
            int insertAt = tasks.Count;
            for (int i = tasks.Count - 1; i >= 0; i--)
            {
                if (tasks[i] is JsonObject o && TaskDef.Str(o, "milestone") == milestone)
                {
                    insertAt = i + 1;
                    break;
                }
            }
            tasks.Insert(insertAt, task);
            UpdateMilestone(doc.Root, milestone, task);

            TaskDoc after = TaskDoc.Parse(JsStyleJson.Serialize(doc.Root, TaskDoc.FileIndent), sourceName);
            ValidationReport report = GraphValidator.Validate(after, schemaPath, specsDir, history);
            return new AddResult(id, after.Serialize(), report);
        }

        private static string NextId(TaskDoc doc, string kind, IReadOnlyDictionary<string, int>? usedMax)
        {
            int max = usedMax != null && usedMax.TryGetValue(kind, out int used) ? Math.Max(0, used) : 0;
            foreach (TaskDef t in doc.Tasks)
            {
                Match m = IdPattern.Match(t.Id);
                if (m.Success && m.Groups[1].Value == kind && int.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n))
                {
                    max = Math.Max(max, n);
                }
            }
            return kind + "-" + (max + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static JsonObject Reorder(JsonObject input)
        {
            var result = new JsonObject();
            var rest = input.Select(kv => kv.Key).ToList();
            foreach (string k in KeyOrder)
            {
                if (input.ContainsKey(k))
                {
                    result[k] = input[k]?.DeepClone();
                    rest.Remove(k);
                }
            }
            foreach (string k in rest)
            {
                result[k] = input[k]?.DeepClone();
            }
            return result;
        }

        private static void UpdateMilestone(JsonObject root, string milestone, JsonObject task)
        {
            if (root["milestones"] is not JsonArray ms)
            {
                return;
            }
            JsonObject? m = ms.OfType<JsonObject>().FirstOrDefault(x => TaskDef.Str(x, "id") == milestone);
            if (m == null)
            {
                return; // validate 가 "milestones에 없다"로 알린다
            }
            m["task_count"] = Count(m, "task_count") + 1;
            if (TaskDef.Str(task, "status") == "blocked-on-assets")
            {
                m["blocked_on_assets"] = Count(m, "blocked_on_assets") + 1;
            }
            string group = TaskDef.Str(task, "parallel_group") ?? "";
            if (m["parallel_groups"] is JsonArray groups && !TaskDef.StrList(m, "parallel_groups").Contains(group))
            {
                InsertGroup(groups, group);
            }
        }

        // 마일스톤의 parallel_groups 에 묶음을 넣는 자리: 목록이 이름순이면 이름순 자리, 아니면 끝. recount 도 이 규칙을 쓴다
        public static void InsertGroup(JsonArray groups, string group)
        {
            var names = new List<string>();
            foreach (JsonNode? n in groups)
            {
                names.Add(n is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : "");
            }
            bool sorted = names.SequenceEqual(names.OrderBy(x => x, StringComparer.Ordinal));
            int at = groups.Count;
            if (sorted)
            {
                at = names.FindIndex(x => string.CompareOrdinal(x, group) > 0);
                if (at < 0)
                {
                    at = groups.Count;
                }
            }
            groups.Insert(at, group);
        }

        private static long Count(JsonObject m, string key)
        {
            return JsStyleJson.AsLong(m[key]) ?? 0;
        }
    }
}
