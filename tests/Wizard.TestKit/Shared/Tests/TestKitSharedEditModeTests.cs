using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wizard.TestKit.Shared;
using Wizard.TestKit.Shared.Arith;
using Wizard.TestKit.Shared.Golden;
using Wizard.TestKit.Shared.Vectors;

namespace Wizard.TestKit.Shared.Tests
{
    // Unity EditMode 에서 TestKit.Shared 의 같은 소스(골든 로더, 벡터 규칙)가 컴파일되고 같은 답을 내는지 본다.
    // dotnet 쪽 같은 검사: tests/Wizard.TestKit/SelfTests/. 이 파일은 dotnet 이 컴파일하지 않는다(Shared csproj 에서 뺐다).
    // 실제 골든 재실행은 M1-UE-03 의 unity/Assets/Tests/EditMode/Golden/ 이 이 로더로 한다.
    public sealed class TestKitSharedEditModeTests
    {
        private static string Root => RepoRoot.Find(UnityEngine.Application.dataPath);

        private static string SampleRoot => RepoRoot.Resolve(Root, "tests/Wizard.TestKit/SelfTests/Samples/golden");

        [Test]
        public void GoldenLoader_ReadsTheSampleCatalog()
        {
            GoldenCatalog c = GoldenCatalog.Load(SampleRoot);
            Assert.AreEqual(2, c.Entries.Count);
            GoldenEntry e = c.Get("m0/sample-endless");
            Assert.AreEqual("endless", e.Kind);
            Assert.AreEqual(0xc0ffee01UL, e.Seed);
            Assert.AreEqual(1L, e.Setup.GetInt64("players"));
            Assert.AreEqual("2026-11-12T00:00:00Z", e.Setup.GetString("startedAt"));
            Assert.IsTrue(File.Exists(e.RecordPath));
            Assert.AreEqual(2, e.Expected.Count);
            Assert.AreEqual(1800u, e.Expected[1].Tick);
            Assert.AreEqual(0xfedcba9876543210UL, e.Expected[1].Hash);
            Assert.AreEqual(ulong.MaxValue, c.Get("m0/sample-duel").Seed);
            Assert.IsNull(e.Compare(e.Expected));
        }

        [Test]
        public void GoldenLoader_ReadsTheRepositoryGoldenFolder()
        {
            GoldenCatalog c = GoldenCatalog.Load(RepoRoot.Resolve(Root, "tests/golden"));
            Assert.IsNotNull(c);
        }

        [Test]
        public void ResultFile_IsByteIdenticalToTheDotnetWriter()
        {
            GoldenCatalog c = GoldenCatalog.Load(SampleRoot);
            var entries = new List<GoldenEntryResult>();
            foreach (GoldenEntry e in c.Entries)
            {
                entries.Add(new GoldenEntryResult(e.Id, e.Expected, null));
            }
            var r = new GoldenRunResult(new GoldenCombo("macos", "mono", "editor", "arm64"), entries, new List<ArithCaseResult>());
            string json = r.ToJson();
            Assert.AreEqual(json, GoldenRunResult.Parse(json, "editor").ToJson());
            string expected = File.ReadAllText(Path.Combine(SampleRoot, "m0", "sample-endless", "expected.json"));
            Assert.AreEqual(expected, GoldenEntry.FormatExpected("m0/sample-endless", c.Get("m0/sample-endless").Expected));
        }

        [Test]
        public void VectorRules_ScanAllFourteenSpecsWithoutProblems()
        {
            VectorConfig config = VectorConfig.LoadFromRepo(Root);
            SpecVectorScan scan = SpecVectorScanner.ScanRepo(Root, config);
            Assert.IsEmpty(scan.Problems, string.Join("\n", scan.Problems));
            Assert.AreEqual(14, config.Specs.Count);
            foreach (string spec in config.Specs)
            {
                Assert.IsNotEmpty(scan.InSpec(spec), "spec " + spec);
            }
            Assert.IsTrue(VectorNames.TryParseTestName("S01_T_H04__LockAtExactly100", out string s, out string id));
            Assert.AreEqual("01", s);
            Assert.AreEqual("T-H04", id);
        }
    }
}
