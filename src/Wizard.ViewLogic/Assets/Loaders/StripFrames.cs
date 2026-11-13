using System.Collections.Generic;

namespace Wizard.ViewLogic.Assets.Loaders
{
    // 가로 띠의 프레임 사각형(spec 14 4.4절 "한 파일에 가로로 이어 그린다. width·height 는 프레임 하나의 크기")
    internal static class StripFrames
    {
        public static PixelRect[] Horizontal(int x, int y, int width, int height, int frames)
        {
            var rects = new PixelRect[frames];
            for (int i = 0; i < frames; i++)
            {
                rects[i] = new PixelRect(x + i * width, y, width, height);
            }
            return rects;
        }

        public static IReadOnlyList<PixelRect> One(int width, int height) => new[] { new PixelRect(0, 0, width, height) };
    }
}
