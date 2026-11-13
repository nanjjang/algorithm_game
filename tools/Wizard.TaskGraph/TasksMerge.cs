using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wizard.TaskGraph
{
    // tasks.json 의 구조 병합(DECISIONS-TECH T-65 9번). 세 판(바탕, main 쪽, 옮기는 커밋 쪽)의 글자를 받아 합친 글자나 진짜 충돌을 낸다.
    // git 을 부르지 않는 순수 함수다.
    //   - 카드는 id, 에이전트는 name, 마일스톤은 id 로 맞춘다. 한쪽만 더한 것은 넣는다.
    //     같은 id 로 양쪽이 다르게 더했으면, 또는 한쪽이 지운 것을 다른 쪽이 고쳤으면 진짜 충돌이다.
    //   - 값 칸(그 밖의 모든 것: schema, project, 카드의 title·parallel_group…, 에이전트의 default_model, 마일스톤의 title·gate,
    //     새로 생기거나 없어진 열쇠): 한쪽만 바꿨으면 그 값, 양쪽이 같은 값으로 바꿨으면 그 값, 다르게 바꿨으면 진짜 충돌.
    //   - 목록 칸(카드의 deps, spec_refs, contracts, owned_paths, acceptance, vectors 와 맨 위의 notes)은 항목의 집합으로 합친다:
    //     어느 한쪽이 뺀 항목은 빠지고, 더한 항목은 다 들며(같은 항목은 한 번), 순서는 main 쪽 다음에 옮기는 쪽만 더한 항목이다.
    //     양쪽이 같은 항목을 빼고 서로 다른 항목을 더했으면(같은 줄을 다르게 고쳐 쓴 것) 진짜 충돌이다.
    //   - 순서는 main 쪽을 따른다. 옮기는 쪽만 더한 것은 그쪽에서 바로 앞에 있던 것의 뒤(그 자리에 main 쪽이 더한 것이 있으면 그 뒤)다.
    //   - 마일스톤의 task_count, blocked_on_assets, parallel_groups 는 합치지 않고 합친 카드에서 다시 센다(Recount, main 쪽 순서가 바탕).
    //   - 결과는 JSON.stringify(…, null, 1) + 줄바꿈 꼴이다. 양쪽이 건드리지 않은 노드는 main 쪽 것을 그대로 옮긴다.
    public static class TasksMerge
    {
        public const string FileLabel = "tasks.json";

        public sealed class Outcome
        {
            public Outcome(string? text, IReadOnlyList<string> conflicts)
            {
                Text = text;
                Conflicts = conflicts;
            }

            // 진짜 충돌이 있으면 null
            public string? Text { get; }
            public IReadOnlyList<string> Conflicts { get; }
            public bool Ok => Conflicts.Count == 0 && Text != null;
        }

        public static Outcome Merge(string baseText, string mainText, string movingText)
        {
            JsonObject b, m, t;
            try
            {
                b = Parse(baseText, "바탕");
                m = Parse(mainText, "main 쪽");
                t = Parse(movingText, "옮기는 쪽");
            }
            catch (TaskGraphException e)
            {
                return new Outcome(null, new[] { e.Message });
            }
            var merger = new Merger();
            JsonObject result = merger.Root(b, m, t);
            if (merger.Conflicts.Count > 0)
            {
                return new Outcome(null, merger.Conflicts);
            }
            Recount.Apply(result);
            return new Outcome(JsStyleJson.Serialize(result, TaskDoc.FileIndent), merger.Conflicts);
        }

        private static JsonObject Parse(string text, string which)
        {
            JsonNode? n;
            try
            {
                n = JsonNode.Parse(text);
            }
            catch (JsonException e)
            {
                throw new TaskGraphException($"{FileLabel}({which}): JSON 이 아니다 — {e.Message}");
            }
            catch (ArgumentException e)
            {
                throw new TaskGraphException($"{FileLabel}({which}): JSON 을 읽지 못했다 — {e.Message}");
            }
            return n as JsonObject ?? throw new TaskGraphException($"{FileLabel}({which}): 최상위가 객체가 아니다");
        }

        // 값이 있는가와 그 값. Has=false 는 "열쇠가 없다"
        private readonly struct Opt
        {
            public Opt(bool has, JsonNode? node)
            {
                Has = has;
                Node = node;
            }

            public bool Has { get; }
            public JsonNode? Node { get; }

            public static Opt Of(JsonObject? o, string key)
            {
                return o != null && o.TryGetPropertyValue(key, out JsonNode? v) ? new Opt(true, v) : new Opt(false, null);
            }

            public bool Same(Opt other) => Has == other.Has && (!Has || JsonNode.DeepEquals(Node, other.Node));
        }

        private enum Kind
        {
            Root,
            Card,
            Agent,
            Milestone,
        }

        private sealed class Merger
        {
            private static readonly HashSet<string> CardLists = new HashSet<string>(TaskDef.ArrayFields, StringComparer.Ordinal);
            private static readonly HashSet<string> RootLists = new HashSet<string>(StringComparer.Ordinal) { "notes" };
            private static readonly HashSet<string> Recounted = new HashSet<string>(StringComparer.Ordinal) { "task_count", "blocked_on_assets", "parallel_groups" };

            public List<string> Conflicts { get; } = new List<string>();

            public JsonObject Root(JsonObject b, JsonObject m, JsonObject t) => Object(Kind.Root, "맨 위", b, m, t);

            // label: 충돌 문구에서 이 객체를 가리키는 말("카드 FIX-9")
            private JsonObject Object(Kind kind, string label, JsonObject? b, JsonObject m, JsonObject t)
            {
                var result = new JsonObject();
                var keys = m.Select(kv => kv.Key).ToList();
                keys.AddRange(t.Select(kv => kv.Key).Where(k => !m.ContainsKey(k)));
                foreach (string k in keys)
                {
                    Opt r = Field(kind, label, k, Opt.Of(b, k), Opt.Of(m, k), Opt.Of(t, k));
                    if (r.Has)
                    {
                        result[k] = r.Node?.DeepClone();
                    }
                }
                return result;
            }

            private Opt Field(Kind kind, string label, string key, Opt b, Opt m, Opt t)
            {
                switch (kind)
                {
                    case Kind.Root:
                        switch (key)
                        {
                            case "tasks": return Keyed("카드", "id", Kind.Card, label, key, b, m, t);
                            case "agents": return Keyed("에이전트", "name", Kind.Agent, label, key, b, m, t);
                            case "milestones": return Keyed("마일스톤", "id", Kind.Milestone, label, key, b, m, t);
                            default: return RootLists.Contains(key) ? List(label, key, b, m, t) : Value(label, key, b, m, t);
                        }
                    case Kind.Card:
                        return CardLists.Contains(key) ? List(label, key, b, m, t) : Value(label, key, b, m, t);
                    case Kind.Milestone:
                        // 집계 셋은 합치지 않는다. 합친 카드에서 다시 센다
                        return Recounted.Contains(key) ? (m.Has ? m : t) : Value(label, key, b, m, t);
                    default:
                        return Value(label, key, b, m, t);
                }
            }

            private Opt Value(string label, string key, Opt b, Opt m, Opt t)
            {
                if (m.Same(t) || t.Same(b))
                {
                    return m;
                }
                if (m.Same(b))
                {
                    return t;
                }
                Conflicts.Add($"{FileLabel}: {label}의 '{key}'를 양쪽이 다르게 고쳤다 — main 쪽 {Show(m)}, 옮기는 쪽 {Show(t)}");
                return m;
            }

            private Opt List(string label, string key, Opt b, Opt m, Opt t)
            {
                if (m.Node is not JsonArray ma || t.Node is not JsonArray ta || (b.Has && b.Node is not JsonArray))
                {
                    return Value(label, key, b, m, t);
                }
                var ba = b.Node as JsonArray ?? new JsonArray();
                List<string> bs = Keys(ba), ms = Keys(ma), ts = Keys(ta);
                var removedBoth = bs.Where(x => !ms.Contains(x) && !ts.Contains(x)).ToList();
                bool addedM = ms.Any(x => !bs.Contains(x) && !ts.Contains(x));
                bool addedT = ts.Any(x => !bs.Contains(x) && !ms.Contains(x));
                if (removedBoth.Count > 0 && addedM && addedT)
                {
                    Conflicts.Add($"{FileLabel}: {label}의 '{key}'에서 양쪽이 같은 항목을 빼고 서로 다른 항목을 더했다(같은 줄을 다르게 고쳐 썼다) — 뺀 것 {string.Join(", ", removedBoth.Select(x => Short(JsonNode.Parse(x)?.ToJsonString(Readable) ?? x)))}");
                    return m;
                }
                if (m.Same(t) || t.Same(b))
                {
                    return m;
                }
                if (m.Same(b))
                {
                    return t;
                }
                var result = new JsonArray();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < ma.Count; i++)
                {
                    string k = ms[i];
                    bool removedByT = bs.Contains(k) && !ts.Contains(k);
                    if (!removedByT && seen.Add(k))
                    {
                        result.Add(ma[i]?.DeepClone());
                    }
                }
                for (int i = 0; i < ta.Count; i++)
                {
                    string k = ts[i];
                    if (!bs.Contains(k) && !ms.Contains(k) && seen.Add(k))
                    {
                        result.Add(ta[i]?.DeepClone());
                    }
                }
                return new Opt(true, result);
            }

            // 열쇠로 맞추는 목록(카드, 에이전트, 마일스톤)
            private Opt Keyed(string what, string idField, Kind elemKind, string label, string key, Opt b, Opt m, Opt t)
            {
                if (m.Node is not JsonArray ma || t.Node is not JsonArray ta || (b.Has && b.Node is not JsonArray))
                {
                    return Value(label, key, b, m, t);
                }
                var ba = b.Node as JsonArray ?? new JsonArray();
                Dictionary<string, JsonObject>? bd = ById(ba, idField), md = ById(ma, idField), td = ById(ta, idField);
                if (bd == null || md == null || td == null)
                {
                    Conflicts.Add($"{FileLabel}: '{key}'의 항목에 '{idField}'가 없거나 겹친다(구조로 합칠 수 없다)");
                    return m;
                }
                var result = new List<(string Id, JsonObject Node)>();
                foreach (JsonObject mo in ma.OfType<JsonObject>())
                {
                    string id = TaskDef.Str(mo, idField)!;
                    string elem = $"{what} {id}";
                    bool inB = bd.TryGetValue(id, out JsonObject? bo);
                    bool inT = td.TryGetValue(id, out JsonObject? to);
                    if (inB && inT)
                    {
                        result.Add((id, Object(elemKind, elem, bo, mo, to!)));
                    }
                    else if (inB)
                    {
                        if (!JsonNode.DeepEquals(bo, mo))
                        {
                            Conflicts.Add($"{FileLabel}: {elem}을(를) 옮기는 쪽이 지웠는데 main 쪽이 고쳤다");
                        }
                    }
                    else if (inT)
                    {
                        if (JsonNode.DeepEquals(mo, to))
                        {
                            result.Add((id, (JsonObject)mo.DeepClone()));
                        }
                        else
                        {
                            Conflicts.Add($"{FileLabel}: {elem}을(를) 양쪽이 같은 id로 다르게 더했다(번호 겹침)");
                        }
                    }
                    else
                    {
                        result.Add((id, (JsonObject)mo.DeepClone()));
                    }
                }
                string? prev = null;
                foreach (JsonObject to in ta.OfType<JsonObject>())
                {
                    string id = TaskDef.Str(to, idField)!;
                    if (md.ContainsKey(id))
                    {
                        if (result.Any(r => r.Id == id))
                        {
                            prev = id;
                        }
                        continue;
                    }
                    if (bd.TryGetValue(id, out JsonObject? bo))
                    {
                        if (!JsonNode.DeepEquals(bo, to))
                        {
                            Conflicts.Add($"{FileLabel}: {what} {id}을(를) main 쪽이 지웠는데 옮기는 쪽이 고쳤다");
                        }
                        continue;
                    }
                    int at = prev == null ? 0 : result.FindIndex(r => r.Id == prev) + 1;
                    while (at < result.Count && !bd.ContainsKey(result[at].Id) && !td.ContainsKey(result[at].Id))
                    {
                        at++; // 그 자리에 main 쪽이 더한 것이 있으면 그 뒤
                    }
                    result.Insert(at, (id, (JsonObject)to.DeepClone()));
                    prev = id;
                }
                var arr = new JsonArray();
                foreach ((string _, JsonObject node) in result)
                {
                    arr.Add(node);
                }
                return new Opt(true, arr);
            }

            private static Dictionary<string, JsonObject>? ById(JsonArray arr, string idField)
            {
                var d = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
                foreach (JsonNode? n in arr)
                {
                    if (n is not JsonObject o || TaskDef.Str(o, idField) is not string id || d.ContainsKey(id))
                    {
                        return null;
                    }
                    d[id] = o;
                }
                return d;
            }

            private static List<string> Keys(JsonArray a) => a.Select(n => n?.ToJsonString() ?? "null").ToList();

            private static string Short(string s) => s.Length > 60 ? s.Substring(0, 60) + "…" : s;

            private static readonly JsonSerializerOptions Readable = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            private static string Show(Opt o) => o.Has ? Short(o.Node?.ToJsonString(Readable) ?? "null") : "(없음)";
        }
    }
}
