using System;
using System.Numerics;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Spell;
using Wizard.ViewLogic.Convert;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // ISimView 의 적·투사체를 도형으로 옮기는 변환(CONTRACTS 3.3 EnemyView·ProjectileView, T-54)과 예고의 채움.
    // 기대값의 출처: 뷰의 값과 FixConvert(칸 → 표시 좌표, y 뒤집기), 손맛 시제품의 수치(포탑 (12, 2)·몸 0.30·시전 24틱·탄 0.12,
    // spec 05 4.4), 채움의 정의(CONTRACTS 3.3: 시작 틱 + 길이로만 센다).
    public sealed class EntityShapeSamplerTests
    {
        private static FixVec2 Cell(int x, int y) => new FixVec2(Fix64.FromInt(x), Fix64.FromInt(y));

        [Fact]
        public void Enemy_BecomesAShapeOfItsSpecies()
        {
            var slime = new EnemyView(new EntityId(5), EnemyTypeId.SlimeMoss, Cell(3, 4), Fix64.FromRatio(35, 100),
                                      Fix64.FromInt(5), Fix64.FromInt(20), 0U, 0);
            ShapeSample s = EntityShapeSampler.FromEnemy(in slime);
            Assert.Equal((5, ShapeKind.Slime), (s.Id, s.Kind));
            Assert.Equal(new Vector2(3f, -4f), s.Pos);                         // y 뒤집기
            Assert.Equal(0.35f, s.RadiusCells, 5);
            Assert.Equal(0.25f, s.HpRatio);
            Assert.False(s.Casting);
            Assert.Equal((int)EnemyTypeId.SlimeMoss, s.Variant);
            Assert.Equal((byte)255, s.Owner);

            var turret = new EnemyView(new EntityId(6), EnemyTypeId.ShamanCap, Cell(12, 2), Fix64.FromRatio(3, 10),
                                       Fix64.FromInt(40), Fix64.FromInt(40), 120U, 24);
            ShapeSample t = EntityShapeSampler.FromEnemy(in turret);
            Assert.Equal(ShapeKind.Turret, t.Kind);
            Assert.True(t.Casting);
            Assert.Equal((120U, 24, 144U), (t.CastStartTick, t.CastTicks, t.CastEndTick));
            Assert.Equal(1f, t.HpRatio);
        }

        [Fact]
        public void OtherSpecies_AreGenericEnemies_AndBrokenHpDoesNotDivideByZero()
        {
            Assert.Equal(ShapeKind.Enemy, EntityShapeSampler.KindOf(EnemyTypeId.WolfBramble));
            Assert.Equal(ShapeKind.Enemy, EntityShapeSampler.KindOf(EnemyTypeId.None));
            var e = new EnemyView(new EntityId(1), EnemyTypeId.GolemMenhir, Cell(0, 0), Fix64.One, Fix64.Zero, Fix64.Zero, 0U, 0);
            Assert.Equal(1f, EntityShapeSampler.FromEnemy(in e).HpRatio);
        }

        [Fact]
        public void Projectiles_KeepFactionOwnerCoreAndFlippedDirection()
        {
            var bolt = new ProjectileView(new EntityId(7), Faction.Player, new PlayerSlot(1), BlockId.Fireball, Element.Fire,
                                          Cell(12, 6), new FixVec2(Fix64.Zero, Fix64.One), Fix64.FromRatio(15, 100), 3);
            ShapeSample b = EntityShapeSampler.FromProjectile(in bolt);
            Assert.Equal((7, ShapeKind.PlayerBullet, (byte)1, (int)BlockId.Fireball), (b.Id, b.Kind, b.Owner, b.Variant));
            Assert.Equal(new Vector2(12f, -6f), b.Pos);
            Assert.Equal(new Vector2(0f, -1f), b.Dir);                         // 전장의 아래(+y)는 표시 좌표의 −y
            Assert.Equal(0.15f, b.RadiusCells, 5);
            Assert.False(b.Casting);

            var shot = new ProjectileView(new EntityId(8), Faction.Enemy, PlayerSlot.None, BlockId.None, Element.None,
                                          Cell(12, 3), new FixVec2(Fix64.One, Fix64.Zero), Fix64.FromRatio(12, 100), 1);
            ShapeSample e = EntityShapeSampler.FromProjectile(in shot);
            Assert.Equal((ShapeKind.EnemyShot, (byte)255, 0), (e.Kind, e.Owner, e.Variant));
            Assert.Equal(new Vector2(1f, 0f), e.Dir);
        }

        [Fact]
        public void CastFill_CountsOnlyFromTheStartTickAndLength()
        {
            // 시전이 틱 100 에 시작해 24틱(공격은 틱 124 에 나간다). 뷰의 틱은 "다음에 돌 틱"이고 그림은 한 틱 늦게 그린다
            var s = new ShapeSample(1, ShapeKind.Turret, Vector2.Zero, 0.3f, Vector2.Zero, 1f, 100U, 24, 255, 0);
            Assert.Equal(0f, s.CastFill(101U, 0.0));                           // 시작한 틱의 상태를 처음 본 순간
            Assert.Equal(0.5f / 24f, s.CastFill(101U, 0.5), 6);
            Assert.Equal(12f / 24f, s.CastFill(113U, 0.0), 6);
            Assert.Equal(1f, s.CastFill(124U, 1.0));                           // 공격이 나가는 틱의 직전
            Assert.Equal(1f, s.CastFill(130U, 0.0));                           // 넘치지 않는다
            Assert.Equal(0f, s.CastFill(100U, 0.0));                           // 모자라지도 않는다
            Assert.Equal(s.CastFill(110U, 1.0), s.CastFill(111U, 0.0), 6);     // 틱 경계에서 이어진다
            var idle = new ShapeSample(1, ShapeKind.Turret, Vector2.Zero, 0.3f, Vector2.Zero, 1f, 0U, 0, 255, 0);
            Assert.Equal(0f, idle.CastFill(50U, 0.5));
        }

        [Fact]
        public void CastFill_SurvivesTickWrap()
        {
            var s = new ShapeSample(1, ShapeKind.Turret, Vector2.Zero, 0.3f, Vector2.Zero, 1f, uint.MaxValue - 3U, 24, 255, 0);
            Assert.Equal(8U, unchecked(s.CastEndTick - 12U));                  // 끝 틱 = 20(넘침은 래핑)
            Assert.Equal(12f / 24f, s.CastFill(9U, 0.0), 6);                   // 시작에서 13틱 뒤의 뷰 = 12틱 지남
        }

        [Fact]
        public void RealPracticeWorld_ShowsTheTurretItsTelegraphAndTheShots()
        {
            ConvertRig rig = ConvertRig.Practice();
            var frame = new ShapeFrame(4, 64);
            rig.Step();                                                        // 틱 0: 포탑이 서고 시전을 시작한다
            frame.Reset(rig.World.View.Tick);
            EntityShapeSampler.Fill(rig.World.View, frame);
            Assert.Equal(1, frame.EntityCount);
            ShapeSample turret = frame.Entity(0);
            Assert.Equal(ShapeKind.Turret, turret.Kind);
            Assert.Equal(new Vector2(12f, -2f), turret.Pos);
            Assert.Equal(0.3f, turret.RadiusCells, 5);
            Assert.True(turret.Casting);
            Assert.Equal(24U, turret.CastEndTick);
            Assert.Equal(0f, turret.CastFill(rig.World.View.Tick, 0.0));

            while (rig.Tick < 24)
            {
                rig.Step();
            }
            frame.Reset(rig.World.View.Tick);
            EntityShapeSampler.Fill(rig.World.View, frame);
            Assert.Equal(1f, frame.Entity(0).CastFill(rig.World.View.Tick, 1.0));   // 발사 직전에 가득 찬다

            rig.Step();                                                        // 틱 24: 3발
            frame.Reset(rig.World.View.Tick);
            EntityShapeSampler.Fill(rig.World.View, frame);
            Assert.Equal(4, frame.EntityCount);
            Assert.False(frame.Entity(0).Casting);
            for (int i = 1; i < 4; i++)
            {
                ShapeSample shot = frame.Entity(i);
                Assert.Equal(ShapeKind.EnemyShot, shot.Kind);
                Assert.Equal(0.12f, shot.RadiusCells, 5);
                Assert.True(shot.Dir.Y < 0f);                                  // 플레이어(아래쪽)로: 표시 좌표의 −y
                Assert.Equal(1f, shot.Dir.Length(), 4);
            }
        }

        [Fact]
        public void RealPracticeWorld_ShowsPlayerBulletsAndSlimes()
        {
            ConvertRig rig = ConvertRig.Practice();
            var frame = new ShapeFrame(4, 64);
            rig.Step(ConvertRig.Press(LogicalKey.Fire));
            frame.Reset(rig.World.View.Tick);
            EntityShapeSampler.Fill(rig.World.View, frame);
            ShapeSample bullet = default;
            for (int i = 0; i < frame.EntityCount; i++)
            {
                if (frame.Entity(i).Kind == ShapeKind.PlayerBullet)
                {
                    bullet = frame.Entity(i);
                }
            }
            Assert.Equal(ShapeKind.PlayerBullet, bullet.Kind);
            Assert.Equal((byte)0, bullet.Owner);
            Assert.Equal((int)BlockId.MagicBolt, bullet.Variant);
            Assert.Equal(FixConvert.ToFloat(ConvertRig.Db.Block(BlockId.MagicBolt).HitRadius), bullet.RadiusCells);
            Assert.Equal(new Vector2(0f, -1f), bullet.Dir);                    // 시작 시야는 아래

            while (rig.Tick <= 90)
            {
                rig.Step();                                                    // 틱 90 에 첫 슬라임(1.5초마다)
            }
            frame.Reset(rig.World.View.Tick);
            EntityShapeSampler.Fill(rig.World.View, frame);
            int slimes = 0;
            for (int i = 0; i < frame.EntityCount; i++)
            {
                ShapeSample s = frame.Entity(i);
                if (s.Kind == ShapeKind.Slime)
                {
                    slimes++;
                    Assert.Equal(0.35f, s.RadiusCells, 5);
                    Assert.InRange(s.Pos.X, 0f, 24f);                          // 전장 안(표시 좌표: x 0~24, y 0~−14)
                    Assert.InRange(s.Pos.Y, -14f, 0f);
                }
            }
            Assert.Equal(1, slimes);
            Assert.Equal(0, frame.DroppedCount);
        }

        [Fact]
        public void Fill_RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => EntityShapeSampler.Fill(null!, new ShapeFrame(1, 1)));
            Assert.Throws<ArgumentNullException>(() => EntityShapeSampler.Fill(ConvertRig.Practice().World.View, null!));
        }
    }
}
