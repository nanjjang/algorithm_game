using System.Globalization;
using System.Text;

namespace Wizard.TestKit.Shared.Fuzz
{
    // 퍼저 실행의 결과. 실패면 재현용 씨앗(주 씨앗, 사례 번호, 사례 씨앗)과 원래 입력, 줄인 입력을 담는다.
    public sealed class FuzzResult<T>
    {
        internal FuzzResult(ulong seed, int casesRun, int casesPlanned)
        {
            Seed = seed;
            CasesRun = casesRun;
            CasesPlanned = casesPlanned;
            Passed = true;
            Report = "fuzz: " + casesRun.ToString(CultureInfo.InvariantCulture) + " cases passed (seed " + Hex(seed) + ")";
        }

        internal FuzzResult(ulong seed, int casesRun, int casesPlanned, int failingCase, ulong caseSeed, T original, string originalText,
                            T minimized, string minimizedText, string message, int shrinkSteps)
        {
            Seed = seed;
            CasesRun = casesRun;
            CasesPlanned = casesPlanned;
            Passed = false;
            FailingCase = failingCase;
            FailingCaseSeed = caseSeed;
            Original = original;
            Minimized = minimized;
            Message = message;
            ShrinkSteps = shrinkSteps;
            var sb = new StringBuilder();
            sb.Append("fuzz: property violated\n");
            sb.Append("  seed:            ").Append(Hex(seed)).Append(" (case ").Append(failingCase.ToString(CultureInfo.InvariantCulture))
              .Append(" of ").Append(casesPlanned.ToString(CultureInfo.InvariantCulture)).Append(")\n");
            sb.Append("  case seed:       ").Append(Hex(caseSeed)).Append('\n');
            sb.Append("  replay:          FuzzOptions.Replay(").Append(Hex(caseSeed)).Append("UL)\n");
            sb.Append("  message:         ").Append(message).Append('\n');
            sb.Append("  original input:  ").Append(originalText).Append('\n');
            sb.Append("  minimized input: ").Append(minimizedText).Append(" (shrink steps: ")
              .Append(shrinkSteps.ToString(CultureInfo.InvariantCulture)).Append(")\n");
            Report = sb.ToString();
        }

        public bool Passed { get; }

        public ulong Seed { get; }

        public int CasesRun { get; }

        public int CasesPlanned { get; }

        // 실패한 사례의 번호(0부터). 통과면 -1
        public int FailingCase { get; } = -1;

        // 실패한 사례의 씨앗. FuzzOptions.Replay 에 넘기면 같은 입력이 다시 만들어진다
        public ulong FailingCaseSeed { get; }

        public T Original { get; } = default!;

        public T Minimized { get; } = default!;

        // 줄인 입력에 대한 성질 검사의 이유
        public string? Message { get; }

        public int ShrinkSteps { get; }

        public string Report { get; }

        private static string Hex(ulong v) => "0x" + v.ToString("x16", CultureInfo.InvariantCulture);
    }
}
