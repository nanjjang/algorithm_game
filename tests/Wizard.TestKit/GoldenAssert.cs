using System.Collections.Generic;
using Wizard.TestKit.Shared.Golden;
using Xunit.Sdk;

namespace Wizard.TestKit
{
    // 골든 비교의 xUnit 연결. 처음 갈라진 틱을 메시지에 담는다(어느 상태 절인지는 ReplayCli 가 찾는다, PIPELINE 7.3).
    public static class GoldenAssert
    {
        public static void Matches(GoldenEntry entry, IReadOnlyList<GoldenHash> actual)
        {
            GoldenDiff? d = entry.Compare(actual);
            if (d != null)
            {
                throw new XunitException("golden " + entry.Id + ": " + d +
                    "\n(if the change is meant to alter the hash, regenerate with `replaycli golden --update` and add a line to tests/golden/CHANGELOG.md)");
            }
        }

        public static void Passed(GoldenComparison comparison)
        {
            if (!comparison.Passed)
            {
                throw new XunitException("golden comparison failed:\n  " + string.Join("\n  ", comparison.Failures) + "\n\n" + comparison.Table);
            }
        }
    }
}
