using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>07 요약서 12.1절(확률 계산), 12.2절(판정 구간), 12.3절(비용과 강화석), 08 요약서 10.4절(확률 표시).</summary>
    public class EnhanceProbabilityTests
    {
        static EnhanceConfig Cfg => TestContent.Db.Enhance;
        static IContentDb Db => TestContent.Db;

        static string[] Repeat(string id, int n)
        {
            var a = new string[n];
            for (int i = 0; i < n; i++) a[i] = id;
            return a;
        }

        static int Pct(int stage, int aura, SacrificeBreakdown sac) => EnhanceRules.SuccessPct(Cfg, stage, aura, sac.TotalPct);

        // ------------------------------------------------------------ 12.1 확률 계산

        [Test]
        public void S07_TV_P_01__StageZeroIsAlways100WithNoGreat()
        {
            // stage 0, aura 3, 제물 영웅 5개 → 100, great_bp = 0
            var sac = EnhanceRules.SacrificeBonus(Cfg, Db, Repeat(Ids.Block.Homing, 5), Ids.Element.Fire);
            Assert.AreEqual(100, Pct(0, 3, sac));
            Assert.AreEqual(0, EnhanceRules.GreatBp(Cfg, 0, 100));
            ProbBreakdown b = EnhanceRules.Breakdown(Cfg, 0, 0, 3, sac);
            Assert.AreEqual(100, b.SuccessPct);
            Assert.AreEqual(0, b.GreatBp);
            Assert.AreEqual(10000, b.SuccessBp);
            Assert.IsFalse(b.Capped);
        }

        [Test]
        public void S07_TV_P_02__Stage7Sacrifice22SameElementAura2Is82()
        {
            // 등급 합 22(희귀 8 + 영웅 14), 같은 속성 있음, aura 2 → 25 + 22 + 5 + 30 = 82
            var sac = EnhanceRules.SacrificeBonus(Cfg, Db, new[] { Ids.Block.Fireball, Ids.Block.ChainLightning }, Ids.Element.Fire);
            Assert.AreEqual(22, sac.GradeSum);
            Assert.AreEqual(5, sac.SameElementPct);
            Assert.AreEqual(82, Pct(7, 2, sac));
        }

        [Test]
        public void S07_TV_P_03__Stage1Aura1HitsCap95()
        {
            var none = EnhanceRules.SacrificeBonus(Cfg, Db, null, Ids.Element.Fire);
            Assert.AreEqual(95, Pct(1, 1, none));
            Assert.IsTrue(EnhanceRules.IsCapped(Cfg, 1, 1, none.TotalPct));
            Assert.IsTrue(EnhanceRules.Breakdown(Cfg, 1, 1, 1, none).Capped);
        }

        [Test]
        public void S07_TV_P_04__SacrificeCapCoversGradeAndElementTogether()
        {
            // stage 12, 영웅 3개(42) + 같은 속성, aura 3 → 제물 min(40, 47) = 40 → 3 + 40 + 45 = 88
            var sac = EnhanceRules.SacrificeBonus(Cfg, Db, Repeat(Ids.Block.Homing, 3), Ids.Element.Lightning);
            Assert.AreEqual(42, sac.GradeSum);
            Assert.AreEqual(40, sac.TotalPct);
            Assert.AreEqual(88, Pct(12, 3, sac));
        }

        [Test]
        public void S07_TV_P_05__FiveHeroOtherElementIs40()
        {
            var sac = EnhanceRules.SacrificeBonus(Cfg, Db, Repeat(Ids.Block.Homing, 5), Ids.Element.Fire);
            Assert.AreEqual(70, sac.GradeSum);
            Assert.AreEqual(0, sac.SameElementPct);
            Assert.AreEqual(40, sac.TotalPct);
        }

        [Test]
        public void S07_TV_P_06__ThreeCommonOneSameElementIs17()
        {
            var sac = EnhanceRules.SacrificeBonus(Cfg, Db,
                new[] { Ids.Block.WallBounce, Ids.Block.SplitTwo, Ids.Block.DoubleSize }, Ids.Element.Fire);
            Assert.AreEqual(17, sac.TotalPct);
        }

        [Test]
        public void S07_TV_P_07__SameElementBonusCountsOncePerAttempt()
        {
            // 일반 2개, 둘 다 같은 속성 → 8 + 5 = 13
            var sac = EnhanceRules.SacrificeBonus(Cfg, Db, Repeat(Ids.Block.DoubleSize, 2), Ids.Element.Fire);
            Assert.AreEqual(13, sac.TotalPct);
        }

        [Test]
        public void S07_TV_P_08__OneRareNeutralIs8()
        {
            var sac = EnhanceRules.SacrificeBonus(Cfg, Db, new[] { Ids.Block.SplitThree }, Ids.Element.Fire);
            Assert.AreEqual(8, sac.TotalPct);
        }

        [Test]
        public void S07_TV_P_09__SixSacrificesAreInvalid()
        {
            var kit = new EnhanceKit();
            kit.Rich();
            kit.Give(Ids.Block.Frost, 6);
            WandData w = kit.Set(5, 5, 0);
            Assert.AreEqual(ErrorCode.InvalidSacrifice, kit.Service.Enhance(w.Id, Repeat(Ids.Block.Frost, 6), false).Error);
            Assert.AreEqual(ErrorCode.InvalidSacrifice, kit.Service.Preview(w.Id, Repeat(Ids.Block.Frost, 6), false).Blocked);
            Assert.AreEqual(6, kit.Qty(Ids.Block.Frost), "nothing was spent");
            Assert.AreEqual(ErrorCode.None, kit.Service.Preview(w.Id, Repeat(Ids.Block.Frost, 5), false).Blocked);
        }

        [Test]
        public void S07_TV_P_10__Stage11NoBonusIs5()
        {
            Assert.AreEqual(5, EnhanceRules.SuccessPct(Cfg, 11, 0, 0));
        }

        [Test]
        public void S07_TV_P_11__Stage9Aura3Is57()
        {
            Assert.AreEqual(57, EnhanceRules.SuccessPct(Cfg, 9, 3, 0));
        }

        [Test]
        public void S07_TV_P_12__BaseProbabilityTable()
        {
            int[] expected = { 100, 90, 75, 60, 50, 40, 32, 25, 18, 12, 8, 5, 3 };
            Assert.AreEqual(expected.Length, Cfg.BaseProb.Count);
            for (int s = 0; s < expected.Length; s++) Assert.AreEqual(expected[s], EnhanceRules.BasePct(Cfg, s), "stage " + s);
        }

        // ------------------------------------------------------------ 12.2 판정 구간

        [Test]
        public void S07_TV_R_01__BandsAt82()
        {
            // great [0,1230), success [1230,8200), mutation [8200,8650), fail [8650,10000)
            Assert.AreEqual(1230, EnhanceRules.GreatBp(Cfg, 7, 82));
            Assert.AreEqual(8200, EnhanceRules.SuccessBp(82));
            Assert.AreEqual(8650, EnhanceRules.MutationEndBp(Cfg, 82));
        }

        [Test]
        public void S07_TV_R_02__Roll3720IsSuccess()
        {
            Assert.AreEqual(JudgeBand.Success, EnhanceRules.Judge(3720, 8200, 1230, 8650));
        }

        [Test]
        public void S07_TV_R_03__GreatBoundaryIsExclusive()
        {
            Assert.AreEqual(JudgeBand.GreatSuccess, EnhanceRules.Judge(1229, 8200, 1230, 8650));
            Assert.AreEqual(JudgeBand.Success, EnhanceRules.Judge(1230, 8200, 1230, 8650));
        }

        [Test]
        public void S07_TV_R_04__SuccessAndMutationBoundaries()
        {
            Assert.AreEqual(JudgeBand.Success, EnhanceRules.Judge(8199, 8200, 1230, 8650));
            Assert.AreEqual(JudgeBand.Mutation, EnhanceRules.Judge(8200, 8200, 1230, 8650));
            Assert.AreEqual(JudgeBand.Mutation, EnhanceRules.Judge(8649, 8200, 1230, 8650));
            Assert.AreEqual(JudgeBand.Fail, EnhanceRules.Judge(8650, 8200, 1230, 8650));
        }

        [Test]
        public void S07_TV_R_05__StageZeroEveryRollIsSuccess()
        {
            ProbBreakdown b = EnhanceRules.Breakdown(Cfg, 0, 0, 0, new SacrificeBreakdown());
            Assert.AreEqual(0, b.GreatBp);
            Assert.AreEqual(10000, b.SuccessBp);
            Assert.AreEqual(JudgeBand.Success, EnhanceRules.Judge(0, b.SuccessBp, b.GreatBp, b.MutationEndBp));
            Assert.AreEqual(JudgeBand.Success, EnhanceRules.Judge(9999, b.SuccessBp, b.GreatBp, b.MutationEndBp));
        }

        [Test]
        public void S07_TV_R_06__BandsAt95()
        {
            Assert.AreEqual(1425, EnhanceRules.GreatBp(Cfg, 1, 95));
            Assert.AreEqual(9625, EnhanceRules.MutationEndBp(Cfg, 95));
        }

        [Test]
        public void S07_TV_R_07__BandsAt3()
        {
            Assert.AreEqual(45, EnhanceRules.GreatBp(Cfg, 12, 3));
            Assert.AreEqual(2725, EnhanceRules.MutationEndBp(Cfg, 3));
        }

        [Test]
        public void S07_TV_R_08__GreatIsExactly15PercentOfSuccess()
        {
            for (int p = 1; p <= 95; p++)
            {
                int great = EnhanceRules.GreatBp(Cfg, 5, p);
                int success = EnhanceRules.SuccessBp(p);
                Assert.AreEqual(success * 15, great * 100, "p = " + p);   // great / success = 0.15 정확히
            }
        }

        // ------------------------------------------------------------ 12.3 비용과 강화석

        [Test]
        public void S07_TV_C_01__GoldCostTable()
        {
            long[] expected = { 100, 400, 900, 1600, 2500, 3600, 4900, 6400, 8100, 10000, 12100, 14400, 16900 };
            for (int s = 0; s < expected.Length; s++) Assert.AreEqual(expected[s], EnhanceRules.Cost(Cfg, s), "stage " + s);
        }

        [Test]
        public void S07_TV_C_02__StoneTiers()
        {
            Assert.AreEqual(StoneTier.Low, EnhanceRules.StoneTierFor(Cfg, 4));
            Assert.AreEqual(StoneTier.Mid, EnhanceRules.StoneTierFor(Cfg, 5));
            Assert.AreEqual(StoneTier.Mid, EnhanceRules.StoneTierFor(Cfg, 8));
            Assert.AreEqual(StoneTier.High, EnhanceRules.StoneTierFor(Cfg, 9));
            Assert.AreEqual(StoneTier.High, EnhanceRules.StoneTierFor(Cfg, 12));
            Assert.AreEqual(Ids.Item.StoneLow, EnhanceRules.StoneId(StoneTier.Low));
            Assert.AreEqual(Ids.Item.StoneMid, EnhanceRules.StoneId(StoneTier.Mid));
            Assert.AreEqual(Ids.Item.StoneHigh, EnhanceRules.StoneId(StoneTier.High));
        }

        [Test]
        public void S07_TV_C_03__BeginnerThreeAttemptsSpend1400GoldAnd3LowStones()
        {
            var kit = new EnhanceKit();
            kit.Gold = 1400;
            kit.Give(Ids.Item.StoneLow, 3);
            WandData w = kit.Wand();
            kit.Succeed(w); kit.Succeed(w); kit.Succeed(w);
            Assert.AreEqual(3, w.Stage);
            Assert.AreEqual(0, kit.Gold);
            Assert.AreEqual(0, kit.Qty(Ids.Item.StoneLow));
        }

        [Test]
        public void S07_TV_C_04__OneGoldShortIsRefused()
        {
            var kit = new EnhanceKit();
            kit.Gold = 16899;
            kit.Give(Ids.Item.StoneHigh, 1);
            WandData w = kit.Set(12, 12, 0);
            Assert.AreEqual(ErrorCode.NotEnoughGold, kit.Service.Enhance(w.Id, null, false).Error);
            Assert.AreEqual(16899, kit.Gold);
            Assert.AreEqual(1, kit.Qty(Ids.Item.StoneHigh));
            Assert.AreEqual(0, kit.Store.SaveCount);
        }

        [Test]
        public void S07_TV_C_05__WrongTierStoneDoesNotCount()
        {
            var kit = new EnhanceKit();
            kit.Gold = 1_000_000;
            kit.Give(Ids.Item.StoneMid, 5);
            WandData w = kit.Set(9, 9, 0);
            Assert.AreEqual(ErrorCode.NotEnoughStone, kit.Service.Enhance(w.Id, null, false).Error);
            Assert.AreEqual(Ids.Item.StoneHigh, kit.Preview(w).StoneId);
        }

        [Test]
        public void S07_TV_C_06__BoundStonesAreSpentFirst()
        {
            var kit = new EnhanceKit();
            kit.Gold = 100;
            ItemStack low = kit.Profile.Item(Ids.Item.StoneLow);
            low.Qty = 3; low.QtyBound = 1;   // 귀속 1 + 거래 가능 2
            kit.Succeed(kit.Wand());
            Assert.AreEqual(0, low.QtyBound);
            Assert.AreEqual(2, low.Qty - low.QtyBound, "tradable count is unchanged");
        }

        [Test]
        public void S07_TV_C_07__CostIsSpentWhateverTheOutcome()
        {
            // 대성공, 실패, 변이 모두: 골드·강화석·제물이 빠져 있다.
            for (int kind = 0; kind < 4; kind++)
            {
                var kit = new EnhanceKit();
                kit.Gold = 10_000;
                kit.Give(Ids.Item.StoneMid, 2);
                kit.Give(Ids.Block.Frost, 2);
                WandData w = kit.Set(5, 5, 0);
                var sac = new List<string> { Ids.Block.Frost };
                EnhanceResult r = kind == 0 ? kit.Great(w, false, sac) : kind == 1 ? kit.Succeed(w, false, sac)
                    : kind == 2 ? kit.Fail(w, false, sac) : kit.Mutate(w, false, 0, sac);
                Assert.AreEqual(10_000 - 3600, kit.Gold, "gold, outcome " + r.Outcome);
                Assert.AreEqual(1, kit.Qty(Ids.Item.StoneMid), "stone, outcome " + r.Outcome);
                Assert.AreEqual(1, kit.Qty(Ids.Block.Frost), "sacrifice, outcome " + r.Outcome);
                Assert.AreEqual(kit.Gold, r.GoldAfter);
                Assert.AreEqual(1, r.StoneMidAfter);
                Assert.AreEqual(3600, r.Breakdown.CostGold);
                if (kind == 3)
                {
                    // 변이를 거절해도 돌아오지 않는다.
                    kit.Resolve(w, false);
                    Assert.AreEqual(10_000 - 3600, kit.Gold);
                    Assert.AreEqual(1, kit.Qty(Ids.Block.Frost));
                }
            }
        }

        // ------------------------------------------------------------ 08 요약서 10.4절 확률 표시

        [Test]
        public void S13_TV_084__Stage7RareHeroSameElementAura2Is82()
        {
            var kit = new EnhanceKit();
            kit.Rich();
            kit.Give(Ids.Block.Fireball, 1); kit.Give(Ids.Block.ChainLightning, 1);
            WandData w = kit.Set(7, 7, 2);   // 제자 1 = 화염
            ProbBreakdown b = kit.Preview(w, false, new[] { Ids.Block.Fireball, Ids.Block.ChainLightning });
            Assert.AreEqual(25, b.BasePct);
            Assert.AreEqual(27, b.SacrificePct);   // 22 + 같은 속성 5
            Assert.AreEqual(30, b.AuraPct);
            Assert.AreEqual(82, b.SuccessPct);
            SacrificeBreakdown lines = kit.Service.SacrificeOf(w.Id, new[] { Ids.Block.Fireball, Ids.Block.ChainLightning });
            Assert.AreEqual(22, lines.GradeLinePct);
            Assert.AreEqual(5, lines.SameElementPct);
        }

        [Test]
        public void S13_TV_085__Stage7Best8NoCharmShowsDropTo6()
        {
            Assert.AreEqual(FailHint.DropTo, EnhanceRules.HintFor(Cfg, 7, 8, false));
            Assert.IsTrue(EnhanceRules.DropPossible(Cfg, 7, 8));   // "하락 가능" 꼬리표 = DropPossible && 부적 없음
            Assert.AreEqual(6, EnhanceRules.ResolveFail(Cfg, 7, 8, 0, false).StageAfter);
        }

        [Test]
        public void S13_TV_086__FourBandsAt82()
        {
            // 대성공 1230, 성공 6970, 변이 450, 하락 1350
            ProbBreakdown b = EnhanceRules.Breakdown(Cfg, 7, 7, 2, EnhanceRules.SacrificeBonus(Cfg, 22, true, 2));
            Assert.AreEqual(1230, b.GreatBp);
            Assert.AreEqual(6970, b.SuccessBp - b.GreatBp);
            Assert.AreEqual(450, b.MutationEndBp - b.SuccessBp);
            Assert.AreEqual(1350, EnhanceRules.BpFull - b.MutationEndBp);
        }

        [Test]
        public void S13_TV_087__StageZeroIs100PercentWithNoGreatBand()
        {
            var kit = new EnhanceKit();
            kit.Rich();
            ProbBreakdown b = kit.Preview(kit.Wand());
            Assert.AreEqual(100, b.SuccessPct);
            Assert.AreEqual(0, b.GreatBp);
            Assert.AreEqual(10000, b.SuccessBp);
            Assert.IsFalse(b.Capped);
            Assert.AreEqual(0, b.SacrificePct);
            Assert.AreEqual(0, b.AuraPct);
        }

        [Test]
        public void S13_TV_088__Stage1OneHeroIsCappedAt95()
        {
            // 90 + 14 = 104 → 95, "상한"
            ProbBreakdown b = EnhanceRules.Breakdown(Cfg, 1, 1, 0, EnhanceRules.SacrificeBonus(Cfg, 14, false, 1));
            Assert.AreEqual(104, b.RawPct);
            Assert.AreEqual(95, b.SuccessPct);
            Assert.IsTrue(b.Capped);
        }

        [Test]
        public void S13_TV_089__Stage12HeroThreeRareOneSameElementAura3__AgreedParts()
        {
            // 08 요약서 10.4절은 같은 속성 +5를 상한(+40) 밖에 두어 3 + 40 + 5 + 45 = 93이라 적고,
            // 07 요약서 13절 M-1(강화 규칙의 주인 명세 S8)은 상한을 합계에 걸어 3 + 40 + 45 = 88이다(TV-P-04와 같은 규칙).
            // 두 요약서가 어긋나므로 합계는 여기서 단정하지 않고(결과 보고에 올린다) 두 문서가 같이 말하는 부분만 본다.
            var sac = EnhanceRules.SacrificeBonus(Cfg, Db,
                new[] { Ids.Block.Homing, Ids.Block.Homing, Ids.Block.Homing, Ids.Block.SplitThree }, Ids.Element.Lightning);
            Assert.AreEqual(50, sac.GradeSum);                       // 영웅 3 + 희귀 1
            Assert.AreEqual(5, sac.SameElementPct);                  // 같은 속성은 시도당 한 번
            Assert.AreEqual(40, System.Math.Min(Cfg.Sacrifice.Cap, sac.GradeSum));   // 등급 합 50 → 40
            Assert.AreEqual(3, EnhanceRules.BasePct(Cfg, 12));
            Assert.AreEqual(45, EnhanceRules.AuraPct(Cfg, 3));
        }

        [Test]
        public void S13_TV_090__Stage3NoDropAndStage4DropsTo3()
        {
            Assert.AreEqual(FailHint.NoDropLow, EnhanceRules.HintFor(Cfg, 3, 3, false));
            Assert.AreEqual(FailHint.DropTo, EnhanceRules.HintFor(Cfg, 4, 4, false));
            Assert.AreEqual(3, EnhanceRules.ResolveFail(Cfg, 4, 4, 0, false).StageAfter);
        }

        [Test]
        public void S13_TV_091__Stage7Best9IsAtFloorAndCharmFolds()
        {
            Assert.AreEqual(FailHint.AtFloor, EnhanceRules.HintFor(Cfg, 7, 9, false));
            Assert.IsFalse(EnhanceRules.DropPossible(Cfg, 7, 9));   // 부적 칸 접힘 = !DropPossible
            Assert.AreEqual(7, EnhanceRules.DisplayFloor(Cfg, 9));
        }

        [Test]
        public void S13_TV_092__Stage8Best8WithCharmIsProtected()
        {
            Assert.AreEqual(FailHint.CharmProtects, EnhanceRules.HintFor(Cfg, 8, 8, true));
        }

        [Test]
        public void S13_TV_093__GoldCost()
        {
            Assert.AreEqual(100, EnhanceRules.Cost(Cfg, 0));
            Assert.AreEqual(6400, EnhanceRules.Cost(Cfg, 7));
            Assert.AreEqual(16900, EnhanceRules.Cost(Cfg, 12));
        }

        [Test]
        public void S13_TV_094__StoneByTarget()
        {
            // 도전 5 하, 6 중, 9 중, 10 상, 13 상 (현재 단계 = 도전 − 1)
            Assert.AreEqual(StoneTier.Low, EnhanceRules.StoneTierFor(Cfg, 5 - 1));
            Assert.AreEqual(StoneTier.Mid, EnhanceRules.StoneTierFor(Cfg, 6 - 1));
            Assert.AreEqual(StoneTier.Mid, EnhanceRules.StoneTierFor(Cfg, 9 - 1));
            Assert.AreEqual(StoneTier.High, EnhanceRules.StoneTierFor(Cfg, 10 - 1));
            Assert.AreEqual(StoneTier.High, EnhanceRules.StoneTierFor(Cfg, 13 - 1));
        }

        [Test]
        public void S13_TV_095__Gold6399CannotTryPlus8()
        {
            var kit = new EnhanceKit();
            kit.Gold = 6399;
            kit.Give(Ids.Item.StoneMid, 1);
            WandData w = kit.Set(7, 7, 0);
            Assert.AreEqual(ErrorCode.NotEnoughGold, kit.Preview(w).Blocked);
            kit.Gold = 6400;
            Assert.AreEqual(ErrorCode.None, kit.Preview(w).Blocked);
        }

        [Test]
        public void S13_TV_096__LastProjectileCannotBeSacrificed()
        {
            var kit = new EnhanceKit();
            kit.Rich();
            // 새 프로필의 투사체는 끼워 둔 마력탄 5개뿐이다. 넷을 서고로 빼 여분으로 만들고 넷 다 올리면 하나 남아 된다.
            ItemStack bolt = kit.Profile.Item(Ids.Block.MagicBolt);
            for (int c = 2; c <= 5; c++) kit.Wand(c).Loadout.Core = null;
            bolt.QtySocketed = 1;
            WandData w = kit.Set(5, 5, 0);
            Assert.AreEqual(ErrorCode.None, kit.Preview(w, false, Repeat(Ids.Block.MagicBolt, 4)).Blocked);

            // 마지막 하나까지 여분으로 만들어 다섯을 올리면 프로필에 투사체가 남지 않는다.
            w.Loadout.Core = null;
            bolt.QtySocketed = 0;
            Assert.AreEqual(ErrorCode.LastProjectile, kit.Preview(w, false, Repeat(Ids.Block.MagicBolt, 5)).Blocked);
            Assert.AreEqual(ErrorCode.LastProjectile, kit.Service.Enhance(w.Id, Repeat(Ids.Block.MagicBolt, 5), false).Error);
            Assert.AreEqual(5, bolt.Qty);

            // 끼운 블록은 제물이 되지 못한다.
            bolt.QtySocketed = 5;
            Assert.AreEqual(ErrorCode.InvalidSacrifice, kit.Preview(w, false, new[] { Ids.Block.MagicBolt }).Blocked);
        }
    }
}
