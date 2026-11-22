using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;

namespace Wizard.Solo.Tests.Bosses.Common
{
    /// <summary>보스 공통 규칙의 순수 함수(05 요약서 3.2·8·9.7·10.4절). 기대값은 요약서에서 가져왔다.</summary>
    public class BossRulesTests
    {
        static IContentDb Db => TestContent.Db;

        // 05 요약서 3.2절 (S6 T011~T020): 정수 내림 기준 값. float 계산이라 ±1 안에서 본다.
        [TestCase(Ids.Boss.KilnBoar, 10, 3000f)]
        [TestCase(Ids.Boss.MirrorPhantom, 20, 7111f)]
        [TestCase(Ids.Boss.SporeQueen, 30, 14326f)]
        [TestCase(Ids.Boss.StormOwl, 40, 28507f)]
        [TestCase(Ids.Boss.FrostGoat, 50, 56157f)]
        [TestCase(Ids.Boss.NightSerpent, 60, 118855f)]
        [TestCase(Ids.Boss.KilnBoar, 70, 98239f)]
        [TestCase(Ids.Boss.MirrorPhantom, 80, 234575f)]
        public void S06_T011_T020__BossMaxHpByWave(string bossId, int wave, float expected)
        {
            float hp = new BossSystem().MaxHp(Db, bossId, wave, false);
            Assert.That(hp, Is.EqualTo(expected).Within(1.0f));
        }

        // 05 요약서 3.2절: H(1) = 1.00, H(6) = 1.60, H(10) = 2.08, H(12) = 2.32, H(13) = 2.4592, H(20) ≈ 3.6977, H(30) ≈ 6.6221
        [TestCase(1, 1.00)]
        [TestCase(6, 1.60)]
        [TestCase(10, 2.08)]
        [TestCase(12, 2.32)]
        [TestCase(13, 2.4592)]
        [TestCase(20, 3.6977)]
        [TestCase(30, 6.6221)]
        public void S05_3_7__HpMulCurve(int wave, double expected)
        {
            Assert.That(BossRules.HpMul(Db.Waves, wave), Is.EqualTo(expected).Within(0.0001));
        }

        // 05 요약서 10.2절: 첫 만남 체력 4000(w10 배율 1), 2000에서 멈춤.
        [Test]
        public void S06_10_2__FirstEncounterHp4000AndFloor2000()
        {
            Assert.That(new BossSystem().MaxHp(Db, Ids.Boss.MirrorPhantom, 10, true), Is.EqualTo(4000f));
            var bw = BossWorld.Create();
            var body = bw.Spawn(Ids.Boss.MirrorPhantom, 10, phantomFirst: true);
            Assert.IsTrue(bw.Boss.FirstEncounter);
            Assert.That(body.MaxHp, Is.EqualTo(4000f));
            Assert.That(body.HpFloor, Is.EqualTo(2000f));
        }

        // 05 요약서 8.1절 순환.
        [Test]
        public void S06_8_1__SelectBossRotationAndFirstEncounter()
        {
            var sys = new BossSystem();
            Assert.AreEqual(Ids.Boss.MirrorPhantom, sys.SelectBoss(Db, 10, true));
            Assert.AreEqual(Ids.Boss.KilnBoar, sys.SelectBoss(Db, 10, false));
            Assert.AreEqual(Ids.Boss.MirrorPhantom, sys.SelectBoss(Db, 20, false));
            Assert.AreEqual(Ids.Boss.MirrorPhantom, sys.SelectBoss(Db, 20, true));
            Assert.AreEqual(Ids.Boss.SporeQueen, sys.SelectBoss(Db, 30, false));
            Assert.AreEqual(Ids.Boss.StormOwl, sys.SelectBoss(Db, 40, false));
            Assert.AreEqual(Ids.Boss.FrostGoat, sys.SelectBoss(Db, 50, false));
            Assert.AreEqual(Ids.Boss.NightSerpent, sys.SelectBoss(Db, 60, false));
            Assert.AreEqual(Ids.Boss.KilnBoar, sys.SelectBoss(Db, 70, true));
            Assert.AreEqual(Ids.Boss.MirrorPhantom, sys.SelectBoss(Db, 80, false));
            Assert.IsNull(sys.SelectBoss(Db, 15, false));
        }

