using System;
using NUnit.Framework;
using Wizard.Solo.Data;

namespace Wizard.Solo.Tests.Data
{
    /// <summary>요약서 표의 값과 읽은 Def를 대조한다. 기대값은 요약서에서 옮겼다(구현 출력에서 베끼지 않음).</summary>
    public class ContentValueTests
    {
        const float Eps = 1e-5f;
        static IContentDb Db => TestContent.Db;

        // ---------------------------------------------------------------- 03 요약서 2.2절 블록 표

        // id, 종류, 등급, 속성, 열, 피해, 간격(초), 간격(틱), 속도, 사거리, 반지름
        [TestCase("block.magic_bolt", "projectile", "common", "element.arcane", 4f, 10f, 0.15f, 9, 14f, 10f, 0.12f)]
        [TestCase("block.fireball", "projectile", "rare", "element.fire", 18f, 40f, 0.6f, 36, 9f, 8f, 0.18f)]
        [TestCase("block.chain_lightning", "projectile", "hero", "element.lightning", 12f, 18f, 0.45f, 27, 0f, 7f, 0.12f)]
        [TestCase("block.boomerang", "projectile", "rare", "element.arcane", 10f, 15f, 0.35f, 21, 10f, 6f, 0.15f)]
        [TestCase("block.ice_spike", "projectile", "rare", "element.ice", 8f, 14f, 0.3f, 18, 12f, 9f, 0.12f)]
        [TestCase("block.shadow_orb", "projectile", "rare", "element.dark", 14f, 28f, 0.5f, 30, 6f, 8f, 0.25f)]
        public void S02_BlockTable__Projectiles(string id, string kind, string rarity, string element, float heat, float damage,
            float interval, int intervalTicks, float speed, float range, float radius)
        {
            var b = Db.Block(id);
            Assert.IsNotNull(b, id);
            Assert.AreEqual(kind, b.Kind);
            Assert.AreEqual(BlockKind.Projectile, b.KindEnum);
            Assert.IsTrue(b.IsProjectile);
            Assert.AreEqual(rarity, b.Rarity);
            Assert.AreEqual(element, b.Element);
            Assert.AreEqual(heat, b.Heat, Eps);
            Assert.AreEqual(1f, b.HeatMultiplier, Eps);
            Assert.AreEqual(damage, b.BaseDamage, Eps);
            Assert.AreEqual(interval, b.IntervalSeconds, Eps);
            Assert.AreEqual(intervalTicks, b.IntervalTicks);
            Assert.AreEqual(speed, b.Speed, Eps);
            Assert.AreEqual(range, b.Range, Eps);
            Assert.AreEqual(radius, b.HitRadius, Eps);
            Assert.AreEqual(SimConst.HeatToHu(heat), b.HeatHu);
        }

        [Test]
        public void S02_BlockTable__ProjectileBehaviours()
        {
            var mb = Db.Block(Ids.Block.MagicBolt);
            Assert.IsEmpty(mb.Flags);
            Assert.AreEqual(Ids.BlockEffect.None, mb.Effect);

            var fb = Db.Block(Ids.Block.Fireball);
            Assert.IsTrue(fb.HasFlag(Ids.BlockFlag.Explodes));
            Assert.AreEqual(2f, fb.Params.ExplosionRadius, Eps);

            var cl = Db.Block(Ids.Block.ChainLightning);
            Assert.IsTrue(cl.Instant);
            Assert.IsTrue(cl.HasFlag(Ids.BlockFlag.Chains));
            Assert.IsTrue(cl.HasFlag(Ids.BlockFlag.IgnoresWolfDodge));
            Assert.AreEqual(3, cl.Params.ChainHops);
            Assert.AreEqual(3f, cl.Params.ChainRadius, Eps);
            Assert.AreEqual(0.7f, cl.Params.ChainHopMultiplier, Eps);

            var bm = Db.Block(Ids.Block.Boomerang);
            Assert.IsTrue(bm.HasFlag(Ids.BlockFlag.Returns));
            Assert.AreEqual(0.5f, bm.Params.ReturnArriveRadius, Eps);
            Assert.AreEqual(9f, bm.Params.ReturnMaxDistance, Eps);

            var ice = Db.Block(Ids.Block.IceSpike);
            Assert.AreEqual(30f, ice.Params.SlowPercent, Eps);
            Assert.AreEqual(1f, ice.Params.SlowSeconds, Eps);

            Assert.IsTrue(Db.Block(Ids.Block.ShadowOrb).HasFlag(Ids.BlockFlag.PassThrough));
            foreach (var b in Db.Blocks) if (b.Id != Ids.Block.ChainLightning) Assert.IsFalse(b.Instant, b.Id);
        }

