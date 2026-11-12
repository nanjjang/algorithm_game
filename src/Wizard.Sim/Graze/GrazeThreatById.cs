using System.Collections.Generic;

namespace Wizard.Sim.Graze
{
    // 같은 틱의 후보를 위협 id 오름차순으로 둔다(spec 01 8.3 "id 순"). 구조체 비교자라 박싱이 없다(CONTRACTS 1.5 DetSort).
    // id 가 같은 후보(같은 위협을 두 번 넣은 것)는 안정 정렬이 넣은 순서를 지킨다.
    internal struct GrazeThreatById : IComparer<GrazeThreat>
    {
        public int Compare(GrazeThreat x, GrazeThreat y) =>
            x.Id.Value < y.Id.Value ? -1 : (x.Id.Value > y.Id.Value ? 1 : 0);
    }
}
