using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Threats;

namespace Wizard.Solo.Tests.Threats
{
    /// <summary>플레이어가 받는 피해 칸. 05 요약서 3.1절, Modules.cs 공통 규칙 6, 00 문서 D-62.</summary>
    public class ThreatDamageTests
    {
        ThreatWorld _t;
        EnemyState _normal, _elite, _boss;

        [SetUp]
        public void SetUp()
        {
            _t = ThreatWorld.Create(ThreatWorld.Center);
            _normal = _t.AddEnemy(new Vector2(2, 2));
            _elite = _t.AddEnemy(new Vector2(4, 2), elite: true);
            _boss = _t.AddEnemy(new Vector2(6, 2), boss: true);
        }

        int Cells(int wave, int baseDamage, PlayerDamageSource source, EnemyState owner)
        {
            _t.W.Wave.Wave = wave;
            return _t.Threats.DamageCells(_t.W, baseDamage, source, owner);
        }

        // 예고 공격: base = attack.damage, w ≥ 61 +1, w ≥ 31이고 엘리트·보스면 +1
        [TestCase(30, 1, 1)]
        [TestCase(31, 1, 1)]
        [TestCase(60, 1, 1)]
        [TestCase(61, 1, 2)]
        [TestCase(30, 2, 2)]
        [TestCase(31, 2, 2)]
        [TestCase(60, 2, 2)]
        [TestCase(61, 2, 3)]
        public void S05_DMG__TelegraphNormalEnemyGrowsOnlyFrom61(int wave, int baseDamage, int expected)
        {
            Assert.AreEqual(expected, Cells(wave, baseDamage, PlayerDamageSource.Telegraph, _normal));
        }

        [TestCase(30, 1, 1)]
        [TestCase(31, 1, 2)]
        [TestCase(60, 1, 2)]
        [TestCase(61, 1, 2)]
        [TestCase(30, 2, 2)]
        [TestCase(31, 2, 3)]
        [TestCase(60, 2, 3)]
        [TestCase(61, 2, 3)]
        public void S05_DMG__TelegraphEliteAndBossGrowFrom31(int wave, int baseDamage, int expected)
        {
            Assert.AreEqual(expected, Cells(wave, baseDamage, PlayerDamageSource.Telegraph, _elite));
            Assert.AreEqual(expected, Cells(wave, baseDamage, PlayerDamageSource.Telegraph, _boss));
        }

        [Test]
        public void S05_DMG__OwnerUnknownCountsAsNormal()
        {
            Assert.AreEqual(1, Cells(31, 1, PlayerDamageSource.Telegraph, null));
            Assert.AreEqual(2, Cells(61, 1, PlayerDamageSource.Telegraph, null));
        }

        // 접촉·탄·남는 구역은 성장이 없다(가마등 접촉 2는 종별 값)
        [TestCase(PlayerDamageSource.Contact)]
        [TestCase(PlayerDamageSource.Projectile)]
        [TestCase(PlayerDamageSource.Lingering)]
        public void S05_DMG__ContactProjectileLingeringDoNotGrow(PlayerDamageSource source)
        {
            Assert.AreEqual(1, Cells(1, 1, source, _normal));
            Assert.AreEqual(1, Cells(61, 1, source, _normal));
            Assert.AreEqual(1, Cells(61, 1, source, _elite));
            Assert.AreEqual(1, Cells(100, 1, source, _boss));
            Assert.AreEqual(2, Cells(70, 2, source, _boss));
        }

        // D-62: 돌진의 몸 충돌은 예고 공격과 같이 센다
        [TestCase(30, 1)]
        [TestCase(31, 1)]
        [TestCase(60, 2)]
        [TestCase(61, 2)]
        public void S05_DMG__DashEqualsTelegraph(int wave, int baseDamage)
        {
            foreach (var owner in new[] { _normal, _elite, _boss, null })
                Assert.AreEqual(Cells(wave, baseDamage, PlayerDamageSource.Telegraph, owner),
                    Cells(wave, baseDamage, PlayerDamageSource.Dash, owner));
            Assert.AreEqual(2, Cells(61, 1, PlayerDamageSource.Dash, _normal));
            Assert.AreEqual(2, Cells(31, 1, PlayerDamageSource.Dash, _elite));
            Assert.AreEqual(1, Cells(31, 1, PlayerDamageSource.Dash, _normal));
        }

        [Test]
        public void S05_DMG__FinalRageAddsOneToEveryClass()
        {
            _t.W.Boss = new BossState { EnemyId = _boss.Id, State = BossFightState.Waiting, FinalRage = true };
            Assert.AreEqual(2, Cells(10, 1, PlayerDamageSource.Contact, _boss));
            Assert.AreEqual(2, Cells(10, 1, PlayerDamageSource.Projectile, _boss));
            Assert.AreEqual(2, Cells(10, 1, PlayerDamageSource.Lingering, _boss));
            Assert.AreEqual(3, Cells(10, 2, PlayerDamageSource.Telegraph, _boss));
            Assert.AreEqual(3, Cells(10, 2, PlayerDamageSource.Dash, _boss));
            // 졸개의 공격도(보스 싸움 중의 모든 공격)
            Assert.AreEqual(2, Cells(10, 1, PlayerDamageSource.Contact, _normal));
        }

