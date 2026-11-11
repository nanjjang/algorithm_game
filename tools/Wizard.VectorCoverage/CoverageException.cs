using System;

namespace Wizard.VectorCoverage
{
    // 사용법·환경 오류(2)와 검사 실패(1)를 가른다. 메시지는 한 줄로 무엇이 어디서 틀렸는지 말한다.
    public sealed class CoverageException : Exception
    {
        public const int CheckFailedExitCode = 1;
        public const int UsageExitCode = 2;

        public CoverageException(string message, int exitCode)
            : base(message)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
