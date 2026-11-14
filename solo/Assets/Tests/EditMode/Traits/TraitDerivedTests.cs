using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Traits;

namespace Wizard.Solo.Tests.Traits
{
    /// <summary>PlayerDerived(02 요약서 6.1·8.2·9.1절, 03 요약서 9절, 07 요약서 4.10절)와 컴파일 문맥.</summary>
    public class TraitDerivedTests
    {
        static readonly string[] None = new string[0];
        static readonly string[] Chars = { "char.1", "char.2", "char.3", "char.4", "char.5" };

        static PlayerDerived Derive(string charId, int stage, params string[] mutations)
        {
            var db = TestContent.Db;
            return new TraitSystem().ComputeDerived(db, db.Character(charId), stage, mutations);
        }

        // 02 요약서 6.1절: 임계 시작 기준 85(10,200) / char.1 80(9,600) / char.4 90(10,800).
        [TestCase("char.1", 9600)]
        [TestCase("char.2", 10200)]
        [TestCase("char.3", 10200)]
        [TestCase("char.4", 10800)]
        [TestCase("char.5", 10200)]
        public void S01_7_1__CritStartHuPerCharacter(string charId, int expectedHu)
        {
            Assert.AreEqual(expectedHu, Derive(charId, 0).CritStartHu);
        }

        // 02 요약서 6.1절: 과민 회로 70(8,400) — 여럿이면 가장 낮은 값.
        [TestCase("char.1")]
        [TestCase("char.2")]
        [TestCase("char.3")]
        [TestCase("char.4")]
        [TestCase("char.5")]
        public void S04_9_4__HypersensitiveCritStartIs70(string charId)
        {
            var d = Derive(charId, 0, Ids.Mutation.HypersensitiveCircuit);
            Assert.AreEqual(8400, d.CritStartHu);
            Assert.IsTrue(d.Hypersensitive);
        }

        // 02 요약서 6.1절 잠금 틱(3.0초 180 / char.2 2.5초 150 / char.3 3.5초 210),
        // 9.1절 과민 회로 +90틱(char.1 4.5초, char.2 4.0, char.3 5.0, char.4·5 4.5).
        [TestCase("char.1", 180, 270)]
        [TestCase("char.2", 150, 240)]
        [TestCase("char.3", 210, 300)]
        [TestCase("char.4", 180, 270)]
        [TestCase("char.5", 180, 270)]
        public void S01_7_5__LockTicksAndHypersensitivePlus90(string charId, int baseTicks, int hyperTicks)
        {
            Assert.AreEqual(baseTicks, Derive(charId, 0).LockTicks);
            Assert.AreEqual(hyperTicks, Derive(charId, 0, Ids.Mutation.HypersensitiveCircuit).LockTicks);
        }

        // 02 요약서 6.1·6.4절: 냉각 45/초 = 90 HU/틱, char.2 50/초 = 100 HU/틱. 탐식이면 절반(45, 50). 냉각 대기 12틱.
        [TestCase("char.1", 90, 45)]
        [TestCase("char.2", 100, 50)]
        [TestCase("char.3", 90, 45)]
        [TestCase("char.4", 90, 45)]
        [TestCase("char.5", 90, 45)]
        public void S01_7_4__CoolingAndGluttonyHalf(string charId, int coolHu, int gluttonyHu)
        {
            var d = Derive(charId, 0);
            Assert.AreEqual(coolHu, d.CoolHuPerTick);
            Assert.AreEqual(12, d.CoolDelayTicks);
            var g = Derive(charId, 0, Ids.Mutation.Gluttony);
            Assert.AreEqual(gluttonyHu, g.CoolHuPerTick);
            Assert.IsTrue(g.Gluttony);
        }

        // 02 요약서 8.2절: 무적 1.0초 = 60틱 전원, 스침 가속 1.5초 = 90틱 / char.2 2.0초 = 120틱.
        [TestCase("char.1", 90)]
        [TestCase("char.2", 120)]
        [TestCase("char.3", 90)]
        [TestCase("char.4", 90)]
        [TestCase("char.5", 90)]
        public void S04_2_2__InvulnAndGrazeBuffTicks(string charId, int grazeBuffTicks)
        {
            var d = Derive(charId, 0);
            Assert.AreEqual(60, d.InvulnTicks);
            Assert.AreEqual(grazeBuffTicks, d.GrazeBuffTicks);
        }

