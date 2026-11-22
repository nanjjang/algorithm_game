using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Threats;

namespace Wizard.Solo.Tests.Threats
{
    /// <summary>적 탄과 스침 후보. 05 요약서 0절(탄 상한 150)·5절(병풍·포물선), 02 요약서 7.1절(T-G12~19), 06 요약서 1.4절(느린 탄막).</summary>
    public class ThreatShotTests
    {
        static readonly Vector2 C = ThreatWorld.Center;
        ThreatWorld _t;
        readonly List<GrazeCandidate> _graze = new List<GrazeCandidate>();

        [SetUp]
        public void SetUp()
        {
            _t = ThreatWorld.Create(C);
            _graze.Clear();
        }

        static EnemyShotSpec Shot(Vector2 pos, Vector2 dir, float speed, float radius = 0.12f, int castId = 1)
        {
            return new EnemyShotSpec
            {
                OwnerEnemyId = 0, CastId = castId, Pos = pos, Dir = dir, Speed = speed, Radius = radius, Range = -1f, BaseDamage = 1, Grazeable = true
            };
        }

        List<GrazeCandidate> Graze(ThreatWorld t)
        {
            _graze.Clear();
            t.Threats.CollectGraze(t.W, t.P, _graze);
            return _graze;
        }

        // ------------------------------------------------------------ 상한·이동

        [Test]
        public void S05_ShotCap__150OnScreenThen151stIsNotCreated()
        {
            Assert.AreEqual(150, TestContent.Db.Const.EnemyShotCap);
            for (int i = 0; i < 150; i++)
                Assert.IsNotNull(_t.Threats.SpawnShot(_t.W, Shot(new Vector2(1f + (i % 20), 1f), new Vector2(0, 1), 0f)), "탄 " + i);
            Assert.IsNull(_t.Threats.SpawnShot(_t.W, Shot(new Vector2(2f, 2f), new Vector2(0, 1), 0f)));
            Assert.AreEqual(150, _t.W.EnemyShots.Count);

            // 하나가 사라지면 다시 만들 수 있다.
            Assert.Greater(_t.Threats.ClearShots(_t.W, new Vector2(1f, 1f), 0.01f), 0);
            Assert.IsNotNull(_t.Threats.SpawnShot(_t.W, Shot(new Vector2(2f, 2f), new Vector2(0, 1), 0f)));
        }

        [Test]
        public void S05_Shot__MovesAtSpeedPerTickAndDiesAtWall()
        {
            // 술사 탄 속도 6.5칸/초 = 틱당 6.5/60칸
            var s = _t.Threats.SpawnShot(_t.W, Shot(new Vector2(20f, 2f), new Vector2(1, 0), 6.5f));
            Assert.AreEqual(1, _t.CountEvents(SimEventType.EnemyShotFired, s.Id));
            _t.Steps(6);
            Assert.AreEqual(20f + 6.5f * 6f / 60f, s.Pos.x, 1e-4f);
            Assert.AreEqual(2f, s.Pos.y, 1e-5f);
            Assert.IsFalse(s.Dead);
            _t.Steps(40);   // 벽(24)까지 4칸 = 37틱
            Assert.IsTrue(s.Dead);
            Assert.AreEqual(0, _t.W.EnemyShots.Count);
            Assert.AreEqual(0, _t.Player.Calls.Count);
        }

        [Test]
        public void S05_Shot__RangeLimitsTravel()
        {
            // 술사 탄 사거리 9칸: 속도 6 → 90틱에 사라진다
            var spec = Shot(new Vector2(2f, 2f), new Vector2(1, 0), 6f);
            spec.Range = 9f;
            var s = _t.Threats.SpawnShot(_t.W, spec);
            _t.Steps(89);
            Assert.IsFalse(s.Dead);
            Assert.AreEqual(2f + 8.9f, s.Pos.x, 1e-3f);
            _t.Steps(2);
            Assert.IsTrue(s.Dead);
            Assert.LessOrEqual(s.Pos.x, 11.001f);
        }

        [Test]
        public void S06_Whim__SlowBarrageScalesShotSpeed()
        {
            float mul = TestContent.Db.Modifier(Ids.Whim.SlowBarrage).EnemyShots.SpeedMultiplier;
            Assert.AreEqual(0.8f, mul, 1e-6f);
            _t.W.Wave.EnemyShotSpeedMul = mul;
            var s = _t.Threats.SpawnShot(_t.W, Shot(new Vector2(2f, 2f), new Vector2(1, 0), 5f));
            Assert.AreEqual(4f, s.Vel.magnitude, 1e-4f);
        }

