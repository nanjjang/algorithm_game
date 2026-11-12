using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wizard.ReplayCli;
using Wizard.TestKit;
using Wizard.TestKit.Shared.Golden;
using Xunit;

namespace Wizard.Sim.Tests.Determinism
{
    // tests/golden/m1/ 의 골든(README: tests/golden/README.md, tests/golden/m1/README.md). 이 프로세스의 조합(macos-dotnet-test-arm64,
    // `dotnet test -a x64` 면 x64)에서 기대 해시와 같아야 한다. 같은 기록을 두 번 돌리기, 스냅숏 저장·복원 뒤 이어 돌리기도 같은 기대 목록과 비교한다.
    public sealed class GoldenTests
    {
        public static TheoryData<string> M1 => GoldenData.Ids("m1");

        [Theory]
        [MemberData(nameof(M1))]
        public void GoldenMatchesOnThisCombo(string id)
        {
            GoldenEntry entry = TestRepo.LoadGolden().Get(id);
            GoldenAssert.Matches(entry, GoldenRunner.Run(DetKit.Db, entry));
            GoldenAssert.Matches(entry, GoldenRunner.Run(DetKit.Db, entry));               // 두 번
        }

        // 판의 가운데쯤에서 스냅숏 저장 → 9틱 진행 → 복원, 4분의 3 지점에서 WriteState → 새 세계 ReadState 를 거쳐도 같은 기대 목록이다
        [Theory]
        [MemberData(nameof(M1))]
        public void GoldenMatchesAfterSnapshotRewindAndStateTransfer(string id)
        {
            GoldenEntry entry = TestRepo.LoadGolden().Get(id);
            uint end = entry.Expected[entry.Expected.Count - 1].Tick;
            uint rewindAt = end / 2 + 3;
            uint transferAt = end / 4 * 3 + 1;
            GoldenAssert.Matches(entry, GoldenRunner.Run(DetKit.Db, entry, (world, feed) =>
            {
                if (world.Tick == rewindAt)
                {
                    SimSnapshot snap = world.CreateSnapshotBuffer();
                    world.SaveSnapshot(snap);
                    Replayer.RunTo(world, feed, rewindAt + 9, new CountingSink());
                    world.LoadSnapshot(snap);
                }
                else if (world.Tick == transferAt)
                {
                    // 상태 바이트를 새 세계에 받아(ReadState) 그 세계의 스냅숏을 원래 세계에 싣는다: 이어 돌리는 상태는 ReadState 가 만든 것이다
                    var bytes = new Wizard.Core.ByteWriter();
                    world.WriteState(bytes);
                    SimWorld fresh = ReplayWorlds.Create(DetKit.Db, ReplayWorlds.ParseKind(entry.Kind), feed);
                    fresh.ReadState(bytes.Written);
                    SimSnapshot snap = fresh.CreateSnapshotBuffer();
                    fresh.SaveSnapshot(snap);
                    world.LoadSnapshot(snap);
                }
            }));
        }

        [Fact]
        public void M1HasAtLeastFiveGoldensIncludingPracticeSoloAndATwoPlayerWorld()
        {
            List<GoldenEntry> m1 = TestRepo.LoadGolden().InMilestone("m1");
            Assert.True(m1.Count >= 5, "m1 needs at least five goldens, has " + m1.Count);
            Assert.Contains(m1, e => e.Kind == "practice" && e.Setup.GetInt64(GoldenRunner.PlayersKey) == 1);
            Assert.Contains(m1, e => e.Setup.GetInt64(GoldenRunner.PlayersKey) == 2);
            Assert.Contains(m1, e => e.Kind == "endless");
        }

        // 기대 목록은 기록의 끝에서 끝난다("final"). 기록은 코어의 문(코덱)을 통과한다
        [Theory]
        [MemberData(nameof(M1))]
        public void GoldenRecordIsValidAndTheListEndsAtItsEnd(string id)
        {
            GoldenEntry entry = TestRepo.LoadGolden().Get(id);
            RecordFeed feed = DetKit.Feed(entry.ReadRecord());
            GoldenHash last = entry.Expected[entry.Expected.Count - 1];
            Assert.Equal(feed.EndTick, last.Tick);
            Assert.Equal(GoldenRunner.FinalLabel, last.Label);
            long every = entry.Setup.GetInt64(GoldenRunner.HashEveryKey);
            Assert.All(entry.Expected.Take(entry.Expected.Count - 1), h => Assert.Equal(0L, h.Tick % every));
            Assert.Equal(ReplayWorlds.WorldSeed(ReplayWorlds.ParseKind(entry.Kind), feed), entry.Seed);
        }

        [Fact]
        public void ReplayCliGoldenCommandPassesAndUpdateRewritesNothing()
        {
            var output = new StringWriter();
            var error = new StringWriter();
            Assert.Equal(Cli.Ok, Cli.Run(new[] { "golden", "--root", TestRepo.Root }, output, error));
            Assert.Contains("ok       m1/practice-solo", output.ToString());

            // --update 는 같은 해시면 같은 바이트라 아무 파일도 바꾸지 않는다(expected.json 은 도구의 출력과 바이트가 같아야 한다)
            foreach (GoldenEntry e in TestRepo.LoadGolden().InMilestone("m1"))
            {
                string path = Path.Combine(e.Directory, GoldenEntry.ExpectedFileName);
                Assert.Equal(File.ReadAllText(path), GoldenEntry.FormatExpected(e.Id, GoldenRunner.Run(DetKit.Db, e)));
            }
        }

        [Fact]
        public void GoldenRunnerRejectsAMismatchedSetup()
        {
            GoldenEntry entry = TestRepo.LoadGolden().Get("m1/practice-solo");
            byte[] record = entry.ReadRecord();
            GoldenSetup two = Parse("{ \"hashEveryTicks\": 300, \"players\": 2 }");
            Assert.Throws<FormatException>(() => GoldenRunner.Run(DetKit.Db, entry.Id, "practice", 0, two, record));
            GoldenSetup typo = Parse("{ \"hashEveryTick\": 300, \"players\": 1 }");
            Assert.Throws<FormatException>(() => GoldenRunner.Run(DetKit.Db, entry.Id, "practice", 0, typo, record));
            GoldenSetup ok = Parse("{ \"hashEveryTicks\": 300, \"players\": 1 }");
            Assert.Throws<FormatException>(() => GoldenRunner.Run(DetKit.Db, entry.Id, "practice", 1, ok, record));    // 씨앗이 다르다
            Assert.Throws<FormatException>(() => GoldenRunner.Run(DetKit.Db, entry.Id, "duel", 0, ok, record));        // .wzrc 로 결투
        }

        private static GoldenSetup Parse(string setupJson)
        {
            string golden = "{ \"format\": 1, \"id\": \"m1/x\", \"kind\": \"practice\", \"seed\": \"0x0\", \"setup\": " + setupJson +
                            ", \"record\": \"record.wzrc\" }";
            string expected = GoldenEntry.FormatExpected("m1/x", new[] { new GoldenHash(1, 1, null) });
            return GoldenEntry.Parse("m1/x", Path.GetTempPath(), golden, expected, false).Setup;
        }
    }
}
