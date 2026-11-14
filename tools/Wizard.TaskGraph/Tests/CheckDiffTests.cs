using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // 수락 3·8: check-diff 가 owned_paths 와 공유 추가 전용 파일 밖의 변경, 다시 만드는 파일의 불일치,
    // Unity 가 고쳐 쓴 파일의 커밋을 실패시킨다.
    public sealed class CheckDiffTests : IDisposable
    {
        private const string Id = "M0-BT-90";
        private readonly TempRepo _repo;
        private readonly string _wt;

        public CheckDiffTests()
        {
            _repo = TempRepo.Create();
            _repo.Write("tools/Wizard.Mine/Program.cs", "class P {}\n");
            _repo.Write("docs/dev/tasks.json", "{}\n");
            _repo.Write("data/names/names.ko.json", "{\n\"a.b\": \"가\",\n\"~end\": \"\"\n}\n");
            _repo.Write("Directory.Packages.props", "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"A\" Version=\"1.0.0\" />\n  </ItemGroup>\n</Project>\n");
            _repo.Write("tools/Wizard.Other/Wizard.Other.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"A\" />\n  </ItemGroup>\n</Project>\n");
            _repo.Write("tests/fixtures/old.txt", "old\n");
            _repo.Write("tests/golden/CHANGELOG.md", "# 골든 변경 기록\n");
            _repo.Write("tests/golden/m1/expected.json", "{\"hash\":\"1\"}\n");
            _repo.Write("src/Wizard.Core/Generated/SinTable.g.cs", "stale\n");
            _repo.Write("src/Wizard.Core/Old.cs.meta", "guid: 1\n");
            _repo.Write("unity/ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.3.20f1\n");
            _repo.Write("unity/Assets/Game/App/Game.App.asmdef", "{\n  \"name\": \"Game.App\",\n  \"precompiledReferences\": [\n    \"A.dll\"\n  ]\n}\n");
            _repo.Commit("chore: base");
            _repo.Git("branch", "task/" + Id);
            _wt = _repo.PathOf(".claude/worktrees/" + Id);
            _repo.Git("worktree", "add", "-q", _wt, "task/" + Id);
        }

        public void Dispose() => _repo.Dispose();

        private void Change(string rel, string? content)
        {
            string p = Path.Combine(_wt, rel);
            if (content == null)
            {
                File.Delete(p);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllText(p, content);
            }
        }

        private void Commit() => _repo.Commit($"feat({Id}): change", _wt);

        private DiffReport Check(IReadOnlyList<Regenerator>? regens = null, params string[] owned)
        {
            var task = TaskDef.From(Fx.Task(Id, paths: owned.Length == 0 ? new[] { "tools/Wizard.Mine/" } : owned));
            var checker = new DiffChecker(new Git(_repo.Dir), _repo.PathOf("artifacts/taskgraph"), regens ?? FakeRegens(ok: true));
            return checker.Check(task, "main", "task/" + Id);
        }

        // 가짜 "도구"(찾은 셸의 -c 로 띄운다. T-67 3번): 다시 만든다는 것은 표 파일에 "gen\n" 을, 골든에 {"hash":"2"} 를 쓰는 것이다
        private static IReadOnlyList<Regenerator> FakeRegens(bool ok)
        {
            return new[]
            {
                new Regenerator("fake-tablegen", new[] { "src/*/Generated/*.g.cs" },
                    new[] { TestShell.Command("printf 'gen\\n' > src/Wizard.Core/Generated/SinTable.g.cs") }, true),
                new Regenerator("fake-replaycli", new[] { "tests/golden/**/expected*.json" },
                    new[] { TestShell.Command("printf '{\"hash\":\"2\"}\\n' > tests/golden/m1/expected.json") }, true),
                new Regenerator("fake-metagen", new[] { "src/**/*.meta", "unity/Assets/**/*.meta" },
                    new[] { TestShell.Command(ok ? "exit 0" : "exit 1") }, false),
            };
        }

        private static void AssertFail(DiffReport r, string path, string fragment)
        {
            Assert.False(r.Passed, Dump(r));
            Assert.True(r.Failures.Any(f => f.Path == path && f.Note.Contains(fragment, StringComparison.Ordinal)), $"{path}: '{fragment}' 가 없다\n{Dump(r)}");
        }

        private static void AssertPass(DiffReport r)
        {
            Assert.True(r.Passed, Dump(r));
        }

        private static string Dump(DiffReport r)
        {
            var w = new StringWriter();
            r.Print(w);
            return w.ToString();
        }

        [Fact]
        public void OwnedChanges_Pass()
        {
            Change("tools/Wizard.Mine/Program.cs", "class P { int x; }\n");
            Change("tools/Wizard.Mine/New.cs", "class N {}\n");
            Commit();
            DiffReport r = Check();
            AssertPass(r);
            Assert.Equal(2, r.ChangedCount);
        }

        [Fact]
        public void OutsideOwnedPaths_Fails()
        {
            Change("tools/Wizard.Mine/Program.cs", "class P { int x; }\n");
            Change("docs/dev/tasks.json", "{\"x\":1}\n");
            Commit();
            AssertFail(Check(), "docs/dev/tasks.json", "소유 파일 밖이다");
        }

        [Fact]
        public void DeletingSharedFile_Fails()
        {
            Change("tests/golden/CHANGELOG.md", null);
            Commit();
            AssertFail(Check(), "tests/golden/CHANGELOG.md", "지우거나");
        }

        [Fact]
        public void NamesTable_AddOnly()
        {
            Change("data/names/names.ko.json", "{\n\"a.b\": \"가\",\n\"a.c\": \"나\",\n\"~end\": \"\"\n}\n");
            Commit();
            AssertPass(Check());
        }

        [Fact]
        public void NamesTable_EditingAnExistingLine_Fails()
        {
            Change("data/names/names.ko.json", "{\n\"a.b\": \"바뀜\",\n\"~end\": \"\"\n}\n");
            Commit();
            AssertFail(Check(), "data/names/names.ko.json", "줄을 지우거나 고쳤다");
        }

        [Fact]
        public void PackagesProps_OnlyPackageVersionLines()
        {
            Change("Directory.Packages.props", "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"A\" Version=\"1.0.0\" />\n    <PackageVersion Include=\"B\" Version=\"2.0.0\" />\n  </ItemGroup>\n</Project>\n");
            Commit();
            AssertPass(Check());
        }

        [Fact]
        public void PackagesProps_OtherLine_Fails()
        {
            Change("Directory.Packages.props", "<Project>\n  <PropertyGroup><X>1</X></PropertyGroup>\n  <ItemGroup>\n    <PackageVersion Include=\"A\" Version=\"1.0.0\" />\n  </ItemGroup>\n</Project>\n");
            Commit();
            AssertFail(Check(), "Directory.Packages.props", "더한 줄의 꼴");
        }

        [Fact]
        public void Csproj_PackageReferenceLineOnly()
        {
            Change("tools/Wizard.Other/Wizard.Other.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"A\" />\n    <PackageReference Include=\"B\" PrivateAssets=\"all\" />\n  </ItemGroup>\n</Project>\n");
            Commit();
            DiffReport r = Check();
            AssertPass(r);
            Assert.Contains(r.Lines, l => l.Path == "tools/Wizard.Other/Wizard.Other.csproj" && l.Note.Contains("검토 승인", StringComparison.Ordinal));
        }

        [Fact]
        public void Csproj_OtherEdit_Fails()
        {
            Change("tools/Wizard.Other/Wizard.Other.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"A\" />\n  </ItemGroup>\n</Project>\n");
            Commit();
            AssertFail(Check(), "tools/Wizard.Other/Wizard.Other.csproj", "줄을 지우거나 고쳤다");
        }

        [Fact]
        public void Fixtures_NewFilesOnly()
        {
            Change("tests/fixtures/new.bin", "new\n");
            Commit();
            AssertPass(Check());
            Change("tests/fixtures/old.txt", "changed\n");
            Commit();
            AssertFail(Check(), "tests/fixtures/old.txt", "새 파일을 더하기만");
        }

        [Fact]
        public void Asmdef_PrecompiledReferenceLine_WithTrailingComma()
        {
            Change("unity/Assets/Game/App/Game.App.asmdef", "{\n  \"name\": \"Game.App\",\n  \"precompiledReferences\": [\n    \"A.dll\",\n    \"B.dll\"\n  ]\n}\n");
            Change("unity/Assets/Plugins/B.dll", "MZ");
            Change("unity/Assets/Plugins/B.dll.meta", "guid: 2\n");
            Commit();
            AssertPass(Check());
        }

        [Fact]
        public void UnityRewrittenFiles_InCommit_Fail()
        {
            Change("unity/ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.3.21f1\n");
            Change("unity/Packages/packages-lock.json", "{}\n");
            Change("unity/ProjectSettings/QualitySettings.asset", "%YAML 1.1\n");
            Commit();
            DiffReport r = Check();
            AssertFail(r, "unity/ProjectSettings/ProjectVersion.txt", "Unity 가 스스로 고쳐 쓰는 파일");
            AssertFail(r, "unity/Packages/packages-lock.json", "Unity 가 스스로 고쳐 쓰는 파일");
            AssertFail(r, "unity/ProjectSettings/QualitySettings.asset", "Unity 가 스스로 고쳐 쓰는 파일");
        }

        [Fact]
        public void UnityRewrittenFiles_AllowedForTheTaskThatOwnsThem()
        {
            Change("unity/ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.3.21f1\n");
            Commit();
            AssertPass(Check(null, "unity/ProjectSettings/"));
        }

        [Fact]
        public void UnityLibraryOrGeneratedSln_AlwaysFail()
        {
            Change("unity/Library/x.bin", "x");
            Change("unity/Assembly-CSharp.csproj", "<Project/>\n");
            TempRepo.GitIn(_wt, "add", "-f", "unity/Library/x.bin");
            Commit();
            DiffReport r = Check(null, "unity/");
            AssertFail(r, "unity/Library/x.bin", "Unity 가 만드는 파일");
            AssertFail(r, "unity/Assembly-CSharp.csproj", "Unity 가 만드는 파일");
        }

        [Fact]
        public void RegeneratedFile_SameAsToolOutput_Passes()
        {
            Change("src/Wizard.Core/Generated/SinTable.g.cs", "gen\n");
            Commit();
            DiffReport r = Check();
            AssertPass(r);
            Assert.Contains(r.Lines, l => l.Path == "src/Wizard.Core/Generated/SinTable.g.cs" && l.Note.Contains("fake-tablegen 의 출력과 같다", StringComparison.Ordinal));
        }

        [Fact]
        public void RegeneratedFile_HandEdited_Fails()
        {
            Change("src/Wizard.Core/Generated/SinTable.g.cs", "hand edited\n");
            Commit();
            AssertFail(Check(), "src/Wizard.Core/Generated/SinTable.g.cs", "출력과 다르다");
        }

        [Fact]
        public void RegeneratedFile_NewFileTheToolDoesNotMake_Fails()
        {
            Change("src/Wizard.Core/Generated/PowTables.g.cs", "made by hand\n");
            Commit();
            AssertFail(Check(), "src/Wizard.Core/Generated/PowTables.g.cs", "출력과 다르다");
        }

        [Fact]
        public void RegeneratedFile_Deleted_Fails()
        {
            Change("src/Wizard.Core/Generated/SinTable.g.cs", null);
            Commit();
            AssertFail(Check(), "src/Wizard.Core/Generated/SinTable.g.cs", "지웠다");
        }

        [Fact]
        public void RegeneratorThatFails_FailsLoudly()
        {
            Change("src/Wizard.Core/New.cs.meta", "guid: 3\n");
            Commit();
            AssertFail(Check(FakeRegens(ok: false)), "src/Wizard.Core/New.cs.meta", "확인하지 못했다");
        }

        [Fact]
        public void Meta_NewFileOnly()
        {
            Change("src/Wizard.Core/New.cs.meta", "guid: 3\n");
            Commit();
            AssertPass(Check());
            Change("src/Wizard.Core/Old.cs.meta", "guid: 9\n");
            Commit();
            AssertFail(Check(), "src/Wizard.Core/Old.cs.meta", "MetaGen 이 만든 새 파일만");
        }

        [Fact]
        public void Golden_NeedsChangelogLine()
        {
            Change("tests/golden/m1/expected.json", "{\"hash\":\"2\"}\n");
            Commit();
            AssertFail(Check(), "tests/golden/CHANGELOG.md", Id);
            Change("tests/golden/CHANGELOG.md", "# 골든 변경 기록\n- " + Id + ": m1 — 상태 절을 더했다\n");
            Commit();
            AssertPass(Check());
        }

        [Fact]
        public void Golden_NotToolOutput_Fails()
        {
            Change("tests/golden/m1/expected.json", "{\"hash\":\"3\"}\n");
            Change("tests/golden/CHANGELOG.md", "# 골든 변경 기록\n- " + Id + ": m1\n");
            Commit();
            AssertFail(Check(), "tests/golden/m1/expected.json", "출력과 다르다");
        }

        [Fact]
        public void DefaultRegenerators_WithoutTheTools_FailInsteadOfSkipping()
        {
            Change("src/Wizard.Core/Generated/SinTable.g.cs", "gen\n");
            Commit();
            AssertFail(Check(Regenerator.Defaults()), "src/Wizard.Core/Generated/SinTable.g.cs", "확인하지 못했다");
        }

        [Fact]
        public void Cli_CheckDiff_UsesTaskBranchAndOwnedPaths()
        {
            _repo.Write("docs/dev/tasks.json", Fx.Text(Fx.Doc(Fx.Task(Id, paths: new[] { "tools/Wizard.Mine/" }))));
            _repo.Commit("docs: graph");
            string tasks = _repo.PathOf("docs/dev/tasks.json");
            Change("tools/Wizard.Mine/Program.cs", "class P { int z; }\n");
            Commit();
            CliRun ok = CliRun.In(_repo, tasks, "check-diff", Id);
            Assert.True(ok.Code == 0, ok.ToString());
            Assert.Contains("위반 0건", ok.Stdout);

            Change("README.md", "changed\n");
            Commit();
            CliRun bad = CliRun.In(_repo, tasks, "check-diff", Id);
            Assert.Equal(1, bad.Code);
            Assert.Contains("FAIL  README.md — 소유 파일 밖이다", bad.Stdout);

            CliRun missing = CliRun.In(_repo, tasks, "check-diff", Id, "--head", "task/nope");
            Assert.Equal(1, missing.Code);
            Assert.Contains("'task/nope' 이 없다", missing.Stderr);
        }
    }
}
