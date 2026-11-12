using System.IO;
using Wizard.Content;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Names;
using Xunit;

namespace Wizard.DataBuild.Tests.Names
{
    // databuild build, databuild check, databuild names --check|--fix|--scan (ARCHITECTURE 8.2·8.3, T-35)
    public sealed class CommandLineTests
    {
        private static (int Code, string Out, string Err) Run(string root, params string[] args)
        {
            var o = new StringWriter();
            var e = new StringWriter();
            int code = Program.Run(args, o, e, root);
            return (code, o.ToString(), e.ToString());
        }

        [Fact]
        public void Check_OnSkeletonData_IsOk()
        {
            using var repo = new TempRepo();
            var (code, output, err) = Run(repo.Root, "check");
            Assert.True(code == Program.ExitOk, err);
            Assert.StartsWith("databuild check: ok", output);
        }

        [Fact]
        public void Build_WritesPackAndNames_SameBytesTwice()
        {
            using var repo = new TempRepo();
            repo.Write("data/names/names.ko.json", "{\n  \"char.1\": \"제자 1\",\n  \"~end\": \"\"\n}\n");
            string out1 = Path.Combine(repo.Root, "out1");
            string out2 = Path.Combine(repo.Root, "out2");
            Assert.Equal(Program.ExitOk, Run(repo.Root, "build", "--out", out1, "--data-version", "12").Code);
            Assert.Equal(Program.ExitOk, Run(repo.Root, "build", "--out", out2, "--data-version", "12").Code);
            byte[] pack1 = File.ReadAllBytes(Path.Combine(out1, "base.wzpack"));
            Assert.Equal(pack1, File.ReadAllBytes(Path.Combine(out2, "base.wzpack")));
            Assert.Equal(File.ReadAllBytes(Path.Combine(out1, "names.ko.wznm")), File.ReadAllBytes(Path.Combine(out2, "names.ko.wznm")));
            ContentPack pack = ContentPack.Load(pack1);
            Assert.Equal(12, pack.DataVersion);
            Assert.Equal(Wizard.Sim.SimVersion.CoreVersion, pack.CoreCompat);
        }

        [Fact]
        public void Build_DefaultsToArtifactsContent()
        {
            using var repo = new TempRepo();
            Assert.Equal(Program.ExitOk, Run(repo.Root, "build").Code);
            Assert.True(repo.Exists("artifacts/content/base.wzpack"));
            Assert.True(repo.Exists("artifacts/content/names.ko.wznm"));
        }

        [Fact]
        public void Build_WritesNothingWhenCheckFails()
        {
            using var repo = new TempRepo();
            repo.Write("data/names/names.ko.json", "{\n  \"b.x\": \"2\",\n  \"a.x\": \"1\",\n  \"~end\": \"\"\n}\n");
            var (code, _, err) = Run(repo.Root, "build");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("data/names/names.ko.json: 3:", err);
            Assert.False(repo.Exists("artifacts/content/base.wzpack"));
        }

        [Fact]
        public void NamesCheck_FailsThenFixRepairsThenCheckPasses()
        {
            using var repo = new TempRepo();
            repo.Write("data/names/names.ko.json", "{\n  \"~end\": \"\",\n  \"b.x\": \"2\",\n  \"a.x\": \"1\",\n  \"a.x\": \"1\",\n  \"~end\": \"\"\n}\n");
            var (c1, _, e1) = Run(repo.Root, "names", "--check");
            Assert.Equal(Program.ExitProblems, c1);
            Assert.Contains("FAIL  data/names/names.ko.json:", e1);
            var (c2, o2, _) = Run(repo.Root, "names", "--fix");
            Assert.Equal(Program.ExitOk, c2);
            Assert.Contains("고쳐 썼다", o2);
            Assert.Equal("{\n  \"a.x\": \"1\",\n  \"b.x\": \"2\",\n  \"~end\": \"\"\n}\n", repo.Read(NamesFile.RepoPath));
            Assert.Equal(Program.ExitOk, Run(repo.Root, "names", "--check").Code);
            var (c3, o3, _) = Run(repo.Root, "names", "--fix");
            Assert.Equal(Program.ExitOk, c3);
            Assert.Contains("바꿀 것이 없다", o3);
        }

        [Fact]
        public void NamesFix_LeavesFileAloneOnConflict()
        {
            using var repo = new TempRepo();
            string conflict = "{\n  \"a.x\": \"1\",\n  \"a.x\": \"하나\",\n  \"~end\": \"\"\n}\n";
            repo.Write("data/names/names.ko.json", conflict);
            var (code, _, err) = Run(repo.Root, "names", "--fix");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("값이 둘", err);
            Assert.Equal(conflict, repo.Read(NamesFile.RepoPath));
        }

        [Fact]
        public void NamesScan_ReportsAndFails()
        {
            using var repo = new TempRepo();
            Assert.Equal(Program.ExitOk, Run(repo.Root, "names", "--scan").Code);
            repo.Write("src/Wizard.X/A.cs", "class A { string s = \"한글\"; }\n");
            var (code, _, err) = Run(repo.Root, "names", "--scan");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("FAIL  src/Wizard.X/A.cs:1:", err);
        }

        [Theory]
        [InlineData()]
        [InlineData("nope")]
        [InlineData("names")]
        [InlineData("names", "--check", "--fix")]
        [InlineData("check", "--fix")]
        [InlineData("check", "--out", "x")]
        [InlineData("build", "--data-version", "-1")]
        [InlineData("build", "--data-version")]
        [InlineData("build", "--bogus")]
        public void Usage_IsExit2(params string[] args)
        {
            using var repo = new TempRepo();
            Assert.Equal(Program.ExitUsage, Run(repo.Root, args).Code);
        }

        [Fact]
        public void RepoRoot_FoundFromBelow_NotFromCurrentFolder()
        {
            using var repo = new TempRepo();
            string deep = Path.Combine(repo.Root, "artifacts", "bin", "Wizard.DataBuild", "release");
            Directory.CreateDirectory(deep);
            Assert.Equal(repo.Root, RepoRoot.FindFrom(deep));
        }

        [Fact]
        public void RootOption_OverridesDiscovery()
        {
            using var repo = new TempRepo();
            var o = new StringWriter();
            var e = new StringWriter();
            Assert.Equal(Program.ExitOk, Program.Run(new[] { "names", "--check", "--root", repo.Root }, o, e, null));
        }
    }
}
