namespace Wizard.ViewLogic.Common
{
    // 글꼴 한 크기. SourcePx 급(12px급 또는 16px급) 비트맵을 Scale 정수 배로 키워 SizePx 를 만든다. LineHeightPx = 줄 높이
    public readonly struct UiFontSpec
    {
        public readonly UiFontId Id;
        public readonly int SourcePx;
        public readonly int Scale;
        public readonly int LineHeightPx;

        public UiFontSpec(UiFontId id, int sourcePx, int scale, int lineHeightPx)
        {
            Id = id;
            SourcePx = sourcePx;
            Scale = scale;
            LineHeightPx = lineHeightPx;
        }

        public int SizePx => (int)Id;

        // MAN-ui 의 글꼴 그림 id("F-12" 따위)
        public string AssetId => UiFonts.AssetIdOf(Id);
    }
}
