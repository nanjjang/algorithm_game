using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // spec 02 16.3 컴파일 구조(TV-C01~C21). 기대값은 명세 표에서 왔다. 명세의 블록 이름은 CONTRACTS 2.1 대응표로 읽는다
    // (great_fireball → Fireball, split_2/3 → SplitTwo/Three, pierce_2 → Pierce).
    public sealed class CompileStructureVectorTests
    {
        private const long Decay085Raw = 3_650_722_202L;   // spec 02 2.2 상수표 0.85

        [Fact]
        public void S02_TV_C01__BoltOnly_ValidOneScope_Interval9_Heat4_OneProjectile_NoDiag()
        {
            CompiledSpell m = Dalgu1(Bolt, __, __, __, __);
            Assert.True(m.IsValid);
            Assert.Single(m.Scopes);
            SpellScope s0 = m.Scopes[0];
            Assert.Equal(new[] { Bolt }, Projectiles(s0));
            Assert.Equal(0, s0.OwnerSlot);
            Assert.Equal(1, s0.SplitCount);
            Assert.Equal(9, m.FireIntervalTicks);
            Assert.Equal(Hu(4), m.HeatPerShotHu);
            Assert.Equal(1, m.InstancesTotal);
            Assert.Empty(m.Diagnostics);
        }

        [Fact]
        public void S02_TV_C02__CoreEmpty_Invalid_CoreEmpty()
        {
            CompiledSpell m = Dalgu1(__, __, __, __, __);
            Assert.False(m.IsValid);
            AssertDiags(m, (DiagCode.CoreEmpty, 0));
            Assert.Equal(DiagSeverity.Error, m.Diagnostics[0].Severity);
        }

        [Fact]
        public void S02_TV_C03__CoreSplit_Invalid_CoreNotProjectile()
        {
            CompiledSpell m = Dalgu1(Split2, __, __, __, __);
            Assert.False(m.IsValid);
            AssertDiags(m, (DiagCode.CoreNotProjectile, 0));
        }

        [Fact]
        public void S02_TV_C04__GalpiWithFourLinks_SlotCountMismatch()
        {
            CompiledSpell m = Build(Galpi(Bolt, L(__, __, __, __)));      // 갈피는 5칸
            Assert.False(m.IsValid);
            AssertDiags(m, (DiagCode.SlotCountMismatch, 0));
        }

        [Fact]
        public void S02_TV_C05__FireballCoreWithBoltLink_Interval36_TwoProjectilesSameScope()
        {
            CompiledSpell m = Dalgu1(Fireball, Bolt, __, __, __);
            Assert.True(m.IsValid);
            Assert.Equal(36, m.FireIntervalTicks);
            Assert.Single(m.Scopes);
            Assert.Equal(new[] { Fireball, Bolt }, Projectiles(m.Scopes[0]));
            Assert.Equal(2, m.InstancesTotal);
        }

        [Fact]
        public void S02_TV_C06__ModifierBeforeProjectile_AppliesToWholeScope()
        {
            CompiledSpell m = Dalgu1(Bolt, Size, Ice, __, __);
            Assert.Single(m.Scopes);
            SpellScope s0 = m.Scopes[0];
            Assert.Equal(new[] { Bolt, Ice }, Projectiles(s0));          // 둘 다 같은 스코프
            Assert.Equal(200, s0.Mods.DamageBonusPermille);              // 두 탄 모두 보너스 0.2
            Assert.Equal(new[] { 1 }, s0.Mods.SourceSlots);
        }

        [Fact]
        public void S02_TV_C07__SizeAndPierce_Bonus0Point3_Damage13()
        {
            CompiledSpell m = Dalgu1(Bolt, Size, Pierce, __, __);
            Assert.Equal(300, m.Scopes[0].Mods.DamageBonusPermille);
            Assert.Equal(Fix64.FromInt(13), m.StandardT);                // 10 × 1.3
        }

        [Fact]
        public void S02_TV_C08__TwoSplits_FirstWins_SplitDuplicateAtSlot2_ThreeProjectiles()
        {
            CompiledSpell m = Dalgu1(Bolt, Split3, Split2, __, __);
            Assert.Equal(3, m.Scopes[0].SplitCount);
            Assert.Equal(1, m.Scopes[0].SplitSlot);
            AssertDiags(m, (DiagCode.SplitDuplicate, 2));
            Assert.Equal(3, m.InstancesTotal);
        }

        [Fact]
        public void S02_TV_C09__TwoProjectilesSplit3_Six()
        {
            CompiledSpell m = Dalgu1(Bolt, Ice, Split3, __, __);
            Assert.Equal(6, m.InstancesTotal);
            Assert.Equal(6, m.Scopes[0].InstancesPerParent);
        }

        [Fact]
        public void S02_TV_C10__TriggerOpensScope1_OwnerFireballAtSlot2_Decay085()
        {
            CompiledSpell m = Dalgu1(Bolt, Trig, Fireball, __, __);
            Assert.Equal(2, m.Scopes.Length);
            Assert.Equal(new[] { Bolt }, Projectiles(m.Scopes[0]));
            SpellScope s1 = m.Scopes[1];
            Assert.Equal(1, s1.Depth);
            Assert.Equal(1, s1.TriggerSlot);
            Assert.Equal(2, s1.OwnerSlot);
            Assert.Equal(new[] { Fireball }, Projectiles(s1));
            Assert.Equal(Decay085Raw, s1.DepthMultiplier.Raw);
            Assert.Equal(Fix64.One, m.Scopes[0].DepthMultiplier);
        }

        [Fact]
        public void S02_TV_C11__TriggerWithoutProjectile_ValidWithWarning_NoChildProjectiles()
        {
            CompiledSpell m = Dalgu1(Bolt, Trig, __, __, __);
            Assert.True(m.IsValid);
            AssertDiags(m, (DiagCode.TriggerNoProjectile, 1));
            Assert.Equal(DiagSeverity.Warning, m.Diagnostics[0].Severity);
            Assert.Equal(0, m.Scopes[1].InstancesPerParent);             // 적중해도 하위 탄 0
            Assert.Equal(0, m.Scopes[1].InstancesTotal);
        }

        [Fact]
        public void S02_TV_C12__TriggerThenFrostOnly_OnlyTriggerWarning_Heat7()
        {
            CompiledSpell m = Dalgu1(Bolt, Trig, Frost, __, __);
            AssertDiags(m, (DiagCode.TriggerNoProjectile, 1));          // W_NO_EFFECT 없음
            Assert.Equal(Hu(4 + 3), m.HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_C13__ScopeOwnerIsFirstProjectileAfterTrigger()
        {
            CompiledSpell m = Dalgu1(Bolt, Trig, Ice, Fireball, __);
            SpellScope s1 = m.Scopes[1];
            Assert.Equal(2, s1.OwnerSlot);
            Assert.Equal(new[] { Ice, Fireball }, Projectiles(s1));      // 같은 스코프에서 동시
            Assert.Equal(new[] { 2, 3 }, ProjectileSlots(s1));
        }

        [Fact]
        public void S02_TV_C14__ModifierDoesNotCrossIntoChildScope()
        {
            CompiledSpell m = Dalgu1(Bolt, Size, Trig, Fireball, __);
            Assert.Equal(200, m.Scopes[0].Mods.DamageBonusPermille);
            Assert.Equal(0, m.Scopes[1].Mods.DamageBonusPermille);
        }

        [Fact]
        public void S02_TV_C15__FourthTriggerIgnored_TriggerDepthAtSlot7_Slot8JoinsScope3()
        {
            CompiledSpell m = Build(Galpi(Bolt, L(Trig, Bolt, Trig, Bolt, Trig, Bolt, Trig, Bolt), ext: 3));
            Assert.True(m.IsValid);
            Assert.Equal(4, m.Scopes.Length);
            for (int d = 0; d < 4; d++)
            {
                Assert.Equal(d, m.Scopes[d].Depth);
            }
            AssertDiags(m, (DiagCode.TriggerDepth, 7));
            Assert.Equal(new[] { 6, 8 }, ProjectileSlots(m.Scopes[3]));  // S3 투사체 2
        }

        [Fact]
        public void S02_TV_C16__ShadowOrbCoreWithPierce_NoEffectAtSlot2()
        {
            CompiledSpell m = Dalgu1(Orb, Orb, Pierce, __, __);
            AssertDiags(m, (DiagCode.NoEffect, 2));
            Assert.Equal(DiagSeverity.Warning, m.Diagnostics[0].Severity);
        }

        [Fact]
        public void S02_TV_C17__BoltCoreShadowOrbAndPierce_NoDiag()
        {
            CompiledSpell m = Dalgu1(Bolt, Orb, Pierce, __, __);
            Assert.Empty(m.Diagnostics);
        }

        [Fact]
        public void S02_TV_C18__ChainLightningWithHoming_NoEffect()
        {
            CompiledSpell m = Dalgu1(Chain, Homing, __, __, __);
            AssertDiags(m, (DiagCode.NoEffect, 1));
        }

        [Fact]
        public void S02_TV_C19__Char1Ext2_SixLinks_LastSlotIs6()
        {
            CompiledSpell m = Build(Dalgu(Bolt, L(__, __, __, __, __, __), ext: 2));
            Assert.True(m.IsValid);
            Assert.Equal(7, m.Slots.Length);                             // 슬롯 0..6
            Assert.Equal(6, m.Slots[m.Slots.Length - 1].Slot);
            CompiledSpell four = Build(Dalgu(Bolt, L(__, __, __, __), ext: 2));
            AssertDiags(four, (DiagCode.SlotCountMismatch, 0));
        }

        [Fact]
        public void S02_TV_C20__SameInputTwice_SameHashAndSameText()
        {
            CompileSource a = Dalgu(Bolt, L(Split3, Trig, Fireball, Size));
            CompileSource b = Dalgu(Bolt, L(Split3, Trig, Fireball, Size));
            CompiledSpell x = Build(a);
            CompiledSpell y = Build(b);
            Assert.Equal(x.Hash, y.Hash);
            Assert.Equal(CompileKit.Print(x), CompileKit.Print(y));
            Assert.Equal(x.HeatPerShotHu, y.HeatPerShotHu);
            Assert.Equal(x.StandardT, y.StandardT);
            Assert.NotEqual(x.Hash, Build(Dalgu(Bolt, L(Split3, Trig, Fireball, __))).Hash);
        }

        [Fact]
        public void S02_TV_C21__SeveralDiagnostics_SortedBySlotThenCode()
        {
            // 넣는 순서는 분열 중복(3) → 효과 없음(1, 스코프 마무리) → 트리거 뒤 투사체 없음(4)이다. 정렬 뒤 슬롯 1, 3, 4
            CompiledSpell m = Dalgu1(Chain, Homing, Split3, Split2, Trig);
            AssertDiags(m, (DiagCode.NoEffect, 1), (DiagCode.SplitDuplicate, 3), (DiagCode.TriggerNoProjectile, 4));
        }
    }
}