        // 05 요약서 8.3절: 대기 = 90 × (10 − rage) / 10 → 90, 81, 72, 63, 54, 45
        [TestCase(0, 90)]
        [TestCase(1, 81)]
        [TestCase(2, 72)]
        [TestCase(3, 63)]
        [TestCase(4, 54)]
        [TestCase(5, 45)]
        public void S06_8_3__WaitTicksByRage(int rage, int expected)
        {
            Assert.AreEqual(expected, BossRules.WaitTicks(Db.BossCommon, rage));
        }

        // 05 요약서 8.2·8.3절: 격노 스택 = fightTicks / 1200(최대 5), 최종 격노 10800틱.
        [TestCase(0, 0)]
        [TestCase(1199, 0)]
        [TestCase(1200, 1)]
        [TestCase(2399, 1)]
        [TestCase(2400, 2)]
        [TestCase(6000, 5)]
        [TestCase(10799, 5)]
        public void S06_8_3__RageStackEvery1200Ticks(int fightTicks, int expected)
        {
            Assert.AreEqual(expected, BossRules.RageStacks(Db.BossCommon, fightTicks));
        }

        [Test]
        public void S06_8_2__FinalRageAt10800Ticks()
        {
            Assert.IsFalse(BossRules.FinalRage(Db.BossCommon, 10799));
            Assert.IsTrue(BossRules.FinalRage(Db.BossCommon, 10800));
        }

        // 05 요약서 8.3절: 연사형 간격 = base × (10 − n) / 10 내림, 하한 min_interval_ticks. 9.1절: 가마등 2회차 둘째 고리 30틱, 하한 12.
        [TestCase(30, 0, 12, 30)]
        [TestCase(30, 3, 12, 21)]
        [TestCase(30, 5, 12, 15)]
        [TestCase(15, 5, 12, 12)]
        [TestCase(25, 1, 10, 22)]
        public void S06_8_3__RapidIntervalFloorsAndClampsToMin(int baseTicks, int rage, int min, int expected)
        {
            Assert.AreEqual(expected, BossRules.RapidIntervalTicks(Db.BossCommon, baseTicks, rage, min));
        }

        // 05 요약서 8.3절: hp × 100 ≤ hpMax × enter_at_hp_percent → 다음 단계. 되돌아가지 않고, 한 틱에 두 경계면 마지막 단계로 한 번.
        [Test]
        public void S06_8_3__PhaseBoundariesAndDoubleCrossing()
        {
            var kiln = Db.Boss(Ids.Boss.KilnBoar);
            Assert.AreEqual(0, BossRules.PhaseFor(kiln, 3000f, 3000f, 0));
            Assert.AreEqual(0, BossRules.PhaseFor(kiln, 1801f, 3000f, 0));
            Assert.AreEqual(1, BossRules.PhaseFor(kiln, 1800f, 3000f, 0));
            Assert.AreEqual(1, BossRules.PhaseFor(kiln, 3000f, 3000f, 1), "단계는 되돌아가지 않는다");

            var phantom = Db.Boss(Ids.Boss.MirrorPhantom);
            Assert.AreEqual(1, BossRules.PhaseFor(phantom, 2400f, 4000f, 0));
            Assert.AreEqual(1, BossRules.PhaseFor(phantom, 1001f, 4000f, 0));
            Assert.AreEqual(2, BossRules.PhaseFor(phantom, 1000f, 4000f, 1));
            Assert.AreEqual(2, BossRules.PhaseFor(phantom, 900f, 4000f, 0), "60%와 25%를 한 번에 넘으면 3단계로 바로");
        }

