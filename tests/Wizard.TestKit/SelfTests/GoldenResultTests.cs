using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wizard.TestKit.Shared.Arith;
using Wizard.TestKit.Shared.Golden;
using Xunit;
using Xunit.Sdk;

namespace Wizard.TestKit.SelfTests
{
    // (런타임, 아키텍처) 조합별 결과 파일과 조합 대조(T-30, T-39)
    public sealed class GoldenResultTests
    {
        private static GoldenCatalog Sample() => GoldenCatalog.Load(GoldenFormatTests.SampleRoot);

        // 표본 골든을 그대로 맞힌 결과(산술은 실제 코어 나눗셈 도우미로 돌린다)
        private static GoldenRunResult Matching(GoldenCombo combo, GoldenCatalog c)
        {
            var entries = c.Entries.Select(e => new GoldenEntryResult(e.Id, e.Expected.ToArray(), null)).ToList();
            var arith = new List<ArithCaseResult>();
            foreach (ArithBundle b in c.ArithBundles)
            {
                arith.AddRange(ArithRunner.Run(b, new ArithBundleTests.CoreOps()));
            }
            return new GoldenRunResult(combo, entries, arith);
        }

        private static readonly GoldenCombo Arm = new GoldenCombo("macos", "dotnet", "test", "arm64");
        private static readonly GoldenCombo X64 = new GoldenCombo("macos", "mono", "player", "x64");
        private static readonly GoldenCombo Il2cpp = new GoldenCombo("macos", "il2cpp", "player", "arm64");

        [Fact]
        public void Combo_NamesRoundTrip_AndRejectUnknownParts()
        {
            Assert.Equal("macos-dotnet-test-arm64", Arm.Name);
            Assert.Equal("golden-result.macos-mono-player-x64.json", X64.ResultFileName);
            Assert.True(GoldenCombo.TryParse("windows-mono-player-x64", out GoldenCombo w));
            Assert.Equal("windows", w.Os);
            Assert.False(GoldenCombo.TryParse("macos-dotnet-arm64", out _));
            Assert.False(GoldenCombo.TryParse("macos-coreclr-test-arm64", out _));
            Assert.Throws<ArgumentException>(() => new GoldenCombo("macos", "dotnet", "test", "riscv"));
            Assert.Equal("dotnet", DotnetCombo.Current().Runtime);
        }

