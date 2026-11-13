#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Wizard.TestKit.Shared;

namespace Wizard.Unity.App.Headless
{
    // 머리 없는 골든 실행의 명령 줄(ARCHITECTURE 4.3 Headless, T-30, T-39):
    //   <플레이어> -batchmode -nographics -golden <tests/golden 폴더> [-goldenPack <base.wzpack>] [-goldenOut <폴더>] [-goldenCombo <조합>]
    //   -golden       골든 뿌리(tests/golden/). 이것이 있어야 골든 실행이다
    //   -goldenPack   팩. 없으면 <저장소>/artifacts/golden/pack/base.wzpack(unity-golden.sh 가 databuild build 로 만든다)
    //   -goldenOut    결과 파일 golden-result.<조합>.json 을 쓸 폴더. 없으면 <저장소>/artifacts/golden
    //   -goldenCombo  이 실행이어야 하는 조합(예: macos-il2cpp-player-x64). 실제 조합과 다르면 돌리지 않고 실패한다
    //                 (x64 플레이어를 띄웠는데 arm64 로 돌았다 같은 실수를 결과 파일 이름이 덮지 않게)
    // <저장소>는 골든 뿌리에서 위로 올라가 찾는다(RepoRoot). 다른 기기(Windows)에서 저장소 없이 돌릴 때는 -goldenPack 과 -goldenOut 을 준다.
    public sealed class HeadlessArgs
    {
        public const string GoldenFlag = "-golden";
        public const string PackFlag = "-goldenPack";
        public const string OutFlag = "-goldenOut";
        public const string ComboFlag = "-goldenCombo";

        public const string DefaultPackRelative = "artifacts/golden/pack/base.wzpack";
        public const string DefaultOutRelative = "artifacts/golden";

        private HeadlessArgs(string goldenRoot, string packPath, string outDirectory, string? expectedCombo)
        {
            GoldenRoot = goldenRoot;
            PackPath = packPath;
            OutDirectory = outDirectory;
            ExpectedCombo = expectedCombo;
        }

        public string GoldenRoot { get; }

        public string PackPath { get; }

        public string OutDirectory { get; }

        public string? ExpectedCombo { get; }

        // 명령 줄에 -golden 이 있는가(값이 없어도 참. 그 경우 TryParse 가 까닭을 알려 준다)
        public static bool IsRequested(IReadOnlyList<string> argv)
        {
            for (int i = 0; i < argv.Count; i++)
            {
                if (string.Equals(argv[i], GoldenFlag, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool TryParse(IReadOnlyList<string> argv, out HeadlessArgs? args, out string error)
        {
            args = null;
            string? golden = null;
            string? pack = null;
            string? outDir = null;
            string? combo = null;
            for (int i = 0; i < argv.Count; i++)
            {
                string a = argv[i];
                if (a != GoldenFlag && a != PackFlag && a != OutFlag && a != ComboFlag)
                {
                    continue;                                   // Unity 의 인자(-batchmode, -logFile …)는 지나간다
                }
                if (i + 1 >= argv.Count || argv[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    error = a + " needs a value";
                    return false;
                }
                string v = argv[++i];
                switch (a)
                {
                    case GoldenFlag:
                        golden = v;
                        break;
                    case PackFlag:
                        pack = v;
                        break;
                    case OutFlag:
                        outDir = v;
                        break;
                    default:
                        combo = v;
                        break;
                }
            }
            if (golden == null)
            {
                error = GoldenFlag + " <tests/golden folder> is required";
                return false;
            }
            golden = Path.GetFullPath(golden);
            if (!Directory.Exists(golden))
            {
                error = "golden folder does not exist: " + golden;
                return false;
            }
            string? repo = null;
            if (pack == null || outDir == null)
            {
                try
                {
                    repo = RepoRoot.Find(golden);
                }
                catch (DirectoryNotFoundException)
                {
                    error = "no repository above " + golden + "; give " + PackFlag + " and " + OutFlag;
                    return false;
                }
            }
            pack = pack != null ? Path.GetFullPath(pack) : RepoRoot.Resolve(repo!, DefaultPackRelative);
            outDir = outDir != null ? Path.GetFullPath(outDir) : RepoRoot.Resolve(repo!, DefaultOutRelative);
            args = new HeadlessArgs(golden, pack, outDir, combo);
            error = string.Empty;
            return true;
        }
    }
}
