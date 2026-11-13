using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Wizard.AssetCheck.Report;
using Xunit;

namespace Wizard.AssetCheck.Tests
{
    // 수락 조건: assetcheck report 가 자리마다 없음/반입됨/검수됨/실패를 artifacts/asset-report.json 으로 낸다.
    // 승인은 approved.json 의 (id, 파일 SHA-256)이고 파일이 바뀌면 승인이 풀린다
    public sealed class ReportAndApprovalTests
    {
        private const string FxManifest = "{ \"notes\": { \"origin\": \"top-left\", \"filter\": \"point\", \"mipmaps\": false }, \"player\": {"
            + " \"good\": { \"path\": \"player/good_8x8_f1.png\", \"size\": [8, 8], \"frames\": [[0, 0, 8, 8]], \"fps\": 12, \"loop\": false },"
            + " \"bad\": { \"path\": \"player/bad_8x8_f1.png\", \"size\": [8, 8], \"frames\": [[0, 0, 8, 8]], \"fps\": 12, \"loop\": false } } }";

        private static Sandbox Repo()
        {
            var sb = new Sandbox();
            sb.WriteText("art/production/effects/manifest.json", FxManifest);
            sb.WritePng("effects/player/good_8x8_f1.png", TestPng.Rgba(8, 8, (x, y) => TestPng.Opaque(0x5E8F4A)));
            sb.WritePng("effects/player/bad_8x8_f1.png", TestPng.Rgba(8, 8, (x, y) => TestPng.Opaque(0xFF3B30)));
            sb.WriteText("data/presentation/asset_slots.json", "{ \"slots\": [ { \"pack\": \"character\", \"id\": \"char.1\", \"variant\": \"reactive/lid\", \"frame_size\": [24, 16], \"count\": 4, \"spec\": \"14 §4.6\" } ] }");
            return sb;
        }

        private static int Cli(Sandbox sb, out string stdout, out string stderr, params string[] args)
        {
            var o = new StringWriter();
            var e = new StringWriter();
            int code = Program.Run(args.Concat(new[] { "--root", sb.Root }).ToArray(), o, e);
            stdout = o.ToString();
            stderr = e.ToString();
            return code;
        }

        private static JsonElement Slot(JsonDocument doc, string id) =>
            doc.RootElement.GetProperty("slots").EnumerateArray().Single(s => s.GetProperty("id").GetString() == id);

        [Fact]
        public void ReportWritesFourStatusesAndFailsWhenASlotFails()
        {
            using (Sandbox sb = Repo())
            {
                int code = Cli(sb, out _, out string err, "report");
                Assert.Equal(Program.ExitFail, code);
                Assert.Contains("failed", err);
                string path = sb.PathOf(AssetReport.DefaultOutput);
                Assert.True(File.Exists(path));
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    Assert.Equal("imported", Slot(doc, "Fx:player/good").GetProperty("status").GetString());
                    JsonElement bad = Slot(doc, "Fx:player/bad");
                    Assert.Equal("failed", bad.GetProperty("status").GetString());
                    Assert.Contains("danger", bad.GetProperty("problems")[0].GetString());
                    JsonElement lid = Slot(doc, "Character:char.1/reactive/lid");
                    Assert.Equal("missing", lid.GetProperty("status").GetString());
                    Assert.Equal(4, lid.GetProperty("count").GetInt32());
                    Assert.Equal(64, Slot(doc, "Fx:player/good").GetProperty("sha256").GetString()!.Length);
                    JsonElement summary = doc.RootElement.GetProperty("summary");
                    Assert.Equal(1, summary.GetProperty("failed").GetInt32());
                    Assert.Equal(0, summary.GetProperty("approved").GetInt32());
                }
            }
        }

        [Fact]
        public void ReportIsDeterministic()
        {
            using (Sandbox sb = Repo())
            {
                Cli(sb, out _, out _, "report");
                byte[] first = File.ReadAllBytes(sb.PathOf(AssetReport.DefaultOutput));
                Cli(sb, out _, out _, "report");
                byte[] second = File.ReadAllBytes(sb.PathOf(AssetReport.DefaultOutput));
                Assert.Equal(first, second);
                Assert.DoesNotContain((byte)'\r', first);
                Assert.Equal((byte)'\n', first[first.Length - 1]);
            }
        }

