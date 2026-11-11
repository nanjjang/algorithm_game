using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wizard.TestKit.Shared;
using Wizard.TestKit.Shared.Golden;
using Xunit;

namespace Wizard.TestKit.SelfTests
{
    // 골든 항목 형식(씨앗, 설정, .wzrc 경로, 기대 해시 목록)의 로더. tests/golden/README.md 의 형식과 같아야 한다.
    public sealed class GoldenFormatTests
    {
        internal static string SampleRoot => TestRepo.Path("tests/Wizard.TestKit/SelfTests/Samples/golden");

        private const string Good = @"{ ""format"": 1, ""id"": ""m1/a"", ""kind"": ""endless"", ""seed"": ""0x10"", ""setup"": { ""players"": 2 }, ""record"": ""record.wzrc"" }";
        private const string GoodExpected = @"{ ""format"": 1, ""id"": ""m1/a"", ""hashes"": [ { ""tick"": 1, ""hash"": ""00000000000000ff"" } ] }";

        private static GoldenEntry Parse(string golden, string? expected = GoodExpected) =>
            GoldenEntry.Parse("m1/a", "/nowhere", golden, expected, false);

        private static string ProblemsOf(Action a)
        {
            GoldenFormatException ex = Assert.Throws<GoldenFormatException>(a);
            return string.Join("\n", ex.Problems);
        }

        [Fact]
        public void SampleCatalog_LoadsEveryField()
        {
            GoldenCatalog c = GoldenCatalog.Load(SampleRoot);
            Assert.Equal(new[] { "m0/sample-duel", "m0/sample-endless" }, c.Entries.Select(e => e.Id).ToArray());

            GoldenEntry e = c.Get("m0/sample-endless");
            Assert.Equal("endless", e.Kind);
            Assert.Equal(0xc0ffee01UL, e.Seed);
            Assert.Equal("record.wzrc", e.Record);
            Assert.True(File.Exists(e.RecordPath));
            Assert.Equal((byte)'W', e.ReadRecord()[0]);
            Assert.Equal(new[] { "character", "phantomFirst", "players", "startedAt" }, e.Setup.Keys.ToArray());
            Assert.Equal(1L, e.Setup.GetInt64("players"));
            Assert.Equal("char.1", e.Setup.GetString("character"));
            Assert.False(e.Setup.GetBool("phantomFirst"));
            Assert.Equal("2026-11-12T00:00:00Z", e.Setup.GetString("startedAt"));   // 날짜처럼 생긴 글자는 글자 그대로
            Assert.Equal(new[] { new GoldenHash(300, 0x0123456789abcdefUL, "wave5"), new GoldenHash(1800, 0xfedcba9876543210UL, "final") },
                e.Expected.ToArray());
            Assert.Equal("wave5", e.Expected[0].Label);

            GoldenEntry d = c.Get("m0/sample-duel");
            Assert.Equal(ulong.MaxValue, d.Seed);
            Assert.Equal(0, d.Setup.Count);
            Assert.Equal("record.wzdr", d.Record);
            Assert.Equal(new[] { "m0/sample-duel", "m0/sample-endless" }, c.InMilestone("m0").Select(x => x.Id).ToArray());
            Assert.Empty(c.InMilestone("m1"));
            Assert.Single(c.ArithBundles);
        }

        [Fact]
        public void RepositoryGoldenFolder_LoadsWithoutProblems()
        {
            GoldenCatalog c = TestRepo.LoadGolden();
            Assert.Equal(TestRepo.GoldenRoot, c.Root);
        }

        [Fact]
        public void Seeds_AcceptHexAndDecimalStrings_Only()
        {
            Assert.Equal(16UL, Parse(Good).Seed);
            Assert.Equal(42UL, Parse(Good.Replace("\"0x10\"", "\"42\"")).Seed);
            Assert.Contains("seed", ProblemsOf(() => Parse(Good.Replace("\"0x10\"", "16"))));
            Assert.Contains("seed", ProblemsOf(() => Parse(Good.Replace("\"0x10\"", "\"0x\""))));
            Assert.Contains("seed", ProblemsOf(() => Parse(Good.Replace("\"0x10\"", "\"0x10000000000000000\""))));
            Assert.Contains("seed", ProblemsOf(() => Parse(Good.Replace("\"0x10\"", "\"-1\""))));
        }

