using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Threats;

namespace Wizard.Solo.Tests.Threats
{
    /// <summary>남는 구역, 장애물, 별똥 자리 검사, 전부 걷기. 05 요약서 1.2절·4절·6.10절, 00 문서 D-63, 01 요약서 7.3절.</summary>
    public class ThreatHazardTests
    {
        static readonly Vector2 C = ThreatWorld.Center;
        ThreatWorld _t;

        [SetUp]
        public void SetUp() { _t = ThreatWorld.Create(C); }

        static HazardSpec Thorn(Vector2 pos, int durationTicks = 240, int maxCount = 0)
        {
            return new HazardSpec
            {
                Kind = HazardKind.ThornField, Shape = ZoneShape.Circle, Pos = pos, Radius = 1.5f, DurationTicks = durationTicks,
                BaseDamage = 1, ContactCooldownTicks = 90, MaxCount = maxCount
            };
        }

        // ------------------------------------------------------------ 접촉 규칙

        [Test]
        public void RemovedHazard_CannotHitAndExpiresOnlyOnce()
        {
            var h = _t.Threats.SpawnHazard(_t.W, Thorn(C));
            _t.Threats.RemoveHazard(_t.W, h);
            _t.Threats.RemoveHazard(_t.W, h);
            _t.Steps(2);
            Assert.AreEqual(0, _t.Player.Calls.Count, "A boss-extinguished hazard must stop judging damage.");
            int expired = 0;
            foreach (var e in _t.W.Events)
                if (e.Type == SimEventType.HazardExpired && e.EntityId == h.Id) expired++;
            Assert.AreEqual(1, expired, "Repeated cleanup must not replay the expiration effect.");
        }

        [Test]
        public void MovedHazard_JudgesAtItsNewPosition()
        {
            var h = _t.Threats.SpawnHazard(_t.W, Thorn(C + Vector2.right * 4f));
            _t.Steps(1);
            Assert.AreEqual(0, _t.Player.Calls.Count);
            _t.Threats.MoveHazard(_t.W, h, C);
            _t.Steps(1);
            Assert.AreEqual(1, _t.Player.Accepted.Count, "Drifting mist must judge at the moved location.");
            Assert.AreEqual(h.Id, _t.Player.Accepted[0].AttackId);
        }

        [Test]
        public void S05_TV_CON_05__StandingInThornField_HitsAt0_90_180()
        {
            // 가시밭 안에 240틱 서 있음 → 피해 틱 0, 90, 180(구역 id 쿨다운 1.5초)
            Assert.AreEqual(90, SimConst.Ticks(TestContent.Db.Const.ContactIntervalSeconds));
            var h = _t.Threats.SpawnHazard(_t.W, Thorn(C));
            _t.Steps(240);
            CollectionAssert.AreEqual(new[] { 0, 90, 180 }, _t.Player.AcceptedTicks);
            foreach (var hit in _t.Player.Accepted)
            {
                Assert.AreEqual(h.Id, hit.AttackId, "AttackId = 남는 구역의 EntityId");
                Assert.AreEqual(PlayerDamageSource.Lingering, hit.Source);
                Assert.AreEqual(1, hit.DamageCells);
            }
            Assert.AreEqual(3, _t.Player.Calls.Count, "쿨다운 중에는 후보를 올리지 않는다");
        }

        [Test]
        public void S05_Contact__DefaultCooldownComesFromData()
        {
            // 쿨다운을 비워 둔 구역은 접촉 간격 1.5초(90틱)
            var spec = Thorn(C);
            spec.ContactCooldownTicks = 0;
            var h = _t.Threats.SpawnHazard(_t.W, spec);
            Assert.AreEqual(90, h.ContactCooldownTicks);
            _t.Steps(100);
            CollectionAssert.AreEqual(new[] { 0, 90 }, _t.Player.AcceptedTicks);
        }

        [Test]
        public void Contract__InvulnerableContactDoesNotSpendCooldown()
        {
            // 무적 중(QueueHit = false)의 접촉은 쿨다운을 쓰지 않는다 → 무적이 끝난 바로 그 틱에 맞는다(D-63)
            var h = _t.Threats.SpawnHazard(_t.W, Thorn(C));
            _t.Player.Accept = false;
            _t.Steps(40);
            Assert.AreEqual(40, _t.Player.Calls.Count, "매 틱 후보를 올려 본다");
            Assert.AreEqual(0, _t.Player.AcceptedTicks.Count);
            Assert.AreEqual(0, h.ContactReadyTick, "쿨다운이 소비되지 않았다");

            _t.Player.Accept = true;
            _t.Steps(100);
            CollectionAssert.AreEqual(new[] { 40, 130 }, _t.Player.AcceptedTicks);
            Assert.AreEqual(220, h.ContactReadyTick);
        }