        // 제어: id, 등급, 열 배율, 갈래 수, 부채 반각
        [TestCase("block.split_two", "common", 1.6f, 2, 10f)]
        [TestCase("block.split_three", "rare", 2.2f, 3, 15f)]
        [TestCase("block.on_hit_trigger", "rare", 1f, 1, 0f)]
        public void S02_BlockTable__Controls(string id, string rarity, float heatMul, int split, float fanHalf)
        {
            var b = Db.Block(id);
            Assert.AreEqual("control", b.Kind);
            Assert.AreEqual(BlockKind.Control, b.KindEnum);
            Assert.AreEqual(rarity, b.Rarity);
            Assert.AreEqual(Ids.Element.Neutral, b.Element);
            Assert.AreEqual(0f, b.Heat, Eps);
            Assert.AreEqual(heatMul, b.HeatMultiplier, Eps);
            Assert.AreEqual(split, b.SplitCount);
            Assert.AreEqual(split > 1, b.IsSplit);
            Assert.AreEqual(fanHalf, b.Params.FanHalfAngleDegrees, Eps);
            Assert.AreEqual(id == Ids.Block.OnHitTrigger, b.IsTrigger);
            Assert.AreEqual(0f, b.BaseDamage, Eps);
            Assert.AreEqual(0f, b.IntervalSeconds, Eps);
        }

        // 변형: id, 등급, 속성, 열, 피해 보너스, effect
        [TestCase("block.wall_bounce", "common", "element.neutral", 2f, 0f, "bounce")]
        [TestCase("block.double_size", "common", "element.fire", 3f, 0.2f, "size")]
        [TestCase("block.pierce", "common", "element.arcane", 3f, 0.1f, "pierce")]
        [TestCase("block.frost", "common", "element.ice", 3f, 0f, "frost")]
        [TestCase("block.homing", "hero", "element.lightning", 5f, 0f, "homing")]
        [TestCase("block.triple_heat", "hero", "element.fire", 2f, 0f, "triple_heat")]
        [TestCase("block.corrosion", "hero", "element.dark", 5f, 0f, "corrosion")]
        public void S02_BlockTable__Modifiers(string id, string rarity, string element, float heat, float bonus, string effect)
        {
            var b = Db.Block(id);
            Assert.AreEqual("modifier", b.Kind);
            Assert.AreEqual(BlockKind.Modifier, b.KindEnum);
            Assert.AreEqual(rarity, b.Rarity);
            Assert.AreEqual(element, b.Element);
            Assert.AreEqual(heat, b.Heat, Eps);
            Assert.AreEqual(1f, b.HeatMultiplier, Eps);
            Assert.AreEqual(bonus, b.DamageBonus, Eps);
            Assert.AreEqual(effect, b.Effect);
        }

        [Test]
        public void S02_BlockTable__ModifierParams()
        {
            Assert.AreEqual(2, Db.Block(Ids.Block.WallBounce).Params.BounceCount);
            Assert.AreEqual(2f, Db.Block(Ids.Block.DoubleSize).Params.RadiusMultiplier, Eps);
            Assert.AreEqual(4f, Db.Block(Ids.Block.DoubleSize).Params.RadiusMultiplierCap, Eps);
            Assert.AreEqual(2, Db.Block(Ids.Block.Pierce).Params.PierceCount);
            Assert.AreEqual(20f, Db.Block(Ids.Block.Frost).Params.SlowPercent, Eps);
            Assert.AreEqual(1f, Db.Block(Ids.Block.Frost).Params.SlowSeconds, Eps);
            Assert.AreEqual(180f, Db.Block(Ids.Block.Homing).Params.HomingTurnDegreesPerSecond, Eps);
            Assert.AreEqual(90f, Db.Block(Ids.Block.Homing).Params.HomingConeHalfDegrees, Eps);
            Assert.AreEqual(6f, Db.Block(Ids.Block.Homing).Params.HomingRange, Eps);
            Assert.AreEqual(3f, Db.Block(Ids.Block.TripleHeat).Params.CritMultiplier, Eps);
            Assert.AreEqual(15f, Db.Block(Ids.Block.Corrosion).Params.DamageTakenBonusPercent, Eps);
            Assert.AreEqual(3f, Db.Block(Ids.Block.Corrosion).Params.DamageTakenBonusSeconds, Eps);
        }

        // 03 요약서 1.3절: 약점 ×1.25, 저항 ×0.8
        [Test]
        public void S02_Elements__WeakAndResistMultipliers()
        {
            Assert.AreEqual(1.25f, Db.Elements.WeakMultiplier, Eps);
            Assert.AreEqual(0.8f, Db.Elements.ResistMultiplier, Eps);
        }

        // ---------------------------------------------------------------- 02 요약서 8.1·8.2절 캐릭터 표

