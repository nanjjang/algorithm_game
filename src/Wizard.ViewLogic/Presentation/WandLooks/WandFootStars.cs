namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // spec 04 12.7절 wand.5: Stage 단계에서 발밑 별 = 결정(최대 MaxStars), 빚만큼 속 빈 고리(최대 MaxDebtRings)
    public readonly struct WandFootStars
    {
        public readonly int Stage;
        public readonly int MaxStars;
        public readonly int MaxDebtRings;

        public WandFootStars(int stage, int maxStars, int maxDebtRings)
        {
            Stage = stage;
            MaxStars = maxStars;
            MaxDebtRings = maxDebtRings;
        }
    }
}
