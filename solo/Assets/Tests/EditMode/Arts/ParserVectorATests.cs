using NUnit.Framework;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Arts;

namespace Wizard.Solo.Tests.Arts
{
    /// <summary>
    /// 04 요약서 14.1절 A군(입력열 → 판정 결과). 하네스: 자세 중, 대기 없음, 커서 루트. +n = 직전 입력에서 n u 뒤.
    /// 기대값(나간 기술, 판정 직후 결정/빚, 늦은 입력 번호·초과량 u·문구의 초)은 요약서 표 그대로다.
    /// </summary>
    public class ParserVectorATests
    {
        static readonly string[] All = null;

        static ArtKit Run(string charId, string[] commons, int crystals, int debt, string inputs)
        {
            var kit = new ArtKit(charId, crystals, debt, commons);
            kit.Seq(inputs);
            Assert.IsTrue(kit.HasDecision, "K produced no decision");
            return kit;
        }

        static ArtKit Fire(string charId, string[] commons, int crystals, int debt, string inputs, string art, int crystalsAfter, int debtAfter,
            bool viaFallback = false)
        {
            var kit = Run(charId, commons, crystals, debt, inputs);
            var d = kit.Last;
            Assert.AreEqual(ArtOutcome.Fire, d.Outcome, "outcome");
            Assert.AreEqual(art, d.Art, "art");
            Assert.AreEqual(viaFallback, d.ViaFallback, "via_fallback");
            Assert.AreEqual(crystalsAfter, kit.P.Crystals, "crystals after");
            Assert.AreEqual(debtAfter, kit.P.Debt, "debt after");
            Assert.AreEqual(crystalsAfter, d.CrystalsAfter, "decision.CrystalsAfter");
            Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtFired, art), "ArtFired event");
            Assert.AreEqual(viaFallback ? 1 : 0, kit.LastEvent(SimEventType.ArtFired).A, "ArtFired.A = via_fallback");
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFizzled));
            Assert.AreEqual(CommandTrie.Root, kit.Parser.Cursor, "cursor resets after K");
            return kit;
        }

        static ArtKit Fizzle(string charId, string[] commons, int crystals, int debt, string inputs, string selected, ArtFizzleReason reason,
            int crystalsAfter, int debtAfter, int lateIndex = 0, long lateByU = 0, int lateHundredths = 0)
        {
            var kit = Run(charId, commons, crystals, debt, inputs);
            var d = kit.Last;
            Assert.AreEqual(ArtOutcome.Fizzle, d.Outcome, "outcome");
            Assert.AreEqual(reason, d.Reason, "reason");
            Assert.AreEqual(selected, d.SelectedArt, "selected art");
            Assert.AreEqual(crystalsAfter, kit.P.Crystals, "crystals after");
            Assert.AreEqual(debtAfter, kit.P.Debt, "debt after");
            Assert.AreEqual(lateIndex, d.LateIndex, "late index");
            Assert.AreEqual(lateByU, d.LateByU, "late by (u)");
            Assert.AreEqual(lateHundredths, LateText.Hundredths(d.LateByU), "late text (1/100 s)");
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFired));
            var e = kit.LastEvent(SimEventType.ArtFizzled);
            Assert.AreEqual((int)reason, e.A);
            Assert.AreEqual(lateIndex, e.B);
            Assert.AreEqual(lateHundredths / 100f, e.F, 1e-6f);
            // 불발은 재사용 대기를 시작하지 않는다(U-05).
            Assert.AreEqual(0, kit.Parser.CooldownEndU);
            return kit;
        }

        [Test]
        public void S03_TV_A001__SpearAfterThreeForwards()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, F+2304, F+2304, K+2304", Ids.Art.Spear, 3, 0);
        }

        [Test]
        public void S03_TV_A006__CopyCircleViaFailLink()
        {
            Fire(Ids.Char.C3, All, 4, 0, "F, F+2304, L+2304, K+2304", Ids.Art.CopyCircle, 2, 0);
        }

        [Test]
        public void S03_TV_A008__VentAfterBackBackForward()
        {
            Fire(Ids.Char.C1, All, 5, 0, "B, B+2304, F+2304, K+2304", Ids.Art.Vent, 3, 0);
        }

        [Test]
        public void S03_TV_A009__AfterimageAfterLeftRightRight()
        {
            Fire(Ids.Char.C5, All, 7, 0, "L, R+2304, R+2304, K+2304", Ids.Art.Afterimage, 5, 0);
        }

        [Test]
        public void S03_TV_A011__Char1FbbFallsToKiln()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, B+2304, B+2304, K+2304", Ids.Art.Kiln, 3, 0);
        }

        [Test]
        public void S03_TV_A012__Char2FbbFallsToRecoil()
        {
            Fire(Ids.Char.C2, All, 5, 0, "F, B+2304, B+2304, K+2304", Ids.Art.Recoil, 4, 0);
        }

        [Test]
        public void S03_TV_A016__KilnFallsBackToRecoil()
        {
            Fire(Ids.Char.C1, All, 1, 0, "B, B+2304, K+2304", Ids.Art.Recoil, 0, 0, true);
        }

        [Test]
        public void S03_TV_A017__ShadowHandGap019()
        {
            Fire(Ids.Char.C5, All, 7, 0, "L, R+2918, K+2304", Ids.Art.ShadowHand, 5, 0);
        }

        [Test]
        public void S03_TV_A018__ShadowHandGap022TooLate()
        {
            Fire(Ids.Char.C5, All, 7, 0, "L, R+3379, K+2304", Ids.Art.Wings, 6, 0);
        }

        [Test]
        public void S03_TV_A019__BoltDashGap022TooLate()
        {
            Fire(Ids.Char.C2, All, 5, 0, "L, F+3379, K+2304", Ids.Art.Fan, 4, 0);
        }

        [Test]
        public void S03_TV_A020__ParentOnCreditWhenDebtOne()
        {
            Fire(Ids.Char.C5, All, 0, 1, "L, R+2304, K+2304", Ids.Art.Wings, 0, 2, true);
        }

        [Test]
        public void S03_TV_A021__OctastarGap019()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, B+2918, K+2304", Ids.Art.Octastar, 3, 0);
        }

        [Test]
        public void S03_TV_A022__OctastarGap022TooLate()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, B+3379, K+2304", Ids.Art.Recoil, 4, 0);
        }

        [Test]
        public void S03_TV_A032__BloomRepeats5()
        {
            var kit = Fire(Ids.Char.C1, All, 5, 0, "F, B+2611, B+2611, F+2611, K+2611", Ids.Art.Bloom, 0, 0);
            Assert.AreEqual(5, kit.Last.Repeats);
        }

        [Test]
        public void S03_TV_A047__BloomLockedFallsToVent()
        {
            Fire(Ids.Char.C1, new[] { Ids.Art.Vent, Ids.Art.Fan, Ids.Art.Octastar }, 5, 0, "F, B+2611, B+2611, F+2611, K+2611", Ids.Art.Vent, 3, 0);
        }

        [Test]
        public void S03_TV_A052__BloomAndVentLockedFallsToFan()
        {
            Fire(Ids.Char.C1, new[] { Ids.Art.Fan }, 5, 0, "F, B+2611, B+2611, F+2611, K+2611", Ids.Art.Fan, 4, 0);
        }

        [Test]
        public void S03_TV_A054__JustAt006()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, F+2304, K+922", Ids.Art.ThunderSpear, 4, 0);
        }

        [Test]
        public void S03_TV_A055__JustMissedAt009()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, F+2304, K+1382", Ids.Art.Spear, 3, 0);
        }

        [Test]
        public void S03_TV_A057__KilnGapExactly020()
        {
            Fire(Ids.Char.C1, All, 5, 0, "B, B+3072, K+2304", Ids.Art.Kiln, 3, 0);
        }

        [Test]
        public void S03_TV_A062__BoltDashFallsBackToWings()
        {
            Fire(Ids.Char.C2, All, 1, 0, "L, F+2304, K+2304", Ids.Art.Wings, 0, 0, true);
        }

        [Test]
        public void S03_TV_A063__CopyCircleFallsBackToFan()
        {
            Fire(Ids.Char.C3, All, 1, 0, "F, L+2304, K+2304", Ids.Art.Fan, 0, 0, true);
        }

        [Test]
        public void S03_TV_A068__IceScreenGap029()
        {
            Fire(Ids.Char.C4, All, 6, 0, "B, L+4454, K+2304", Ids.Art.IceScreen, 4, 0);
        }

        [Test]
        public void S03_TV_A069__IceScreenGap031ResetsToWings()
        {
            Fire(Ids.Char.C4, All, 6, 0, "B, L+4762, K+2304", Ids.Art.Wings, 5, 0);
        }

        [Test]
        public void S03_TV_A072__ShadowHandOnCreditOne()
        {
            Fire(Ids.Char.C5, All, 1, 0, "L, R+2304, K+2304", Ids.Art.ShadowHand, 0, 1);
        }

        [Test]
        public void S03_TV_A073__ShadowHandOnCreditTwo()
        {
            Fire(Ids.Char.C5, All, 0, 0, "L, R+2304, K+2304", Ids.Art.ShadowHand, 0, 2);
        }

        [Test]
        public void S03_TV_A075__SpearOnCreditToDebtTwo()
        {
            Fire(Ids.Char.C5, All, 1, 1, "F, F+2304, K+2304", Ids.Art.Spear, 0, 2);
        }

        [Test]
        public void S03_TV_A077__BurstOnCredit()
        {
            Fire(Ids.Char.C5, All, 0, 0, "K", Ids.Art.Burst, 0, 1);
        }

        [Test]
        public void S03_TV_A081__Char1HasNoLrNode()
        {
            Fire(Ids.Char.C1, All, 5, 0, "L, R+2304, K+2304", Ids.Art.Wings, 4, 0);
        }

        [Test]
        public void S03_TV_A082__AfterimageGap024()
        {
            Fire(Ids.Char.C3, All, 4, 0, "L, L+3686, K+2304", Ids.Art.Afterimage, 2, 0);
        }

        [Test]
        public void S03_TV_A083__AfterimageGap026TooLate()
        {
            Fire(Ids.Char.C3, All, 4, 0, "L, L+3994, K+2304", Ids.Art.Wings, 3, 0);
        }

        [Test]
        public void S03_TV_A088__ResetBoundaryExactly030()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, K+4608", Ids.Art.Fan, 4, 0);
        }

        [Test]
        public void S03_TV_A089__ResetAfter031()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, K+4762", Ids.Art.Burst, 4, 0);
        }

        [Test]
        public void S03_TV_A093__JustCostsOneCrystal()
        {
            Fire(Ids.Char.C1, All, 1, 0, "F, F+2304, K+768", Ids.Art.ThunderSpear, 0, 0);
        }

        [Test]
        public void S03_TV_A095__JustAtZeroGap()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, F+2304, K+0", Ids.Art.ThunderSpear, 4, 0);
        }

        [Test]
        public void S03_TV_A096__JustBoundaryExactly1075()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, F+2304, K+1075", Ids.Art.ThunderSpear, 4, 0);
        }

        [Test]
        public void S03_TV_A097__JustMissedAt1080()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, F+2304, K+1080", Ids.Art.Spear, 3, 0);
        }

        [Test]
        public void S03_TV_A098__JustLockedGivesSpear()
        {
            Fire(Ids.Char.C1, new[] { Ids.Art.Spear, Ids.Art.Fan }, 5, 0, "F, F+2304, K+768", Ids.Art.Spear, 3, 0);
        }

        [Test]
        public void S03_TV_A102__BloomAllGapsExactly2765()
        {
            var kit = Fire(Ids.Char.C1, All, 5, 0, "F, B+2765, B+2765, F+2765, K+2765", Ids.Art.Bloom, 0, 0);
            Assert.AreEqual(5, kit.Last.Repeats);
        }

        [Test]
        public void S03_TV_A103__BloomPlusForwardGivesSpear()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, B+2611, B+2611, F+2611, F+2611, K+2611", Ids.Art.Spear, 3, 0);
        }

        [Test]
        public void S03_TV_A106__NothingUnlockedFallsToBurst()
        {
            Fire(Ids.Char.C3, new string[0], 4, 0, "F, F+2304, K+2304", Ids.Art.Burst, 3, 0);
        }

        [Test]
        public void S03_TV_A107__SpearLockedFallsToFan()
        {
            Fire(Ids.Char.C3, new[] { Ids.Art.Fan }, 4, 0, "F, F+2304, K+2304", Ids.Art.Fan, 3, 0);
        }

        [Test]
        public void S03_TV_A110__IceScreenViaFailLink()
        {
            Fire(Ids.Char.C4, All, 6, 0, "F, B+2304, L+2304, K+2304", Ids.Art.IceScreen, 4, 0);
        }

        [Test]
        public void S03_TV_A112__BoltDashViaFailLink()
        {
            Fire(Ids.Char.C2, All, 5, 0, "L, L+2304, F+2304, K+2304", Ids.Art.BoltDash, 3, 0);
        }

        [Test]
        public void S03_TV_A114__KilnAfterFbbb()
        {
            Fire(Ids.Char.C1, All, 5, 0, "F, B+2304, B+2304, B+2304, K+2304", Ids.Art.Kiln, 3, 0);
        }

        [Test]
        public void S03_TV_A116__OnlySuffixGapsMatter()
        {
            Fire(Ids.Char.C4, All, 6, 0, "F, B+3840, L+2304, K+2304", Ids.Art.IceScreen, 4, 0);
        }

        [Test]
        public void S03_TV_A117__HistoryRingOverflow()
        {
            Fire(Ids.Char.C1, All, 5, 0, "L, R+2304, L+2304, R+2304, L+2304, R+2304, L+2304, R+2304, F+2304, F+2304, K+2304", Ids.Art.Spear, 3, 0);
        }

        [Test]
        public void S03_TV_A037__BloomSecondInputLate()
        {
            Fizzle(Ids.Char.C1, All, 5, 0, "F, B+2918, B+2611, F+2611, K+2611", Ids.Art.Bloom, ArtFizzleReason.FullBloomLate, 5, 0, 2, 153, 1);
        }

        [Test]
        public void S03_TV_A042__BloomKLate()
        {
            Fizzle(Ids.Char.C1, All, 5, 0, "F, B+2611, B+2611, F+2611, K+3840", Ids.Art.Bloom, ArtFizzleReason.FullBloomLate, 5, 0, 5, 1075, 7);
        }

        [Test]
        public void S03_TV_A100__BloomThirdInputLate()
        {
            Fizzle(Ids.Char.C1, All, 5, 0, "F, B+2611, B+2918, F+2611, K+2611", Ids.Art.Bloom, ArtFizzleReason.FullBloomLate, 5, 0, 3, 153, 1);
        }

        [Test]
        public void S03_TV_A101__BloomFourthInputLate()
        {
            Fizzle(Ids.Char.C1, All, 5, 0, "F, B+2611, B+2611, F+3072, K+2611", Ids.Art.Bloom, ArtFizzleReason.FullBloomLate, 5, 0, 4, 307, 2);
        }

        [Test]
        public void S03_TV_A053__BloomNeedsThreeCrystals()
        {
            Fizzle(Ids.Char.C1, All, 2, 0, "F, B+2611, B+2611, F+2611, K+2611", Ids.Art.Bloom, ArtFizzleReason.NoCrystal, 2, 0);
        }

        [Test]
        public void S03_TV_A058__KilnAndParentBothUnaffordable()
        {
            Fizzle(Ids.Char.C1, All, 0, 0, "B, B+2304, K+2304", Ids.Art.Kiln, ArtFizzleReason.NoCrystal, 0, 0);
        }

        [Test]
        public void S03_TV_A074__DebtTwoBlocksCredit()
        {
            Fizzle(Ids.Char.C5, All, 0, 2, "L, R+2304, K+2304", Ids.Art.ShadowHand, ArtFizzleReason.NoCrystal, 0, 2);
        }

        [Test]
        public void S03_TV_A076__CreditWouldExceedTwo()
        {
            Fizzle(Ids.Char.C5, All, 0, 1, "F, F+2304, K+2304", Ids.Art.Spear, ArtFizzleReason.NoCrystal, 0, 1);
        }

        [Test]
        public void S03_TV_A080__BloomNeverOnCredit()
        {
            Fizzle(Ids.Char.C5, All, 2, 0, "F, B+2611, B+2611, F+2611, K+2611", Ids.Art.Bloom, ArtFizzleReason.NoCrystal, 2, 0);
        }

        [Test]
        public void S03_TV_A091__BurstNeedsOneCrystal()
        {
            Fizzle(Ids.Char.C1, All, 0, 0, "K", Ids.Art.Burst, ArtFizzleReason.NoCrystal, 0, 0);
        }

        [Test]
        public void S03_TV_A092__CommonArtHasNoFallback()
        {
            Fizzle(Ids.Char.C1, All, 1, 0, "F, F+2304, K+2304", Ids.Art.Spear, ArtFizzleReason.NoCrystal, 1, 0);
        }

    }
}