        // id, 속성, 패시브, 지팡이 특성, 고유 비기, 체력, 걷기, 자세, 스침 반지름, 결정 상한, 결정당 콤보, 피격 손실,
        // 연결 슬롯, 변이 수, 냉각, 임계 시작, 잠금, 스침 가속 초
        [TestCase("char.1", "element.fire", "passive.overflow", "wand_trait.heat_at_80", "art.kiln",
            8, 4.7f, 3.76f, 0.6f, 5, 15, 1, 4, 2, 45f, 80f, 3.0f, 1.5f)]
        [TestCase("char.2", "element.lightning", "passive.lightning_rod", "wand_trait.three_mutations", "art.bolt_dash",
            4, 5.6f, 5.04f, 0.75f, 5, 12, 1, 4, 3, 50f, 85f, 2.5f, 2.0f)]
        [TestCase("char.3", "element.arcane", "passive.underline", "wand_trait.depth_decay", "art.copy_circle",
            6, 5.0f, 4.25f, 0.6f, 4, 15, 1, 5, 2, 45f, 85f, 3.5f, 1.5f)]
        [TestCase("char.4", "element.ice", "passive.thin_ice", "wand_trait.keep_crystal", "art.ice_screen",
            7, 4.5f, 4.5f, 0.6f, 6, 15, 0, 4, 2, 45f, 90f, 3.0f, 1.5f)]
        [TestCase("char.5", "element.dark", "passive.shadow_contract", "wand_trait.on_credit", "art.shadow_hand",
            5, 5.2f, 4.42f, 0.6f, 7, 15, 2, 4, 2, 45f, 85f, 3.0f, 1.5f)]
        public void S04_CharacterTable(string id, string element, string passive, string trait, string unique,
            int hp, float walk, float stance, float graze, int crystalCap, int comboPerCrystal, int crystalLoss,
            int linkSlots, int maxMut, float cool, float critStart, float lockSec, float grazeBuff)
        {
            var c = Db.Character(id);
            Assert.IsNotNull(c, id);
            Assert.AreEqual(Ids.Char.NumberOf(id), c.Number);
            Assert.AreEqual(element, c.Element);
            Assert.AreEqual(passive, c.Passive);
            Assert.AreEqual(trait, c.WandTrait);
            Assert.AreEqual(unique, c.UniqueArt);
            Assert.AreEqual(hp, c.MaxHpCells);
            Assert.AreEqual(walk, c.WalkSpeed, Eps);
            Assert.AreEqual(stance, c.StanceSpeed, Eps);
            Assert.AreEqual(0.15f, c.HitRadius, Eps);
            Assert.AreEqual(0.30f, c.BodyRadius, Eps);
            Assert.AreEqual(graze, c.GrazeRadius, Eps);
            Assert.AreEqual(1.0f, c.InvulnSeconds, Eps);
            Assert.AreEqual(crystalCap, c.CrystalCap);
            Assert.AreEqual(comboPerCrystal, c.ComboPerCrystal);
            Assert.AreEqual(crystalLoss, c.CrystalLossOnHit);
            Assert.AreEqual(linkSlots, c.BaseLinkSlots);
            Assert.AreEqual(maxMut, c.MaxMutations);
            Assert.AreEqual(cool, c.CoolPerSecond, Eps);
            Assert.AreEqual(0.2f, c.CoolDelaySeconds, Eps);
            Assert.AreEqual(critStart, c.CritStart, Eps);
            Assert.AreEqual(lockSec, c.LockSeconds, Eps);
            Assert.AreEqual(grazeBuff, c.GrazeBuffSeconds, Eps);
            Assert.AreEqual(1.2f, c.GrazeBuffSpeed, Eps);
            // 02 요약서 6.1절 단위 환산: 냉각 열/초 × 2 = HU/틱, 임계 열 × 120 = HU
            Assert.AreEqual((int)(cool * 2f), SimConst.HeatToHu(c.CoolPerSecond) / SimConst.TicksPerSecond);
            Assert.AreEqual((int)(critStart * SimConst.HuPerHeat), SimConst.HeatToHu(c.CritStart));
        }

        [Test]
        public void S04_CharacterTable__Baseline()
        {
            var b = Db.BaselineCharacter;
            Assert.IsNotNull(b);
            Assert.IsNull(b.Id);
            Assert.AreEqual(0, b.Number);
            Assert.AreEqual(6, b.MaxHpCells);
            Assert.AreEqual(5.0f, b.WalkSpeed, Eps);
            Assert.AreEqual(4.25f, b.StanceSpeed, Eps);
            Assert.AreEqual(0.6f, b.GrazeRadius, Eps);
            Assert.AreEqual(5, b.CrystalCap);
            Assert.AreEqual(15, b.ComboPerCrystal);
            Assert.AreEqual(1, b.CrystalLossOnHit);
            Assert.AreEqual(4, b.BaseLinkSlots);
            Assert.AreEqual(2, b.MaxMutations);
            Assert.AreEqual(45f, b.CoolPerSecond, Eps);
            Assert.AreEqual(85f, b.CritStart, Eps);
            Assert.AreEqual(3.0f, b.LockSeconds, Eps);
        }

        // ---------------------------------------------------------------- 04 요약서 0.3절 판정기 상수

