using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets
{
    // 없는 그림의 대체(spec 14 5.7절 [임시], CONTRACTS 13 IAssetCatalog.Placeholder):
    // 원하는 크기의 회청·나무 어두움 8px 바둑판 + F-12 글꼴로 Id 글자. 위험색·금색·연두·청록을 쓰지 않는다(4.2절).
    // 색은 토큰 식별자로만 가리키고 실제 색 값과 그리기는 표시 계층(M9-AST-03)이 쥔다
    public static class AssetPlaceholder
    {
        public const string LightToken = "slate";        // 회청 #8E9AAE (spec 14 4.2절 토큰)
        public const string DarkToken = "wood_dark";     // 나무 어두움 #1F2130
        public const int CheckerPx = 8;
        public const string LabelFontId = "F-12";
        public const int DefaultEdgePx = 64;             // 크기를 모를 때 1칸(spec 14 4.1절 1칸 = 64px)

        public static PixelSize DefaultSize => new PixelSize(DefaultEdgePx, DefaultEdgePx);

        // 대체 기록: 프레임 하나, 크기는 원하는 크기(없으면 1칸), 글자는 없는 열쇠의 Id
        public static AssetRecord RecordFor(AssetKey missing, PixelSize size)
        {
            var r = new AssetRecord(missing, "", size, new[] { new PixelRect(0, 0, size.Width, size.Height) });
            r.IsPlaceholder = true;
            r.PlaceholderLabel = missing.Id;
            return r;
        }
    }
}
