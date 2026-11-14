using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;

namespace Wizard.Solo.Tests.RunModuleTests
{
    /// <summary>
    /// 전리품 개편(사용자 조정 — DataSolo/Loot.json): 한 제시 안 중복 없음, 다시 뽑기 값 오름, 건너뛰기 보상, 맞물림·처음 발견,
    /// 운 보정(결정론), 같은 블록 겹치기 별(★). 기대값은 Loot.json의 값(30/+15, 10 + 2w, 1칸, 별 상한 3, 운 보정 2번 뒤 2500bp씩 7500까지).
    /// </summary>
    public class RunFlowLootTests
    {
        static Rig AtLoot(int wave, bool killElite = false, ulong seed = 7, System.Action<RunConfig> tweak = null)
        {
            var r = Rig.Create(c => { c.StartWave = wave; tweak?.Invoke(c); }, null, seed);
            if (wave > 1) r.Cmd(RunCommandKind.CampDepart);
            r.StepUntil(RunPhase.WaveCombat);
            if (killElite) r.Kill(2, elite: true);
            r.ToLoot();
            return r;
        }

        static void AssertDistinct(LootOffer offer, string what)
        {
            var seen = new HashSet<string>();
            foreach (var c in offer.Cards)
            {
                Assert.IsNotNull(c.BlockId, what + ": 빈 카드");
                Assert.IsTrue(seen.Add(c.BlockId), what + ": 한 제시 안에 " + c.BlockId + "가 두 번");
            }
        }

        // ---------------------------------------------------------------- 중복 금지

        [Test]
        public void Loot__NoDuplicateWithinOffer_IncludingRerolls()
        {
            int[] waves = { 2, 3, 10, 35 };
            for (ulong seed = 1; seed <= 150; seed++)
                foreach (int wave in waves)
                {
                    var r = AtLoot(wave, killElite: wave == 3, seed: seed);
                    AssertDistinct(r.Run.Loot, "씨앗 " + seed + " 웨이브 " + wave);
                    r.Run.Purse = 10000;
                    for (int k = 0; k < 3; k++)
                    {
                        r.Cmd(RunCommandKind.LootReroll);
                        r.Step();
                        AssertDistinct(r.Run.Loot, "씨앗 " + seed + " 웨이브 " + wave + " 다시 뽑기 " + (k + 1));
                    }
                    foreach (var c in r.Run.Loot.Cards)
                        Assert.AreEqual(r.Db.Block(c.BlockId).Rarity, c.Rarity, "카드 등급 = 블록 등급");
                }
        }

        // ---------------------------------------------------------------- 다시 뽑기 값

        [Test]
        public void Loot__RerollCostRisesPerReroll_ResetsNextOffer()
        {
            var r = AtLoot(2);
            Assert.AreEqual(30, r.Run.Loot.RerollCost);
            r.Run.Purse = 1000;
            for (int i = 0; i < 3; i++) { r.Cmd(RunCommandKind.LootReroll); r.Step(); }
            Assert.AreEqual(1000 - 30 - 45 - 60, r.Run.Purse);
            Assert.AreEqual(3, r.Run.Loot.Rerolls);
            Assert.AreEqual(75, r.Run.Loot.RerollCost);

            // 지금 값이 모자라면 거절(지갑 그대로)
            r.Run.Purse = 74;
            r.Cmd(RunCommandKind.LootReroll);
            r.Step();
            Assert.AreEqual(74, r.Run.Purse);
            Assert.AreEqual(3, r.Run.Loot.Rerolls);

            // 다음 제시는 처음 값으로
            r.ForceCards(Ids.Block.Fireball, Ids.Block.IceSpike);
            r.PickAndConfirm();
            r.Depart();
            r.ToLoot();
            Assert.AreEqual(30, r.Run.Loot.RerollCost);
            Assert.AreEqual(0, r.Run.Loot.Rerolls);
        }

        // ---------------------------------------------------------------- 건너뛰기

        [Test]
        public void Loot__SkipGold_SmallFixedReward_NoCards()
        {
            var r = AtLoot(5);
            Assert.AreEqual(10 + 2 * 5, r.Run.Loot.SkipGold);
            Assert.AreEqual(1, r.Run.Loot.SkipHealCells);
            int purse = r.Run.Purse;
            r.Cmd(RunCommandKind.LootPick, 0);
            r.W.Enqueue(new RunCommand(RunCommandKind.LootSkip, RunFlow.SkipModeGold, slot: 0));
            r.Step();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.AreEqual(purse + 20, r.Run.Purse);
            Assert.AreEqual(0, r.Run.Storage.Count, "카드는 받지 않는다");
            Assert.IsTrue(r.Run.Purse - purse < r.W.Mods.Run.HealCost(r.W), "건너뛰기 골드 < 회복 1회 값(늘 정답이 되지 않게 작다)");
        }