        [Test]
        public void S03_ParserConstants__SecondsAndUnits()
        {
            var p = Db.Tries.Parser;
            Assert.AreEqual(0.07f, p.JustWindowSeconds, Eps);
            Assert.AreEqual(0.18f, p.GapFullBloomSeconds, Eps);
            Assert.AreEqual(0.2f, p.GapShortSeconds, Eps);
            Assert.AreEqual(0.25f, p.GapSameSideSeconds, Eps);
            Assert.AreEqual(0.3f, p.GapDefaultSeconds, Eps);
            Assert.AreEqual(0.4f, p.ArtCooldownSeconds, Eps);
            Assert.AreEqual(0.1f, p.KBufferWindowSeconds, Eps);
            Assert.AreEqual(2, p.MaxDebt);

            Assert.AreEqual(1075, SimConst.Units(p.JustWindowSeconds));
            Assert.AreEqual(2765, SimConst.Units(p.GapFullBloomSeconds));
            Assert.AreEqual(3072, SimConst.Units(p.GapShortSeconds));
            Assert.AreEqual(3840, SimConst.Units(p.GapSameSideSeconds));
            Assert.AreEqual(4608, SimConst.Units(p.GapDefaultSeconds));
            Assert.AreEqual(6144, SimConst.Units(p.ArtCooldownSeconds));
            Assert.AreEqual(1536, SimConst.Units(p.KBufferWindowSeconds));
            Assert.AreEqual(12, SimConst.Ticks(p.GapShortSeconds));
            Assert.AreEqual(15, SimConst.Ticks(p.GapSameSideSeconds));
            Assert.AreEqual(18, SimConst.Ticks(p.GapDefaultSeconds));
            Assert.AreEqual(24, SimConst.Ticks(p.ArtCooldownSeconds));
            Assert.AreEqual(6, SimConst.Ticks(p.KBufferWindowSeconds));
        }

        // ---------------------------------------------------------------- 01 요약서 6.1절 scoring.json

        [Test]
        public void S09_Scoring__AllFields()
        {
            var s = Db.Scoring;
            Assert.AreEqual(1f, s.WaveCoef.AtWave1, Eps);
            Assert.AreEqual(0.1f, s.WaveCoef.PerWave, Eps);
            Assert.AreEqual(10, s.KillPerCost);
            Assert.AreEqual(3000, s.BossKill);
            Assert.AreEqual(200, s.WaveClear);
            Assert.AreEqual(2, s.FastClear.PerSecond);
            Assert.AreEqual(60, s.FastClear.TargetSeconds);
            Assert.AreEqual(120, s.FastClear.BossTargetSeconds);
            Assert.AreEqual(100, s.NoHit);
            Assert.AreEqual(5, s.Graze.PerGraze);
            Assert.AreEqual(20, s.Graze.Cap);
            Assert.AreEqual(40, s.Graze.BossCap);
            Assert.AreEqual(3648, s.PhantomFirst);
            // 01 6.1절 관련 값: 지체 120초, 지체 뒤 처치 절반
            Assert.AreEqual(120f, Db.Waves.StallSeconds, Eps);
            Assert.AreEqual(0.5f, Db.Waves.StallKillScore, Eps);
        }

        // ---------------------------------------------------------------- 07 요약서 2절 enhance

        [Test]
        public void S08_Enhance__ConfigKeys()
        {
            var e = Db.Enhance;
            CollectionAssert.AreEqual(new[] { 100, 90, 75, 60, 50, 40, 32, 25, 18, 12, 8, 5, 3 }, e.BaseProb);
            Assert.AreEqual(95, e.ProbCapPct);
            Assert.AreEqual(15, e.GreatRatioPct);
            Assert.AreEqual(25, e.MutationRatioPct);
            Assert.AreEqual(15, e.AuraStepPct);
            Assert.AreEqual(3, e.AuraMax);
            Assert.AreEqual(3, e.DropSafeMaxStage);
            Assert.AreEqual(2, e.FloorGap);
            Assert.AreEqual(100, e.CostCoeff);
            CollectionAssert.AreEqual(new[] { 4, 8 }, e.StoneTierBounds);
            Assert.AreEqual(1, e.StonesPerAttempt);
            Assert.AreEqual(3, e.SlotExtraMax);
            Assert.AreEqual(2, e.MutationCapDefault);
            Assert.AreEqual(3, e.MutationCapFor(Ids.Char.C2));
            Assert.AreEqual(2, e.MutationCapFor(Ids.Char.C1));
            Assert.AreEqual(2, e.MutationCapFor(null));
            Assert.AreEqual(0, e.GreatSlotMinStage);
            Assert.AreEqual(5, e.Sacrifice.MaxBlocks);
            Assert.AreEqual(4, e.Sacrifice.ScoreCommon);
            Assert.AreEqual(8, e.Sacrifice.ScoreRare);
            Assert.AreEqual(14, e.Sacrifice.ScoreHero);
            Assert.AreEqual(5, e.Sacrifice.SameElement);
            Assert.AreEqual(40, e.Sacrifice.Cap);
            Assert.AreEqual(7, e.Carve.MinStage);
            Assert.AreEqual(40, e.Carve.BasePct);
            Assert.AreEqual(20, e.Carve.StepPct);
            Assert.AreEqual(20000, e.Carve.GoldCoeff);
            Assert.AreEqual(2, e.Carve.HighStones);
            Assert.AreEqual(3000, e.MutationRemoveGold);
            Assert.AreEqual(1.15f, e.StageDamageBase, Eps);
            Assert.AreEqual(13, e.MaxStage);
            CollectionAssert.AreEqual(new[] { 4, 7, 10, 13 }, e.Milestones);
        }

