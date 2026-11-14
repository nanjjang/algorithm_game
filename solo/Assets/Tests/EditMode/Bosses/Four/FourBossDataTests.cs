using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;

namespace Wizard.Solo.Tests.Bosses.Four
{
    /// <summary>네 보스(천 갓, 밤눈, 흰뿔, 밤삼킴)의 수치 벡터. 기대값은 05 요약서 3.2절·9.3~9.7절에서 가져온다.</summary>
    public class FourBossDataTests
    {
        static IContentDb Db => TestContent.Db;

        static AttackDef Attack(string bossId, string pattern, int step)
        {
            return FourKit.PatternDef(Db.Boss(bossId), pattern).Steps[step].Attack;
        }

        static BossStepDef Step(string bossId, string pattern, int step)
        {
            return FourKit.PatternDef(Db.Boss(bossId), pattern).Steps[step];
        }

        // 05 요약서 3.2절 (S6 T011~T020): 천 갓 w30 14326, 밤눈 w40 28507, 흰뿔 w50 56157, 밤삼킴 w60 118855. float 계산이라 ±1.
        [TestCase(Ids.Boss.SporeQueen, 30, 14326f)]
        [TestCase(Ids.Boss.StormOwl, 40, 28507f)]
        [TestCase(Ids.Boss.FrostGoat, 50, 56157f)]
        [TestCase(Ids.Boss.NightSerpent, 60, 118855f)]
        public void S06_T013_T016__BossMaxHpByWave(string bossId, int wave, float expected)
        {
            Assert.That(new BossSystem().MaxHp(Db, bossId, wave, false), Is.EqualTo(expected).Within(1.0f));
        }

        // 05 요약서 8.1절: 순환 2~5번, 웨이브 30·40·50·60(2회차 90·100·110·120).
        [TestCase(Ids.Boss.SporeQueen, 2, 30, 90)]
        [TestCase(Ids.Boss.StormOwl, 3, 40, 100)]
        [TestCase(Ids.Boss.FrostGoat, 4, 50, 110)]
        [TestCase(Ids.Boss.NightSerpent, 5, 60, 120)]
        public void S06_8_1__RotationAndWaves(string bossId, int rotation, int wave1, int wave2)
        {
            Assert.AreEqual(rotation, Db.Boss(bossId).RotationIndex);
            Assert.AreEqual(bossId, new BossSystem().SelectBoss(Db, wave1, false));
            Assert.AreEqual(bossId, new BossSystem().SelectBoss(Db, wave2, false));
        }

        // 등록: 네 보스가 자기 두뇌로 돈다(등록이 빠지면 DefaultBossBrain이 나온다).
        [Test]
        public void S06_9__FourBrainsRegistered()
        {
            var table = BossBrainRegistry.Build();
            Assert.IsInstanceOf<SporeQueenBrain>(table.Create(Ids.Boss.SporeQueen));
            Assert.IsInstanceOf<StormOwlBrain>(table.Create(Ids.Boss.StormOwl));
            Assert.IsInstanceOf<FrostGoatBrain>(table.Create(Ids.Boss.FrostGoat));
            Assert.IsInstanceOf<NightSerpentBrain>(table.Create(Ids.Boss.NightSerpent));
        }

        // 9.3~9.6절 머리 줄: 기본 체력, 속도, 접촉, 단계 진입 %.
        [TestCase(Ids.Boss.SporeQueen, 4500f, 1.0f, 50)]
        [TestCase(Ids.Boss.StormOwl, 5000f, 3.0f, 50)]
        [TestCase(Ids.Boss.FrostGoat, 5500f, 3.0f, 50)]
        [TestCase(Ids.Boss.NightSerpent, 6500f, 3.5f, 40)]
        public void S06_9__BaseStatsAndPhaseEntry(string bossId, float baseHp, float speed, int phase2Percent)
        {
            var def = Db.Boss(bossId);
            Assert.That(def.BaseHp, Is.EqualTo(baseHp));
            Assert.That(def.MoveSpeed, Is.EqualTo(speed));
            Assert.AreEqual(1, def.ContactDamage);
            Assert.AreEqual(2, def.Phases.Count);
            Assert.AreEqual(100, def.Phases[0].EnterAtHpPercent);
            Assert.AreEqual(phase2Percent, def.Phases[1].EnterAtHpPercent);
            // R2: 모든 보스의 이동 속도 < 기준 속도 3.76.
            Assert.Less(def.MoveSpeed, BossRules.BaselineSpeed(Db));
        }

