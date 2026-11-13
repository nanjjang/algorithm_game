using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4, spec 02 5.2. 컴파일 문맥. 캐릭터·지팡이·변이에서 만든다(DECISIONS-TECH T-57)
    public readonly struct WandCombatProfile
    {
        public readonly CharacterId Character;
        public readonly int BaseLinks, ExtraSlots;        // 연결 칸 수 L = BaseLinks + ExtraSlots
        public readonly PowBase DepthDecay;               // Decay085 또는 Decay092
        public readonly bool UnderlinePassive;
        public readonly MutationSet Mutations;

        public WandCombatProfile(CharacterId character, int baseLinks, int extraSlots, PowBase depthDecay,
                                 bool underlinePassive, MutationSet mutations)
        {
            Character = character;
            BaseLinks = baseLinks;
            ExtraSlots = extraSlots;
            DepthDecay = depthDecay;
            UnderlinePassive = underlinePassive;
            Mutations = mutations;
        }
    }
}
