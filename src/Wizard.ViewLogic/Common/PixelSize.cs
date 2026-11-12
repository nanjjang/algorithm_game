using System.Globalization;

namespace Wizard.ViewLogic.Common
{
    // 화면 그림 크기(정수 px)
    public readonly struct PixelSize
    {
        public readonly int Width;
        public readonly int Height;

        public PixelSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString() =>
            Width.ToString(CultureInfo.InvariantCulture) + "x" + Height.ToString(CultureInfo.InvariantCulture);
    }
}
