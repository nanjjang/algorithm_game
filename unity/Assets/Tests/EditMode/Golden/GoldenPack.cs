#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Wizard.Content;
using Wizard.TestKit.Shared;

namespace Wizard.Unity.Tests.EditMode.Golden
{
    // 골든을 돌릴 팩: 저장소 data/ 로 만든 기본 팩(tests/golden/m1/README.md "팩은 저장소 data/로 만든 팩"). dotnet 쪽은 같은 팩을
    // 메모리에서 만든다(ReplayCli RepoPack). Unity 는 DataBuild(net10 도구)를 실을 수 없으므로 databuild build 를 한 번 돌려 파일로 받는다.
    //   - WIZARD_GOLDEN_PACK 이 있으면 그 파일을 쓴다(unity-golden.sh 가 이미 만들어 준다)
    //   - 없으면 dotnet run --project tools/Wizard.DataBuild -c Release -- build --out <저장소>/artifacts/golden/pack 을 돌린다
    //     (이 편집기 세션에서 한 번. data/ 가 바뀌었는데 옛 팩으로 도는 일이 없게 남은 파일을 믿지 않는다)
    // dotnet 이 없거나 빌드가 실패하면 시험은 건너뛰지 않고 실패한다.
    internal static class GoldenPack
    {
        public const string EnvVar = "WIZARD_GOLDEN_PACK";
        private const int BuildTimeoutMs = 600000;

        private static string? s_builtThisSession;

        public static string Repo => RepoRoot.Find(Application.dataPath);

        public static string GoldenRoot => RepoRoot.Resolve(Repo, "tests/golden");

        public static ContentDb LoadDb() => ContentDb.Build(ContentPack.Load(File.ReadAllBytes(PathOrBuild())));

        public static string PathOrBuild()
        {
            string? given = Environment.GetEnvironmentVariable(EnvVar);
            if (!string.IsNullOrEmpty(given))
            {
                Assert.That(File.Exists(given), Is.True, EnvVar + " points to a missing file: " + given);
                return given!;
            }
            if (s_builtThisSession != null && File.Exists(s_builtThisSession))
            {
                return s_builtThisSession;
            }
            string repo = Repo;
            string outDir = RepoRoot.Resolve(repo, "artifacts/golden/pack");
            string dotnet = FindDotnet();
            var args = new List<string>
            {
                "run", "--project", RepoRoot.Resolve(repo, "tools/Wizard.DataBuild"), "-c", "Release", "--",
                "build", "--out", outDir, "--root", repo,
            };
            int code = RunProcess(dotnet, args, repo, out string output);
            string pack = Path.Combine(outDir, "base.wzpack");
            Assert.That(code, Is.EqualTo(0), "databuild build failed (" + dotnet + " " + string.Join(" ", args) + "):\n" + output);
            Assert.That(File.Exists(pack), Is.True, "databuild build did not write " + pack + ":\n" + output);
            s_builtThisSession = pack;
            return pack;
        }

        private static string FindDotnet()
        {
            string exe = Application.platform == RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet";
            var candidates = new List<string>();
            string? host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(host))
            {
                candidates.Add(host!);
            }
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                foreach (string dir in path!.Split(System.IO.Path.PathSeparator))
                {
                    if (dir.Length > 0)
                    {
                        candidates.Add(System.IO.Path.Combine(dir, exe));
                    }
                }
            }
            candidates.Add("/usr/local/share/dotnet/dotnet");
            candidates.Add("/opt/homebrew/bin/dotnet");
            string? home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                candidates.Add(System.IO.Path.Combine(home!, ".dotnet", exe));
            }
            foreach (string c in candidates)
            {
                if (File.Exists(c))
                {
                    return c;
                }
            }
            Assert.Fail("dotnet not found (looked in DOTNET_HOST_PATH, PATH, /usr/local/share/dotnet, /opt/homebrew/bin, ~/.dotnet). " +
                        "The golden pack is built with `dotnet run --project tools/Wizard.DataBuild -- build`; install the .NET SDK of global.json " +
                        "or set " + EnvVar + " to a built base.wzpack");
            return string.Empty;
        }

        private static int RunProcess(string exe, List<string> args, string workingDirectory, out string output)
        {
            var quoted = new List<string>(args.Count);
            foreach (string a in args)
            {
                quoted.Add("\"" + a.Replace("\"", "\\\"") + "\"");
            }
            var info = new ProcessStartInfo(exe, string.Join(" ", quoted))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
            };
            info.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            info.EnvironmentVariables["DOTNET_NOLOGO"] = "1";
            using (Process p = Process.Start(info)!)
            {
                Task<string> stdout = p.StandardOutput.ReadToEndAsync();
                Task<string> stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(BuildTimeoutMs))
                {
                    try
                    {
                        p.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    output = "timed out after " + (BuildTimeoutMs / 1000) + " s";
                    return -1;
                }
                p.WaitForExit();
                output = stdout.Result + stderr.Result;
                return p.ExitCode;
            }
        }
    }
}
