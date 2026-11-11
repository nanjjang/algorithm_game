using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 바뀐 경로와, 그것으로 고르는 테스트 프로젝트·Unity 단계(PIPELINE 3.8 경로 조건, 4.6 바뀐 프로젝트의 테스트)
    public sealed class ChangeScopeTests
    {
        [Fact]
        public void TaskBranch_SeesCommittedStagedUnstagedAndUntrackedChanges_SinceTheMergeBase()
        {
            using TempGit g = TempGit.Create();
            g.Write("server/a.txt", "1\n");
            g.Write("tools/old.txt", "1\n");
            g.Commit("chore: base");
            g.Git("checkout", "-q", "-b", "task/X");
            g.Write("server/a.txt", "2\n");
            g.Commit("feat(X): committed");
            // main 이 뒤에 나아가도 기준은 갈림점이다(main 의 새 커밋은 바뀐 경로에 들지 않는다)
            g.Git("checkout", "-q", "main");
            g.Write("main-only.txt", "m\n");
            g.Commit("chore: later");
            g.Git("checkout", "-q", "task/X");
            g.Write("docs/staged.md", "s\n");
            g.Git("add", "docs/staged.md");
            g.Write("tools/old.txt", "unstaged\n");
            g.Write("tests/new.txt", "untracked\n");
            g.Write("artifacts/ignored.txt", "ignored\n");

            ChangeScope s = ChangeScope.Detect(g.Dir, null, false);
            Assert.False(s.All, s.Reason);
            Assert.Equal(new[] { "docs/staged.md", "server/a.txt", "tests/new.txt", "tools/old.txt" }, s.Paths.ToArray());
        }

        [Fact]
        public void DeletedFiles_CountAsChanged()
        {
            using TempGit g = TempGit.Create();
            g.Write("src/Gone/x.cs", "class X {}\n");
            g.Commit("chore: base");
            g.Git("checkout", "-q", "-b", "task/Y");
            g.Git("rm", "-q", "src/Gone/x.cs");
            Assert.Equal(new[] { "src/Gone/x.cs" }, ChangeScope.Detect(g.Dir, null, false).Paths.ToArray());
        }

        [Fact]
        public void OnMain_ForcedOrWithoutMain_EverythingCounts()
        {
            using TempGit g = TempGit.Create();
            Assert.True(ChangeScope.Detect(g.Dir, null, false).All);
            g.Git("checkout", "-q", "-b", "task/Z");
            Assert.False(ChangeScope.Detect(g.Dir, null, false).All);
            Assert.True(ChangeScope.Detect(g.Dir, null, true).All);
            g.Git("branch", "-q", "-m", "main", "trunk");
            Assert.True(ChangeScope.Detect(g.Dir, null, false).All);
        }

        [Fact]
        public void ExplicitBase_IsUsed_AndAnUnknownBaseIsAnError()
        {
            using TempGit g = TempGit.Create();
            string first = g.Git("rev-parse", "HEAD").Trim();
            g.Write("a.txt", "a\n");
            g.Commit("chore: a");
            ChangeScope s = ChangeScope.Detect(g.Dir, first, false);
            Assert.False(s.All);
            Assert.Equal(new[] { "a.txt" }, s.Paths.ToArray());
            Assert.Throws<CiException>(() => ChangeScope.Detect(g.Dir, "no-such-ref", false));
        }

        private static ChangeScope Paths(params string[] p) => ChangeScope.Of(p, "base", "test");

        [Theory]
        [InlineData("src/Wizard.Sim/World.cs")]
        [InlineData("unity/Assets/Game/App.cs")]
        [InlineData("data/sim/heat.json")]
        [InlineData("tests/golden/m1/a/expected.json")]
        [InlineData("tests/Wizard.TestKit/Shared/HashText.cs")]
        [InlineData("tools/Wizard.MetaGen/Program.cs")]
        public void UnityStage_IsNeeded_WhenAChangeTouchesItsPaths(string path)
        {
            Assert.True(UnityScope.Needed(Paths("docs/x.md", path), out string reason));
            Assert.Contains(path, reason, StringComparison.Ordinal);
        }

        [Fact]
        public void UnityStage_IsSkippable_ForServerTestsDocsAndOtherTools_ButNeverWhenEverythingCounts()
        {
            Assert.False(UnityScope.Needed(Paths("server/Wizard.Server.Api/A.cs", "tests/Wizard.Server.Tests/B.cs", "docs/dev/x.md",
                "tools/Wizard.Ci/Program.cs", "tools/ci/ci.sh", "tests/Wizard.TestKit/TestRepo.cs"), out string reason));
            Assert.Contains("닿지 않는다", reason, StringComparison.Ordinal);
            Assert.True(UnityScope.Needed(ChangeScope.Everything("gate"), out _));
            Assert.False(UnityScope.Needed(Paths(), out _));
        }

        // a ← b ← c(테스트), a ← t(테스트), d(테스트, 혼자), 공유 src/s ← 분석기
        private static ProjectGraph Graph() => ProjectGraph.Parse(new List<KeyValuePair<string, string>>
        {
            P("tools/A/A.csproj", "<Project/>"),
            P("tools/B/B.csproj", "<Project><ItemGroup><ProjectReference Include=\"../A/A.csproj\" /></ItemGroup></Project>"),
            P("tests/C/C.csproj", "<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup>" +
                                  "<ItemGroup><ProjectReference Include=\"..\\..\\tools\\B\\B.csproj\" /></ItemGroup></Project>"),
            P("tests/T/T.csproj", "<Project><ItemGroup><PackageReference Include=\"Microsoft.NET.Test.Sdk\" />" +
                                  "<ProjectReference Include=\"../../tools/A/A.csproj\" /></ItemGroup></Project>"),
            P("tests/D/D.csproj", "<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>"),
            P("src/S/S.csproj", "<Project/>"),
            P("tests/ST/ST.csproj", "<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup>" +
                                    "<ItemGroup><ProjectReference Include=\"../../src/S/S.csproj\" /></ItemGroup></Project>"),
            P(ProjectGraph.AnalyzerProject, "<Project/>"),
        });

        private static KeyValuePair<string, string> P(string k, string v) => new KeyValuePair<string, string>(k, v);

        [Fact]
        public void AffectedTests_AreTheTestProjectsThatDependOnTheChangedProjects()
        {
            AffectedTests a = AffectedTests.Select(Graph(), Paths("tools/A/Code/X.cs"));
            Assert.False(a.All);
            Assert.Equal(new[] { "tests/C/C.csproj", "tests/T/T.csproj" }, a.Projects.ToArray());

            Assert.Equal(new[] { "tests/C/C.csproj" }, AffectedTests.Select(Graph(), Paths("tools/B/B.csproj")).Projects.ToArray());
            Assert.Equal(new[] { "tests/D/D.csproj" }, AffectedTests.Select(Graph(), Paths("tests/D/Sub/data.json")).Projects.ToArray());
            Assert.Empty(AffectedTests.Select(Graph(), Paths("art/x.png", "unity/Assets/Game/A.cs")).Projects);
        }

        [Fact]
        public void AnalyzerChange_ReachesSharedProjectsThroughDirectoryBuildProps()
        {
            AffectedTests a = AffectedTests.Select(Graph(), Paths("tools/Wizard.Analyzers/Rules.cs"));
            Assert.Equal(new[] { "tests/ST/ST.csproj" }, a.Projects.ToArray());
        }

        [Theory]
        [InlineData("Directory.Build.props")]
        [InlineData("data/sim/heat.json")]
        [InlineData("docs/spec/01-sim-core.md")]
        [InlineData("tests/vectors.config.json")]
        [InlineData("tools/ci/test.sh")]
        public void PathOutsideEveryProject_RunsAllTests(string path)
        {
            AffectedTests a = AffectedTests.Select(Graph(), Paths("tools/A/A.csproj", path));
            Assert.True(a.All);
            Assert.Contains(path, a.Reason, StringComparison.Ordinal);
            Assert.Equal(new[] { "tests/C/C.csproj", "tests/D/D.csproj", "tests/ST/ST.csproj", "tests/T/T.csproj" }, a.Projects.ToArray());
        }

        [Fact]
        public void RealRepository_GraphHasTheTestProjects()
        {
            ProjectGraph g = ProjectGraph.Load(TempGit.RepoRoot);
            Assert.True(g.Nodes["tests/Wizard.Sim.Tests/Wizard.Sim.Tests.csproj"].IsTest);
            Assert.True(g.Nodes["tools/Wizard.Ci/Wizard.Ci.csproj"].IsTest);
            Assert.False(g.Nodes["src/Wizard.Sim/Wizard.Sim.csproj"].IsTest);
            Assert.Contains("src/Wizard.Core/Wizard.Core.csproj", g.Nodes["src/Wizard.Sim/Wizard.Sim.csproj"].References);
            // Wizard.Core 를 바꾸면 Sim 테스트가 돈다
            AffectedTests a = AffectedTests.Select(g, Paths("src/Wizard.Core/Fix64.cs"));
            Assert.Contains("tests/Wizard.Sim.Tests/Wizard.Sim.Tests.csproj", a.Projects);
            Assert.DoesNotContain("tools/Wizard.Ci/Wizard.Ci.csproj", a.Projects);
        }

        [Fact]
        public void Join_ResolvesRelativeProjectPaths()
        {
            Assert.Equal("src/B/B.csproj", ProjectGraph.Join("tests/A", "../../src/B/B.csproj"));
            Assert.Equal("tests/A/x.csproj", ProjectGraph.Join("tests/A", "./x.csproj"));
        }
    }
}
