using System;
using System.Collections.Generic;
using System.IO;

namespace Wizard.Ci
{
    // ProcessRunner.Run 의 입력
    public sealed class ProcRequest
    {
        public ProcRequest(string file, IEnumerable<string> args)
        {
            File = file;
            Args = new List<string>(args);
        }

        public string File { get; }
        public IReadOnlyList<string> Args { get; }
        public string Name { get; set; } = "step";
        public string? WorkingDirectory { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

        // 이 값으로 덮는다. 값이 null 이면 그 변수를 지운다
        public Dictionary<string, string?> Environment { get; } = new Dictionary<string, string?>(StringComparer.Ordinal);

        public TextWriter? Echo { get; set; }
        public TextWriter? EchoError { get; set; }
        public string? LogPath { get; set; }
        public bool Capture { get; set; }

        // 실패를 알릴 때 붙이는 마지막 줄 수. 0 이면 모으지 않는다
        public int TailLines { get; set; }
    }
}
