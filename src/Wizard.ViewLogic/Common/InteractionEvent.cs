namespace Wizard.ViewLogic.Common
{
    // InteractableGroup 이 내는 일 하나. ReasonKey 는 DisabledPress 에서만 쓴다
    public readonly struct InteractionEvent
    {
        public readonly InteractionEventKind Kind;
        public readonly Interactable Target;
        public readonly string? ReasonKey;

        public InteractionEvent(InteractionEventKind kind, Interactable target, string? reasonKey = null)
        {
            Kind = kind;
            Target = target;
            ReasonKey = reasonKey;
        }

        public BracketSize BracketSize => Target.BracketSize;
    }
}
