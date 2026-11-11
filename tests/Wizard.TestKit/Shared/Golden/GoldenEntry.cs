using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Wizard.TestKit.Shared.Golden
{
    // 골든 항목 하나: tests/golden/m<N>/<이름>/ 폴더.
    //   golden.json    항목 정의(씨앗, 설정, 입력 기록 경로). 사람이 쓴다
    //   record.wzrc    입력 기록(CONTRACTS 11.1). 결투는 .wzdr
    //   expected.json  기대 해시 목록. "다시 만드는 파일"(PIPELINE 4.4, T-41). 손으로 고치지 않는다
    // 형식은 tests/golden/README.md 에 있다. 이 로더를 dotnet 테스트와 Unity EditMode 가 같이 쓴다(ARCHITECTURE 3.3).
    public sealed class GoldenEntry
    {
        public const int FormatVersion = 1;
        public const string EntryFileName = "golden.json";
        public const string ExpectedFileName = "expected.json";

        // [임시] 지금 아는 모드. 새 모드가 생기면 여기에 더한다(TestKit 주인).
        private static readonly string[] Kinds = { "endless", "practice", "duel" };

        private GoldenEntry(string id, string kind, ulong seed, GoldenSetup setup, string record, string? notes,
                            string directory, IReadOnlyList<GoldenHash> expected)
        {
            Id = id;
            Kind = kind;
            Seed = seed;
            Setup = setup;
            Record = record;
            Notes = notes;
            Directory = directory;
            Expected = expected;
        }

        // 골든 뿌리 기준 상대 경로. 예: "m1/practice-solo"
        public string Id { get; }

        // endless | practice | duel
        public string Kind { get; }

        public ulong Seed { get; }

        public GoldenSetup Setup { get; }

        // 항목 폴더 기준 상대 경로('/' 구분). 예: "record.wzrc"
        public string Record { get; }

        public string? Notes { get; }

        // 항목 폴더의 절대 경로
        public string Directory { get; }

        // expected.json 의 해시 목록. 틱 오름차순, 비어 있지 않다
        public IReadOnlyList<GoldenHash> Expected { get; }

        public string RecordPath => RepoRoot.Resolve(Directory, Record);

        public byte[] ReadRecord() => File.ReadAllBytes(RecordPath);

        public GoldenDiff? Compare(IReadOnlyList<GoldenHash> actual) => GoldenDiff.Compare(Expected, actual);

        public static GoldenEntry Load(string entryDirectory, string id)
        {
            string goldenPath = Path.Combine(entryDirectory, EntryFileName);
            string expectedPath = Path.Combine(entryDirectory, ExpectedFileName);
            var problems = new List<string>();
            if (!File.Exists(goldenPath))
            {
                problems.Add(id + ": " + EntryFileName + " is missing");
                GoldenFormatException.ThrowIfAny(problems);
            }
            string? expectedJson = File.Exists(expectedPath) ? File.ReadAllText(expectedPath, Encoding.UTF8) : null;
            return Parse(id, entryDirectory, File.ReadAllText(goldenPath, Encoding.UTF8), expectedJson, true);
        }

        // 파일 없이 내용으로 읽는다. checkFiles 가 참이면 입력 기록 파일이 있는지도 본다.
        public static GoldenEntry Parse(string id, string entryDirectory, string goldenJson, string? expectedJson, bool checkFiles)
        {
            var problems = new List<string>();
            var g = new StrictJson(id + "/" + EntryFileName, problems);
            JObject? root = ParseRoot(g, goldenJson);
            string kind = "";
            ulong seed = 0;
            string record = "";
            string? notes = null;
            GoldenSetup setup = GoldenSetup.Empty;
            if (root != null)
            {
                g.OnlyFields(root, "format", "id", "kind", "seed", "setup", "record", "notes");
                g.Format(root, FormatVersion);
                string? declaredId = g.String(root, "id", true);
                if (declaredId != null && !string.Equals(declaredId, id, StringComparison.Ordinal))
                {
                    g.Problem("id '" + declaredId + "' does not match the folder '" + id + "'");
                }
                kind = g.String(root, "kind", true) ?? "";
                if (kind.Length > 0 && Array.IndexOf(Kinds, kind) < 0)
                {
                    g.Problem("kind '" + kind + "' is not one of " + string.Join(", ", Kinds));
                }
                string? seedText = g.String(root, "seed", true);
                if (seedText != null && !HashText.TryParseSeed(seedText, out seed))
                {
                    g.Problem("seed '" + seedText + "' must be \"0x\" + 1..16 hex digits or a decimal u64");
                }
                setup = GoldenSetup.Read(g, g.Object(root, "setup", true));
                record = g.String(root, "record", true) ?? "";
                if (record.Length > 0)
                {
                    CheckRecordPath(g, record);
                    if (checkFiles && !File.Exists(RepoRoot.Resolve(entryDirectory, record)))
                    {
                        g.Problem("record file '" + record + "' does not exist");
                    }
                }
                notes = g.String(root, "notes", false);
            }

            IReadOnlyList<GoldenHash> expected = Array.Empty<GoldenHash>();
            var e = new StrictJson(id + "/" + ExpectedFileName, problems);
            if (expectedJson == null)
            {
                e.Problem("missing (generate it with `replaycli golden --update`, then add a line to tests/golden/CHANGELOG.md)");
            }
            else
            {
                JObject? ex = ParseRoot(e, expectedJson);
                if (ex != null)
                {
                    e.OnlyFields(ex, "format", "id", "hashes");
                    e.Format(ex, FormatVersion);
                    string? exId = e.String(ex, "id", true);
                    if (exId != null && !string.Equals(exId, id, StringComparison.Ordinal))
                    {
                        e.Problem("id '" + exId + "' does not match the folder '" + id + "'");
                    }
                    expected = GoldenHashList.Read(e, e.Array(ex, "hashes", true), "hashes", problems);
                }
            }
            GoldenFormatException.ThrowIfAny(problems);
            return new GoldenEntry(id, kind, seed, setup, record, notes, entryDirectory, expected);
        }

        // expected.json 의 바이트(replaycli golden --update 가 쓴다). 같은 해시면 같은 바이트다.
        public static string FormatExpected(string id, IReadOnlyList<GoldenHash> hashes)
        {
            using (StringWriter text = JsonFiles.CreateText())
            {
                using (Newtonsoft.Json.JsonTextWriter w = JsonFiles.CreateWriter(text))
                {
                    w.WriteStartObject();
                    w.WritePropertyName("format");
                    w.WriteValue(FormatVersion);
                    w.WritePropertyName("id");
                    w.WriteValue(id);
                    w.WritePropertyName("hashes");
                    GoldenHashList.Write(w, hashes);
                    w.WriteEndObject();
                }
                return text.ToString() + "\n";
            }
        }

        internal static JObject? ParseRoot(StrictJson j, string json)
        {
            try
            {
                return JsonFiles.ParseObject(json);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                j.Problem("not a JSON object: " + ex.Message);
                return null;
            }
        }

        private static void CheckRecordPath(StrictJson j, string record)
        {
            bool bad = record.StartsWith("/", StringComparison.Ordinal) || record.IndexOf('\\') >= 0 || record.IndexOf(':') >= 0;
            foreach (string part in record.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    bad = true;
                }
            }
            if (bad)
            {
                j.Problem("record '" + record + "' must be a relative path inside the entry folder ('/' separators, no '..')");
            }
            if (!record.EndsWith(".wzrc", StringComparison.Ordinal) && !record.EndsWith(".wzdr", StringComparison.Ordinal))
            {
                j.Problem("record '" + record + "' must be a .wzrc (or .wzdr for a duel) file");
            }
        }
    }
}
