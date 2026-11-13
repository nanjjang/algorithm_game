#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wizard.Content;
using Wizard.TestKit.Shared.Arith;
using Wizard.TestKit.Shared.Golden;
using Wizard.Unity.App.Headless;

namespace Wizard.Unity.Tests.EditMode.Golden
{
    // Unity Mono(편집기)의 골든 열(T-30, ARCHITECTURE 11절, PIPELINE 7.3 "런타임 × 아키텍처"·"산술 경계").
    // tests/golden/ 의 모든 항목과 산술 경계 벡터를 이 편집기 프로세스에서 돌려 기대 해시·기대 값과 비교하고,
    // 조합 결과 파일(artifacts/golden/golden-result.macos-mono-editor-<arch>.json)을 남긴다. unity-golden.sh 가 이 파일을 다른 열과 한 표로 비교한다.
    public sealed class GoldenEditModeTests
    {
        private static ContentDb? s_db;

        private static ContentDb Db => s_db ??= GoldenPack.LoadDb();

        [Test]
        public void EveryGoldenAndArithCaseMatchesOnTheEditorCombo()
        {
            GoldenCatalog catalog = GoldenCatalog.Load(GoldenPack.GoldenRoot);
            Assert.That(catalog.Entries.Count, Is.GreaterThan(0), "no golden entries under " + catalog.Root);
            Assert.That(catalog.ArithBundles.Count, Is.GreaterThan(0), "no arith bundles under " + catalog.Root);

            GoldenCombo combo = RuntimeCombo.Detect();
            Assert.That(combo.Host, Is.EqualTo("editor"));
            Assert.That(combo.Runtime, Is.EqualTo("mono"));

            GoldenRunResult result = GoldenSuite.Run(catalog, Db, combo);
            string path = result.SaveToArtifacts(GoldenPack.Repo);

            // 남긴 파일을 다시 읽어 그것으로 대조한다(unity-golden.sh 가 읽는 것과 같은 바이트)
            GoldenRunResult back = GoldenRunResult.Load(path);
            Assert.That(back.ToJson(), Is.EqualTo(result.ToJson()));
            Assert.That(back.Combo.Name, Is.EqualTo(combo.Name));
            Assert.That(Path.GetFileName(path), Is.EqualTo("golden-result." + combo.Name + ".json"));

            GoldenComparison cmp = GoldenComparison.Compare(catalog, new[] { back }, new[] { combo });
            Assert.That(cmp.Passed, Is.True, cmp.Table + string.Join("\n", cmp.Failures));
        }

        // 결과에는 실행마다 바뀌는 값이 없다: 같은 프로세스에서 두 번 돌린 결과 파일의 바이트가 같다
        [Test]
        public void TheResultIsByteStableAcrossRuns()
        {
            GoldenCatalog catalog = GoldenCatalog.Load(GoldenPack.GoldenRoot);
            GoldenEntry first = catalog.Entries[0];
            GoldenCombo combo = RuntimeCombo.Detect();
            var one = new GoldenRunResult(combo, new[] { GoldenSuite.RunEntry(Db, first) }, Array.Empty<ArithCaseResult>());
            var two = new GoldenRunResult(combo, new[] { GoldenSuite.RunEntry(Db, first) }, Array.Empty<ArithCaseResult>());
            Assert.That(two.ToJson(), Is.EqualTo(one.ToJson()));
        }

        // 기록 판독기(GoldenRecord)가 코어의 코덱이 센 수와 맞고, 기대 목록이 기록의 끝에서 끝난다
        [Test]
        public void EveryGoldenRecordDecodesAndTheExpectedListEndsAtItsEndTick()
        {
            GoldenCatalog catalog = GoldenCatalog.Load(GoldenPack.GoldenRoot);
            foreach (GoldenEntry e in catalog.Entries)
            {
                GoldenRecord record = GoldenRecord.Load(e.ReadRecord(), GoldenReplay.Limits);
                Assert.That(record.IsDuel, Is.EqualTo(e.Kind == "duel"), e.Id);
                Assert.That(e.Expected[e.Expected.Count - 1].Tick, Is.EqualTo(record.EndTick), e.Id);
                Assert.That(record.InputCount + record.CommandCount, Is.GreaterThan(0), e.Id + " has no input at all");
            }
        }

