using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Enemies
{
    // CONTRACTS 3.6
    public struct EnemyState
    {
        public EntityId Id;
        public EnemyTypeId Type;          // Def 참조 대신 id
        public bool IsElite;
        // 나머지(spec 05 2.4, AI 변수는 고정 길이 int 배열)는 M3-ENM-01 이 더한다
    }
}
