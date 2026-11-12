namespace Wizard.ViewLogic.Common
{
    // 글꼴 원본 아틀라스 한 장(spec 14 4.3 "아틀라스", MAN-ui 의 F-12·F-16 항목). 글자당 여백 1px 을 넣은 칸 CellPx×CellPx 를
    // 한 줄에 Columns 개씩 SheetPx×SheetPx 한 장에 굽는다
    public readonly struct FontAtlasSpec
    {
        public readonly string AssetId;
        public readonly int SourcePx;
        public readonly int CellPx;
        public readonly int Columns;
        public readonly int SheetPx;
        public readonly int BaselinePx;

        public FontAtlasSpec(string assetId, int sourcePx, int cellPx, int columns, int sheetPx, int baselinePx)
        {
            AssetId = assetId;
            SourcePx = sourcePx;
            CellPx = cellPx;
            Columns = columns;
            SheetPx = sheetPx;
            BaselinePx = baselinePx;
        }

        public int Rows => SheetPx / CellPx;

        public int Capacity => Columns * Rows;
    }
}
