namespace Wizard.ViewLogic.Presentation.Limits
{
    // FlashManager.Submit 의 결과. Flash 면 Opacity·RiseMs·FallMs 대로 그린다.
    // BorderHighlight 면 흰 선 BorderWidthPx 를 BorderMs 동안 그린다(정보는 남기고 빛만 뺀다, spec 13 14.2)
    public readonly struct FlashDecision
    {
        public readonly FlashOutcome Outcome;
        public readonly float Opacity;
        public readonly int RiseMs;
        public readonly int FallMs;
        public readonly bool CountedAsFullScreen;   // 관리기의 1초 창에 들어갔는가(작은 국소 번쩍·테두리 강조는 거짓)
        public readonly int BorderWidthPx;
        public readonly int BorderMs;

        private FlashDecision(FlashOutcome outcome, float opacity, int riseMs, int fallMs, bool counted, int borderWidthPx, int borderMs)
        {
            Outcome = outcome;
            Opacity = opacity;
            RiseMs = riseMs;
            FallMs = fallMs;
            CountedAsFullScreen = counted;
            BorderWidthPx = borderWidthPx;
            BorderMs = borderMs;
        }

        public static FlashDecision Flash(float opacity, int riseMs, int fallMs, bool counted) =>
            new FlashDecision(FlashOutcome.Flash, opacity, riseMs, fallMs, counted, 0, 0);

        public static FlashDecision Border(int widthPx, int ms) =>
            new FlashDecision(FlashOutcome.BorderHighlight, 0f, 0, 0, false, widthPx, ms);
    }
}
