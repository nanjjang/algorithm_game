namespace Wizard.ViewLogic.Assets
{
    // 9조각 모서리(px). MAN-ui nine_slice_ltrb = [왼, 위, 오른, 아래] (spec 14 4.4절)
    public readonly struct NineSlice
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public NineSlice(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }
}
