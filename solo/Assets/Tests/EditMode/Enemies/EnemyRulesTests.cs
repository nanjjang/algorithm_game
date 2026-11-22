using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Enemies;

namespace Wizard.Solo.Tests.Enemies
{
    /// <summary>체력 배율, 받는 피해, 엘리트 비용, 꼬리표별 군중 제어, 감속·잠식. 05 요약서 3·5·7절, 02 요약서 8.5절, 03 요약서 10절.</summary>
    public class EnemyRulesTests
    {
        static readonly string[] Species =
        {
            Ids.Enemy.SlimeMoss, Ids.Enemy.WolfBramble, Ids.Enemy.GolemMenhir, Ids.Enemy.ShamanCap, Ids.Enemy.BugLantern, Ids.Enemy.HogNeedle,
            Ids.Enemy.WeaselPot, Ids.Enemy.BatDusk, Ids.Enemy.WispStray, Ids.Enemy.VineThorn, Ids.Enemy.BoarTusk, Ids.Enemy.ToadDrum
        };

        // ------------------------------------------------------------ 3.2절 H(w)

        // 05 요약서 3.2절: H(1)=1.00, H(6)=1.60, H(10)=2.08, H(12)=2.32, H(13)=2.4592, H(20)≈3.6977. 슬라임 base 20.
        [TestCase(1, 1.00f)]
        [TestCase(6, 1.60f)]
        [TestCase(10, 2.08f)]
        [TestCase(12, 2.32f)]
        [TestCase(13, 2.4592f)]
        [TestCase(20, 3.6977f)]
        public void S05_H__MaxHpIsBaseTimesWaveMul(int wave, float h)
        {
            var k = new Kit();
            k.Waves.Mul = wv => wv == wave ? h : -1f;
            k.W.Wave.Wave = wave;
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5);
            Assert.AreEqual(20f * h, e.MaxHp, 1e-3f);
            Assert.AreEqual(e.MaxHp, e.Hp, 1e-5f);
            Assert.AreEqual(wave, e.SpawnWave);
        }

