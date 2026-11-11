using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Arith
{
    // 산술 경계 벡터 하나: 연산 이름, 인자, 그리고 기대 결과 값들 또는 기대 예외 분류 가운데 하나.
    public sealed class ArithCase
    {
        public ArithCase(string id, string op, IReadOnlyList<ArithValue> args, IReadOnlyList<ArithValue>? expect, string? fault)
        {
            Id = id;
            Op = op;
            Args = args;
            Expect = expect;
            Fault = fault;
        }

        public string Id { get; }

        // 예: "FixMath.DivInt". 이름과 뜻은 연산 표(IArithOps)를 가진 작업이 정한다(M1-CORE-01)
        public string Op { get; }

        public IReadOnlyList<ArithValue> Args { get; }

        // 기대 결과(여러 출력이면 순서대로). Fault 가 있으면 null
        public IReadOnlyList<ArithValue>? Expect { get; }

        // 기대 예외 분류(ArithFaults). Expect 가 있으면 null
        public string? Fault { get; }
    }
}
