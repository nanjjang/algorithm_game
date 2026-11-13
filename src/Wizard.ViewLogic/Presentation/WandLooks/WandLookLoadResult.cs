using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 읽기 결과. Problems 가 비어 있지 않으면 자료가 틀렸거나 등록 검사에서 거부된 덧씌움이 있다(거부된 덧씌움은 표에 없다)
    public sealed class WandLookLoadResult
    {
        public WandLookLoadResult(WandLookTable table, IReadOnlyList<string> problems)
        {
            Table = table;
            Problems = problems;
        }

        public WandLookTable Table { get; }

        public IReadOnlyList<string> Problems { get; }

        public bool Ok => Problems.Count == 0;
    }
}
