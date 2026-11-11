using System;
using System.Linq;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 단계 머리말(# ci-stage, # ci-timeout)로 검사를 모으는 규칙(PIPELINE 4.6)
    public sealed class StepScriptTests
    {
        [Fact]
        public void Collect_TakesHeaderedScriptsFromBothFolders_SortedByFileName_WithoutCiSh()
        {
            using TempGit g = TempGit.Create();
            g.Write("tools/ci/zeta.sh", "#!/bin/bash\n# ci-stage: full\necho z\n");
            g.Write("tools/ci/alpha.sh", "#!/bin/bash\n# 설명\n# ci-stage: fast\n# ci-timeout: 30\necho a\n");
            g.Write("tools/ci/lib/beta.sh", "#!/bin/bash\n# ci-stage: integration\n");
            g.Write("tools/ci/lib/helper.sh", "#!/bin/bash\n# 단계 머리말이 없는 보조 스크립트\n");
            g.Write("tools/ci/notes.txt", "# ci-stage: fast\n");
            g.Write("tools/ci/late.sh", "#!/bin/bash\n" + string.Concat(Enumerable.Repeat("#\n", StepScript.HeaderLines)) + "# ci-stage: fast\n");

            var steps = StepScript.Collect(g.Dir);
            Assert.Equal(new[] { "alpha", "beta", "zeta" }, steps.Select(s => s.Name).ToArray());
            Assert.Equal(new[] { "fast", "integration", "full" }, steps.Select(s => s.Stage).ToArray());
            Assert.Equal(new[] { "tools/ci/alpha.sh", "tools/ci/lib/beta.sh", "tools/ci/zeta.sh" }, steps.Select(s => s.RelativePath).ToArray());
            Assert.Equal(30, steps[0].TimeoutSeconds);
            Assert.Null(steps[1].TimeoutSeconds);
        }

        [Theory]
        [InlineData("# ci-stage: quick\n", "fast|full|integration")]
        [InlineData("# ci-stage: fast\n# ci-timeout: soon\n", "양의 정수")]
        [InlineData("# ci-stage: fast\n# ci-timeout: 0\n", "양의 정수")]
        public void BadHeaders_FailLoudly(string header, string expected)
        {
            using TempGit g = TempGit.Create();
            g.Write("tools/ci/bad.sh", "#!/bin/bash\n" + header);
            CiException e = Assert.Throws<CiException>(() => StepScript.Collect(g.Dir));
            Assert.Contains("tools/ci/bad.sh", e.Message, StringComparison.Ordinal);
            Assert.Contains(expected, e.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SameNameInBothFolders_IsRejected()
        {
            using TempGit g = TempGit.Create();
            g.Write("tools/ci/x.sh", "#!/bin/bash\n# ci-stage: fast\n");
            g.Write("tools/ci/lib/x.sh", "#!/bin/bash\n# ci-stage: full\n");
            CiException e = Assert.Throws<CiException>(() => StepScript.Collect(g.Dir));
            Assert.Contains("이름이 같다", e.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Find_AcceptsBareNamesExtensionsAndLibPaths()
        {
            using TempGit g = TempGit.Create();
            g.Write("tools/ci/one.sh", "#!/bin/bash\n# ci-stage: fast\n");
            g.Write("tools/ci/lib/two.sh", "#!/bin/bash\n");
            Assert.Equal("tools/ci/one.sh", StepScript.Find(g.Dir, "one").RelativePath);
            Assert.Equal("tools/ci/one.sh", StepScript.Find(g.Dir, "one.sh").RelativePath);
            Assert.Equal("tools/ci/lib/two.sh", StepScript.Find(g.Dir, "two.sh").RelativePath);
            Assert.Equal("tools/ci/lib/two.sh", StepScript.Find(g.Dir, "lib/two.sh").RelativePath);
            Assert.Equal("tools/ci/lib/two.sh", StepScript.Find(g.Dir, "tools/ci/lib/two.sh").RelativePath);
            Assert.Throws<CiException>(() => StepScript.Find(g.Dir, "three"));
            Assert.Throws<CiException>(() => StepScript.Find(g.Dir, "ci.sh"));
        }

        [Fact]
        public void RealRepository_StagesAreWellFormed_AndCoverTheCardsStages()
        {
            var steps = StepScript.Collect(TempGit.RepoRoot);
            string[] names = steps.Select(s => s.Name).ToArray();
            // M0-BT-05 이 더한 기본 검사
            Assert.Contains("meta-check", names);
            Assert.Contains("test-changed", names);
            Assert.Contains("vector-coverage", names);
            Assert.Equal("fast", steps.Single(s => s.Name == "task-branch").Stage);
            Assert.Contains("tasks-check", names);
            Assert.Contains("agent-config-check", names);
            Assert.Equal("full", steps.Single(s => s.Name == "unity-compile").Stage);
            Assert.Equal("full", steps.Single(s => s.Name == "unity-editmode").Stage);
            Assert.Equal("integration", steps.Single(s => s.Name == "unity-playmode").Stage);
            Assert.Equal("integration", steps.Single(s => s.Name == "unity-player-il2cpp").Stage);
            Assert.Equal("integration", steps.Single(s => s.Name == "dotnet-x64").Stage);
            Assert.DoesNotContain("ci", names);
            Assert.DoesNotContain("common", names);
            Assert.DoesNotContain("install-hooks", names);
        }
    }
}
