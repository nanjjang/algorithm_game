using Wizard.Core;

namespace Wizard.Sim.Enemies
{
    // CONTRACTS 3.6. 종마다 하나. 상태가 없다(상태는 EnemyState 에)
    public interface IEnemyBrain
    {
        AiKind Kind { get; }
        void OnSpawn(ref EnemyState self, in EnemyContext ctx, ref Pcg32 rng);
        void Think(ref EnemyState self, in EnemyContext ctx, ref Pcg32 rng, ref EnemyIntent intent);
        void OnTelegraphGranted(ref EnemyState self, in EnemyContext ctx, int zoneId);
        void OnStrike(ref EnemyState self, in EnemyContext ctx, IEnemyActions actions);     // 탄 생성, 돌진 시작 등
        void OnInterrupted(ref EnemyState self, in EnemyContext ctx);
        void OnDeath(ref EnemyState self, in EnemyContext ctx, IEnemyActions actions);      // 유폭, 갈라짐
    }
}
