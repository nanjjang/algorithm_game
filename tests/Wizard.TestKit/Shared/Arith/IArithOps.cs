using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Arith
{
    // 연산 이름을 실제 코어 함수에 잇는 표. 산술 묶음의 주인(M1-CORE-01)이 만든다.
    // 모르는 연산이면 false. 결과는 results 에 순서대로 더한다. 예외는 밖으로 던져도 된다(ArithRunner 가 분류한다).
    public interface IArithOps
    {
        bool TryEvaluate(string op, IReadOnlyList<ArithValue> args, List<ArithValue> results);
    }
}