        // 07 요약서 2절 mutation_cap = characters.json max_mutations
        [Test]
        public void S08_Enhance__MutationCapMatchesCharacters()
        {
            foreach (var c in Db.Characters) Assert.AreEqual(c.MaxMutations, Db.Enhance.MutationCapFor(c.Id), c.Id);
        }

        // 07 요약서 4.3절 비용 표: 100 × (stage + 1)^2
        [TestCase(0, 100)]
        [TestCase(1, 400)]
        [TestCase(2, 900)]
        [TestCase(3, 1600)]
        [TestCase(4, 2500)]
        [TestCase(5, 3600)]
        [TestCase(6, 4900)]
        [TestCase(7, 6400)]
        [TestCase(8, 8100)]
        [TestCase(9, 10000)]
        [TestCase(10, 12100)]
        [TestCase(11, 14400)]
        [TestCase(12, 16900)]
        public void S08_Enhance__CostTableFromCoeff(int stage, int gold)
        {
            Assert.AreEqual(gold, Db.Enhance.CostCoeff * (stage + 1) * (stage + 1));
            Assert.AreEqual(Db.Enhance.MaxStage, Db.Enhance.BaseProb.Count);
        }

        // 07 요약서 4.10절 표
        [Test]
        public void S08_Enhance__StageDamageTable()
        {
            float[] expected = { 1.0000f, 1.1500f, 1.3225f, 1.5209f, 1.7490f, 2.0114f, 2.3131f, 2.6600f, 3.0590f, 3.5179f, 4.0456f, 4.6524f, 5.3503f, 6.1528f };
            var t = Db.Enhance.StageDamageMul;
            Assert.AreEqual(expected.Length, t.Count);
            for (int n = 0; n < expected.Length; n++)
            {
                Assert.AreEqual(expected[n], t[n], 1e-4f, "stage " + n);
                Assert.AreEqual(expected[n], (float)Math.Pow(Db.Enhance.StageDamageBase, n), 1e-4f, "pow stage " + n);
            }
        }

        // ---------------------------------------------------------------- 07 요약서 2·7·8절 economy

        [Test]
        public void S08_Economy__ShopLibraryAppraisal()
        {
            var e = Db.Economy;
            Assert.AreEqual(10000, e.ShopCharmGold);
            Assert.AreEqual(3, e.ShopCharmWeeklyLimit);
            Assert.AreEqual(500, e.ShopWandPlus0Gold);
            Assert.AreEqual(50, e.ShopLibraryStepBlocks);
            Assert.AreEqual(2000, e.ShopLibraryStepGold);
            Assert.AreEqual((int)DayOfWeek.Monday, e.ShopWeekStartDayOfWeek);
            Assert.AreEqual(100, e.LibraryBaseCap);
            Assert.AreEqual(0.5f, e.AppraisalWandFactor, Eps);
            Assert.AreEqual(250, e.AppraisalWandPlus0);
            Assert.AreEqual(10, e.ReleaseRefundPct);
            Assert.AreEqual(5, e.CrystalGoldEach);
        }

        // 07 요약서 7.4절 표
        [Test]
        public void S08_Economy__AppraisalAndRefundTable()
        {
            int[] appraisal = { 250, 50, 261, 683, 1574, 3400, 6748, 12494, 22068, 38047, 64624, 106720, 169656, 258309 };
            int[] refund = { 0, 5, 26, 68, 157, 340, 674, 1249, 2206, 3804, 6462, 10672, 16965, 25830 };
            CollectionAssert.AreEqual(appraisal, Db.Economy.AppraisalByStage);
            CollectionAssert.AreEqual(refund, Db.Economy.ReleaseRefundByStage);
            Assert.AreEqual(Db.Economy.AppraisalWandPlus0, Db.Economy.AppraisalByStage[0]);
            for (int s = 1; s < appraisal.Length; s++)
                Assert.AreEqual(appraisal[s] * Db.Economy.ReleaseRefundPct / 100, refund[s], "stage " + s);
        }

        // ---------------------------------------------------------------- 02 요약서 9.1절 mutations

        [Test]
        public void S04_Mutations__Values()
        {
            var m = Db.Mutations;
            Assert.AreEqual(1.6f, m.TwistedBarrelDamageMul, Eps);
            Assert.AreEqual(25, m.TwistedBarrelSpreadDegrees);
            Assert.AreEqual(2.0f, m.UnstableCoreRadiusMul, Eps);
            Assert.AreEqual(5, m.UnstableCoreChancePercent);
            Assert.AreEqual(120, SimConst.Ticks(m.UnstableCoreLockSeconds));
            Assert.AreEqual(18, SimConst.Ticks(m.UnstableCoreWarnSeconds));
            Assert.AreEqual(70f, m.HypersensitiveCritStart, Eps);
            Assert.AreEqual(90, SimConst.Ticks(m.HypersensitiveLockPlusSeconds));
            Assert.AreEqual(480, SimConst.HeatToHu(m.GluttonyHeatPerKill));
            Assert.AreEqual(2400, SimConst.HeatToHu(m.GluttonyHeatPerSecondCap));
            Assert.AreEqual(60, SimConst.Ticks(m.GluttonyWindowSeconds));
            Assert.AreEqual(0.5f, m.GluttonyCoolMul, Eps);
            Assert.AreEqual(1.5f, m.MirrorImageHeatMul, Eps);
            Assert.AreEqual(1, m.GlassWandCritBonus);
            Assert.AreEqual(3, m.GlassWandMinStage);
            Assert.AreEqual(1, m.GlassWandDropsPerWave);
        }

