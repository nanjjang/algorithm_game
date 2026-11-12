using Wizard.Content;

namespace Wizard.ViewLogic.Convert
{
    // 섬 간격 보고 하나(spec 03 21절 끝 "섬 간격을 밀리초로 표시", GDD 16 1단계). PracticeGapReport 이벤트에서 옮긴다:
    //   A·V = 마지막 방향 입력에서 K 까지(입력 시각 단위, 없으면 −1), B = 판정한 기술(없으면 0), C = 판정함(0/1).
    // 같은 틱 같은 슬롯의 ArtCast 가 오면 섬 발동 여부(ArtCast.C)를 붙인다.
    public readonly struct GapReport
    {
        public readonly uint Tick;
        public readonly bool HasGap;              // 자세 밖이거나 방향 입력이 없었으면 거짓
        public readonly long GapUnits;
        public readonly ArtId Art;
        public readonly bool Decided;
        public readonly bool Just;

        public GapReport(uint tick, bool hasGap, long gapUnits, ArtId art, bool decided, bool just)
        {
            Tick = tick;
            HasGap = hasGap;
            GapUnits = gapUnits;
            Art = art;
            Decided = decided;
            Just = just;
        }

        public double Milliseconds => HasGap ? DisplayUnits.InputUnitsToMilliseconds(GapUnits) : 0;

        public long RoundedMilliseconds => HasGap ? DisplayUnits.RoundMilliseconds(Milliseconds) : 0;

        public GapReport WithJust(bool just) => new GapReport(Tick, HasGap, GapUnits, Art, Decided, just);
    }
}
