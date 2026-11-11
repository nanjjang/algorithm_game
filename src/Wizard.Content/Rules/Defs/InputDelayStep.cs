namespace Wizard.Content.Rules
{
    // CONTRACTS 2.2. NetRuleSet.InputDelayTable 의 한 줄. 처음 값: ≤60 → 2, ≤90 → 3, 그 밖 4 (online 4.12)
    public readonly struct InputDelayStep
    {
        public readonly int RttUpToMs;     // 이 왕복(ms) 이하이면
        public readonly int DelayTicks;    // 입력 지연 D

        public InputDelayStep(int rttUpToMs, int delayTicks)
        {
            RttUpToMs = rttUpToMs;
            DelayTicks = delayTicks;
        }
    }
}
