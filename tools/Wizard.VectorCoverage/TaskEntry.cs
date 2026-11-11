using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Wizard.VectorCoverage
{
    // docs/dev/tasks.json 의 작업 하나에서 이 도구가 쓰는 부분: id, 마일스톤, 벡터 선택자.
    public sealed class TaskEntry
    {
        public TaskEntry(string id, string milestone, IReadOnlyList<string> vectors)
        {
            Id = id;
            Milestone = milestone;
            Vectors = vectors;
        }

        public string Id { get; }
        public string Milestone { get; }

        // 선택자: "S07§17.3" | "S13:TV-051" | "S13:TV-026..TV-028"
        public IReadOnlyList<string> Vectors { get; }

        public static IReadOnlyList<TaskEntry> Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new CoverageException("작업 그래프가 없다: " + path, CoverageException.UsageExitCode);
            }
            return Parse(File.ReadAllText(path, Encoding.UTF8), path);
        }

        public static IReadOnlyList<TaskEntry> Parse(string json, string source)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException e)
            {
                throw new CoverageException(source + ": JSON 이 아니다 — " + e.Message, CoverageException.UsageExitCode);
            }
            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("tasks", out JsonElement tasks) || tasks.ValueKind != JsonValueKind.Array)
                {
                    throw new CoverageException(source + ": 'tasks' 배열이 없다", CoverageException.UsageExitCode);
                }
                var list = new List<TaskEntry>();
                foreach (JsonElement t in tasks.EnumerateArray())
                {
                    string id = Str(t, "id") ?? throw new CoverageException(source + ": id 가 없는 작업이 있다", CoverageException.UsageExitCode);
                    string milestone = Str(t, "milestone") ?? "";
                    var vectors = new List<string>();
                    if (t.TryGetProperty("vectors", out JsonElement v) && v.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement s in v.EnumerateArray())
                        {
                            vectors.Add(s.ValueKind == JsonValueKind.String ? s.GetString() ?? "" : s.GetRawText());
                        }
                    }
                    list.Add(new TaskEntry(id, milestone, vectors));
                }
                return list;
            }
        }

        private static string? Str(JsonElement o, string name)
        {
            return o.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        }

        public override string ToString() => Id;
    }
}
