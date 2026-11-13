namespace Wizard.ViewLogic.Assets
{
    // spec 14 4.6절 CompositeOrder 의 엔진 무관 판(그리기는 V-01 의 몫이다):
    //   base = [Body, ReactiveParts, WildStain, Cosmetics]
    //   dir ≠ Up                → base + [Wand]
    //   dir = Up, 나눔 예외 있음 → [지팡이 아래쪽(SplitY..끝, +OffsetX)] + base + [지팡이 위쪽(0..SplitY−1, +OffsetX)]
    //   dir = Up                → [Wand] + base
    // 나눔 예외(지팡이 id → SplitY, OffsetX)는 asset_aliases.json 의 wand_up_split 에 있다(wand.2: 손잡이 위 20px = y 28, x +6)
    public static class CharacterComposite
    {
        public static CompositeLayer[] Order(string wandId, SpriteFacing facing, AssetAliases aliases, int wandCellHeight)
        {
            if (facing != SpriteFacing.Up)
            {
                return new[]
                {
                    CompositeLayer.Whole(CompositeLayerKind.Body),
                    CompositeLayer.Whole(CompositeLayerKind.ReactiveParts),
                    CompositeLayer.Whole(CompositeLayerKind.WildStain),
                    CompositeLayer.Whole(CompositeLayerKind.Cosmetics),
                    CompositeLayer.Whole(CompositeLayerKind.Wand),
                };
            }
            if (aliases.TryWandUpSplit(wandId, out WandUpSplit split))
            {
                return new[]
                {
                    CompositeLayer.WandRows(split.SplitY, wandCellHeight - 1, split.OffsetX),
                    CompositeLayer.Whole(CompositeLayerKind.Body),
                    CompositeLayer.Whole(CompositeLayerKind.ReactiveParts),
                    CompositeLayer.Whole(CompositeLayerKind.WildStain),
                    CompositeLayer.Whole(CompositeLayerKind.Cosmetics),
                    CompositeLayer.WandRows(0, split.SplitY - 1, split.OffsetX),
                };
            }
            return new[]
            {
                CompositeLayer.Whole(CompositeLayerKind.Wand),
                CompositeLayer.Whole(CompositeLayerKind.Body),
                CompositeLayer.Whole(CompositeLayerKind.ReactiveParts),
                CompositeLayer.Whole(CompositeLayerKind.WildStain),
                CompositeLayer.Whole(CompositeLayerKind.Cosmetics),
            };
        }

        // AnchorMirror(dxRight): 왼쪽 = 오른쪽 그림을 발밑 기준점 둘레로 좌우 반전, 기준점에서의 x 차이 부호만 바꾼다 [임시]
        public static int AnchorMirror(int dxRight) => -dxRight;
    }
}
