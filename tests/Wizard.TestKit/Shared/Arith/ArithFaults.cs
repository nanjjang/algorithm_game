using System;

namespace Wizard.TestKit.Shared.Arith
{
    // 예외 분류의 이름. 0 제수는 코드가 검사해 SimInvariantException 을 던진다(CONTRACTS 1.1).
    // 런타임의 DivideByZeroException·OverflowException 이 나오면 "Exception:<형식>" 으로 기록되고, 기대와 다르므로 실패한다.
    public static class ArithFaults
    {
        public const string SimInvariant = "SimInvariant";
        public const string UnknownOp = "UnknownOp";
        public const string ExceptionPrefix = "Exception:";

        // 벡터 파일의 "fault" 에 적을 수 있는 이름(기대할 수 있는 분류)
        public static bool IsExpectable(string name) => string.Equals(name, SimInvariant, StringComparison.Ordinal);

        public static string Classify(Exception ex)
        {
            if (ex is Wizard.Core.SimInvariantException)
            {
                return SimInvariant;
            }
            return ExceptionPrefix + ex.GetType().FullName;
        }
    }
}