        [Theory]
        [InlineData(@"""players"": 1.5", "real number")]
        [InlineData(@"""players"": 1e3", "real number")]
        [InlineData(@"""players"": [1]", "must be a string, an integer or a boolean")]
        [InlineData(@"""players"": { ""a"": 1 }", "must be a string, an integer or a boolean")]
        [InlineData(@"""players"": 99999999999999999999", "64-bit")]
        public void Setup_RejectsRealsAndStructures(string setupBody, string expected)
        {
            Assert.Contains(expected, ProblemsOf(() => Parse(Good.Replace(@"""players"": 2", setupBody))));
        }

        [Theory]
        [InlineData("../record.wzrc")]
        [InlineData("/abs/record.wzrc")]
        [InlineData("sub\\\\record.wzrc")]
        [InlineData("record.bin")]
        [InlineData("./record.wzrc")]
        public void RecordPath_MustStayInsideTheEntryFolder(string record)
        {
            Assert.Contains("record", ProblemsOf(() => Parse(Good.Replace("record.wzrc", record))));
        }

        [Fact]
        public void RecordFile_MustExistWhenFilesAreChecked()
        {
            Assert.Contains("does not exist", ProblemsOf(() => GoldenEntry.Parse("m1/a", SampleRoot, Good, GoodExpected, true)));
        }

        [Fact]
        public void IdKindFormatAndUnknownFields_AreChecked()
        {
            Assert.Contains("does not match the folder", ProblemsOf(() => Parse(Good.Replace("m1/a", "m1/b"))));
            Assert.Contains("kind 'battle'", ProblemsOf(() => Parse(Good.Replace("endless", "battle"))));
            Assert.Contains("format 2", ProblemsOf(() => Parse(Good.Replace("\"format\": 1", "\"format\": 2"))));
            Assert.Contains("unknown field 'sed'", ProblemsOf(() => Parse(Good.Replace("\"setup\"", "\"sed\": 1, \"setup\""))));
            Assert.Contains("missing string field 'record'", ProblemsOf(() => Parse(Good.Replace(", \"record\": \"record.wzrc\"", ""))));
            Assert.Contains("not a JSON object", ProblemsOf(() => Parse("{ \"format\": 1, ")));
        }

        [Fact]
        public void Expected_IsRequired_AndChecked()
        {
            Assert.Contains("replaycli golden --update", ProblemsOf(() => Parse(Good, null)));
            Assert.Contains("does not match the folder", ProblemsOf(() => Parse(Good, GoodExpected.Replace("m1/a", "m1/z"))));
            Assert.Contains("is empty", ProblemsOf(() => Parse(Good, GoodExpected.Replace("[ { \"tick\": 1, \"hash\": \"00000000000000ff\" } ]", "[]"))));
            Assert.Contains("16 lowercase hex", ProblemsOf(() => Parse(Good, GoodExpected.Replace("00000000000000ff", "00000000000000FF"))));
            Assert.Contains("16 lowercase hex", ProblemsOf(() => Parse(Good, GoodExpected.Replace("00000000000000ff", "ff"))));
            string twice = GoodExpected.Replace("} ]", "}, { \"tick\": 1, \"hash\": \"0000000000000001\" } ]");
            Assert.Contains("increase strictly", ProblemsOf(() => Parse(Good, twice)));
            Assert.Contains("outside", ProblemsOf(() => Parse(Good, GoodExpected.Replace("\"tick\": 1", "\"tick\": 4294967296"))));
        }

        [Fact]
        public void AllProblems_AreReportedTogether()
        {
            string bad = Good.Replace("endless", "battle").Replace("\"0x10\"", "\"zz\"").Replace("record.wzrc", "../x");
            GoldenFormatException ex = Assert.Throws<GoldenFormatException>(() => Parse(bad, null));
            Assert.True(ex.Problems.Count >= 5, string.Join("\n", ex.Problems));
        }

        [Fact]
        public void FormatExpected_RoundTrips_AndIsByteStable()
        {
            var hashes = new[] { new GoldenHash(0, 1, null), new GoldenHash(60, 0xffffffffffffffffUL, "final") };
            string json = GoldenEntry.FormatExpected("m1/a", hashes);
            Assert.Equal(json, GoldenEntry.FormatExpected("m1/a", hashes));
            Assert.DoesNotContain("\r", json);
            Assert.EndsWith("}\n", json);
            Assert.Equal(hashes, Parse(Good, json).Expected.ToArray());
            // 표본 파일은 이 형식 그대로 쓰여 있다(손으로 쓴 파일과 도구가 쓴 파일이 같은 바이트)
            string sample = File.ReadAllText(Path.Combine(SampleRoot, "m0", "sample-endless", "expected.json"));
            GoldenEntry e = GoldenCatalog.Load(SampleRoot).Get("m0/sample-endless");
            Assert.Equal(sample, GoldenEntry.FormatExpected(e.Id, e.Expected));
        }

        [Fact]
        public void Diff_FindsTheFirstDivergence()
        {
            var expected = new[] { new GoldenHash(30, 1, null), new GoldenHash(60, 2, null), new GoldenHash(90, 3, null) };
            Assert.Null(GoldenDiff.Compare(expected, expected.ToArray()));
            GoldenDiff d = GoldenDiff.Compare(expected, new[] { new GoldenHash(30, 1, "x"), new GoldenHash(60, 7, null), new GoldenHash(90, 3, null) })!;
            Assert.Equal(1, d.Index);
            Assert.Contains("tick 60", d.ToString());
            Assert.Contains("0000000000000007", d.ToString());
            GoldenDiff shorter = GoldenDiff.Compare(expected, expected.Take(2).ToArray())!;
            Assert.Equal(2, shorter.Index);
            Assert.Null(shorter.Actual);
            GoldenDiff longer = GoldenDiff.Compare(expected.Take(1).ToArray(), expected)!;
            Assert.Null(longer.Expected);
            GoldenDiff otherTicks = GoldenDiff.Compare(expected, new[] { new GoldenHash(31, 1, null) })!;
            Assert.Contains("tick lists differ", otherTicks.ToString());
        }

        [Fact]
        public void Catalog_RejectsStrayFilesAndFolders()
        {
            string root = TempDir.Create();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "results"));
                Directory.CreateDirectory(Path.Combine(root, "m1", "Bad Name"));
                Directory.CreateDirectory(Path.Combine(root, "m2", "no-expected"));
                File.WriteAllText(Path.Combine(root, "m2", "no-expected", "golden.json"),
                    Good.Replace("m1/a", "m2/no-expected"));
                File.WriteAllText(Path.Combine(root, "m2", "no-expected", "record.wzrc"), "x");
                File.WriteAllText(Path.Combine(root, "notes.txt"), "x");
                Directory.CreateDirectory(Path.Combine(root, "arith"));
                File.WriteAllText(Path.Combine(root, "arith", "x.csv"), "x");
                string problems = ProblemsOf(() => GoldenCatalog.Load(root));
                Assert.Contains("unexpected folder at the golden root: results", problems);
                Assert.Contains("m1/Bad Name: folder name", problems);
                Assert.Contains("m2/no-expected/expected.json: missing", problems);
                Assert.Contains("unexpected file at the golden root: notes.txt", problems);
                Assert.Contains("arith: unexpected file x.csv", problems);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void RepoRoot_IsFoundFromTheTestAssembly_AndNotFromNowhere()
        {
            Assert.True(File.Exists(Path.Combine(TestRepo.Root, "Wizard.sln")));
            Assert.Equal(TestRepo.Root, RepoRoot.Find(Path.Combine(TestRepo.Root, "tests", "golden")));
            Assert.Throws<DirectoryNotFoundException>(() => RepoRoot.Find(Path.GetPathRoot(TestRepo.Root)!));
        }
    }

    internal static class TempDir
    {
        public static string Create()
        {
            string dir = Path.Combine(Path.GetTempPath(), "wz-testkit-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
