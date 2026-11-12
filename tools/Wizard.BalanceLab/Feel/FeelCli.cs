using System;
using System.Collections.Generic;
using System.IO;
using Wizard.BalanceLab.Core;
using Wizard.Content;

namespace Wizard.BalanceLab.Feel
{
    // balancelab feel 의 몸통(출력 스트림을 받아 시험할 수 있다).
    //
    //   balancelab feel <기록.wzrc> [--practice-minutes N] [--attempt-window-ms N] [--skip-hash-check] [--no-detail]
    //       사람이 플레이한 연습 기록에서 세 축을 뽑는다: 두 번 누르는 커맨드 성공률, 섬 성공률, 자세 중 오발 비율
    //   balancelab feel --bot [--seconds N] [--core bolt|fireball] [--split]
    //       봇 입력으로 "임계 구간 플레이 ÷ 꾹 누르기" 화력 비를 잰다
    //   balancelab feel <기록.wzrc> --bot    네 축 전부
    //   balancelab feel --selfcheck          도구 자체 점검(명세에서 나온 기대값과 일부러 틀린 입력)
    //   balancelab feel --write-sample <파일> 각본으로 만든 시험용 기록(사용 예)
    //   공통: --repo <저장소> (기본: 이 도구를 빌드한 체크아웃), --out <JSON 파일> (기본: 표준 출력)
    //
    // 출력: 판정 근거 JSON(표준 출력 또는 --out). 한 줄 요약은 표준 오류.
    // 종료 코드: 0 모든 축 통과, 1 하나라도 미달, 3 자료 부족(시도 없음), 4 입력 오류, 64 사용법.
    internal static class FeelCli
    {
        private const long DefaultBotSeconds = 120;
        private const int TicksPerSecond = Wizard.Sim.SimVersion.TickRate;

        private static readonly HashSet<string> ValueOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            "repo", "out", "seconds", "core", "practice-minutes", "attempt-window-ms", "write-sample",
        };

        private static readonly HashSet<string> FlagOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            "bot", "split", "selfcheck", "skip-hash-check", "no-detail", "help",
        };

        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (!CliArgs.TryParse(args, ValueOptions, FlagOptions, out CliArgs cli, out string parseError))
            {
                error.WriteLine("feel: " + parseError);
                return Usage(error);
            }
            if (cli.Has("help"))
            {
                Usage(output);
                output.Write(Protocol);
                return ExitCodes.Pass;
            }
            if (cli.Positionals.Count > 1)
            {
                return Usage(error);
            }
            bool selfcheck = cli.Has("selfcheck");
            bool bot = cli.Has("bot");
            string? recordPath = cli.Positionals.Count == 1 ? cli.Positionals[0] : null;
            if (!selfcheck && !bot && recordPath == null && !cli.TryGet("write-sample", out _))
            {
                return Usage(error);
            }
            if (!cli.TryGetUInt("seconds", DefaultBotSeconds, out long seconds, out string e1)
                || !cli.TryGetUInt("practice-minutes", FeelTargets.PracticeMinutes, out long minutes, out e1)
                || !cli.TryGetUInt("attempt-window-ms", FeelTargets.AttemptWindowMsDefault, out long windowMs, out e1))
            {
                error.WriteLine("feel: " + e1);
                return Usage(error);
            }
            if (!TryParseCore(cli, out Wizard.Content.BlockId core, out string coreError))
            {
                error.WriteLine("feel: " + coreError);
                return Usage(error);
            }

            cli.TryGet("repo", out string? repoArg);
            if (!RepoLocator.TryFind(repoArg, out string repoRoot))
            {
                error.WriteLine("feel: cannot find the repository root (Wizard.sln + data/); pass --repo <dir>");
                return ExitCodes.InputError;
            }
            byte[]? file = null;
            if (recordPath != null)
            {
                try
                {
                    file = File.ReadAllBytes(recordPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    error.WriteLine("feel: cannot read '" + recordPath + "': " + e.Message);
                    return ExitCodes.InputError;
                }
            }
            ContentDb db;
            try
            {
                db = LabPack.Build(repoRoot);
            }
            catch (InvalidOperationException e)
            {
                error.WriteLine("feel: " + e.Message);
                return ExitCodes.InputError;
            }

            if (selfcheck)
            {
                return RunSelfCheck(db, output, error);
            }
            if (cli.TryGet("write-sample", out string? samplePath))
            {
                File.WriteAllBytes(samplePath!, FeelSelfCheck.SampleRecord(db));
                return ExitCodes.Pass;
            }

            FireRatioReport? fire = null;
            if (bot)
            {
                fire = FireRatioMeter.Measure(db, (uint)(seconds * TicksPerSecond), core, cli.Has("split"));
            }
            FeelRecordInfo? info = null;
            FeelResult? feel = null;
            var options = new FeelOptions { PracticeMinutes = minutes, AttemptWindowMs = windowMs };
            if (file != null)
            {
                if (!FeelRecordAnalysis.TryAnalyze(db, file, Path.GetFileName(recordPath!), options, cli.Has("skip-hash-check"),
                                                   out FeelRecordInfo i, out FeelResult r, out string analyzeError))
                {
                    error.WriteLine("feel: " + analyzeError);
                    return ExitCodes.InputError;
                }
                info = i;
                feel = r;
            }

            string json = FeelReportWriter.Build(fire, info, feel, options, !cli.Has("no-detail"));
            if (cli.TryGet("out", out string? outPath))
            {
                File.WriteAllText(outPath!, json);
            }
            else
            {
                output.Write(json);
            }
            FeelReportWriter.OverallOf(fire, feel, out string overall);
            WriteSummary(error, fire, feel, overall);
            return overall == "pass" ? ExitCodes.Pass : overall == "fail" ? ExitCodes.Fail : ExitCodes.Inconclusive;
        }

        private static int RunSelfCheck(ContentDb db, TextWriter output, TextWriter error)
        {
            IReadOnlyList<SelfCheckResult> results = FeelSelfCheck.RunAll(db);
            int failed = 0;
            foreach (SelfCheckResult r in results)
            {
                output.WriteLine((r.Passed ? "ok   " : "FAIL ") + r.Name + (r.Detail.Length > 0 ? "  " + r.Detail : string.Empty));
                if (!r.Passed)
                {
                    failed++;
                }
            }
            error.WriteLine("feel selfcheck: " + (results.Count - failed) + "/" + results.Count + " passed");
            return failed == 0 ? ExitCodes.Pass : ExitCodes.Fail;
        }

        private static void WriteSummary(TextWriter error, FireRatioReport? fire, FeelResult? feel, string overall)
        {
            if (fire != null)
            {
                error.WriteLine("화력 비(임계 구간 ÷ 꾹 누르기): " + fire.RatioPermille + "/1000, 목표 ≥ " + FeelTargets.FireRatioMinPermille
                                + " → " + VerdictNames.Of(fire.Verdict));
            }
            if (feel != null)
            {
                error.WriteLine("두 번 누르는 커맨드: " + feel.TwoPressSuccesses + "/" + feel.TwoPressAttempts + ", 목표 ≥ "
                                + FeelTargets.TwoPressMinPermille + "/1000 → " + VerdictNames.Of(FeelJudge.TwoPress(feel)));
                error.WriteLine("섬(연습 " + (feel.PracticeTicks / (60 * TicksPerSecond)) + "분 뒤): " + feel.JustSuccesses + "/" + feel.JustAttempts
                                + ", 목표 ≥ " + FeelTargets.JustMinPermille + "/1000 → " + VerdictNames.Of(FeelJudge.Just(feel)));
                error.WriteLine("자세 중 오발: " + feel.Misfires + "/" + feel.MisfireCasts + ", 목표 < " + FeelTargets.MisfireBelowPermille
                                + "/1000 → " + VerdictNames.Of(FeelJudge.Misfire(feel)));
            }
            error.WriteLine("종합: " + overall);
        }

        private static bool TryParseCore(CliArgs cli, out Wizard.Content.BlockId core, out string error)
        {
            core = Wizard.Content.BlockId.MagicBolt;
            error = string.Empty;
            if (!cli.TryGet("core", out string? name))
            {
                return true;
            }
            switch (name)
            {
                case "bolt":
                    return true;
                case "fireball":
                    core = Wizard.Content.BlockId.Fireball;
                    return true;
                default:
                    error = "--core must be bolt or fireball, got '" + name + "'";
                    return false;
            }
        }

        // 사람이 재는 방법과 기록의 조건(--help). 자동으로 쟀다고 적지 않는다: 이 값들은 사람이 직접 플레이한 기록에서만 뜻이 있다
        private const string Protocol = @"
