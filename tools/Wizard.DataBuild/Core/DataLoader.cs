using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Wizard.Content;
using Wizard.DataBuild.Names;

namespace Wizard.DataBuild.Core
{
    // data/ 를 읽는다(ARCHITECTURE 2절의 꼴):
    //   data/sim/<절>.json, data/rules/<절>.json   팩 절(SectionFiles). 절 목차에 없는 파일, 하위 폴더, .json 이 아닌 파일은 오류
    //   data/presentation/**.json                  팩에 들지 않는 표시 연결. 스키마는 presentation/ 바로 아래 이름(<이름>.schema.json)
    //   data/names/names.ko.json                   이름 표(NamesFile)
    //   data/schema/*.schema.json                  스키마(SchemaCheck)
    // 그 밖의 폴더·파일이 data/ 에 있으면 오류다. 모든 나열은 경로의 서수 오름차순이다.
    // JSON 은 주석·끝 쉼표 없이 읽고, 한 객체 안의 같은 열쇠는 오류다(System.Text.Json 은 그냥 받아 준다).
    public static class DataLoader
    {
        public const string DataDir = "data";
        public const string PresentationDir = "presentation";
        public const string NamesDir = "names";
        public const string SchemaDir = "schema";
        public const string SchemaSuffix = ".schema.json";

        private const int MaxDepth = 64;

        public static DataSet Load(string root, Diagnostics diagnostics)
        {
            string data = Path.Combine(root, DataDir);
            var files = new List<DataFile>();
            if (!Directory.Exists(data))
            {
                diagnostics.Error(DataDir + "/", "data/ 폴더가 없다");
                return new DataSet(root, files, null);
            }
            foreach (string entry in SortedEntries(data))
            {
                string name = Path.GetFileName(entry);
                if (!Directory.Exists(entry))
                {
                    diagnostics.Error(DataDir + "/" + name, "data/ 바로 아래에는 파일을 두지 않는다(sim, rules, presentation, names, schema 폴더만)");
                    continue;
                }
                switch (name)
                {
                    case SectionFiles.SimDir:
                    case SectionFiles.RulesDir:
                        LoadSectionDir(entry, name, files, diagnostics);
                        break;
                    case PresentationDir:
                        LoadPresentation(entry, PresentationDir, files, diagnostics);
                        break;
                    case NamesDir:
                    case SchemaDir:
                        break;
                    default:
                        diagnostics.Error(DataDir + "/" + name + "/", "data/ 에 알 수 없는 폴더(sim, rules, presentation, names, schema 만)");
                        break;
                }
            }
            files.Sort((a, b) => string.CompareOrdinal(a.DataPath, b.DataPath));
            NamesFile? names = NamesFile.LoadFromRepo(root, diagnostics);
            return new DataSet(root, files, names);
        }

        private static void LoadSectionDir(string dir, string dirName, List<DataFile> files, Diagnostics diagnostics)
        {
            foreach (string entry in SortedEntries(dir))
            {
                string rel = dirName + "/" + Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    diagnostics.Error(DataDir + "/" + rel + "/", "data/" + dirName + "/ 에는 하위 폴더를 두지 않는다");
                    continue;
                }
                if (!SectionFiles.TryFromDataFile(rel, out ContentSection section))
                {
                    diagnostics.Error(DataDir + "/" + rel, "절 목차(CONTRACTS 11.2, SectionFiles)에 없는 파일이다");
                    continue;
                }
                string schema = Path.GetFileNameWithoutExtension(entry) + SchemaSuffix;
                Add(entry, rel, section, schema, files, diagnostics);
            }
        }

        private static void LoadPresentation(string dir, string rel, List<DataFile> files, Diagnostics diagnostics)
        {
            foreach (string entry in SortedEntries(dir))
            {
                string childRel = rel + "/" + Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    LoadPresentation(entry, childRel, files, diagnostics);
                    continue;
                }
                if (!childRel.EndsWith(".json", StringComparison.Ordinal))
                {
                    diagnostics.Error(DataDir + "/" + childRel, "data/presentation/ 에는 .json 만 둔다");
                    continue;
                }
                // presentation/<이름>.json 또는 presentation/<이름>/… → <이름>.schema.json
                string afterTop = childRel.Substring(PresentationDir.Length + 1);
                int slash = afterTop.IndexOf('/');
                string head = slash < 0 ? afterTop.Substring(0, afterTop.Length - ".json".Length) : afterTop.Substring(0, slash);
                Add(entry, childRel, ContentSection.None, head + SchemaSuffix, files, diagnostics);
            }
        }

        private static void Add(string path, string rel, ContentSection section, string schema, List<DataFile> files, Diagnostics diagnostics)
        {
            string repoPath = DataDir + "/" + rel;
            if (!rel.EndsWith(".json", StringComparison.Ordinal))
            {
                diagnostics.Error(repoPath, ".json 이 아니다");
                return;
            }
            JsonDocument doc;
            try
            {
                doc = Parse(File.ReadAllBytes(path));
            }
            catch (JsonException e)
            {
                diagnostics.Error(repoPath, "JSON 이 아니다 — " + e.Message);
                return;
            }
            int before = diagnostics.Count;
            CheckDuplicateKeys(doc.RootElement, "", repoPath, diagnostics);
            if (diagnostics.Count != before)
            {
                doc.Dispose();
                return;
            }
            files.Add(new DataFile(rel, section, schema, doc));
        }

        public static JsonDocument Parse(byte[] utf8) =>
            JsonDocument.Parse(utf8, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = MaxDepth });

        // 한 객체 안의 같은 열쇠
        public static void CheckDuplicateKeys(JsonElement e, string pointer, string repoPath, Diagnostics diagnostics)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JsonProperty p in e.EnumerateObject())
                    {
                        string child = pointer + "/" + JsonPointer.Escape(p.Name);
                        if (!seen.Add(p.Name))
                        {
                            diagnostics.Error(repoPath, child, "같은 열쇠 '" + p.Name + "' 가 한 객체에 두 번 있다");
                        }
                        CheckDuplicateKeys(p.Value, child, repoPath, diagnostics);
                    }
                    break;
                case JsonValueKind.Array:
                    int i = 0;
                    foreach (JsonElement item in e.EnumerateArray())
                    {
                        CheckDuplicateKeys(item, pointer + "/" + i.ToString(CultureInfo.InvariantCulture), repoPath, diagnostics);
                        i++;
                    }
                    break;
            }
        }

        // 폴더의 항목(파일·폴더)을 이름의 서수 오름차순으로. 숨김 파일(.DS_Store 등 '.' 으로 시작)은 보지 않는다
        public static List<string> SortedEntries(string dir)
        {
            var list = new List<string>();
            foreach (string e in Directory.EnumerateFileSystemEntries(dir))
            {
                if (!Path.GetFileName(e).StartsWith(".", StringComparison.Ordinal))
                {
                    list.Add(e);
                }
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }
}
