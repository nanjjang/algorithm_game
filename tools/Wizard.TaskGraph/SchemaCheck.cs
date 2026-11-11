using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Wizard.TaskGraph
{
    // docs/dev/tasks.schema.json(JSON Schema 2020-12)으로 tasks.json 을 본다. 위반은 "스키마: <위치> — <이유>" 한 줄씩,
    // 경로 오름차순으로 낸다(같은 입력이면 같은 출력). 맞지 않는 노드 아래로만 내려간다(통과한 oneOf 의 다른 갈래는 알리지 않는다).
    public static class SchemaCheck
    {
        public static IReadOnlyList<string> Evaluate(string schemaPath, JsonNode root)
        {
            if (!File.Exists(schemaPath))
            {
                return new[] { $"스키마 파일이 없다: {schemaPath}" };
            }
            JsonSchema schema;
            try
            {
                schema = JsonSchema.FromText(File.ReadAllText(schemaPath, Encoding.UTF8));
            }
            catch (Exception e) when (e is System.Text.Json.JsonException || e is JsonSchemaException || e is ArgumentException)
            {
                return new[] { $"스키마 파일을 읽지 못했다: {schemaPath} — {e.Message}" };
            }
            EvaluationResults result = schema.Evaluate(root, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
            if (result.IsValid)
            {
                return Array.Empty<string>();
            }
            var lines = new SortedSet<string>(StringComparer.Ordinal);
            Collect(result, lines);
            if (lines.Count == 0)
            {
                lines.Add("스키마: / — 맞지 않는다");
            }
            return lines.ToList();
        }

        private static void Collect(EvaluationResults r, SortedSet<string> lines)
        {
            if (r.IsValid)
            {
                return;
            }
            if (r.Errors != null)
            {
                foreach (KeyValuePair<string, string> kv in r.Errors)
                {
                    string where = r.InstanceLocation.ToString();
                    lines.Add($"스키마: {(where.Length == 0 ? "/" : where)} — {kv.Key}: {kv.Value}");
                }
            }
            if (r.Details != null)
            {
                foreach (EvaluationResults d in r.Details)
                {
                    Collect(d, lines);
                }
            }
        }
    }
}