        // 9.3~9.6절 표의 예고 틱(예고 구역).
        [TestCase(Ids.Boss.SporeQueen, "spore_cloud", 0, 60)]
        [TestCase(Ids.Boss.SporeQueen, "leap", 0, 72)]
        [TestCase(Ids.Boss.StormOwl, "glide", 0, 54)]
        [TestCase(Ids.Boss.StormOwl, "lightning_strike", 0, 48)]
        [TestCase(Ids.Boss.StormOwl, "lightning_plow", 0, 60)]
        [TestCase(Ids.Boss.StormOwl, "lightning_plow", 1, 60)]
        [TestCase(Ids.Boss.FrostGoat, "ice_pillar", 0, 48)]
        [TestCase(Ids.Boss.FrostGoat, "horn_swing", 0, 60)]
        [TestCase(Ids.Boss.FrostGoat, "horn_charge", 0, 54)]
        [TestCase(Ids.Boss.FrostGoat, "frost_field", 0, 60)]
        [TestCase(Ids.Boss.NightSerpent, "coil", 0, 72)]
        [TestCase(Ids.Boss.NightSerpent, "coil", 1, 48)]
        [TestCase(Ids.Boss.NightSerpent, "dark_spit", 0, 42)]
        [TestCase(Ids.Boss.NightSerpent, "burrow", 0, 54)]
        public void S06_9__TelegraphTicks(string bossId, string pattern, int step, int ticks)
        {
            Assert.AreEqual(ticks, SimConst.Ticks(Attack(bossId, pattern, step).TelegraphSeconds));
        }

        // 9.3~9.6절 표의 구역 없는 예고(시전 동작·땅의 금) 틱.
        [Test]
        public void S06_9__CastAndWarnTicks()
        {
            Assert.AreEqual(42, SimConst.Ticks(Attack(Ids.Boss.SporeQueen, "spore_ring", 0).CastSeconds));
            Assert.AreEqual(60, SimConst.Ticks(Step(Ids.Boss.SporeQueen, "summon_sprouts", 0).Summon.WarnSeconds));
            Assert.AreEqual(30, SimConst.Ticks(Attack(Ids.Boss.StormOwl, "feather_fan", 0).CastSeconds));
            Assert.AreEqual(48, SimConst.Ticks(Attack(Ids.Boss.FrostGoat, "frost_breath", 0).CastSeconds));
            Assert.AreEqual(72, SimConst.Ticks(Step(Ids.Boss.FrostGoat, "horn_swing", 0).FrostTelegraphSeconds));
            Assert.AreEqual(36, SimConst.Ticks(Attack(Ids.Boss.NightSerpent, "scale_volley", 0).CastSeconds));
            Assert.AreEqual(24, SimConst.Ticks(Attack(Ids.Boss.NightSerpent, "dark_spit", 0).LockSeconds));
        }

        // 9.3~9.6절 표의 빈틈 틱.
        [TestCase(Ids.Boss.SporeQueen, "spore_ring", 90)]
        [TestCase(Ids.Boss.SporeQueen, "spore_cloud", 0)]
        [TestCase(Ids.Boss.SporeQueen, "summon_sprouts", 0)]
        [TestCase(Ids.Boss.SporeQueen, "leap", 90)]
        [TestCase(Ids.Boss.StormOwl, "glide", 120)]
        [TestCase(Ids.Boss.StormOwl, "feather_fan", 48)]
        [TestCase(Ids.Boss.StormOwl, "lightning_strike", 0)]
        [TestCase(Ids.Boss.StormOwl, "lightning_plow", 60)]
        [TestCase(Ids.Boss.FrostGoat, "ice_pillar", 0)]
        [TestCase(Ids.Boss.FrostGoat, "horn_swing", 60)]
        [TestCase(Ids.Boss.FrostGoat, "frost_breath", 72)]
        [TestCase(Ids.Boss.FrostGoat, "horn_charge", 90)]
        [TestCase(Ids.Boss.FrostGoat, "frost_field", 0)]
        [TestCase(Ids.Boss.NightSerpent, "coil", 90)]
        [TestCase(Ids.Boss.NightSerpent, "dark_spit", 36)]
        [TestCase(Ids.Boss.NightSerpent, "burrow", 120)]
        public void S06_9__RecoveryTicks(string bossId, string pattern, int ticks)
        {
            Assert.AreEqual(ticks, SimConst.Ticks(FourKit.PatternDef(Db.Boss(bossId), pattern).RecoverySeconds));
        }

