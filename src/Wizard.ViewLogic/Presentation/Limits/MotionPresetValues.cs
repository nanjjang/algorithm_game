namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.5 PresetValues. 입자 예산은 data/presentation/particle_budget.json 이 유일한 기준이라(DECISIONS 2.8)
    // 여기에 두지 않고 ParticleBudgetTable.SceneTotal(ParticleScene.Battle, 단계)로 읽는다.
    public readonly struct MotionPresetValues
    {
        public readonly MotionPreset Preset;
        public readonly float ShakeMul;
        public readonly float FlashCap;      // 0 이면 번쩍 없음(테두리 강조로 대체)
        public readonly float HitstopMul;
        public readonly float ZoomMul;
        public readonly TelegraphFlash Telegraph;
        public readonly IFrameStyle IFrame;
        public readonly OverlayMode Overlay;
        public readonly RitualLevel Ritual;

        public MotionPresetValues(MotionPreset preset, float shakeMul, float flashCap, float hitstopMul, float zoomMul,
                                  TelegraphFlash telegraph, IFrameStyle iframe, OverlayMode overlay, RitualLevel ritual)
        {
            Preset = preset;
            ShakeMul = shakeMul;
            FlashCap = flashCap;
            HitstopMul = hitstopMul;
            ZoomMul = zoomMul;
            Telegraph = telegraph;
            IFrame = iframe;
            Overlay = overlay;
            Ritual = ritual;
        }
    }
}
