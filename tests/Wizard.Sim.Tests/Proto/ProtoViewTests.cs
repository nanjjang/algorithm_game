using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Proto;
using Wizard.Sim.Spell;
using Wizard.Sim.Tests.Player;
using Xunit;

namespace Wizard.Sim.Tests.Proto
{
    // CONTRACTS 3.3 "뷰 훅"(T-54)의 시제품 쪽: 시제품의 적과 탄이 ISimView.Enemies·Projectiles 로 나온다(표시 계층은 이것만 읽는다).
    // 기대값의 출처: 포탑 자리 (12, 2)·몸 0.30, 시전 24틱 뒤 3발(spec 05 4.4 갓버섯 술사의 3갈래 탄, 탄 반지름 0.12),
    // 이끼 슬라임 체력 20·몸 0.35(spec 05 2.5, U-04), 마력탄의 속성·판정 반지름(팩의 blocks 절), 시작 위치 (12, 7)에서 시야는 아래.
    // 같은 풀을 ProtoReadout 으로 읽은 값과도 맞춘다. 읽기는 상태를 바꾸지 않는다.
    public sealed class ProtoViewTests
    {
        [Fact]
        public void BeforeTheFirstStep_ListsAreEmpty_AndReadingDoesNotStartThePrototype()
        {
            var rig = ProtoRig.Practice();
            ulong before = rig.World.ComputeStateHash();
            ISimView view = rig.World.View;
            Assert.Equal(0, view.Enemies.Length);
            Assert.Equal(0, view.Projectiles.Length);
            Assert.False(ProtoReadout.IsActive(rig.World));
            Assert.Equal(before, rig.World.ComputeStateHash());
        }

        [Fact]
        public void Turret_IsAShamanCapThatWindsUpForTwentyFourTicks()
        {
            var rig = ProtoRig.Practice();
            rig.Step();                                                       // 틱 0: 포탑이 서고 곧바로 시전을 시작한다
            ReadOnlySpan<EnemyView> enemies = rig.World.View.Enemies;
            Assert.Equal(1, enemies.Length);
            EnemyView t = enemies[0];
            Assert.Equal(EnemyTypeId.ShamanCap, t.Type);
            Assert.Equal(new FixVec2(ProtoRig.Cells(12), ProtoRig.Cells(2)), t.Pos);
            Assert.Equal(ProtoRig.Milli(300), t.BodyRadius);
            Assert.Equal(t.MaxHp, t.Hp);
            Assert.False(t.Id.IsNone);
            Assert.Equal((0U, 24), (t.WindupStartTick, t.WindupTicks));

            rig.RunTo(24);                                                    // 틱 23 까지 돌았다: 아직 시전 중
            t = rig.World.View.Enemies[0];
            Assert.Equal((0U, 24), (t.WindupStartTick, t.WindupTicks));
            Assert.Equal(0, rig.World.View.Projectiles.Length);

            rig.Step();                                                       // 틱 24: 발사, 빈틈으로
            t = rig.World.View.Enemies[0];
            Assert.Equal(0, t.WindupTicks);
            Assert.Equal(0U, t.WindupStartTick);
            Assert.Equal(3, rig.World.View.Projectiles.Length);

            rig.RunTo(121);                                                   // 다음 시전은 틱 120(2.0초 뒤)
            t = rig.World.View.Enemies[0];
            Assert.Equal((120U, 24), (t.WindupStartTick, t.WindupTicks));
        }

