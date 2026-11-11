using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wizard.TaskGraph
{
    // tasks.json 의 작업 하나를 읽은 것. 틀린 꼴(배열이 아님, 문자열이 아님)도 읽어 두고 검사기가 알린다.
    public sealed class TaskDef
    {
        public static readonly string[] RequiredFields =
        {
            "id", "milestone", "title", "module", "owner_agent", "owner_model", "reviewer_model",
            "deps", "spec_refs", "contracts", "owned_paths", "acceptance", "vectors", "parallel_group", "status",
        };

        public static readonly string[] ArrayFields = { "deps", "spec_refs", "contracts", "owned_paths", "acceptance", "vectors" };

        private TaskDef(JsonObject node)
        {
            Node = node;
            Id = Str(node, "id") ?? "?";
            Milestone = Str(node, "milestone") ?? "";
            Title = Str(node, "title") ?? "";
            Module = Str(node, "module") ?? "";
            OwnerAgent = Str(node, "owner_agent") ?? "";
            OwnerModel = Str(node, "owner_model") ?? "";
            ReviewerModel = Str(node, "reviewer_model") ?? "";
            ParallelGroup = Str(node, "parallel_group") ?? "";
            Status = Str(node, "status") ?? "";
            HumanCheck = Str(node, "human_check");
            Deps = StrList(node, "deps");
            SpecRefs = StrList(node, "spec_refs");
            Contracts = StrList(node, "contracts");
            OwnedPaths = StrList(node, "owned_paths");
            Acceptance = StrList(node, "acceptance");
            Vectors = RawList(node, "vectors");
        }

        public JsonObject Node { get; }
        public string Id { get; }
        public string Milestone { get; }
        public string Title { get; }
        public string Module { get; }
        public string OwnerAgent { get; }
        public string OwnerModel { get; }
        public string ReviewerModel { get; }
        public string ParallelGroup { get; }
        public string Status { get; }
        public string? HumanCheck { get; }
        public IReadOnlyList<string> Deps { get; }
        public IReadOnlyList<string> SpecRefs { get; }
        public IReadOnlyList<string> Contracts { get; }
        public IReadOnlyList<string> OwnedPaths { get; }
        public IReadOnlyList<string> Acceptance { get; }

        // 문자열이 아닌 항목은 null 로 둔다(검사기가 "꼴이 틀렸다"로 알린다). Raw 는 알릴 때 쓰는 JSON 표기.
        public IReadOnlyList<(string? Value, string Raw)> Vectors { get; }

        public static TaskDef From(JsonObject node) => new TaskDef(node);

        public static string? Str(JsonObject node, string key)
        {
            if (node.TryGetPropertyValue(key, out JsonNode? v) && v is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
            {
                return jv.GetValue<string>();
            }
            return null;
        }

        public static IReadOnlyList<string> StrList(JsonObject node, string key)
        {
            var list = new List<string>();
            if (node.TryGetPropertyValue(key, out JsonNode? v) && v is JsonArray arr)
            {
                foreach (JsonNode? item in arr)
                {
                    if (item is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
                    {
                        list.Add(jv.GetValue<string>());
                    }
                }
            }
            return list;
        }

        private static IReadOnlyList<(string?, string)> RawList(JsonObject node, string key)
        {
            var list = new List<(string?, string)>();
            if (node.TryGetPropertyValue(key, out JsonNode? v) && v is JsonArray arr)
            {
                foreach (JsonNode? item in arr)
                {
                    if (item is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
                    {
                        string s = jv.GetValue<string>();
                        list.Add((s, s));
                    }
                    else
                    {
                        list.Add((null, item?.ToJsonString() ?? "null"));
                    }
                }
            }
            return list;
        }
    }
}
