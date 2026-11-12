namespace Wizard.ViewLogic.Presentation.Limits
{
    // 일회성 몫이 차서 자리를 내주는 입자: 묶음 BatchId 의 Count 개를 FadeMs(0.1초)에 걸쳐 흐리게 지운다.
    // 자리는 바로 비워진다(흐려지는 동안 새 사건이 그 자리를 쓴다)
    public readonly struct ParticleEviction
    {
        public readonly long BatchId;
        public readonly ParticleCategory Category;
        public readonly int Count;
        public readonly int FadeMs;

        public ParticleEviction(long batchId, ParticleCategory category, int count, int fadeMs)
        {
            BatchId = batchId;
            Category = category;
            Count = count;
            FadeMs = fadeMs;
        }
    }
}