        // ---------------------------------------------------------------- 06 요약서 4.3절·00 문서 13절 camp

        [Test]
        public void S07_Camp__ValuesAndSoloFields()
        {
            var c = Db.Camp;
            Assert.AreEqual(30, c.HealBase);
            Assert.AreEqual(5, c.HealPerWave);
            Assert.AreEqual(3, c.HealRepeatMulNum);
            Assert.AreEqual(2, c.HealRepeatMulDen);
            Assert.AreEqual(1, c.HealCells);
            Assert.AreEqual(1, c.DawnFreeHealCells);
            Assert.AreEqual(60, SimConst.Ticks(c.DepartHoldSeconds));
            Assert.AreEqual(60, SimConst.Ticks(c.WaveClearSeconds));
            Assert.AreEqual(240, SimConst.Ticks(c.TodTransitionSeconds));
            Assert.AreEqual(180, SimConst.Ticks(c.NarrowFieldPreludeSeconds));
            Assert.AreEqual(6, c.ArtUnlockLastWave);
            Assert.AreEqual(2, c.ArtCandidates);
            Assert.AreEqual(2.0f, c.DownedToSettlementSeconds, Eps);
            // D-52 솔로 임시
            Assert.AreEqual(1.5f, c.FireInteractRadius, Eps);
            Assert.AreEqual(4f, c.DepartMarkerOffsetX, Eps);
            Assert.AreEqual(0f, c.DepartMarkerOffsetY, Eps);
            Assert.AreEqual(1.0f, c.DepartMarkerRadius, Eps);
            // D-58
            Assert.AreEqual(120, SimConst.Ticks(c.BossClearSeconds));
            Assert.AreEqual(264, c.RevengeClearTicks);
            Assert.AreEqual(84, c.RevengeSkipAfterTicks);
            // 좁은 들 예고 = modifiers.json thorn_border.telegraph_seconds
            Assert.AreEqual(Db.Modifier(Ids.Whim.NarrowField).ThornBorder.TelegraphSeconds, c.NarrowFieldPreludeSeconds, Eps);
        }

        // 06 요약서 4.3절 TV-190·191: 회복 비용 w=5 → 55 / 82 / 82, w=10 → 80 / 120 (데이터 값으로 계산이 맞는지)
        [TestCase(5, 0, 55)]
        [TestCase(5, 1, 82)]
        [TestCase(5, 2, 82)]
        [TestCase(10, 0, 80)]
        [TestCase(10, 1, 120)]
        public void S07_TV_190__HealCostFromCampData(int wave, int healsSoFar, int cost)
        {
            var c = Db.Camp;
            int b = c.HealBase + c.HealPerWave * wave;
            Assert.AreEqual(cost, healsSoFar == 0 ? b : b * c.HealRepeatMulNum / c.HealRepeatMulDen);
        }

        // ---------------------------------------------------------------- const.json

        [Test]
        public void S00_Const__CombatConstants()
        {
            var k = Db.Const;
            Assert.AreEqual(0.85f, k.DepthDecayDefault, Eps);
            Assert.AreEqual(0.92f, k.DepthDecayTrait, Eps);
            Assert.AreEqual(5f, k.UnderlineMaxCut, Eps);
            Assert.AreEqual(3, k.TriggerMaxDepth);
            Assert.AreEqual(64, k.TreeShotCap);
            Assert.AreEqual(300, k.PlayerShotCap);
            Assert.AreEqual(150, k.EnemyShotCap);
            Assert.AreEqual(160, k.ArtShotCap);
            Assert.AreEqual(120, SimConst.Ticks(k.ComboIdleSeconds));
            Assert.AreEqual(3, k.ComboPerShotCap);
            Assert.AreEqual(2, k.ComboCritGain);
            Assert.AreEqual(2, k.GrazeComboGain);
            Assert.AreEqual(1229, SimConst.Units(k.DiagonalHoldSeconds));   // 02 4.3절: 0.08초 = 1,229단위
            Assert.AreEqual(90, SimConst.Ticks(k.ContactIntervalSeconds));
            Assert.AreEqual(30, SimConst.Ticks(k.SpawnNoticeSeconds));
            Assert.AreEqual(30f, k.PlayerSlowOnFrostPercent, Eps);
            Assert.AreEqual(8, k.MaxInputEventsPerTick);
            Assert.AreEqual(2.5f, k.OverflowRadius, Eps);
            Assert.AreEqual(1.5f, k.OverflowDamageT, Eps);
            Assert.AreEqual(2f, k.OverflowKnockback, Eps);
            Assert.AreEqual(5f, k.LightningRodRadius, Eps);
            Assert.AreEqual(2, k.LightningRodTargets);
            Assert.AreEqual(1f, k.LightningRodDamageT, Eps);
            Assert.AreEqual(6, k.ThinIceHits);
            Assert.AreEqual(60, SimConst.Ticks(k.ThinIceFreezeSeconds));
            Assert.AreEqual(360, SimConst.Ticks(k.ThinIceImmuneSeconds));
            Assert.AreEqual(0.03f, k.ShadowContractPerCrystal, Eps);
            Assert.AreEqual(360, SimConst.Ticks(k.FreezeImmuneSeconds));
            Assert.AreEqual(1.5f, k.StunDamageTakenMul, Eps);
            Assert.AreEqual(0.5f, k.ArmorDamageMul, Eps);
            Assert.AreEqual(50, k.RevengeCrackedPercent);   // 05 10.4절
        }

