using System;
using System.Collections.Generic;
using System.IO;

namespace Wizard.MetaGen
{
    // Unity .meta 의 결정적 생성과 검사(T-34, CONTRACTS 11.5, ARCHITECTURE 4.2, PIPELINE 4.5).
    //   metagen                 src/ 와 unity/Assets/ 의 빠진 .meta 를 만든다. 이미 있는 것은 그대로 둔다.
    //   metagen --check         빠진 .meta, 주인 없는 .meta, 중복 GUID, 틀린 꼴의 .meta, src/·unity/Assets/Game/ 의 GUID 불일치를 찾는다.
    //   metagen --self-test     도구 자체 테스트(임시 폴더에서 돈다. 저장소를 건드리지 않는다).
    //   --root <폴더>           저장소 뿌리를 직접 준다. 없으면 이 도구의 위치에서 위로 올라가며 Wizard.sln 을 찾는다.
    // 종료 코드: 0 문제 없음, 1 문제 있음(--check) 또는 자체 테스트 실패, 2 사용법·환경 오류.
    public static class Program
    {
        public const int ExitOk = 0;
        public const int ExitProblems = 1;
        public const int ExitUsage = 2;

        public static int Main(string[] args)
        {
            return Run(args, Console.Out, Console.Error, AppContext.BaseDirectory);
        }

        public static int Run(string[] args, TextWriter stdout, TextWriter stderr, string toolDirectory)
        {
            bool check = false;
            bool selfTest = false;
            string? root = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--check":
                        check = true;
                        break;
                    case "--self-test":
                        selfTest = true;
                        break;
                    case "--root":
                        if (i + 1 >= args.Length)
                        {
                            stderr.WriteLine("metagen: --root needs a folder");
                            return ExitUsage;
                        }
                        root = args[++i];
                        break;
                    case "-h":
                    case "--help":
                        stdout.WriteLine("usage: metagen [--check | --self-test] [--root <repo root>]");
                        return ExitOk;
                    default:
                        stderr.WriteLine("metagen: unknown argument '" + args[i] + "' (usage: metagen [--check | --self-test] [--root <repo root>])");
                        return ExitUsage;
                }
            }
            if (check && selfTest)
            {
                stderr.WriteLine("metagen: --check and --self-test cannot be combined");
                return ExitUsage;
            }
            if (selfTest)
            {
                return SelfTest.Run(stdout, stderr) == 0 ? ExitOk : ExitProblems;
            }

            if (root == null)
            {
                root = RepoPath.FindRepoRoot(toolDirectory);
                if (root == null)
                {
                    stderr.WriteLine("metagen: cannot find the repo root (Wizard.sln + global.json) above " + toolDirectory + "; pass --root <folder>");
                    return ExitUsage;
                }
            }
            root = Path.GetFullPath(root);
            if (!Directory.Exists(root))
            {
                stderr.WriteLine("metagen: root folder does not exist: " + root);
                return ExitUsage;
            }

            AssetTree tree = AssetTree.Scan(root);
            // 범위 폴더가 없으면 조용히 넘어가지 않는다(잘못된 뿌리를 받았을 가능성이 크다).
            foreach (string scope in AssetTree.Scopes)
            {
                if (!Directory.Exists(Path.Combine(root, scope)))
                {
                    stderr.WriteLine("metagen: " + scope + "/ does not exist under " + root + " (wrong --root?)");
                    return ExitUsage;
                }
            }

            return check ? RunCheck(root, tree, stdout, stderr) : RunGenerate(root, tree, stdout, stderr);
        }

        private static int RunGenerate(string root, AssetTree tree, TextWriter stdout, TextWriter stderr)
        {
            List<string> created;
            try
            {
                created = MetaGenerator.Generate(root, tree);
            }
            catch (IOException e)
            {
                stderr.WriteLine("metagen: could not write a .meta: " + e.Message);
                return ExitUsage;
            }
            foreach (string path in created)
            {
                stdout.WriteLine("created " + path);
            }
            stdout.WriteLine("metagen: created " + created.Count + ", kept " + tree.Metas.Count + " existing");
            return ExitOk;
        }

        private static int RunCheck(string root, AssetTree tree, TextWriter stdout, TextWriter stderr)
        {
            List<MetaProblem> problems = MetaChecker.Check(root, tree);
            foreach (MetaProblem problem in problems)
            {
                stderr.WriteLine("metagen: " + problem);
            }
            if (problems.Count > 0)
            {
                stderr.WriteLine("metagen --check: " + problems.Count + " problem(s) in " + string.Join(", ", tree.ScopesFound));
                return ExitProblems;
            }
            stdout.WriteLine("metagen --check: ok (" + tree.Assets.Count + " assets, " + tree.Metas.Count + " .meta in " + string.Join(", ", tree.ScopesFound) + ")");
            return ExitOk;
        }
    }
}
