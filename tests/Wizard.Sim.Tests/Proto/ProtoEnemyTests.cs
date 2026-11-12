using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Proto;
using Wizard.Sim.Tests.Player;
using Xunit;

namespace Wizard.Sim.Tests.Proto
{
    // 슬라임 무한 생성과 3갈래 탄 포탑(spec 01 13절 7번). 기대값의 출처: 슬라임 체력 20·속도 3.0·접촉 1.5초에 1회(spec 05 2.5, 4.1, 3.6),
    // 약점 화염 ×1.25(spec 05 2.5, spec 02 AF-1), 포탑 = 술사의 3갈래 탄(시전 0.4초, 빈틈 0.6초, 20도, 속도 6.5, spec 05 4.4),
    // 피격 후 무적 1초(기준값 캐릭터), 시작 위치 (12, 7)(data/sim/arena.json).
    public sealed class ProtoEnemyTests
    {
        private const int FacingSouthCenti = 27000;
        private const int TurretSpreadCenti = 2000;

        [Fact]
        public void SlimesKeepComingEveryOneAndAHalfSecondsUpToTheCap()
        {
            var rig = ProtoRig.Practice();
            rig.RunTo(90 * 12 + 1);
            List<SimEvent> slimes = rig.Log.Of(SimEventType.EnemySpawn).FindAll(e => e.B == (int)ProtoEnemyKind.Slime);
            // 슬라임은 이 각본에서 죽지 않는다(아무도 쏘지 않는다). 상한 8마리까지 90틱마다
            Assert.Equal(new uint[] { 90, 180, 270, 360, 450, 540, 630, 720 }, slimes.ConvertAll(e => e.Tick).ToArray());
            Assert.All(slimes, e => Assert.Equal((int)EnemyTypeId.SlimeMoss, e.A));
            Assert.Equal(9, ProtoReadout.EnemyCount(rig.World));
            // 생성 자리는 전장 안
            for (int i = 0; i < ProtoReadout.EnemyCount(rig.World); i++)
            {
                FixVec2 p = ProtoReadout.Enemy(rig.World, i).Pos;
                Assert.True(p.X >= Fix64.Zero && p.X <= ProtoRig.Cells(24) && p.Y >= Fix64.Zero && p.Y <= ProtoRig.Cells(14));
            }
        }

        [Fact]
        public void SlimeSpawnPositionsFollowTheHeldSpawnStream()
        {
            var a = ProtoRig.Practice();
            var b = ProtoRig.Practice();
            a.RunTo(400);
            b.RunTo(400);
            List<SimEvent> sa = a.Log.Of(SimEventType.EnemySpawn), sb = b.Log.Of(SimEventType.EnemySpawn);
            Assert.Equal(sa.Count, sb.Count);
            for (int i = 0; i < sa.Count; i++)
            {
                Assert.Equal(sa[i].X, sb[i].X);
                Assert.Equal(sa[i].Y, sb[i].Y);
            }
            // 위치가 다 같지는 않다(난수가 실제로 쓰인다)
            Assert.Contains(sa, e => e.B == (int)ProtoEnemyKind.Slime && (e.X != sa[1].X || e.Y != sa[1].Y));
        }

        [Fact]
        public void SlimeChasesThePlayerInAStraightLine()
        {
            var rig = ProtoRig.Practice();
            rig.Step();
            EntityId id = rig.PlaceSlime(12000, 10000);
            rig.Step();
            ProtoEnemyView v = ProtoReadout.Enemy(rig.World, rig.SlimeIndex(id));
            // 플레이어(12, 7) 쪽 = 위(−y). 틱당 3.0/60 = 0.05칸
            Assert.Equal(ProtoRig.Cells(12), v.Pos.X);
            Assert.Equal(ProtoRig.Cells(10) - Fix64.FromRatio(300, 6000), v.Pos.Y);
        }

        [Fact]
        public void TwoMagicBoltsKillASlime()
        {
            var rig = ProtoRig.Practice();
            rig.Step();
            EntityId id = rig.PlaceSlime(12000, 10000);
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            rig.RunTo(40);
            List<SimEvent> hits = rig.Log.Of(SimEventType.HitEnemy).FindAll(e => e.Target == id);
            Assert.Equal(2, hits.Count);
            Assert.All(hits, e => Assert.Equal(Fix64.FromInt(10).Raw, e.V));       // 마력탄 10, 비전은 슬라임에게 ×1
            Assert.All(hits, e => Assert.Equal(1000, e.B));
            SimEvent death = rig.Log.Single(SimEventType.EnemyDeath);
            Assert.Equal(id, death.Source);
            Assert.Equal(hits[1].Tick, death.Tick);                                // 피해 확정과 사망은 같은 틱 10단계
            Assert.Equal(-1, rig.SlimeIndex(id));
        }

        [Fact]
        public void FireballKillsASlimeInOneHitThroughItsWeakness()
        {
            var rig = ProtoRig.Practice();
            rig.Step();
            EntityId id = rig.PlaceSlime(12000, 10000);
            rig.SetSlot(0, BlockId.Fireball);
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            rig.RunTo(30);
            SimEvent hit = rig.Log.Single(SimEventType.HitEnemy);
            Assert.Equal(id, hit.Target);
            Assert.Equal(1250, hit.B);
            Assert.Equal(Fix64.FromInt(50).Raw, hit.V);                             // 40 × 1.25
            Assert.Equal(hit.Tick, rig.Log.Single(SimEventType.HitWeakness).Tick);
            Assert.Equal(hit.Tick, rig.Log.Single(SimEventType.EnemyDeath).Tick);
        }

