namespace Wizard.Server.Data
{
    // CONTRACTS 7.2
    public readonly struct IdempotentResult<T>
    {
        public readonly bool Conflict;
        public readonly bool Replayed;
        public readonly T Value;

        public IdempotentResult(bool conflict, bool replayed, T value)
        {
            Conflict = conflict;
            Replayed = replayed;
            Value = value;
        }
    }
}