        // 05 요약서 10.4절 T280: hpMax 7111, hpMin 2702 → 62% ≥ 금 간 거울 문턱
        [Test]
        public void S06_T280__DentAboveCrackedThreshold()
        {
            int dent = (7111 - 2702) * 100 / 7111;
            Assert.AreEqual(62, dent);
            Assert.GreaterOrEqual(dent, Db.Const.RevengeCrackedPercent);
        }

        // 05 요약서 8.2절 boss common, 10.2절 first_encounter
        [Test]
        public void S06_BossCommonAndFirstEncounter()
        {
            var c = Db.BossCommon;
            Assert.AreEqual(72, SimConst.Ticks(c.IntroSeconds));
            Assert.AreEqual(36, SimConst.Ticks(c.PhaseTransitionSeconds));
            Assert.AreEqual(90, SimConst.Ticks(c.WaitSeconds));
            Assert.AreEqual(30, SimConst.Ticks(c.NoCandidateWaitSeconds));
            Assert.AreEqual(3, SimConst.Ticks(c.R5RetrySeconds));
            Assert.AreEqual(120, SimConst.Ticks(c.R5RepickSeconds));
            Assert.AreEqual(1200, SimConst.Ticks(c.RageStackSeconds));
            Assert.AreEqual(5, c.RageMaxStacks);
            Assert.AreEqual(0.1f, c.RageStepRatio, Eps);
            Assert.AreEqual(10800, SimConst.Ticks(c.FinalRageSeconds));
            Assert.AreEqual(1, c.FinalRageDamagePlus);
            Assert.AreEqual(18, SimConst.Ticks(c.ActionHaltSeconds));
            Assert.AreEqual(1.5f, c.ContactCooldownSeconds, Eps);

            var fe = Db.Boss(Ids.Boss.MirrorPhantom).FirstEncounter;
            Assert.AreEqual(50, fe.HpFloorPercent);
            Assert.AreEqual(2700, SimConst.Ticks(fe.MaxFightSeconds));
            Assert.AreEqual(5, fe.RageStacks);
            Assert.AreEqual(3, fe.StartPhase);
            Assert.AreEqual(180, SimConst.Ticks(fe.FinisherSeconds));
            Assert.AreEqual(114, fe.FinisherHpZeroTicks);
            Assert.AreEqual(240, SimConst.Ticks(fe.CollapseSeconds));
        }

        // 05 요약서 8.4절: 보스 step의 추가 필드가 형으로 읽힌다
        [Test]
        public void S06_BossSteps__TypedExtraSections()
        {
            bool copy = false, summon = false, stream = false;
            foreach (var b in Db.Bosses)
                foreach (var p in b.Patterns)
                    foreach (var s in p.Steps)
                    {
                        if (s.Copy != null && p.Key == "mimic_art")
                        {
                            // 04 요약서 11.1절 U-R15: 해금한 ③④⑧ 중에서 따라 씀, 없으면 ①
                            copy = true;
                            CollectionAssert.AreEquivalent(new[] { Ids.Art.Fan, Ids.Art.Wings, Ids.Art.Octastar }, s.Copy.Arts);
                            Assert.AreEqual(Ids.Art.Burst, s.Copy.Fallback);
                        }
                        if (s.Summon != null) { summon = true; Assert.IsNotNull(Db.Enemy(s.Summon.Enemy), s.Summon.Enemy); Assert.Greater(s.Summon.Count, 0); }
                        if (s.Stream != null) { stream = true; Assert.Greater(s.Stream.ShotsPerSecond, 0); }
                    }
            Assert.IsTrue(copy, "copy step");
            Assert.IsTrue(summon, "summon step");
            Assert.IsTrue(stream, "stream step");
        }

        // ---------------------------------------------------------------- 06 요약서 4.4·4.5·5.3절 drops

