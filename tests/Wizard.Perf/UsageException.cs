using System;

namespace Wizard.Perf
{
    // 명령줄이 틀렸다(끝 코드 2)
    internal sealed class UsageException : Exception
    {
        public UsageException(string message)
            : base(message)
        {
        }
    }
}
