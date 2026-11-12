using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Wizard.Perf
{
    // 측정 결과 파일과 기준선 파일(tests/perf/baseline.json)의 같은 꼴:
    //   { "format": 1, "kind": "wizard-perf", "settings": "full"|"quick", "note": "…",
    //     "machine": { 글자 칸 }, "metrics": { 항목 id: 수 }, "context": { 참고 id: 수 } }
    // metrics 만 판정에 쓴다. context 는 부하의 크기(탄·적 수, 이벤트 수, 기록 길이)이고 판정하지 않는다.
    // 열쇠는 순서 비교(Ordinal) 오름차순으로 쓴다(같은 값이면 같은 바이트. 기준선의 차이를 보기 쉽게).
    internal sealed class PerfResults
    {
        public const int Format = 1;
        public const string Kind = "wizard-perf";
        private const int Decimals = 3;

        public string Settings = "full";
        public string Note = string.Empty;
        public readonly SortedDictionary<string, string> Machine = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, double> Metrics = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, double> Context = new SortedDictionary<string, double>(StringComparer.Ordinal);

        // 기기 칸: 운영체제, 아키텍처, 코어 수, 런타임. 기기 이름과 사용자 이름은 넣지 않는다
        public void FillMachine()
        {
            Machine["os"] = RuntimeInformation.OSDescription;
            Machine["arch"] = RuntimeInformation.ProcessArchitecture.ToString();
            Machine["cpu_count"] = Environment.ProcessorCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Machine["runtime"] = RuntimeInformation.FrameworkDescription;
        }

        public string ToJson()
        {
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteNumber("format", Format);
                w.WriteString("kind", Kind);
                w.WriteString("settings", Settings);
                w.WriteString("note", Note);
                w.WriteStartObject("machine");
                foreach (KeyValuePair<string, string> kv in Machine)
                {
                    w.WriteString(kv.Key, kv.Value);
                }
                w.WriteEndObject();
                WriteNumbers(w, "metrics", Metrics);
                WriteNumbers(w, "context", Context);
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }

        public void Save(string path)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
        }

        public static PerfResults Load(string path) => Parse(File.ReadAllText(path), path);

        // 꼴이 다르면 FormatException(어느 파일의 어느 칸인지 적는다). 모르는 맨 위 칸도 거절한다
        public static PerfResults Parse(string json, string origin)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException e)
            {
                throw new FormatException(origin + ": not JSON: " + e.Message);
            }
            using (doc)
            {
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException(origin + ": the top level must be an object");
                }
                var r = new PerfResults();
                bool sawFormat = false, sawKind = false, sawMetrics = false;
                foreach (JsonProperty p in root.EnumerateObject())
                {
                    switch (p.Name)
                    {
                        case "format":
                            if (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetInt32(out int f) || f != Format)
                            {
                                throw new FormatException(origin + ": format must be " + Format);
                            }
                            sawFormat = true;
                            break;
                        case "kind":
                            if (p.Value.ValueKind != JsonValueKind.String || p.Value.GetString() != Kind)
                            {
                                throw new FormatException(origin + ": kind must be \"" + Kind + "\"");
                            }
                            sawKind = true;
                            break;
                        case "settings":
                            r.Settings = String(p, origin);
                            break;
                        case "note":
                            r.Note = String(p, origin);
                            break;
                        case "machine":
                            foreach (JsonProperty m in Object(p, origin).EnumerateObject())
                            {
                                r.Machine[m.Name] = String(m, origin + ".machine");
                            }
                            break;
                        case "metrics":
                            ReadNumbers(Object(p, origin), r.Metrics, origin + ".metrics");
                            sawMetrics = true;
                            break;
                        case "context":
                            ReadNumbers(Object(p, origin), r.Context, origin + ".context");
                            break;
                        default:
                            throw new FormatException(origin + ": unknown field \"" + p.Name + "\"");
                    }
                }
                if (!sawFormat || !sawKind || !sawMetrics)
                {
                    throw new FormatException(origin + ": format, kind and metrics are required");
                }
                return r;
            }
        }

        private static void WriteNumbers(Utf8JsonWriter w, string name, SortedDictionary<string, double> values)
        {
            w.WriteStartObject(name);
            foreach (KeyValuePair<string, double> kv in values)
            {
                w.WriteNumber(kv.Key, Math.Round(kv.Value, Decimals, MidpointRounding.AwayFromZero));
            }
            w.WriteEndObject();
        }

        private static void ReadNumbers(JsonElement obj, SortedDictionary<string, double> into, string origin)
        {
            foreach (JsonProperty p in obj.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Number)
                {
                    throw new FormatException(origin + "." + p.Name + ": must be a number");
                }
                double v = p.Value.GetDouble();
                if (double.IsNaN(v) || double.IsInfinity(v) || v < 0)
                {
                    throw new FormatException(origin + "." + p.Name + ": must be a finite number >= 0");
                }
                if (into.ContainsKey(p.Name))
                {
                    throw new FormatException(origin + "." + p.Name + ": duplicate key");
                }
                into[p.Name] = v;
            }
        }

        private static string String(JsonProperty p, string origin)
        {
            if (p.Value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException(origin + "." + p.Name + ": must be a string");
            }
            return p.Value.GetString() ?? string.Empty;
        }

        private static JsonElement Object(JsonProperty p, string origin)
        {
            if (p.Value.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException(origin + "." + p.Name + ": must be an object");
            }
            return p.Value;
        }
    }
}