        [Fact]
        public void ApproveBindsToFileHashAndChangedFileIsNoLongerApproved()
        {
            using (Sandbox sb = Repo())
            {
                Assert.Equal(Program.ExitOk, Cli(sb, out string ok, out _, "approve", "Fx:player/good"));
                Assert.Contains("Fx:player/good", ok);
                string ledgerPath = sb.PathOf(ApprovalLedger.RepoPath);
                ApprovalLedger ledger = ApprovalLedger.Load(ledgerPath);
                Assert.True(ledger.TryGet("Fx:player/good", out ApprovalEntry entry));
                Assert.Equal("effects/player/good_8x8_f1.png", entry.File);
                Assert.Equal(ApprovalLedger.Sha256Of(sb.PathOf("art/production/effects/player/good_8x8_f1.png")), entry.Sha256);

                Cli(sb, out _, out _, "report");
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(sb.PathOf(AssetReport.DefaultOutput))))
                {
                    Assert.Equal("approved", Slot(doc, "Fx:player/good").GetProperty("status").GetString());
                }

                // 그림을 바꾸면 승인이 풀린다(반입됨으로 돌아가고 approval_stale 을 적는다)
                sb.WritePng("effects/player/good_8x8_f1.png", TestPng.Rgba(8, 8, (x, y) => TestPng.Opaque(x == 0 ? 0x5C6670 : 0x5E8F4A)));
                Cli(sb, out _, out _, "report");
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(sb.PathOf(AssetReport.DefaultOutput))))
                {
                    JsonElement good = Slot(doc, "Fx:player/good");
                    Assert.Equal("imported", good.GetProperty("status").GetString());
                    Assert.True(good.GetProperty("approval_stale").GetBoolean());
                }
            }
        }

        [Fact]
        public void ApproveRefusesFailedAndMissingAndUnknownSlots()
        {
            using (Sandbox sb = Repo())
            {
                Assert.Equal(Program.ExitFail, Cli(sb, out _, out string e1, "approve", "Fx:player/bad"));
                Assert.Contains("fails the import check", e1);
                Assert.Equal(Program.ExitFail, Cli(sb, out _, out string e2, "approve", "Character:char.1/reactive/lid"));
                Assert.Contains("no picture", e2);
                Assert.Equal(Program.ExitFail, Cli(sb, out _, out string e3, "approve", "Fx:player/nothing"));
                Assert.Contains("no slot", e3);
                Assert.False(File.Exists(sb.PathOf(ApprovalLedger.RepoPath)));
            }
        }

        [Fact]
        public void LedgerSaveIsSortedAndStable()
        {
            using (var sb = new Sandbox())
            {
                var ledger = new ApprovalLedger();
                ledger.Set(new ApprovalEntry("Ui:K-02/b", "ui/kit/b.png", new string('b', 64)));
                ledger.Set(new ApprovalEntry("Ui:K-01/a", "ui/kit/a.png", new string('a', 64)));
                string path = sb.PathOf(ApprovalLedger.RepoPath);
                ledger.Save(path);
                string text = File.ReadAllText(path, Encoding.UTF8);
                Assert.True(text.IndexOf("Ui:K-01/a") < text.IndexOf("Ui:K-02/b"));
                ApprovalLedger again = ApprovalLedger.Load(path);
                Assert.Equal(2, again.Count);
                again.Save(path);
                Assert.Equal(text, File.ReadAllText(path, Encoding.UTF8));
            }
        }

        [Fact]
        public void CheckCommandFailsWithOneLinePerError()
        {
            using (Sandbox sb = Repo())
            {
                Assert.Equal(Program.ExitFail, Cli(sb, out _, out string err, "check"));
                Assert.Contains("FAIL  [danger] art/production/effects/player/bad_8x8_f1.png", err);
            }
        }

        [Fact]
        public void CheckCommandPassesOnCleanInput()
        {
            using (var sb = new Sandbox())
            {
                Assert.Equal(Program.ExitOk, Cli(sb, out string o, out string err, "check"));
                Assert.Contains("0 error(s)", o);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("nope")]
        [InlineData("scaffold")]
        [InlineData("approve")]
        [InlineData("check extra")]
        public void UsageErrorsExitWithTwo(string line)
        {
            string[] args = line.Length == 0 ? new string[0] : line.Split(' ');
            using (var sb = new Sandbox())
            {
                var e = new StringWriter();
                string[] full = args.Length == 0 ? args : args.Concat(new[] { "--root", sb.Root }).ToArray();
                Assert.Equal(Program.ExitUsage, Program.Run(full, new StringWriter(), e));
                Assert.Contains("usage", e.ToString());
            }
        }

        [Fact]
        public void RootWithoutSolutionIsRejected()
        {
            var e = new StringWriter();
            Assert.Equal(Program.ExitUsage, Program.Run(new[] { "check", "--root", Path.GetTempPath() }, new StringWriter(), e));
            Assert.Contains("Wizard.sln", e.ToString());
        }
    }
}
