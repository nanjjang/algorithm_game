using System.Collections.Generic;

namespace Wizard.ViewLogic.Assets
{
    // 매니페스트 다섯 개를 읽은 결과. 문제가 있어도 읽을 수 있는 기록은 레지스트리에 들어 있고(게임은 멈추지 않는다),
    // 문제 목록이 비어 있어야 반입 검사(M9-AST-02)와 시험이 통과한다
    public sealed class AssetLoadResult
    {
        public AssetLoadResult(AssetRegistry registry, AssetAliases aliases, IReadOnlyList<AssetLoadProblem> problems)
        {
            Registry = registry;
            Aliases = aliases;
            Problems = problems;
        }

        public AssetRegistry Registry { get; }

        public AssetAliases Aliases { get; }

        public IReadOnlyList<AssetLoadProblem> Problems { get; }

        public bool Ok => Problems.Count == 0;
    }
}