        [Test]
        public void S05_Contact__TwoOverlappingFieldsHaveSeparateCooldowns()
        {
            // 쿨다운 열쇠 = 구역 id(U-16): 겹친 가시밭 2개는 각각 후보를 올린다
            var a = _t.Threats.SpawnHazard(_t.W, Thorn(C + new Vector2(0.5f, 0)));
            var b = _t.Threats.SpawnHazard(_t.W, Thorn(C + new Vector2(-0.5f, 0)));
            _t.W.Step();
            Assert.AreEqual(2, _t.Player.Accepted.Count);
            Assert.AreEqual(a.Id, _t.Player.Accepted[0].AttackId);
            Assert.AreEqual(b.Id, _t.Player.Accepted[1].AttackId);
        }

        [Test]
        public void S05_Contact__OutsideFieldIsSafe()
        {
            // 반경 1.5 + 판정원 0.15: 1.66칸 밖은 안 맞고 1.64칸은 맞는다
            _t.Threats.SpawnHazard(_t.W, Thorn(C + new Vector2(1.66f, 0)));
            _t.Steps(5);
            Assert.AreEqual(0, _t.Player.Calls.Count);
            _t.Threats.SpawnHazard(_t.W, Thorn(C + new Vector2(-1.64f, 0)));
            _t.Steps(1);
            Assert.AreEqual(1, _t.Player.Calls.Count);
        }

        // ------------------------------------------------------------ 수명·개수

        [Test]
        public void S05_TV_SPC_37__FourthThornFieldRemovesOldest()
        {
            // 가시밭 전장 동시 3개: 4번째가 생기면 가장 오래된 것을 지운다
            var h1 = _t.Threats.SpawnHazard(_t.W, Thorn(new Vector2(3, 3), 240, 3));
            _t.W.Step();
            var h2 = _t.Threats.SpawnHazard(_t.W, Thorn(new Vector2(7, 3), 240, 3));
            _t.W.Step();
            var h3 = _t.Threats.SpawnHazard(_t.W, Thorn(new Vector2(3, 11), 240, 3));
            _t.W.Step();
            Assert.AreEqual(3, _t.W.Hazards.Count);
            var h4 = _t.Threats.SpawnHazard(_t.W, Thorn(new Vector2(7, 11), 240, 3));
            Assert.IsTrue(h1.Dead);
            Assert.IsFalse(h2.Dead || h3.Dead || h4.Dead);
            Assert.AreEqual(1, _t.CountEvents(SimEventType.HazardExpired, h1.Id));
            _t.W.Step();
            Assert.AreEqual(3, _t.W.Hazards.Count);
            Assert.AreEqual(4, _t.CountEvents(SimEventType.HazardSpawned));
        }

        [Test]
        public void S05_Hazard__ExpiresAfterDurationWithOneSecondWarning()
        {
            // 4초(240틱) 남고, 사라지기 1초 전부터 깜박인다
            var h = _t.Threats.SpawnHazard(_t.W, Thorn(new Vector2(3, 3)));
            Assert.AreEqual(240, h.UntilTick);
            _t.StepUntil(240);
            Assert.IsFalse(h.Dead);
            CollectionAssert.AreEqual(new[] { 180 }, _t.EventTicks(SimEventType.HazardExpiring, h.Id));
            _t.W.Step();   // 틱 240
            Assert.IsTrue(h.Dead);
            CollectionAssert.AreEqual(new[] { 240 }, _t.EventTicks(SimEventType.HazardExpired, h.Id));
            Assert.AreEqual(0, _t.W.Hazards.Count);
        }

        [Test]
        public void S06_Hazard__DurationZeroLastsUntilWaveEnd()
        {
            var h = _t.Threats.SpawnHazard(_t.W, Thorn(new Vector2(3, 3), 0));
            Assert.AreEqual(int.MaxValue, h.UntilTick);
            _t.Steps(2000);
            Assert.IsFalse(h.Dead);
            Assert.AreEqual(0, _t.CountEvents(SimEventType.HazardExpiring));
            _t.Threats.ClearAll(_t.W, keepObstacles: false);
            Assert.IsTrue(h.Dead);
        }