        [Fact]
        public void TurretShots_AreEnemyProjectilesOfOneCast()
        {
            var rig = ProtoRig.Practice();
            rig.RunTo(25);
            ReadOnlySpan<ProjectileView> shots = rig.World.View.Projectiles;
            Assert.Equal(3, shots.Length);
            for (int i = 0; i < shots.Length; i++)
            {
                Assert.Equal(Faction.Enemy, shots[i].Faction);
                Assert.Equal(PlayerSlot.None, shots[i].Owner);
                Assert.Equal(BlockId.None, shots[i].Core);
                Assert.Equal(Element.None, shots[i].Element);
                Assert.Equal(ProtoRig.Milli(120), shots[i].Radius);
                Assert.Equal(1, shots[i].Group);                              // 첫 시전 묶음(EnemyProjectileFire 의 A)
                Assert.True(i == 0 || shots[i].Id.Value > shots[i - 1].Id.Value);
                ProtoEnemyShotView r = ProtoReadout.Shot(rig.World, i);
                Assert.Equal((r.Id, r.Pos, r.Dir), (shots[i].Id, shots[i].Pos, shots[i].Dir));
            }
            Assert.True(shots[1].Dir.Y > Fix64.Zero);                         // 가운데 탄은 플레이어 쪽(아래 = +y)
            Assert.Equal(Fix64.Zero, shots[1].Dir.X);
        }

        [Fact]
        public void PlayerBullets_ArePlayerProjectilesWithTheirShotGroup()
        {
            var rig = ProtoRig.Practice();
            rig.Step(PlayerRig.Press(LogicalKey.Fire));                       // 틱 0: 마력탄 한 발(ShotId 1)
            ReadOnlySpan<ProjectileView> all = rig.World.View.Projectiles;
            Assert.Equal(1, all.Length);
            ProjectileView b = all[0];
            BlockDef bolt = ProtoRig.Db.Block(BlockId.MagicBolt);
            Assert.Equal(Faction.Player, b.Faction);
            Assert.Equal(ProtoRig.P0, b.Owner);
            Assert.Equal(BlockId.MagicBolt, b.Core);
            Assert.Equal(bolt.Element, b.Element);
            Assert.Equal(bolt.HitRadius, b.Radius);
            Assert.Equal(1, b.Group);
            Assert.Equal(rig.Log.Single(SimEventType.ShotFired).A, b.Group);  // ShotFired 의 shotId 와 같은 묶음
            Assert.Equal(new FixVec2(Fix64.Zero, Fix64.One), b.Dir);          // 시작 시야는 아래(전장 좌표 +y)
            ProtoBulletView r = ProtoReadout.Bullet(rig.World, 0);
            Assert.Equal((r.Id, r.Pos, r.Radius), (b.Id, b.Pos, b.Radius));

            rig.RunTo(25);                                                    // 9틱마다 한 발 + 틱 24 의 포탑 탄 3발
            all = rig.World.View.Projectiles;
            int players = 0;
            while (players < all.Length && all[players].Faction == Faction.Player)
            {
                players++;
            }
            Assert.Equal(ProtoReadout.BulletCount(rig.World), players);       // 플레이어 탄이 앞, 적 탄이 뒤(훅 순서)
            Assert.Equal(ProtoReadout.ShotCount(rig.World), all.Length - players);
            for (int i = players; i < all.Length; i++)
            {
                Assert.Equal(Faction.Enemy, all[i].Faction);
            }
        }

        [Fact]
        public void Slime_IsAMossSlime()
        {
            var rig = ProtoRig.Practice();
            rig.Step();
            EntityId id = rig.PlaceSlime(12000, 10000);
            ReadOnlySpan<EnemyView> enemies = rig.World.View.Enemies;
            Assert.Equal(2, enemies.Length);
            EnemyView s = enemies[1];                                         // 풀은 id 오름차순: 포탑, 슬라임
            Assert.Equal(id, s.Id);
            Assert.True(enemies[0].Id.Value < s.Id.Value);
            Assert.Equal(EnemyTypeId.SlimeMoss, s.Type);
            Assert.Equal(new FixVec2(ProtoRig.Cells(12), ProtoRig.Cells(10)), s.Pos);
            Assert.Equal(ProtoRig.Milli(350), s.BodyRadius);
            Assert.Equal((ProtoRig.Cells(20), ProtoRig.Cells(20)), (s.Hp, s.MaxHp));
            Assert.Equal(0, s.WindupTicks);

            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            while (rig.Log.Of(SimEventType.HitEnemy).FindAll(e => e.Target == id).Count == 0)
            {
                rig.Step();                                                   // 마력탄 한 발이 맞을 때까지(피해 10)
            }
            s = rig.World.View.Enemies[1];
            Assert.Equal(ProtoRig.Cells(10), s.Hp);
            Assert.Equal(ProtoRig.Cells(20), s.MaxHp);
        }

