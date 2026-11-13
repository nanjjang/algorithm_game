namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 단계 하나의 강화 연출 변주. Intensity 는 이 단계에 도전할 때의 강도(effects.md 4절 1~5단, +1 도전은 2절의 예외라 0,
    // +0 은 도전 대상이 아니라 null). Scene·TransitionFrames 는 spec 04 18.3절 MilestoneScene·Stage13Achieved·StageTransition.
    // 재질·무늬·악기의 속성별 변주는 지팡이의 Element 로 고른다(effects.md 5절, WandRitualFx)
    public readonly struct WandEnhanceVariant
    {
        public readonly int? Intensity;
        public readonly WandEnhanceScene Scene;
        public readonly int? TransitionFrames;

        public WandEnhanceVariant(int? intensity, WandEnhanceScene scene, int? transitionFrames)
        {
            Intensity = intensity;
            Scene = scene;
            TransitionFrames = transitionFrames;
        }
    }
}
