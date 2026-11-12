namespace Wizard.ViewLogic.Common
{
    // 숫자 그림 글꼴(spec 14 4.3 F-N1 ~ F-N3). 글자 Glyphs 를 순서대로 GlyphWidthPx×GlyphHeightPx 칸에 가로로 늘어놓은 한 장
    public readonly struct NumberFontSpec
    {
        public readonly string AssetId;
        public readonly int GlyphWidthPx;
        public readonly int GlyphHeightPx;
        public readonly string Glyphs;

        public NumberFontSpec(string assetId, int glyphWidthPx, int glyphHeightPx, string glyphs)
        {
            AssetId = assetId;
            GlyphWidthPx = glyphWidthPx;
            GlyphHeightPx = glyphHeightPx;
            Glyphs = glyphs;
        }

        public int GlyphCount => Glyphs.Length;

        public PixelSize SheetSize => new PixelSize(GlyphWidthPx * Glyphs.Length, GlyphHeightPx);

        // 글자의 칸 번호. 없는 글자면 -1
        public int IndexOf(char glyph) => Glyphs.IndexOf(glyph);
    }
}
