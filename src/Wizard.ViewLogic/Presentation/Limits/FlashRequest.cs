namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.2 FlashRequest. 불투명도와 면적은 0..1, 켜짐·꺼짐 길이는 밀리초
    public readonly struct FlashRequest
    {
        public readonly FlashColor Color;
        public readonly float Opacity;
        public readonly int RiseMs;
        public readonly int FallMs;
        public readonly float Coverage;      // 화면 비율 0..1
        public readonly FlashSource Source;

        public FlashRequest(FlashColor color, float opacity, int riseMs, int fallMs, float coverage, FlashSource source)
        {
            Color = color;
            Opacity = opacity;
            RiseMs = riseMs;
            FallMs = fallMs;
            Coverage = coverage;
            Source = source;
        }
    }
}
