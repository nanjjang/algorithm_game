using System;

namespace Wizard.Ci
{
    // 사용법·환경 오류. 메시지는 무엇이 어디서 틀렸는지와 할 일을 한 줄로 말한다.
    public sealed class CiException : Exception
    {
        public const int UsageExitCode = 2;

        public CiException(string message, int exitCode = UsageExitCode)
            : base(message)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