        [Test]
        public void S05_H__EliteTimesThreeAndThickHide()
        {
            var k = new Kit();
            k.Waves.Mul = wv => 2.32f;
            k.W.Wave.Wave = 12;
            var elite = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5, elite: true);
            Assert.AreEqual(20f * 2.32f * 3f, elite.MaxHp, 1e-3f);
            // 변덕 "두꺼운 가죽" ×1.3
            k.W.Wave.EnemyHpMul = 1.3f;
            var thick = k.Spawn(Ids.Enemy.GolemMenhir, 8, 5);
            Assert.AreEqual(300f * 2.32f * 1.3f, thick.MaxHp, 1e-2f);
        }

        [Test]
        public void S05_H__PracticeRoomWaveZeroUsesH1()
        {
            var k = new Kit();
            k.W.Wave.Wave = 0;
            k.Waves.Mul = wv => wv == 1 ? 1f : 99f;
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5);
            Assert.AreEqual(20f, e.MaxHp, 1e-4f);
            Assert.AreEqual(1, k.Waves.HpMulArgs[k.Waves.HpMulArgs.Count - 1]);
        }

        [Test]
        public void S05_H__HpOverrideWins()
        {
            var k = new Kit();
            var e = k.Sys.Spawn(k.W, new EnemySpawnSpec { DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(5, 5), HpOverride = 77f, Instant = true });
            Assert.AreEqual(77f, e.MaxHp, 1e-5f);
        }

        // ------------------------------------------------------------ 3.3절 받는 피해

        // 선돌지기 정면 비임계 비전(약점 ×1.25) 20 → 25 → 12.5. 상성은 이미 곱해져 온다.
        [Test]
        public void S05_DMG__GolemFrontNonCritHalved()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 12, 7);
            g.Facing = new Vector2(1, 0);
            Assert.IsTrue(g.ArmorActive);
            Assert.AreEqual(12.5f, k.Sys.ModifyIncomingDamage(k.W, g, 20f * 1.25f, false, false, new Vector2(15, 7), new Vector2(15, 7)), 1e-4f);
        }

        [Test]
        public void S05_DMG__GolemCritIgnoresArmor()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 12, 7);
            g.Facing = new Vector2(1, 0);
            Assert.AreEqual(25f, k.Sys.ModifyIncomingDamage(k.W, g, 25f, true, false, new Vector2(15, 7), new Vector2(15, 7)), 1e-4f);
        }

        [Test]
        public void S05_DMG__GolemRecoveryGapIgnoresArmor()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 12, 7);
            g.Facing = new Vector2(1, 0);
            g.Ai = EnemyAiState.Recovery;
            Assert.AreEqual(25f, k.Sys.ModifyIncomingDamage(k.W, g, 25f, false, false, new Vector2(15, 7), new Vector2(15, 7)), 1e-4f);
        }

        // 뒤쪽 번개(저항 ×0.8) 20 → 16
        [Test]
        public void S05_DMG__GolemBackNotArmored()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 12, 7);
            g.Facing = new Vector2(1, 0);
            Assert.AreEqual(16f, k.Sys.ModifyIncomingDamage(k.W, g, 20f * 0.8f, false, false, new Vector2(9, 7), new Vector2(9, 7)), 1e-4f);
        }

        // ⑥ 꿰뚫는 창·⑨ 뇌광창은 철갑 무시
        [Test]
        public void S05_DMG__IgnoresArmorFlag()
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 12, 7);
            g.Facing = new Vector2(1, 0);
            Assert.AreEqual(25f, k.Sys.ModifyIncomingDamage(k.W, g, 25f, false, true, new Vector2(15, 7), new Vector2(15, 7)), 1e-4f);
        }

        // 정면 ±60도(고인돌지기 ±80도)
        [TestCase(59f, false, true)]
        [TestCase(61f, false, false)]
        [TestCase(79f, true, true)]
        [TestCase(81f, true, false)]
        [TestCase(70f, false, false)]
        public void S05_DMG__ArmorHalfAngle(float degrees, bool elite, bool armored)
        {
            var k = new Kit();
            var g = k.Spawn(Ids.Enemy.GolemMenhir, 12, 7, elite);
            g.Facing = new Vector2(1, 0);
            Vector2 from = g.Pos + Dir8Util.DegreesToSimVector(degrees) * 3f;
            float got = k.Sys.ModifyIncomingDamage(k.W, g, 10f, false, false, from, from);
            Assert.AreEqual(armored ? 5f : 10f, got, 1e-4f);
        }

        // 멧돼지 기절 중 냉기(약점) 10 → 12.5 → 18.75
        [Test]
        public void S05_DMG__StunnedTakesOneAndHalf()
        {
            var k = new Kit();
            var boar = k.Spawn(Ids.Enemy.BoarTusk, 12, 7);
            boar.DamageTakenMul = TestContent.Db.Enemy(Ids.Enemy.BoarTusk).Ai.Value<float>("stun_damage_ratio");
            Assert.AreEqual(18.75f, k.Sys.ModifyIncomingDamage(k.W, boar, 10f * 1.25f, false, false, new Vector2(15, 7), new Vector2(15, 7)), 1e-4f);
        }

        [Test]
        public void S05_DMG__BurrowedAndSpawningTakeNothing()
        {
            var k = new Kit();
            var weasel = k.Spawn(Ids.Enemy.WeaselPot, 3, 3);
            Assert.IsTrue(weasel.Burrowed);
            Assert.AreEqual(0f, k.Sys.ModifyIncomingDamage(k.W, weasel, 50f, true, true, Vector2.zero, Vector2.zero));
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5, instant: false);
            Assert.AreEqual(EnemyAiState.Spawning, slime.Ai);
            Assert.AreEqual(0f, k.Sys.ModifyIncomingDamage(k.W, slime, 50f, false, false, Vector2.zero, Vector2.zero));
        }

        [Test]
        public void S05_DMG__BossBodyGoesToBosses()
        {
            var k = new Kit();
            var boss = k.AddBossBody(12, 3);
            k.Bosses.DamageMul = 0.25f;
            Assert.AreEqual(10f, k.Sys.ModifyIncomingDamage(k.W, boss, 40f, false, false, Vector2.zero, Vector2.zero), 1e-5f);
            Assert.AreEqual(1, k.Bosses.ModifyCalls);
        }

        // ------------------------------------------------------------ 6.0절 비용, 7절 TV-ELT-04

        // 일반 비용(골드 = ×2: 2, 6, 12, 8, 4, 8, 6, 2, 8, 8, 10, 10 — TV-REW-01)
        static readonly int[] NormalCost = { 1, 3, 6, 4, 2, 4, 3, 1, 4, 4, 5, 5 };
        // TV-ELT-04 엘리트 비용: 3, 9, 18, 12, 6, 12, 9, 3, 12, 12, 15, 15
        static readonly int[] EliteCost = { 3, 9, 18, 12, 6, 12, 9, 3, 12, 12, 15, 15 };
        static readonly int[] EliteGold = { 6, 18, 36, 24, 12, 24, 18, 6, 24, 24, 30, 30 };

        [Test]
        public void S05_TV_ELT_04__EliteCostTimesThree()
        {
            var k = new Kit();
            for (int i = 0; i < Species.Length; i++)
            {
                var n = k.Spawn(Species[i], 3 + i, 3);
                var e = k.Spawn(Species[i], 3 + i, 10, elite: true);
                Assert.AreEqual(NormalCost[i], n.Cost, Species[i]);
                Assert.AreEqual(EliteCost[i], e.Cost, Species[i]);
                Assert.IsTrue(e.IsElite && e.Has(EnemyTags.Elite), Species[i]);
                Assert.AreEqual(Ids.Enemy.EliteOf(Species[i]), e.Elite.Id);
                // 속도·약점·저항은 바탕과 같다
                Assert.AreEqual(n.MoveSpeed, e.MoveSpeed);
                Assert.AreEqual(n.Weak, e.Weak);
                Assert.AreEqual(n.Resist, e.Resist);
            }
        }

        [Test]
        public void S05_TV_ELT_04__KillEventCarriesGoldAndKind()
        {
            var k = new Kit();
            for (int i = 0; i < Species.Length; i++)
            {
                var n = k.Spawn(Species[i], 3 + i, 3);
                var e = k.Spawn(Species[i], 3 + i, 10, elite: true);
                n.PendingDeath = true;
                e.PendingDeath = true;
                k.Sys.Stage10_Deaths(k.W);
                SimEvent ev;
                Assert.IsTrue(k.TryFindEvent(SimEventType.EnemyKilled, n.Id, out ev));
                Assert.AreEqual(NormalCost[i] * 2, ev.A, Species[i]);
                Assert.AreEqual(0, ev.B);
                Assert.IsTrue(k.TryFindEvent(SimEventType.EnemyKilled, e.Id, out ev));
                Assert.AreEqual(EliteGold[i], ev.A, Species[i]);
                Assert.AreEqual(1, ev.B);
            }
        }

        // ------------------------------------------------------------ 5절 꼬리표 표

        public enum Row { Normal, Elite, Heavy, Boss, Rooted, Flying }

        static EnemyState Make(Kit k, Row row)
        {
            switch (row)
            {
                case Row.Normal: return k.Spawn(Ids.Enemy.SlimeMoss, 12, 4);
                case Row.Elite: return k.Spawn(Ids.Enemy.SlimeMoss, 12, 4, elite: true);
                case Row.Heavy: return k.Spawn(Ids.Enemy.GolemMenhir, 12, 4);
                case Row.Boss: return k.AddBossBody(12, 4);
                case Row.Rooted: return k.Spawn(Ids.Enemy.VineThorn, 12, 4);
                default: return k.Spawn(Ids.Enemy.BatDusk, 12, 4);
            }
        }

        // ② 반동 사격 넉백 2칸: 일반 2 / 엘리트 1 / 중량·보스·뿌리 없음 / 비행 2
        [TestCase(Row.Normal, 2f)]
        [TestCase(Row.Elite, 1f)]
        [TestCase(Row.Heavy, 0f)]
        [TestCase(Row.Boss, 0f)]
        [TestCase(Row.Rooted, 0f)]
        [TestCase(Row.Flying, 2f)]
        public void S05_TAG__RecoilKnockback(Row row, float expected)
        {
            var k = new Kit();
            var e = Make(k, row);
            Vector2 from = e.Pos;
            float got = k.Sys.ApplyCc(k.W, e, CcKind.Knockback, 2f, CcSource.Recoil, new Vector2(1, 0));
            Assert.AreEqual(expected, got, 1e-4f);
            Kit.AreClose(from + new Vector2(expected, 0), e.Pos);
        }

        // 끓어넘침 넉백 2칸: 같은 줄
        [TestCase(Row.Normal, 2f)]
        [TestCase(Row.Elite, 1f)]
        [TestCase(Row.Heavy, 0f)]
        [TestCase(Row.Boss, 0f)]
        [TestCase(Row.Rooted, 0f)]
        [TestCase(Row.Flying, 2f)]
        public void S05_TAG__OverflowKnockback(Row row, float expected)
        {
            var k = new Kit();
            var e = Make(k, row);
            Assert.AreEqual(expected, k.Sys.ApplyCc(k.W, e, CcKind.Knockback, 2f, CcSource.Overflow, new Vector2(0, 1)), 1e-4f);
        }

        // ⑨ 뇌광창 기절 1초: 일반 60 / 엘리트 30 / 중량 없음 / 보스 18틱 동작 멈춤 / 뿌리 60 / 비행 60
        [TestCase(Row.Normal, 60)]
        [TestCase(Row.Elite, 30)]
        [TestCase(Row.Heavy, 0)]
        [TestCase(Row.Rooted, 60)]
        [TestCase(Row.Flying, 60)]
        public void S05_TAG__ThunderSpearStun(Row row, int ticks)
        {
            var k = new Kit();
            var e = Make(k, row);
            float got = k.Sys.ApplyCc(k.W, e, CcKind.Stun, 60f, CcSource.ThunderSpear, Vector2.zero);
            Assert.AreEqual(ticks, got, 1e-4f);
            Assert.AreEqual(ticks > 0 ? k.W.Tick + ticks : 0, e.StunUntilTick);
        }

        [Test]
        public void S05_TAG__ThunderSpearHaltsBoss18Ticks()
        {
            var k = new Kit();
            var boss = k.AddBossBody(12, 4);
            k.Sys.ApplyCc(k.W, boss, CcKind.Stun, 60f, CcSource.ThunderSpear, Vector2.zero);
            Assert.AreEqual(1, k.Bosses.Halts.Count);
            Assert.AreEqual(18, k.Bosses.Halts[0].Ticks);
            Assert.AreEqual(boss.Id, k.Bosses.Halts[0].EnemyId);
            Assert.AreEqual(0, boss.StunUntilTick, "보스는 기절 면역");
            // 뇌광창이 아닌 기절은 보스에게 아무 일도 없다
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, boss, CcKind.Stun, 60f, CcSource.Overflow, Vector2.zero));
            Assert.AreEqual(1, k.Bosses.Halts.Count);
        }

        // 불가마 감속 −20%: 일반·엘리트·중량·비행 −20% / 보스 −10% / 뿌리 해당 없음
        [TestCase(Row.Normal, 0.2f)]
        [TestCase(Row.Elite, 0.2f)]
        [TestCase(Row.Heavy, 0.2f)]
        [TestCase(Row.Boss, 0.1f)]
        [TestCase(Row.Rooted, 0f)]
        [TestCase(Row.Flying, 0.2f)]
        public void S05_TAG__KilnSlow(Row row, float expected)
        {
            var k = new Kit();
            var e = Make(k, row);
            k.Sys.ApplyCc(k.W, e, CcKind.Slow, 0.2f, CcSource.Kiln, Vector2.zero);
            Assert.AreEqual(expected, e.SlowStrength(k.W.Tick), 1e-5f);
        }

        // 벼락 질주 경직 0.6초(36틱): 일반 36 / 엘리트 18 / 중량·보스 없음 / 뿌리 36 / 비행 36
        [TestCase(Row.Normal, 36)]
        [TestCase(Row.Elite, 18)]
        [TestCase(Row.Heavy, 0)]
        [TestCase(Row.Boss, 0)]
        [TestCase(Row.Rooted, 36)]
        [TestCase(Row.Flying, 36)]
        public void S05_TAG__BoltDashStagger(Row row, int ticks)
        {
            var k = new Kit();
            var e = Make(k, row);
            Assert.AreEqual(ticks, k.Sys.ApplyCc(k.W, e, CcKind.Stagger, 36f, CcSource.BoltDash, Vector2.zero), 1e-4f);
            Assert.AreEqual(ticks > 0 ? ticks : 0, e.StaggerUntilTick);
        }

        // 살얼음 빙결 1.0초: 일반 60 / 엘리트 30 / 뿌리 60 / 비행 60(뜬 채)
        [TestCase(Row.Normal, 60)]
        [TestCase(Row.Elite, 30)]
        [TestCase(Row.Rooted, 60)]
        [TestCase(Row.Flying, 60)]
        public void S05_TAG__ThinIceFreeze(Row row, int ticks)
        {
            var k = new Kit();
            var e = Make(k, row);
            Assert.AreEqual(ticks, k.Sys.ApplyCc(k.W, e, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero), 1e-4f);
            Assert.AreEqual(ticks, e.FrozenUntilTick);
            Assert.IsFalse(e.CanAct(ticks - 1));
            Assert.IsTrue(e.CanAct(ticks));
        }

        // 살얼음: 중량은 감속 −30% 1.0초, 보스는 −15% 1.0초(빙결 없음)
        [TestCase(Row.Heavy, 0.3f)]
        [TestCase(Row.Boss, 0.15f)]
        public void S05_TAG__ThinIceBecomesSlowOnHeavyAndBoss(Row row, float slow)
        {
            var k = new Kit();
            var e = Make(k, row);
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, e, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.AreEqual(0, e.FrozenUntilTick);
            Assert.AreEqual(slow, e.SlowStrength(0), 1e-5f);
            Assert.AreEqual(slow, e.SlowStrength(59), 1e-5f);
            Assert.AreEqual(0f, e.SlowStrength(60), 1e-5f);
            Assert.AreEqual(slow, e.Slows[(int)SlowSlot.ThinIce].Strength, 1e-5f);
        }

        // 그림자 손 당김 3칸·경직 0.5초: 일반 3·30 / 엘리트 1.5·15 / 중량·보스 없음 / 뿌리 당김 없음·30 / 비행 3·30
        [TestCase(Row.Normal, 3f, 30)]
        [TestCase(Row.Elite, 1.5f, 15)]
        [TestCase(Row.Heavy, 0f, 0)]
        [TestCase(Row.Boss, 0f, 0)]
        [TestCase(Row.Rooted, 0f, 30)]
        [TestCase(Row.Flying, 3f, 30)]
        public void S05_TAG__ShadowHandPullAndStagger(Row row, float pull, int stagger)
        {
            var k = new Kit();
            var e = Make(k, row);
            Assert.AreEqual(pull, k.Sys.ApplyCc(k.W, e, CcKind.Pull, 3f, CcSource.ShadowHand, new Vector2(0, 1)), 1e-4f);
            Assert.AreEqual(stagger, k.Sys.ApplyCc(k.W, e, CcKind.Stagger, 30f, CcSource.ShadowHand, Vector2.zero), 1e-4f);
        }

        // 땅속: 피해 포함 전부 면역(TV-TAG-02: 지상에 솟으면 꼬리표 없음)
        [Test]
        public void S05_TAG__BurrowedImmuneToEverything()
        {
            var k = new Kit();
            var e = k.Spawn(Ids.Enemy.WeaselPot, 12, 4);
            Assert.IsTrue(e.Has(EnemyTags.Burrowed));
            Vector2 from = e.Pos;
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, e, CcKind.Knockback, 2f, CcSource.Recoil, new Vector2(1, 0)));
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, e, CcKind.Stun, 60f, CcSource.ThunderSpear, Vector2.zero));
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, e, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, e, CcKind.Stagger, 36f, CcSource.BoltDash, Vector2.zero));
            k.Sys.ApplySlow(k.W, e, SlowSlot.IceSpike, 0.3f, 60, CcSource.BlockSlow);
            k.Sys.ApplyCorrosion(k.W, e, 180);
            Assert.AreEqual(from, e.Pos);
            Assert.AreEqual(0f, e.SlowStrength(0));
            Assert.IsFalse(e.Corroded(0));
            Assert.IsTrue(e.CanAct(0));
        }

        [Test]
        public void S05_TAG__PendingDeathEnemyGetsNothing()
        {
            var k = new Kit();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 12, 4);
            e.PendingDeath = true;
            Vector2 from = e.Pos;
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, e, CcKind.Knockback, 2f, CcSource.Recoil, new Vector2(1, 0)));
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, e, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            k.Sys.ApplySlow(k.W, e, SlowSlot.Frost, 0.2f, 60, CcSource.BlockSlow);
            k.Sys.ApplyCorrosion(k.W, e, 180);
            Assert.IsFalse(k.Sys.TryCancelDash(k.W, e, 48));
            Assert.AreEqual(from, e.Pos);
            Assert.AreEqual(0, e.FrozenUntilTick);
            Assert.AreEqual(0f, e.SlowStrength(0));
            Assert.AreEqual(0, e.CorrosionUntilTick);
            Assert.AreEqual(0, k.CountEvents(SimEventType.EnemyCc));
        }

        // 빙결 내성 6초(360틱), 기준 = 빙결 시작 틱(U-67)
        [Test]
        public void S05_U67__FreezeImmunity360TicksFromFreezeStart()
        {
            var k = new Kit();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 3, 3);
            e.MoveSpeed = 0f;
            Assert.AreEqual(60f, k.Sys.ApplyCc(k.W, e, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero));
            Assert.AreEqual(360, e.FreezeImmuneUntilTick);
            k.RunBefore(359);
            Assert.AreEqual(0f, k.Sys.ApplyCc(k.W, e, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero), "359틱: 아직 내성");
            k.RunBefore(360);
            Assert.AreEqual(60f, k.Sys.ApplyCc(k.W, e, CcKind.Freeze, 60f, CcSource.ThinIce, Vector2.zero), "360틱: 다시 언다");
            Assert.AreEqual(420, e.FrozenUntilTick);
        }

        // 넉백은 벽에서 멈춘다
        [Test]
        public void S05_TAG__KnockbackStopsAtWall()
        {
            var k = new Kit();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 23f, 7f);
            float moved = k.Sys.ApplyCc(k.W, e, CcKind.Knockback, 2f, CcSource.Recoil, new Vector2(1, 0));
            Assert.AreEqual(k.W.ArenaWidth - e.BodyRadius, e.Pos.x, 1e-4f);
            Assert.AreEqual(k.W.ArenaWidth - e.BodyRadius - 23f, moved, 1e-4f);
        }

        // ------------------------------------------------------------ 03 요약서 10절 감속·잠식

        // TV-I01: 얼음 송곳(−30%, 60틱) 뒤 30틱에 서리(−20%, 60틱) → 0~59틱 −30%, 60~89틱 −20%, 90틱부터 0
        [Test]
        public void S02_TV_I01__SlowSlotsTakeLargestNotSum()
        {
            var k = new Kit();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 3, 3);
            e.MoveSpeed = 0f;
            k.Sys.ApplySlow(k.W, e, SlowSlot.IceSpike, 0.3f, 60, CcSource.BlockSlow);
            k.RunBefore(30);
            k.Sys.ApplySlow(k.W, e, SlowSlot.Frost, 0.2f, 60, CcSource.BlockSlow);
            Assert.AreEqual(0.3f, e.SlowStrength(30), 1e-6f);
            Assert.AreEqual(0.3f, e.SlowStrength(59), 1e-6f);
            Assert.AreEqual(0.2f, e.SlowStrength(60), 1e-6f);
            Assert.AreEqual(0.2f, e.SlowStrength(89), 1e-6f);
            Assert.AreEqual(0f, e.SlowStrength(90), 1e-6f);
        }

        // 보스 절반(−15%, −10%), 엘리트·중량은 그대로. 같은 출처는 시간(과 세기)만 새로.
        [Test]
        public void S02_SLOW__BossHalfEliteHeavyFullSameSlotOverwrites()
        {
            var k = new Kit();
            var boss = k.AddBossBody(12, 3);
            k.Sys.ApplySlow(k.W, boss, SlowSlot.IceSpike, 0.3f, 60, CcSource.BlockSlow);
            Assert.AreEqual(0.15f, boss.SlowStrength(0), 1e-6f);
            k.Sys.ApplySlow(k.W, boss, SlowSlot.Frost, 0.2f, 60, CcSource.BlockSlow);
            Assert.AreEqual(0.1f, boss.Slows[(int)SlowSlot.Frost].Strength, 1e-6f);

            var elite = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5, elite: true);
            var heavy = k.Spawn(Ids.Enemy.GolemMenhir, 8, 5);
            k.Sys.ApplySlow(k.W, elite, SlowSlot.IceSpike, 0.3f, 60, CcSource.BlockSlow);
            k.Sys.ApplySlow(k.W, heavy, SlowSlot.IceSpike, 0.3f, 60, CcSource.BlockSlow);
            Assert.AreEqual(0.3f, elite.SlowStrength(0), 1e-6f);
            Assert.AreEqual(0.3f, heavy.SlowStrength(0), 1e-6f);

            k.Sys.ApplySlow(k.W, elite, SlowSlot.IceSpike, 0.3f, 100, CcSource.BlockSlow);
            Assert.AreEqual(100, elite.Slows[(int)SlowSlot.IceSpike].UntilTick);
        }

        // 잠식: 남은 180틱, 다시 걸면 시간만 180으로
        [Test]
        public void S02_CORROSION__ReapplyRefreshesOnly()
        {
            var k = new Kit();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 3, 3);
            e.MoveSpeed = 0f;
            k.Sys.ApplyCorrosion(k.W, e, 180);
            Assert.IsTrue(e.Corroded(179));
            Assert.IsFalse(e.Corroded(180));
            k.RunBefore(100);
            k.Sys.ApplyCorrosion(k.W, e, 180);
            Assert.AreEqual(280, e.CorrosionUntilTick);
        }

        // 감속은 적 이동 속도에 (1 − 실효 감속)으로 든다: 슬라임 3.0 × 0.7 = 2.1칸/초
        [Test]
        public void S02_SLOW__MoveSpeedTimesOneMinusSlow()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(20, 7);
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 4, 7);
            k.Sys.ApplySlow(k.W, e, SlowSlot.IceSpike, 0.3f, 600, CcSource.BlockSlow);
            k.RunThrough(59);
            Assert.AreEqual(4f + 2.1f, e.Pos.x, 1e-3f);
        }

        // ------------------------------------------------------------ 6.12절 버프 식, 6.4절 엄폐 자리, 06 1.4절 탄막

        // 버프 속도: 슬라임 3.6, 꽈리·박쥐·족제비 3.7, 늑대 4.5 그대로, 술사 3.0, 멧돼지 2.88
        [TestCase(Ids.Enemy.SlimeMoss, 3.6f)]
        [TestCase(Ids.Enemy.BugLantern, 3.7f)]
        [TestCase(Ids.Enemy.BatDusk, 3.7f)]
        [TestCase(Ids.Enemy.WeaselPot, 3.7f)]
        [TestCase(Ids.Enemy.WolfBramble, 4.5f)]
        [TestCase(Ids.Enemy.ShamanCap, 3.0f)]
        [TestCase(Ids.Enemy.BoarTusk, 2.88f)]
        public void S05_TOAD__BuffedSpeed(string id, float expected)
        {
            var toad = TestContent.Db.Enemy(Ids.Enemy.ToadDrum).Ai;
            float got = EnemyRules.BuffedSpeed(TestContent.Db.Enemy(id).MoveSpeed, toad.Value<float>("buff_speed_ratio"), toad.Value<float>("buff_speed_cap"));
            Assert.AreEqual(expected, got, 1e-4f);
        }

        // 버프 빈틈 = ceil(t × 7/10): 선돌지기 72 → 51, 늑대 90 → 63, 멧돼지 기절 120 → 84(Q-25)
        [TestCase(72, 51)]
        [TestCase(90, 63)]
        [TestCase(120, 84)]
        [TestCase(48, 34)]
        [TestCase(60, 42)]
        public void S05_TOAD__BuffedRecoveryCeil(int ticks, int expected)
        {
            float ratio = TestContent.Db.Enemy(Ids.Enemy.ToadDrum).Ai.Value<float>("buff_recovery_ratio");
            Assert.AreEqual(expected, EnemyRules.BuffedRecovery(ticks, ratio));
        }

        // 6.4절 벡터: 선돌지기(10,7), P(4,7) → (11.5,7). 가시덩굴(6,3), P(6,9) → (6,1.5)
        [Test]
        public void S05_SHAMAN__CoverPointVectors()
        {
            float offset = TestContent.Db.Enemy(Ids.Enemy.ShamanCap).Ai.Value<float>("cover_offset");
            Kit.AreClose(new Vector2(11.5f, 7f), EnemyRules.CoverPoint(new Vector2(10, 7), new Vector2(4, 7), offset));
            Kit.AreClose(new Vector2(6f, 1.5f), EnemyRules.CoverPoint(new Vector2(6, 3), new Vector2(6, 9), offset));
        }

        // 느린 탄막: 탄 수 floor(n × 3/2)
        [TestCase(1, 1)]
        [TestCase(3, 4)]
        [TestCase(5, 7)]
        [TestCase(8, 12)]
        public void S07_SLOW_BARRAGE__CountFloorThreeHalves(int n, int expected)
        {
            Assert.AreEqual(expected, EnemyRules.BarrageCount(n, 1.5f));
            Assert.AreEqual(n, EnemyRules.BarrageCount(n, 1f));
        }

        // 같은 방향으로 이어 쏘는 탄의 간격 = ceil(1.0칸 ÷ (탄 속도 × 0.8)): 도깨비불 4.5 → 17틱, 술사 6.5 → 12틱, 가시 16 → 5틱
        [TestCase(4.5f, 17)]
        [TestCase(6.5f, 12)]
        [TestCase(16f, 5)]
        [TestCase(5f, 15)]
        public void S07_SLOW_BARRAGE__GapTicksKeepOneTile(float speed, int expected)
        {
            int gap = EnemyRules.BarrageGapTicks(1.0f, speed, 0.8f);
            Assert.AreEqual(expected, gap);
            Assert.GreaterOrEqual(gap * speed * 0.8f / 60f, 1.0f - 1e-4f);
            Assert.Less((gap - 1) * speed * 0.8f / 60f, 1.0f);
        }

        // ------------------------------------------------------------ 6.2절 늑대 회피 조건식

        [Test]
        public void S05_WOLF__DodgeConditionFormula()
        {
            Vector2 dir;
            // 탄이 14칸/초로 정면에서 온다. 거리 3칸 → 0.214초 뒤 도달(≤ 0.25) → 회피. 정면이면 탄 진행 방향의 왼쪽.
            Assert.IsTrue(EnemyRules.WolfShouldDodge(new Vector2(5, 5), Vector2.zero, 0.40f, new Vector2(2, 5), new Vector2(14, 0), 0.12f, 0.25f, out dir));
            Kit.AreClose(new Vector2(0, -1), dir);
            // 거리 4칸 → 0.286초 뒤(> 0.25) → 아직 아님
            Assert.IsFalse(EnemyRules.WolfShouldDodge(new Vector2(6, 5), Vector2.zero, 0.40f, new Vector2(2, 5), new Vector2(14, 0), 0.12f, 0.25f, out dir));
            // 스쳐 지나가는 탄: 옆으로 0.53칸(> 몸 0.40 + 탄 0.12) → 회피 안 함
            Assert.IsFalse(EnemyRules.WolfShouldDodge(new Vector2(5, 5.53f), Vector2.zero, 0.40f, new Vector2(2, 5), new Vector2(14, 0), 0.12f, 0.25f, out dir));
            // 0.5칸 옆이면 맞는다 → 탄에서 멀어지는 쪽(아래 = +y)으로
            Assert.IsTrue(EnemyRules.WolfShouldDodge(new Vector2(5, 5.5f), Vector2.zero, 0.40f, new Vector2(2, 5), new Vector2(14, 0), 0.12f, 0.25f, out dir));
            Kit.AreClose(new Vector2(0, 1), dir);
            // 멀어지는 탄은 t* = 0에서 가장 가깝다 → 이미 떨어져 있으면 회피 안 함
            Assert.IsFalse(EnemyRules.WolfShouldDodge(new Vector2(5, 5), Vector2.zero, 0.40f, new Vector2(7, 5), new Vector2(14, 0), 0.12f, 0.25f, out dir));
            // 늑대가 4.5로 탄 쪽으로 달리면 상대 속도 18.5 → 거리 4칸도 0.216초
            Assert.IsTrue(EnemyRules.WolfShouldDodge(new Vector2(6, 5), new Vector2(-4.5f, 0), 0.40f, new Vector2(2, 5), new Vector2(14, 0), 0.12f, 0.25f, out dir));
        }

        // 02 요약서 8.5절 ResolveCc 표(엘리트 + 중량은 중량 줄)
        [Test]
        public void S04_RESOLVE_CC__EliteHeavyUsesHeavyRow()
        {
            float a;
            var tags = EnemyTags.Elite | EnemyTags.Heavy;
            Assert.AreEqual(CcVerdict.None, EnemyRules.ResolveCc(CcKind.Knockback, tags, CcSource.Recoil, 2f, out a));
            Assert.AreEqual(CcVerdict.None, EnemyRules.ResolveCc(CcKind.Stun, tags, CcSource.ThunderSpear, 60f, out a));
            Assert.AreEqual(CcVerdict.None, EnemyRules.ResolveCc(CcKind.Stagger, tags, CcSource.BoltDash, 36f, out a));
            Assert.AreEqual(CcVerdict.ConvertToSlow, EnemyRules.ResolveCc(CcKind.Freeze, tags, CcSource.ThinIce, 60f, out a));
            Assert.AreEqual(60f, a);
            Assert.AreEqual(CcVerdict.Apply, EnemyRules.ResolveCc(CcKind.Slow, tags, CcSource.Kiln, 0.2f, out a));
            Assert.AreEqual(0.2f, a);
        }
    }
}
