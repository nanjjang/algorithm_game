using Wizard.Sim.Events;

namespace Wizard.Sim.Enemies
{
    // CONTRACTS 3.6. 두뇌가 읽을 수 있는 것
    public readonly struct EnemyContext
    {
        public readonly uint Tick;
        public readonly int Wave;
        public readonly ISimView World;
        public readonly IArenaQuery Arena;                // 벽, 기둥, 설치 벽, 레이캐스트
        public readonly ITelegraphService Telegraphs;
    }
}
