using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Names;

namespace Wizard.DataBuild
{
    // databuild: data/ JSON → 검증 → 이진 팩(.wzpack) + 이름 표 이진 절(.wznm). ARCHITECTURE 8.2·8.3, CONTRACTS 5·11.2, T-17·T-35.
    //   databuild check [--data-version <n>]                 G5: 꼴·스키마·변환·교차 검사(이름 열쇠 …)·결정성. 아무것도 쓰지 않는다
    //   databuild build [--out <폴더>] [--data-version <n>]   check 와 같은 검사 뒤 <폴더>/base.wzpack, <폴더>/names.ko.wznm 을 쓴다
    //                                                        (기본 <저장소>/artifacts/content. 커밋하지 않는다)
    //   databuild names --check                              이름 표 형식(정렬, 중복 없음, 한 줄에 열쇠 하나, 끝 표지 "~end", 정규 띄어쓰기)
    //   databuild names --fix                                정렬·중복·표지·띄어쓰기를 고쳐 다시 쓴다(값이 다른 같은 열쇠는 고치지 않고 실패)
    //   databuild names --scan                               제품 코드의 한글 리터럴, 작업용 이름의 로마자 표기(NamesScan. names-scan.sh)
    //   --root <폴더>                                        저장소 뿌리. 없으면 이 도구의 위치에서 위로 올라가며 Wizard.sln 을 찾는다
    // 종료 코드: 0 문제 없음, 1 문제 있음(한 줄에 하나씩 "FAIL  <경로>[:<위치>]: <이유>"), 2 사용법·환경 오류.
    // 변환기(ISectionConverter)와 교차 검사(IDataValidator)는 이 어셈블리에서 찾는다(ModuleRegistry). 이 파일을 고치지 않고 더한다.
    public static class Program
    {
        public const int ExitOk = 0;
        public const int ExitProblems = 1;
        public const int ExitUsage = 2;

        public const string PackFileName = "base.wzpack";
        public const string DefaultOutRelative = "artifacts/content";

        private const string Usage =
            "usage: databuild check [--data-version <n>] [--root <repo>]\n" +
            "       databuild build [--out <dir>] [--data-version <n>] [--root <repo>]\n" +
            "       databuild names --check | --fix | --scan [--root <repo>]";

        public static int Main(string[] args) => Run(args, Console.Out, Console.Error, null);

