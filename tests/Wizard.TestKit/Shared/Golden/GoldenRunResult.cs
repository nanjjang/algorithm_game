using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wizard.TestKit.Shared.Arith;

namespace Wizard.TestKit.Shared.Golden
{
    // (런타임, 아키텍처) 조합 하나의 결과 파일: golden-result.<조합>.json
    // 시각·기기 이름처럼 실행마다 바뀌는 값은 넣지 않는다. 같은 결과면 파일 바이트가 같다.
    //   { "format": 1, "combo": "macos-dotnet-test-arm64",
    //     "entries": [ { "id": "m1/…", "hashes": [ {"tick":…, "hash":"…"} … ] } | { "id": …, "error": "…" } ],
    //     "arith":   [ { "bundle": "…", "case": "…", "values": ["…"] } | { …, "fault": "SimInvariant" } ] }
    public sealed class GoldenRunResult
    {
        public const int FormatVersion = 1;

        public GoldenRunResult(GoldenCombo combo, IReadOnlyList<GoldenEntryResult> entries, IReadOnlyList<ArithCaseResult> arith)
        {
            Combo = combo;
            Entries = entries;
            Arith = arith;
        }

        public GoldenCombo Combo { get; }

        public IReadOnlyList<GoldenEntryResult> Entries { get; }

        public IReadOnlyList<ArithCaseResult> Arith { get; }

        public string ToJson()
        {
            using (StringWriter text = JsonFiles.CreateText())
            {
                using (JsonTextWriter w = JsonFiles.CreateWriter(text))
                {
                    w.WriteStartObject();
                    w.WritePropertyName("format");
                    w.WriteValue(FormatVersion);
                    w.WritePropertyName("combo");
                    w.WriteValue(Combo.Name);
                    w.WritePropertyName("entries");
                    w.WriteStartArray();
                    foreach (GoldenEntryResult e in Entries)
                    {
                        w.WriteStartObject();
                        w.WritePropertyName("id");
                        w.WriteValue(e.Id);
                        if (e.Error != null)
                        {
                            w.WritePropertyName("error");
                            w.WriteValue(e.Error);
                        }
                        else
                        {
                            w.WritePropertyName("hashes");
                            GoldenHashList.Write(w, e.Hashes);
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WritePropertyName("arith");
                    w.WriteStartArray();
                    foreach (ArithCaseResult a in Arith)
                    {
                        w.WriteStartObject();
                        w.WritePropertyName("bundle");
                        w.WriteValue(a.Bundle);
                        w.WritePropertyName("case");
                        w.WriteValue(a.CaseId);
                        if (a.Fault != null)
                        {
                            w.WritePropertyName("fault");
                            w.WriteValue(a.Fault);
                        }
                        else
                        {
                            w.WritePropertyName("values");
                            w.WriteStartArray();
                            foreach (ArithValue v in a.Values!)
                            {
                                w.WriteValue(v.Text);
                            }
                            w.WriteEndArray();
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                return text.ToString() + "\n";
            }
        }

        public void Save(string path) => JsonFiles.WriteUtf8(path, ToJson());

        // 기본 위치: <저장소>/artifacts/golden/golden-result.<조합>.json
        public string SaveToArtifacts(string repoRoot)
        {
            string path = Path.Combine(repoRoot, "artifacts", "golden", Combo.ResultFileName);
            Save(path);
            return path;
        }

        public static GoldenRunResult Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8), Path.GetFileName(path));

        public static GoldenRunResult Parse(string json, string where)
        {
            var problems = new List<string>();
            var j = new StrictJson(where, problems);
            GoldenCombo combo = default;
            var entries = new List<GoldenEntryResult>();
            var arith = new List<ArithCaseResult>();
            JObject? root = GoldenEntry.ParseRoot(j, json);
            if (root != null)
            {
                j.OnlyFields(root, "format", "combo", "entries", "arith");
                j.Format(root, FormatVersion);
                string? comboName = j.String(root, "combo", true);
                if (comboName != null && !GoldenCombo.TryParse(comboName, out combo))
                {
                    j.Problem("combo '" + comboName + "' is not <os>-<runtime>-<host>-<arch>");
                }
                JArray? es = j.Array(root, "entries", true);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; es != null && i < es.Count; i++)
                {
                    var e = new StrictJson(where + " entries[" + i + "]", problems);
                    if (es[i].Type != JTokenType.Object)
                    {
                        e.Problem("must be an object");
                        continue;
                    }
                    var o = (JObject)es[i];
                    e.OnlyFields(o, "id", "hashes", "error");
                    string? id = e.String(o, "id", true);
                    string? error = e.String(o, "error", false);
                    JArray? hs = e.Array(o, "hashes", false);
                    if ((hs == null) == (error == null))
                    {
                        e.Problem("exactly one of 'hashes' and 'error' is required");
                    }
                    if (id != null && !seen.Add(id))
                    {
                        e.Problem("duplicate entry id '" + id + "'");
                    }
                    List<GoldenHash> hashes = GoldenHashList.Read(e, hs, "hashes", problems);
                    if (id != null)
                    {
                        entries.Add(new GoldenEntryResult(id, hashes, error));
                    }
                }
                JArray? ar = j.Array(root, "arith", true);
                var seenCase = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; ar != null && i < ar.Count; i++)
                {
                    var a = new StrictJson(where + " arith[" + i + "]", problems);
                    if (ar[i].Type != JTokenType.Object)
                    {
                        a.Problem("must be an object");
                        continue;
                    }
                    var o = (JObject)ar[i];
                    a.OnlyFields(o, "bundle", "case", "values", "fault");
                    string? bundle = a.String(o, "bundle", true);
                    string? caseId = a.String(o, "case", true);
                    string? fault = a.String(o, "fault", false);
                    JArray? vs = a.Array(o, "values", false);
                    if ((vs == null) == (fault == null))
                    {
                        a.Problem("exactly one of 'values' and 'fault' is required");
                    }
                    List<ArithValue>? values = null;
                    if (vs != null)
                    {
                        values = new List<ArithValue>();
                        foreach (JToken t in vs)
                        {
                            if (t.Type == JTokenType.String && ArithValue.TryParse((string?)t, out ArithValue v))
                            {
                                values.Add(v);
                            }
                            else
                            {
                                a.Problem("values must be decimal or 0x strings");
                            }
                        }
                    }
                    if (bundle != null && caseId != null)
                    {
                        if (!seenCase.Add(bundle + "/" + caseId))
                        {
                            a.Problem("duplicate arith case '" + bundle + "/" + caseId + "'");
                        }
                        arith.Add(new ArithCaseResult(bundle, caseId, values, fault));
                    }
                }
            }
            GoldenFormatException.ThrowIfAny(problems);
            return new GoldenRunResult(combo, entries, arith);
        }
    }
}
