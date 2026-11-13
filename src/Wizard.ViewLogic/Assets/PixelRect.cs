using System;
using System.Globalization;

namespace Wizard.ViewLogic.Assets
{
    // 텍스처 안의 사각형(정수 px, 왼쪽 위 원점). 매니페스트의 rect [x, y, w, h] 와 같은 꼴
    public readonly struct PixelRect : IEquatable<PixelRect>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public PixelRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Right => X + Width;

        public int Bottom => Y + Height;

        public bool Equals(PixelRect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) => obj is PixelRect r && Equals(r);

        public override int GetHashCode() => ((X * 31 + Y) * 31 + Width) * 31 + Height;

        public static bool operator ==(PixelRect a, PixelRect b) => a.Equals(b);

        public static bool operator !=(PixelRect a, PixelRect b) => !a.Equals(b);

        public override string ToString() =>
            "(" + X.ToString(CultureInfo.InvariantCulture) + ", " + Y.ToString(CultureInfo.InvariantCulture) + ", "
            + Width.ToString(CultureInfo.InvariantCulture) + ", " + Height.ToString(CultureInfo.InvariantCulture) + ")";
    }
}
