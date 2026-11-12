#nullable enable
using System.Globalization;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 화면 그림 좌표의 사각형(정수 px). 원점은 왼쪽 위, y 는 아래로 는다(spec 14 4.3: 1920×1080 무대, 정수 px 에만 놓는다).
    // 위젯의 LocalRect 는 부모의 왼쪽 위 기준, AbsoluteRect 는 무대(UiCanvas.Stage)의 왼쪽 위 기준이다.
    public readonly struct PixelRect
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

        // 반 px 를 버리지 않게 두 배로 센 가운데(정렬과 거리 비교에만 쓴다)
        public int CenterX2 => X + X + Width;
        public int CenterY2 => Y + Y + Height;

        public PixelSize Size => new PixelSize(Width, Height);

        // 오른쪽·아래 끝은 들지 않는다
        public bool Contains(PixelPoint p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;

        public PixelRect Offset(int dx, int dy) => new PixelRect(X + dx, Y + dy, Width, Height);

        // 네 변을 px 만큼 안으로(음수면 밖으로)
        public PixelRect Inset(int px) => new PixelRect(X + px, Y + px, Width - px - px, Height - px - px);

        public static PixelRect FromSize(int width, int height) => new PixelRect(0, 0, width, height);

        public override string ToString() =>
            "(" + X.ToString(CultureInfo.InvariantCulture) + ", " + Y.ToString(CultureInfo.InvariantCulture) + ", "
            + Width.ToString(CultureInfo.InvariantCulture) + "x" + Height.ToString(CultureInfo.InvariantCulture) + ")";
    }
}