        // 03 요약서 9절 강화 배율 표(1.15^n, 14개).
        [TestCase(0, 1.000000f)]
        [TestCase(1, 1.15f)]
        [TestCase(2, 1.3225f)]
        [TestCase(3, 1.520875f)]
        [TestCase(4, 1.749006f)]
        [TestCase(5, 2.011357f)]
        [TestCase(6, 2.313061f)]
        [TestCase(7, 2.660020f)]
        [TestCase(8, 3.059023f)]
        [TestCase(9, 3.517876f)]
        [TestCase(10, 4.045558f)]
        [TestCase(11, 4.652391f)]
        [TestCase(12, 5.350250f)]
        [TestCase(13, 6.152788f)]
        public void S04_9_2__EnhanceMulTable(int stage, float expected)
        {
            for (int c = 0; c < Chars.Length; c++)
                Assert.AreEqual(expected, Derive(Chars[c], stage).EnhanceMul, 1e-5f, Chars[c]);
        }

        // 02 요약서 8.3절: 깊이 감쇠 0.85, wand_trait.depth_decay(char.3)만 0.92. 밑줄은 char.3만.
        [TestCase("char.1", 0.85f, false)]
        [TestCase("char.2", 0.85f, false)]
        [TestCase("char.3", 0.92f, true)]
        [TestCase("char.4", 0.85f, false)]
        [TestCase("char.5", 0.85f, false)]
        public void S04_7_1__DepthDecayAndUnderline(string charId, float decay, bool underline)
        {
            var d = Derive(charId, 0);
            Assert.AreEqual(decay, d.DepthDecay, 1e-6f);
            Assert.AreEqual(underline, d.Underline);
        }

        // 02 요약서 8.1·8.3절: 얼린 결정 = char.4, 외상(빚 2개까지) = char.5.
        [TestCase("char.1", false, false, 0)]
        [TestCase("char.2", false, false, 0)]
        [TestCase("char.3", false, false, 0)]
        [TestCase("char.4", true, false, 0)]
        [TestCase("char.5", false, true, 2)]
        public void S04_7_3__KeepCrystalAndCredit(string charId, bool keep, bool credit, int maxDebt)
        {
            var d = Derive(charId, 0);
            Assert.AreEqual(keep, d.KeepCrystal);
            Assert.AreEqual(credit, d.HasCredit);
            Assert.AreEqual(maxDebt, d.MaxDebt);
        }

        // 02 요약서 9.1절: 뒤틀린 총열 변이 배율 ×1.6, 유리 지팡이 임계 배율 +1, 거울상·불안정 핵은 플래그.
        [Test]
        public void S04_11__MutationFlagsAndValues()
        {
            var none = Derive("char.3", 0);
            Assert.AreEqual(1f, none.MutationDamageMul, 1e-6f);
            Assert.AreEqual(0, none.CritBonus);
            Assert.IsFalse(none.TwistedBarrel || none.UnstableCore || none.Hypersensitive || none.Gluttony || none.MirrorImage || none.GlassWand);

            var twisted = Derive("char.3", 0, Ids.Mutation.TwistedBarrel);
            Assert.IsTrue(twisted.TwistedBarrel);
            Assert.AreEqual(1.6f, twisted.MutationDamageMul, 1e-6f);

            var glass = Derive("char.3", 0, Ids.Mutation.GlassWand);
            Assert.IsTrue(glass.GlassWand);
            Assert.AreEqual(1, glass.CritBonus);
            Assert.AreEqual(1f, glass.MutationDamageMul, 1e-6f);

            Assert.IsTrue(Derive("char.3", 0, Ids.Mutation.MirrorImage).MirrorImage);
            Assert.IsTrue(Derive("char.3", 0, Ids.Mutation.UnstableCore).UnstableCore);

            var all = Derive("char.2", 0, Ids.Mutation.TwistedBarrel, Ids.Mutation.GlassWand, Ids.Mutation.HypersensitiveCircuit);
            Assert.AreEqual(1.6f, all.MutationDamageMul, 1e-6f);
            Assert.AreEqual(1, all.CritBonus);
            Assert.AreEqual(8400, all.CritStartHu);
            Assert.AreEqual(240, all.LockTicks);
        }

