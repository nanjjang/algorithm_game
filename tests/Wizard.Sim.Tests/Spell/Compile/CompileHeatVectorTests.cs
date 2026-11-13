using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Wizard.Sim.Tests.Heat;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // spec 02 16.4 열(TV-D01~D13, D17~D19). 열은 HU(열 1 = 120, CONTRACTS 0.3).
    // D09·D17~D19 는 과열 명세와 공유하는 벡터라 컴파일한 발사 열·간격을 세계의 Heat 모듈(SetHeatShot)에 넣고 돌린다.
    // 다섯 캐릭터의 과열 수치는 HeatData 가 spec 01 7.8 표의 값으로 만든 팩을 쓴다(M5-DAT-01 전).
    // TV-D14~D16(거울상·사본 마법진·비기의 HeatToAdd)은 실행기(Execute)의 값이라 M2-SPL-03 의 벡터다(DECISIONS-TECH T-57).
    public sealed class CompileHeatVectorTests
    {
        [Fact]
        public void S02_TV_D01__BoltHeat4()
        {
            Assert.Equal(Hu(4), Dalgu1(Bolt, __, __, __, __).HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_D02__Split3_4x2Point2_Is8Point8()
        {
            Assert.Equal(HuTenths(88), Dalgu1(Bolt, Split3, __, __, __).HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_D03__Split2AndSize_7x1Point6_Is11Point2()
        {
            Assert.Equal(HuTenths(112), Dalgu1(Bolt, Split2, Size, __, __).HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_D04__TriggerFireball_4Plus18_Is22()
        {
            Assert.Equal(Hu(22), Dalgu1(Bolt, Trig, Fireball, __, __).HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_D05__TriggerFireballSplit3_4Plus18x2Point2_Is43Point6()
        {
            CompiledSpell m = Dalgu1(Bolt, Trig, Fireball, Split3, __);
            Assert.Equal(HuTenths(436), m.HeatPerShotHu);
            Assert.Equal(Hu(4), m.Scopes[0].HeatHu);
            Assert.Equal(HuTenths(396), m.Scopes[1].HeatHu);
        }

        [Fact]
        public void S02_TV_D06__ParentSplitNotAppliedToChild_26Point8()
        {
            CompiledSpell m = Dalgu1(Bolt, Split3, Trig, Fireball, __);
            Assert.Equal(HuTenths(268), m.HeatPerShotHu);
            Assert.Equal(HuTenths(88), m.Scopes[0].HeatHu);
            Assert.Equal(Hu(18), m.Scopes[1].HeatHu);                    // 부모의 분열 배수를 곱하지 않는다
        }

        [Fact]
        public void S02_TV_D07__GalpiUnderline_LastSlotHeatTable()
        {
            // 맨 끝 칸(slot 5)의 블록 열: +3 → +1, +5 → +2, 8 → 4, 14 → 9, 18 → 13 (W 4.3 표)
            var cases = new (BlockId Block, int Actual)[]
            {
                (Size, 1), (Homing, 2), (Ice, 4), (Orb, 9), (Fireball, 13),
            };
            foreach ((BlockId block, int actual) in cases)
            {
                CompiledSpell m = Build(Galpi(Bolt, L(__, __, __, __, block)));
                Assert.Equal(Hu(4 + actual), m.HeatPerShotHu);
            }
            // 7.1 아래 줄의 따라 나오는 값: +2 → +1, 10 → 5, 12 → 7(상한 5), 4 → 2
            var more = new (BlockId Block, int Actual)[] { (Bounce, 1), (BlockId.Boomerang, 5), (Chain, 7), (Bolt, 2) };
            foreach ((BlockId block, int actual) in more)
            {
                Assert.Equal(Hu(4 + actual), Build(Galpi(Bolt, L(__, __, __, __, block))).HeatPerShotHu);
            }
        }

        [Fact]
        public void S02_TV_D08__GalpiFireballLastSlot_17_Not22()
        {
            Assert.Equal(Hu(17), Build(Galpi(Bolt, L(__, __, __, __, Fireball))).HeatPerShotHu);
            // 같은 조립, 밑줄 없는 문맥이면 22
            var plain = new CompileSource(Bolt, L(__, __, __, __, Fireball), CharacterId.Char3, 5, 0, Wizard.Core.PowBase.Decay092, false, 0);
            Assert.Equal(Hu(22), Build(plain).HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_D09__ShotsUntilLock_Galpi6_Dalgu5()
        {
            // 갈피: 17 × 6 = 102 > 100 → 6발째에 잠금
            CompiledSpell galpi = Build(Galpi(Bolt, L(__, __, __, __, Fireball)));
            Assert.Equal(6, ShotsUntilLock(HeatData.Galpi, galpi));
            // 같은 조립의 달구(밑줄 없음, 연결 4칸 + 확장 1로 같은 슬롯 배열): 22 × 5 = 110 → 5발
            CompiledSpell dalgu = Build(Dalgu(Bolt, L(__, __, __, __, Fireball), ext: 1));
            Assert.Equal(Hu(22), dalgu.HeatPerShotHu);
            Assert.Equal(5, ShotsUntilLock(HeatData.Dalgu, dalgu));
        }

        [Fact]
        public void S02_TV_D10__GalpiSplit2AndFrostLast_Is8Point0()
        {
            Assert.Equal(Hu(8), Build(Galpi(Bolt, L(__, __, __, Split2, Frost))).HeatPerShotHu);   // (4 + 1) × 1.6
        }

        [Fact]
        public void S02_TV_D11__GalpiLastSlotEmpty_NoCut_22()
        {
            Assert.Equal(Hu(22), Build(Galpi(Bolt, L(__, __, __, Fireball, __))).HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_D12__GalpiExt1_LastSlotMovesTo6_22()
        {
            Assert.Equal(Hu(22), Build(Galpi(Bolt, L(__, __, __, __, Fireball, __), ext: 1)).HeatPerShotHu);
            Assert.Equal(Hu(17), Build(Galpi(Bolt, L(__, __, __, __, __, Fireball), ext: 1)).HeatPerShotHu);
        }

        [Fact]
        public void S02_TV_D13__GalpiLastSlotTriggerOrSplit_NothingCut()
        {
            Assert.Equal(Hu(4), Build(Galpi(Bolt, L(__, __, __, __, Trig))).HeatPerShotHu);
            Assert.Equal(HuTenths(88), Build(Galpi(Bolt, L(__, __, __, __, Split3))).HeatPerShotHu);   // 4 × 2.2, 배수는 깎지 않는다
        }

        [Fact]
        public void S02_TV_D17__BaselineHold_26Shots_21Plus5Crit_414Ticks_310Damage_44Point9Dps()
        {
            CompiledSpell bolt = Dalgu1(Bolt, __, __, __, __);
            HeatRig rig = ArmedRig(HeatData.Baseline, bolt);
            (List<SimEvent> shots, uint period) = HoldOneCycle(rig);
            Assert.Equal(26, shots.Count);
            Assert.Equal(5, shots.FindAll(e => e.C == 1).Count);
            Assert.Equal(21, shots.FindAll(e => e.C == 0).Count);
            Assert.Equal(414U, period);                                         // 6.9초 = 414틱
            int damage = 0;
            foreach (SimEvent e in shots)
            {
                damage += HeatRig.Damage(e, BoltDamage);
            }
            Assert.Equal(310, damage);
            Assert.Equal(44.9, Math.Round(Dps(damage, period), 1));
        }

        [Fact]
        public void S02_TV_D18__BaselineCritMaintain_N4_69Point2Dps_RatioAtLeast1Point4()
        {
            CompiledSpell bolt = Dalgu1(Bolt, __, __, __, __);
            (double maintain, int n, int locks) = CritMaintain(ArmedRig(HeatData.Baseline, bolt), bolt.HeatPerShotHu, BoltDamage);
            Assert.Equal(4, n);
            Assert.Equal(0, locks);
            Assert.InRange(maintain, 69.2 * 0.99, 69.2 * 1.01);
            (List<SimEvent> shots, uint period) = HoldOneCycle(ArmedRig(HeatData.Baseline, bolt));
            int damage = 0;
            foreach (SimEvent e in shots)
            {
                damage += HeatRig.Damage(e, BoltDamage);
            }
            double ratio = maintain / Dps(damage, period);
            Assert.True(ratio >= 1.4, "ratio " + ratio);
            Assert.InRange(ratio, 1.54 * 0.99, 1.54 * 1.01);
        }

        [Fact]
        public void S02_TV_D19__CharacterCritMaintain_Dalgu6_73Point5_Chorong4_71Point4_Seonge3_65Point5()
        {
            CompiledSpell bolt = Dalgu1(Bolt, __, __, __, __);
            var cases = new (ContentDb Db, int N, double Dps)[]
            {
                (HeatData.Dalgu, 6, 73.5),
                (HeatData.Chorong, 4, 71.4),
                (HeatData.Seonge, 3, 65.5),
            };
            foreach ((ContentDb db, int expectedN, double expectedDps) in cases)
            {
                (double dps, int n, int locks) = CritMaintain(ArmedRig(db, bolt), bolt.HeatPerShotHu, BoltDamage);
                Assert.Equal(expectedN, n);
                Assert.Equal(0, locks);
                Assert.InRange(dps, expectedDps * 0.99, expectedDps * 1.01);
            }
        }

        private static int BoltDamage => Db.Block(BlockId.MagicBolt).BaseDamage.FloorToInt();

        private static int ShotsUntilLock(ContentDb heatDb, CompiledSpell spell)
        {
            HeatRig rig = ArmedRig(heatDb, spell);
            List<SimEvent> shots = HoldOneCycle(rig).Shots;
            Assert.True(rig.H.Locked);
            return shots.Count;
        }
    }
}
