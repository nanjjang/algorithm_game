using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wizard.TaskGraph
{
    // docs/dev/tasks.json 전체. 원래의 JsonObject 를 들고 있어서 add 가 열쇠 순서를 지키며 다시 쓸 수 있다.
    public sealed class TaskDoc
    {
        public const int FileIndent = 1;

        private TaskDoc(JsonObject root)
        {
            Root = root;
            var tasks = new List<TaskDef>();
            if (root["tasks"] is JsonArray arr)
            {
                foreach (JsonNode? n in arr)
                {
                    if (n is JsonObject o)
                    {
                        tasks.Add(TaskDef.From(o));
                    }
                }
            }
            Tasks = tasks;
            var byId = new Dictionary<string, TaskDef>(StringComparer.Ordinal);
            foreach (TaskDef t in tasks)
            {
                byId.TryAdd(t.Id, t);
            }
            ById = byId;
        }

        public JsonObject Root { get; }
        public IReadOnlyList<TaskDef> Tasks { get; }
        public IReadOnlyDictionary<string, TaskDef> ById { get; }

        public static TaskDoc Parse(string text, string sourceName)
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            }
            catch (JsonException e)
            {
                throw new TaskGraphException($"{sourceName}: JSON 이 아니다 — {e.Message}");
            }
            catch (ArgumentException e)
            {
                // JsonObject 는 같은 열쇠가 두 번 나오면 여기로 온다
                throw new TaskGraphException($"{sourceName}: JSON 을 읽지 못했다 — {e.Message}");
            }
            if (node is not JsonObject obj)
            {
                throw new TaskGraphException($"{sourceName}: 최상위가 객체가 아니다");
            }
            return new TaskDoc(obj);
        }

        public static TaskDoc Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new TaskGraphException($"작업 그래프가 없다: {path}", TaskGraphException.UsageExitCode);
            }
            return Parse(File.ReadAllText(path, Encoding.UTF8), path);
        }

        public TaskDef Get(string id)
        {
            if (!ById.TryGetValue(id, out TaskDef? t))
            {
                throw new TaskGraphException($"작업 '{id}' 이 tasks.json 에 없다", TaskGraphException.UsageExitCode);
            }
            return t;
        }

        public string Serialize() => JsStyleJson.Serialize(Root, FileIndent);
    }
}
