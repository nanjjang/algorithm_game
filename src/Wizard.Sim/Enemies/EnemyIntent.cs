using Wizard.Core;

namespace Wizard.Sim.Enemies
{
    // CONTRACTS 3.6. 두뇌가 쓰는 것. 세계가 9단계에서 적용한다
    public struct EnemyIntent
    {
        public FixVec2 MoveDelta;
        public bool WantsAttack;
        public int AttackIndex;
        public FixVec2 AimPoint;
    }
}