        [Fact]
        public void TurretCastsForTwentyFourTicksOfTelegraphThenFiresThreeShots()
        {
            var rig = ProtoRig.Practice();
            rig.RunTo(130);
            List<SimEvent> tint = rig.Log.Of(SimEventType.EnemyWeaponTintOn);
            // 첫 시전은 0틱(플레이어가 9칸 안), 다음 시전은 2.0초 뒤
            Assert.Equal(new uint[] { 0, 120 }, tint.ConvertAll(e => e.Tick).ToArray());
            Assert.Equal(24, tint[0].A);                                           // 시전 0.4초 = 24틱 뒤 발사
            List<SimEvent> fired = rig.Log.Of(SimEventType.EnemyProjectileFire);
            Assert.Equal(3, fired.Count);
            Assert.All(fired, e => Assert.Equal(24U, e.Tick));
            Assert.All(fired, e => Assert.Equal(1, e.A));                         // 한 시전 묶음
            // 가운데는 플레이어 쪽(포탑 (12, 2) → 플레이어 (12, 7) = 아래), 좌우 ±20도
            Assert.Equal(new[] { FacingSouthCenti - TurretSpreadCenti, FacingSouthCenti, FacingSouthCenti + TurretSpreadCenti },
                         fired.ConvertAll(e => e.C).ToArray());
            Assert.Equal(1, rig.Log.Count(SimEventType.EnemyWeaponTintOff));
        }

        [Fact]
        public void TurretShotHitsAStandingPlayer()
        {
            var rig = ProtoRig.Practice();
            rig.RunTo(100);
            List<SimEvent> fired = rig.Log.Of(SimEventType.EnemyProjectileFire);
            EntityId center = fired[1].Source;
            SimEvent hit = rig.Log.Single(SimEventType.PlayerHit);
            Assert.Equal(center, hit.Source);
            Assert.Equal(100, hit.A);                                              // 1칸 = 100점
            Assert.Equal(500, hit.B);
            SimEvent gone = rig.Log.Of(SimEventType.EnemyProjectileDespawn).Find(e => e.Source == center);
            Assert.Equal(1, gone.B);
            Assert.Equal(hit.Tick, gone.Tick);
            // 거리 5 − (0.12 + 0.15) = 4.73칸을 6.5/60 칸/틱으로: 44번째 이동(발사 틱 24 의 9단계가 첫 이동)
            Assert.Equal(24U + 43U, hit.Tick);
        }

        [Fact]
        public void ShotPassingJustBesideThePlayerIsGrazedNotHit()
        {
            var rig = ProtoRig.Practice();
            rig.RunTo(30);                                                         // 첫 발사(24틱)는 (12, 7)을 겨눴다
            rig.Step(PlayerRig.Press(LogicalKey.Right));
            rig.RunTo(36);
            rig.Step(PlayerRig.Release(LogicalKey.Right));                         // 6틱 × 5.0/60 = 0.5칸 오른쪽
            Assert.True(Fix64.Abs(rig.P.Pos.X - (ProtoRig.Cells(12) + ProtoRig.Milli(500))) < ProtoRig.Milli(1));
            rig.RunTo(100);
            Assert.Equal(0, rig.Log.Count(SimEventType.PlayerHit));
            SimEvent graze = rig.Log.Single(SimEventType.GrazeAwarded);
            Assert.Equal(1, graze.A);                                              // 시전 묶음 1
            Assert.Equal(rig.Log.Of(SimEventType.EnemyProjectileFire)[1].Source, graze.Source);
            Assert.True(rig.World.GrazeCastRecorded(0, 1));                         // 묶음의 탄이 아직 날고 있다
            // 사거리 9칸 ÷ 6.5/60 = 83.1 → 84번째 이동(틱 107)에 마지막 탄이 사라지고, 그 묶음은 스침 기록에서 지워진다
            rig.RunTo(107);
            Assert.True(rig.World.GrazeCastRecorded(0, 1));
            rig.Step();
            Assert.Equal(0, ProtoReadout.ShotCount(rig.World));
            Assert.False(rig.World.GrazeCastRecorded(0, 1));
        }

        [Fact]
        public void SlimeContactHitsOncePerCooldownAndInvulnerabilityDoesNotSpendIt()
        {
            var rig = ProtoRig.Practice();
            rig.Step();
            EntityId id = rig.PlaceSlime(12000, 7000);                              // 플레이어 자리에 겹쳐 둔다
            var invulnAt = new Dictionary<uint, bool>();
            while (rig.Tick < 400)
            {
                invulnAt[rig.Tick] = rig.World.PlayerInvulnerable(0);
                rig.Step();
            }
            List<SimEvent> slimeHits = rig.Log.Of(SimEventType.PlayerHit).FindAll(e => e.Source == id);
            Assert.True(slimeHits.Count >= 2);
            Assert.Equal(1U, slimeHits[0].Tick);
            for (int k = 1; k < slimeHits.Count; k++)
            {
                // 다음 접촉 = 쿨다운(90틱)이 끝난 뒤 처음으로 무적이 아닌 틱(spec 05 3.6, U-15)
                uint t = slimeHits[k - 1].Tick + 90U;
                while (invulnAt[t])
                {
                    t++;
                }
                Assert.Equal(t, slimeHits[k].Tick);
            }
        }
    }
}
