using System;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. 전장 24×14칸, 사방이 막혀 있다(DECISIONS 2.3). 모드마다 하나(ContentDb.Arena(SimMode)). 원본 data/sim/arena.json.
    // 좌표는 칸 단위 Fix64, 왼쪽 위 안쪽 모서리가 (0, 0), y 는 아래가 +(CONTRACTS 0.3).
    public sealed class ArenaDef
    {
        public Fix64 Width, Height;

        // 1×1칸 기둥(world-characters 2.6). 값은 그 칸의 작은 쪽 모서리다: 칸 좌표 (9,3) = 범위 x 9~10, y 3~4
        public FixVec2[] Pillars = Array.Empty<FixVec2>();

        // 시작 위치(판정 중심, spec 01 3.7). 슬롯 순
        public FixVec2[] SpawnPoints = Array.Empty<FixVec2>();
    }
}
