using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>컴파일러 벡터. 03 요약서 15절(S02 16.3), 5절 열 검산(S02 16.4), 8절 수 검산(S02 16.5), 10 요약서 9절.</summary>
    public class SpellCompilerTests
    {
        SpellWorld _k;

        [SetUp]
        public void SetUp() { _k = new SpellWorld(); }

        static int Hu(float heat) => SimConst.HeatToHu(heat);

        static bool Has(CompiledSpell s, SpellDiagCode code, int slot)
        {
            foreach (var d in s.Diagnostics) if (d.Code == code && d.Slot == slot) return true;
            return false;
        }

        static bool HasCode(CompiledSpell s, SpellDiagCode code)
        {
            foreach (var d in s.Diagnostics) if (d.Code == code) return true;
            return false;
        }

        // ------------------------------------------------------------ 구조 (15절)

        [Test]
        public void S02_C01__MagicBoltAlone()
        {
            var s = _k.Compile(B.Mb, B.None, B.None, B.None, B.None);
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(1, s.Scopes.Count);
            Assert.AreEqual(1, s.Scopes[0].Projectiles.Count);
            Assert.AreEqual(B.Mb, s.Scopes[0].Projectiles[0].Block.Id);
            Assert.AreEqual(0, s.Scopes[0].OwnerSlot);
            Assert.AreEqual(1, s.Scopes[0].SplitCount);
            Assert.AreEqual(9, s.FireIntervalTicks);
            Assert.AreEqual(Hu(4f), s.HeatPerShotHu);
            Assert.AreEqual(1, s.InstancesTotal);
            Assert.AreEqual(0, s.Diagnostics.Count);
            Assert.AreEqual(5, s.SlotBlockIds.Length);
            Assert.AreEqual(B.Mb, s.SlotBlockIds[0]);
        }

        [Test]
        public void S02_C02__CoreEmpty()
        {
            var s = _k.Compile(null, B.None, B.None, B.None, B.None);
            Assert.IsFalse(s.IsValid);
            Assert.AreEqual(1, s.Diagnostics.Count);
            Assert.AreEqual(SpellDiagCode.E_CORE_EMPTY, s.Diagnostics[0].Code);
            Assert.IsTrue(s.Diagnostics[0].IsError);
            Assert.AreEqual("warn.core_empty", s.Diagnostics[0].NameKey);
        }

        [Test]
        public void S02_C03__CoreNotProjectile()
        {
            var s = _k.Compile(B.S2, B.None, B.None, B.None, B.None);
            Assert.IsFalse(s.IsValid);
            Assert.AreEqual(SpellDiagCode.E_CORE_NOT_PROJECTILE, s.Diagnostics[0].Code);
        }

        [Test]
        public void S02_C04__SlotCountMismatch()
        {
            // 갈피(연결 5칸)에 길이 4.
            var ctx = new SpellCompileContext { LinkSlotCount = 5, DepthDecay = _k.Db.Const.DepthDecayTrait };
            var s = _k.Sys.Compile(_k.Db, B.Mb, new List<string> { null, null, null, null }, ctx);
            Assert.IsFalse(s.IsValid);
            Assert.AreEqual(SpellDiagCode.E_SLOT_COUNT_MISMATCH, s.Diagnostics[0].Code);
        }

        [Test]
        public void S02_C05__IntervalIsCoreAndLinkProjectileFiresTogether()
        {
            var s = _k.Compile(B.Fb, B.Mb, B.None, B.None, B.None);
            Assert.AreEqual(36, s.FireIntervalTicks);
            Assert.AreEqual(2, s.Scopes[0].Projectiles.Count);
            Assert.AreEqual(2, s.InstancesTotal);
        }

        [Test]
        public void S02_C06__ModifierAppliesToWholeScopeRegardlessOfOrder()
        {
            var s = _k.Compile(B.Mb, B.Size, B.Ice, B.None, B.None);
            Assert.AreEqual(1, s.Scopes.Count);
            Assert.AreEqual(2, s.Scopes[0].Projectiles.Count);
            Assert.AreEqual(0.2f, s.Scopes[0].Mods.DamageBonusSum, 1e-5f);
        }

        [Test]
        public void S02_C07__BonusAccumulates()
        {
            var s = _k.Compile(B.Mb, B.Size, B.Pierce, B.None, B.None);
            Assert.AreEqual(0.3f, s.Scopes[0].Mods.DamageBonusSum, 1e-5f);
            Assert.AreEqual(13f, s.StandardTBase, 1e-4f);
        }

        [Test]
        public void S02_C08__SecondSplitIgnored()
        {
            var s = _k.Compile(B.Mb, B.S3, B.S2, B.None, B.None);
            Assert.AreEqual(3, s.Scopes[0].SplitCount);
            Assert.AreEqual(1, s.Scopes[0].SplitSlot);
            Assert.IsTrue(Has(s, SpellDiagCode.W_SPLIT_DUPLICATE, 2));
            Assert.AreEqual(3, s.InstancesTotal);
        }

        [Test]
        public void S02_C09__SplitMultipliesEveryProjectileInScope()
        {
            var s = _k.Compile(B.Mb, B.Ice, B.S3, B.None, B.None);
            Assert.AreEqual(6, s.InstancesTotal);
        }

        [Test]
        public void S02_C10__TriggerOpensChildScope()
        {
            var s = _k.Compile(B.Mb, B.Trig, B.Fb, B.None, B.None);
            Assert.AreEqual(2, s.Scopes.Count);
            Assert.AreEqual(1, s.Scopes[1].Depth);
            Assert.AreEqual(1, s.Scopes[1].TriggerSlot);
            Assert.AreEqual(2, s.Scopes[1].OwnerSlot);
            Assert.AreEqual(B.Fb, s.Scopes[1].Projectiles[0].Block.Id);
            Assert.AreEqual(0.85f, s.Scopes[1].DepthMultiplier, 1e-5f);
        }

        [Test]
        public void S02_C11__TriggerWithoutProjectile()
        {
            var s = _k.Compile(B.Mb, B.Trig, B.None, B.None, B.None);
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(1, s.Diagnostics.Count);
            Assert.IsTrue(Has(s, SpellDiagCode.W_TRIGGER_NO_PROJECTILE, 1));
        }

        [Test]
        public void S02_C12__TriggerThenModifierOnly()
        {
            var s = _k.Compile(B.Mb, B.Trig, B.Frost, B.None, B.None);
            Assert.AreEqual(1, s.Diagnostics.Count);
            Assert.IsTrue(Has(s, SpellDiagCode.W_TRIGGER_NO_PROJECTILE, 1));
            Assert.IsFalse(HasCode(s, SpellDiagCode.W_NO_EFFECT));
            Assert.AreEqual(Hu(7f), s.HeatPerShotHu);
        }

        [Test]
        public void S02_C13__FirstProjectileAfterTriggerOwnsScope()
        {
            var s = _k.Compile(B.Mb, B.Trig, B.Ice, B.Fb, B.None);
            Assert.AreEqual(2, s.Scopes[1].OwnerSlot);
            Assert.AreEqual(2, s.Scopes[1].Projectiles.Count);
        }

        [Test]
        public void S02_C14__ParentModifiersDoNotCarryIntoChildScope()
        {
            var s = _k.Compile(B.Mb, B.Size, B.Trig, B.Fb, B.None);
            Assert.AreEqual(0.2f, s.Scopes[0].Mods.DamageBonusSum, 1e-5f);
            Assert.AreEqual(0f, s.Scopes[1].Mods.DamageBonusSum, 1e-5f);
        }

        [Test]
        public void S02_C15__FourthTriggerIgnored()
        {
            // 갈피 + 확장 3 (L = 8)
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.Trig, B.Mb, B.Trig, B.Mb, B.Trig, B.Mb, B.Trig, B.Mb);
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(4, s.Scopes.Count);
            Assert.IsTrue(Has(s, SpellDiagCode.W_TRIGGER_DEPTH, 7));
            Assert.AreEqual(2, s.Scopes[3].Projectiles.Count);
            Assert.AreEqual(8, s.Scopes[3].Projectiles[1].Slot);
        }

        [Test]
        public void S02_C16__PierceOnShadowOrbHasNoEffect()
        {
            var s = _k.Compile(B.Orb, B.Pierce, B.None, B.None, B.None);
            Assert.IsTrue(Has(s, SpellDiagCode.W_NO_EFFECT, 1));
        }

        [Test]
        public void S02_C17__PierceEffectiveWhenAnyProjectileBenefits()
        {
            var s = _k.Compile(B.Mb, B.Orb, B.Pierce, B.None, B.None);
            Assert.AreEqual(0, s.Diagnostics.Count);
        }

        [Test]
        public void S02_C18__HomingOnChainLightningHasNoEffect()
        {
            var s = _k.Compile(B.Cl, B.Homing, B.None, B.None, B.None);
            Assert.IsTrue(Has(s, SpellDiagCode.W_NO_EFFECT, 1));
        }

        [Test]
        public void S02_U31__ShadowOrbWithBounceHasNoWarning()
        {
            var s = _k.Compile(B.Orb, B.Bounce, B.None, B.None, B.None);
            Assert.AreEqual(0, s.Diagnostics.Count);
            Assert.AreEqual(2, s.Scopes[0].Mods.BounceCount);
        }

        [Test]
        public void S02_C21__DiagnosticsSortedBySlotThenCode()
        {
            var s = _k.Compile(B.Cl, B.Homing, B.S2, B.S3, B.Bounce);
            Assert.AreEqual(3, s.Diagnostics.Count);
            Assert.AreEqual(1, s.Diagnostics[0].Slot);
            Assert.AreEqual(SpellDiagCode.W_NO_EFFECT, s.Diagnostics[0].Code);
            Assert.AreEqual(3, s.Diagnostics[1].Slot);
            Assert.AreEqual(SpellDiagCode.W_SPLIT_DUPLICATE, s.Diagnostics[1].Code);
            Assert.AreEqual(4, s.Diagnostics[2].Slot);
            Assert.AreEqual(SpellDiagCode.W_NO_EFFECT, s.Diagnostics[2].Code);
        }

        // 03 요약서 4.4절: 변형 누적
        [Test]
        public void S02_ScopeModifiers__Accumulate()
        {
            var s = _k.CompileWith(false, 0f, B.Mb, B.Bounce, B.Bounce, B.Pierce, B.Size, B.Size, B.Homing, B.Triple, B.Corr);
            var m = s.Scopes[0].Mods;
            Assert.AreEqual(4, m.BounceCount);
            Assert.AreEqual(2, m.PierceCount);
            Assert.AreEqual(2, m.SizeDoublings);
            Assert.AreEqual(0.5f, m.DamageBonusSum, 1e-5f);
            Assert.IsTrue(m.Homing);
            Assert.IsTrue(m.TripleHeat);
            Assert.IsTrue(m.Corrosion);
            Assert.IsFalse(m.Frost);
        }

        // ------------------------------------------------------------ 열 (5절, S02 16.4)

        [Test]
        public void S02_D01__MagicBolt4()
        {
            Assert.AreEqual(Hu(4f), _k.Compile(B.Mb, B.None, B.None, B.None, B.None).HeatPerShotHu);
        }

        [Test]
        public void S02_D02__SplitThree8_8()
        {
            Assert.AreEqual(Hu(8.8f), _k.Compile(B.Mb, B.S3, B.None, B.None, B.None).HeatPerShotHu);
        }

        [Test]
        public void S02_D03__SplitTwoPlusSize11_2()
        {
            Assert.AreEqual(Hu(11.2f), _k.Compile(B.Mb, B.S2, B.Size, B.None, B.None).HeatPerShotHu);
        }

        [Test]
        public void S02_D04__TriggerFireball22()
        {
            Assert.AreEqual(Hu(22f), _k.Compile(B.Mb, B.Trig, B.Fb, B.None, B.None).HeatPerShotHu);
        }

        [Test]
        public void S02_D05__ChildSplit43_6()
        {
            Assert.AreEqual(Hu(43.6f), _k.Compile(B.Mb, B.Trig, B.Fb, B.S3, B.None).HeatPerShotHu);
        }

        [Test]
        public void S02_D06__ParentSplitNotAppliedToChild26_8()
        {
            Assert.AreEqual(Hu(26.8f), _k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.None).HeatPerShotHu);
        }

        // 밑줄 결과: +2→+1, +3→+1, +5→+2, 4→2, 8→4, 10→5, 12→7, 14→9, 18→13
        [TestCase(B.Bounce, 1f)]
        [TestCase(B.Size, 1f)]
        [TestCase(B.Homing, 2f)]
        [TestCase(B.Mb, 2f)]
        [TestCase(B.Ice, 4f)]
        [TestCase(B.Bm, 5f)]
        [TestCase(B.Cl, 7f)]
        [TestCase(B.Orb, 9f)]
        [TestCase(B.Fb, 13f)]
        public void S02_D07__UnderlineCutsLastLinkSlot(string block, float heatAfterCut)
        {
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.None, B.None, B.None, B.None, block);
            Assert.AreEqual(Hu(4f + heatAfterCut), s.HeatPerShotHu);
        }

        [Test]
        public void S02_D08__UnderlineFireballLast17()
        {
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.None, B.None, B.None, B.None, B.Fb);
            Assert.AreEqual(Hu(17f), s.HeatPerShotHu);
        }

        [Test]
        public void S02_D10__UnderlineCutBeforeSplitMultiplier8_0()
        {
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.None, B.None, B.None, B.S2, B.Frost);
            Assert.AreEqual(Hu(8f), s.HeatPerShotHu);
        }

        [Test]
        public void S02_D11__UnderlineDoesNotMoveToEarlierSlot22()
        {
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.None, B.None, B.None, B.Fb, B.None);
            Assert.AreEqual(Hu(22f), s.HeatPerShotHu);
        }

        [Test]
        public void S02_D12__UnderlineFollowsExtensionSlot22()
        {
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.None, B.None, B.None, B.None, B.Fb, B.None);
            Assert.AreEqual(Hu(22f), s.HeatPerShotHu);
        }

        [Test]
        public void S02_D13__UnderlineSkipsControlBlocks()
        {
            var trig = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.None, B.None, B.None, B.None, B.Trig);
            Assert.AreEqual(Hu(4f), trig.HeatPerShotHu);
            var split = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.None, B.None, B.None, B.None, B.S3);
            Assert.AreEqual(Hu(8.8f), split.HeatPerShotHu);
        }

        [Test]
        public void S02_D14__MirrorImageHeat6()
        {
            var s = _k.Compile(B.Mb, B.None, B.None, B.None, B.None);
            _k.P.Derived.MirrorImage = true;
            var r = _k.Fire(s);
            Assert.AreEqual(Hu(6f), r.HeatToAddHu);
            Assert.IsTrue(r.Mirrored);
        }

        [Test]
        public void S02_D15_D16__ExemptAndArtsAddNoHeat()
        {
            var s = _k.Compile(B.Mb, B.None, B.None, B.None, B.None);
            _k.P.Derived.MirrorImage = true;
            var req = _k.Request(s);
            req.Source = ShotSource.CopyCircle;
            req.HeatExempt = true;
            var r = _k.Sys.Execute(_k.W, req);
            Assert.AreEqual(0, r.HeatToAddHu);
            Assert.IsFalse(r.Mirrored);
            Assert.AreEqual(1, r.Spawned);
        }

        // ------------------------------------------------------------ 수와 상한 (8절, S02 16.5)

        [Test]
        public void S02_E01__TwelveNoPrune()
        {
            var s = _k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.S3);
            Assert.AreEqual(3, s.Scopes[0].InstancesTotal);
            Assert.AreEqual(9, s.Scopes[1].InstancesTotal);
            Assert.AreEqual(12, s.InstancesTotal);
            Assert.AreEqual(1, s.MaxDepthNormal);
            Assert.IsFalse(HasCode(s, SpellDiagCode.W_PROJECTILE_CAP));
        }

        [Test]
        public void S02_E02__SeventyFivePrunesDeepestScope()
        {
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.S3, B.Trig, B.Mb, B.Mb, B.S3, B.Trig, B.Mb, B.S3);
            Assert.AreEqual(3, s.Scopes[0].InstancesTotal);
            Assert.AreEqual(18, s.Scopes[1].InstancesTotal);
            Assert.AreEqual(54, s.Scopes[2].InstancesTotal);
            Assert.AreEqual(75, s.InstancesTotal);
            Assert.AreEqual(1, s.MaxDepthNormal);
            Assert.IsTrue(Has(s, SpellDiagCode.W_PROJECTILE_CAP, 2));
            // 실행되는 수 = 21
            Assert.AreEqual(21, s.Scopes[0].InstancesTotal + s.Scopes[1].InstancesTotal);
        }

        [Test]
        public void S02_E03_E04__SixtyFitsButMirroredPrunesToTwelve()
        {
            var s = _k.CompileWith(false, 0f, B.Mb, B.Mb, B.S3, B.Trig, B.Mb, B.Mb, B.Mb, B.S3);
            Assert.AreEqual(6, s.Scopes[0].InstancesTotal);
            Assert.AreEqual(54, s.Scopes[1].InstancesTotal);
            Assert.AreEqual(60, s.InstancesTotal);
            Assert.AreEqual(1, s.MaxDepthNormal);
            Assert.IsFalse(HasCode(s, SpellDiagCode.W_PROJECTILE_CAP));
            Assert.AreEqual(120, s.InstancesTotalMirrored);
            Assert.AreEqual(0, s.MaxDepthMirrored);

            _k.P.Derived.MirrorImage = true;
            var r = _k.Fire(s);
            Assert.AreEqual(12, r.Spawned);
            foreach (var pr in _k.W.Projectiles) Assert.AreEqual(0, pr.MaxDepth);
        }

        [Test]
        public void S02_E05__MirrorNotAppliedToOtherArts()
        {
            var s = _k.CompileWith(false, 0f, B.Mb, B.Mb, B.S3, B.Trig, B.Mb, B.Mb, B.Mb, B.S3);
            _k.P.Derived.MirrorImage = true;
            var req = _k.Request(s);
            req.Source = ShotSource.ArtOther;
            var r = _k.Sys.Execute(_k.W, req);
            Assert.IsFalse(r.Mirrored);
            Assert.AreEqual(6, r.Spawned);
            foreach (var pr in _k.W.Projectiles) Assert.AreEqual(1, pr.MaxDepth);
        }

        [Test]
        public void S02_E06__LargestRootFitsWithMirror()
        {
            var s = _k.CompileWith(true, _k.Db.Const.DepthDecayTrait, B.Mb, B.Mb, B.Mb, B.Mb, B.Mb, B.Mb, B.Mb, B.Mb, B.S3);
            // 코어 + 연결 7 = 8발 × 3 = 24, 거울상 48 ≤ 64 (투사체 9개면 분열 칸이 없다)
            Assert.AreEqual(24, s.InstancesTotal);
            Assert.AreEqual(0, s.MaxDepthMirrored);
            Assert.IsFalse(HasCode(s, SpellDiagCode.W_PROJECTILE_CAP));
        }

        // ------------------------------------------------------------ 표준 T, 디버그 출력

        [Test]
        public void S02_H26__StandardTMagicBolt10()
        {
            var s = _k.Compile(B.Mb, B.None, B.None, B.None, B.None);
            Assert.AreEqual(10f, s.StandardTBase, 1e-5f);
            _k.P.Spell = s;
            Assert.AreEqual(10f, _k.Sys.StandardT(_k.W, _k.P), 1e-5f);
        }

        [Test]
        public void S02_D07_StandardT__RootScopeOnlyWithSplitTimesEnhanceAndMutation()
        {
            // 00 문서 D-07: 루트 스코프만(분열 반영) × 강화 × 변이. 하위 스코프(화염구)는 넣지 않는다.
            var s = _k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.None);
            Assert.AreEqual(30f, s.StandardTBase, 1e-4f);
            _k.P.Spell = s;
            _k.P.Derived.EnhanceMul = 1.15f;
            _k.P.Derived.MutationDamageMul = 1.6f;
            Assert.AreEqual(30f * 1.15f * 1.6f, _k.Sys.StandardT(_k.W, _k.P), 1e-3f);
        }

        [Test]
        public void S02_J01__DebugDumpMatchesSpecExample()
        {
            // 10 요약서 9절의 예(정본 id로).
            var s = _k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.None);
            string expected =
                "SPELL interval=9t heat=26.80 count=6/64 mirror_count=12/64 T=30.00\n" +
                "S0 depth=0 decay=1.00 split=3@1 heat=8.80 inst=3\n" +
                "  [0] P block.magic_bolt *owner\n" +
                "S1 depth=1 decay=0.85 trigger@2 split=1 heat=18.00 inst=3\n" +
                "  [3] P block.fireball *owner\n" +
                "DIAG none";
            Assert.AreEqual(expected, _k.Sys.DebugDump(s));
            Assert.AreEqual(expected, new Wizard.Solo.Sim.Spells.SpellSystem().DebugDump(s),
                "A compiled tree keeps its diagnostics when another module renders the preview.");
        }

        [Test]
        public void S02_J01b__DebugDumpListsModifiersAndDiagnostics()
        {
            var s = _k.Compile(B.Mb, B.Frost, B.S3, B.S2, B.None);
            string dump = _k.Sys.DebugDump(s);
            StringAssert.Contains("  [1] M block.frost", dump);
            StringAssert.Contains("  [3] C block.split_two ignored", dump);
            StringAssert.Contains("DIAG W_SPLIT_DUPLICATE@3", dump);
            StringAssert.Contains("SPELL invalid", _k.Sys.DebugDump(_k.Compile(null, B.None, B.None, B.None, B.None)));
        }

        [Test]
        public void S02_C20__SameInputSameOutput()
        {
            var a = _k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.Frost);
            var b = _k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.Frost);
            Assert.AreEqual(_k.Sys.DebugDump(a), _k.Sys.DebugDump(b));
        }

        // 03 요약서 2.2절 TV-A02: 등급별 일반 6, 희귀 6, 영웅 4
        [Test]
        public void S02_TV_A02__BlockCountsByRarity()
        {
            Assert.AreEqual(16, _k.Db.Blocks.Count);
            Assert.AreEqual(6, _k.Db.BlocksOfRarity(Ids.Rarity.Common).Count);
            Assert.AreEqual(6, _k.Db.BlocksOfRarity(Ids.Rarity.Rare).Count);
            Assert.AreEqual(4, _k.Db.BlocksOfRarity(Ids.Rarity.Hero).Count);
        }
    }
}
