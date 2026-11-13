namespace Wizard.ViewLogic.Assets
{
    // 합성 순서의 한 층. 지팡이를 둘로 나눠 그리는 예외(4.6절)에서는 지팡이 칸의 행 범위 [RowFirst, RowLast] 와 x 이동이 붙는다.
    // 나누지 않은 층은 IsWholeSprite 이고 OffsetX 는 0 이다
    public readonly struct CompositeLayer
    {
        public readonly CompositeLayerKind Kind;
        public readonly bool IsWholeSprite;
        public readonly int RowFirst;
        public readonly int RowLast;
        public readonly int OffsetX;

        private CompositeLayer(CompositeLayerKind kind, bool whole, int rowFirst, int rowLast, int offsetX)
        {
            Kind = kind;
            IsWholeSprite = whole;
            RowFirst = rowFirst;
            RowLast = rowLast;
            OffsetX = offsetX;
        }

        public static CompositeLayer Whole(CompositeLayerKind kind) => new CompositeLayer(kind, true, 0, 0, 0);

        public static CompositeLayer WandRows(int rowFirst, int rowLast, int offsetX) =>
            new CompositeLayer(CompositeLayerKind.Wand, false, rowFirst, rowLast, offsetX);
    }
}