        [Test]
        public void Loot__SkipHeal_OneCell_RejectedWhenFull()
        {
            var full = AtLoot(4);
            Assert.AreEqual(full.P.MaxHp, full.P.Hp);
            full.W.Enqueue(new RunCommand(RunCommandKind.LootSkip, RunFlow.SkipModeHeal, slot: 0));
            full.Step();
            Assert.AreEqual(RunPhase.LootPick, full.Run.Phase, "체력이 가득하면 체력 건너뛰기는 거절");

            var hurt = AtLoot(4);
            hurt.P.Hp = 300;
            int heals = hurt.Player.HealCalls;
            hurt.W.Enqueue(new RunCommand(RunCommandKind.LootSkip, RunFlow.SkipModeHeal, slot: 0));
            hurt.Step();
            Assert.AreEqual(RunPhase.Camp, hurt.Run.Phase);
            Assert.AreEqual(heals + 1, hurt.Player.HealCalls);
            Assert.AreEqual(400, hurt.P.Hp, "1칸(100)");
        }

        [Test]
        public void Loot__SkipStillGivesBorrowedBlock()
        {
            var r = AtLoot(6);
            Assert.IsNotNull(r.Run.Loot.BorrowedBlockId);
            r.W.Enqueue(new RunCommand(RunCommandKind.LootSkip, RunFlow.SkipModeGold, slot: 0));
            r.Step();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.AreEqual(1, r.Run.Storage.Count);
            Assert.IsTrue(r.Run.Storage[0].Borrowed);
        }

        // ---------------------------------------------------------------- 별 겹치기

        [Test]
        public void Loot__SameBlockAgain_StarsUpInsteadOfNewCopy()
        {
            var r = AtLoot(2);
            r.Run.Storage.Add(new WildBlock { Uid = r.Run.NextWildUid++, BlockId = Ids.Block.Pierce, Rarity = Ids.Rarity.Common, AcquiredWave = 1 });
            r.ForceCards(Ids.Block.Pierce, Ids.Block.Frost);
            r.PickAndConfirm();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.AreEqual(2, r.Run.Storage.Count, "관통은 겹치고 서리만 새로");
            Assert.AreEqual(Ids.Block.Pierce, r.Run.Storage[0].BlockId);
            Assert.AreEqual(1, r.Run.Storage[0].Stars);
            Assert.AreEqual(0, r.Run.Storage[1].Stars);
        }

        [Test]
        public void Loot__EquippedLibraryCore_StarsUp_Recompiles_CapThree()
        {
            var r = AtLoot(2);
            Assert.AreEqual(Ids.Block.MagicBolt, r.P.Loadout.Core.BlockId);
            int recompiles = r.Player.RecompileCalls;
            r.ForceCards(Ids.Block.MagicBolt, Ids.Block.Fireball);
            r.PickAndConfirm();
            Assert.AreEqual(1, r.P.Loadout.Core.Stars, "지팡이 칸의 같은 블록이 먼저 자란다");
            Assert.AreEqual(recompiles + 1, r.Player.RecompileCalls, "지팡이 칸이 바뀌었으니 다시 컴파일");
            Assert.AreEqual(1, r.Run.Storage.Count);

            // 상한 3: 다 자란 블록은 건너뛰고(같은 블록이 더 없으면) 새로 들어간다
            var p = r.P.Loadout.Core; p.Stars = 3; r.P.Loadout.Core = p;
            r.Depart();
            r.ToLoot();
            r.ForceCards(Ids.Block.MagicBolt, Ids.Block.IceSpike);
            r.PickAndConfirm();
            Assert.AreEqual(3, r.P.Loadout.Core.Stars);
            Assert.AreEqual(3, r.Run.Storage.Count, "화염구 + 새 마력탄 + 얼음 송곳");
            Assert.AreEqual(0, r.Run.Storage.Find(b => b.BlockId == Ids.Block.MagicBolt).Stars);
        }

