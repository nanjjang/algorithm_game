using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Wizard.Ci
{
    // 단계 하나의 결과(artifacts/ci/<단계>.json)
    public sealed class StageResult
    {
        public StageResult(string stage, DateTime startedUtc, DateTime finishedUtc, IReadOnlyList<StepResult> steps)
        {
            Stage = stage;
            StartedUtc = startedUtc;
            FinishedUtc = finishedUtc;
            Steps = steps;
        }

        public string Stage { get; }
        public DateTime StartedUtc { get; }
        public DateTime FinishedUtc { get; }
        public IReadOnlyList<StepResult> Steps { get; }

        public int PassedCount => Count(s => s.Passed && !s.Skipped);
        public int FailedCount => Count(s => !s.Passed);
        public int SkippedCount => Count(s => s.Skipped);
        public bool Passed => FailedCount == 0;

        private int Count(Func<StepResult, bool> f)
        {
            int n = 0;
            foreach (StepResult s in Steps)
            {
                if (f(s))
                {
                    n++;
                }
            }
            return n;
        }

        public static string Time(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        public void Write(Utf8JsonWriter w, string root, string? branch, string? commit, bool forceAll)
        {
            w.WriteStartObject();
            WriteBody(w, root, branch, commit, forceAll);
            w.WriteEndObject();
        }

        // 게이트 파일에도 같은 몸통을 쓴다
        // writeRoot: 커밋되는 게이트 파일에는 기기의 절대 경로를 쓰지 않는다
        public void WriteBody(Utf8JsonWriter w, string root, string? branch, string? commit, bool forceAll, bool writeRoot = true)
        {
            w.WriteString("stage", Stage);
            w.WriteBoolean("passed", Passed);
            if (writeRoot)
            {
                w.WriteString("root", root);
            }
            if (branch != null)
            {
                w.WriteString("branch", branch);
            }
            if (commit != null)
            {
                w.WriteString("commit", commit);
            }
            w.WriteBoolean("force_all", forceAll);
            w.WriteString("started_at", Time(StartedUtc));
            w.WriteString("finished_at", Time(FinishedUtc));
            w.WriteNumber("seconds", Math.Round((FinishedUtc - StartedUtc).TotalSeconds, 1));
            w.WriteNumber("passed_steps", PassedCount);
            w.WriteNumber("failed_steps", FailedCount);
            w.WriteNumber("skipped_steps", SkippedCount);
            w.WriteStartArray("steps");
            foreach (StepResult s in Steps)
            {
                s.Write(w, root, false);
            }
            w.WriteEndArray();
        }
    }
}
