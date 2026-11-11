using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Golden
{
    // 한 조합에서 골든 항목 하나를 돌린 결과: 낸 해시 목록, 또는 끝까지 돌지 못한 이유.
    public sealed class GoldenEntryResult
    {
        public GoldenEntryResult(string id, IReadOnlyList<GoldenHash> hashes, string? error)
        {
            Id = id;
            Hashes = hashes;
            Error = error;
        }

        public string Id { get; }

        public IReadOnlyList<GoldenHash> Hashes { get; }

        // 예외나 재실행 실패(RecordCorrupt, SimFault …)의 설명. 정상이면 null
        public string? Error { get; }
    }
}