사람이 재는 방법(GDD 16절 1단계, spec 03 22절). 기록 = 연습 세계에서 사람이 플레이한 .wzrc 한 개.
  1. 연습을 시작하고 자세(Shift)를 잡은 채 ""앞, 앞, K""(꿰뚫는 창)를 반복한다. 앞 = 시야 방향의 키다.
  2. 연습을 10분 이어 간다. 10분 뒤에는 ""앞, 앞"" 직후에 K 를 0.07초 안에 누르는 섬을 노려 계속 시도한다(시도 전부가 섬 시도로 센다).
  3. 마지막에 자세를 잡은 채 걸어 다니며 K 만 누른다. ""뒤, K""(반동 사격)는 일부러 쓰지 않는다: 그것이 나가면 자세 중 오발이다.
  4. 기록을 파일로 받아 `balancelab feel <기록.wzrc>` 를 돌린다. 판정 근거 JSON 의 시도 수, 성공 수, 간격 분포가 값의 근거다.
  - 두 번 누르는 커맨드 성공률 ≥ 90%, 연습 10분 뒤의 섬 성공률 ≥ 30%, 자세 중 오발 < 10%, 임계 구간 ÷ 꾹 누르기 화력 ≥ 1.4배(봇).
  - 기록이 이 코어·팩의 연습 세계에서 나온 것이 아니면(재실행 해시가 꼬리말과 다르면) 거절한다. 연습 기록을 내놓는 경로는 열린 질문이다.
  - 종료 코드: 0 통과, 1 미달, 3 자료 부족, 4 입력 오류, 64 사용법.
";

        private static int Usage(TextWriter error)
        {
            error.WriteLine("usage: balancelab feel <기록.wzrc> [--bot] [--practice-minutes N] [--attempt-window-ms N] [--skip-hash-check] [--no-detail]");
            error.WriteLine("       balancelab feel --bot [--seconds N] [--core bolt|fireball] [--split]");
            error.WriteLine("       balancelab feel --selfcheck");
            error.WriteLine("       balancelab feel --write-sample <파일>   각본으로 만든 시험용 기록을 쓴다(사람이 플레이한 기록이 아니다)");
            error.WriteLine("       공통: [--repo <저장소>] [--out <JSON 파일>]");
            return Program.UsageExitCode;
        }
    }
}
