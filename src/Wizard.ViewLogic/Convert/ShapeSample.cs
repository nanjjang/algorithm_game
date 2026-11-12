using System.Numerics;

namespace Wizard.ViewLogic.Convert
{
    // 한 틱의 도형 하나(표시 좌표, 실수). 세계의 읽기 전용 값(ISimView 의 PlayerView·EnemyView·ProjectileView)을 FixConvert 로 옮긴 것이다.
    // 표시 좌표: 1칸 = 1유닛, y 는 위로 는다(FixConvert.ToWorld). 방향은 같은 변환을 거친 단위 벡터다(없으면 0).
    public readonly struct ShapeSample
    {
        public readonly int Id;                   // 개체 id(EntityId.Value). 플레이어는 슬롯 번호
        public readonly ShapeKind Kind;
        public readonly Vector2 Pos;
        public readonly float RadiusCells;
        public readonly Vector2 Dir;
        public readonly float HpRatio;            // 0~1. 체력이 없는 것은 1
        public readonly uint CastStartTick;       // 공격 준비(시전, 예비 동작)의 시작 틱(EnemyView.WindupStartTick)
        public readonly int CastTicks;            // 그 길이(EnemyView.WindupTicks). 0이면 준비 중이 아니다
        public readonly byte Owner;               // 탄의 주인 슬롯. 해당 없으면 255
        public readonly int Variant;              // 탄의 코어 블록 번호, 적의 종 번호, 플레이어의 캐릭터 번호. 해당 없으면 0

        public ShapeSample(int id, ShapeKind kind, Vector2 pos, float radiusCells, Vector2 dir, float hpRatio,
                           uint castStartTick, int castTicks, byte owner, int variant)
        {
            Id = id;
            Kind = kind;
            Pos = pos;
            RadiusCells = radiusCells;
            Dir = dir;
            HpRatio = hpRatio;
            CastStartTick = castStartTick;
            CastTicks = castTicks;
            Owner = owner;
            Variant = variant;
        }

        // 공격 준비 중인가(예고를 그린다)
        public bool Casting => CastTicks > 0;

        // 준비가 끝나는(공격이 나가는) 틱
        public uint CastEndTick => unchecked(CastStartTick + (uint)CastTicks);

        // 예고의 채움 0~1. 세계가 준 시작 틱과 길이로만 센다(CONTRACTS 3.3: 표시 쪽 시계나 "처음 본 틱"을 쓰지 않는다).
        // viewTick = ISimView.Tick(다음에 돌 틱). 그리는 시각은 위치 보간과 같이 한 틱 늦은 (viewTick − 1 + alpha)다.
        // 시작한 틱의 상태를 처음 본 순간(viewTick = 시작 + 1, alpha = 0)에 0, 공격이 나가는 틱의 직전(viewTick = 끝, alpha = 1)에 1 이다.
        public float CastFill(uint viewTick, double alpha)
        {
            if (CastTicks <= 0)
            {
                return 0f;
            }
            double a = alpha < 0 ? 0 : (alpha > 1 ? 1 : alpha);
            double done = unchecked((int)(viewTick - CastStartTick)) - 1 + a;
            double fill = done / CastTicks;
            return fill < 0 ? 0f : (fill > 1 ? 1f : (float)fill);
        }

        public ShapeSample WithPos(Vector2 pos) =>
            new ShapeSample(Id, Kind, pos, RadiusCells, Dir, HpRatio, CastStartTick, CastTicks, Owner, Variant);
    }
}
