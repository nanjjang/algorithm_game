namespace Wizard.ViewLogic.Presentation.Limits
{
    // ParticleBudget.Request 의 결과. Granted 개만 그린다(0 이면 그리지 않는다). BatchId 는 자리를 내줄 때 가리키는 핸들
    public readonly struct ParticleGrant
    {
        public readonly long BatchId;
        public readonly int Requested;
        public readonly int Granted;

        public ParticleGrant(long batchId, int requested, int granted)
        {
            BatchId = batchId;
            Requested = requested;
            Granted = granted;
        }
    }
}
