using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Wizard.VersionStamp
{
    // versionstamp --self-test. 수락 조건(M0-BT-03)을 가짜 저장소에서 확인한다.
    // 가짜 저장소는 <뿌리>/artifacts/selftest/versionstamp/ 아래에 만든다(커밋하지 않는 폴더). 시스템 임시 폴더를 쓰지 않는다.
    public static class SelfTest
    {
        private static readonly string[] HashedFiles =
        {
            "src/Wizard.Content/Defs/BlockDef.cs",
            "src/Wizard.Content/Pack/ContentDb.cs",
            "src/Wizard.Core/B.cs",
            "src/Wizard.Core/Fix64.cs",
            "src/Wizard.Core/Generated/SinTable.g.cs",
            "src/Wizard.Core/a.cs",
            "src/Wizard.Sim/Generated/Other.g.cs",
            "src/Wizard.Sim/Runtime/SimWorld.cs",
            "src/Wizard.Sim/Waves/WavePlan.cs",
        };

        private static readonly string[] IgnoredFiles =
        {
            "src/Wizard.Core/Errors/ErrorCode.cs",
            "src/Wizard.Core/Errors/Sub/Result.cs",
            "src/Wizard.Core/Generated/Handwritten.cs",
            "src/Wizard.Core/Generated/Nested/Deep.g.cs",
            "src/Wizard.Core/notes.txt",
            "src/Wizard.Content/Rules/ContentDb.Rules.cs",
            "src/Wizard.Content/Rules/Defs/ShopCatalog.cs",
            "src/Wizard.Sim/Generated/SimVersion.g.cs",
            "src/Wizard.Sim/Wizard.Sim.csproj",
            "src/Wizard.Rules/EnhanceRules.cs",
            "src/Wizard.CoreExtra/Leak.cs",
            "tests/Wizard.Sim.Tests/SimTests.cs",
            "tools/Wizard.TableGen/Program.cs",
            ".claude/worktrees/M9-XX-01/src/Wizard.Sim/Runtime/SimWorld.cs",
            ".claude/worktrees/M9-XX-01/src/Wizard.Core/Fix64.cs",
            "unity/Assets/Game/View.cs",
        };

        private sealed class Ctx
        {
            public readonly List<string> Failures = new List<string>();
            public int Passed;

            public void Check(string name, bool ok, string detail)
            {
                if (ok)
                {
                    Passed++;
                }
                else
                {
                    Failures.Add($"FAIL  versionstamp self-test {name}: {detail}");
                }
            }
        }

        public static int Run(string realRoot, TextWriter stdout, TextWriter stderr)
        {
            string baseDir = Path.Combine(realRoot, "artifacts", "selftest", "versionstamp");
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
            var ctx = new Ctx();
            string savedCwd = Directory.GetCurrentDirectory();
            try
            {
                ListMatchesT18(ctx, baseDir);
                KnownAnswer(ctx, baseDir);
                SameSourceSameBytes(ctx, baseDir, realRoot);
                ExcludedEditsDoNotChange(ctx, baseDir);
                IncludedEditsChange(ctx, baseDir);
                LineEndingsAndBom(ctx, baseDir);
                CheckModeDiagnostics(ctx, baseDir);
                RootFromOwnLocation(ctx, baseDir, realRoot);
                RealRepoScansSrcOnly(ctx, realRoot);
                RealSourceCopy(ctx, baseDir, realRoot);
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(baseDir))
                {
                    Directory.Delete(baseDir, true);
                }
            }
            foreach (string f in ctx.Failures)
            {
                stderr.WriteLine(f);
            }
            stdout.WriteLine($"versionstamp self-test: {ctx.Passed} passed, {ctx.Failures.Count} failed");
            return ctx.Failures.Count == 0 ? 0 : 1;
        }

        // 수락 4: 대상이 T-18 의 목록과 같다. 경로 오름차순(서수: 대문자 'B' < 소문자 'a').
        private static void ListMatchesT18(Ctx ctx, string baseDir)
        {
            string root = MakeRepo(baseDir, "list");
            var actual = Program.SourceFiles(root);
            var expected = HashedFiles.OrderBy(s => s, StringComparer.Ordinal).ToList();
            ctx.Check("list.exact", actual.SequenceEqual(expected),
                $"expected [{string.Join(", ", expected)}] got [{string.Join(", ", actual)}]");
            ctx.Check("list.ordinal-order", IndexOf(actual, "src/Wizard.Core/B.cs") < IndexOf(actual, "src/Wizard.Core/a.cs")
                && IndexOf(actual, "src/Wizard.Content/Pack/ContentDb.cs") < IndexOf(actual, "src/Wizard.Core/B.cs"),
                "paths are not in ordinal ascending order");
            foreach (string ignored in IgnoredFiles)
            {
                ctx.Check("list.excludes " + ignored, !Program.IsHashed(ignored) && !actual.Contains(ignored), "file must not be hashed");
            }
        }

        // 해시 입력의 형식: [경로, 0, LF 내용, 0]… 의 SHA-256 앞 16바이트. 테스트 쪽에서 따로 조립해 대조한다.
        private static void KnownAnswer(Ctx ctx, string baseDir)
        {
            string root = MakeRepo(baseDir, "known");
            var buf = new List<byte>();
            foreach (string rel in HashedFiles.OrderBy(s => s, StringComparer.Ordinal))
            {
                buf.AddRange(Encoding.UTF8.GetBytes(rel));
                buf.Add(0);
                buf.AddRange(Encoding.UTF8.GetBytes(Body(rel)));
                buf.Add(0);
            }
            byte[] digest = SHA256.HashData(buf.ToArray());
            var sb = new StringBuilder();
            for (int i = 0; i < 16; i++)
            {
                sb.Append(digest[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }
            string got = Program.Compute(root);
            ctx.Check("known-answer", got == sb.ToString(), $"expected {sb} got {got}");
            ctx.Check("format.hex32", got.Length == 32 && got.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')), got);

            // 빈 대상이면 빈 입력의 SHA-256 앞 16바이트(e3b0c442…)
            string empty = Path.Combine(baseDir, "empty");
            Directory.CreateDirectory(Path.Combine(empty, "src"));
            File.WriteAllText(Path.Combine(empty, "Wizard.sln"), "");
            ctx.Check("known-answer.empty", Program.Compute(empty) == "e3b0c44298fc1c149afbf4c8996fb924", Program.Compute(empty));
        }

        // 수락 1: 같은 소스에서 두 번 돌리면 바이트가 같다(가짜 저장소와 실제 저장소 둘 다).
        private static void SameSourceSameBytes(Ctx ctx, string baseDir, string realRoot)
        {
            string a = MakeRepo(baseDir, "same-a");
            string b = MakeRepo(baseDir, "same-b");
            ctx.Check("deterministic.copy", Program.Compute(a) == Program.Compute(b), "two identical trees hash differently");

            Quiet(Program.Run(a, "--write", TextWriter.Null, TextWriter.Null));
            byte[] first = File.ReadAllBytes(Path.Combine(a, Program.OutputRelative));
            Quiet(Program.Run(a, "--write", TextWriter.Null, TextWriter.Null));
            byte[] second = File.ReadAllBytes(Path.Combine(a, Program.OutputRelative));
            ctx.Check("deterministic.write-twice", first.AsSpan().SequenceEqual(second), "SimVersion.g.cs bytes differ between two runs");
            ctx.Check("deterministic.output-lf", !first.Contains((byte)'\r') && !(first.Length >= 3 && first[0] == 0xEF), "output must be LF without BOM");

            string r1 = Program.Compute(realRoot);
            string r2 = Program.Compute(realRoot);
            ctx.Check("deterministic.real-repo", r1 == r2, $"{r1} != {r2}");
        }

        // 수락 4: Errors/, Content/Rules/, SimVersion.g.cs, 목록 밖의 파일을 고쳐도 값이 같다.
        private static void ExcludedEditsDoNotChange(Ctx ctx, string baseDir)
        {
            string root = MakeRepo(baseDir, "excluded");
            string before = Program.Compute(root);
            foreach (string rel in IgnoredFiles)
            {
                string prev = Program.Compute(root);
                File.AppendAllText(Path.Combine(root, rel), "// edit\n");
                string after = Program.Compute(root);
                ctx.Check("unchanged-by " + rel, after == prev, $"CoreVersion changed {prev} -> {after}");
            }
            ctx.Check("unchanged-by all excluded edits", Program.Compute(root) == before, "excluded edits changed CoreVersion");
            // 목록 밖 폴더에 새 파일을 더해도 같다
            Write(root, "src/Wizard.Core/Errors/NewError.cs", "class NewError {}\n");
            Write(root, "src/Wizard.Content/Rules/NewRule.cs", "class NewRule {}\n");
            Write(root, ".claude/worktrees/M9-XX-02/src/Wizard.Sim/Extra.cs", "class Extra {}\n");
            ctx.Check("unchanged-by new excluded files", Program.Compute(root) == before, "adding excluded files changed CoreVersion");
        }

        // 수락 4: Sim 파일 한 글자를 고치면 바뀐다. Core·Content(Rules 밖)·표도 같다. 새 파일, 이름 바꾸기도 바뀐다.
        private static void IncludedEditsChange(Ctx ctx, string baseDir)
        {
            foreach (string rel in HashedFiles)
            {
                string root = MakeRepo(baseDir, "inc-" + rel.Replace('/', '_'));
                string before = Program.Compute(root);
                string path = Path.Combine(root, rel);
                string text = File.ReadAllText(path);
                int at = text.IndexOf('X');
                File.WriteAllText(path, text.Substring(0, at) + "Y" + text.Substring(at + 1));
                string after = Program.Compute(root);
                ctx.Check("changed-by one char in " + rel, after != before, "CoreVersion did not change");
            }
            string r = MakeRepo(baseDir, "inc-new");
            string b0 = Program.Compute(r);
            Write(r, "src/Wizard.Sim/Duel/NewThing.cs", "class NewThing {}\n");
            ctx.Check("changed-by new Sim file", Program.Compute(r) != b0, "adding a Sim file did not change CoreVersion");

            string m = MakeRepo(baseDir, "inc-move");
            string m0 = Program.Compute(m);
            File.Move(Path.Combine(m, "src/Wizard.Sim/Waves/WavePlan.cs"), Path.Combine(m, "src/Wizard.Sim/Waves/WavePlan2.cs"));
            ctx.Check("changed-by rename", Program.Compute(m) != m0, "renaming a Sim file did not change CoreVersion");
        }

        // 줄 끝을 LF 로 맞춘다: CRLF·CR·BOM 사본은 같은 값.
        private static void LineEndingsAndBom(Ctx ctx, string baseDir)
        {
            string lf = MakeRepo(baseDir, "eol-lf");
            string crlf = MakeRepo(baseDir, "eol-crlf");
            string cr = MakeRepo(baseDir, "eol-cr");
            string bom = MakeRepo(baseDir, "eol-bom");
            foreach (string rel in HashedFiles)
            {
                string body = Body(rel);
                File.WriteAllText(Path.Combine(crlf, rel), body.Replace("\n", "\r\n"), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(cr, rel), body.Replace("\n", "\r"), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(bom, rel), body, new UTF8Encoding(true));
            }
            string want = Program.Compute(lf);
            ctx.Check("eol.crlf", Program.Compute(crlf) == want, "CRLF copy hashes differently");
            ctx.Check("eol.cr", Program.Compute(cr) == want, "CR copy hashes differently");
            ctx.Check("eol.bom", Program.Compute(bom) == want, "BOM copy hashes differently");
        }

        // 일부러 틀린 입력: 낡은 SimVersion.g.cs, 없는 파일, 모르는 옵션은 정해진 진단으로 실패한다.
        private static void CheckModeDiagnostics(Ctx ctx, string baseDir)
        {
            string root = MakeRepo(baseDir, "check");
            var err = new StringWriter();
            int code = Program.Run(root, "--check", TextWriter.Null, err);
            ctx.Check("check.stale.exit", code == 1, $"exit {code}");
            ctx.Check("check.stale.message", err.ToString().StartsWith("FAIL  src/Wizard.Sim/Generated/SimVersion.g.cs is stale (expected CoreVersion ", StringComparison.Ordinal), err.ToString().Trim());

            ctx.Check("check.write", Program.Run(root, "--write", TextWriter.Null, TextWriter.Null) == 0, "write failed");
            ctx.Check("check.fresh", Program.Run(root, "--check", TextWriter.Null, TextWriter.Null) == 0, "check after write failed");

            // 손으로 고친 생성 파일(줄 끝만 CRLF 로 바꾼 것 포함)은 바이트가 달라 실패한다
            string outPath = Path.Combine(root, Program.OutputRelative);
            File.WriteAllText(outPath, File.ReadAllText(outPath).Replace("\n", "\r\n"));
            ctx.Check("check.crlf-output", Program.Run(root, "--check", TextWriter.Null, TextWriter.Null) == 1, "CRLF output was accepted");

            Quiet(Program.Run(root, "--write", TextWriter.Null, TextWriter.Null));
            File.AppendAllText(Path.Combine(root, "src/Wizard.Sim/Waves/WavePlan.cs"), "// one more line\n");
            ctx.Check("check.source-changed", Program.Run(root, "--check", TextWriter.Null, TextWriter.Null) == 1, "edited Sim source was not detected");

            File.Delete(outPath);
            var err2 = new StringWriter();
            ctx.Check("check.missing.exit", Program.Run(root, "--check", TextWriter.Null, err2) == 1, "missing file accepted");
            ctx.Check("check.missing.message", err2.ToString().StartsWith("FAIL  src/Wizard.Sim/Generated/SimVersion.g.cs is missing", StringComparison.Ordinal), err2.ToString().Trim());

            var err3 = new StringWriter();
            ctx.Check("check.unknown-option", Program.Run(root, "--frobnicate", TextWriter.Null, err3) == 2
                && err3.ToString().StartsWith("FAIL  unknown option", StringComparison.Ordinal), err3.ToString().Trim());

            var err4 = new StringWriter();
            string noSrc = Path.Combine(baseDir, "nosrc");
            Directory.CreateDirectory(noSrc);
            ctx.Check("check.no-src", Program.Run(noSrc, "--check", TextWriter.Null, err4) == 2
                && err4.ToString().StartsWith("FAIL  ", StringComparison.Ordinal), err4.ToString().Trim());
        }

        // 수락 5: 뿌리는 현재 폴더가 아니라 도구의 위치에서 구한다.
        private static void RootFromOwnLocation(Ctx ctx, string baseDir, string realRoot)
        {
            string fake = MakeRepo(baseDir, "cwd");
            Directory.SetCurrentDirectory(Path.Combine(fake, "src", "Wizard.Sim"));
            string found = RepoRoot.Find();
            ctx.Check("root.ignores-cwd", found == realRoot, $"Find() returned {found} while cwd was a different repository");
            ctx.Check("root.from-dll", Path.GetFullPath(AppContext.BaseDirectory).StartsWith(realRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal),
                $"tool runs from {AppContext.BaseDirectory}, outside {realRoot}");
            string nested = RepoRoot.FindFrom(Path.Combine(fake, "src", "Wizard.Sim", "Waves"));
            ctx.Check("root.walks-up", nested == fake, $"FindFrom returned {nested}");
        }

        // 수락 5: 실제 저장소에서도 src/ 아래만 센다(.claude/worktrees/ 의 사본, tests/, tools/ 는 세지 않는다).
        private static void RealRepoScansSrcOnly(Ctx ctx, string realRoot)
        {
            var files = Program.SourceFiles(realRoot);
            ctx.Check("real.nonempty", files.Count > 0, "no hashed files found in the repository");
            var bad = files.Where(f => !(f.StartsWith("src/Wizard.Core/", StringComparison.Ordinal)
                || f.StartsWith("src/Wizard.Content/", StringComparison.Ordinal)
                || f.StartsWith("src/Wizard.Sim/", StringComparison.Ordinal))).ToList();
            ctx.Check("real.src-only", bad.Count == 0, "outside the T-18 roots: " + string.Join(", ", bad));
            ctx.Check("real.no-generated-stamp", !files.Contains(Program.OutputRelative), "SimVersion.g.cs is hashed");
            ctx.Check("real.no-errors", !files.Any(f => f.StartsWith("src/Wizard.Core/Errors/", StringComparison.Ordinal)), "Core/Errors is hashed");
            ctx.Check("real.no-rules", !files.Any(f => f.StartsWith("src/Wizard.Content/Rules/", StringComparison.Ordinal)), "Content/Rules is hashed");
            ctx.Check("real.tables", files.Contains("src/Wizard.Core/Generated/SinTable.g.cs") && files.Contains("src/Wizard.Core/Generated/PowTables.g.cs"),
                "generated tables are not hashed");
            var sorted = files.OrderBy(s => s, StringComparer.Ordinal).ToList();
            ctx.Check("real.sorted", files.SequenceEqual(sorted), "real list is not in ordinal order");
        }

        // 수락 4를 실제 소스의 사본에서: 사본은 같은 값, Errors/·Content/Rules/ 를 고쳐도 같고, Sim 파일 한 글자를 고치면 바뀐다.
        private static void RealSourceCopy(Ctx ctx, string baseDir, string realRoot)
        {
            string copy = Path.Combine(baseDir, "realcopy");
            Directory.CreateDirectory(copy);
            File.WriteAllText(Path.Combine(copy, "Wizard.sln"), "");
            foreach (string top in new[] { "src/Wizard.Core", "src/Wizard.Content", "src/Wizard.Sim" })
            {
                string from = Path.Combine(realRoot, top);
                foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(realRoot, file);
                    string to = Path.Combine(copy, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    File.Copy(file, to);
                }
            }
            string real = Program.Compute(realRoot);
            ctx.Check("realcopy.same", Program.Compute(copy) == real, "a copy of src/ hashes differently from the repository");

            string[] excluded =
            {
                FirstUnder(copy, "src/Wizard.Core/Errors"),
                FirstUnder(copy, "src/Wizard.Content/Rules"),
                Program.OutputRelative,
            };
            foreach (string rel in excluded)
            {
                if (rel.Length == 0)
                {
                    ctx.Check("realcopy.excluded-file-exists", false, "no file found under an excluded folder");
                    continue;
                }
                File.AppendAllText(Path.Combine(copy, rel), "// edit\n");
                ctx.Check("realcopy.unchanged-by " + rel, Program.Compute(copy) == real, "editing an excluded file changed CoreVersion");
            }

            string sim = Program.SourceFiles(copy).First(f => f.StartsWith("src/Wizard.Sim/", StringComparison.Ordinal));
            string path = Path.Combine(copy, sim);
            byte[] bytes = File.ReadAllBytes(path);
            int at = Array.FindIndex(bytes, b => b >= (byte)'a' && b <= (byte)'y');
            bytes[at] = (byte)(bytes[at] + 1);
            File.WriteAllBytes(path, bytes);
            ctx.Check("realcopy.changed-by one char in " + sim, Program.Compute(copy) != real, "editing one character of a Sim file did not change CoreVersion");
        }

        private static string FirstUnder(string root, string relDir)
        {
            string abs = Path.Combine(root, relDir);
            if (!Directory.Exists(abs))
            {
                return "";
            }
            var files = Directory.EnumerateFiles(abs, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
            return files.Count == 0 ? "" : files[0];
        }

        private static string MakeRepo(string baseDir, string name)
        {
            string root = Path.Combine(baseDir, name);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "Wizard.sln"), "");
            foreach (string rel in HashedFiles.Concat(IgnoredFiles))
            {
                Write(root, rel, Body(rel));
            }
            return root;
        }

        // 파일마다 다른 내용. 'X' 를 하나 넣어 한 글자 바꾸기 시험에 쓴다.
        private static string Body(string rel) => "// " + rel + "\nnamespace Fixture\n{\n    class X { }\n}\n";

        private static void Write(string root, string rel, string text)
        {
            string path = Path.Combine(root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static int IndexOf(IReadOnlyList<string> list, string item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void Quiet(int code)
        {
            if (code != 0)
            {
                throw new InvalidOperationException("versionstamp self-test setup step failed with exit " + code);
            }
        }
    }
}