        // ------------------------------------------------------------ 서리밭

        [Test]
        public void S05_Frost__SlowsPlayerInsideAndRestoresOutside()
        {
            // 서리밭: 피해 없음, 안에서 이동 속도 −30%
            Assert.AreEqual(30f, TestContent.Db.Const.PlayerSlowOnFrostPercent, 1e-6f);
            _t.Threats.SpawnHazard(_t.W, new HazardSpec
            {
                Kind = HazardKind.FrostField, Shape = ZoneShape.Circle, Pos = C, Radius = 2.5f, DurationTicks = 480,
                SlowOnly = true, SlowPercent = 0.3f
            });
            _t.W.Step();
            Assert.AreEqual(0.7f, _t.P.EnvSpeedMul, 1e-5f);
            Assert.AreEqual(0, _t.Player.Calls.Count, "서리밭은 피해가 없다");

            _t.P.Pos = C + new Vector2(2.6f, 0);
            _t.W.Step();
            Assert.AreEqual(1f, _t.P.EnvSpeedMul, 1e-6f);

            _t.P.Pos = C;
            _t.StepUntil(481);
            Assert.AreEqual(1f, _t.P.EnvSpeedMul, 1e-6f, "서리밭이 사라지면 되돌아간다");
            Assert.AreEqual(0, _t.Player.Calls.Count);
        }

        [Test]
        public void S05_Frost__PercentFormsAndDefault()
        {
            // 30(%)으로 적어도, 비워 두어도(const.json 기본값) 같은 −30%
            var spec = new HazardSpec { Kind = HazardKind.FrostField, Shape = ZoneShape.Circle, Pos = C, Radius = 2.5f, SlowOnly = true, SlowPercent = 30f };
            _t.Threats.SpawnHazard(_t.W, spec);
            _t.W.Step();
            Assert.AreEqual(0.7f, _t.P.EnvSpeedMul, 1e-5f);

            var t2 = ThreatWorld.Create(C);
            spec.SlowPercent = 0f;
            t2.Threats.SpawnHazard(t2.W, spec);
            t2.W.Step();
            Assert.AreEqual(0.7f, t2.P.EnvSpeedMul, 1e-5f);
        }

        [Test]
        public void S05_Frost__LeftByZoneIsSlowOnly()
        {
            // 흰뿔 서리밭: leaves_hazard frost_field 반경 2.5, 8초(480틱)
            var def = new Wizard.Solo.Data.HazardDef { Kind = "frost_field", Radius = 2.5f, DurationSeconds = 8f, ContactCooldownSeconds = 1.5f };
            var spec = ThreatWorld.Circle(C, 2.5f, 60, 0);
            spec.LeavesHazard = def;
            var req = ThreatWorld.Request(spec);
            req.Bypass = true;
            _t.Threats.RequestTelegraph(_t.W, req);
            _t.StepUntil(61);
            Assert.AreEqual(1, _t.W.Hazards.Count);
            var h = _t.W.Hazards[0];
            Assert.AreEqual(HazardKind.FrostField, h.Kind);
            Assert.IsTrue(h.SlowOnly);
            Assert.AreEqual(60 + 480, h.UntilTick);
            Assert.AreEqual(0, _t.Player.Calls.Count);
            _t.W.Step();
            Assert.AreEqual(0.7f, _t.P.EnvSpeedMul, 1e-5f);
        }

        // ------------------------------------------------------------ 장애물

        [Test]
        public void S05_Obstacle__SegmentQueriesRespectBlockFlags()
        {
            var rock = _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec
            {
                Kind = "rock", Pos = new Vector2(10f, 5f), Radius = 0.5f, DurationTicks = 240, BlocksPlayerShots = true, BlocksEnemyShots = false
            });
            Assert.AreEqual(1, _t.CountEvents(SimEventType.ObstacleSpawned, rock.Id));
            Assert.IsTrue(_t.Threats.SegmentHitsObstacle(_t.W, new Vector2(8f, 5f), new Vector2(12f, 5f), true, out var hit));
            Assert.AreSame(rock, hit);
            Assert.IsFalse(_t.Threats.SegmentHitsObstacle(_t.W, new Vector2(8f, 5f), new Vector2(12f, 5f), false, out hit));
            Assert.IsNull(hit);
            Assert.IsFalse(_t.Threats.SegmentHitsObstacle(_t.W, new Vector2(8f, 5.6f), new Vector2(12f, 5.6f), true, out _), "0.6칸 옆으로 지나간다");
            Assert.IsFalse(_t.Threats.SegmentHitsObstacle(_t.W, new Vector2(8f, 5f), new Vector2(9f, 5f), true, out _), "닿기 전에 끝난다");
        }