        // 9.3절: 포자 안개 5초(300틱)·동시 6개, 새싹 4마리·전장 8마리.
        [Test]
        public void S06_9_3__SporeCloudAndSproutNumbers()
        {
            var cloud = Step(Ids.Boss.SporeQueen, "spore_cloud", 0);
            Assert.AreEqual(3, cloud.Count);
            Assert.That(cloud.Attack.Radius, Is.EqualTo(2.0f));
            Assert.AreEqual("spore_cloud", cloud.Attack.LeavesHazard.Kind);
            Assert.AreEqual(300, SimConst.Ticks(cloud.Attack.LeavesHazard.DurationSeconds));
            Assert.AreEqual(6, cloud.Attack.LeavesHazard.MaxCount);
            var summon = Step(Ids.Boss.SporeQueen, "summon_sprouts", 0).Summon;
            Assert.AreEqual(Ids.Enemy.SporeSeedling, summon.Enemy);
            Assert.AreEqual(4, summon.Count);
            Assert.AreEqual(8, summon.FieldCap);
            Assert.That(Db.Boss(Ids.Boss.SporeQueen).Phases[0].AnchorRadius, Is.EqualTo(1f));
        }

        // 9.5절: 기둥 체력 80·반지름 1.0·전장 6개, 서리밭 8초(480틱) 3개·이동 속도 ×0.7, 서리밭 위 2.4.
        [Test]
        public void S06_9_5__PillarAndFrostFieldNumbers()
        {
            var pillar = Step(Ids.Boss.FrostGoat, "ice_pillar", 0);
            Assert.AreEqual(4, pillar.Count);
            Assert.That((float)pillar.Obstacle["hp"], Is.EqualTo(80f));
            Assert.That((float)pillar.Obstacle["radius"], Is.EqualTo(1.0f));
            Assert.AreEqual(6, (int)pillar.Obstacle["max_count"]);
            var field = Step(Ids.Boss.FrostGoat, "frost_field", 0);
            Assert.AreEqual(3, field.Count);
            Assert.AreEqual(0, field.Attack.Damage);
            Assert.That(field.Attack.Radius, Is.EqualTo(2.5f));
            Assert.AreEqual(480, SimConst.Ticks(field.Attack.LeavesHazard.DurationSeconds));
            Assert.That(field.HazardSpeedRatio, Is.EqualTo(0.7f));
            Assert.That(Db.Boss(Ids.Boss.FrostGoat).MoveSpeedOnFrost, Is.EqualTo(2.4f));
        }

        // 9.6절: 머리 0.75, 마디 0.5 × 10(간격 1.0), 꼬리 0.5, 마디·꼬리 피해 ×0.5, 탄 한 발당 최대 3마디.
        [Test]
        public void S06_9_6__SerpentBodyNumbers()
        {
            var body = SerpentBodyDef.From(Db.Boss(Ids.Boss.NightSerpent));
            Assert.That(body.HeadRadius, Is.EqualTo(0.75f));
            Assert.That(body.SegmentRadius, Is.EqualTo(0.5f));
            Assert.That(body.TailRadius, Is.EqualTo(0.5f));
            Assert.AreEqual(10, body.SegmentCount);
            Assert.AreEqual(11, body.PartCount);
            Assert.That(body.SegmentSpacing, Is.EqualTo(1.0f));
            Assert.That(body.SegmentDamageRatio, Is.EqualTo(0.5f));
            Assert.That(body.TailDamageRatio, Is.EqualTo(0.5f));
            Assert.AreEqual(3, body.MaxSegmentHits);
            Assert.AreEqual(240, Attack(Ids.Boss.NightSerpent, "coil", 0).ActiveTicks);
        }

