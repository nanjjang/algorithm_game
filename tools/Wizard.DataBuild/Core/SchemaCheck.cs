using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Json.Schema;

namespace Wizard.DataBuild.Core
{
    // ARCHITECTURE 8.2 1단계: JSON Schema 검사(JsonSchema.Net, T-37).
    //   - data/schema/*.schema.json 은 draft 2020-12 이고 "$id" 가 SchemaIdBase + 파일 이름이다(서로를 상대 경로로 가리킨다:
    //     "$ref": "_common.schema.json#/$defs/contentId"). 등록은 이번 검사만의 표에 한다(전역 표와 네트워크를 쓰지 않는다).
    //   - data/ 의 파일마다 스키마(DataFile.SchemaFile)가 있어야 하고 그 스키마를 통과해야 한다.
    //   - _common.schema.json 은 늘 있어야 한다(공통 정의).
    public static class SchemaCheck
    {
        public const string SchemaIdBase = "https://wizard.invalid/data/schema/";
        public const string CommonSchema = "_common.schema.json";
        public const string Draft = "https://json-schema.org/draft/2020-12/schema";

        public static void Run(DataSet data, Diagnostics diagnostics)
        {
            var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
            Dictionary<string, JsonSchema> schemas = LoadSchemas(data.Root, options, diagnostics);
            if (!schemas.ContainsKey(CommonSchema))
            {
                diagnostics.Error(SchemaPath(CommonSchema), "공통 스키마가 없다");
            }
            foreach (DataFile f in data.Files)
            {
                if (!schemas.TryGetValue(f.SchemaFile, out JsonSchema? schema))
                {
                    diagnostics.Error(f.RepoPath, "스키마 " + SchemaPath(f.SchemaFile) + " 가 없다(데이터 파일마다 스키마가 있다)");
                    continue;
                }
                EvaluationResults results = schema.Evaluate(f.Document, options);
                if (results.IsValid)
                {
                    continue;
                }
                bool any = false;
                foreach (EvaluationResults d in Flatten(results))
                {
                    if (d.HasErrors && d.Errors != null)
                    {
                        var keys = new List<string>(d.Errors.Keys);
                        keys.Sort(StringComparer.Ordinal);
                        foreach (string k in keys)
                        {
                            diagnostics.Error(f.RepoPath, d.InstanceLocation.ToString(), "스키마(" + k + "): " + d.Errors[k]);
                            any = true;
                        }
                    }
                }
                if (!any)
                {
                    diagnostics.Error(f.RepoPath, "스키마 " + SchemaPath(f.SchemaFile) + " 를 통과하지 못했다");
                }
            }
        }

        private static IEnumerable<EvaluationResults> Flatten(EvaluationResults r)
        {
            yield return r;
            if (r.HasDetails)
            {
                foreach (EvaluationResults d in r.Details)
                {
                    foreach (EvaluationResults x in Flatten(d))
                    {
                        yield return x;
                    }
                }
            }
        }

        private static Dictionary<string, JsonSchema> LoadSchemas(string root, EvaluationOptions options, Diagnostics diagnostics)
        {
            var result = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
            string dir = Path.Combine(root, DataLoader.DataDir, DataLoader.SchemaDir);
            if (!Directory.Exists(dir))
            {
                diagnostics.Error(DataLoader.DataDir + "/" + DataLoader.SchemaDir + "/", "스키마 폴더가 없다");
                return result;
            }
            foreach (string path in DataLoader.SortedEntries(dir))
            {
                string name = Path.GetFileName(path);
                string repoPath = SchemaPath(name);
                if (Directory.Exists(path) || !name.EndsWith(DataLoader.SchemaSuffix, StringComparison.Ordinal))
                {
                    diagnostics.Error(repoPath, "data/schema/ 에는 <이름>.schema.json 파일만 둔다");
                    continue;
                }
                string text = File.ReadAllText(path);
                JsonDocument parsed;
                try
                {
                    parsed = DataLoader.Parse(System.Text.Encoding.UTF8.GetBytes(text));
                }
                catch (JsonException e)
                {
                    diagnostics.Error(repoPath, "JSON 이 아니다 — " + e.Message);
                    continue;
                }
                using (JsonDocument doc = parsed)
                {
                    int before = diagnostics.Count;
                    DataLoader.CheckDuplicateKeys(doc.RootElement, "", repoPath, diagnostics);
                    if (diagnostics.Count != before)
                    {
                        continue;
                    }
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        diagnostics.Error(repoPath, "스키마는 JSON 객체다");
                        continue;
                    }
                    if (!doc.RootElement.TryGetProperty("$schema", out JsonElement draft) || draft.ValueKind != JsonValueKind.String || draft.GetString() != Draft)
                    {
                        diagnostics.Error(repoPath, "\"$schema\" 는 \"" + Draft + "\" 이다");
                        continue;
                    }
                    if (!doc.RootElement.TryGetProperty("$id", out JsonElement id) || id.ValueKind != JsonValueKind.String || id.GetString() != SchemaIdBase + name)
                    {
                        diagnostics.Error(repoPath, "\"$id\" 는 \"" + SchemaIdBase + name + "\" 이다");
                        continue;
                    }
                }
                JsonSchema schema;
                try
                {
                    schema = JsonSchema.FromText(text);
                }
                catch (Exception e) when (e is JsonException || e is ArgumentException || e is InvalidOperationException)
                {
                    diagnostics.Error(repoPath, "스키마로 읽지 못했다 — " + e.Message);
                    continue;
                }
                options.SchemaRegistry.Register(schema);
                result[name] = schema;
            }
            return result;
        }

        private static string SchemaPath(string name) => DataLoader.DataDir + "/" + DataLoader.SchemaDir + "/" + name;
    }
}