        // 05 요약서 8.3절: 직전 패턴 제외 + Eligible → 비면 직전 포함 허용 → 그래도 비면 빈 목록(30틱 더 대기).
        [Test]
        public void S06_8_3__PatternCandidates()
        {
            var patterns = new List<string> { "a", "b", "c" };
            var buf = new List<string>();
            BossRules.Candidates(patterns, "b", null, buf);
            CollectionAssert.AreEqual(new[] { "a", "c" }, buf, "직전 패턴 제외");

            BossRules.Candidates(patterns, "b", k => k == "b", buf);
            CollectionAssert.AreEqual(new[] { "b" }, buf, "비면 직전 패턴을 허용");

            BossRules.Candidates(patterns, "b", k => false, buf);
            Assert.AreEqual(0, buf.Count, "그래도 비면 고를 것이 없다");

            BossRules.Candidates(new List<string> { "a" }, "a", null, buf);
            CollectionAssert.AreEqual(new[] { "a" }, buf, "하나뿐이면 반복");

            BossRules.Candidates(new List<string>(), null, null, buf);
            Assert.AreEqual(0, buf.Count, "허상 1단계처럼 패턴이 없으면 빈 목록");
            Assert.AreEqual(30, SimConst.Ticks(Db.BossCommon.NoCandidateWaitSeconds));
        }

        // 05 요약서 9.7절 R1 검산(달구 3.76, 여유 초): 가마등 돌진 2·3 0.14 / 발구르기 0.08 / 낙석 0.31 / 허상 폭발 0.11
        [Test]
        public void S06_9_7__R1SlackKilnBoarAndPhantom()
        {
            var tuning = BossTuning.Load(Db);
            float v = BossRules.BaselineSpeed(Db), hit = BossRules.BaselineHitRadius(Db);
            Assert.That(v, Is.EqualTo(3.76f).Within(0.0001f));
            Assert.That(hit, Is.EqualTo(0.15f).Within(0.0001f));

            var kiln = Db.Boss(Ids.Boss.KilnBoar);
            Assert.That(Slack(Attack(kiln, "triple_charge", 1), v, hit, tuning), Is.EqualTo(0.14f).Within(0.006f));
            Assert.That(Slack(Attack(kiln, "stomp", 0), v, hit, tuning), Is.EqualTo(0.08f).Within(0.006f));
            Assert.That(Slack(Attack(kiln, "rockfall", 0), v, hit, tuning), Is.EqualTo(0.31f).Within(0.006f));

            // 허상의 복사 폭발: 반경 최대 1.5, 예고 0.8초.
            var prm = PhantomCopyParams.From(Db.Boss(Ids.Boss.MirrorPhantom).PhantomCopy);
            float explosion = BossRules.R1SlackSeconds(prm.ExplosionTelegraphSeconds, prm.ExplosionRadiusMax + hit, v, tuning.R1ReactionSeconds);
            Assert.That(explosion, Is.EqualTo(0.11f).Within(0.006f));
            Assert.GreaterOrEqual(explosion, tuning.R1MarginSeconds);
            // 직선 번개(폭 0.5): 0.25 + 0.4 / 3.76 = 0.356 ≤ 0.6 − 0.05
            float line = BossRules.R1SlackSeconds(prm.ChainLineTelegraphSeconds, prm.ChainLineWidth / 2f + hit, v, tuning.R1ReactionSeconds);
            Assert.GreaterOrEqual(line, tuning.R1MarginSeconds);

            var violations = new List<string>();
            BossRules.CheckR1(Db, kiln, tuning, violations);
            BossRules.CheckR1(Db, Db.Boss(Ids.Boss.MirrorPhantom), tuning, violations);
            CollectionAssert.IsEmpty(violations);
        }

        static AttackDef Attack(BossDef def, string pattern, int step)
        {
            return def.Patterns.Find(p => p.Key == pattern).Steps[step].Attack;
        }

        static float Slack(AttackDef a, float v, float hit, BossTuning tuning)
        {
            return BossRules.R1SlackSeconds(a.TelegraphSeconds, BossRules.EscapeDistance(a, hit), v, tuning.R1ReactionSeconds);
        }