        [Test]
        public void S05_Shot__BounceReflectsOffWall()
        {
            var spec = Shot(new Vector2(23.9f, 2f), new Vector2(1, 0), 6f);
            spec.Bounces = 1;
            var s = _t.Threats.SpawnShot(_t.W, spec);
            _t.Steps(3);
            Assert.IsFalse(s.Dead);
            Assert.Less(s.Vel.x, 0f);
            Assert.AreEqual(0, s.BouncesLeft);
            Assert.AreEqual(24f - 0.2f, s.Pos.x, 1e-3f);   // 0.3칸 가서 0.1칸에서 꺾였다
        }

        // ------------------------------------------------------------ 플레이어 충돌

        [Test]
        public void S05_Shot__HitQueuesPlayerHitAndConsumesShot()
        {
            var s = _t.Threats.SpawnShot(_t.W, Shot(C + new Vector2(-1f, 0), new Vector2(1, 0), 6f, 0.12f, 9));
            _t.Steps(20);
            Assert.AreEqual(1, _t.Player.Calls.Count);
            var hit = _t.Player.Calls[0];
            Assert.AreEqual(s.Id, hit.AttackId);
            Assert.AreEqual(9, hit.CastId);
            Assert.AreEqual(1, hit.DamageCells);
            Assert.AreEqual(PlayerDamageSource.Projectile, hit.Source);
            Assert.IsTrue(s.Dead);
            // 1 − (0.12 + 0.15) = 0.73칸을 틱당 0.1칸으로: 8번째 걸음(틱 7)에 처음 겹친다
            Assert.AreEqual(7, _t.Player.CallTicks[0]);
        }

        [Test]
        public void S05_Shot__PassesThroughInvulnerablePlayer()
        {
            _t.Player.Accept = false;   // 무적: QueueHit = false
            var s = _t.Threats.SpawnShot(_t.W, Shot(C + new Vector2(-1f, 0), new Vector2(1, 0), 6f));
            _t.Steps(30);
            Assert.IsFalse(s.Dead, "무적 중인 사람은 탄이 지나간다");
            Assert.Greater(s.Pos.x, C.x);
            Assert.Greater(_t.Player.Calls.Count, 0);
        }

        // ------------------------------------------------------------ 병풍·포물선·장애물

        [Test]
        public void S05_Shot__StraightShotIsBlockedByIceScreen_NotAGrazeCandidate()
        {
            _t.Arts.HasScreen = true;
            _t.Arts.ScreenX = C.x - 0.5f;
            var s = _t.Threats.SpawnShot(_t.W, Shot(C + new Vector2(-1f, 0.2f), new Vector2(1, 0), 6f));
            // 병풍 앞(0.5칸)까지 5틱. 그 전 틱에 병풍이 없다면 스침 거리(겉면 0.6 안)까지 왔을 탄이다.
            int steps = 0;
            while (!s.Blocked && steps < 10) { _t.Threats.Stage9_Shots(_t.W); steps++; }
            Assert.IsTrue(s.Blocked);
            Assert.IsTrue(s.Dead);
            Assert.AreEqual(1, _t.CountEvents(SimEventType.EnemyShotBlocked, s.Id));
            Assert.Less(s.Pos.x, _t.Arts.ScreenX + 1e-4f);
            Assert.AreEqual(0, Graze(_t).Count, "막힌 탄은 스침 후보가 아니다");
            Assert.AreEqual(0, _t.Player.Calls.Count);
        }

        [Test]
        public void S05_Shot__LobFliesOverScreenAndJudgesOnlyOnLanding()
        {
            _t.Arts.HasScreen = true;
            _t.Arts.ScreenX = C.x - 2f;
            var s = _t.Threats.SpawnShot(_t.W, new EnemyShotSpec
            {
                CastId = 4, Pos = C + new Vector2(-6f, 0), Radius = 0.3f, BaseDamage = 1, Grazeable = true,
                Lob = true, LobTarget = C, LobFlightTicks = 60
            });
            Assert.AreEqual(60, s.LandTick);
            // 날아가는 동안: 병풍을 넘고, 플레이어 위를 지나도 판정이 없고, 스침 후보도 아니다.
            for (int i = 0; i < 60; i++)
            {
                Assert.AreEqual(0, Graze(_t).Count, "틱 " + i);
                _t.W.Step();
                Assert.IsFalse(s.Blocked);
                Assert.AreEqual(0, _t.Player.Calls.Count, "틱 " + i);
            }
            Assert.IsFalse(s.Dead);
            _t.W.Step();   // 틱 60 = 착지
            Assert.IsTrue(s.Dead);
            Assert.AreEqual(C.x, s.Pos.x, 1e-3f);
            CollectionAssert.AreEqual(new[] { 60 }, _t.Player.CallTicks);
        }

