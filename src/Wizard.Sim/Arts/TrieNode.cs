using Wizard.Content;

namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5, spec 03 5.3. CommandTrie 의 마디 하나의 값(불변). 배열 칸(자식 4개, 펼친 전이 4개, 경로)은 값 타입에 담지 않고
    // CommandTrie 의 Child·Next·Path 로 읽는다(T-52). 해금 여부는 마디에 굽지 않고 판 동안의 UnlockSet 에서 읽는다(spec 03 5.3 [임시])
    public readonly struct TrieNode
    {
        public const long NoGap = -1;

        public readonly int Id;                 // 0 = 루트. BFS 순서(자식은 F, B, L, R 순)
        public readonly byte Depth;             // 0~4
        public readonly int Parent;             // 부모 마디(부모 대체용). 루트는 -1
        public readonly int Fail;               // 실패 링크. 루트는 0(자기 자신)
        public readonly ArtId KArt;             // K 기술. 없으면 None
        public readonly ArtId JustArt;          // 섬 기술. 없으면 None
        public readonly long InEdgeGapUnits;    // 들어오는 간선의 허용 간격(입력 시각 단위). 깊이 0~1 은 NoGap
        public readonly bool UniqueFallback;    // 부모 대체 여부(고유 비기 마디만)
        public readonly bool IsFullBloom;       // 만개 마디만

        public TrieNode(int id, byte depth, int parent, int fail, ArtId kArt, ArtId justArt, long inEdgeGapUnits,
                        bool uniqueFallback, bool isFullBloom)
        {
            Id = id;
            Depth = depth;
            Parent = parent;
            Fail = fail;
            KArt = kArt;
            JustArt = justArt;
            InEdgeGapUnits = inEdgeGapUnits;
            UniqueFallback = uniqueFallback;
            IsFullBloom = isFullBloom;
        }

        public bool HasInEdgeGap => InEdgeGapUnits >= 0;
    }
}
