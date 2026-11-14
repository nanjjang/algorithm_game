using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 통합 (가)~(다): 진짜 모듈 전부를 한 세계에 끼워 웨이브 1 전투를 돌린다. 기대값은 요약서에서 가져왔다
    /// (02 요약서 6·7절 과열·스침, 06 요약서 1절 웨이브). 가짜는 입력(봇)뿐이다.
    /// </summary>
    public class CombatIntegrationTests
    {
        IntegrationKit _k;

        [SetUp] public void SetUp() { _k = new IntegrationKit(); }
        [TearDown] public void TearDown() { _k.Dispose(); }

        /// <summary>전투가 시작될 때까지(WaveCombat) 걸린 틱 수만큼 진행한다. 못 가면 실패.</summary>
        void ToCombat(int maxTicks = 1200)
        {
            Assert.IsTrue(_k.AutoPlay(maxTicks, w => w.Run.Phase == RunPhase.WaveCombat, god: false),
                "WaveCombat에 못 들어갔다(Phase = " + _k.W.Run.Phase + ")");
        }

        // ------------------------------------------------------------ (가)

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Integ_A__EachCharacterWave1Runs600Ticks_EnemiesSpawn_ShotsDealDamage(int character)
        {
            _k.StartRun(character, seed: (ulong)(100 + character));
            Assert.AreEqual(Ids.Char.Of(character), _k.P.CharId);

            // 전투 시작부터 600틱. 봇이 가까운 적 쪽으로 걸으며 J를 누른다. 예외 없이 돌고, 적이 나오고, 피해가 들어가야 한다.
            ToCombat();
            int start = _k.W.Tick;
            int maxAlive = 0;
            for (int t = 0; t < 600; t++)
            {
                if (_k.W.Run.Phase == RunPhase.RunEnded) break;
                _k.CombatBot();
                _k.Step(god: true);
                int alive = 0;
                foreach (var e in _k.W.Enemies) if (e.Alive) alive++;
                if (alive > maxAlive) maxAlive = alive;
            }
            TestContext.WriteLine("char." + character + ": 적 " + _k.Count(SimEventType.EnemySpawned) + "마리, 발사 " + _k.Count(SimEventType.ShotFired)
                + ", 적중 " + _k.Count(SimEventType.EnemyDamaged) + ", 처치 " + _k.W.Run.TotalKills + ", 최대 동시 " + maxAlive);
            Assert.Greater(_k.Count(SimEventType.EnemySpawned), 0, "적이 나와야 한다");
            Assert.Greater(maxAlive, 0);
            Assert.Greater(_k.Count(SimEventType.ShotFired), 0, "J를 누르고 있었으니 발사가 있어야 한다");
            Assert.Greater(_k.Count(SimEventType.ProjectileSpawned), 0);
            var hits = _k.Events(SimEventType.EnemyDamaged);
            Assert.Greater(hits.Count, 0, "탄이 맞아 피해가 들어가야 한다");
            foreach (var h in hits) Assert.Greater(h.A, 0, "피해량은 양수");
            // 피해를 받은 적은 체력이 줄었거나 이미 죽었다.
            Assert.Greater(_k.W.Tick - start, 0);
            Assert.GreaterOrEqual(_k.Count(SimEventType.WaveStarted), 1);
        }

        [Test]
        public void Integ_A__SameSeedSameInputGivesSameStateHash()
        {
            string Run()
            {
                using (var k = new IntegrationKit())
                {
                    k.StartRun(1, seed: 777UL);
                    k.AutoPlay(900, w => w.Tick >= 900, god: true);
                    // 판의 모양을 줄여 적은 문자열(위치는 소수 둘째 자리까지).
                    var sb = new System.Text.StringBuilder();
                    sb.Append(k.W.Tick).Append('|').Append(k.W.Run.Phase).Append('|').Append(k.W.Run.TotalKills).Append('|').Append(k.W.Run.Score)
                      .Append('|').Append(k.P.HeatHu).Append('|').Append(k.P.Combo).Append('|').Append(k.W.Enemies.Count);
                    foreach (var e in k.W.Enemies) sb.Append('|').Append(e.DefId).Append(e.Hp.ToString("F2"));
                    return sb.ToString();
                }
            }
            Assert.AreEqual(Run(), Run(), "같은 씨앗·같은 입력이면 같은 결과(결정론)");
        }

        // ------------------------------------------------------------ (나) 과열: 임계 → 잠금 → 냉각

        [Test]
        public void Integ_B__Heat_CritThenLockThenCool()
        {
            _k.StartRun(1, seed: 9UL);
            ToCombat();
            var p = _k.P;
            int critStart = p.Derived.CritStartHu;
            Assert.Greater(critStart, 0);

            // J를 누른 채 움직이지 않는다(적이 나오기 전부터 쏜다). 열이 올라 임계 → 잠금까지 간다.
            _k.SetKey(LogicalKey.Fire, true);
            int lockTick = -1;
            for (int t = 0; t < 900 && lockTick < 0; t++)
            {
                _k.Step(god: true);
                if (p.Locked) lockTick = _k.W.Tick;
            }
            Assert.GreaterOrEqual(lockTick, 0, "열이 쌓이면 잠겨야 한다");

            // 잠기기 전에 임계 발사(ShotFired B = 1)가 있었다: 임계 → 잠금.
            var shots = _k.Events(SimEventType.ShotFired);
            int firstCrit = -1;
            foreach (var s in shots) if (s.B == 1) { firstCrit = s.Tick; break; }
            Assert.GreaterOrEqual(firstCrit, 0, "잠그기 전에 임계 발사가 있어야 한다");
            Assert.LessOrEqual(firstCrit, _k.FirstTick(SimEventType.HeatLockEntered));
            Assert.AreEqual(1, _k.Count(SimEventType.HeatLockEntered), "잠금은 한 번 들어간다");
            Assert.Greater(p.HeatHu, SimConst.HeatToHu(100f) - 1, "잠금 때 열은 100 초과");

            // 잠금 중에는 발사가 나가지 않는다.
            int shotsAtLock = _k.Count(SimEventType.ShotFired);
            _k.Step(10, god: true);
            Assert.AreEqual(shotsAtLock, _k.Count(SimEventType.ShotFired), "잠금 중 J는 발사되지 않는다");
            // 헛누름 표시(FireDryWhileLocked)는 누르는 순간에만 난다 → 뗐다가 다시 누른다.
            _k.SetKey(LogicalKey.Fire, false);
            _k.Step(2, god: true);
            _k.SetKey(LogicalKey.Fire, true);
            _k.Step(2, god: true);
            Assert.Greater(_k.Count(SimEventType.FireDryWhileLocked), 0, "잠금 중 J를 누르면 헛누름 표시");
            Assert.AreEqual(shotsAtLock, _k.Count(SimEventType.ShotFired), "여전히 발사 없음");

            // 잠금 시간이 지나면 풀리고 열이 0이 된다(u 값은 캐릭터마다: char.1 3.0초).
            int lockTicks = p.Def != null ? SimConst.Ticks(p.Def.LockSeconds) : 180;
            _k.SetKey(LogicalKey.Fire, false);
            _k.Step(lockTicks + 30, god: true);
            Assert.IsFalse(p.Locked, "잠금이 풀려야 한다");
            Assert.GreaterOrEqual(_k.Count(SimEventType.HeatUnlocked), 1);

            // 냉각: 쏘지 않으면 열이 0까지 내려온다.
            _k.Step(400, god: true);
            Assert.AreEqual(0, p.HeatHu, "쏘지 않으면 열은 0까지 식는다");
        }

        // ------------------------------------------------------------ (다) 스침 인정과 보상

        [Test]
        public void Integ_C__GrazeAwardedAndRewarded()
        {
            _k.StartRun(1, seed: 11UL);
            ToCombat();
            var p = _k.P;
            // 적 탄 하나가 플레이어 곁(피격 원 밖, 스침 원 안)을 지난다: 겉면까지 0.35칸(피격 0.15 < 0.35 ≤ 스침 0.6).
            p.HeatHu = 0;
            float shotR = 0.1f;
            // 겉면 거리 = |오프셋| − 탄 반지름 = 0.35 → 오프셋 0.45. 탄은 플레이어 왼쪽 3칸에서 오른쪽으로 지나간다.
            var spec = new EnemyShotSpec
            {
                OwnerEnemyId = 0, CastId = 424242,
                Pos = p.Pos + new Vector2(-3f, 0.45f),
                Dir = new Vector2(1f, 0f), Speed = 6f, Radius = shotR, Range = 8f, BaseDamage = 1, Grazeable = true
            };
            var shot = _k.W.Mods.Threats.SpawnShot(_k.W, spec);
            Assert.IsNotNull(shot);

            int hpBefore = p.Hp;
            int comboBefore = p.Combo;
            int grazesBefore = _k.W.Wave.Stats.Grazes;
            _k.Step(120, god: false);

            Assert.AreEqual(1, _k.Count(SimEventType.GrazeAwarded), "스침 한 번");
            Assert.AreEqual(0, _k.Count(SimEventType.PlayerHit), "스침은 피해가 아니다");
            Assert.AreEqual(hpBefore, p.Hp);
            Assert.IsTrue(_k.Events(SimEventType.GrazeAwarded)[0].A == 424242, "castId가 같다");
            // 보상(02 요약서 7.2절): 열이 임계 시작까지 오르고, 다음 발사 열 면제가 서고, 가속이 켜지고, 콤보가 오르고, 점수용 횟수가 오른다.
            Assert.Greater(_k.Count(SimEventType.HeatExemptArmed), 0, "면제가 선다");
            Assert.Greater(_k.Count(SimEventType.GrazeBuffStarted), 0, "가속이 켜진다");
            Assert.Greater(p.Combo, comboBefore, "콤보가 오른다");
            Assert.AreEqual(grazesBefore + 1, _k.W.Wave.Stats.Grazes, "Run이 스침 횟수를 센다(점수용)");
            Assert.GreaterOrEqual(p.HeatHu, 0);

            // 같은 시전(castId)은 두 번 인정하지 않는다.
            var again = _k.W.Mods.Threats.SpawnShot(_k.W, new EnemyShotSpec
            {
                OwnerEnemyId = 0, CastId = 424242, Pos = p.Pos + new Vector2(-3f, 0.45f), Dir = new Vector2(1f, 0f),
                Speed = 6f, Radius = shotR, Range = 8f, BaseDamage = 1, Grazeable = true
            });
            Assert.IsNotNull(again);
            _k.Step(120, god: false);
            Assert.AreEqual(1, _k.Count(SimEventType.GrazeAwarded), "같은 시전의 두 번째 탄은 다시 인정되지 않는다");
        }
    }
}
