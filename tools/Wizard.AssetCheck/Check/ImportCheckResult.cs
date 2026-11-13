using System.Collections.Generic;
using System.Linq;

namespace Wizard.AssetCheck.Check
{
    public sealed class ImportCheckResult
    {
        public ImportCheckResult(AssetCheckInput input, IReadOnlyList<Finding> findings, IReadOnlyList<MissingSlot> missing)
        {
            Input = input;
            Findings = findings;
            Missing = missing;
        }

        public AssetCheckInput Input { get; }

        // 파일, 검사, 자리, 문구의 서수 순
        public IReadOnlyList<Finding> Findings { get; }

        // 그림이 없는 자리(자리 id 서수 순)
        public IReadOnlyList<MissingSlot> Missing { get; }

        public IEnumerable<Finding> Errors => Findings.Where(f => f.IsError);

        public bool Ok => !Findings.Any(f => f.IsError);
    }
}