        // 9.6절 OnHitSerpent: 머리 ×1.0(상한 무관), 마디 ×0.5, 탄 한 발이 마디를 3번 맞힌 뒤에는 0.
        [Test]
        public void S06_9_6__OnHitSerpentMultipliers()
        {
            var body = SerpentBodyDef.From(Db.Boss(Ids.Boss.NightSerpent));
            int segHits = 0;
            Assert.That(SerpentRules.HitMul(body, SerpentPart.Head, ref segHits), Is.EqualTo(1f));
            Assert.AreEqual(0, segHits);
            Assert.That(SerpentRules.HitMul(body, SerpentPart.Segment, ref segHits), Is.EqualTo(0.5f));
            Assert.That(SerpentRules.HitMul(body, SerpentPart.Tail, ref segHits), Is.EqualTo(0.5f));
            Assert.That(SerpentRules.HitMul(body, SerpentPart.Segment, ref segHits), Is.EqualTo(0.5f));
            Assert.AreEqual(3, segHits);
            Assert.That(SerpentRules.HitMul(body, SerpentPart.Segment, ref segHits), Is.EqualTo(0f));
            Assert.That(SerpentRules.HitMul(body, SerpentPart.Tail, ref segHits), Is.EqualTo(0f));
            Assert.AreEqual(3, segHits);
            Assert.That(SerpentRules.HitMul(body, SerpentPart.Head, ref segHits), Is.EqualTo(1f), "머리는 상한과 무관");
        }

        // 9.6절: 마디 10개 + 꼬리가 머리가 지나간 길을 따라 간격 1.0으로.
        [Test]
        public void S06_9_6__FollowPathSpacing()
        {
            var body = SerpentBodyDef.From(Db.Boss(Ids.Boss.NightSerpent));
            var trail = new List<Vector2>();
            // 머리가 (0,5) → (5,5)로 간 뒤 위로 꺾어 (5,12)까지 간 길(가장 최근 자리부터).
            for (float y = 11.9f; y > 5f; y -= 0.1f) trail.Add(new Vector2(5f, y));
            for (float x = 5f; x >= -10f; x -= 0.1f) trail.Add(new Vector2(x, 5f));
            var parts = new List<Vector2>();
            SerpentRules.Follow(new Vector2(5f, 12f), trail, body.SegmentSpacing, body.PartCount, parts);
            Assert.AreEqual(11, parts.Count);
            for (int k = 0; k < 7; k++)
            {
                Assert.That(parts[k].x, Is.EqualTo(5f).Within(0.01f));
                Assert.That(parts[k].y, Is.EqualTo(12f - (k + 1)).Within(0.01f));
            }
            for (int k = 7; k < 11; k++)
            {
                Assert.That(parts[k].y, Is.EqualTo(5f).Within(0.01f));
                Assert.That(parts[k].x, Is.EqualTo(5f - (k - 6)).Within(0.01f));
            }
        }

        // 9.6절: 맞은 자리로 머리·마디·꼬리를 가린다.
        [Test]
        public void S06_9_6__PartAtHitPosition()
        {
            var body = SerpentBodyDef.From(Db.Boss(Ids.Boss.NightSerpent));
            Vector2 head = new Vector2(12f, 7f);
            var parts = new List<Vector2>();
            for (int k = 1; k <= body.PartCount; k++) parts.Add(new Vector2(12f - k, 7f));
            Assert.AreEqual(SerpentPart.Head, SerpentRules.PartAt(body, head, parts, new Vector2(12.3f, 7.2f), 0.1f));
            Assert.AreEqual(SerpentPart.Segment, SerpentRules.PartAt(body, head, parts, new Vector2(9f, 7.3f), 0.1f));
            Assert.AreEqual(SerpentPart.Tail, SerpentRules.PartAt(body, head, parts, new Vector2(1f, 7f), 0.1f));
            Assert.AreEqual(SerpentPart.Head, SerpentRules.PartAt(body, head, parts, new Vector2(20f, 1f), 0.1f), "어느 원에도 없으면 몸통(머리)");
        }

        // 9.4절: 14칸 높이를 폭 2칸 가로 띠 7줄(0~6)로.
        [Test]
        public void S06_9_4__PlowRowCenters()
        {
            var w = new SimWorld(Db, SimModuleSet.Stubs());
            var step = Step(Ids.Boss.StormOwl, "lightning_plow", 0);
            Assert.AreEqual(7, step.Count);
            Assert.That(step.Attack.Width, Is.EqualTo(2.0f));
            Assert.That(w.ArenaHeight, Is.EqualTo(14f));
            for (int i = 0; i < 7; i++)
                Assert.That(StormOwlBrain.PlowRowCenterY(w, i, 7), Is.EqualTo(1f + 2f * i).Within(0.0001f));
        }