        // root 가 null 이면 --root 또는 도구의 위치에서 찾는다(테스트는 임시 저장소를 준다)
        public static int Run(string[] args, TextWriter stdout, TextWriter stderr, string? root)
        {
            if (args.Length == 0)
            {
                stderr.WriteLine(Usage);
                return ExitUsage;
            }
            string command = args[0];
            string? outDir = null;
            uint dataVersion = PackOptions.DefaultDataVersion;
            string? namesMode = null;
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--root":
                    case "--out":
                    case "--data-version":
                        if (i + 1 >= args.Length)
                        {
                            stderr.WriteLine("databuild: " + a + " needs a value\n" + Usage);
                            return ExitUsage;
                        }
                        string v = args[++i];
                        if (a == "--root")
                        {
                            root = Path.GetFullPath(v);
                        }
                        else if (a == "--out")
                        {
                            outDir = Path.GetFullPath(v);
                        }
                        else if (!uint.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out dataVersion))
                        {
                            stderr.WriteLine("databuild: --data-version must be a non-negative integer");
                            return ExitUsage;
                        }
                        break;
                    case "--check":
                    case "--fix":
                    case "--scan":
                        if (command != "names" || namesMode != null)
                        {
                            stderr.WriteLine("databuild: " + a + " belongs to 'names' (one of --check | --fix | --scan)\n" + Usage);
                            return ExitUsage;
                        }
                        namesMode = a;
                        break;
                    default:
                        stderr.WriteLine("databuild: unknown argument '" + a + "'\n" + Usage);
                        return ExitUsage;
                }
            }
            try
            {
                root ??= RepoRoot.Find();
            }
            catch (DataBuildException e)
            {
                stderr.WriteLine("databuild: " + e.Message);
                return ExitUsage;
            }
            switch (command)
            {
                case "check":
                    if (outDir != null)
                    {
                        stderr.WriteLine("databuild: check writes nothing (--out belongs to build)");
                        return ExitUsage;
                    }
                    return Check(root, dataVersion, null, stdout, stderr);
                case "build":
                    return Check(root, dataVersion, outDir ?? Path.Combine(root, DefaultOutRelative), stdout, stderr);
                case "names":
                    if (outDir != null)
                    {
                        stderr.WriteLine("databuild: names takes no --out");
                        return ExitUsage;
                    }
                    return NamesCommand(root, namesMode, stdout, stderr);
                default:
                    stderr.WriteLine("databuild: unknown command '" + command + "'\n" + Usage);
                    return ExitUsage;
            }
        }

        private static int Check(string root, uint dataVersion, string? outDir, TextWriter stdout, TextWriter stderr)
        {
            ModuleRegistry modules;
            try
            {
                modules = ModuleRegistry.Discover();
            }
            catch (DataBuildException e)
            {
                stderr.WriteLine("FAIL  databuild: " + e.Message);
                return ExitProblems;
            }
            var diagnostics = new Diagnostics();
            Pipeline.Output output = Pipeline.Run(root, modules, PackOptions.Default(dataVersion), diagnostics);
            if (diagnostics.HasErrors || output.Pack == null || output.Names == null)
            {
                diagnostics.WriteTo(stderr);
                stderr.WriteLine("databuild " + (outDir == null ? "check" : "build") + ": " + diagnostics.Count.ToString(CultureInfo.InvariantCulture) + " problem(s)");
                return ExitProblems;
            }
            string summary = "files " + output.FileCount.ToString(CultureInfo.InvariantCulture)
                + ", sections " + output.SectionCount.ToString(CultureInfo.InvariantCulture)
                + ", converters " + modules.Converters.Count.ToString(CultureInfo.InvariantCulture)
                + ", validators " + modules.Validators.Count.ToString(CultureInfo.InvariantCulture);
            if (outDir == null)
            {
                stdout.WriteLine("databuild check: ok (" + summary + ")");
                return ExitOk;
            }
            Directory.CreateDirectory(outDir);
            string pack = Path.Combine(outDir, PackFileName);
            string names = Path.Combine(outDir, NameTableWriter.FileName(NamesFile.Locale));
            File.WriteAllBytes(pack, output.Pack);
            File.WriteAllBytes(names, output.Names);
            stdout.WriteLine("databuild build: ok (" + summary + ") → " + pack + ", " + names);
            return ExitOk;
        }

        private static int NamesCommand(string root, string? mode, TextWriter stdout, TextWriter stderr)
        {
            if (mode == null)
            {
                stderr.WriteLine("databuild: names needs --check, --fix or --scan\n" + Usage);
                return ExitUsage;
            }
            if (mode == "--scan")
            {
                List<string> found = NamesScan.Run(root);
                foreach (string p in found)
                {
                    stderr.WriteLine("FAIL  " + p);
                }
                if (found.Count > 0)
                {
                    stderr.WriteLine("databuild names --scan: " + found.Count.ToString(CultureInfo.InvariantCulture) + " problem(s)");
                    return ExitProblems;
                }
                stdout.WriteLine("databuild names --scan: ok");
                return ExitOk;
            }
            string path = Path.Combine(root, NamesFile.RepoPath);
            if (!File.Exists(path))
            {
                stderr.WriteLine("FAIL  " + NamesFile.RepoPath + ": 이름 표가 없다");
                return ExitProblems;
            }
            string text = NamesFile.ReadUtf8(path);
            if (mode == "--check")
            {
                List<string> problems = NamesFile.Check(text);
                foreach (string p in problems)
                {
                    stderr.WriteLine("FAIL  " + NamesFile.RepoPath + ":" + p);
                }
                if (problems.Count > 0)
                {
                    stderr.WriteLine("databuild names --check: " + problems.Count.ToString(CultureInfo.InvariantCulture) + " problem(s). 고치기: databuild names --fix");
                    return ExitProblems;
                }
                stdout.WriteLine("databuild names --check: ok");
                return ExitOk;
            }
            var unfixable = new List<string>();
            string? fixedText = NamesFile.Fix(text, unfixable);
            if (fixedText == null)
            {
                foreach (string p in unfixable)
                {
                    stderr.WriteLine("FAIL  " + NamesFile.RepoPath + ":" + p);
                }
                stderr.WriteLine("databuild names --fix: 고치지 못한 문제 " + unfixable.Count.ToString(CultureInfo.InvariantCulture) + "개(파일을 쓰지 않았다)");
                return ExitProblems;
            }
            if (fixedText == text)
            {
                stdout.WriteLine("databuild names --fix: 바꿀 것이 없다");
                return ExitOk;
            }
            File.WriteAllBytes(path, new System.Text.UTF8Encoding(false).GetBytes(fixedText));
            stdout.WriteLine("databuild names --fix: " + NamesFile.RepoPath + " 를 정규 꼴로 고쳐 썼다");
            return ExitOk;
        }
    }
}
