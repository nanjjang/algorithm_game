using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Wizard.Ci
{
    // 단계 실행(PIPELINE 4.6). fast → full → integration 순서로, 요청한 단계까지의 검사를 전부 돈다(앞 검사가 실패해도 나머지를 돈다).
    // 검사 하나는 <bash> <스크립트> 이고 시간 제한 안에서 돈다(bash 는 BashLocator 가 찾은 것. T-67 3번). 결과:
    //   artifacts/ci/<단계>/<이름>.json   검사 하나
    //   artifacts/ci/<단계>/<이름>.log    그 검사의 출력
    //   artifacts/ci/<단계>.json          단계 요약
    // 검사 스크립트가 받는 환경 변수:
    //   WIZARD_BASH          이 실행기가 스크립트를 띄운 bash(T-67 3번)
    //   경로가 든 값(스크립트 경로, WIZARD_ROOT, WIZARD_CI_NOTE, WIZARD_CI_DLL, WIZARD_CI_TRX_DIR)은 '/' 구분으로 넘긴다(Windows 에서 D:/… 꼴. T-67 4번)
    //   WIZARD_ROOT          저장소 뿌리(이 체크아웃)
    //   WIZARD_CI_STAGE      지금 단계(fast|full|integration|run)
    //   WIZARD_CI_FORCE_ALL  1 이면 바뀐 경로로 줄이지 않는다(게이트, --all)
    //   WIZARD_CI_NOTE       검사가 남길 말을 적는 파일. "skipped: <이유>" 줄은 건너뜀으로 기록된다
    //   WIZARD_CI_DLL        이 도구(Wizard.Ci.dll). 검사 스크립트가 changed, affected-tests, unity-scope, require 를 부른다
    //   WIZARD_CI_TRX_DIR    이 실행에서 dotnet test 의 TRX 결과를 모으는 폴더(실행을 시작할 때 비운다)
    //   WIZARD_CI_BASE       비교 기준(명령 줄 층이 --base 또는 WIZARD_CI_BASE 에서 정한 것. 없으면 지운다 → main 과의 갈림점)
    // 이 클래스는 프로세스 환경을 읽지 않는다. 기본 시간 제한(CI_STEP_TIMEOUT)과 비교 기준은 명령 줄 층(Program.Run)이 넘긴다(FIX-4).
    public sealed class StageRunner
    {
        public const int DefaultStepTimeoutSeconds = 1800;
        private const int TailLines = 40;

        private readonly string _root;
        private readonly TextWriter _out;
        private readonly bool _forceAll;
        private readonly string? _baseRef;
        private readonly int _defaultTimeout;
        private readonly string _bash;

        // bash: 검사 스크립트를 띄울 셸(BashLocator 가 찾은 것. 명령 줄 층이 넘긴다)
        public StageRunner(string root, TextWriter output, bool forceAll, string? baseRef, int defaultStepTimeoutSeconds, string bash)
        {
            if (defaultStepTimeoutSeconds <= 0)
            {
                throw new CiException("검사의 기본 시간 제한은 양의 정수(초)다: " + defaultStepTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
            }
            _root = root;
            _out = output;
            _forceAll = forceAll;
            _baseRef = baseRef;
            _defaultTimeout = defaultStepTimeoutSeconds;
            _bash = bash;
        }

        public string CiDir => Path.Combine(_root, "artifacts", "ci");

        // 요청한 단계까지 돈다. 단계마다 요약을 쓰고 단계별 결과를 돌려준다.
        public List<StageResult> RunUpTo(string stage)
        {
            int want = StepScript.StageRank(stage);
            if (want == 0)
            {
                throw new CiException("알 수 없는 단계 '" + stage + "' (fast|full|integration)");
            }
            List<StepScript> all = StepScript.Collect(_root);
            ResetTrx();
            var results = new List<StageResult>();
            foreach (string s in StepScript.Stages)
            {
                if (StepScript.StageRank(s) > want)
                {
                    break;
                }
                var steps = all.FindAll(x => x.Stage == s);
                results.Add(RunStage(s, steps));
            }
            return results;
        }

        // 검사 하나(ci.sh run). 결과는 artifacts/ci/run/<이름>.json, 요약은 artifacts/ci/run.json
        public StageResult RunSingle(string name)
        {
            StepScript found = StepScript.Find(_root, name);
            var asRun = new StepScript(found.Path, found.RelativePath, "run", found.TimeoutSeconds);
            ResetTrx();
            return RunStage("run", new List<StepScript> { asRun });
        }

        private void ResetTrx()
        {
            string dir = TrxDir;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
            Directory.CreateDirectory(dir);
        }

        public string TrxDir => Path.Combine(CiDir, "tests");

        private StageResult RunStage(string stage, List<StepScript> steps)
        {
            DateTime started = DateTime.UtcNow;
            var results = new List<StepResult>();
            if (steps.Count == 0)
            {
                _out.WriteLine("==> [" + stage + "] 검사 없음(tools/ci/ 에 '# ci-stage: " + stage + "' 스크립트가 없다)");
            }
            foreach (StepScript step in steps)
            {
                results.Add(RunStep(stage, step));
            }
            var sr = new StageResult(stage, started, DateTime.UtcNow, results);
            WriteSummary(sr);
            _out.WriteLine("ci " + stage + ": " + sr.PassedCount + " passed, " + sr.FailedCount + " failed, " + sr.SkippedCount +
                           " skipped → " + RepoPaths.Relative(_root, SummaryPath(stage)));
            return sr;
        }

        public StepResult RunStep(string stage, StepScript step)
        {
            string dir = Path.Combine(CiDir, stage);
            Directory.CreateDirectory(dir);
            string note = Path.Combine(dir, step.Name + ".note");
            string log = Path.Combine(dir, step.Name + ".log");
            string json = Path.Combine(dir, step.Name + ".json");
            File.Delete(note);
            int timeout = step.TimeoutSeconds ?? _defaultTimeout;
            _out.WriteLine("==> [" + stage + "] " + step.Name + " (" + step.RelativePath + ", 시간 제한 " + timeout.ToString(CultureInfo.InvariantCulture) + "초)");
            _out.Flush();
            var req = new ProcRequest(_bash, new[] { BashLocator.SlashPath(step.Path) })
            {
                Name = step.Name,
                WorkingDirectory = _root,
                Timeout = TimeSpan.FromSeconds(timeout),
                Echo = _out,
                EchoError = _out,
                LogPath = log,
                TailLines = TailLines,
            };
            req.Environment["WIZARD_BASH"] = _bash;
            req.Environment["WIZARD_ROOT"] = BashLocator.SlashPath(_root);
            req.Environment["WIZARD_CI_STAGE"] = stage;
            req.Environment["WIZARD_CI_FORCE_ALL"] = _forceAll ? "1" : "0";
            req.Environment["WIZARD_CI_NOTE"] = BashLocator.SlashPath(note);
            req.Environment["WIZARD_CI_DLL"] = BashLocator.SlashPath(typeof(StageRunner).Assembly.Location);
            req.Environment["WIZARD_CI_TRX_DIR"] = BashLocator.SlashPath(TrxDir);
            req.Environment["WIZARD_CI_BASE"] = _baseRef;
            ProcResult r = ProcessRunner.Run(req);

            var notes = new List<string>();
            string? skip = null;
            if (File.Exists(note))
            {
                foreach (string line in File.ReadAllLines(note, Encoding.UTF8))
                {
                    string l = line.Trim();
                    if (l.Length == 0)
                    {
                        continue;
                    }
                    if (l.StartsWith("skipped:", StringComparison.Ordinal))
                    {
                        skip = l.Substring("skipped:".Length).Trim();
                    }
                    else
                    {
                        notes.Add(l);
                    }
                }
                File.Delete(note);
            }
            bool skipped = skip != null && r.ExitCode == 0;
            if (skip != null && r.ExitCode != 0)
            {
                notes.Add("skipped 를 적었지만 종료 코드가 0 이 아니어서 실패로 친다: " + skip);
                skip = null;
            }
            var result = new StepResult(step, r.ExitCode, r.TimedOut, r.Seconds, skipped, skip, notes, log, r.Tail);
            JsonFile.Write(json, w => result.Write(w, _root, true));
            string secs = r.Seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
            if (skipped)
            {
                _out.WriteLine("    SKIP  " + step.Name + " (" + skip + ")");
            }
            else if (result.Passed)
            {
                _out.WriteLine("    PASS  " + step.Name + " (" + secs + ")");
            }
            else
            {
                _out.WriteLine("    FAIL  " + step.Name + " (exit " + r.ExitCode.ToString(CultureInfo.InvariantCulture) + (r.TimedOut ? ", 시간 제한" : "") +
                               ", " + secs + ", 출력 " + RepoPaths.Relative(_root, log) + ")");
            }
            foreach (string n in notes)
            {
                _out.WriteLine("    note  " + n);
            }
            _out.Flush();
            return result;
        }

        public string SummaryPath(string stage) => Path.Combine(CiDir, stage + ".json");

        private void WriteSummary(StageResult sr)
        {
            string? branch = Git.IsRepo(_root) ? Git.CurrentBranch(_root) : null;
            string? commit = Git.IsRepo(_root) ? Git.RevParse(_root, "HEAD") : null;
            JsonFile.Write(SummaryPath(sr.Stage), w => sr.Write(w, _root, branch, commit, _forceAll));
        }
    }
}
