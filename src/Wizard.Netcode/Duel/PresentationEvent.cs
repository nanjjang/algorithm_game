using Wizard.Sim.Events;

namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4
    public struct PresentationEvent
    {
        public SimEvent Event;
        public EventPhase Phase;
        public ushort Ordinal;            // 그 틱에서 같은 (Type, Slot)의 몇 번째인가. 열쇠 (Tick, Type, Slot, Ordinal)에 EntityId 를 넣지 않는다
    }
}
