using Wizard.Core;

namespace Wizard.Sim.Input
{
    // CONTRACTS 3.1. 입력은 언제나 (슬롯, InputEvent) 열로만 들어간다(ARCHITECTURE P2)
    public readonly struct PlayerInput
    {
        public readonly PlayerSlot Slot;
        public readonly InputEvent Event;

        public PlayerInput(PlayerSlot slot, InputEvent e)
        {
            Slot = slot;
            Event = e;
        }
    }
}
