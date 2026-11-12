using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Common
{
    // spec 14 4.3절 "글꼴"(UI 1.4, RD-ui). 글자 크기는 여섯 가지만 쓴다. 정수 배로만 키운다(점 필터, 안티에일리어싱 끔).
    // 원본 두 벌(12px급, 16px급)의 아틀라스 치수는 MAN-ui 의 F-12·F-16 항목과 같다.
    public static class UiFonts
    {
        public const int SmallSourcePx = 12;
        public const int BodySourcePx = 16;
        public const int AtlasSheetPx = 2048;

        // ID, 원본, 배율, 줄 높이 — spec 14 4.3 글꼴 표
        public static readonly UiFontSpec F12 = new UiFontSpec(UiFontId.F12, SmallSourcePx, 1, 16);
        public static readonly UiFontSpec F16 = new UiFontSpec(UiFontId.F16, BodySourcePx, 1, 24);
        public static readonly UiFontSpec F24 = new UiFontSpec(UiFontId.F24, SmallSourcePx, 2, 32);
        public static readonly UiFontSpec F32 = new UiFontSpec(UiFontId.F32, BodySourcePx, 2, 40);
        public static readonly UiFontSpec F48 = new UiFontSpec(UiFontId.F48, BodySourcePx, 3, 56);
        public static readonly UiFontSpec F64 = new UiFontSpec(UiFontId.F64, BodySourcePx, 4, 72);

        public static readonly IReadOnlyList<UiFontSpec> All = new[] { F12, F16, F24, F32, F48, F64 };

        // 원본 아틀라스: 12px급 14×14 칸 한 줄 146자, 16px급 18×18 칸 한 줄 113자. 각각 2048×2048 한 장
        public static readonly FontAtlasSpec Atlas12 = new FontAtlasSpec("F-12", SmallSourcePx, 14, 146, AtlasSheetPx, 11);
        public static readonly FontAtlasSpec Atlas16 = new FontAtlasSpec("F-16", BodySourcePx, 18, 113, AtlasSheetPx, 13);

        // 숫자 그림 글꼴. F-N1·F-N2 는 같은 18자, F-N3 는 0~9 와 % 11자
        public const string ScreenNumberGlyphs = "0123456789,.:%+−×/";
        public const string BigPercentGlyphs = "0123456789%";
        public static readonly NumberFontSpec NumbersSmall = new NumberFontSpec("F-N1", 10, 16, ScreenNumberGlyphs);
        public static readonly NumberFontSpec NumbersLarge = new NumberFontSpec("F-N2", 20, 32, ScreenNumberGlyphs);
        public static readonly NumberFontSpec PercentHuge = new NumberFontSpec("F-N3", 80, 128, BigPercentGlyphs);

        // F-N4 "상한" 딱지 한 장(확률이 상한에 닿았을 때 숫자 옆)
        public const string CapLabelAssetId = "F-N4";
        public static readonly PixelSize CapLabelSize = new PixelSize(64, 32);

        public static bool IsAllowedSize(int sizePx) => TryGet(sizePx, out _);

        public static bool TryGet(int sizePx, out UiFontSpec spec)
        {
            foreach (UiFontSpec s in All)
            {
                if (s.SizePx == sizePx)
                {
                    spec = s;
                    return true;
                }
            }
            spec = default;
            return false;
        }

        // 허용 크기(12/16/24/32/48/64)가 아니면 ArgumentOutOfRangeException. 10px 따위를 조용히 바꿔 주지 않는다(spec 14 TV-159)
        public static UiFontSpec Get(int sizePx)
        {
            if (!TryGet(sizePx, out UiFontSpec spec))
            {
                throw new ArgumentOutOfRangeException(nameof(sizePx), sizePx, "font size must be one of 12, 16, 24, 32, 48, 64 px (spec 14 4.3)");
            }
            return spec;
        }

        public static UiFontSpec Get(UiFontId id) => Get((int)id);

        public static FontAtlasSpec AtlasFor(UiFontSpec spec) => spec.SourcePx == SmallSourcePx ? Atlas12 : Atlas16;

        public static string AssetIdOf(UiFontId id)
        {
            switch (id)
            {
                case UiFontId.F12: return "F-12";
                case UiFontId.F16: return "F-16";
                case UiFontId.F24: return "F-24";
                case UiFontId.F32: return "F-32";
                case UiFontId.F48: return "F-48";
                case UiFontId.F64: return "F-64";
                default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }
    }
}
