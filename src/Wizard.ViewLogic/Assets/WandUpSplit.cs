namespace Wizard.ViewLogic.Assets
{
    // 위 방향 겹침 예외(spec 14 4.6절의 wand.2 위 방향 예외, 지팡이 문서 2.2): 지팡이 그림을 SplitY 줄에서 둘로 나눠
    // 아래쪽(SplitY..끝)은 몸 아래, 위쪽(0..SplitY−1)은 몸 위에 그리고, 둘 다 x 로 OffsetX 만큼 옮긴다.
    // 값은 asset_aliases.json 의 wand_up_split(지팡이 id 마다)
    public readonly struct WandUpSplit
    {
        public readonly int SplitY;
        public readonly int OffsetX;

        public WandUpSplit(int splitY, int offsetX)
        {
            SplitY = splitY;
            OffsetX = offsetX;
        }
    }
}
