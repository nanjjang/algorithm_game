using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Data
{
    /// <summary>
    /// 솔로 재미 조정 손잡이(Resources/DataSolo/FunPass.json, Docs/11-fun-pass.md). 명세 사본(Resources/Data)과 보충 조각(DataSolo)은
    /// 그대로 두고, 읽는 순간에 JSON 위에 덮어쓴다. 켜는 곳은 게임 부팅(<see cref="ContentDb.LoadFromResources"/>)이고,
    /// 테스트·도구의 <see cref="ContentDb.LoadFromDirectory"/>는 기본 꺼짐 → 명세 벡터 테스트는 명세 값 그대로 돈다.
    ///
    /// 파일 꼴:
    /// <code>
    /// { "enabled": true,
    ///   "patches": [
    ///     { "id": "heat.cool_delay", "_note": "이유", "target": "sim/characters",
    ///       "select": ["baseline", "characters[*]"], "set": { "cool_delay_seconds": 0.1 }, "was": { "cool_delay_seconds": 0.2 } },
    ///     { "id": "waves.script_w14", "target": "sim/waves", "select": "script", "append": [ { "wave": 14, ... } ] },
    ///     { "id": "player.start_crystals", "target": "DataSolo/G01", "set": { "start_crystals": 1 } } ] }
    /// </code>
    /// - target: Resources/Data 상대 경로(ContentDb.Paths) 또는 "DataSolo/조각 이름".
    /// - select: 경로 하나 또는 여럿. 경로 = '/'로 이은 마디, 마디 = 이름 | 이름[*] | 이름[열쇠=값]. 비우면 뿌리. 걸린 것이 없으면 오류.
    /// - set: 걸린 객체마다 열쇠를 덮어쓴다(값은 통째로 바뀐다). append: 걸린 배열 끝에 더한다.
    /// - was: 덮어쓰기 전 값(문서의 "전" 칸). 있으면 맞는지 검사해 다르면 오류 — 명세 사본이 바뀌었는데 조정이 모르고 덮는 일을 막는다.
    /// 오류는 ContentDb.LoadErrors에 모인다(테스트가 비어 있음을 확인한다).
    /// </summary>
    public static class TuningPatches
    {
        /// <summary>손잡이 파일의 조각 이름(DataSolo/FunPass.json).</summary>
        public const string ExtraName = "FunPass";
        /// <summary>보충 조각을 가리키는 target의 머리.</summary>
        public const string SoloExtraPrefix = "DataSolo/";

        /// <summary>파일이 켜져 있으면 patches 배열, 아니면 null.</summary>
        public static JArray EnabledPatches(JObject file)
        {
            if (file == null) return null;
            var en = file["enabled"];
            if (en == null || en.Type != JTokenType.Boolean || !en.Value<bool>()) return null;
            return file["patches"] as JArray;
        }

        /// <summary>target에 맞는 덮어쓰기를 JSON 글에 적용한 글을 돌려준다(걸린 것이 없으면 원문 그대로).</summary>
        public static string ApplyToText(JArray patches, string target, string text, List<string> errors, List<string> applied)
        {
            if (patches == null || text == null || !HasTarget(patches, target)) return text;
            JToken root;
            try { root = JToken.Parse(text); }
            catch (Exception e) { errors?.Add("TuningPatches: cannot parse '" + target + "': " + e.Message); return text; }
            ApplyToToken(patches, target, root, errors, applied);
            return root.ToString(Formatting.None);
        }

        static bool HasTarget(JArray patches, string target)
        {
            for (int i = 0; i < patches.Count; i++)
            {
                var p = patches[i] as JObject;
                if (p != null && (string)p["target"] == target) return true;
            }
            return false;
        }

        /// <summary>target에 맞는 덮어쓰기를 그 자리에서 적용한다.</summary>
        public static void ApplyToToken(JArray patches, string target, JToken root, List<string> errors, List<string> applied)
        {
            if (patches == null || root == null) return;
            for (int i = 0; i < patches.Count; i++)
            {
                var p = patches[i] as JObject;
                if (p == null || (string)p["target"] != target) continue;
                string id = (string)p["id"] ?? ("#" + i);
                var nodes = new List<JToken>();
                var sel = p["select"];
                if (sel == null) nodes.Add(root);
                else if (sel.Type == JTokenType.String) Select(root, (string)sel, nodes);
                else if (sel is JArray arr) for (int k = 0; k < arr.Count; k++) Select(root, (string)arr[k], nodes);
                if (nodes.Count == 0)
                {
                    errors?.Add("TuningPatches: '" + id + "' selects nothing in '" + target + "'");
                    continue;
                }
                var set = p["set"] as JObject;
                var was = p["was"] as JObject;
                var append = p["append"] as JArray;
                bool ok = true;
                for (int n = 0; n < nodes.Count; n++)
                {
                    if (set != null)
                    {
                        var obj = nodes[n] as JObject;
                        if (obj == null) { errors?.Add("TuningPatches: '" + id + "' set on a non-object"); ok = false; continue; }
                        if (was != null)
                            foreach (var kv in was)
                                if (!JToken.DeepEquals(obj[kv.Key], kv.Value))
                                {
                                    errors?.Add("TuningPatches: '" + id + "' expected " + kv.Key + " = " + kv.Value.ToString(Formatting.None)
                                                + " but found " + (obj[kv.Key] != null ? obj[kv.Key].ToString(Formatting.None) : "nothing"));
                                    ok = false;
                                }
                        foreach (var kv in set) obj[kv.Key] = kv.Value.DeepClone();
                    }
                    if (append != null)
                    {
                        var list = nodes[n] as JArray;
                        if (list == null) { errors?.Add("TuningPatches: '" + id + "' append on a non-array"); ok = false; continue; }
                        for (int k = 0; k < append.Count; k++) list.Add(append[k].DeepClone());
                    }
                }
                if (ok) applied?.Add(id);
            }
        }

        /// <summary>경로 하나가 가리키는 마디를 모은다.</summary>
        static void Select(JToken root, string path, List<JToken> into)
        {
            var cur = new List<JToken> { root };
            if (!string.IsNullOrEmpty(path))
            {
                string[] parts = path.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    var next = new List<JToken>();
                    for (int c = 0; c < cur.Count; c++) Step(cur[c], parts[i], next);
                    cur = next;
                    if (cur.Count == 0) return;
                }
            }
            into.AddRange(cur);
        }

        static void Step(JToken node, string part, List<JToken> into)
        {
            string name = part, filter = null;
            int b = part.IndexOf('[');
            if (b >= 0 && part.EndsWith("]", StringComparison.Ordinal))
            {
                name = part.Substring(0, b);
                filter = part.Substring(b + 1, part.Length - b - 2);
            }
            JToken child = name.Length == 0 ? node : (node as JObject)?[name];
            if (child == null) return;
            if (filter == null) { into.Add(child); return; }
            var arr = child as JArray;
            if (arr == null) return;
            if (filter == "*") { for (int i = 0; i < arr.Count; i++) into.Add(arr[i]); return; }
            int eq = filter.IndexOf('=');
            if (eq <= 0) return;
            string key = filter.Substring(0, eq), value = filter.Substring(eq + 1);
            for (int i = 0; i < arr.Count; i++)
            {
                var v = (arr[i] as JObject)?[key];
                if (v != null && v.Type != JTokenType.Null && string.Equals(v.ToString(Formatting.None).Trim('"'), value, StringComparison.Ordinal))
                    into.Add(arr[i]);
            }
        }
    }
}
