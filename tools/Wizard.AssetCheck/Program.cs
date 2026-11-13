using System;
using System.IO;
using Wizard.AssetCheck.Check;
using Wizard.AssetCheck.Png;
using Wizard.AssetCheck.Report;
using Wizard.AssetCheck.Scaffold;

namespace Wizard.AssetCheck
{
    // assetcheck(PIPELINE 6.4절):
    //   check   [--root R]                     반입 검사(spec 14 5.7.1). 오류가 하나라도 있으면 1
    //   report  [--root R] [--out F]           자리마다 없음/반입됨/검수됨/실패 → artifacts/asset-report.json. 실패한 자리가 있으면 1
    //   scaffold <png> [--root R]              파일 이름 규칙에서 매니페스트 항목 초안을 보여 준다(쓰지 않는다)
    //   approve <slot id> [--root R]           반입됨인 자리를 art/production/approved.json 에 (id, 파일 SHA-256)으로 적는다
    // 저장소 뿌리는 --root 이거나 실행 파일의 위치에서 구한다(현재 폴더에 기대지 않는다). 사용법 오류는 2
    public static class Program
    {
        public const int ExitOk = 0;
        public const int ExitFail = 1;
        public const int ExitUsage = 2;

        public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

        public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            if (args.Length == 0)
            {
                return Usage(stderr);
            }
            string command = args[0];
            string? root = null;
            string? output = null;
            string? positional = null;
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--root":
                        if (++i >= args.Length) return Usage(stderr);
                        root = args[i];
                        break;
                    case "--out":
                        if (++i >= args.Length) return Usage(stderr);
                        output = args[i];
                        break;
                    default:
                        if (positional != null || args[i].StartsWith("--", StringComparison.Ordinal)) return Usage(stderr);
                        positional = args[i];
                        break;
                }
            }

            string repo;
            try
            {
                repo = AssetCheckRepo.FindRoot(root);
            }
            catch (DirectoryNotFoundException e)
            {
                stderr.WriteLine("FAIL  assetcheck: " + e.Message);
                return ExitUsage;
            }

            try
            {
                switch (command)
                {
                    case "check":
                        return positional == null ? Check(repo, stdout, stderr) : Usage(stderr);
                    case "report":
                        return positional == null ? ReportCmd(repo, output, stdout, stderr) : Usage(stderr);
                    case "scaffold":
                        return positional != null ? ScaffoldCmd(repo, positional, stdout, stderr) : Usage(stderr);
                    case "approve":
                        return positional != null ? Approve(repo, positional, stdout, stderr) : Usage(stderr);
                    default:
                        return Usage(stderr);
                }
            }
            catch (FileNotFoundException e)
            {
                stderr.WriteLine("FAIL  assetcheck: " + e.Message + " (" + e.FileName + ")");
                return ExitFail;
            }
            catch (InvalidDataException e)
            {
                stderr.WriteLine("FAIL  assetcheck: " + e.Message);
                return ExitFail;
            }
        }

        private static int Check(string repo, TextWriter stdout, TextWriter stderr)
        {
            ImportCheckResult result = ImportChecker.Run(AssetCheckRepo.Load(repo));
            int errors = 0, warnings = 0;
            foreach (Finding f in result.Findings)
            {
                if (f.IsError)
                {
                    errors++;
                    stderr.WriteLine(f.ToString());
                }
                else
                {
                    warnings++;
                }
            }
            string line = "assetcheck check: " + result.Input.Registry.Count + " record(s), " + errors + " error(s), " + warnings
                + " warning(s) (warnings are in assetcheck report), " + result.Missing.Count + " missing slot(s)";
            if (errors > 0)
            {
                stderr.WriteLine("FAIL  " + line);
                return ExitFail;
            }
            stdout.WriteLine(line);
            return ExitOk;
        }

        private static int ReportCmd(string repo, string? output, TextWriter stdout, TextWriter stderr)
        {
            ImportCheckResult result = ImportChecker.Run(AssetCheckRepo.Load(repo));
            ApprovalLedger ledger = ApprovalLedger.Load(AssetCheckRepo.Resolve(repo, ApprovalLedger.RepoPath));
            AssetReport report = AssetReport.Build(result, ledger);
            string path = output != null ? Path.GetFullPath(output) : AssetCheckRepo.Resolve(repo, AssetReport.DefaultOutput);
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(path, report.ToJson());
            string line = report.SummaryLine() + " -> " + path;
            if (report.CountOf(SlotStatus.Failed) > 0)
            {
                stderr.WriteLine("FAIL  " + line + " (failed slots list their problems in the report)");
                return ExitFail;
            }
            stdout.WriteLine(line);
            return ExitOk;
        }

        private static int ScaffoldCmd(string repo, string png, TextWriter stdout, TextWriter stderr)
        {
            string full = Path.GetFullPath(png);
            string art = Path.GetFullPath(AssetCheckRepo.Resolve(repo, AssetCheckRepo.ArtRoot)) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(art, StringComparison.Ordinal))
            {
                stderr.WriteLine("FAIL  assetcheck scaffold: " + full + " is not under " + art);
                return ExitFail;
            }
            if (!File.Exists(full))
            {
                stderr.WriteLine("FAIL  assetcheck scaffold: " + full + " does not exist");
                return ExitFail;
            }
            PngImage image;
            try
            {
                image = PngImage.Load(full);
            }
            catch (PngFormatException e)
            {
                stderr.WriteLine("FAIL  assetcheck scaffold: " + full + " is not a readable PNG: " + e.Message);
                return ExitFail;
            }
            string artPath = full.Substring(art.Length).Replace(Path.DirectorySeparatorChar, '/');
            ScaffoldResult draft = ManifestScaffold.Draft(artPath, image, AssetCheckRepo.Load(repo).Registry);
            if (!draft.Success)
            {
                stderr.WriteLine("FAIL  assetcheck scaffold: " + draft.Error);
                return ExitFail;
            }
            stdout.WriteLine("# draft for " + draft.Manifest + " (nothing was written; check it and add it by hand)");
            stdout.WriteLine(draft.Entry);
            foreach (string n in draft.Notes)
            {
                stdout.WriteLine("# " + n);
            }
            return ExitOk;
        }

        private static int Approve(string repo, string slotId, TextWriter stdout, TextWriter stderr)
        {
            ImportCheckResult result = ImportChecker.Run(AssetCheckRepo.Load(repo));
            string ledgerPath = AssetCheckRepo.Resolve(repo, ApprovalLedger.RepoPath);
            ApprovalLedger ledger = ApprovalLedger.Load(ledgerPath);
            AssetReport report = AssetReport.Build(result, ledger);
            SlotReport? slot = report.Find(slotId);
            if (slot == null)
            {
                stderr.WriteLine("FAIL  assetcheck approve: no slot '" + slotId + "' (slot ids are in " + AssetReport.DefaultOutput + ")");
                return ExitFail;
            }
            switch (slot.Status)
            {
                case SlotStatus.Missing:
                    stderr.WriteLine("FAIL  assetcheck approve: " + slotId + " has no picture");
                    return ExitFail;
                case SlotStatus.Failed:
                    stderr.WriteLine("FAIL  assetcheck approve: " + slotId + " fails the import check: " + string.Join("; ", slot.Problems));
                    return ExitFail;
                case SlotStatus.Approved:
                    stdout.WriteLine("assetcheck approve: " + slotId + " is already approved");
                    return ExitOk;
            }
            ledger.Set(new ApprovalEntry(slotId, slot.File!, slot.Sha256!));
            ledger.Save(ledgerPath);
            stdout.WriteLine("assetcheck approve: " + slotId + " (" + slot.File + ", sha256 " + slot.Sha256 + ") -> " + ApprovalLedger.RepoPath);
            return ExitOk;
        }

        private static int Usage(TextWriter stderr)
        {
            stderr.WriteLine("usage: assetcheck check|report [--out F]|scaffold <png>|approve <slot id> [--root R]");
            return ExitUsage;
        }
    }
}
