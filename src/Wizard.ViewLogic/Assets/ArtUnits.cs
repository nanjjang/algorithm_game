using Wizard.Core;

namespace Wizard.ViewLogic.Assets
{
    // 그림 배율(spec 14 4.1절 [조율: DEC 2.1]): 1칸 = 64px, 그림 1픽셀 = 화면 1픽셀. 게임 수치는 칸 단위 그대로이고
    // 그림 좌표로 옮길 때만 이 환산을 쓴다. 칸 → 실수 px 는 FixConvert 를 거친다(Fix64 → float 는 거기에만 있다)
    public static class ArtUnits
    {
        public const int PixelsPerCell = 64;

        // 1px 의 Q32.32 Raw = 2^32 / 64 = 2^26 = 67,108,864 (TV-227)
        public static readonly long RawPerPixel = Fix64.One.Raw / PixelsPerCell;

        // 칸 → px(표시용 실수). 0.15칸 → 9.6px (TV-226)
        public static float CellsToPx(Fix64 cells) => FixConvert.ToFloat(cells) * PixelsPerCell;

        // 정수 px → 칸(Q32.32). 정확하다(1px = 2^26 Raw)
        public static Fix64 PxToCells(int px) => Fix64.FromRaw(px * RawPerPixel);

        // 정수 칸 → px (24×14칸 → 1536×896, TV-225)
        public static int WholeCellsToPx(int cells) => cells * PixelsPerCell;

        // 칸 값이 정수 px 에 떨어지면 그 px(6.0칸 → 384px, TV-229). 1/64칸의 배수가 아니면 false
        public static bool TryCellsToWholePx(Fix64 cells, out int px)
        {
            px = 0;
            if (cells.Raw % RawPerPixel != 0)
            {
                return false;
            }
            px = (int)(cells.Raw / RawPerPixel);
            return true;
        }
    }
}
