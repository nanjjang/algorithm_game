using Wizard.Core;

namespace Wizard.Sim.Graze
{
    // spec 01 8.2~8.3. 9단계의 충돌 모듈(적 탄·돌진·예고 구역을 가진 쪽)이 플레이어마다 내놓는 스침 후보 하나.
    // 스침 모듈은 후보의 모양을 모른다: 탄과 돌진은 원(중심, 반지름)으로, 구역은 그 구역을 가진 쪽이 계산한
    // "플레이어 피격 원과 겹치는가"(zone.Contains(p, hitRadius))와 "경계까지의 거리"(zone.DistanceToBoundary(p.Pos))로 받는다.
    // 띠·부채꼴 같은 원이 아닌 구역도 같은 두 값으로 들어온다(8.3 마지막 줄). 원 구역은 CircleZone 이 두 값을 계산한다.
    // 값 형식이고 참조가 없다. 세계 상태와 같이 직렬화한다(Write/Read).
    internal struct GrazeThreat
    {
        public GrazeKind Kind;             // Bullet, Dash, Zone 만(Skill 은 후보가 아니다)
        public EntityId Id;                // 위협의 개체 id. 같은 틱의 후보는 이 순서로 판정한다(8.3 "id 순")
        public int CastId;                 // 시전 묶음(8.2). 1 이상
        public bool Grazeable;             // 적 공격 정의의 Grazeable 플래그(8.1 8번)
        public bool Blocked;               // 얼음 병풍이 막은 탄(8.1 9번, wc 4.4)
        public FixVec2 Pos;                // 탄·몸의 중심. 구역은 이벤트에 실을 위치(보통 중심)
        public Fix64 Radius;               // 탄·몸의 반지름. 구역은 쓰지 않는다
        public bool ZoneDetonatesNow;      // 구역: 이번 틱에 터지는가. 터지는 틱에만 스침이 된다(8.3, T-G19)
        public bool ZoneContainsPlayer;    // 구역: 플레이어의 피격 원과 겹친다(= 맞는다)
        public Fix64 ZoneEdgeDistance;     // 구역: 플레이어 판정 중심에서 구역 경계까지의 거리. 안쪽이면 음수여도 된다

        public static GrazeThreat Bullet(EntityId id, int castId, FixVec2 pos, Fix64 radius) =>
            new GrazeThreat { Kind = GrazeKind.Bullet, Id = id, CastId = castId, Grazeable = true, Pos = pos, Radius = radius };

        // 돌진 상태인 적의 몸 원. 판정은 탄과 같다(8.3)
        public static GrazeThreat Dash(EntityId id, int castId, FixVec2 bodyCenter, Fix64 bodyRadius) =>
            new GrazeThreat { Kind = GrazeKind.Dash, Id = id, CastId = castId, Grazeable = true, Pos = bodyCenter, Radius = bodyRadius };

        public static GrazeThreat Zone(EntityId id, int castId, FixVec2 pos, bool detonatesNow, bool containsPlayer, Fix64 edgeDistance) =>
            new GrazeThreat
            {
                Kind = GrazeKind.Zone,
                Id = id,
                CastId = castId,
                Grazeable = true,
                Pos = pos,
                ZoneDetonatesNow = detonatesNow,
                ZoneContainsPlayer = containsPlayer,
                ZoneEdgeDistance = edgeDistance,
            };

        // 원 구역(중심, 반지름). 플레이어마다 다르므로 플레이어의 판정 중심과 피격 반지름을 받는다
        public static GrazeThreat CircleZone(EntityId id, int castId, FixVec2 center, Fix64 radius, bool detonatesNow,
                                             FixVec2 playerPos, Fix64 playerHitRadius)
        {
            GrazeRules.CircleZone(center, radius, playerPos, playerHitRadius, out bool contains, out Fix64 edge);
            return Zone(id, castId, center, detonatesNow, contains, edge);
        }

        // 넣을 때와 상태를 읽을 때 같은 검사를 쓴다
        internal bool IsWellFormed =>
            Kind <= GrazeKind.Zone && Id.Value > 0 && CastId > 0 && Radius.Raw >= 0L;

        // 바이트: kind u8, id i32, cast i32, grazeable bool, blocked bool, pos (x i64, y i64), radius i64,
        //         detonates bool, contains bool, edge i64
        public void Write(ref StateWriter w)
        {
            w.U8((byte)Kind);
            w.I32(Id.Value);
            w.I32(CastId);
            w.Bool(Grazeable);
            w.Bool(Blocked);
            w.Vec(Pos);
            w.Fix(Radius);
            w.Bool(ZoneDetonatesNow);
            w.Bool(ZoneContainsPlayer);
            w.Fix(ZoneEdgeDistance);
        }

        public void Read(ref StateReader r)
        {
            Kind = (GrazeKind)r.U8();
            Id = new EntityId(r.I32());
            CastId = r.I32();
            Grazeable = r.Bool();
            Blocked = r.Bool();
            Pos = r.Vec();
            Radius = r.Fix();
            ZoneDetonatesNow = r.Bool();
            ZoneContainsPlayer = r.Bool();
            ZoneEdgeDistance = r.Fix();
            if (!IsWellFormed)
            {
                throw new SimInvariantException("graze: malformed threat in state");
            }
        }
    }
}