        [Fact]
        public void ResultFile_RoundTrips_AndIsByteStable()
        {
            GoldenCatalog c = Sample();
            GoldenRunResult r = Matching(Arm, c);
            string json = r.ToJson();
            Assert.Equal(json, Matching(Arm, c).ToJson());
            Assert.DoesNotContain("\r", json);
            GoldenRunResult back = GoldenRunResult.Parse(json, "x.json");
            Assert.Equal(json, back.ToJson());
            Assert.Equal(Arm, back.Combo);
            Assert.Equal(c.Entries.Select(e => e.Id), back.Entries.Select(e => e.Id));
            Assert.Equal("SimInvariant", back.Arith.Single(a => a.CaseId == "divint-by-zero").Fault);
            Assert.Equal("-2147483648", back.Arith.Single(a => a.CaseId == "divint-min-neg1").Values!.Single().Text);

            string dir = TempDir.Create();
            try
            {
                string path = Path.Combine(dir, Arm.ResultFileName);
                r.Save(path);
                Assert.Equal(json, File.ReadAllText(path));
                Assert.Equal(json, GoldenRunResult.Load(path).ToJson());
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void ResultFile_WithErrorsAndFaults_RoundTrips()
        {
            var r = new GoldenRunResult(X64,
                new[] { new GoldenEntryResult("m0/sample-duel", Array.Empty<GoldenHash>(), "RecordCorrupt: bad magic") },
                new[] { new ArithCaseResult("sample", "divint-by-zero", null, "Exception:System.DivideByZeroException") });
            GoldenRunResult back = GoldenRunResult.Parse(r.ToJson(), "x.json");
            Assert.Equal("RecordCorrupt: bad magic", back.Entries.Single().Error);
            Assert.Equal("Exception:System.DivideByZeroException", back.Arith.Single().Fault);
        }

        [Theory]
        [InlineData(@"{ ""format"": 1, ""combo"": ""macos-dotnet-arm64"", ""entries"": [], ""arith"": [] }", "combo")]
        [InlineData(@"{ ""format"": 1, ""combo"": ""macos-dotnet-test-arm64"", ""entries"": [ { ""id"": ""a"" } ], ""arith"": [] }", "exactly one of")]
        [InlineData(@"{ ""format"": 1, ""combo"": ""macos-dotnet-test-arm64"", ""entries"": [ { ""id"": ""a"", ""error"": ""x"" }, { ""id"": ""a"", ""error"": ""y"" } ], ""arith"": [] }", "duplicate entry")]
        [InlineData(@"{ ""format"": 1, ""combo"": ""macos-dotnet-test-arm64"", ""entries"": [], ""arith"": [], ""when"": ""today"" }", "unknown field 'when'")]
        public void ResultFile_FormatProblems_AreReported(string json, string expected)
        {
            GoldenFormatException ex = Assert.Throws<GoldenFormatException>(() => GoldenRunResult.Parse(json, "x.json"));
            Assert.Contains(ex.Problems, p => p.Contains(expected));
        }

        [Fact]
        public void Comparison_PassesWhenEveryComboMatches()
        {
            GoldenCatalog c = Sample();
            GoldenComparison cmp = GoldenComparison.Compare(c, new[] { Matching(Arm, c), Matching(X64, c), Matching(Il2cpp, c) },
                new[] { Arm, X64, Il2cpp });
            Assert.True(cmp.Passed, string.Join("\n", cmp.Failures));
            GoldenAssert.Passed(cmp);
            Assert.Contains("macos-il2cpp-player-arm64", cmp.Table);
            Assert.Equal(1 + c.Entries.Count + c.ArithBundles.Sum(b => b.Cases.Count), cmp.Table.TrimEnd('\n').Split('\n').Length);
        }

        [Fact]
        public void Comparison_FailsOnAHashThatDiffersInOneCombo()
        {
            GoldenCatalog c = Sample();
            GoldenRunResult good = Matching(Arm, c);
            var bad = new GoldenRunResult(X64, good.Entries.Select(e => e.Id != "m0/sample-endless" ? e
                : new GoldenEntryResult(e.Id, new[] { e.Hashes[0], new GoldenHash(e.Hashes[1].Tick, e.Hashes[1].Hash ^ 1UL, null) }, null)).ToList(),
                good.Arith);
            GoldenComparison cmp = GoldenComparison.Compare(c, new[] { good, bad });
            Assert.False(cmp.Passed);
            Assert.Single(cmp.Failures);
            Assert.Contains("macos-mono-player-x64: m0/sample-endless", cmp.Failures[0]);
            Assert.Contains("tick 1800", cmp.Failures[0]);
            Assert.Contains("diff@1800", cmp.Table);
            Assert.Throws<XunitException>(() => GoldenAssert.Passed(cmp));
        }

        [Fact]
        public void Comparison_FailsOnArithFaultMissingUnknownAndDuplicateCombos()
        {
            GoldenCatalog c = Sample();
            GoldenRunResult good = Matching(Arm, c);
            var arith = good.Arith.Select(a => a.CaseId == "divint-by-zero"
                ? new ArithCaseResult(a.Bundle, a.CaseId, null, "Exception:System.DivideByZeroException") : a).ToList();
            arith.Add(new ArithCaseResult("sample", "not-in-bundle", new[] { ArithValue.FromInt32(1) }, null));
            var entries = good.Entries.Skip(1).ToList();
            entries.Add(new GoldenEntryResult("m9/ghost", new[] { new GoldenHash(1, 1, null) }, null));
            var weird = new GoldenRunResult(X64, entries, arith);
            GoldenComparison cmp = GoldenComparison.Compare(c, new[] { good, weird, good }, new[] { Arm, Il2cpp });
            string all = string.Join("\n", cmp.Failures);
            Assert.False(cmp.Passed);
            Assert.Contains("divint-by-zero: expected fault SimInvariant, got fault Exception:System.DivideByZeroException", all);
            Assert.Contains("unknown arith case 'sample/not-in-bundle'", all);
            Assert.Contains("m0/sample-duel was not run", all);
            Assert.Contains("unknown golden 'm9/ghost'", all);
            Assert.Contains("combo macos-dotnet-test-arm64 appears twice", all);
            Assert.Contains("combo macos-il2cpp-player-arm64 has no result file", all);
            Assert.False(GoldenComparison.Compare(c, Array.Empty<GoldenRunResult>()).Passed);
        }

        public static TheoryData<string> SampleIds => GoldenData.Ids(GoldenCatalog.Load(GoldenFormatTests.SampleRoot), "m0");

        [Theory]
        [MemberData(nameof(SampleIds))]
        public void GoldenData_GivesOneTheoryRowPerEntry(string id)
        {
            GoldenEntry e = Sample().Get(id);
            GoldenAssert.Matches(e, e.Expected.ToArray());
        }

        [Fact]
        public void GoldenAssert_Matches_ReportsTheTick()
        {
            GoldenEntry e = Sample().Get("m0/sample-endless");
            GoldenAssert.Matches(e, e.Expected.ToArray());
            XunitException ex = Assert.Throws<XunitException>(() => GoldenAssert.Matches(e, new[] { new GoldenHash(300, 5, null) }));
            Assert.Contains("tick 300", ex.Message);
            Assert.Contains("CHANGELOG", ex.Message);
        }
    }
}