        // 9.4절: 활강 축은 직전 축과 다르다. 십자는 서로 수직인 두 축.
        [Test]
        public void S06_9_4__GlideAxisNeverRepeats()
        {
            var rng = new Pcg32(11);
            for (int last = 0; last < StormOwlBrain.AxisCount; last++)
                for (int i = 0; i < 40; i++)
                    Assert.AreNotEqual(last, StormOwlBrain.PickAxis(rng, last, -1));
            for (int a = 0; a < StormOwlBrain.AxisCount; a++)
            {
                int b = StormOwlBrain.PerpendicularAxis(a);
                Assert.That(Vector2.Dot(StormOwlBrain.AxisDir(a), StormOwlBrain.AxisDir(b)), Is.EqualTo(0f).Within(0.0001f));
                // 직전 십자의 두 축을 다 피하면 남는 것은 다른 십자의 두 축뿐이다.
                for (int i = 0; i < 20; i++)
                {
                    int next = StormOwlBrain.PickAxis(rng, a, b);
                    Assert.AreNotEqual(a, next);
                    Assert.AreNotEqual(b, next);
                }
            }
        }

        // 9.5절: 숨결 90도 = 120틱·8발, 2단계 120도 = 160틱·11발(15틱마다 1발).
        [TestCase(90f, 120, 8)]
        [TestCase(120f, 160, 11)]
        public void S06_9_5__BreathDurationAndShotCount(float sweep, int ticks, int shots)
        {
            var stream = Step(Ids.Boss.FrostGoat, "frost_breath", 0).Stream;
            Assert.That(stream.SweepDegreesPerSecond, Is.EqualTo(45f));
            Assert.AreEqual(4, stream.ShotsPerSecond);
            int duration = FrostGoatBrain.BreathDurationTicks(sweep, stream.SweepDegreesPerSecond);
            Assert.AreEqual(ticks, duration);
            int interval = SimConst.Ticks(1f / stream.ShotsPerSecond);
            Assert.AreEqual(15, interval);
            int n = 0;
            for (int t = 0; t < duration; t++) if (t % interval == 0) n++;
            Assert.AreEqual(shots, n);
        }

        // 9.5절 고르기 규칙(S6 7.4).
        [Test]
        public void S06_9_5__ChooseForcedRules()
        {
            var all = new List<string> { "ice_pillar", "horn_swing", "frost_breath", "horn_charge" };
            var rng = new Pcg32(3);

            // 숨결이 뽑혔는데 기둥이 2개 미만이고 기둥을 고를 수 있다 → 기둥 먼저, 숨결 우선 표시.
            bool priority = false;
            Assert.AreEqual("ice_pillar", FrostGoatBrain.ChooseForced(all, "frost_breath", 1, ref priority, rng));
            Assert.IsTrue(priority);

            // 표시가 켜져 있고 숨결을 고를 수 있다 → 숨결, 표시 끔.
            Assert.AreEqual("frost_breath", FrostGoatBrain.ChooseForced(all, "horn_swing", 4, ref priority, rng));
            Assert.IsFalse(priority);

            // 기둥이 2개 이상이면 숨결 그대로.
            Assert.AreEqual("frost_breath", FrostGoatBrain.ChooseForced(all, "frost_breath", 2, ref priority, rng));
            Assert.IsFalse(priority);

            // 기둥을 고를 수 없으면(후보에 없음) 숨결 그대로.
            var noPillar = new List<string> { "horn_swing", "frost_breath", "horn_charge" };
            Assert.AreEqual("frost_breath", FrostGoatBrain.ChooseForced(noPillar, "frost_breath", 0, ref priority, rng));
            Assert.IsFalse(priority);

            // 표시가 켜져 있는데 숨결을 고를 수 없고 뿔 돌진이 뽑혔다 → 돌진이 아닌 후보.
            priority = true;
            var noBreath = new List<string> { "ice_pillar", "horn_swing", "horn_charge" };
            for (int i = 0; i < 20; i++)
            {
                string chosen = FrostGoatBrain.ChooseForced(noBreath, "horn_charge", 4, ref priority, rng);
                Assert.AreNotEqual("horn_charge", chosen);
                Assert.IsTrue(priority, "숨결을 못 골랐으니 표시는 남는다");
            }

            // 표시가 꺼져 있으면 뿔 돌진 그대로.
            priority = false;
            Assert.AreEqual("horn_charge", FrostGoatBrain.ChooseForced(all, "horn_charge", 4, ref priority, rng));
        }

