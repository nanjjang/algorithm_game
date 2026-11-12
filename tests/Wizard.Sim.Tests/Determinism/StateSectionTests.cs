using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wizard.ReplayCli;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Sim.Tests.Determinism
{
    // 되감기·직렬화 퍼저가 "세계가 가진 모든 상태 절"을 대상으로 하는지(M1-TE-01 수락 조건). 퍼저는 절 목록(StateSections.List)에서 돈다.
    // 그 목록이 코어의 절 열거형과 같고, 세계의 WriteState 바이트를 남김없이 덮고, 상태가 있는 절은 상태 훅 넷을 다 가졌고,
    // 퍼저의 입력이 그 절들을 실제로 움직이는지 본다. 뒤에 절을 더하면 이 시험들이 저절로 그 절도 본다.
    public sealed class StateSectionTests
    {
        private readonly ITestOutputHelper _output;

        public StateSectionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SectionListIsTheCoreEnumInIdOrder()
        {
            ushort[] fromEnum = Enum.GetValues(typeof(StateSection)).Cast<StateSection>().Select(s => (ushort)s).OrderBy(x => x).ToArray();
            Assert.Equal(fromEnum, StateSections.List.Select(s => s.Id).ToArray());
            Assert.Equal(Enum.GetNames(typeof(StateSection)).OrderBy(n => (ushort)Enum.Parse<StateSection>(n)).ToArray(),
                         StateSections.List.Select(s => s.Name).ToArray());
        }

        // CONTRACTS 3.2: 모듈마다 Snapshot_Save, Snapshot_Load, State_Write, State_Read. 하나라도 빠지면 되감기나 직렬화가 그 상태를 잃는다.
        // Rng(16)은 Runtime/Core 의 세계 공통 스칼라라 스냅숏이 세계 복사(CopyCoreTo)로 함께 옮긴다(훅 표: "Rng(16) 훅 없음")
        [Fact]
        public void EverySectionWithStateHasAllFourStateHooks()
        {
            foreach (StateSectionInfo s in StateSections.List)
            {
                bool any = s.HasWrite || s.HasRead || s.HasSnapshotSave || s.HasSnapshotLoad;
                if (!any)
                {
                    continue;
                }
                Assert.True(s.HasWrite && s.HasRead, s + ": State_Write and State_Read come in pairs");
                if (s.Name != nameof(StateSection.Rng))
                {
                    Assert.True(s.HasSnapshotSave && s.HasSnapshotLoad, s + ": a section with state needs Snapshot_Save and Snapshot_Load");
                }
            }
        }

        // 절로 나눈 바이트를 이어 붙이면 WriteState 와 같다(Capture 가 확인한다. 어긋나면 던진다). 세 종류의 세계, 여러 틱
        [Theory]
        [InlineData(WorldKind.Practice)]
        [InlineData(WorldKind.Endless)]
        [InlineData(WorldKind.Duel)]
        public void SectionsCoverWriteStateExactly(WorldKind kind)
        {
            RecordFeed feed = DetKit.Feed(DetKit.Record(kind, 0x5EC_0001UL, 0x5EC_0002UL, 900, "dense"));
            SimWorld w = DetKit.World(kind, feed);
            var sink = new CountingSink();
            while (true)
            {
                StateImage image = StateSections.Capture(w);
                Assert.Equal(StateSections.List.Count, image.Sections.Count);
                byte[] joined = image.Sections.SelectMany(s => s.Bytes).ToArray();
                Assert.Equal(image.All, joined);
                foreach (SectionBytes s in image.Sections)
                {
                    if (s.Shape == SectionShape.Scalar)
                    {
                        Assert.True(s.Present, s.Info + " is a scalar section and must always be written (CONTRACTS 3.2)");
                    }
                }
                if (w.Tick >= feed.EndTick)
                {
                    break;
                }
                for (int i = 0; i < 45 && w.Tick < feed.EndTick; i++)
                {
                    Replayer.StepOnce(w, feed, sink);
                }
            }
        }

        // 퍼저의 입력이 상태가 있는 모든 절을 움직인다(움직이지 않는 절은 되감기·직렬화 대조가 아무것도 시험하지 못한다).
        // 퍼저가 쓰는 세 종류의 세계와 네 가지 봇 꼴로 1분씩 돌려 절마다 서로 다른 내용이 둘 이상 나오는지 본다.
        // 새 모듈의 상태가 봇 입력으로 닿지 않으면(예: 특정 웨이브에서만 생기는 상태) 이 시험이 실패한다. 그때는 퍼저의 입력(BotProfile, FuzzRun)을 넓힌다.
        [Fact]
        public void FuzzInputsMoveEverySectionThatHasState()
        {
            var seen = new Dictionary<ushort, HashSet<ulong>>();
            foreach (WorldKind kind in new[] { WorldKind.Practice, WorldKind.Endless, WorldKind.Duel })
            {
                foreach (string profile in FuzzRun.Profiles)
                {
                    RecordFeed feed = DetKit.Feed(DetKit.Record(kind, 0x5EC_1001UL, 0x5EC_1002UL, DetKit.Minute, profile));
                    SimWorld w = DetKit.World(kind, feed);
                    var sink = new CountingSink();
                    while (w.Tick < feed.EndTick)
                    {
                        Replayer.StepOnce(w, feed, sink);
                        if (w.Tick % 30U == 0U)
                        {
                            foreach (SectionBytes s in StateSections.Capture(w).Sections)
                            {
                                if (!seen.TryGetValue(s.Info.Id, out HashSet<ulong>? set))
                                {
                                    seen[s.Info.Id] = set = new HashSet<ulong>();
                                }
                                set.Add(s.Digest);
                            }
                        }
                    }
                }
            }
            var report = new StringBuilder();
            var unmoved = new List<string>();
            foreach (StateSectionInfo s in StateSections.List)
            {
                int distinct = seen[s.Id].Count;
                report.Append(s).Append(": ").Append(distinct).Append(" distinct contents").Append(s.HasWrite ? "" : " (no state yet)").Append('\n');
                if (s.HasWrite && distinct < 2)
                {
                    unmoved.Add(s.ToString());
                }
            }
            _output.WriteLine(report.ToString());
            Assert.True(unmoved.Count == 0, "fuzz inputs never change these sections: " + string.Join(", ", unmoved) + "\n" + report);
        }

        // 다른 절이 있으면 그 절 이름과 처음 다른 바이트 자리가 나온다(ReplayCli 가 "어느 절인지" 알려 주는 몸통)
        [Fact]
        public void DiffNamesTheSectionThatDiffers()
        {
            RecordFeed feed = DetKit.Feed(DetKit.PracticeRecord(0x5EC_2001UL, 120, "dense"));
            SimWorld a = DetKit.World(WorldKind.Practice, feed), b = DetKit.World(WorldKind.Practice, feed);
            Replayer.RunTo(a, feed, 120, new CountingSink());
            Replayer.RunTo(b, feed, 120, new CountingSink());
            Assert.Empty(StateImage.Diff(StateSections.Capture(a), StateSections.Capture(b)));
            b.HeldRng(Wizard.Core.RngStream.EnemyAi).NextU32();
            SectionDifference d = Assert.Single(StateImage.Diff(StateSections.Capture(a), StateSections.Capture(b)));
            Assert.Equal(nameof(StateSection.Rng), d.Section.Name);
            Assert.True(d.FirstByte > 2);
        }
    }
}