        // 어긋난 설정은 돌리기 전에 거절한다(dotnet 의 GoldenTests.GoldenRunnerRejectsAMismatchedSetup 과 같은 경우들)
        [Test]
        public void PrepareRejectsAMismatchedSetup()
        {
            GoldenEntry entry = GoldenCatalog.Load(GoldenPack.GoldenRoot).Get("m1/practice-solo");
            byte[] record = entry.ReadRecord();
            Assert.Throws<FormatException>(() => GoldenReplay.Prepare(Db, entry.Id, "practice", 0, Setup("{ \"hashEveryTicks\": 300, \"players\": 2 }"), record));
            Assert.Throws<FormatException>(() => GoldenReplay.Prepare(Db, entry.Id, "practice", 0, Setup("{ \"hashEveryTick\": 300, \"players\": 1 }"), record));
            Assert.Throws<FormatException>(() => GoldenReplay.Prepare(Db, entry.Id, "practice", 0, Setup("{ \"hashEveryTicks\": 0, \"players\": 1 }"), record));
            GoldenSetup ok = Setup("{ \"hashEveryTicks\": 300, \"players\": 1 }");
            Assert.DoesNotThrow(() => GoldenReplay.Prepare(Db, entry.Id, "practice", 0, ok, record));
            Assert.Throws<FormatException>(() => GoldenReplay.Prepare(Db, entry.Id, "practice", 1, ok, record));      // 씨앗이 다르다
            Assert.Throws<FormatException>(() => GoldenReplay.Prepare(Db, entry.Id, "duel", 0, ok, record));          // .wzrc 로 결투
            Assert.Throws<FormatException>(() => GoldenReplay.Prepare(Db, entry.Id, "arena", 0, ok, record));         // 모르는 종류
        }

        // 산술 묶음의 연산 이름은 전부 dotnet 열과 같은 표(TestKit.Shared 의 CoreArithOps)에 있다. 없으면 UnknownOp 으로 실패하지만
        // 그 실패가 "표에 이름이 빠졌다"인지 바로 보이게 따로 본다
        [Test]
        public void EveryOpInTheGoldenBundlesIsInTheSharedTable()
        {
            GoldenCatalog catalog = GoldenCatalog.Load(GoldenPack.GoldenRoot);
            int cases = 0;
            foreach (ArithBundle b in catalog.ArithBundles)
            {
                foreach (ArithCase c in b.Cases)
                {
                    cases++;
                    Assert.That(Array.IndexOf(CoreArithOps.Ops, c.Op), Is.GreaterThanOrEqualTo(0), b.Name + "/" + c.Id + ": " + c.Op);
                }
            }
            Assert.That(cases, Is.GreaterThan(0));
        }

        // 깨진 기록은 예외로 새지 않고 그 항목의 error 가 된다(결과 파일은 끝까지 쓴다)
        [Test]
        public void ACorruptRecordBecomesAnEntryError()
        {
            GoldenEntry entry = GoldenCatalog.Load(GoldenPack.GoldenRoot).Get("m1/practice-solo");
            string dir = TempGolden.CopyEntries(entry);
            try
            {
                string recordPath = Path.Combine(dir, "m1", "practice-solo", entry.Record);
                byte[] bytes = File.ReadAllBytes(recordPath);
                bytes[bytes.Length / 2] ^= 0x40;
                File.WriteAllBytes(recordPath, bytes);
                GoldenEntry broken = GoldenCatalog.Load(dir).Get(entry.Id);

                GoldenEntryResult r = GoldenSuite.RunEntry(Db, broken);
                Assert.That(r.Error, Does.StartWith("FormatException: not a valid .wzrc"));
                Assert.That(r.Hashes, Is.Empty);
            }
            finally
            {
                TempGolden.Delete(dir);
            }
        }

        private static GoldenSetup Setup(string setupJson)
        {
            string golden = "{ \"format\": 1, \"id\": \"m1/x\", \"kind\": \"practice\", \"seed\": \"0x0\", \"setup\": " + setupJson +
                            ", \"record\": \"record.wzrc\" }";
            string expected = GoldenEntry.FormatExpected("m1/x", new List<GoldenHash> { new GoldenHash(1, 1, null) });
            return GoldenEntry.Parse("m1/x", Path.GetTempPath(), golden, expected, false).Setup;
        }
    }
}