        // ------------------------------------------------------------ 단계 덮어쓰기와 순환 2회차

        static BossStep Resolved(string bossId, int phase, int cycle, string pattern, int step)
        {
            return BossPatternData.Resolve(Db.Boss(bossId), phase, cycle)[pattern].Steps[step];
        }

        // 9.3~9.6절 단계 덮어쓰기: 고리 4겹, 활강 십자(count 2), 숨결 120도, 또아리 반경 4.0(바깥 4.5 / 안 3.5).
        [Test]
        public void S06_9__PhaseTwoOverrides()
        {
            Assert.AreEqual(3, Resolved(Ids.Boss.SporeQueen, 0, 1, "spore_ring", 0).Repeat);
            Assert.AreEqual(4, Resolved(Ids.Boss.SporeQueen, 1, 1, "spore_ring", 0).Repeat);
            Assert.AreEqual(1, Resolved(Ids.Boss.StormOwl, 0, 1, "glide", 0).Count);
            Assert.AreEqual(2, Resolved(Ids.Boss.StormOwl, 1, 1, "glide", 0).Count);
            Assert.That(Resolved(Ids.Boss.FrostGoat, 0, 1, "frost_breath", 0).Float(FrostGoatBrain.SweepKey, 90f), Is.EqualTo(90f));
            Assert.That(Resolved(Ids.Boss.FrostGoat, 1, 1, "frost_breath", 0).Float(FrostGoatBrain.SweepKey, 90f), Is.EqualTo(120f));
            var coil1 = Resolved(Ids.Boss.NightSerpent, 0, 1, "coil", 0).Attack;
            var coil2 = Resolved(Ids.Boss.NightSerpent, 1, 1, "coil", 0).Attack;
            Assert.That(coil1.Radius, Is.EqualTo(5.0f));
            Assert.That(coil1.InnerRadius, Is.EqualTo(4.0f));
            Assert.That(coil2.Radius, Is.EqualTo(4.5f));
            Assert.That(coil2.InnerRadius, Is.EqualTo(3.5f));
            CollectionAssert.Contains(Db.Boss(Ids.Boss.StormOwl).Phases[1].Patterns, "lightning_plow");
            CollectionAssert.DoesNotContain(Db.Boss(Ids.Boss.StormOwl).Phases[0].Patterns, "lightning_plow");
            CollectionAssert.Contains(Db.Boss(Ids.Boss.SporeQueen).Phases[1].Patterns, "leap");
            CollectionAssert.Contains(Db.Boss(Ids.Boss.FrostGoat).Phases[1].Patterns, "frost_field");
            CollectionAssert.Contains(Db.Boss(Ids.Boss.NightSerpent).Phases[1].Passives, "scale_volley");
            CollectionAssert.IsEmpty(Db.Boss(Ids.Boss.NightSerpent).Phases[0].Passives);
        }

        // 9.3~9.6절 2회차: 안개 흐름 0.8칸/초, 낙뢰 6번, 기둥 파편 12방향 30도, 어둠 침 5발. 두 단계 모두에 적용된다.
        [TestCase(0)]
        [TestCase(1)]
        public void S06_9__SecondCycleOverridesApplyToFourBosses(int phase)
        {
            Assert.That(Resolved(Ids.Boss.SporeQueen, phase, 1, "spore_cloud", 0).Float(SporeQueenBrain.DriftKey, 0f), Is.EqualTo(0f));
            Assert.That(Resolved(Ids.Boss.SporeQueen, phase, 2, "spore_cloud", 0).Float(SporeQueenBrain.DriftKey, 0f), Is.EqualTo(0.8f));
            Assert.AreEqual(4, Resolved(Ids.Boss.StormOwl, phase, 1, "lightning_strike", 0).Repeat);
            Assert.AreEqual(6, Resolved(Ids.Boss.StormOwl, phase, 2, "lightning_strike", 0).Repeat);
            Assert.AreEqual(8, Resolved(Ids.Boss.FrostGoat, phase, 1, "ice_pillar", 0).Int(FrostGoatBrain.ShardCountKey, 8));
            Assert.AreEqual(12, Resolved(Ids.Boss.FrostGoat, phase, 2, "ice_pillar", 0).Int(FrostGoatBrain.ShardCountKey, 8));
            Assert.That(Resolved(Ids.Boss.FrostGoat, phase, 2, "ice_pillar", 0).Float(FrostGoatBrain.ShardSpreadKey, 45f), Is.EqualTo(30f));
            Assert.AreEqual(3, Resolved(Ids.Boss.NightSerpent, phase, 1, "dark_spit", 0).Repeat);
            Assert.AreEqual(5, Resolved(Ids.Boss.NightSerpent, phase, 2, "dark_spit", 0).Repeat);
        }