        [Test]
        public void S05_Obstacle__NearestAlongTheSegmentWins()
        {
            var far = _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec { Kind = "rock", Pos = new Vector2(14f, 5f), Radius = 0.5f, BlocksPlayerShots = true });
            var near = _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec { Kind = "rock", Pos = new Vector2(10f, 5f), Radius = 0.5f, BlocksPlayerShots = true });
            Assert.IsTrue(_t.Threats.SegmentHitsObstacle(_t.W, new Vector2(8f, 5f), new Vector2(16f, 5f), true, out var hit));
            Assert.AreSame(near, hit);
            Assert.IsTrue(_t.Threats.SegmentHitsObstacle(_t.W, new Vector2(16f, 5f), new Vector2(8f, 5f), true, out hit));
            Assert.AreSame(far, hit);
        }

        [Test]
        public void S05_Obstacle__ExpiresAndBreaks()
        {
            // 바위: 4초(240틱), hp 0 = 플레이어 탄으로 안 깨진다
            var rock = _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec { Kind = "rock", Pos = new Vector2(10f, 5f), Radius = 0.5f, DurationTicks = 240, Hp = 0f });
            _t.Threats.DamageObstacle(_t.W, rock, 9999f);
            Assert.IsFalse(rock.Dead);
            _t.StepUntil(240);
            Assert.IsFalse(rock.Dead);
            _t.W.Step();
            Assert.IsTrue(rock.Dead);
            SimEvent ev = default;
            foreach (var e in _t.W.Events) if (e.Type == SimEventType.ObstacleDestroyed) ev = e;
            Assert.AreEqual(rock.Id, ev.EntityId);
            Assert.AreEqual(0, ev.A, "만료 = 0");
            Assert.AreEqual(0, _t.W.Obstacles.Count);

            // 체력이 있는 장애물(얼음 기둥)은 깎여서 깨진다. 지속 0 = 무한.
            var pillar = _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec { Kind = "ice_pillar", Pos = new Vector2(12f, 5f), Radius = 0.5f, DurationTicks = 0, Hp = 50f });
            Assert.AreEqual(int.MaxValue, pillar.UntilTick);
            _t.Threats.DamageObstacle(_t.W, pillar, 30f);
            Assert.IsFalse(pillar.Dead);
            _t.Threats.DamageObstacle(_t.W, pillar, 30f);
            Assert.IsTrue(pillar.Dead);
            foreach (var e in _t.W.Events) if (e.Type == SimEventType.ObstacleDestroyed && e.EntityId == pillar.Id) ev = e;
            Assert.AreEqual(1, ev.A, "부서짐 = 1");
            Assert.IsFalse(_t.Threats.SegmentHitsObstacle(_t.W, new Vector2(10f, 5f), new Vector2(14f, 5f), true, out _), "깨진 장애물은 막지 않는다");
        }

        // ------------------------------------------------------------ 별똥 자리(01 요약서 7.3절 hazard_clearance)

        [Test]
        public void S01_Starfall__IsClearKeepsDistanceFromEnemiesZonesAndHazards()
        {
            float clearance = TestContent.Db.Starfall.HazardClearance;
            Assert.AreEqual(2f, clearance, 1e-6f);
            Vector2 spot = new Vector2(6f, 6f);
            Assert.IsTrue(_t.Threats.IsClear(_t.W, spot, clearance));

            // 적: 몸 겉에서 2칸 이상(몸 반지름 0.4 → 중심 2.4칸)
            var enemy = _t.AddEnemy(spot + new Vector2(2.3f, 0));
            Assert.IsFalse(_t.Threats.IsClear(_t.W, spot, clearance));
            enemy.Pos = spot + new Vector2(2.5f, 0);
            Assert.IsTrue(_t.Threats.IsClear(_t.W, spot, clearance));
            enemy.Pos = spot;
            enemy.PendingDeath = true;   // 죽어 가는 적은 세지 않는다
            Assert.IsTrue(_t.Threats.IsClear(_t.W, spot, clearance));

            // 남는 구역: 경계에서 2칸(반경 1.5 → 중심 3.5칸)
            var h = _t.Threats.SpawnHazard(_t.W, Thorn(spot + new Vector2(0, 3.4f)));
            Assert.IsFalse(_t.Threats.IsClear(_t.W, spot, clearance));
            h.Pos = spot + new Vector2(0, 3.6f);
            Assert.IsTrue(_t.Threats.IsClear(_t.W, spot, clearance));

            // 예고 구역
            var req = ThreatWorld.Request(ThreatWorld.Circle(spot + new Vector2(-2.9f, 0), 1.0f, 60));
            req.Bypass = true;
            _t.Threats.RequestTelegraph(_t.W, req);
            _t.Threats.Stage8_Queue(_t.W);
            Assert.IsFalse(_t.Threats.IsClear(_t.W, spot, clearance));
            _t.W.Zones[0].Pos = spot + new Vector2(-3.1f, 0);
            Assert.IsTrue(_t.Threats.IsClear(_t.W, spot, clearance));
        }

        // ------------------------------------------------------------ 전부 걷기

        [Test]
        public void S05_ClearAll__RemovesZonesHazardsShotsAndOptionallyObstacles()
        {
            int active = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(C, 2.0f, 60)));
            _t.W.Wave.TelegraphCap = 1;
            int waiting = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(new Vector2(3, 3), 1.0f, 60)));
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, waiting));
            _t.Threats.SpawnHazard(_t.W, new HazardSpec { Kind = HazardKind.FrostField, Shape = ZoneShape.Circle, Pos = C, Radius = 2.5f, SlowOnly = true, SlowPercent = 0.3f });
            _t.Threats.SpawnHazard(_t.W, Thorn(new Vector2(20, 3)));
            _t.Threats.SpawnShot(_t.W, new EnemyShotSpec { Pos = new Vector2(20, 10), Dir = new Vector2(1, 0), Speed = 0f, Radius = 0.12f, BaseDamage = 1 });
            var rock = _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec { Kind = "rock", Pos = new Vector2(10f, 2f), Radius = 0.5f });
            _t.W.Step();
            Assert.AreEqual(0.7f, _t.P.EnvSpeedMul, 1e-5f);

            _t.Threats.ClearAll(_t.W, keepObstacles: true);
            Assert.AreEqual(TicketStatus.Cancelled, _t.Threats.Status(_t.W, active));
            Assert.AreEqual(TicketStatus.Cancelled, _t.Threats.Status(_t.W, waiting));
            Assert.AreEqual(1f, _t.P.EnvSpeedMul, 1e-6f);
            Assert.IsFalse(rock.Dead);
            _t.Steps(70);
            Assert.AreEqual(0, _t.W.Zones.Count);
            Assert.AreEqual(0, _t.W.Hazards.Count);
            Assert.AreEqual(0, _t.W.EnemyShots.Count);
            Assert.AreEqual(1, _t.W.Obstacles.Count);
            Assert.AreEqual(0, _t.Player.Calls.Count, "걷힌 예고는 터지지 않는다");

            _t.Threats.ClearAll(_t.W, keepObstacles: false);
            Assert.IsTrue(rock.Dead);
        }

        [Test]
        public void Contract__TwoWorldsDoNotShareState()
        {
            // 모듈 객체 하나를 두 세계가 써도 표가 섞이지 않는다(전역 상태 없음).
            var mods = SimModuleSet.Stubs();
            var threats = new ThreatSystem();
            mods.Threats = threats;
            var w1 = new SimWorld(TestContent.Db, mods, 1);
            var w2 = new SimWorld(TestContent.Db, mods, 2);
            var req = ThreatWorld.Request(ThreatWorld.Circle(new Vector2(3, 3), 1f, 30));
            int t1 = threats.RequestTelegraph(w1, req);
            Assert.AreEqual(TicketStatus.Waiting, threats.Status(w1, t1));
            Assert.AreEqual(TicketStatus.Unknown, threats.Status(w2, t1));
            w1.Step();
            w2.Step();
            Assert.AreEqual(1, w1.Zones.Count);
            Assert.AreEqual(0, w2.Zones.Count);
        }

        [Test]
        public void Contract__FactoryRegistersThreatSystem()
        {
            var mods = SimModuleSet.CreateDefault();
            Assert.IsInstanceOf<ThreatSystem>(mods.Threats);
        }
    }
}
