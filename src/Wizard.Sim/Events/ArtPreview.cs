using Wizard.Content;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3, spec 03 11절. 벡터 B08·B13·B14·B15·B15b·B37
    public readonly struct ArtPreview
    {
        public readonly ArtId JustArt;
        public readonly int JustCost;                     // 섬 자리의 기술. 없으면 None
        public readonly ArtId NormalArt;
        public readonly int NormalCost;                   // 일반 간격의 기술. 둘 다 있으면 HUD 가 두 줄로 그린다(B15)
        public readonly int CreditCount;                  // 외상으로 낼 결정 수(제자 5, B13). 0이면 없음
        public readonly int FizzleInputIndex;             // 불발이면 몇 번째 입력이 늦었는가(1부터). 0이면 불발 아님(B37)
        public readonly int FizzleLateByUnits;            // 늦은 양(입력 시각 단위)
    }
}
