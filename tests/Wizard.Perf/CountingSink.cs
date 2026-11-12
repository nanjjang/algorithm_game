using Wizard.Sim.Events;

namespace Wizard.Perf
{
    // 이벤트를 세기만 하는 받는 쪽(할당 없음). 표시 계층의 비용은 재지 않는다: 재는 것은 Step 이다.
    // 부하의 크기를 알리려고 탄·적이 생긴 이벤트를 따로 센다(살아 있는 개체 수는 ISimView 로 읽는다. TickBench)
    internal sealed class CountingSink : ISimEventSink
    {
        public long Count;
        public long ProjectilesSpawned;      // 플레이어 탄(spec 04 18절 ProjectileSpawned)
        public long EnemiesSpawned;
        public long EnemyShotsFired;

        public void Emit(in SimEvent e)
        {
            Count++;
            switch (e.Type)
            {
                case SimEventType.ProjectileSpawned:
                    ProjectilesSpawned++;
                    break;
                case SimEventType.EnemySpawn:
                    EnemiesSpawned++;
                    break;
                case SimEventType.EnemyProjectileFire:
                    EnemyShotsFired++;
                    break;
            }
        }
    }
}