        [Test]
        public void S05_DMG__AttackCapIsThree()
        {
            // S6 T057: 흰뿔 휘두르기(2), w = 50 → 3. T058: 최종 격노까지 켜져도 공격 쪽 상한 3
            Assert.AreEqual(3, Cells(50, 2, PlayerDamageSource.Telegraph, _boss));
            _t.W.Boss = new BossState { EnemyId = _boss.Id, State = BossFightState.Execute, FinalRage = true };
            Assert.AreEqual(3, Cells(50, 2, PlayerDamageSource.Telegraph, _boss));
            Assert.AreEqual(3, Cells(61, 2, PlayerDamageSource.Dash, _normal));
            Assert.AreEqual(3, Cells(61, 3, PlayerDamageSource.Telegraph, _boss));
        }

        [Test]
        public void S05_DMG__NoFinalRageBonusWithoutLivingBoss()
        {
            // w.Boss가 null이면 없다
            _t.W.Boss = null;
            Assert.AreEqual(1, Cells(10, 1, PlayerDamageSource.Contact, _boss));
            // 격노가 꺼져 있으면 없다
            _t.W.Boss = new BossState { State = BossFightState.Waiting, FinalRage = false };
            Assert.AreEqual(1, Cells(10, 1, PlayerDamageSource.Projectile, _boss));
            // 보스가 죽은 뒤(State = Dead)에는 FinalRage가 남아 있어도 없다
            _t.W.Boss = new BossState { State = BossFightState.Dead, FinalRage = true };
            Assert.AreEqual(1, Cells(10, 1, PlayerDamageSource.Contact, _normal));
            Assert.AreEqual(2, Cells(10, 2, PlayerDamageSource.Telegraph, _boss));
        }

        [Test]
        public void S05_DMG__ScriptedIsBaseAsIs()
        {
            _t.W.Boss = new BossState { State = BossFightState.Reflection, FinalRage = true };
            Assert.AreEqual(8, Cells(61, 8, PlayerDamageSource.Scripted, _boss));
            Assert.AreEqual(1, Cells(61, 1, PlayerDamageSource.Scripted, null));
        }

        [Test]
        public void Contract__ShotDamageIsComputedWhenItHits_FinalRageTurnedOnAfterSpawn()
        {
            // 탄을 만든 뒤 최종 격노가 켜져도, 맞는 순간의 값(+1)이 들어간다(개체는 기본 칸만 든다).
            _t.W.Boss = new BossState { EnemyId = _boss.Id, State = BossFightState.Execute, FinalRage = false };
            var shot = _t.Threats.SpawnShot(_t.W, new EnemyShotSpec
            {
                OwnerEnemyId = _boss.Id, CastId = 5, Pos = _t.P.Pos + new Vector2(-3f, 0), Dir = new Vector2(1, 0),
                Speed = 6f, Radius = 0.18f, Range = -1f, BaseDamage = 1, Grazeable = true
            });
            Assert.AreEqual(1, shot.BaseDamage);
            _t.Steps(5);
            Assert.AreEqual(0, _t.Player.Calls.Count);
            _t.W.Boss.FinalRage = true;
            _t.Steps(40);
            Assert.AreEqual(1, _t.Player.Calls.Count);
            Assert.AreEqual(2, _t.Player.Calls[0].DamageCells);
            Assert.AreEqual(shot.Id, _t.Player.Calls[0].AttackId);
            Assert.AreEqual(PlayerDamageSource.Projectile, _t.Player.Calls[0].Source);
            Assert.AreEqual(1, shot.BaseDamage, "개체의 기본 칸은 바뀌지 않는다");
        }

        [Test]
        public void Contract__ZoneDamageIsComputedAtDetonation()
        {
            // 예고 중에 최종 격노가 켜지면 판정 틱의 값: 2 + 1 = 3
            _t.W.Wave.Wave = 10;
            _t.W.Boss = new BossState { EnemyId = _boss.Id, State = BossFightState.Telegraph, FinalRage = false };
            var req = ThreatWorld.Request(ThreatWorld.Circle(_t.P.Pos, 2.0f, 54, 2));
            req.OwnerEnemyId = _boss.Id;
            _t.RequestAtStage7(req);
            _t.Steps(20);
            _t.W.Boss.FinalRage = true;
            _t.Steps(40);
            Assert.AreEqual(1, _t.Player.Calls.Count);
            Assert.AreEqual(3, _t.Player.Calls[0].DamageCells);
            Assert.AreEqual(_boss.Id, _t.Player.Calls[0].SourceEnemyId);
        }

        [Test]
        public void Contract__EliteZoneAtWave31HitsForPlusOne()
        {
            _t.W.Wave.Wave = 31;
            var req = ThreatWorld.Request(ThreatWorld.Circle(_t.P.Pos, 1.8f, 54, 1));
            req.OwnerEnemyId = _elite.Id;
            _t.RequestAtStage7(req);
            _t.Steps(60);
            Assert.AreEqual(1, _t.Player.Calls.Count);
            Assert.AreEqual(2, _t.Player.Calls[0].DamageCells);
        }
    }
}
