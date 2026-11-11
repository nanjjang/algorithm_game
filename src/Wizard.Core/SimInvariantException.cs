using System;

namespace Wizard.Core
{
    // CONTRACTS 0.2: 상태가 깨졌을 때, 제수 0, Step 의 전제 위반에만 던진다. 게임 규칙상의 "안 됨"은 예외가 아니다.
    // 런타임의 0 나누기 검사에 맡기지 않고 코드가 직접 검사해 이 예외를 던진다(CONTRACTS 1.1).
    public sealed class SimInvariantException : Exception
    {
        public SimInvariantException(string message) : base(message)
        {
        }

        internal static SimInvariantException DivideByZero() => new SimInvariantException("divide by zero");
    }
}