        [Test]
        public void S06_8_1__CycleTwoFromWave70()
        {
            var bw = FourKit.Make(Ids.Boss.SporeQueen, 30, FourKit.NoPattern);
            Assert.AreEqual(1, bw.Boss.Cycle);
            bw = FourKit.Make(Ids.Boss.SporeQueen, 90, FourKit.NoPattern);
            Assert.AreEqual(2, bw.Boss.Cycle);
            bw = FourKit.Make(Ids.Boss.NightSerpent, 120, FourKit.NoPattern);
            Assert.AreEqual(2, bw.Boss.Cycle);
        }

        // ------------------------------------------------------------ R1 검산 (05 요약서 9.7절, 달구 3.76, 여유 초)

        static float Slack(AttackDef a)
        {
            var tuning = BossTuning.Load(Db);
            return BossRules.R1SlackSeconds(a.TelegraphSeconds, BossRules.EscapeDistance(a, BossRules.BaselineHitRadius(Db)),
                BossRules.BaselineSpeed(Db), tuning.R1ReactionSeconds);
        }

        // 천 갓 구름 0.18, 도약 0.11 / 밤눈 활강 0.34, 낙뢰 0.11, 밭갈이 0.44 / 흰뿔 기둥 0.24, 휘두르기 0.16, 돌진 0.40 /
        // 밤삼킴 무는 띠 0.31, 잠행 0.08. 요약서가 소수 둘째 자리로 적어 ±0.006 안에서 본다.
        [TestCase(Ids.Boss.SporeQueen, "spore_cloud", 0, 0.18f)]
        [TestCase(Ids.Boss.SporeQueen, "leap", 0, 0.11f)]
        [TestCase(Ids.Boss.StormOwl, "glide", 0, 0.34f)]
        [TestCase(Ids.Boss.StormOwl, "lightning_strike", 0, 0.11f)]
        [TestCase(Ids.Boss.StormOwl, "lightning_plow", 0, 0.44f)]
        [TestCase(Ids.Boss.StormOwl, "lightning_plow", 1, 0.44f)]
        [TestCase(Ids.Boss.FrostGoat, "ice_pillar", 0, 0.24f)]
        [TestCase(Ids.Boss.FrostGoat, "horn_swing", 0, 0.16f)]
        [TestCase(Ids.Boss.FrostGoat, "horn_charge", 0, 0.40f)]
        [TestCase(Ids.Boss.NightSerpent, "coil", 1, 0.31f)]
        [TestCase(Ids.Boss.NightSerpent, "burrow", 0, 0.08f)]
        public void S06_9_7__R1Slack(string bossId, string pattern, int step, float expected)
        {
            float slack = Slack(Attack(bossId, pattern, step));
            Assert.That(slack, Is.EqualTo(expected).Within(0.006f));
            Assert.GreaterOrEqual(slack, BossTuning.Load(Db).R1MarginSeconds);
        }

        // 9.7절: 밤눈 십자 0.22(교차점의 벗어나는 거리 √2 × (w/2 + 0.15) = 1.63).
        [Test]
        public void S06_9_7__R1SlackGlideCross()
        {
            var tuning = BossTuning.Load(Db);
            var a = Attack(Ids.Boss.StormOwl, "glide", 0);
            float escape = Mathf.Sqrt(2f) * (a.Width / 2f + BossRules.BaselineHitRadius(Db));
            Assert.That(escape, Is.EqualTo(1.63f).Within(0.005f));
            float slack = BossRules.R1SlackSeconds(a.TelegraphSeconds, escape, BossRules.BaselineSpeed(Db), tuning.R1ReactionSeconds);
            Assert.That(slack, Is.EqualTo(0.22f).Within(0.006f));
        }

