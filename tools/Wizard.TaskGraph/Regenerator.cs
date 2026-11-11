using System.Collections.Generic;

namespace Wizard.TaskGraph
{
    // "다시 만드는 파일"(T-41)과 MetaGen 이 만드는 .meta 를 대조하는 방법 하나.
    // 명령은 브랜치 끝을 꺼낸 임시 작업 트리에서 돈다. 인자의 "{root}" 는 그 작업 트리의 절대 경로로 바뀐다.
    //   ExitCode  : 명령이 0 으로 끝나면 일치(도구의 --check 가 커밋된 바이트와 자기 출력을 비교한다)
    //   GitStatus : 명령(쓰기)을 돌린 뒤 바뀐 파일이 git status 에 나오지 않으면 일치
    public sealed class Regenerator
    {
        public Regenerator(string name, IReadOnlyList<string> globs, IReadOnlyList<string[]> commands, bool compareByGitStatus)
        {
            Name = name;
            Globs = globs;
            Commands = commands;
            CompareByGitStatus = compareByGitStatus;
        }

        public string Name { get; }
        public IReadOnlyList<string> Globs { get; }
        public IReadOnlyList<string[]> Commands { get; }
        public bool CompareByGitStatus { get; }

        public bool Covers(string path)
        {
            foreach (string g in Globs)
            {
                if (PathPattern.Glob(g, path))
                {
                    return true;
                }
            }
            return false;
        }

        // 저장소의 도구(PIPELINE 4.4절의 표, tools/ci/gen-check.sh, CLAUDE.md 9절의 명령)
        public static IReadOnlyList<Regenerator> Defaults()
        {
            static string[] DotnetTool(string project, params string[] args)
            {
                var a = new List<string> { "dotnet", "run", "--project", "{root}/tools/" + project + "/" + project + ".csproj", "-c", "Release", "--" };
                a.AddRange(args);
                return a.ToArray();
            }
            return new[]
            {
                new Regenerator("versionstamp·tablegen", new[] { "src/*/Generated/*.g.cs" },
                    new[] { DotnetTool("Wizard.TableGen", "--check"), DotnetTool("Wizard.VersionStamp", "--check") }, false),
                new Regenerator("replaycli golden", new[] { "tests/golden/**/expected*.json" },
                    new[] { DotnetTool("Wizard.ReplayCli", "golden", "--update") }, true),
                new Regenerator("metagen", new[] { "src/**/*.meta", "unity/Assets/**/*.meta" },
                    new[] { DotnetTool("Wizard.MetaGen", "--check") }, false),
            };
        }
    }
}
