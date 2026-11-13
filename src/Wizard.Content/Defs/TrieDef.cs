using System;

namespace Wizard.Content
{
    // CONTRACTS 2.2. 캐릭터별 커맨드 트라이의 데이터(spec 03 5절). 팩에서 읽은 뒤 바뀌지 않는다.
    // 원본 data/sim/tries.json, 절 꼴은 Pack/Sections/ContentDb.Tries.cs.
    //   - Arts: 이 캐릭터의 트라이에 넣는 비기. 공용 10 + 그 캐릭터의 고유 1 = 11종(spec 03 5.1). ArtId 오름차순.
    //     경로·슬롯·비용은 ArtDef(arts 절)에 있다. 트라이를 만드는 것은 Wizard.Sim.Arts.CommandTrie 다(노드, 실패 링크, 간격, 전이).
    //   - 판정기 상수(spec 03 1.2, 입력 시각 단위 1/15,360초)와 외상 빚 상한(9.2)은 모든 캐릭터가 같은 값이다.
    //     절에 한 벌만 있고 캐릭터마다 같은 값을 싣는다(접근자가 캐릭터별이라서).
    //     DirHistorySize(최근 방향 입력 8개)는 판정기의 자료구조 크기라 데이터가 아니다(CommandTrie.DirHistorySize).
    public sealed class TrieDef
    {
        public CharacterId Character;
        public ArtId[] Arts = Array.Empty<ArtId>();

        public long JustWindowUnits;        // 섬 창 0.07초 = 1,075
        public long GapFullBloomUnits;      // 만개의 각 간격 0.18초 = 2,765
        public long GapShortUnits;          // 앞→뒤, 옆→앞, 앞→옆, 좌→우, char.1 의 뒤→뒤 0.2초 = 3,072
        public long GapSameSideUnits;       // 같은 쪽 옆 두 번 0.25초 = 3,840
        public long GapDefaultUnits;        // 기본 간격 = 커맨드 초기화 시간 0.3초 = 4,608
        public long ArtCooldownUnits;       // 비기 공통 재사용 대기 0.4초 = 6,144
        public long KBufferWindowUnits;     // 대기 끝 전 K 보관 창 0.1초 = 1,536
        public int MaxDebt;                 // 외상 빚 상한(결정 2개, spec 03 9.2)
    }
}
