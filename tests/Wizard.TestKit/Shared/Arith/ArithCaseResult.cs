using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Arith
{
    // 한 런타임·아키텍처 조합에서 벡터 하나를 돌린 결과. 결과 파일(GoldenRunResult)에 그대로 실린다.
    public sealed class ArithCaseResult
    {
        public ArithCaseResult(string bundle, string caseId, IReadOnlyList<ArithValue>? values, string? fault)
        {
            Bundle = bundle;
            CaseId = caseId;
            Values = values;
            Fault = fault;
        }

        public string Bundle { get; }

        public string CaseId { get; }

        // 정상 끝: 결과 값들. 예외로 끝났으면 null
        public IReadOnlyList<ArithValue>? Values { get; }

        // 예외로 끝났으면 분류(ArithFaults), 아니면 null
        public string? Fault { get; }

        public string Key => Bundle + "/" + CaseId;

        public string Describe()
        {
            if (Fault != null)
            {
                return "fault " + Fault;
            }
            var parts = new List<string>();
            foreach (ArithValue v in Values!)
            {
                parts.Add(v.Text);
            }
            return "[" + string.Join(", ", parts) + "]";
        }

        // 같은 결과인가(값은 64비트 패턴으로 비교)
        public bool SameOutcome(ArithCaseResult other)
        {
            if (Fault != null || other.Fault != null)
            {
                return string.Equals(Fault, other.Fault, System.StringComparison.Ordinal);
            }
            return SameValues(Values!, other.Values!);
        }

        // 기대(ArithCase)를 만족하는가
        public bool Satisfies(ArithCase expected)
        {
            if (expected.Fault != null)
            {
                return string.Equals(Fault, expected.Fault, System.StringComparison.Ordinal);
            }
            return Fault == null && SameValues(Values!, expected.Expect!);
        }

        private static bool SameValues(IReadOnlyList<ArithValue> a, IReadOnlyList<ArithValue> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