        [Test]
        public void Loot__CardShowsStarTarget_WhenOwned()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var r = AtLoot(2, seed: seed);
                foreach (var c in r.Run.Loot.Cards)
                {
                    bool owned = c.BlockId == Ids.Block.MagicBolt || c.BlockId == Ids.Block.SplitTwo;   // 출발 조립(서고 블록)
                    Assert.AreEqual(owned ? 1 : 0, c.StarTarget, "씨앗 " + seed + " " + c.BlockId);
                }
            }
        }

        [Test]
        public void Loot__StarsFollowBlockThroughMoves()
        {
            var r = AtLoot(2);
            r.ForceCards(Ids.Block.MagicBolt, Ids.Block.Pierce);
            r.PickAndConfirm();
            Assert.AreEqual(1, r.P.Loadout.Core.Stars);
            // 코어(서고 블록) → 대기 칸 → 다시 코어: 별이 따라간다
            r.Cmd(RunCommandKind.LoadoutMove, 0, -1); r.Step();
            Assert.AreEqual(1, r.Run.LibraryBench[0].Stars);
            r.Cmd(RunCommandKind.LoadoutMove, 300, 0); r.Step();
            Assert.AreEqual(1, r.P.Loadout.Core.Stars);
            // 보관함의 야생 블록 ★: 끼우고 빼도 그대로
            r.Run.Storage[0].Stars = 2;
            r.Cmd(RunCommandKind.LoadoutMove, 100, 2); r.Step();
            Assert.AreEqual(Ids.Block.Pierce, r.P.Loadout.Links[1].BlockId);
            Assert.AreEqual(2, r.P.Loadout.Links[1].Stars);
            r.Cmd(RunCommandKind.LoadoutMove, 2, -1); r.Step();
            Assert.AreEqual(2, r.Run.Storage[0].Stars);
            Assert.AreEqual(2, r.W.Mods.Run.BuildSummary(r.W).WildBlocks.Find(b => b.BlockId == Ids.Block.Pierce).Stars);
        }

        // ---------------------------------------------------------------- 맞물림

        [Test]
        public void Loot__SynergyRules()
        {
            var db = TestContent.Db;
            var t = LootTuning.From(db);
            string partner;
            var bolt = Lo(Ids.Block.MagicBolt);
            Assert.AreEqual(LootSynergy.Pair, LootRules.Synergy(db, t, bolt, Ids.Block.Pierce, out partner));
            Assert.AreEqual(Ids.Block.MagicBolt, partner);
            Assert.AreEqual(LootSynergy.Pair, LootRules.Synergy(db, t, bolt, Ids.Block.SplitTwo, out partner));
            Assert.AreEqual(LootSynergy.None, LootRules.Synergy(db, t, bolt, Ids.Block.Corrosion, out partner));
            Assert.AreEqual(LootSynergy.None, LootRules.Synergy(db, t, bolt, Ids.Block.MagicBolt, out partner), "같은 블록은 맞물림이 아니라 별 겹치기");
            // 같은 속성의 변형이 그 투사체에 효과가 있으면 속성 맞물림(화염구 + 크기 2배)
            var fire = Lo(Ids.Block.Fireball);
            Assert.AreEqual(LootSynergy.Element, LootRules.Synergy(db, t, fire, Ids.Block.DoubleSize, out partner));
            Assert.AreEqual(Ids.Block.Fireball, partner);
            // 효과 없는 조합은 같은 속성이어도 아니다(유도는 연쇄 번개에 효과 없음)
            Assert.AreEqual(LootSynergy.None, LootRules.Synergy(db, t, Lo(Ids.Block.ChainLightning), Ids.Block.Homing, out partner));
            // 연결 칸의 변형과 짝인 투사체 카드(관통이 끼워져 있으면 얼음 송곳이 맞물림)
            Assert.AreEqual(LootSynergy.Pair, LootRules.Synergy(db, t, Lo(Ids.Block.Fireball, Ids.Block.Pierce), Ids.Block.IceSpike, out partner));
            Assert.AreEqual(Ids.Block.Pierce, partner);
        }

        static RunLoadout Lo(string core, params string[] links)
        {
            var lo = new RunLoadout { Core = new SlotItem { BlockId = core }, Links = new SlotItem[4] };
            for (int i = 0; i < links.Length; i++) lo.Links[i] = new SlotItem { BlockId = links[i] };
            return lo;
        }

        [Test]
        public void Loot__SynergyFirst_OnlyFirstTimeInRun()
        {
            int checkedFirst = 0, checkedRepeat = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var r = AtLoot(2, seed: seed);
                r.Run.Purse = 100000;
                var seen = new HashSet<string>();
                for (int k = 0; k < 8; k++)
                {
                    foreach (var c in r.Run.Loot.Cards)
                    {
                        if (c.Synergy == LootSynergy.None) { Assert.IsFalse(c.SynergyFirst); continue; }
                        string key = LootRules.SynergyKey(c.BlockId, c.SynergyPartnerId);
                        bool first = seen.Add(key);
                        Assert.AreEqual(first, c.SynergyFirst, "씨앗 " + seed + " " + key);
                        if (first) checkedFirst++; else checkedRepeat++;
                    }
                    r.Cmd(RunCommandKind.LootReroll);
                    r.Step();
                }
            }
            Assert.Greater(checkedFirst, 0);
            Assert.Greater(checkedRepeat, 0);
        }

        // ---------------------------------------------------------------- 운 보정

        [Test]
        public void Loot__PityBp_Formula()
        {
            var t = LootTuning.From(TestContent.Db);
            Assert.AreEqual(0, t.PityBp(0));
            Assert.AreEqual(0, t.PityBp(1));
            Assert.AreEqual(2500, t.PityBp(2));
            Assert.AreEqual(5000, t.PityBp(3));
            Assert.AreEqual(7500, t.PityBp(4));
            Assert.AreEqual(7500, t.PityBp(9), "상한");
        }

        /// <summary>코어에 연쇄 번개만: 맞물림 블록이 적어 마른 제시가 잘 이어진다.</summary>
        static void DryLoadout(RunConfig c) => c.Loadout = new LoadoutSpec { Core = Ids.Block.ChainLightning, Links = { null, null, null, null } };

        [Test]
        public void Loot__Pity_RisesOnDryOffers_ResetsOnSynergy_BoostsOnlySynergyBlocks()
        {
            var t = LootTuning.From(TestContent.Db);
            int boosted = 0;
            for (ulong seed = 1; seed <= 80; seed++)
            {
                var r = AtLoot(2, seed: seed, tweak: DryLoadout);
                r.Run.Purse = 100000;
                int dry = 0;   // 첫 제시 전에는 마른 제시가 없다
                for (int k = 0; k < 10; k++)
                {
                    var offer = r.Run.Loot;
                    Assert.AreEqual(t.PityBp(dry), offer.PityBp, "씨앗 " + seed + " 제시 " + k);
                    bool any = false;
                    foreach (var c in offer.Cards)
                    {
                        if (c.Synergy != LootSynergy.None) any = true;
                        if (c.PityBoosted) { boosted++; Assert.AreNotEqual(LootSynergy.None, c.Synergy, "운 보정이 뽑은 카드는 맞물림 블록"); }
                    }
                    if (offer.PityBp == 0) foreach (var c in offer.Cards) Assert.IsFalse(c.PityBoosted);
                    dry = any ? 0 : dry + 1;
                    r.Cmd(RunCommandKind.LootReroll);
                    r.Step();
                }
            }
            Assert.Greater(boosted, 0, "마른 제시가 이어지면 운 보정이 실제로 걸린다");
        }

        [Test]
        public void Loot__Pity_Deterministic_SameSeedSameSequence()
        {
            for (ulong seed = 3; seed <= 12; seed++)
            {
                var a = AtLoot(2, seed: seed, tweak: DryLoadout);
                var b = AtLoot(2, seed: seed, tweak: DryLoadout);
                a.Run.Purse = b.Run.Purse = 100000;
                for (int k = 0; k < 8; k++)
                {
                    var oa = a.Run.Loot; var ob = b.Run.Loot;
                    Assert.AreEqual(oa.PityBp, ob.PityBp);
                    Assert.AreEqual(oa.Cards.Count, ob.Cards.Count);
                    for (int i = 0; i < oa.Cards.Count; i++)
                    {
                        Assert.AreEqual(oa.Cards[i].BlockId, ob.Cards[i].BlockId, "씨앗 " + seed + " 제시 " + k + " 카드 " + i);
                        Assert.AreEqual(oa.Cards[i].PityBoosted, ob.Cards[i].PityBoosted);
                        Assert.AreEqual(oa.Cards[i].Synergy, ob.Cards[i].Synergy);
                        Assert.AreEqual(oa.Cards[i].SynergyFirst, ob.Cards[i].SynergyFirst);
                    }
                    a.Cmd(RunCommandKind.LootReroll); a.Step();
                    b.Cmd(RunCommandKind.LootReroll); b.Step();
                }
            }
        }

        [Test]
        public void Loot__Pity_RaisesSynergyRate()
        {
            // 같은 조립으로 마른 제시가 이어진 뒤(운 보정이 걸린 제시)의 맞물림 카드 비율이 운 보정 없는 첫 제시보다 높다.
            int firstSyn = 0, firstAll = 0, lateSyn = 0, lateAll = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var r = AtLoot(2, seed: seed, tweak: DryLoadout);
                r.Run.Purse = 100000;
                for (int k = 0; k < 8; k++)
                {
                    var offer = r.Run.Loot;
                    foreach (var c in offer.Cards)
                    {
                        bool syn = c.Synergy != LootSynergy.None;
                        if (offer.PityBp == 0) { firstAll++; if (syn) firstSyn++; }
                        else { lateAll++; if (syn) lateSyn++; }
                    }
                    r.Cmd(RunCommandKind.LootReroll);
                    r.Step();
                }
            }
            Assert.Greater(lateAll, 0);
            Assert.Greater(lateSyn / (float)lateAll, firstSyn / (float)firstAll + 0.1f, "운 보정이 맞물림 확률을 올린다");
        }
    }
}
