using System;

namespace Wizard.Content
{
    // CONTRACTS 2.2. spec 07 2.3(시간대 4행), spec 14 3.9(코드와 밝은 림 세기). 팩에서 읽은 뒤 바뀌지 않는다.
    // 원본 data/sim/tod.json. ContentDb.TimeOfDay(tod) 로 찾는다.
    // 시간대가 하루 속 몇 번째 웨이브인지(spec 07 2.3 "하루 속 웨이브")는 한 곳에만 둔다: WaveCurveDef.TodRange*(spec 07 2.1 tod_ranges).
    // 바닥 색·팔레트는 표시 계층의 값이다(world-characters 2.4). 코어에 두지 않는다.
    public sealed class TimeOfDayDef
    {
        public TimeOfDay Id;

        // 일반 적이 나오는가. tod.boss 는 false(보스와 졸개만, spec 07 2.3·7.5). false 면 Weight* 는 비어 있다
        public bool NormalEnemies;

        // 스폰 가중치 배율(백분율 정수, ×1.5 → 150). 0 은 "나오지 않음". 여기 없는 종은 100 이다(spec 07 2.3).
        // WeightSpecies 는 EnemyTypeId 오름차순이고 겹치지 않는다. WeightPct[i] 가 WeightSpecies[i] 의 배율이다
        public EnemyTypeId[] WeightSpecies = Array.Empty<EnemyTypeId>();
        public int[] WeightPct = Array.Empty<int>();

        // 적 윤곽의 밝은 림 세기(천분율, 20% → 200). spec 14 3.9·TV-051, monsters 1.4. 표시 이벤트 enemy.rim_strength 의 값이고 판정에 쓰지 않는다
        public int RimStrengthPermille;
    }
}
