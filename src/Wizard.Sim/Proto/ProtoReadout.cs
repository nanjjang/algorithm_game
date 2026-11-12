using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Proto
{
    // 시제품의 탄·적·조립을 표시 계층(회색 도형)과 측정 도구가 읽는 문. 세계 상태를 바꾸지 않는다.
    // ISimView 의 Enemies·Projectiles 를 채우는 훅이 계약에 아직 없어(SimWorldView, M1-SIM-02 결과 보고) 시제품은 이 문으로 내준다.
    // 이 폴더와 같이 M3-CB-02 에서 사라진다. 그때 표시 계층은 ISimView 로 옮긴다
    public static class ProtoReadout
    {
        // 이 세계에서 시제품이 도는가(연습 모드이고 첫 Step 뒤)
        public static bool IsActive(SimWorld world) => Require(world).ProtoOn && world.ProtoReady;

        public static int BulletCount(SimWorld world) => Active(world) ? world.ProtoBulletCount : 0;

        public static ProtoBulletView Bullet(SimWorld world, int index)
        {
            ref readonly ProtoBullet b = ref RequireActive(world).ProtoBulletAt(index);
            return new ProtoBulletView(b.Id, b.Owner, b.Block, b.Pos, b.Dir, b.Radius);
        }

        public static int EnemyCount(SimWorld world) => Active(world) ? world.ProtoEnemyCount : 0;

        public static ProtoEnemyView Enemy(SimWorld world, int index)
        {
            ref readonly ProtoEnemy e = ref RequireActive(world).ProtoEnemyAt(index);
            int bodyMilli = e.Kind == ProtoEnemyKind.Slime ? ProtoTuning.SlimeBodyMilli : ProtoTuning.TurretBodyMilli;
            bool casting = e.Kind == ProtoEnemyKind.Turret && e.Phase == ProtoTurretPhase.Cast;
            return new ProtoEnemyView(e.Id, e.Kind, e.Pos, Fix64.FromRatio(bodyMilli, ProtoTuning.MilliPerCell), e.Hp, e.MaxHp,
                                      casting, casting ? e.PhaseEndTick : 0U);
        }

        public static int ShotCount(SimWorld world) => Active(world) ? world.ProtoShotCount : 0;

        public static ProtoEnemyShotView Shot(SimWorld world, int index)
        {
            ref readonly ProtoEnemyShot s = ref RequireActive(world).ProtoShotAt(index);
            return new ProtoEnemyShotView(s.Id, s.CastId, s.Pos, s.Dir,
                                          Fix64.FromRatio(ProtoTuning.TurretShotRadiusMilli, ProtoTuning.MilliPerCell));
        }

        // 지금 조립: 코어 블록(마력탄·화염구)과 3갈래 여부
        public static BlockId Core(SimWorld world, PlayerSlot slot) => RequireActive(world).ProtoLoadoutOf(slot.Value).Core;

        public static bool SplitThree(SimWorld world, PlayerSlot slot) => RequireActive(world).ProtoLoadoutOf(slot.Value).SplitThree;

        private static bool Active(SimWorld world) => IsActive(world);

        private static SimWorld Require(SimWorld world) => world ?? throw new ArgumentNullException(nameof(world));

        private static SimWorld RequireActive(SimWorld world)
        {
            if (!IsActive(world))
            {
                throw new InvalidOperationException("the prototype content is not running in this world");
            }
            return world;
        }
    }
}
