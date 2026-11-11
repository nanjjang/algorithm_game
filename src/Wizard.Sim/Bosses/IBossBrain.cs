using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Enemies;

namespace Wizard.Sim.Bosses
{
    // CONTRACTS 3.6
    public interface IBossBrain
    {
        BossId Id { get; }
        void OnIntro(ref BossState self, in EnemyContext ctx);
        void Think(ref BossState self, in EnemyContext ctx, ref Pcg32 rng, IBossActions actions);
        void OnPhaseChanged(ref BossState self, int phase, in EnemyContext ctx, IBossActions actions);
        void OnDeath(ref BossState self, in EnemyContext ctx, IBossActions actions);
    }
}