        // 05 요약서 2절: 벗어나는 거리. Circle r + 0.15 / Band w/2 + 0.15 / Fan R·sinα/(1 + sinα) + 0.15
        [Test]
        public void S05_10_1__EscapeDistanceByShape()
        {
            Assert.That(BossRules.EscapeDistance(new AttackDef { Shape = "circle", Radius = 2f }, 0.15f), Is.EqualTo(2.15f).Within(0.0001f));
            Assert.That(BossRules.EscapeDistance(new AttackDef { Shape = "band", Width = 2f }, 0.15f), Is.EqualTo(1.15f).Within(0.0001f));
            float s = Mathf.Sin(45f * Mathf.Deg2Rad);
            Assert.That(BossRules.EscapeDistance(new AttackDef { Shape = "fan", Radius = 5f, FanDegrees = 90f }, 0.15f),
                Is.EqualTo(5f * s / (1f + s) + 0.15f).Within(0.0001f));
            Assert.That(BossRules.EscapeDistance(new AttackDef { Shape = "circle", Radius = 2f, EscapeOverrideMilli = 1630 }, 0.15f),
                Is.EqualTo(1.63f).Within(0.0001f));
        }

        // 05 요약서 10.4절 T280: hpMax 7111, hpMin 2702 → 62%
        [Test]
        public void S06_T280__DentPercent()
        {
            Assert.AreEqual(62, BossRules.DentPercent(7111f, 2702f));
            Assert.AreEqual(0, BossRules.DentPercent(7111f, 7111f));
            Assert.AreEqual(100, BossRules.DentPercent(7111f, 0f));
        }

        // 05 요약서 9.1절: 2단계 돌진 4회(step 1 repeat 3), 2회차 발구르기 고리 두 겹(+0.5초, 15도).
        [Test]
        public void S06_9_1__KilnBoarOverridesByPhaseAndCycle()
        {
            var kiln = Db.Boss(Ids.Boss.KilnBoar);
            var p1 = BossPatternData.Resolve(kiln, 0, 1);
            Assert.AreEqual(1, p1["triple_charge"].Steps[0].Repeat);
            Assert.AreEqual(2, p1["triple_charge"].Steps[1].Repeat, "1단계: 1 + 2 = 삼연 돌진");
            Assert.AreEqual(1, p1["stomp"].Steps[1].Repeat);

            var p2 = BossPatternData.Resolve(kiln, 1, 1);
            Assert.AreEqual(3, p2["triple_charge"].Steps[1].Repeat, "2단계: 1 + 3 = 돌진 4회");
            Assert.AreEqual(2, kiln.Patterns[0].Steps[1].Repeat, "원본 정의는 고치지 않는다");

            var c2 = BossPatternData.Resolve(kiln, 0, 2);
            Assert.AreEqual(2, c2["stomp"].Steps[1].Repeat);
            Assert.AreEqual(30, SimConst.Ticks(c2["stomp"].Steps[1].RepeatIntervalSeconds));
            Assert.That(c2["stomp"].Steps[1].RepeatAngleDegrees, Is.EqualTo(15f));
            Assert.AreEqual(12, c2["stomp"].Steps[1].Def.MinIntervalTicks);
        }

        // 05 요약서 9.1절 패턴 수치(데이터가 요약서와 같은지).
        [Test]
        public void S06_9_1__KilnBoarPatternValues()
        {
            var kiln = Db.Boss(Ids.Boss.KilnBoar);
            Assert.That(kiln.BaseHp, Is.EqualTo(3000f));
            Assert.That(kiln.MoveSpeed, Is.EqualTo(2.2f));
            Assert.AreEqual(2, kiln.ContactDamage);
            Assert.AreEqual(60, kiln.Phases[1].EnterAtHpPercent);
            var first = Attack(kiln, "triple_charge", 0);
            var next = Attack(kiln, "triple_charge", 1);
            Assert.AreEqual(60, SimConst.Ticks(first.TelegraphSeconds));
            Assert.AreEqual(42, SimConst.Ticks(next.TelegraphSeconds));
            Assert.That(first.Width, Is.EqualTo(2f));
            Assert.That(first.DashSpeed, Is.EqualTo(16f));
            Assert.AreEqual(2, first.Damage);
            Assert.IsTrue(first.IsDash);
            Assert.AreEqual(150, SimConst.Ticks(kiln.Patterns[0].RecoverySeconds));
            Assert.That(kiln.Patterns[0].RecoveryDamageRatio, Is.EqualTo(1.5f));
            Assert.AreEqual(180, SimConst.Ticks(first.LeavesHazard.DurationSeconds));
            Assert.AreEqual(54, SimConst.Ticks(Attack(kiln, "stomp", 0).TelegraphSeconds));
            Assert.AreEqual(12, Attack(kiln, "stomp", 1).Proj.Count);
            Assert.That(Attack(kiln, "stomp", 1).Proj.SpreadDegrees, Is.EqualTo(30f));
            Assert.AreEqual(48, SimConst.Ticks(kiln.Patterns[1].RecoverySeconds));
            Assert.AreEqual(6, kiln.Patterns[2].Steps[0].Count);
            Assert.AreEqual(60, SimConst.Ticks(Attack(kiln, "rockfall", 0).TelegraphSeconds));
        }

