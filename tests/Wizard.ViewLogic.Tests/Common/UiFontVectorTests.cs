using System;
using System.Linq;
using Wizard.ViewLogic.Common;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 14 7.7 TV-159~165, 4.3절 글꼴 표
    public sealed class UiFontVectorTests
    {
        [Fact]
        public void S14_TV_159__OnlySixSizesAreAllowedAnd10pxIsAnError()
        {
            Assert.Equal(new[] { 12, 16, 24, 32, 48, 64 }, UiFonts.All.Select(f => f.SizePx).ToArray());
            foreach (int px in new[] { 12, 16, 24, 32, 48, 64 })
            {
                Assert.True(UiFonts.IsAllowedSize(px));
            }
            Assert.False(UiFonts.IsAllowedSize(10));
            Assert.False(UiFonts.TryGet(10, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiFonts.Get(10));
            Assert.False(UiFonts.IsAllowedSize(20));
        }

        [Fact]
        public void S14_TV_160__F24IsThe12pxSourceAt2xWithLineHeight32()
        {
            UiFontSpec f = UiFonts.Get(24);
            Assert.Equal(UiFontId.F24, f.Id);
            Assert.Equal(12, f.SourcePx);
            Assert.Equal(2, f.Scale);
            Assert.Equal(32, f.LineHeightPx);
            Assert.Equal(f.SizePx, f.SourcePx * f.Scale);
        }

        [Fact]
        public void S14_TV_161__F48IsThe16pxSourceAt3xWithLineHeight56()
        {
            UiFontSpec f = UiFonts.Get(48);
            Assert.Equal(16, f.SourcePx);
            Assert.Equal(3, f.Scale);
            Assert.Equal(56, f.LineHeightPx);
            Assert.Equal(f.SizePx, f.SourcePx * f.Scale);
        }

        [Fact]
        public void S14_TV_162__F12AtlasIs14pxCells146PerRowOn2048Sheet()
        {
            FontAtlasSpec a = UiFonts.Atlas12;
            Assert.Equal("F-12", a.AssetId);
            Assert.Equal(14, a.CellPx);
            Assert.Equal(146, a.Columns);
            Assert.Equal(2048, a.SheetPx);
            Assert.True(a.Columns * a.CellPx <= a.SheetPx);
            Assert.True(a.Capacity >= 11172);   // 현대 한글 완성형 전부를 한 장에(spec 14 4.3 글자 범위)
        }

        [Fact]
        public void S14_TV_163__F16AtlasIs18pxCells113PerRow()
        {
            FontAtlasSpec a = UiFonts.Atlas16;
            Assert.Equal("F-16", a.AssetId);
            Assert.Equal(18, a.CellPx);
            Assert.Equal(113, a.Columns);
            Assert.Equal(2048, a.SheetPx);
            Assert.True(a.Columns * a.CellPx <= a.SheetPx);
            Assert.True(a.Capacity >= 11172);
        }

        [Fact]
        public void S14_TV_164__FN1GlyphOrderIs18CharsOn180x16()
        {
            NumberFontSpec n = UiFonts.NumbersSmall;
            Assert.Equal("F-N1", n.AssetId);
            Assert.Equal("0123456789,.:%+−×/", n.Glyphs);
            Assert.Equal(18, n.GlyphCount);
            Assert.Equal(180, n.SheetSize.Width);
            Assert.Equal(16, n.SheetSize.Height);
            Assert.Equal(14, n.IndexOf('+'));
            Assert.Equal(-1, n.IndexOf('-'));   // 빼기는 U+2212 하나뿐이다
        }

        [Fact]
        public void S14_TV_165__FN3Is80x128Times11Equals880x128()
        {
            NumberFontSpec n = UiFonts.PercentHuge;
            Assert.Equal("F-N3", n.AssetId);
            Assert.Equal(80, n.GlyphWidthPx);
            Assert.Equal(128, n.GlyphHeightPx);
            Assert.Equal(11, n.GlyphCount);
            Assert.Equal("0123456789%", n.Glyphs);
            Assert.Equal(880, n.SheetSize.Width);
            Assert.Equal(128, n.SheetSize.Height);
        }

        [Fact]
        public void FontTableMatchesSpec14Section43()
        {
            // ID, 원본, 배율, 줄 높이(spec 14 4.3 글꼴 표)
            var expected = new[]
            {
                (12, 12, 1, 16), (16, 16, 1, 24), (24, 12, 2, 32), (32, 16, 2, 40), (48, 16, 3, 56), (64, 16, 4, 72),
            };
            Assert.Equal(expected, UiFonts.All.Select(f => (f.SizePx, f.SourcePx, f.Scale, f.LineHeightPx)).ToArray());
            Assert.Equal(new[] { "F-12", "F-16", "F-24", "F-32", "F-48", "F-64" }, UiFonts.All.Select(f => f.AssetId).ToArray());
            Assert.Equal("F-N2", UiFonts.NumbersLarge.AssetId);
            Assert.Equal(20, UiFonts.NumbersLarge.GlyphWidthPx);
            Assert.Equal(32, UiFonts.NumbersLarge.GlyphHeightPx);
            Assert.Equal(UiFonts.NumbersSmall.Glyphs, UiFonts.NumbersLarge.Glyphs);
            Assert.Equal(64, UiFonts.CapLabelSize.Width);
            Assert.Equal(32, UiFonts.CapLabelSize.Height);
            Assert.Equal(UiFonts.AtlasFor(UiFonts.Get(24)).AssetId, UiFonts.Atlas12.AssetId);
            Assert.Equal(UiFonts.AtlasFor(UiFonts.Get(64)).AssetId, UiFonts.Atlas16.AssetId);
        }
    }
}