        [Test]
        public void S05_Shot__ObstacleStopsEnemyShots()
        {
            _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec { Kind = "rock", Pos = C + new Vector2(-2f, 0), Radius = 0.5f, BlocksEnemyShots = true, BlocksPlayerShots = true });
            var s = _t.Threats.SpawnShot(_t.W, Shot(C + new Vector2(-4f, 0), new Vector2(1, 0), 6f));
            _t.Steps(40);
            Assert.IsTrue(s.Dead);
            Assert.AreEqual(0, _t.Player.Calls.Count);
            Assert.AreEqual(C.x - 2.5f, s.Pos.x, 1e-3f);

            // 적 탄을 막지 않는 장애물은 지나간다.
            var t2 = ThreatWorld.Create(C);
            t2.Threats.SpawnObstacle(t2.W, new ObstacleSpec { Kind = "rock", Pos = C + new Vector2(-2f, 0), Radius = 0.5f, BlocksEnemyShots = false, BlocksPlayerShots = true });
            t2.Threats.SpawnShot(t2.W, Shot(C + new Vector2(-4f, 0), new Vector2(1, 0), 6f));
            t2.Steps(45);
            Assert.AreEqual(1, t2.Player.Calls.Count);
        }

        [Test]
        public void S05_Shot__ClearShotsByRadiusAndAll()
        {
            _t.Threats.SpawnShot(_t.W, Shot(new Vector2(5f, 5f), new Vector2(1, 0), 0f));
            _t.Threats.SpawnShot(_t.W, Shot(new Vector2(6f, 5f), new Vector2(1, 0), 0f));
            _t.Threats.SpawnShot(_t.W, Shot(new Vector2(15f, 5f), new Vector2(1, 0), 0f));
            Assert.AreEqual(2, _t.Threats.ClearShots(_t.W, new Vector2(5f, 5f), 2f));
            Assert.AreEqual(1, _t.CountEvents(SimEventType.EnemyShotsCleared));
            Assert.AreEqual(1, _t.Threats.ClearShots(_t.W, Vector2.zero, 0f));
            Assert.AreEqual(0, _t.Threats.ClearShots(_t.W, Vector2.zero, 0f));
            _t.W.Step();
            Assert.AreEqual(0, _t.W.EnemyShots.Count);
        }

        // ------------------------------------------------------------ 스침 (02 요약서 7.1절 T-G12 ~ T-G19)

        /// <summary>가만히 있는 탄(반지름 0.12) 하나를 플레이어에게서 distance만큼 떨어뜨려 놓고 9단계의 탄 처리 뒤 후보를 모은다.</summary>
        List<GrazeCandidate> BulletAt(ThreatWorld t, float distance, out EnemyShotEntity shot)
        {
            shot = t.Threats.SpawnShot(t.W, Shot(t.P.Pos + new Vector2(distance, 0), new Vector2(1, 0), 0f, 0.12f, 21));
            t.Threats.Stage9_Shots(t.W);
            return Graze(t);
        }

        [Test]
        public void S01_T_G12__BulletAt070_IsGrazeCandidate()
        {
            var list = BulletAt(_t, 0.70f, out var shot);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(GrazeKind.Bullet, list[0].Kind);
            Assert.AreEqual(21, list[0].CastId);
            Assert.AreEqual(shot.Id, list[0].ThreatId);
            // 겉면까지 0.70 − 0.12 = 0.58: 피격 반지름 0.15 < 0.58 ≤ 감지 0.6
            Assert.AreEqual(0.58f, list[0].SurfaceDistance, 1e-4f);
            Assert.AreEqual(0, _t.Player.Calls.Count);
        }

        [Test]
        public void S01_T_G13__BulletAt073_NoCandidate()
        {
            // 0.73 − 0.12 = 0.61 > 0.6
            Assert.AreEqual(0, BulletAt(_t, 0.73f, out _).Count);
            Assert.AreEqual(0, _t.Player.Calls.Count);
        }

        [Test]
        public void S01_T_G14__Char2BulletAt085_IsGrazeCandidate()
        {
            // 제자 2의 감지 반지름 0.75: 0.85 − 0.12 = 0.73 ≤ 0.75
            var t = ThreatWorld.Create(C, Ids.Char.C2);
            Assert.AreEqual(0.75f, t.P.Def.GrazeRadius, 1e-6f);
            var list = BulletAt(t, 0.85f, out _);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(0.73f, list[0].SurfaceDistance, 1e-4f);
            // 같은 거리는 제자 1에게는 후보가 아니다
            Assert.AreEqual(0, BulletAt(_t, 0.85f, out _).Count);
        }

        [Test]
        public void S01_T_G15__BulletAt026_IsHitNotGraze()
        {
            // 0.26 < 0.12 + 0.15 → 피격 후보(스침 아님)
            var list = BulletAt(_t, 0.26f, out var shot);
            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(1, _t.Player.Calls.Count);
            Assert.AreEqual(shot.Id, _t.Player.Calls[0].AttackId);
            Assert.IsTrue(shot.Dead);
        }