        // 9.7절: 흰뿔 휘두르기는 서리밭 위(속도 ×0.7)에서 1.2초 예고로 0.11.
        [Test]
        public void S06_9_7__R1SlackHornSwingOnFrost()
        {
            var tuning = BossTuning.Load(Db);
            var step = Step(Ids.Boss.FrostGoat, "horn_swing", 0);
            float ratio = Step(Ids.Boss.FrostGoat, "frost_field", 0).HazardSpeedRatio;
            float escape = BossRules.EscapeDistance(step.Attack, BossRules.BaselineHitRadius(Db));
            Assert.That(escape, Is.EqualTo(2.221f).Within(0.002f));
            float slack = BossRules.R1SlackSeconds(step.FrostTelegraphSeconds, escape, BossRules.BaselineSpeed(Db) * ratio, tuning.R1ReactionSeconds);
            Assert.That(slack, Is.EqualTo(0.11f).Within(0.006f));
            // 서리밭 위에서 1.0초 그대로면 R1을 어긴다(그래서 1.2초로 늘린다).
            float shortSlack = BossRules.R1SlackSeconds(step.Attack.TelegraphSeconds, escape, BossRules.BaselineSpeed(Db) * ratio, tuning.R1ReactionSeconds);
            Assert.Less(shortSlack, tuning.R1MarginSeconds);
        }

        // 9.7절 "전부 통과": 네 보스의 모든 예고 공격이 R1을 지킨다.
        [TestCase(Ids.Boss.SporeQueen)]
        [TestCase(Ids.Boss.StormOwl)]
        [TestCase(Ids.Boss.FrostGoat)]
        [TestCase(Ids.Boss.NightSerpent)]
        public void S06_9_7__AllTelegraphsPassR1(string bossId)
        {
            var violations = new List<string>();
            BossRules.CheckR1(Db, Db.Boss(bossId), BossTuning.Load(Db), violations);
            // 흰뿔의 서리밭 원은 피해 0(느려지기만 한다)이라 R1의 대상이 아니다 — 9.7절의 검산 목록에도 없다.
            var field = FourKit.PatternDef(Db.Boss(Ids.Boss.FrostGoat), FrostGoatBrain.PatternField).Steps[0].Attack;
            Assert.AreEqual(0, field.Damage);
            violations.Remove(FrostGoatBrain.PatternField + "." + field.Key);
            CollectionAssert.IsEmpty(violations);
        }

        // 2절 R3: 흩뿌림·연사 탄은 발사점 3칸 지점에서 이웃 탄 사이 호 길이 3 × Δθ ≥ 1.0칸, 속도 ≤ 9.
        [TestCase(Ids.Boss.SporeQueen, "spore_ring", 0)]
        [TestCase(Ids.Boss.SporeQueen, "leap", 1)]
        [TestCase(Ids.Boss.StormOwl, "feather_fan", 0)]
        public void S05_R3__SpreadGapAtThreeCells(string bossId, string pattern, int step)
        {
            var tuning = BossTuning.Load(Db);
            var proj = Attack(bossId, pattern, step).Proj;
            Assert.GreaterOrEqual(tuning.R3CheckDistance * proj.SpreadDegrees * Mathf.Deg2Rad, tuning.R3MinGap);
            Assert.LessOrEqual(proj.Speed, 9f);
        }

        // G08.json의 보충 수치가 두뇌의 내장 기본값과 같다(조각을 못 읽어도 같은 값으로 돈다).
        [Test]
        public void S00_G08__TuningFragmentMatchesBuiltInDefaults()
        {
            var j = Db.SoloExtra("G08");
            Assert.IsNotNull(j, "Resources/DataSolo/G08.json을 읽지 못했다");
            var spore = new SporeQueenBrain();
            Assert.That((float)j["spore_queen"]["sprout_min_distance"], Is.EqualTo(spore.SproutMinDistance));
            Assert.That((float)j["spore_queen"]["sprout_max_distance"], Is.EqualTo(spore.SproutMaxDistance));
            Assert.That((float)j["spore_queen"]["sprout_min_spacing"], Is.EqualTo(spore.SproutMinSpacing));
            Assert.AreEqual(spore.SproutMaxTries, (int)j["spore_queen"]["sprout_max_tries"]);
            Assert.AreEqual(new FrostGoatBrain().PillarShardDamage, (int)j["frost_goat"]["pillar_shard_damage"]);
            var serpent = new NightSerpentBrain();
            Assert.That((float)j["night_serpent"]["trail_sample_distance"], Is.EqualTo(serpent.TrailSampleDistance));
            Assert.That((float)j["night_serpent"]["part_hit_slack"], Is.EqualTo(serpent.PartHitSlack));
        }
    }
}
