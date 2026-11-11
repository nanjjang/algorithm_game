using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Wizard.TaskGraph.Tests
{
    // 테스트용 작업 그래프를 만든다. 마일스톤 집계는 작업에서 계산해 넣는다(일부러 틀리게 하는 테스트는 따로 고친다).
    internal static class Fx
    {
        public static readonly string RepoRoot = RepoLayout.FindRootFrom(AppContext.BaseDirectory);
        public static readonly string SchemaPath = Path.Combine(RepoRoot, "docs", "dev", "tasks.schema.json");
        public static readonly string RealTasksPath = Path.Combine(RepoRoot, "docs", "dev", "tasks.json");
        public static readonly string RealSpecsDir = Path.Combine(RepoRoot, "docs", "spec");

        public static JsonObject Task(
            string id,
            string milestone = "M0",
            string group = "M0-W01",
            string[]? deps = null,
            string[]? paths = null,
            string status = "todo",
            string[]? vectors = null,
            string? acceptance = null,
            string agent = "build-tools-engineer",
            string model = "opus",
            string? humanCheck = null)
        {
            var t = new JsonObject
            {
                ["id"] = id,
                ["milestone"] = milestone,
                ["title"] = "작업 " + id,
                ["module"] = "build",
                ["owner_agent"] = agent,
                ["owner_model"] = model,
                ["reviewer_model"] = "opus",
                ["deps"] = Arr(deps ?? Array.Empty<string>()),
                ["spec_refs"] = Arr(new[] { "docs/dev/PIPELINE.md §3" }),
                ["contracts"] = Arr(Array.Empty<string>()),
                ["owned_paths"] = Arr(paths ?? new[] { "tools/" + id + "/" }),
                ["acceptance"] = Arr(new[] { acceptance ?? id + " 의 수락 조건" }),
                ["vectors"] = Arr(vectors ?? Array.Empty<string>()),
                ["parallel_group"] = group,
                ["status"] = status,
            };
            if (humanCheck != null)
            {
                t["human_check"] = humanCheck;
            }
            return t;
        }

        public static JsonArray Arr(IEnumerable<string> items)
        {
            var a = new JsonArray();
            foreach (string s in items)
            {
                a.Add(s);
            }
            return a;
        }

        public static JsonObject Doc(params JsonObject[] tasks)
        {
            var milestones = new JsonArray();
            foreach (var g in tasks.GroupBy(t => (string)t["milestone"]!))
            {
                milestones.Add(new JsonObject
                {
                    ["id"] = g.Key,
                    ["title"] = "마일스톤 " + g.Key,
                    ["gate"] = null,
                    ["task_count"] = g.Count(),
                    ["blocked_on_assets"] = g.Count(t => (string)t["status"]! == "blocked-on-assets"),
                    ["parallel_groups"] = Arr(g.Select(t => (string)t["parallel_group"]!).Distinct().OrderBy(x => x, StringComparer.Ordinal)),
                });
            }
            var taskArr = new JsonArray();
            foreach (JsonObject t in tasks)
            {
                taskArr.Add(t);
            }
            return new JsonObject
            {
                ["schema"] = 2,
                ["project"] = "테스트",
                ["notes"] = Arr(new[] { "테스트용 그래프" }),
                ["agents"] = new JsonArray
                {
                    new JsonObject { ["name"] = "build-tools-engineer", ["default_model"] = "opus", ["default_reviewer_model"] = "opus" },
                    new JsonObject { ["name"] = "content-data-engineer", ["default_model"] = "sonnet", ["default_reviewer_model"] = "opus" },
                },
                ["milestones"] = milestones,
                ["tasks"] = taskArr,
            };
        }

        public static string Text(JsonObject doc) => JsStyleJson.Serialize(doc, TaskDoc.FileIndent);

        public static TaskDoc Parse(JsonObject doc) => TaskDoc.Parse(Text(doc), "fixture");

        public static ValidationReport Validate(JsonObject doc, string? specsDir = null)
        {
            return GraphValidator.Validate(Parse(doc), SchemaPath, specsDir);
        }

        public static RepoLayout LayoutIn(string dir, string tasksPath)
        {
            return new RepoLayout(dir, dir, tasksPath, Path.Combine(dir, "docs", "spec"), SchemaPath, Path.Combine(dir, ".claude", "taskstate.json"));
        }
    }
}
