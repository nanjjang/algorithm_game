using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4
    public readonly struct WandCombatProfile
    {
        public readonly CharacterId Character;
        public readonly int BaseLinks, ExtraSlots;
        public readonly PowBase DepthDecay;               // Decay085 또는 Decay092
        public readonly bool UnderlinePassive;
        public readonly MutationSet Mutations;
    }
}
