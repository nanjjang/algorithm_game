using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Wizard.Ci.Tests
{
    // tools/ci 의 셸 스크립트는 #!/bin/bash 와 bash 3.2 문법만 쓴다(이 맥의 /bin/bash 는 3.2.57. T-40, PIPELINE 4.1).
    // 연관 배열, mapfile/readarray, ${x,,}·${x^^}, 이름 참조, coproc, |&, &>>, timeout, flock 이 없고, git 은 command git 으로만 부른다.
    // 이 작업이 만든 스크립트는 그 밖의 도구도 절대 경로로 부른다(/usr/bin/grep …). 셋 다 찾은 셸(BashLocator)의 -n 으로 문법을 확인한다:
    // 맥에서는 /bin/bash 3.2, Windows 에서는 Git Bash(5.2)다. 3.2 문법은 아래의 금지 구문 검사가 두 OS 에서 지킨다(T-67 2번).
    public sealed class ShellScriptTests
    {
        private static readonly string CiDir = Path.Combine(TempGit.RepoRoot, "tools", "ci");

        // M0-BT-05 의 소유 경로에 든 스크립트
        private static readonly string[] Owned =
        {
            "tools/ci/ci.sh", "tools/ci/install-hooks.sh", "tools/ci/tasks-check.sh", "tools/ci/agent-config-check.sh",
        };

        public static IEnumerable<object[]> AllScripts()
        {
            var files = new List<string>();
            foreach (string f in Directory.GetFiles(CiDir, "*", SearchOption.AllDirectories))
            {
                string rel = RepoPaths.Relative(TempGit.RepoRoot, f);
                if (rel.EndsWith(".sh", StringComparison.Ordinal) || rel.StartsWith("tools/ci/hooks/", StringComparison.Ordinal))
                {
                    files.Add(rel);
                }
            }
            files.Sort(StringComparer.Ordinal);
            return files.Select(f => new object[] { f });
        }

        private static bool IsOwned(string rel) =>
            Array.IndexOf(Owned, rel) >= 0 || rel.StartsWith("tools/ci/lib/", StringComparison.Ordinal) || rel.StartsWith("tools/ci/hooks/", StringComparison.Ordinal);

        private static readonly (Regex Rx, string What)[] Bash4 =
        {
            (new Regex("\\b(declare|local|typeset)\\s+-[a-zA-Z]*[An]"), "연관 배열·이름 참조(bash 4)"),
            (new Regex("\\b(mapfile|readarray|coproc)\\b"), "mapfile/readarray/coproc(bash 4)"),
            (new Regex("\\$\\{[A-Za-z_][A-Za-z0-9_]*(\\[[^]]*\\])?(,,?|\\^\\^?)\\}"), "${x,,}·${x^^}(bash 4)"),
            (new Regex("\\$\\{[^}]*@[QEPAaKk]\\}"), "${x@Q}(bash 4.4)"),
            (new Regex("\\|&|&>>"), "|& 또는 &>>(bash 4)"),
            (new Regex("(^|[\\s;&|(`])(timeout|flock|gtimeout)\\s"), "timeout/flock(이 맥에 없다. Wizard.Ci 가 한다)"),
            (new Regex("(^|[\\s;&|(`])sudo\\s"), "sudo"),
            (new Regex("push\\s+(-f|--force)"), "강제 푸시"),
        };

        // 명령 자리의 맨 git(앞이 command 가 아님)
        private static readonly Regex BareGit = new Regex("(^\\s*|[;&|(`]\\s*|\\$\\(\\s*|\\b(then|do|else)\\s+)git\\s");

        // 명령 자리의 상대 경로 도구(소유 스크립트만): /usr/bin/… 또는 /bin/… 으로 부른다
        private static readonly Regex BareTool = new Regex(
            "(^\\s*|[;&|(`]\\s*|\\$\\(\\s*|\\b(then|do|else)\\s+)(ls|cat|grep|sed|awk|find|rm|cp|mv|mkdir|chmod|head|tail|sort|tr|wc|cut|basename|dirname|sleep|cmp|tee|xargs|mktemp)\\s");

        private static IEnumerable<(int Line, string Text)> CodeLines(string rel)
        {
            string[] lines = File.ReadAllLines(RepoPaths.Combine(TempGit.RepoRoot, rel));
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();
                if (t.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                yield return (i + 1, lines[i]);
            }
        }

        [Theory]
        [MemberData(nameof(AllScripts))]
        public void Script_StartsWithBinBash_AndParsesUnderBinBash32(string rel)
        {
            string path = RepoPaths.Combine(TempGit.RepoRoot, rel);
            Assert.Equal("#!/bin/bash", File.ReadLines(path).First());
            ProcResult r = ProcessRunner.Run(new ProcRequest(TestBash.Path, new[] { "-n", path }) { Capture = true });
            Assert.True(r.ExitCode == 0, rel + ": " + r.Stderr);
        }

        [Theory]
        [MemberData(nameof(AllScripts))]
        public void Script_UsesOnlyBash32Syntax_AndCommandGit(string rel)
        {
            var problems = new List<string>();
            foreach ((int line, string text) in CodeLines(rel))
            {
                foreach ((Regex rx, string what) in Bash4)
                {
                    if (rx.IsMatch(text))
                    {
                        problems.Add(rel + ":" + line + ": " + what + ": " + text.Trim());
                    }
                }
                foreach (Match m in BareGit.Matches(text))
                {
                    string before = text.Substring(0, m.Index + m.Length - 4);
                    if (!before.TrimEnd().EndsWith("command", StringComparison.Ordinal))
                    {
                        problems.Add(rel + ":" + line + ": git 은 command git 으로 부른다: " + text.Trim());
                    }
                }
                if (IsOwned(rel))
                {
                    foreach (Match m in BareTool.Matches(text))
                    {
                        problems.Add(rel + ":" + line + ": '" + m.Groups[3].Value + "' 는 절대 경로로 부른다: " + text.Trim());
                    }
                }
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void TheLintItself_CatchesBash4Constructs()
        {
            string[] bad = { "declare -A map", "mapfile -t lines < f", "echo ${name,,}", "cmd |& tee x", "timeout 5 make", "x=$(git status)", "then git add x" };
            foreach (string b in bad)
            {
                bool hit = Bash4.Any(p => p.Rx.IsMatch(b)) || BareGit.IsMatch(b);
                Assert.True(hit, b);
            }
            Assert.DoesNotMatch(BareGit, "x=\"$(command git status)\"");
            Assert.Matches(BareGit, "    git status");
            Assert.Matches(BareTool, "x=$(basename \"$f\")");
            Assert.DoesNotMatch(BareTool, "x=$(/usr/bin/basename \"$f\")");
        }

        // git 이 기록한 모드가 100755 다(두 OS 에서 본다. T-67 1번). 파일의 유닉스 실행 권한은 Windows 에 대응이 없어서 맥·리눅스에서만 더 본다
        [Fact]
        public void ScriptsTheCardOwns_AreExecutable()
        {
            foreach (string rel in Owned.Concat(new[] { "tools/ci/hooks/pre-commit", "tools/ci/hooks/commit-msg" }))
            {
                string staged = TempGit.GitIn(TempGit.RepoRoot, "ls-files", "-s", "--", rel).Trim();
                Assert.True(staged.StartsWith("100755 ", StringComparison.Ordinal), rel + ": git 의 모드가 100755 가 아니다: '" + staged + "'");
                if (!OperatingSystem.IsWindows())
                {
                    UnixFileMode mode = File.GetUnixFileMode(RepoPaths.Combine(TempGit.RepoRoot, rel));
                    Assert.True((mode & UnixFileMode.UserExecute) != 0, rel + " is not executable");
                }
            }
        }
    }
}