        [Test]
        public void S07_Drops__CardsGoldShardsBag()
        {
            var d = Db.Drops;
            Assert.AreEqual(4, d.CardsNormal);
            Assert.AreEqual(5, d.CardsElite);
            Assert.AreEqual(5, d.CardsBoss);
            Assert.AreEqual(4, d.CardsPhantomFirst);
            Assert.AreEqual(2, d.CardsPick);
            Assert.AreEqual(30, d.BossHeroGuaranteeWave);
            Assert.AreEqual(30, d.RerollGold);
            Assert.AreEqual(12, d.StorageCap);
            Assert.AreEqual(5, d.DiscardGold(Ids.Rarity.Common, false));
            Assert.AreEqual(15, d.DiscardGold(Ids.Rarity.Rare, false));
            Assert.AreEqual(50, d.DiscardGold(Ids.Rarity.Hero, false));
            Assert.AreEqual(50, d.DiscardGold(Ids.Rarity.Common, true));
            Assert.AreEqual(6, d.BorrowedBlockWave);
            Assert.AreEqual(Ids.Rarity.Hero, d.BorrowedBlockRarity);
            Assert.AreEqual(1, d.BorrowedBlockCount);
            Assert.AreEqual(2, d.GoldPerCost);
            Assert.AreEqual(3, d.EliteGoldMul);
            Assert.AreEqual(20, d.WaveClearGold);
            Assert.AreEqual(30, d.HealthShardBpPerCost);
            Assert.AreEqual(1000, d.HealthShardEliteBp);
            Assert.AreEqual(1, d.HealthShardPerWave);
            Assert.AreEqual(1, d.HealthShardHealCells);
            Assert.AreEqual(3, d.BagWavesPerSlot);
            Assert.AreEqual(12, d.BagSlotCap);
            Assert.AreEqual(10, d.BagWavesPerHero);
        }

        // 06 요약서 4.5절: 보스 처치 골드 20 × (9 + w) = 10: 380, 20: 580, 30: 780, 50: 1,180
        [TestCase(10, 380)]
        [TestCase(20, 580)]
        [TestCase(30, 780)]
        [TestCase(50, 1180)]
        public void S07_BossGold__FromData(int wave, int gold)
        {
            Assert.AreEqual(gold, Db.Drops.BossGoldPerS10 * (9 + wave));
        }

        // 06 요약서 4.5절: 일반 적 처치 골드 = 비용 × 2, 엘리트 3배(슬라임 엘리트 6, 선돌지기 엘리트 36), 체력 조각 30 × 비용 bp
        [Test]
        public void S07_KillGold__FromEnemyCosts()
        {
            var d = Db.Drops;
            Assert.AreEqual(2, Db.Enemy(Ids.Enemy.SlimeMoss).Cost * d.GoldPerCost);
            Assert.AreEqual(12, Db.Enemy(Ids.Enemy.GolemMenhir).Cost * d.GoldPerCost);
            Assert.AreEqual(6, Db.Enemy(Ids.Enemy.SlimeMoss).Cost * d.GoldPerCost * d.EliteGoldMul);
            Assert.AreEqual(36, Db.Enemy(Ids.Enemy.GolemMenhir).Cost * d.GoldPerCost * d.EliteGoldMul);
            Assert.AreEqual(180, Db.Enemy(Ids.Enemy.GolemMenhir).Cost * d.HealthShardBpPerCost);
        }

        // 06 요약서 4.4절 카드 등급 확률 표
        [TestCase(1, 78, 22, 0)]
        [TestCase(2, 73, 25, 2)]
        [TestCase(3, 70, 26, 4)]
        [TestCase(4, 70, 25, 5)]
        [TestCase(9, 70, 25, 5)]
        public void S07_CardRarity__ByDay(int day, int common, int rare, int hero)
        {
            var row = Db.Drops.RarityRowForDay(day);
            Assert.IsNotNull(row);
            Assert.AreEqual(common, row.Common);
            Assert.AreEqual(rare, row.Rare);
            Assert.AreEqual(hero, row.Hero);
            Assert.AreEqual(100, row.Common + row.Rare + row.Hero);
        }

        // 06 요약서 5.3절 TV-230·231: cleared → 가방 칸, 영웅 한도(데이터 값으로 계산)
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 0)]
        [TestCase(2, 0, 0)]
        [TestCase(3, 1, 0)]
        [TestCase(8, 2, 0)]
        [TestCase(10, 3, 1)]
        [TestCase(14, 4, 1)]
        [TestCase(20, 6, 2)]
        [TestCase(30, 10, 3)]
        [TestCase(36, 12, 3)]
        [TestCase(40, 12, 4)]
        [TestCase(60, 12, 6)]
        public void S07_TV_230__BagSlotsAndHeroLimitFromData(int cleared, int slots, int heroLimit)
        {
            var d = Db.Drops;
            Assert.AreEqual(slots, Math.Min(d.BagSlotCap, cleared / d.BagWavesPerSlot));
            Assert.AreEqual(heroLimit, cleared / d.BagWavesPerHero);
        }

        [Test]
        public void S07_Drops__RevengeReward()
        {
            var r = Db.Drops.RevengeReward;
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual(Ids.Item.StoneMid, r[0].Item); Assert.AreEqual(3, r[0].Count);
            Assert.AreEqual(Ids.Item.StoneHigh, r[1].Item); Assert.AreEqual(1, r[1].Count);
            Assert.AreEqual(Ids.Item.Charm, r[2].Item); Assert.AreEqual(1, r[2].Count);
        }
    }
}
