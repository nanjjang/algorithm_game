using System.IO;
using System.Linq;
using Wizard.AssetCheck.Check;
using Wizard.AssetCheck.Report;
using Wizard.AssetCheck.Slots;
using Wizard.TestKit;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.AssetCheck.Tests
{
    // spec 14 TV-196(반응 부품 표의 장수 합 71)과 asset_slots.json(DECISIONS-TECH T-58 5번)
    public sealed class SlotTableVectorTests
    {
        private static AssetSlotTable RepoTable() => AssetSlotTable.Parse(File.ReadAllText(TestRepo.Path(AssetSlotTable.RepoPath)));

        // T-58 표 = spec 14 4.6절 반응 부품 표(캐릭터, 부품 열쇠, 크기, 장수)
        private static readonly (string Char, string Part, int W, int H, int Count)[] Reactive =
        {
            ("char.1", "steam", 16, 16, 12),
            ("char.1", "lid", 24, 16, 4),
            ("char.2", "lantern_orb", 16, 16, 9),
            ("char.3", "bookmark_cord", 16, 24, 12),
            ("char.3", "glyph_shard", 8, 8, 4),
            ("char.4", "crown_shard", 8, 12, 15),
            ("char.4", "frost_step", 16, 8, 3),
            ("char.5", "eye", 16, 8, 10),
            ("char.5", "debt_ring", 16, 16, 2),
        };

        [Fact]
        public void S14_TV_196__ReactivePartsNineRowsSumTo71()
        {
            AssetSlotTable table = RepoTable();
            Assert.Empty(table.Problems);
            var reactive = table.Entries.Where(e => e.Key.Variant!.StartsWith("reactive/", System.StringComparison.Ordinal)).ToList();
            Assert.Equal(9, reactive.Count);
            for (int i = 0; i < Reactive.Length; i++)
            {
                AssetSlotEntry e = reactive[i];
                Assert.Equal(AssetPack.Character, e.Key.Pack);
                Assert.Equal(Reactive[i].Char, e.Key.Id);
                Assert.Equal("reactive/" + Reactive[i].Part, e.Key.Variant);
                Assert.Equal(Reactive[i].W, e.FrameSize.Width);
                Assert.Equal(Reactive[i].H, e.FrameSize.Height);
                Assert.Equal(Reactive[i].Count, e.Count);
            }
            Assert.Equal(12 + 4 + 9 + 12 + 4 + 15 + 3 + 10 + 2, reactive.Sum(e => e.Count));
            Assert.Equal(71, reactive.Sum(e => e.Count));
        }

        [Fact]
        public void S14_TV_196__ReportListsReactivePartsAsMissing()
        {
            ImportCheckResult result = ImportChecker.Run(AssetCheckRepo.Load(TestRepo.Root));
            AssetReport report = AssetReport.Build(result, new ApprovalLedger());
            Assert.Equal(71, report.SlotTableCount);
            foreach (var r in Reactive)
            {
                SlotReport? s = report.Find("Character:" + r.Char + "/reactive/" + r.Part);
                Assert.NotNull(s);
                Assert.Equal(SlotStatus.Missing, s!.Status);
                Assert.Equal(r.Count, s.Frames);
                Assert.Equal(r.W, s.FrameSize!.Value.Width);
                Assert.Equal(r.H, s.FrameSize.Value.Height);
            }
        }

        [Fact]
        public void DuplicateSlotKeyIsAProblem()
        {
            AssetSlotTable t = AssetSlotTable.Parse("{ \"slots\": ["
                + "{ \"pack\": \"character\", \"id\": \"char.1\", \"variant\": \"reactive/lid\", \"frame_size\": [24, 16], \"count\": 4, \"spec\": \"14 §4.6\" },"
                + "{ \"pack\": \"character\", \"id\": \"char.1\", \"variant\": \"reactive/lid\", \"frame_size\": [24, 16], \"count\": 4, \"spec\": \"14 §4.6\" } ] }");
            Assert.Single(t.Entries);
            Assert.Contains(t.Problems, p => p.Contains("duplicate"));
        }

        [Theory]
        [InlineData("{ }")]
        [InlineData("{ \"slots\": [ { \"pack\": \"sound\", \"id\": \"x\", \"variant\": \"y\", \"frame_size\": [1, 1], \"count\": 1, \"spec\": \"14 §4.6\" } ] }")]
        [InlineData("{ \"slots\": [ { \"pack\": \"ui\", \"id\": \"I-W1\", \"variant\": \"y\", \"frame_size\": [0, 1], \"count\": 1, \"spec\": \"14 §4.6\" } ] }")]
        [InlineData("{ \"slots\": [ { \"pack\": \"ui\", \"id\": \"I-W1\", \"variant\": \"y\", \"frame_size\": [8, 8], \"count\": 0, \"spec\": \"14 §4.6\" } ] }")]
        [InlineData("not json")]
        public void BadSlotTableIsAProblem(string json)
        {
            Assert.NotEmpty(AssetSlotTable.Parse(json).Problems);
        }

        [Fact]
        public void MissingSlotTableFailsTheCheck()
        {
            using (var sb = new Sandbox())
            {
                File.Delete(sb.PathOf(AssetSlotTable.RepoPath));
                ImportCheckResult r = ImportChecker.Run(sb.Load());
                Assert.Contains(r.Errors, f => f.Check == CheckNames.Slots);
                Assert.False(r.Ok);
            }
        }
    }
}
