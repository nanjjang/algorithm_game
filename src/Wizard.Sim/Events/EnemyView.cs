using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3(T-54). 적 하나의 읽기 전용 값. 세계가 읽을 때마다 적 상태에서 만들어 준다(뷰 훅 View_Enemies_*).
    // 칸은 더하기만 한다. 종별 연출(예비 동작의 종류, 정예 표식 등)은 표시 이벤트(spec 05 14절)가 알리고, 여기에는 매 틱 읽는 연속 값만 둔다.
    public readonly struct EnemyView
    {
        public readonly EntityId Id;
        public readonly EnemyTypeId Type;                 // 종(spec 14 3절). 종이 없는 개체는 None
        public readonly FixVec2 Pos;                      // 판정 중심(칸)
        public readonly Fix64 BodyRadius;                 // 몸 반지름(칸)
        public readonly Fix64 Hp, MaxHp;
        public readonly uint WindupStartTick;             // 공격 준비(시전, 예비 동작) 중이면 시작 틱. 아니면 0
        public readonly int WindupTicks;                  // 그 길이. 0이면 준비 중이 아니다. 끝나는 틱 = WindupStartTick + WindupTicks
                                                          // (TelegraphView 와 같은 꼴. 표시는 이 둘로만 채움을 센다)

        public EnemyView(EntityId id, EnemyTypeId type, FixVec2 pos, Fix64 bodyRadius, Fix64 hp, Fix64 maxHp,
                         uint windupStartTick, int windupTicks)
        {
            Id = id;
            Type = type;
            Pos = pos;
            BodyRadius = bodyRadius;
            Hp = hp;
            MaxHp = maxHp;
            WindupStartTick = windupStartTick;
            WindupTicks = windupTicks;
        }
    }
}
