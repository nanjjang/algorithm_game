using System;

namespace Wizard.TaskGraph
{
    // 도구가 일을 할 수 없을 때(사용법, 없는 작업, 허용되지 않는 상태 전이, git 실패) 던진다.
    // Cli 가 받아 한 줄로 알리고 ExitCode 로 끝난다. 검사의 "위반"은 예외가 아니라 결과 목록이다.
    public sealed class TaskGraphException : Exception
    {
        public const int UsageExitCode = 2;
        public const int FailureExitCode = 1;

        public TaskGraphException(string message, int exitCode = FailureExitCode)
            : base(message)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