        // 조각(G07.json)을 읽은 값이 코드의 처음 값과 같은가(조각을 못 읽는 가짜 콘텐츠에서도 같은 규칙이 돌게).
        [Test]
        public void S00_G07Json__MatchesBuiltInDefaults()
        {
            Assert.IsNotNull(Db.SoloExtra(BossTuning.ExtraName), "Resources/DataSolo/G07.json을 읽지 못했다");
            var loaded = BossTuning.Load(Db);
            var builtIn = new BossTuning();
            Assert.AreEqual(builtIn.RevengeWave, loaded.RevengeWave);
            CollectionAssert.AreEqual(builtIn.ReflectionBloomTicks, loaded.ReflectionBloomTicks);
            Assert.AreEqual(builtIn.CollapseMasterLineTicks, loaded.CollapseMasterLineTicks);
            Assert.AreEqual(builtIn.R1ReactionSeconds, loaded.R1ReactionSeconds);
            Assert.AreEqual(builtIn.R1MarginSeconds, loaded.R1MarginSeconds);
            Assert.AreEqual(builtIn.R3CheckDistance, loaded.R3CheckDistance);
            Assert.AreEqual(builtIn.R3MinGap, loaded.R3MinGap);
            Assert.AreEqual(builtIn.SpawnXRatio, loaded.SpawnXRatio);
            Assert.AreEqual(builtIn.SpawnYRatioA, loaded.SpawnYRatioA);
            Assert.AreEqual(builtIn.SpawnYRatioB, loaded.SpawnYRatioB);
            Assert.AreEqual(builtIn.BoulderShardDamage, loaded.BoulderShardDamage);
            Assert.AreEqual(builtIn.EmberDamage, loaded.EmberDamage);
            Assert.AreEqual(builtIn.ChaseStopDistance, loaded.ChaseStopDistance);
            Assert.AreEqual(builtIn.KeepDistanceBand, loaded.KeepDistanceBand);
            Assert.AreEqual(builtIn.SummonRingRadius, loaded.SummonRingRadius);
            Assert.AreEqual(builtIn.EmberSegmentLength, loaded.EmberSegmentLength);
            Assert.AreEqual(builtIn.CopyHitSlack, loaded.CopyHitSlack);
            Assert.AreEqual(builtIn.PatternStallTicks, loaded.PatternStallTicks);
            // 05 요약서 10.2절: 되비침 만개 24/54/84틱, 쓰러짐 72틱 대사 / 10.3절: 웨이브 20
            CollectionAssert.AreEqual(new[] { 24, 54, 84 }, loaded.ReflectionBloomTicks);
            Assert.AreEqual(72, loaded.CollapseMasterLineTicks);
            Assert.AreEqual(20, loaded.RevengeWave);
        }

        [Test]
        public void Registration__FactoryAndBrains()
        {
            Assert.IsInstanceOf<BossSystem>(SimModuleSet.CreateDefault().Bosses);
            var sys = new BossSystem();
            Assert.IsInstanceOf<KilnBoarBrain>(sys.Brains.Create(Ids.Boss.KilnBoar));
            Assert.IsInstanceOf<MirrorPhantomBrain>(sys.Brains.Create(Ids.Boss.MirrorPhantom));
            Assert.IsInstanceOf<DefaultBossBrain>(sys.Brains.Create("boss.unknown"));
            Assert.AreNotSame(sys.Brains.Create(Ids.Boss.KilnBoar), sys.Brains.Create(Ids.Boss.KilnBoar), "보스마다 두뇌 객체가 따로다");
        }
    }
}
