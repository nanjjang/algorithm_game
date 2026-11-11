using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Wizard.VersionStamp
{
    // DECISIONS-TECH T-18, CONTRACTS 3.2. SimVersion.CoreVersion = 재실행에 영향을 주는 소스의 SHA-256 앞 16바이트(hex 32자).
    // 대상(T-18의 목록, 그대로):
    //   src/Wizard.Core/**       에서 Errors/ 와 Generated/ 밖의 .cs
    //   src/Wizard.Core/Generated/*.g.cs (표)
    //   src/Wizard.Content/**    에서 Rules/ 밖의 .cs
    //   src/Wizard.Sim/**        에서 Generated/SimVersion.g.cs 밖의 .cs
    // 해시 입력: 경로 오름차순(서수)으로 파일마다 [UTF-8 상대 경로('/' 구분), 0x00, 줄 끝을 LF 로 맞춘 UTF-8 내용(BOM 제거), 0x00].
    //   versionstamp              SimVersion.g.cs 를 다시 쓴다
    //   versionstamp --check      다시 만든 결과가 커밋된 파일과 바이트가 같은지 본다(다르면 1)
    //   versionstamp --print      값만 찍는다
    //   versionstamp --list       해시에 들어가는 파일을 순서대로 찍는다
    //   versionstamp --self-test  도구의 자체 테스트(SelfTest.cs)
    // 저장소 뿌리는 현재 폴더가 아니라 이 도구의 위치에서 구하고 src/ 만 훑는다(.claude/worktrees/ 의 사본을 세지 않는다).
    public static class Program
    {
        public const string OutputRelative = "src/Wizard.Sim/Generated/SimVersion.g.cs";

        private const string CoreDir = "src/Wizard.Core/";
        private const string CoreErrorsDir = "src/Wizard.Core/Errors/";
        private const string CoreGeneratedDir = "src/Wizard.Core/Generated/";
        private const string ContentDir = "src/Wizard.Content/";
        private const string ContentRulesDir = "src/Wizard.Content/Rules/";
        private const string SimDir = "src/Wizard.Sim/";

        public static int Main(string[] args)
        {
            string mode = args.Length == 0 ? "--write" : args[0];
            if (args.Length > 1)
            {
                Console.Error.WriteLine("FAIL  versionstamp takes at most one option (--check | --print | --list | --self-test)");
                return 2;
            }
            if (mode == "--self-test")
            {
                return SelfTest.Run(RepoRoot.Find(), Console.Out, Console.Error);
            }
            return Run(RepoRoot.Find(), mode, Console.Out, Console.Error);
        }

        public static int Run(string root, string mode, TextWriter stdout, TextWriter stderr)
        {
            switch (mode)
            {
                case "--write":
                case "--check":
                case "--print":
                case "--list":
                    break;
                default:
                    stderr.WriteLine($"FAIL  unknown option '{mode}' (--check | --print | --list | --self-test)");
                    return 2;
            }
            if (!Directory.Exists(Path.Combine(root, "src")))
            {
                stderr.WriteLine($"FAIL  {root}/src not found; versionstamp must run inside the repository");
                return 2;
            }
            if (mode == "--list")
            {
                foreach (string rel in SourceFiles(root))
                {
                    stdout.WriteLine(rel);
                }
                return 0;
            }
            string version = Compute(root);
            if (mode == "--print")
            {
                stdout.WriteLine(version);
                return 0;
            }
            string path = Path.Combine(root, OutputRelative);
            byte[] bytes = Encode(Render(version));
            if (mode == "--check")
            {
                if (!File.Exists(path))
                {
                    stderr.WriteLine($"FAIL  {OutputRelative} is missing (expected CoreVersion {version}). Run tools/Wizard.VersionStamp and commit the file.");
                    return 1;
                }
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                {
                    stderr.WriteLine($"FAIL  {OutputRelative} is stale (expected CoreVersion {version}). Run tools/Wizard.VersionStamp and commit the file.");
                    return 1;
                }
                stdout.WriteLine($"ok    {OutputRelative} ({version})");
                return 0;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            stdout.WriteLine($"wrote {OutputRelative} ({version})");
            return 0;
        }

        // T-18의 목록. 상대 경로는 '/' 구분, 서수 오름차순.
        public static IReadOnlyList<string> SourceFiles(string root)
        {
            var files = new List<string>();
            foreach (string top in new[] { "src/Wizard.Core", "src/Wizard.Content", "src/Wizard.Sim" })
            {
                string abs = Path.Combine(root, top);
                if (!Directory.Exists(abs))
                {
                    continue;
                }
                foreach (string file in Directory.EnumerateFiles(abs, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                    if (IsHashed(rel))
                    {
                        files.Add(rel);
                    }
                }
            }
            files.Sort(StringComparer.Ordinal);
            return files;
        }

        // rel: 저장소 뿌리 기준 '/' 구분 경로
        public static bool IsHashed(string rel)
        {
            if (!rel.EndsWith(".cs", StringComparison.Ordinal))
            {
                return false;
            }
            if (rel.StartsWith(CoreDir, StringComparison.Ordinal))
            {
                if (rel.StartsWith(CoreErrorsDir, StringComparison.Ordinal))
                {
                    return false;
                }
                if (rel.StartsWith(CoreGeneratedDir, StringComparison.Ordinal))
                {
                    string name = rel.Substring(CoreGeneratedDir.Length);
                    return name.IndexOf('/') < 0 && name.EndsWith(".g.cs", StringComparison.Ordinal);
                }
                return true;
            }
            if (rel.StartsWith(ContentDir, StringComparison.Ordinal))
            {
                return !rel.StartsWith(ContentRulesDir, StringComparison.Ordinal);
            }
            if (rel.StartsWith(SimDir, StringComparison.Ordinal))
            {
                return !string.Equals(rel, OutputRelative, StringComparison.Ordinal);
            }
            return false;
        }

        public static string Compute(string root)
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var zero = new byte[] { 0 };
            var utf8 = new UTF8Encoding(false);
            foreach (string rel in SourceFiles(root))
            {
                sha.AppendData(utf8.GetBytes(rel));
                sha.AppendData(zero);
                sha.AppendData(utf8.GetBytes(NormalizeText(File.ReadAllBytes(Path.Combine(root, rel)))));
                sha.AppendData(zero);
            }
            byte[] digest = sha.GetHashAndReset();
            return Convert.ToHexString(digest, 0, 16).ToLowerInvariant();
        }

        // UTF-8 로 읽고 BOM 을 떼고 줄 끝(CRLF, 홀로 선 CR)을 LF 로 맞춘다.
        public static string NormalizeText(byte[] raw)
        {
            string text = new UTF8Encoding(false).GetString(raw);
            if (text.Length > 0 && text[0] == '﻿')
            {
                text = text.Substring(1);
            }
            return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        }

        public static byte[] Encode(string text) => new UTF8Encoding(false).GetBytes(text);

        public static string Render(string version) =>
            "// <auto-generated>\n" +
            "// tools/Wizard.VersionStamp 이 만든 파일이다. 손으로 고치지 않는다. 코어 소스를 고치면 다시 만들어 같이 커밋한다(T-41).\n" +
            "// </auto-generated>\n" +
            "namespace Wizard.Sim\n" +
            "{\n" +
            "    public static partial class SimVersion\n" +
            "    {\n" +
            "        // 재실행에 영향을 주는 소스(T-18의 대상 목록) SHA-256의 앞 16바이트(hex 32자)\n" +
            $"        public const string CoreVersion = \"{version}\";\n" +
            "    }\n" +
            "}\n";
    }

    // 저장소 뿌리: 이 도구의 DLL 위치에서 위로 올라가며 Wizard.sln 이 있는 첫 폴더. 현재 폴더는 보지 않는다.
    public static class RepoRoot
    {
        public static string Find() => FindFrom(AppContext.BaseDirectory);

        public static string FindFrom(string start)
        {
            string? dir = Path.GetFullPath(start);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "Wizard.sln")))
                {
                    return dir.TrimEnd(Path.DirectorySeparatorChar);
                }
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            throw new InvalidOperationException("Wizard.sln not found above " + start);
        }
    }
}
