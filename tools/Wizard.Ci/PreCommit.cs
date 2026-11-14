using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Wizard.Ci
{
    // pre-commit 훅의 검사(PIPELINE 4.6 "커밋 훅"): fast 의 정적 검사 가운데 빠른 것을, 올린(staged) 경로에 닿는 것만 돈다.
    //   1. 금지 API 빠른 검사: 올린 .cs 의 내용(index)을 구문으로 본다(ForbiddenApiScan). 정본은 분석기다
    //   2. 진단 끄기 금지(T-42): 올린 .editorconfig/.globalconfig 의 dotnet_diagnostic.WZ, csproj/props/targets 의 NoWarn·WarningsNotAsErrors 의 WZ,
    //      .cs 의 '#pragma warning disable WZ…'·SuppressMessage("…", "WZ…"), 공유 패키지 csc.rsp 의 nowarn·warnaserror-
    //   3. 이름 표 형식(T-35): data/names/names.ko.json 을 올렸으면 그 내용(index)
    //   4. 스크립트 검사(작업 트리에서 돈다):
    //        src/ 또는 unity/Assets/ 에 닿으면                        tools/ci/lib/meta-check.sh(.meta, T-34)
    //        docs/dev/tasks.json·tasks.schema.json·docs/spec/ 에 닿으면  tools/ci/tasks-check.sh
    //        CLAUDE.md·.claude/agents/·docs/dev/agent-config/ 에 닿으면  tools/ci/agent-config-check.sh
    //        프로젝트 파일(csproj, asmdef, package.json, csc.rsp, Wizard.sln, global.json, Directory.*)에 닿으면  tools/ci/layout-check.sh
    // 빌드, 분석기 탐침, 테스트는 ci.sh fast 가 한다(커밋마다 돌리기에는 느리다).
    public static class PreCommit
    {
        private static readonly Regex EditorConfigWz = new Regex("dotnet_diagnostic\\.WZ", RegexOptions.CultureInvariant);
        private static readonly Regex ProjectWz = new Regex("<(NoWarn|WarningsNotAsErrors)>[^<]*WZ", RegexOptions.CultureInvariant);
        private static readonly Regex PragmaWz = new Regex("^\\s*#\\s*pragma\\s+warning\\s+disable\\b[^\\n]*WZ", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private static readonly Regex SuppressWz = new Regex("SuppressMessage\\s*\\([^)]*\"WZ", RegexOptions.CultureInvariant);
        private static readonly Regex RspOff = new Regex("nowarn|warnaserror-", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // bash: 스크립트를 띄울 셸(BashLocator). 경로는 '/' 구분으로 넘긴다(T-67 3·4번)
        public static int Run(string root, TextWriter output, string bash)
        {
            List<string> staged = Git.Paths(root, "diff", "--cached", "--name-only", "-z", "--diff-filter=ACMR");
            List<string> touched = Git.Paths(root, "diff", "--cached", "--name-only", "-z", "--no-renames");
            var problems = new List<string>();
            foreach (string path in staged)
            {
                string? text = null;
                string Content() => text ??= Git.Run(root, "show", ":" + path);
                ForbiddenApiScan.Role role = ForbiddenApiScan.RoleOf(path);
                if (role != ForbiddenApiScan.Role.None)
                {
                    problems.AddRange(ForbiddenApiScan.Scan(path, Content(), role));
                }
                problems.AddRange(SuppressionProblems(path, Content));
                if (path == NamesFormat.Path)
                {
                    problems.AddRange(NamesFormat.Check(Content()));
                }
            }

            var scripts = ScriptsFor(touched);
            foreach (string script in scripts)
            {
                output.WriteLine("pre-commit: " + script);
                output.Flush();
                var req = new ProcRequest(bash, new[] { BashLocator.SlashPath(RepoPaths.Combine(root, script)) })
                {
                    Name = script,
                    WorkingDirectory = root,
                    Timeout = TimeSpan.FromMinutes(10),
                    Echo = output,
                    EchoError = output,
                };
                req.Environment["WIZARD_BASH"] = bash;
                req.Environment["WIZARD_ROOT"] = BashLocator.SlashPath(root);
                ProcResult r = ProcessRunner.Run(req);
                if (r.ExitCode != 0)
                {
                    problems.Add(script + " 이 실패했다(exit " + r.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
                }
            }

            foreach (string p in problems)
            {
                output.WriteLine("FAIL  " + p);
            }
            if (problems.Count > 0)
            {
                output.WriteLine("pre-commit: 커밋을 거절한다(" + problems.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                 "건). 고친 뒤 다시 올린다(git add). 훅을 끄지 않는다");
                return 1;
            }
            output.WriteLine("pre-commit: ok (올린 파일 " + staged.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "개, 스크립트 검사 " +
                             scripts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "개)");
            return 0;
        }

        public static List<string> SuppressionProblems(string path, Func<string> content)
        {
            var list = new List<string>();
            string name = Path.GetFileName(path);
            if (name == ".editorconfig" || name.EndsWith(".globalconfig", StringComparison.Ordinal))
            {
                if (EditorConfigWz.IsMatch(content()))
                {
                    list.Add(path + ": dotnet_diagnostic.WZ… 로 분석기 진단을 바꾸지 않는다(T-42)");
                }
            }
            else if (name.EndsWith(".csproj", StringComparison.Ordinal) || name.EndsWith(".props", StringComparison.Ordinal) ||
                     name.EndsWith(".targets", StringComparison.Ordinal))
            {
                if (ProjectWz.IsMatch(content()))
                {
                    list.Add(path + ": NoWarn·WarningsNotAsErrors 에 WZ 진단을 넣지 않는다(T-42)");
                }
            }
            else if (name.EndsWith(".cs", StringComparison.Ordinal))
            {
                string c = content();
                if (PragmaWz.IsMatch(c) || SuppressWz.IsMatch(c))
                {
                    list.Add(path + ": WZ 진단을 억제하지 않는다(#pragma warning disable, SuppressMessage. CLAUDE.md 3절)");
                }
            }
            else if (name.EndsWith(".rsp", StringComparison.Ordinal) &&
                     (path.StartsWith("src/", StringComparison.Ordinal) || path.StartsWith("tests/Wizard.TestKit/Shared/", StringComparison.Ordinal)))
            {
                if (RspOff.IsMatch(content()))
                {
                    list.Add(path + ": csc.rsp 로 경고를 끄거나 낮추지 않는다(T-42)");
                }
            }
            return list;
        }

        // 올린 경로(지운 것 포함)에 따라 돌릴 스크립트(저장소 기준, 순서 고정)
        public static List<string> ScriptsFor(IReadOnlyList<string> touched)
        {
            bool meta = false, tasks = false, agents = false, layout = false;
            foreach (string p in touched)
            {
                string name = Path.GetFileName(p);
                meta |= p.StartsWith("src/", StringComparison.Ordinal) || p.StartsWith("unity/Assets/", StringComparison.Ordinal);
                tasks |= p == "docs/dev/tasks.json" || p == "docs/dev/tasks.schema.json" || p.StartsWith("docs/spec/", StringComparison.Ordinal) ||
                         p == "tests/vectors.config.json";
                agents |= p == "CLAUDE.md" || p.StartsWith(".claude/agents/", StringComparison.Ordinal) ||
                          p.StartsWith("docs/dev/agent-config/", StringComparison.Ordinal);
                layout |= name.EndsWith(".csproj", StringComparison.Ordinal) || name.EndsWith(".asmdef", StringComparison.Ordinal) ||
                          name == "package.json" || name == "csc.rsp" || p == "Wizard.sln" || p == "global.json" ||
                          name.StartsWith("Directory.Build.", StringComparison.Ordinal) || p == "Directory.Packages.props";
            }
            var list = new List<string>();
            if (meta)
            {
                list.Add("tools/ci/lib/meta-check.sh");
            }
            if (tasks)
            {
                list.Add("tools/ci/tasks-check.sh");
            }
            if (agents)
            {
                list.Add("tools/ci/agent-config-check.sh");
            }
            if (layout)
            {
                list.Add("tools/ci/layout-check.sh");
            }
            return list;
        }
    }
}
