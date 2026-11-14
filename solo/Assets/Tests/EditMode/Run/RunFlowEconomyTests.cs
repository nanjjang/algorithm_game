using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.RunModuleTests
{
    /// <summary>원정 지갑, 처치·클리어 골드, 기회 목록, 점수 재료, 체력 조각, 전리품 카드. 06 요약서 4.2·4.4·4.5·5.1절.</summary>
    public class RunFlowEconomyTests
    {
        // 06 요약서 3.1절의 예산 B(w): w ≤ 12는 10 + 6w, 그 뒤 82 + 4(w − 12), 상한 200.
        static int Budget(int w) => w <= 12 ? 10 + 6 * w : Mathf.Min(200, 82 + 4 * (w - 12));

        static void KillBudget(Rig r, int budget)
        {
            for (int i = 0; i < budget; i++) r.Kill(1);
        }

        // TV-173: 일반 웨이브 처치 + 클리어 = 2B + 20 (w1 52, w5 100, w9 148, w14 200, w42+ 420)
        [TestCase(1, 52)]
        [TestCase(5, 100)]
        [TestCase(9, 148)]
        [TestCase(14, 200)]
        [TestCase(42, 420)]
        [TestCase(57, 420)]
        public void S07_TV_173__NormalWaveGold_KillsPlusClear(int wave, int expected)
        {
            var r = Rig.InCombatAt(wave);
            KillBudget(r, Budget(wave));
            r.ClearWave();
            Assert.AreEqual(expected, r.Run.Purse);
            Assert.AreEqual(expected, r.Run.WaveResults[0].GoldEarned);
        }

        // TV-173: w20 보스 600 (클리어 20 + 보스 20 × (9 + 20) = 580) / S09 T070: 웨이브 10 보스 400
        [TestCase(10, 400)]
        [TestCase(20, 600)]
        [TestCase(30, 800)]
        [TestCase(50, 1200)]
        public void S07_TV_173__BossWaveGold(int wave, int expected)
        {
            var r = Rig.InCombatAt(wave);
            r.ClearWave();
            Assert.AreEqual(expected, r.Run.Purse);
        }

        // 01 요약서 8절 S09 T070: 웨이브 1 → 52, 5 → 100, 9 → 148, 10(보스) → 400
        [Test]
        public void S09_T070__WaveGoldVector()
        {
            int[] waves = { 1, 5, 9 };
            int[] gold = { 52, 100, 148 };
            for (int i = 0; i < waves.Length; i++)
            {
                var r = Rig.InCombatAt(waves[i]);
                KillBudget(r, Budget(waves[i]));
                r.ClearWave();
                Assert.AreEqual(gold[i], r.Run.Purse, "웨이브 " + waves[i]);
            }
            var boss = Rig.InCombatAt(10);
            boss.ClearWave();
            Assert.AreEqual(400, boss.Run.Purse);
        }

        // TV-174: 누적 w1~n: 5 → 380, 8 → 752, 10 → 1,300 (웨이브 10 = 가마등)
        [Test]
        public void S07_TV_174__CumulativeGold()
        {
            var r = Rig.InCombatAt(1);
            var at = new Dictionary<int, int> { { 5, 380 }, { 8, 752 }, { 10, 1300 } };
            for (int wave = 1; wave <= 10; wave++)
            {
                Assert.AreEqual(wave, r.W.Wave.Wave);
                if (!DayCycle.IsBossWave(wave)) KillBudget(r, Budget(wave));
                r.ClearWave();
                int expected;
                if (at.TryGetValue(wave, out expected)) Assert.AreEqual(expected, r.Run.Purse, "웨이브 " + wave + "까지");
                if (wave == 10) break;
                r.StepUntil(RunPhase.LootPick);
                r.PickAndConfirm();
                // 보관함 넘침(버림 골드)이 누적 값을 흔들지 않게 비운다. 체력은 가득이라 회복·조각도 없다.
                r.Run.Storage.Clear();
                r.Depart();
            }
            Assert.AreEqual(10, r.Run.ClearedDirect);
        }

        // 05 요약서 3.4절 TV-REW-01: 처치 골드 = cost × 2, 엘리트 × 3 (슬라임 엘리트 6, 선돌지기 엘리트 36)
        [TestCase(1, false, 2)]
        [TestCase(3, false, 6)]
        [TestCase(6, false, 12)]
        [TestCase(5, false, 10)]
        [TestCase(1, true, 6)]
        [TestCase(6, true, 36)]
        public void S05_TV_REW_01__KillGoldGoesToPurseAtKill(int cost, bool elite, int expected)
        {
            var r = Rig.InCombatAt(2);
            r.Kill(cost, elite);
            Assert.AreEqual(expected, r.Run.Purse, "처치 순간 지갑에 들어간다");
            SimEvent ev;
            Assert.IsTrue(r.TryLast(SimEventType.GoldGained, out ev));
            Assert.AreEqual(expected, ev.A);
            Assert.IsTrue(r.TryLast(SimEventType.PurseChanged, out ev));
            Assert.AreEqual(expected, ev.A);
        }

        [Test]
        public void S07_4_5__NoRewardEnemies_GiveNothing()
        {
            var r = Rig.InCombatAt(2);
            r.P.Hp = 100;
            for (int i = 0; i < 200; i++) r.Kill(4, false, noReward: true);
            Assert.AreEqual(0, r.Run.Purse);
            Assert.AreEqual(0, r.W.Wave.Stats.KillScoreX2);
            Assert.AreEqual(0, r.W.Wave.Stats.KillCount);
            Assert.AreEqual(0, r.W.Drops.Count);
            Assert.AreEqual(0, r.Run.DropChances.Count);
        }

        // 05 요약서 3.4절: 점수 재료 = cost × 10(엘리트 × 3), 지체(120초 = 7,200틱) 뒤 절반. 2배 정수로 든다.
        [Test]
        public void S05_3_4__KillScoreMaterial_HalvedAfterStall()
        {
            var r = Rig.InCombatAt(2);
            r.Kill(3);
            Assert.AreEqual(3 * 10 * 2, r.W.Wave.Stats.KillScoreX2);
            r.Kill(2, elite: true);
            Assert.AreEqual(60 + 2 * 3 * 10 * 2, r.W.Wave.Stats.KillScoreX2);
            Assert.AreEqual(2, r.W.Wave.Stats.KillCount);
            Assert.AreEqual(1, r.W.Wave.Stats.ElitesKilled);

            r.W.Wave.WaveTick = 7199;
            r.Kill(1);
            Assert.AreEqual(180 + 20, r.W.Wave.Stats.KillScoreX2, "7,199틱은 아직 온 점수");
            r.W.Wave.WaveTick = 7200;
            r.Kill(1);
            Assert.AreEqual(200 + 10, r.W.Wave.Stats.KillScoreX2, "7,200틱부터 절반");
            r.W.Wave.WaveTick = 0;
            r.W.Wave.Stalled = true;
            r.Kill(1, elite: true);
            Assert.AreEqual(210 + 30, r.W.Wave.Stats.KillScoreX2);
            Assert.AreEqual(5, r.Run.TotalKills);
        }

        // TV-175: 결정 4개 → 20골드, 빚 1이면 15
        [TestCase(4, 0, 20)]
        [TestCase(4, 1, 15)]
        [TestCase(0, 0, 0)]
        public void S07_TV_175__CrystalExchangeAtWaveEnd(int crystals, int debt, int expectedGold)
        {
            var r = Rig.InCombatAt(2);
            r.P.Crystals = crystals;
            r.P.Debt = debt;
            r.ClearWave();
            Assert.AreEqual(20 + expectedGold, r.Run.Purse, "클리어 20 + 환전");
            Assert.AreEqual(0, r.P.Crystals);
        }

        // 10 요약서 6절: 그믐 빚 > 결정 → 환전 0, 빚 유지
        [Test]
        public void S07_16__DebtLargerThanCrystals_NoExchange_DebtStays()
        {
            var r = Rig.InCombatAt(2);
            r.P.Crystals = 1;
            r.P.Debt = 2;
            r.ClearWave();
            Assert.AreEqual(20, r.Run.Purse);
            Assert.AreEqual(2, r.P.Debt);
        }

        // ---- 기회 목록(06 요약서 4.2절 5, 5.1절)

        [Test]
        public void S07_5_1__FifthWaveChance_OnSlot5Only()
        {
            var r = Rig.InCombatAt(5);
            r.ClearWave();
            Assert.AreEqual(1, r.Run.DropChances.Count);
            Assert.AreEqual(DropChanceSource.FifthWave, r.Run.DropChances[0].Source);
            Assert.AreEqual(5, r.Run.DropChances[0].Wave);
            Assert.AreEqual(1, r.Run.DropChances[0].Count);

            var w15 = Rig.InCombatAt(15);
            w15.ClearWave();
            Assert.AreEqual(DropChanceSource.FifthWave, w15.Run.DropChances[0].Source);
            Assert.AreEqual(15, w15.Run.DropChances[0].Wave);

            var w4 = Rig.InCombatAt(4);
            w4.ClearWave();
            Assert.AreEqual(0, w4.Run.DropChances.Count);
        }

        [Test]
        public void S07_5_1__BossKillChance()
        {
            var r = Rig.InCombatAt(20);
            r.ClearWave();
            Assert.AreEqual(1, r.Run.DropChances.Count);
            Assert.AreEqual(DropChanceSource.BossKill, r.Run.DropChances[0].Source);
            Assert.AreEqual(20, r.Run.DropChances[0].Wave);
        }

        [Test]
        public void S07_5_1__EliteChance_CountsPerWave_AddedAtKill()
        {
            var r = Rig.InCombatAt(13);
            r.Kill(2, elite: true);
            Assert.AreEqual(1, r.Run.DropChances.Count, "엘리트 기회는 처치 때 바로");
            r.Kill(3, elite: true);
            Assert.AreEqual(1, r.Run.DropChances.Count);
            Assert.AreEqual(DropChanceSource.EliteKill, r.Run.DropChances[0].Source);
            Assert.AreEqual(13, r.Run.DropChances[0].Wave);
            Assert.AreEqual(2, r.Run.DropChances[0].Count, "마리 수");
            Assert.AreEqual(2, r.W.Drops.FindAll(d => d.Kind == DropKind.MaterialBundle).Count, "기회가 생길 때 꾸러미가 떨어진다");
        }

        [Test]
        public void S07_5_1__CrowBundle_FirstTimePastWave3_Once()
        {
            var r = Rig.InCombatAt(1, c => c.CrowBundlePending = true);
            for (int wave = 1; wave <= 4; wave++)
            {
                r.ClearWave();
                int crow = r.Run.DropChances.FindAll(c => c.Source == DropChanceSource.CrowBundle).Count;
                Assert.AreEqual(wave >= 3 ? 1 : 0, crow, "웨이브 " + wave);
                Assert.AreEqual(wave >= 3, r.Run.CrowBundleEarned);
                r.StepUntil(RunPhase.LootPick);
                r.PickAndConfirm();
                r.Depart();
            }
            Assert.AreEqual(3, r.Run.DropChances.Find(c => c.Source == DropChanceSource.CrowBundle).Wave);
            Assert.IsTrue(r.W.Mods.Run.BuildSummary(r.W).CrowBundleEarned);

            var notPending = Rig.InCombatAt(3);
            notPending.ClearWave();
            Assert.AreEqual(0, notPending.Run.DropChances.Count);
            Assert.IsFalse(notPending.Run.CrowBundleEarned);
        }

        // ---- 훅

        [Test]
        public void S09_6_2__OnPlayerHitBreaksNoHit_OnGrazeCounts_NotAfterFinalRage()
        {
            var r = Rig.InCombatAt(10);
            Assert.IsTrue(r.W.Wave.Stats.NoHit);
            r.W.Mods.Run.OnGraze(r.W, r.P);
            r.W.Mods.Run.OnGraze(r.W, r.P);
            Assert.AreEqual(2, r.W.Wave.Stats.Grazes);
            r.W.Boss.FinalRage = true;
            r.W.Mods.Run.OnGraze(r.W, r.P);
            Assert.AreEqual(2, r.W.Wave.Stats.Grazes, "보스 최종 격노 뒤의 스침은 세지 않는다");
            r.W.Mods.Run.OnPlayerHit(r.W, r.P);
            Assert.IsFalse(r.W.Wave.Stats.NoHit);
        }

        [Test]
        public void S07_4_5__AddPurse_AddsAndEmits()
        {
            var r = Rig.InCombatAt(1);
            r.W.Mods.Run.AddPurse(r.W, 7, new Vector2(2f, 3f));
            Assert.AreEqual(7, r.Run.Purse);
            SimEvent ev;
            Assert.IsTrue(r.TryLast(SimEventType.GoldGained, out ev));
            Assert.AreEqual(7, ev.A);
            Assert.AreEqual(new Vector2(2f, 3f), ev.Pos);
        }

        // ---- 체력 조각(06 요약서 4.5절, TV-267~270)

        static uint NextDropsRoll(Rig r) => r.W.Rng(RngStream.Drops).NextU32();
        static uint FreshDropsRoll(Rig r) => Pcg32.Derive(r.W.Seed, RngStream.Drops).NextU32();

        [Test]
        public void S07_TV_267__HealthShard_FullHp_NoRollAtAll()
        {
            var r = Rig.InCombatAt(2);
            Assert.AreEqual(r.P.MaxHp, r.P.Hp);
            for (int i = 0; i < 50; i++) r.Kill(6, elite: true);
            Assert.AreEqual(0, r.W.Drops.FindAll(d => d.Kind == DropKind.HealthShard).Count);
            Assert.AreEqual(FreshDropsRoll(r), NextDropsRoll(r), "체력이 가득이면 추첨 자체를 하지 않는다(난수를 쓰지 않는다)");
        }

        [Test]
        public void S07_TV_268__HealthShard_OnePerWave_ThenNoRoll()
        {
            var r = Rig.InCombatAt(2);
            r.P.Hp = 100;
            int guard = 0;
            while (!r.W.Wave.HealthShardDropped && guard++ < 5000) r.Kill(1, elite: true);
            Assert.IsTrue(r.W.Wave.HealthShardDropped);
            var shards = r.W.Drops.FindAll(d => d.Kind == DropKind.HealthShard);
            Assert.AreEqual(1, shards.Count);
            Assert.AreEqual(new Vector2(3f, 3f), shards[0].Pos, "죽은 자리에 떨어진다");

            // 이미 나온 웨이브: 더 굴리지 않는다
            var probe = Pcg32.Derive(r.W.Seed, RngStream.Drops);
            for (int i = 0; i < guard; i++) probe.Range(0, 10000);
            for (int i = 0; i < 200; i++) r.Kill(6, elite: true);
            Assert.AreEqual(1, r.W.Drops.FindAll(d => d.Kind == DropKind.HealthShard).Count);
            Assert.AreEqual(probe.NextU32(), NextDropsRoll(r));
        }

        // 엘리트 10%(1000bp), 일반 30 × cost bp
        [Test]
        public void S07_TV_269__HealthShard_Chance_Elite10Percent_Normal30BpPerCost()
        {
            var r = Rig.InCombatAt(2);
            r.P.Hp = 100;
            int eliteDrops = 0, n = 4000;
            for (int i = 0; i < n; i++)
            {
                r.W.Wave.HealthShardDropped = false;
                r.Kill(1, elite: true);
                if (r.W.Wave.HealthShardDropped) eliteDrops++;
            }
            Assert.That(eliteDrops / (float)n, Is.InRange(0.08f, 0.12f), "엘리트 10%");

            int normalDrops = 0;
            for (int i = 0; i < n; i++)
            {
                r.W.Wave.HealthShardDropped = false;
                r.Kill(10);
                if (r.W.Wave.HealthShardDropped) normalDrops++;
            }
            // 비용 10 → 300bp = 3%
            Assert.That(normalDrops / (float)n, Is.InRange(0.018f, 0.042f), "일반 0.3% × 비용");
        }

        [Test]
        public void S07_TV_270__HealthShard_PickupHealsOneCell_FullHpLeavesIt_AutoCollectAtClear()
        {
            var r = Rig.InCombatAt(2);
            r.P.Hp = 300;
            int guard = 0;
            while (!r.W.Wave.HealthShardDropped && guard++ < 5000) r.Kill(1, elite: true);
            var shard = r.W.Drops.Find(d => d.Kind == DropKind.HealthShard);
            Assert.IsNotNull(shard);

            // 멀리 있으면 줍지 않는다
            r.P.Pos = shard.Pos + new Vector2(5f, 0f);
            r.Step();
            Assert.IsFalse(shard.Picked);
            Assert.AreEqual(300, r.P.Hp);
            // 닿으면 체력 +1칸
            r.P.Pos = shard.Pos;
            r.Step();
            Assert.IsTrue(shard.Picked);
            Assert.AreEqual(400, r.P.Hp);
            Assert.AreEqual(0, r.W.Drops.FindAll(d => d.Kind == DropKind.HealthShard).Count, "주운 것은 치워진다");

            // 10 요약서 6절: 떨군 뒤 체력이 가득이면 바닥에 두고 클리어 때 회수(효과 없음)
            var full = Rig.InCombatAt(2);
            full.P.Hp = 700;
            guard = 0;
            while (!full.W.Wave.HealthShardDropped && guard++ < 5000) full.Kill(1, elite: true);
            var left = full.W.Drops.Find(d => d.Kind == DropKind.HealthShard);
            full.P.Hp = full.P.MaxHp;
            full.P.Pos = left.Pos;
            full.Step();
            Assert.IsFalse(left.Picked, "가득이면 줍지 않고 둔다");
            full.ClearWave();
            Assert.IsTrue(left.Picked);
            Assert.AreEqual(full.P.MaxHp, full.P.Hp);

            // U-18: 남아 있던 조각은 클리어 때 자동 회수 = 체력 +1칸
            var auto = Rig.InCombatAt(2);
            auto.P.Hp = 300;
            guard = 0;
            while (!auto.W.Wave.HealthShardDropped && guard++ < 5000) auto.Kill(1, elite: true);
            auto.P.Pos = new Vector2(20f, 12f);
            auto.ClearWave();
            Assert.AreEqual(400, auto.P.Hp);
        }

        // ---- 전리품 카드(06 요약서 4.4절)

        static Rig AtLoot(int wave, bool killElite = false, ulong seed = 7)
        {
            var r = Rig.Create(c => c.StartWave = wave, null, seed);
            if (wave > 1) r.Cmd(RunCommandKind.CampDepart);
            r.StepUntil(RunPhase.WaveCombat);
            if (killElite) r.Kill(2, elite: true);
            r.ToLoot();
            return r;
        }

        [Test]
        public void S07_4_4__CardCounts_Normal4_Elite5_Boss5_Pick2()
        {
            var normal = AtLoot(2);
            Assert.AreEqual(4, normal.Run.Loot.Cards.Count);
            Assert.AreEqual(2, normal.Run.Loot.PickCount);
            Assert.AreEqual(2, normal.Run.Loot.Wave);
            foreach (var c in normal.Run.Loot.Cards) Assert.IsFalse(c.Guaranteed);

            var elite = AtLoot(3, killElite: true);
            Assert.AreEqual(5, elite.Run.Loot.Cards.Count);
            Assert.AreEqual(2, elite.Run.Loot.PickCount);
            foreach (var c in elite.Run.Loot.Cards) Assert.IsFalse(c.Guaranteed);

            var boss = AtLoot(10);
            Assert.AreEqual(5, boss.Run.Loot.Cards.Count);
            Assert.AreEqual(2, boss.Run.Loot.PickCount);
            int guaranteed = 0;
            foreach (var c in boss.Run.Loot.Cards) if (c.Guaranteed) guaranteed++;
            Assert.AreEqual(1, guaranteed);
            Assert.IsTrue(boss.Run.Loot.Cards[4].Guaranteed, "확정 칸은 마지막 칸");
        }

        [Test]
        public void S07_4_4__CardsComeFromTheirRarityPool()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var r = AtLoot(35, false, seed);
                foreach (var c in r.Run.Loot.Cards)
                {
                    CollectionAssert.Contains(r.Db.BlocksOfRarity(c.Rarity), c.BlockId);
                    Assert.AreEqual(c.Rarity, r.Db.Block(c.BlockId).Rarity);
                }
            }
        }

        // TV-212: 1일째 확정 칸 = 희귀 100%
        [Test]
        public void S07_TV_212__GuaranteedSlot_Day1_AlwaysRare()
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var r = AtLoot(10, false, seed);
                Assert.AreEqual(Ids.Rarity.Rare, r.Run.Loot.Cards[4].Rarity, "씨앗 " + seed);
            }
        }

        // TV-213: 2일째 확정 칸 = Random(0,27) < 25 희귀, 아니면 영웅(일반 없음)
        [Test]
        public void S07_TV_213__GuaranteedSlot_Day2_RareOrHero_NeverCommon()
        {
            int rare = 0, hero = 0, n = 600;
            for (ulong seed = 1; seed <= (ulong)n; seed++)
            {
                var r = AtLoot(20, false, seed);
                string rarity = r.Run.Loot.Cards[4].Rarity;
                Assert.AreNotEqual(Ids.Rarity.Common, rarity);
                if (rarity == Ids.Rarity.Rare) rare++; else hero++;
            }
            Assert.AreEqual(n, rare + hero);
            // 영웅 2/27 ≈ 7.4%
            Assert.That(hero / (float)n, Is.InRange(0.035f, 0.12f));
        }

        // TV-214: w ≥ 30 보스의 확정 칸 = 영웅
        [TestCase(30)]
        [TestCase(40)]
        [TestCase(70)]
        public void S07_TV_214__GuaranteedSlot_Wave30Plus_Hero(int wave)
        {
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var r = AtLoot(wave, false, seed);
                Assert.AreEqual(Ids.Rarity.Hero, r.Run.Loot.Cards[4].Rarity);
            }
        }

        // 날별 등급 확률: 1일째 78/22/0, 4일째 이후 70/25/5
        [Test]
        public void S07_4_4__RarityOddsByDay()
        {
            int common = 0, rare = 0, hero = 0;
            for (ulong seed = 1; seed <= 500; seed++)
            {
                var r = AtLoot(3, false, seed);
                foreach (var c in r.Run.Loot.Cards)
                {
                    if (c.Rarity == Ids.Rarity.Common) common++;
                    else if (c.Rarity == Ids.Rarity.Rare) rare++;
                    else hero++;
                }
            }
            int total = common + rare + hero;
            Assert.AreEqual(2000, total);
            Assert.AreEqual(0, hero, "1일째에는 영웅이 없다");
            Assert.That(common / (float)total, Is.InRange(0.74f, 0.82f));

            common = rare = hero = 0;
            for (ulong seed = 1; seed <= 500; seed++)
            {
                var r = AtLoot(33, false, seed);
                foreach (var c in r.Run.Loot.Cards)
                {
                    if (c.Rarity == Ids.Rarity.Common) common++;
                    else if (c.Rarity == Ids.Rarity.Rare) rare++;
                    else hero++;
                }
            }
            total = common + rare + hero;
            Assert.That(common / (float)total, Is.InRange(0.66f, 0.74f));
            Assert.That(rare / (float)total, Is.InRange(0.21f, 0.29f));
            Assert.That(hero / (float)total, Is.InRange(0.03f, 0.075f));
        }

        // 빌린 블록: 1일째 웨이브 6에만, 영웅 4종 균등 1장. 시작 11 이후의 16·26엔 없다(10 요약서 6절)
        [Test]
        public void S07_4_4__BorrowedBlock_OnlyWave6_HeroGrade()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 80; seed++)
            {
                var r = AtLoot(6, false, seed);
                Assert.IsNotNull(r.Run.Loot.BorrowedBlockId);
                CollectionAssert.Contains(r.Db.BlocksOfRarity(Ids.Rarity.Hero), r.Run.Loot.BorrowedBlockId);
                seen.Add(r.Run.Loot.BorrowedBlockId);
            }
            Assert.AreEqual(4, seen.Count, "영웅 4종이 다 나온다");

            Assert.IsNull(AtLoot(5).Run.Loot.BorrowedBlockId);
            Assert.IsNull(AtLoot(16).Run.Loot.BorrowedBlockId);
            Assert.IsNull(AtLoot(26).Run.Loot.BorrowedBlockId);

            var got = AtLoot(6);
            got.PickAndConfirm();
            Assert.AreEqual(RunPhase.Camp, got.Run.Phase);
            Assert.AreEqual(3, got.Run.Storage.Count, "고른 2장 + 빌린 1장");
            var borrowed = got.Run.Storage.FindAll(b => b.Borrowed);
            Assert.AreEqual(1, borrowed.Count);
            Assert.AreEqual(Ids.Rarity.Hero, borrowed[0].Rarity);
            Assert.AreEqual(6, borrowed[0].AcquiredWave);
        }

        // 08 요약서 8.1절: 2장까지만 고른다, 확인은 2장을 채워야
        [Test]
        public void S13_8_1__PickTwo_ThirdIgnored_ConfirmNeedsTwo()
        {
            var r = AtLoot(2);
            r.Cmd(RunCommandKind.LootPick, 0);
            r.Cmd(RunCommandKind.LootConfirm);
            r.Step();
            Assert.AreEqual(RunPhase.LootPick, r.Run.Phase, "1장으로는 확인되지 않는다");
            Assert.AreEqual(0, r.Run.Storage.Count);

            r.Cmd(RunCommandKind.LootPick, 1);
            r.Cmd(RunCommandKind.LootPick, 2);
            r.Step();
            Assert.IsTrue(r.Run.Loot.Cards[0].Picked);
            Assert.IsTrue(r.Run.Loot.Cards[1].Picked);
            Assert.IsFalse(r.Run.Loot.Cards[2].Picked, "세 번째는 무시");

            r.Cmd(RunCommandKind.LootUnpick, 0);
            r.Cmd(RunCommandKind.LootPick, 3);
            r.Step();
            Assert.IsFalse(r.Run.Loot.Cards[0].Picked);
            Assert.IsTrue(r.Run.Loot.Cards[3].Picked);

            string id1 = r.Run.Loot.Cards[1].BlockId, id3 = r.Run.Loot.Cards[3].BlockId;
            r.Cmd(RunCommandKind.LootConfirm);
            r.Step();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.AreEqual(2, r.Run.Storage.Count);
            Assert.AreEqual(id1, r.Run.Storage[0].BlockId);
            Assert.AreEqual(id3, r.Run.Storage[1].BlockId);
            Assert.AreNotEqual(r.Run.Storage[0].Uid, r.Run.Storage[1].Uid);
            Assert.AreEqual(2, r.Run.Storage[0].AcquiredWave);
            Assert.IsNull(r.Run.Loot);
        }

        // TV-192: 지갑 < 30이면 다시 뽑기 불가 / 08 TV-062: 100 → 70
        [Test]
        public void S07_TV_192__Reroll_Needs30Gold_KeepsGuaranteedSlot()
        {
            var poor = AtLoot(10);
            poor.Run.Purse = 29;
            var before = new List<LootCard>(poor.Run.Loot.Cards);
            poor.Cmd(RunCommandKind.LootReroll);
            poor.Step();
            Assert.AreEqual(29, poor.Run.Purse);
            Assert.AreEqual(0, poor.Run.Loot.Rerolls);
            for (int i = 0; i < before.Count; i++) Assert.AreSame(before[i], poor.Run.Loot.Cards[i]);

            var r = AtLoot(10);
            r.Run.Purse = 100;
            r.Cmd(RunCommandKind.LootPick, 0);
            r.Step();
            var old = new List<LootCard>(r.Run.Loot.Cards);
            r.Cmd(RunCommandKind.LootReroll);
            r.Step();
            Assert.AreEqual(70, r.Run.Purse);
            Assert.AreEqual(1, r.Run.Loot.Rerolls);
            Assert.AreEqual(5, r.Run.Loot.Cards.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreNotSame(old[i], r.Run.Loot.Cards[i], "전부 새로 뽑는다");
                Assert.IsFalse(r.Run.Loot.Cards[i].Picked, "고른 것은 비운다");
            }
            Assert.IsTrue(r.Run.Loot.Cards[4].Guaranteed, "확정 칸은 유지");
            Assert.AreEqual(Ids.Rarity.Rare, r.Run.Loot.Cards[4].Rarity);
            // 횟수 무제한: 두 번 더
            r.Cmd(RunCommandKind.LootReroll);
            r.Cmd(RunCommandKind.LootReroll);
            r.Step();
            Assert.AreEqual(10, r.Run.Purse);
            Assert.AreEqual(3, r.Run.Loot.Rerolls);
            r.Cmd(RunCommandKind.LootReroll);
            r.Step();
            Assert.AreEqual(10, r.Run.Purse);
        }

        static void FillStorage(Rig r, int count, string blockId, string rarity, bool borrowed = false)
        {
            for (int i = 0; i < count; i++)
                r.Run.Storage.Add(new WildBlock { Uid = r.Run.NextWildUid++, BlockId = blockId, Rarity = rarity, Borrowed = borrowed, AcquiredWave = 1 });
        }

        // TV-224: 보관함 12칸이 넘치면 버릴 블록을 고른다. 버린 블록은 즉시 골드: 일반 5, 희귀 15, 영웅 50, 빌림 50
        [Test]
        public void S07_TV_224__Overflow_DiscardGivesGoldByGrade()
        {
            var r = AtLoot(2);
            FillStorage(r, 8, Ids.Block.MagicBolt, Ids.Rarity.Common);
            FillStorage(r, 1, Ids.Block.Fireball, Ids.Rarity.Rare);
            FillStorage(r, 1, Ids.Block.Homing, Ids.Rarity.Hero);
            FillStorage(r, 1, Ids.Block.Corrosion, Ids.Rarity.Hero, borrowed: true);
            FillStorage(r, 1, Ids.Block.Pierce, Ids.Rarity.Common);
            Assert.AreEqual(12, r.Run.Storage.Count);
            int purse = r.Run.Purse;

            r.Cmd(RunCommandKind.LootDiscard, 100);
            r.Step();
            Assert.AreEqual(12, r.Run.Storage.Count, "확정 전에는 버리지 못한다");

            r.PickAndConfirm();
            Assert.AreEqual(RunPhase.LootPick, r.Run.Phase, "버릴 블록을 골라야 야영으로 간다");
            Assert.AreEqual(2, r.Run.Loot.OverflowToDiscard);
            Assert.AreEqual(14, r.Run.Storage.Count, "지금 보관함 + 새 카드 중에서 고른다");

            r.Cmd(RunCommandKind.LootDiscard, 100 + 8);   // 희귀
            r.Step();
            Assert.AreEqual(purse + 15, r.Run.Purse);
            Assert.AreEqual(1, r.Run.Loot.OverflowToDiscard);
            Assert.AreEqual(RunPhase.LootPick, r.Run.Phase);

            r.Cmd(RunCommandKind.LootDiscard, 100 + 9);   // 빌린 블록(희귀가 빠져 한 칸 당겨졌다: 8 = 영웅, 9 = 빌림)
            r.Step();
            Assert.AreEqual(purse + 15 + 50, r.Run.Purse);
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.AreEqual(12, r.Run.Storage.Count);
            Assert.AreEqual(0, r.Run.Storage.FindAll(b => b.Borrowed).Count);
        }

        [TestCase("common", false, 5)]
        [TestCase("rare", false, 15)]
        [TestCase("hero", false, 50)]
        [TestCase("hero", true, 50)]
        public void S07_TV_224__DiscardGoldTable(string rarity, bool borrowed, int gold)
        {
            var r = AtLoot(2);
            FillStorage(r, 1, Ids.Block.MagicBolt, rarity, borrowed);
            FillStorage(r, 10, Ids.Block.MagicBolt, Ids.Rarity.Common);
            int purse = r.Run.Purse;
            r.PickAndConfirm();
            Assert.AreEqual(1, r.Run.Loot.OverflowToDiscard);
            r.Cmd(RunCommandKind.LootDiscard, 100);
            r.Step();
            Assert.AreEqual(purse + gold, r.Run.Purse);
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
        }

        [Test]
        public void S07_4_4__SameSeedSameCards()
        {
            var a = AtLoot(12, false, 99);
            var b = AtLoot(12, false, 99);
            Assert.AreEqual(a.Run.Loot.Cards.Count, b.Run.Loot.Cards.Count);
            for (int i = 0; i < a.Run.Loot.Cards.Count; i++)
            {
                Assert.AreEqual(a.Run.Loot.Cards[i].BlockId, b.Run.Loot.Cards[i].BlockId);
                Assert.AreEqual(a.Run.Loot.Cards[i].Rarity, b.Run.Loot.Cards[i].Rarity);
            }
        }
    }
}