        /// <summary>반지름 1.0 원 구역을 플레이어에게서 distance 떨어진 곳에 6틱 예고로 깔고, 판정 틱까지 돌린다.</summary>
        static ThreatWorld ZoneAt(float distance, string charId, out ZoneEntity zone, bool stopBeforeDetonation = false)
        {
            var t = ThreatWorld.Create(C, charId);
            var req = ThreatWorld.Request(ThreatWorld.Circle(C + new Vector2(distance, 0), 1.0f, 6));
            req.CastId = 33;
            req.Bypass = true;
            int ticket = t.Threats.RequestTelegraph(t.W, req);
            t.Threats.Stage8_Queue(t.W);   // 틱 0에 시작, 판정 틱 6
            var zones = new List<ZoneEntity>();
            t.Threats.GetZones(t.W, ticket, zones);
            zone = zones[0];
            int last = stopBeforeDetonation ? 5 : 6;
            for (int tick = 0; tick <= last; tick++)
            {
                t.W.Tick = tick;
                t.Threats.Stage9_Zones(t.W);
            }
            return t;
        }

        [Test]
        public void S01_T_G16__ZoneCenterAt15_IsGrazeCandidateOnDetonation()
        {
            var t = ZoneAt(1.5f, Ids.Char.C1, out var zone);
            Assert.IsTrue(zone.Detonated);
            var list = Graze(t);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(GrazeKind.Zone, list[0].Kind);
            Assert.AreEqual(33, list[0].CastId);
            Assert.AreEqual(zone.Id, list[0].ThreatId);
            // 경계까지 1.5 − 1.0 = 0.5
            Assert.AreEqual(0.5f, list[0].SurfaceDistance, 1e-4f);
            Assert.AreEqual(0, t.Player.Calls.Count, "밖에 있어 맞지 않았다");
        }

        [Test]
        public void S01_T_G17__ZoneCenterAt17_NoCandidate()
        {
            // 1.7 − 1.0 = 0.7 > 0.6
            var t = ZoneAt(1.7f, Ids.Char.C1, out var zone);
            Assert.IsTrue(zone.Detonated);
            Assert.AreEqual(0, Graze(t).Count);
        }

        [Test]
        public void S01_T_G18__Char2ZoneCenterAt17_IsGrazeCandidate()
        {
            // 제자 2는 구역도 0.75를 쓴다(D-09)
            var t = ZoneAt(1.7f, Ids.Char.C2, out _);
            var list = Graze(t);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(0.7f, list[0].SurfaceDistance, 1e-4f);
        }

        [Test]
        public void S01_T_G19__NoZoneCandidateBeforeDetonationTick()
        {
            var t = ZoneAt(1.5f, Ids.Char.C1, out var zone, stopBeforeDetonation: true);
            Assert.IsFalse(zone.Detonated);
            Assert.AreEqual(0, Graze(t).Count);
        }

        [Test]
        public void S01_Graze__ZoneThatHitThePlayerIsNotACandidate()
        {
            var t = ZoneAt(0.9f, Ids.Char.C1, out _);
            Assert.AreEqual(1, t.Player.Calls.Count);
            Assert.AreEqual(0, Graze(t).Count);
        }

        [Test]
        public void S01_Graze__UngrazeableThreatsAreSkipped()
        {
            var spec = Shot(C + new Vector2(0.70f, 0), new Vector2(1, 0), 0f);
            spec.Grazeable = false;
            _t.Threats.SpawnShot(_t.W, spec);
            _t.Threats.Stage9_Shots(_t.W);
            Assert.AreEqual(0, Graze(_t).Count);
        }

        [Test]
        public void S01_Graze__DashingEnemyBodyIsACandidate()
        {
            // 돌진 중인 적의 몸 원(반지름 0.40): 중심 거리 0.9 → 겉면 0.5
            var wolf = _t.AddEnemy(C + new Vector2(0.9f, 0));
            wolf.BodyRadius = 0.40f;
            Assert.AreEqual(0, Graze(_t).Count, "돌진 중이 아니면 후보가 아니다");
            wolf.DashActive = true;
            wolf.DashCastId = 44;
            var list = Graze(_t);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(GrazeKind.Dash, list[0].Kind);
            Assert.AreEqual(44, list[0].CastId);
            Assert.AreEqual(wolf.Id, list[0].ThreatId);
            Assert.AreEqual(0.5f, list[0].SurfaceDistance, 1e-4f);

            // 죽어 가는 적은 "없는 것"(공통 규칙 2)
            wolf.PendingDeath = true;
            Assert.AreEqual(0, Graze(_t).Count);
        }
    }
}
