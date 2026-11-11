using System.Collections.Generic;
using System.Text.Json;

namespace Wizard.Ci
{
    // 검사 하나의 결과. artifacts/ci/<단계>/<이름>.json 으로 남고 단계 요약(artifacts/ci/<단계>.json)에도 들어간다.
    // 건너뜀(Skipped)은 검사 스크립트가 WIZARD_CI_NOTE 파일에 "skipped: <이유>" 를 적고 0 으로 끝난 경우뿐이다
    // (예: 바뀐 경로가 Unity 단계에 닿지 않음. PIPELINE 3.8). 필요한 것이 없어서 못 도는 검사는 건너뛰지 않고 실패한다.
    public sealed class StepResult
    {
        public StepResult(StepScript script, int exitCode, bool timedOut, double seconds, bool skipped, string? skipReason,
                          IReadOnlyList<string> notes, string logPath, IReadOnlyList<string> tail)
        {
            Script = script;
            ExitCode = exitCode;
            TimedOut = timedOut;
            Seconds = seconds;
            Skipped = skipped;
            SkipReason = skipReason;
            Notes = notes;
            LogPath = logPath;
            Tail = tail;
        }

        public StepScript Script { get; }
        public int ExitCode { get; }
        public bool TimedOut { get; }
        public double Seconds { get; }
        public bool Skipped { get; }
        public string? SkipReason { get; }
        public IReadOnlyList<string> Notes { get; }
        public string LogPath { get; }
        public IReadOnlyList<string> Tail { get; }
        public bool Passed => ExitCode == 0;

        public void Write(Utf8JsonWriter w, string root, bool withTail)
        {
            w.WriteStartObject();
            w.WriteString("name", Script.Name);
            w.WriteString("script", Script.RelativePath);
            w.WriteString("stage", Script.Stage);
            w.WriteBoolean("passed", Passed);
            w.WriteNumber("exit_code", ExitCode);
            w.WriteBoolean("timed_out", TimedOut);
            w.WriteBoolean("skipped", Skipped);
            if (SkipReason != null)
            {
                w.WriteString("skip_reason", SkipReason);
            }
            w.WriteNumber("seconds", System.Math.Round(Seconds, 1));
            w.WriteStartArray("notes");
            foreach (string n in Notes)
            {
                w.WriteStringValue(n);
            }
            w.WriteEndArray();
            w.WriteString("log", RepoPaths.Relative(root, LogPath));
            if (withTail && !Passed)
            {
                w.WriteStartArray("tail");
                foreach (string t in Tail)
                {
                    w.WriteStringValue(t);
                }
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }
    }
}