        [Fact]
        public void ListsAreRebuiltFromTheStateOnEveryRead()
        {
            var rig = ProtoRig.Practice();
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            rig.RunTo(30);
            SimSnapshot snap = rig.World.CreateSnapshotBuffer();
            rig.World.SaveSnapshot(snap);
            int enemiesThen = rig.World.View.Enemies.Length;
            int shotsThen = rig.World.View.Projectiles.Length;
            FixVec2 firstThen = rig.World.View.Projectiles[0].Pos;
            rig.RunTo(200);
            Assert.NotEqual(enemiesThen, rig.World.View.Enemies.Length);      // 슬라임이 생겼다(90틱마다)
            rig.World.LoadSnapshot(snap);                                     // 되감으면 뷰도 그때의 것이다(캐시 없음)
            Assert.Equal(enemiesThen, rig.World.View.Enemies.Length);
            Assert.Equal(shotsThen, rig.World.View.Projectiles.Length);
            Assert.Equal(firstThen, rig.World.View.Projectiles[0].Pos);
        }

        [Fact]
        public void OtherModes_HaveNoPrototypeInTheirViews()
        {
            var endless = PlayerRig.Endless();
            endless.Step(PlayerRig.Press(LogicalKey.Fire));
            endless.RunTo(200);
            Assert.Equal(0, endless.World.View.Enemies.Length);
            Assert.Equal(0, endless.World.View.Projectiles.Length);
        }

        [Fact]
        public void ReadingTheViewsEveryTick_DoesNotChangeTheHashes()
        {
            var read = ProtoRig.Practice();
            var plain = ProtoRig.Practice();
            int seenEnemies = 0, seenProjectiles = 0;
            for (uint t = 0; t < 600; t++)
            {
                Key[] keys = t == 0 ? new[] { PlayerRig.Press(LogicalKey.Fire) }
                    : t == 100 ? new[] { PlayerRig.Press(LogicalKey.Left) }
                    : t == 160 ? new[] { PlayerRig.Release(LogicalKey.Left), PlayerRig.Press(LogicalKey.Stance, 40) }
                    : t == 170 ? new[] { PlayerRig.Press(LogicalKey.Up), PlayerRig.Press(LogicalKey.Skill, 120) }
                    : t == 171 ? new[] { PlayerRig.Release(LogicalKey.Up), PlayerRig.Release(LogicalKey.Skill) }
                    : t == 300 ? new[] { PlayerRig.Release(LogicalKey.Stance), PlayerRig.Release(LogicalKey.Fire, 10) }
                    : Array.Empty<Key>();
                if (t == 200)
                {
                    read.SetSlot(1, BlockId.SplitThree);
                    plain.SetSlot(1, BlockId.SplitThree);
                }
                read.Step(keys);
                plain.Step(keys);
                ISimView view = read.World.View;
                view.Player(ProtoRig.P0);
                seenEnemies = Math.Max(seenEnemies, view.Enemies.Length);
                seenProjectiles = Math.Max(seenProjectiles, view.Projectiles.Length);
                Assert.Equal(plain.World.ComputeStateHash(), read.World.ComputeStateHash());
            }
            Assert.True(seenEnemies > 1);                                     // 각본이 슬라임과 탄을 실제로 지난다
            Assert.True(seenProjectiles > 3);
        }
    }
}
