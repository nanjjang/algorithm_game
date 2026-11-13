using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Assets
{
    // 가로 띠 그림의 칸 크기와 프레임 수. 매니페스트가 적지 않은 캐릭터 초상·큰 그림·실루엣의 규격(spec 14 4.6절 표)을
    // asset_aliases.json 의 character_sheets 에서 받는다
    public readonly struct SheetStrip
    {
        public readonly PixelSize Cell;
        public readonly int Frames;

        public SheetStrip(PixelSize cell, int frames)
        {
            Cell = cell;
            Frames = frames;
        }
    }
}
