using System;
using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Golden
{
    // 골든 파일(항목, 기대 해시, 결과 파일, 산술 묶음)의 형식이 틀렸다. 찾은 문제를 전부 담는다.
    public sealed class GoldenFormatException : Exception
    {
        public GoldenFormatException(IReadOnlyList<string> problems)
            : base("golden format problems:\n  " + string.Join("\n  ", problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }

        internal static void ThrowIfAny(List<string> problems)
        {
            if (problems.Count > 0)
            {
                throw new GoldenFormatException(problems.ToArray());
            }
        }
    }
}
