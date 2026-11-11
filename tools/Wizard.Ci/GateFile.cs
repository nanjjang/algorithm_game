using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wizard.Ci
{
    // 마일스톤 게이트 파일 docs/dev/gates/M<N>.json(PIPELINE 4.6, 7.1 G11). ci.sh gate M<N> 이 쓴다. 그 파일을 커밋하는 것은 M<N>-INT 작업이다.
    //   "ci"            이번 실행: 단계별 결과(fast, full, integration. 바뀐 경로로 줄이지 않는다)와 전체 통과 여부
    //   "human_checks"  그 마일스톤에서 human_check 가 있는 작업마다 한 항목. 새 항목은 status "기록 대기"이고,
    //                   이미 적힌 항목(사람이 기록한 status, recorded_by, note …)은 그대로 둔다
    // 파일에 이미 있는 다른 열쇠(예: M0 의 check-diff 소급 결과)는 건드리지 않는다.
    public static class GateFile
    {
        public const string Pending = "기록 대기";

        public static string PathFor(string root, string milestone) => Path.Combine(root, "docs", "dev", "gates", milestone + ".json");

        // tasks.json 에서 그 마일스톤의 human_check 를 (작업 id, 내용) 목록으로. 마일스톤이 없으면 실패
        public static List<KeyValuePair<string, string>> HumanChecks(string tasksJson, string milestone)
        {
            JsonNode? root = JsonNode.Parse(tasksJson);
            bool known = false;
            if (root?["milestones"] is JsonArray ms)
            {
                foreach (JsonNode? m in ms)
                {
                    if ((string?)m?["id"] == milestone)
                    {
                        known = true;
                    }
                }
            }
            if (!known)
            {
                throw new CiException("tasks.json 에 마일스톤 " + milestone + " 이 없다(ci.sh gate M<N>)");
            }
            var list = new List<KeyValuePair<string, string>>();
            if (root?["tasks"] is JsonArray tasks)
            {
                foreach (JsonNode? t in tasks)
                {
                    if ((string?)t?["milestone"] == milestone && t?["human_check"] is JsonValue hc && hc.TryGetValue(out string? text) && text != null)
                    {
                        list.Add(new KeyValuePair<string, string>((string?)t["id"] ?? "?", text));
                    }
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }

        // 기존 내용(없으면 null)에 이번 실행을 합친 새 내용
        public static string Merge(string? existing, string milestone, IReadOnlyList<StageResult> stages, string root, string? branch, string? commit,
                                   IReadOnlyList<KeyValuePair<string, string>> humanChecks)
        {
            JsonObject doc;
            if (existing == null)
            {
                doc = new JsonObject();
            }
            else
            {
                JsonNode? parsed;
                try
                {
                    parsed = JsonNode.Parse(existing);
                }
                catch (JsonException e)
                {
                    throw new CiException("게이트 파일이 JSON 이 아니다: " + e.Message + ". 손으로 고친 뒤 다시 돌린다");
                }
                doc = parsed as JsonObject ?? throw new CiException("게이트 파일의 최상위가 객체가 아니다. 손으로 고친 뒤 다시 돌린다");
            }
            doc["milestone"] = milestone;

            bool passed = stages.Count == 3;
            foreach (StageResult s in stages)
            {
                passed &= s.Passed;
            }
            string ciText = JsonFile.ToText(w =>
            {
                w.WriteStartObject();
                w.WriteBoolean("passed", passed);
                if (branch != null)
                {
                    w.WriteString("branch", branch);
                }
                if (commit != null)
                {
                    w.WriteString("commit", commit);
                }
                w.WriteString("finished_at", StageResult.Time(DateTime.UtcNow));
                w.WriteStartObject("stages");
                foreach (StageResult s in stages)
                {
                    w.WriteStartObject(s.Stage);
                    s.WriteBody(w, root, branch, commit, true, false);
                    w.WriteEndObject();
                }
                w.WriteEndObject();
                w.WriteEndObject();
            });
            doc["ci"] = JsonNode.Parse(ciText);

            var old = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            if (doc["human_checks"] is JsonArray prev)
            {
                foreach (JsonNode? n in prev)
                {
                    if (n is JsonObject o && (string?)o["task"] is string id)
                    {
                        old[id] = o;
                    }
                }
            }
            var checks = new JsonArray();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> hc in humanChecks)
            {
                seen.Add(hc.Key);
                if (old.TryGetValue(hc.Key, out JsonObject? o))
                {
                    checks.Add(JsonNode.Parse(o.ToJsonString()));
                }
                else
                {
                    checks.Add(new JsonObject { ["task"] = hc.Key, ["check"] = hc.Value, ["status"] = Pending });
                }
            }
            // tasks.json 에서 사라진 항목도 사람이 적은 기록이므로 지우지 않는다
            foreach (KeyValuePair<string, JsonObject> kv in old)
            {
                if (!seen.Contains(kv.Key))
                {
                    checks.Add(JsonNode.Parse(kv.Value.ToJsonString()));
                }
            }
            doc["human_checks"] = checks;
            return JsonFile.ToText(w => doc.WriteTo(w));
        }

        public static void Write(string root, string milestone, IReadOnlyList<StageResult> stages, string? branch, string? commit)
        {
            string tasksPath = Path.Combine(root, "docs", "dev", "tasks.json");
            List<KeyValuePair<string, string>> hcs = HumanChecks(File.ReadAllText(tasksPath, Encoding.UTF8), milestone);
            string path = PathFor(root, milestone);
            string? existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            string text = Merge(existing, milestone, stages, root, branch, commit, hcs);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
