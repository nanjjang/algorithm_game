namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "꼬리 띠의 한도(입자가 아니어도 지킨다)"와 바닥 흔적 그림의 한도
    public readonly struct TrailLimits
    {
        public readonly int MaxLengthPx;
        public readonly int MaxFrames;
        public readonly int MaxOpacityPercent;
        public readonly int GroundMarksPerWand;
        public readonly int GroundMarkMaxDiameterPx;
        public readonly int GroundMarkMaxOpacityPercent;
        public readonly int GroundMarkMaxLifeMs;

        public TrailLimits(int maxLengthPx, int maxFrames, int maxOpacityPercent,
                           int groundMarksPerWand, int groundMarkMaxDiameterPx, int groundMarkMaxOpacityPercent, int groundMarkMaxLifeMs)
        {
            MaxLengthPx = maxLengthPx;
            MaxFrames = maxFrames;
            MaxOpacityPercent = maxOpacityPercent;
            GroundMarksPerWand = groundMarksPerWand;
            GroundMarkMaxDiameterPx = groundMarkMaxDiameterPx;
            GroundMarkMaxOpacityPercent = groundMarkMaxOpacityPercent;
            GroundMarkMaxLifeMs = groundMarkMaxLifeMs;
        }
    }
}
