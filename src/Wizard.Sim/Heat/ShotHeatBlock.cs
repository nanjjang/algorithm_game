namespace Wizard.Sim.Heat
{
    // ShotHeat.ShotHeatHu 의 입력 한 칸(spec 01 7.2, spec 02 7.1). 실행 트리의 블록 하나.
    // 목록은 슬롯 번호 오름차순으로 준다(같은 스코프의 "첫 번째 분열"을 그 순서로 가린다)
    public readonly struct ShotHeatBlock
    {
        public readonly int Scope;               // 스코프 번호(트리거 스코프 포함). 0 이상
        public readonly int Slot;                // 슬롯 번호(0 = 코어, 1~L = 연결 슬롯)
        public readonly int HeatHu;              // 블록 열(BlockDef.HeatHu). 분열과 적중 시 발동은 0
        public readonly int SplitMulPermille;    // 분열만 1600 / 2200(BlockDef.HeatMulPermille). 분열이 아니면 0

        public ShotHeatBlock(int scope, int slot, int heatHu, int splitMulPermille)
        {
            Scope = scope;
            Slot = slot;
            HeatHu = heatHu;
            SplitMulPermille = splitMulPermille;
        }
    }
}
