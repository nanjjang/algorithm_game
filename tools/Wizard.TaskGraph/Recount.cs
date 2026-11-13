using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wizard.TaskGraph
{
    // taskgraph recount (DECISIONS-TECH T-65 10번). 마일스톤마다 task_count, blocked_on_assets, parallel_groups 를 tasks 에서 다시 센다.
    //   - parallel_groups 는 있던 순서를 지킨다. 작업이 없는 묶음은 빼고, 빠진 묶음은 tasks 에 처음 나온 순서로
    //     taskgraph add 와 같은 자리 규칙(TaskAdder.InsertGroup)으로 넣는다.
    //   - 다른 값은 건드리지 않는다. 같은 값이면 노드도 그대로 둔다.
    // 두 브랜치가 같은 마일스톤에 카드를 하나씩 더하면 둘 다 task_count 를 14 → 15 로 고쳐 git 이 충돌 없이 15 로 합친다(맞는 값 16).
    public static class Recount
    {
        // root 를 제자리에서 고치고 바뀐 것을 한 줄씩 돌려준다(없으면 빈 목록)
        public static IReadOnlyList<string> Apply(JsonObject root)
        {
            var changes = new List<string>();
            if (root["milestones"] is not JsonArray ms)
            {
                return changes;
            }
            var tasks = new List<JsonObject>();
            if (root["tasks"] is JsonArray ta)
            {
                tasks.AddRange(ta.OfType<JsonObject>());
            }
            foreach (JsonObject m in ms.OfType<JsonObject>())
            {
                string? id = TaskDef.Str(m, "id");
                if (id == null)
                {
                    continue;
                }
                List<JsonObject> mine = tasks.Where(t => TaskDef.Str(t, "milestone") == id).ToList();
                SetCount(m, id, "task_count", mine.Count, changes);
                SetCount(m, id, "blocked_on_assets", mine.Count(t => TaskDef.Str(t, "status") == "blocked-on-assets"), changes);

                if (m["parallel_groups"] is JsonArray groups)
                {
                    var want = new List<string>();
                    foreach (JsonObject t in mine)
                    {
                        string g = TaskDef.Str(t, "parallel_group") ?? "";
                        if (!want.Contains(g))
                        {
                            want.Add(g);
                        }
                    }
                    List<string> before = Names(groups);
                    var next = new JsonArray();
                    foreach (string g in before)
                    {
                        if (want.Contains(g) && !Names(next).Contains(g))
                        {
                            next.Add(g);
                        }
                    }
                    foreach (string g in want)
                    {
                        if (!Names(next).Contains(g))
                        {
                            TaskAdder.InsertGroup(next, g);
                        }
                    }
                    List<string> after = Names(next);
                    bool sameShape = groups.Count == after.Count && groups.All(n => n is JsonValue v && v.GetValueKind() == JsonValueKind.String);
                    if (!sameShape || !before.SequenceEqual(after))
                    {
                        m["parallel_groups"] = next;
                        changes.Add($"milestone {id}: parallel_groups [{string.Join(", ", before)}] → [{string.Join(", ", after)}]");
                    }
                }
            }
            return changes;
        }

        private static void SetCount(JsonObject m, string id, string key, int want, List<string> changes)
        {
            long? have = JsStyleJson.AsLong(m[key]);
            if (have == want)
            {
                return;
            }
            string shown = m.TryGetPropertyValue(key, out JsonNode? v) ? (v?.ToJsonString() ?? "null") : "없음";
            m[key] = want;
            changes.Add($"milestone {id}: {key} {shown} → {want}");
        }

        private static List<string> Names(JsonArray a)
        {
            var list = new List<string>();
            foreach (JsonNode? n in a)
            {
                list.Add(n is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : n?.ToJsonString() ?? "null");
            }
            return list;
        }
    }
}
