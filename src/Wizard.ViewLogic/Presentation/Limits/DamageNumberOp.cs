namespace Wizard.ViewLogic.Presentation.Limits
{
    // 피해 숫자 한 건의 처리. EvictedId 가 0 이 아니면 40개 상한 때문에 그 숫자를 먼저 지운다
    public readonly struct DamageNumberOp
    {
        public readonly DamageNumberOpKind Kind;
        public readonly long Id;
        public readonly long Amount;
        public readonly long EvictedId;

        public DamageNumberOp(DamageNumberOpKind kind, long id, long amount, long evictedId)
        {
            Kind = kind;
            Id = id;
            Amount = amount;
            EvictedId = evictedId;
        }
    }
}
