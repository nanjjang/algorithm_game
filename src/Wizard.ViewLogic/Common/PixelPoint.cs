using System.Globalization;

namespace Wizard.ViewLogic.Common
{
    // 화면 그림 좌표(정수 px). 화면 그림은 정수 px 에만 놓인다(spec 14 4.3 글꼴 "글자 위치는 정수 px")
    public readonly struct PixelPoint
    {
        public readonly int X;
        public readonly int Y;

        public PixelPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() =>
            "(" + X.ToString(CultureInfo.InvariantCulture) + ", " + Y.ToString(CultureInfo.InvariantCulture) + ")";
    }
}