        // 02 요약서 6.7절 화력 검산 표(S04 14.1): 임계 유지 DPS = 20n ÷ (0.15n + 0.2 + 4n ÷ 냉각).
        // n = 임계 사격 가능 횟수 = (12000 − critStartHu) / 480 + 1(6.6절). Derived 값만으로 표의 수를 낸다.
        [TestCase("char.1", 73.5f)]
        [TestCase("char.2", 71.4f)]
        [TestCase("char.3", 69.2f)]
        [TestCase("char.4", 65.5f)]
        [TestCase("char.5", 69.2f)]
        public void S04_14_1__CritHoldDpsFromDerived(string charId, float expectedDps)
        {
            var d = Derive(charId, 0);
            const int shotHu = 4 * SimConst.HuPerHeat;        // 마력탄 열 4
            int n = (100 * SimConst.HuPerHeat - d.CritStartHu) / shotHu + 1;
            float coolPerSecond = d.CoolHuPerTick * SimConst.TicksPerSecond / (float)SimConst.HuPerHeat;
            float delaySeconds = d.CoolDelayTicks / (float)SimConst.TicksPerSecond;
            float dps = 20f * n / (0.15f * n + delaySeconds + 4f * n / coolPerSecond);
            Assert.AreEqual(expectedDps, dps, 0.06f);
        }

        // 계약: ComputeDerived는 SimWorld·PlayerState 없이 돌고, RecomputeDerived가 넣는 값과 같다(캐릭터 5 × 변이 조합 64 × 단계).
        [Test]
        public void Contract__ComputeDerivedMatchesRecomputeDerived()
        {
            var db = TestContent.Db;
            var pure = new TraitSystem();
            int[] stages = { 0, 5, 13 };
            for (int c = 0; c < Chars.Length; c++)
            for (int mask = 0; mask < 64; mask++)
            for (int si = 0; si < stages.Length; si++)
            {
                var muts = new List<string>();
                for (int b = 0; b < Ids.Mutation.All.Length; b++)
                    if ((mask & (1 << b)) != 0) muts.Add(Ids.Mutation.All[b]);

                var expected = pure.ComputeDerived(db, db.Character(Chars[c]), stages[si], muts);
                var again = pure.ComputeDerived(db, db.Character(Chars[c]), stages[si], muts);
                Assert.AreEqual(expected, again, "pure function");

                var rig = new TraitRig(Chars[c], stages[si], Ids.Block.Fireball, 7, muts.ToArray());
                Assert.AreEqual(expected, rig.P.Derived, Chars[c] + " mask " + mask + " stage " + stages[si]);
            }
        }

        // 계약: RecomputeDerived는 실효 단계(임시 하락 반영)로 만든다. 07 요약서 4.10절: +10, 임시 하락 1 → 1.15^9.
        [Test]
        public void S04_TV_108__RecomputeUsesEffectiveStage()
        {
            var rig = new TraitRig("char.1", 10);
            Assert.AreEqual(4.045558f, rig.P.Derived.EnhanceMul, 1e-5f);
            rig.P.Wand.TempStageDrop = 1;
            rig.Traits.RecomputeDerived(rig.W, rig.P);
            Assert.AreEqual(3.517876f, rig.P.Derived.EnhanceMul, 1e-5f);
        }

        // 계약: 컴파일 문맥(밑줄, 감쇠 0.92, 연결 칸 수)은 Derived에서 그대로 온다.
        [Test]
        public void Contract__CompileContextCarriesUnderlineAndDecay()
        {
            var t = new TraitSystem();
            var d3 = Derive("char.3", 0);
            var c3 = t.CompileContext(d3, 5);
            Assert.AreEqual(5, c3.LinkSlotCount);
            Assert.IsTrue(c3.Underline);
            Assert.AreEqual(0.92f, c3.DepthDecay, 1e-6f);

            var d1 = Derive("char.1", 0);
            var c1 = t.CompileContext(d1, 7);
            Assert.AreEqual(7, c1.LinkSlotCount);
            Assert.IsFalse(c1.Underline);
            Assert.AreEqual(0.85f, c1.DepthDecay, 1e-6f);
        }

        // 기준값 캐릭터(id 없음): 기준 85, 3.0초, 45/초. 패시브·특성 없음.
        [Test]
        public void S01_5_1__BaselineCharacter()
        {
            var db = TestContent.Db;
            var d = new TraitSystem().ComputeDerived(db, db.BaselineCharacter, 0, None);
            Assert.AreEqual(10200, d.CritStartHu);
            Assert.AreEqual(180, d.LockTicks);
            Assert.AreEqual(90, d.CoolHuPerTick);
            Assert.AreEqual(0.85f, d.DepthDecay, 1e-6f);
            Assert.IsFalse(d.Underline || d.KeepCrystal || d.HasCredit);
        }

        [Test]
        public void Contract__FactoryRegistersTraitSystem()
        {
            Assert.IsInstanceOf<TraitSystem>(SimModuleSet.CreateDefault().Traits);
        }
    }
}
