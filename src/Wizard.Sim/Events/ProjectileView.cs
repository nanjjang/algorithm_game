using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Spell;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3(T-54). 투사체 하나의 읽기 전용 값(플레이어 탄과 적 탄). 세계가 읽을 때마다 탄 상태에서 만들어 준다(뷰 훅 View_Projectiles_*).
    // 칸은 더하기만 한다.
    public readonly struct ProjectileView
    {
        public readonly EntityId Id;
        public readonly Faction Faction;                  // 누구의 탄인가. 위험색은 Enemy 에만 쓴다
        public readonly PlayerSlot Owner;                 // 플레이어 탄의 주인. 적 탄은 None
        public readonly BlockId Core;                     // 플레이어 탄의 코어 블록. 적 탄은 None
        public readonly Element Element;                  // 속성. 없으면 None
        public readonly FixVec2 Pos;
        public readonly FixVec2 Dir;                      // 나아가는 방향의 단위 벡터(전장 좌표: y 아래로 +). 없으면 0
        public readonly Fix64 Radius;                     // 판정 반지름(칸)
        public readonly int Group;                        // 묶음 id. 플레이어 탄 = J 발사의 ShotId(ShotFired·CritShotFired 의 것. 비기의 탄은 0),
                                                          // 적 탄 = 시전 묶음 CastId(GrazeAwarded 의 A, spec 01 8.2)

        public ProjectileView(EntityId id, Faction faction, PlayerSlot owner, BlockId core, Element element,
                              FixVec2 pos, FixVec2 dir, Fix64 radius, int group)
        {
            Id = id;
            Faction = faction;
            Owner = owner;
            Core = core;
            Element = element;
            Pos = pos;
            Dir = dir;
            Radius = radius;
            Group = group;
        }
    }
}
