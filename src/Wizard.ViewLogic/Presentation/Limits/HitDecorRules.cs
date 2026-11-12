namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3: 적중 1회에 덧붙는 것(임계 +4, 약점 +2, 저항 −2), 최소형의 입자 수,
    // "같은 적에게 0.05초 안에 3번 넘게 맞으면 큰 불꽃 하나(입자 8)로 합친다", "한 프레임에 5마리 넘게 죽으면 조각을 절반으로"
    public readonly struct HitDecorRules
    {
        public readonly int ExtraCrit;
        public readonly int ExtraWeak;
        public readonly int ExtraResist;
        public readonly int MinimalParticles;
        public readonly int MergeWindowMs;
        public readonly int MergeOverHits;
        public readonly int MergedParticles;
        public readonly int KillHalveOverPerFrame;

        public HitDecorRules(int extraCrit, int extraWeak, int extraResist, int minimalParticles,
                             int mergeWindowMs, int mergeOverHits, int mergedParticles, int killHalveOverPerFrame)
        {
            ExtraCrit = extraCrit;
            ExtraWeak = extraWeak;
            ExtraResist = extraResist;
            MinimalParticles = minimalParticles;
            MergeWindowMs = mergeWindowMs;
            MergeOverHits = mergeOverHits;
            MergedParticles = mergedParticles;
            KillHalveOverPerFrame = killHalveOverPerFrame;
        }
    }
}
