namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 본체에서 떨어진 조각 그림(레지스트리 변형 "detached_*", asset_aliases.json wand_detached)과 그리는 단계 범위(spec 04 12.1절)
    public readonly struct WandDetachedRange
    {
        public readonly string Variant;
        public readonly int MinStage;
        public readonly int MaxStage;

        public WandDetachedRange(string variant, int minStage, int maxStage)
        {
            Variant = variant;
            MinStage = minStage;
            MaxStage = maxStage;
        }

        public bool Covers(int stage) => stage >= MinStage && stage <= MaxStage;
    }
}
