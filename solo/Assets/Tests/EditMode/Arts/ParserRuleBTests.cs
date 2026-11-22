using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Arts;

namespace Wizard.Solo.Tests.Arts
{
    /// <summary>04 요약서 14.2절 B군(규칙) 가운데 판정기·결제·미리보기·대기 쪽. 해금은 UnlockTests, 효과는 ArtEffectTests.</summary>
    public class ParserRuleBTests
    {
        const long U005 = 768, U010 = 1536, U015 = 2304;

        static void AssertFired(ArtKit kit, string art, int crystals)
        {
            var d = kit.Last;
            Assert.AreEqual(ArtOutcome.Fire, d.Outcome);
            Assert.AreEqual(art, d.Art);
            Assert.AreEqual(crystals, kit.P.Crystals);
        }

        [Test]
        public void S03_TV_B01__SameTimeForwardThenK_IsJust()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Dir(CmdDir.Forward);
            // 같은 시각이면 방향이 K보다 먼저 처리된다(입력 정렬은 Player의 몫이고 판정기는 그 순서로 받는다).
            kit.Dir(CmdDir.Forward, U015);
            Assert.AreEqual(kit.Trie.Find(CmdDir.Forward, CmdDir.Forward).Id, kit.Parser.Cursor);
            kit.K(0);
            AssertFired(kit, Ids.Art.ThunderSpear, 4);
            Assert.IsTrue(kit.Last.IsJust);
            Assert.AreEqual(1, kit.LastEvent(SimEventType.ArtFired).B);
        }

        [Test]
        public void S03_TV_B01b__SameTimeForwardThenKFromRoot_IsFan()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Dir(CmdDir.Forward);
            kit.K(0);
            AssertFired(kit, Ids.Art.Fan, 4);
        }

        [Test]
        public void S03_TV_B02__HeldKeyIsNotAnInput()
        {
            // 자세 밖에서 누른 채이던 방향 키는 입력이 아니다(누르는 순간만 넘어온다) → 자세에 들어간 뒤 K = 마력 방출.
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.EnterStance();
            kit.K(U010);
            AssertFired(kit, Ids.Art.Burst, 4);
        }

        [Test]
        public void S03_TV_B03__StanceExitAndReenterResets()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, F+2304");
            kit.Now += U005; kit.ExitStance();
            kit.Now += U005; kit.EnterStance();
            kit.K(U005);
            AssertFired(kit, Ids.Art.Burst, 4);
        }

        [Test]
        public void S03_TV_B04__KOutOfStanceIsBurst()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, F+2304");
            kit.Now += U005; kit.ExitStance();
            kit.K(U005);
            AssertFired(kit, Ids.Art.Burst, 4);
        }

        [Test]
        public void S03_TV_B05__DirectionOutOfStanceIsNotRecorded()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.Dir(CmdDir.Forward);
            Assert.AreEqual(0, kit.CountEvents(SimEventType.CommandDirInput));
            Assert.AreEqual(-1, kit.Parser.LastDirTimeU);
            kit.Now += U005; kit.EnterStance();
            kit.K(U005);
            AssertFired(kit, Ids.Art.Burst, 4);
        }

        [Test]
        public void S03_TV_B06__ReverseResetsThenForwardIsFan()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Dir(CmdDir.Forward);
            kit.Now += U010;
            kit.P.Stance = StanceState.Reverse;
            kit.P.Facing = Dir8Util.Flip(kit.P.Facing);
            kit.Arts.OnCommandReset(kit.W, kit.P, CommandResetReason.Reverse);
            Assert.AreEqual(CommandTrie.Root, kit.Parser.Cursor);
            kit.Dir(CmdDir.Forward, U010);
            kit.K(U010);
            AssertFired(kit, Ids.Art.Fan, 4);
        }

        [Test]
        public void S03_TV_B07__PauseResets()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, F+2304");
            kit.Arts.OnCommandReset(kit.W, kit.P, CommandResetReason.Pause);
            kit.K(U005);
            AssertFired(kit, Ids.Art.Burst, 4);
        }

        [Test]
        public void S01_T_F19__PauseResetClearsCursorAndHistoryOnce()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, B+2304");
            Assert.AreNotEqual(CommandTrie.Root, kit.Parser.Cursor);
            kit.Arts.OnCommandReset(kit.W, kit.P, CommandResetReason.Pause);
            Assert.AreEqual(CommandTrie.Root, kit.Parser.Cursor);
            Assert.IsFalse(kit.Parser.HasDir);
            Assert.AreEqual(-1, kit.Parser.LastDirTimeU);
            Assert.IsTrue(kit.Parser.InStance, "pause keeps the stance state");
            var recent = new List<RecentInput>();
            kit.Arts.GetRecentInputs(kit.P, recent);
            Assert.AreEqual(0, recent.Count);
            Assert.AreEqual(5, kit.P.Crystals);
        }

        [Test]
        public void S03_TV_B08__PreviewAtResetBoundary()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Dir(CmdDir.Forward);
            long t0 = kit.Now;
            var a = kit.Arts.Preview(kit.W, kit.P, t0 + 4608);
            Assert.AreEqual(Ids.Art.Fan, a.ArtId);
            Assert.AreEqual(1, a.Cost);
            Assert.IsFalse(a.Fizzle);
            var b = kit.Arts.Preview(kit.W, kit.P, t0 + 4609);
            Assert.AreEqual(Ids.Art.Burst, b.ArtId);
            Assert.AreEqual(1, b.Cost);
            // 지갑·커서는 그대로다.
            Assert.AreEqual(5, kit.P.Crystals);
            Assert.AreEqual(0, kit.P.Debt);
            Assert.AreEqual(kit.Trie.Find(CmdDir.Forward).Id, kit.Parser.Cursor);
            Assert.IsFalse(kit.HasDecision);
        }

        [Test]
        public void S03_TV_B09__CooldownIgnoreThenBufferThenFireAtCeilTick()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            long t0 = ArtKit.T0 + 100;
            kit.W.Tick = (int)(t0 / SimConst.UnitsPerTick);
            kit.Now = t0;
            kit.K();
            AssertFired(kit, Ids.Art.Burst, 4);
            Assert.AreEqual(t0 + 6144, kit.Parser.CooldownEndU);

            kit.Now = t0 + 3000; kit.K();
            Assert.AreEqual(4, kit.P.Crystals, "second K is ignored");
            Assert.IsFalse(kit.Parser.HasBuffered);

            kit.Now = t0 + 4700; kit.K();
            Assert.IsTrue(kit.Parser.HasBuffered);
            Assert.AreEqual(3, kit.P.Crystals, "the buffered K pays at press time");
            Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtBuffered));
            Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtFired));

            // 발동 틱 = ceil((t0 + 6144) / 256).
            int fireTick = (int)((t0 + 6144 + 255) / 256);
            Assert.AreEqual(65, fireTick);
            while (kit.W.Tick < fireTick)
            {
                kit.Stage5();
                Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtFired), "not before tick " + fireTick);
            }
            kit.Stage5();
            Assert.AreEqual(2, kit.CountEvents(SimEventType.ArtFired));
            Assert.AreEqual(fireTick, kit.LastEvent(SimEventType.ArtFired).Tick);
            Assert.AreEqual(t0 + 4700, kit.Last.JudgeTimeU, "judged with the press time");
            Assert.IsFalse(kit.Parser.HasBuffered);
            // D-13: 새 대기는 실제로 나간 틱부터 0.4초.
            Assert.AreEqual((long)fireTick * 256 + 6144, kit.Parser.CooldownEndU);
            Assert.AreEqual(3, kit.P.Crystals);
        }

        [Test]
        public void S03_TV_B09b__BufferWindowStartIsInclusive()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.K();
            kit.K(4608);
            Assert.IsTrue(kit.Parser.HasBuffered);
            Assert.AreEqual(3, kit.P.Crystals);
        }

        [Test]
        public void S03_7__OneUnitBeforeBufferWindowIsIgnored()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.K();
            kit.K(4607);
            Assert.IsFalse(kit.Parser.HasBuffered);
            Assert.AreEqual(4, kit.P.Crystals);
        }

        [Test]
        public void S03_TV_B09c__SecondKWhileBufferedIsIgnored()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            long t0 = kit.Now;
            kit.K();
            kit.Now = t0 + 4700; kit.K();
            kit.Now = t0 + 5000; kit.K();
            Assert.AreEqual(3, kit.P.Crystals);
            Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtBuffered));
        }

        [Test]
        public void S03_7__IgnoredKKeepsCursorAndHistory()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.K();
            kit.Dir(CmdDir.Forward, 1000);
            kit.K(1000);
            Assert.AreEqual(kit.Trie.Find(CmdDir.Forward).Id, kit.Parser.Cursor);
            Assert.AreEqual(4, kit.P.Crystals);
        }

        [Test]
        public void S03_TV_B10__FizzleStartsNoCooldown()
        {
            var kit = new ArtKit(Ids.Char.C1, 0, inStance: false);
            kit.K();
            Assert.AreEqual(ArtOutcome.Fizzle, kit.Last.Outcome);
            Assert.AreEqual(ArtFizzleReason.NoCrystal, kit.Last.Reason);
            kit.P.Crystals = 1;
            kit.K(100);
            AssertFired(kit, Ids.Art.Burst, 0);
        }

        [Test]
        public void S03_TV_B12__UniqueFallbackNeedsUnlockedParent()
        {
            var kit = new ArtKit(Ids.Char.C3, 1, 0, new string[0]);
            kit.Seq("F, L+2304, K+2304");
            Assert.AreEqual(ArtOutcome.Fizzle, kit.Last.Outcome);
            Assert.AreEqual(ArtFizzleReason.NoCrystal, kit.Last.Reason);
            Assert.AreEqual(Ids.Art.CopyCircle, kit.Last.SelectedArt);
            Assert.AreEqual(1, kit.P.Crystals);
        }

        [Test]
        public void S03_TV_B13__PreviewShowsCreditForShadowHand()
        {
            var kit = new ArtKit(Ids.Char.C5, 1);
            kit.Seq("L, R+2304");
            var pv = kit.Arts.Preview(kit.W, kit.P, kit.Now);
            Assert.AreEqual(Ids.Art.ShadowHand, pv.ArtId);
            Assert.AreEqual(2, pv.Cost);
            Assert.AreEqual(1, pv.DebtUsed);
            Assert.IsFalse(pv.Fizzle);
            Assert.AreEqual(1, kit.P.Crystals);
            Assert.AreEqual(0, kit.P.Debt);
        }

        [Test]
        public void S03_TV_B14__PreviewShowsParentWhenShort()
        {
            var kit = new ArtKit(Ids.Char.C1, 1);
            kit.Seq("B, B+2304");
            var pv = kit.Arts.Preview(kit.W, kit.P, kit.Now);
            Assert.AreEqual(Ids.Art.Recoil, pv.ArtId);
            Assert.AreEqual(1, pv.Cost);
            Assert.AreEqual(0, pv.DebtUsed);
            Assert.IsFalse(pv.Fizzle);
        }

        [Test]
        public void S03_TV_B15__PreviewTwoLinesWhenJustUnlocked()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, F+2304");
            // 섬 창 안에서도, 넘긴 뒤에도 늘 두 줄(U-29).
            foreach (long dt in new long[] { 0, 500, 1075, 1076, 3000 })
            {
                var pv = kit.Arts.Preview(kit.W, kit.P, kit.Now + dt);
                Assert.IsTrue(pv.HasJustLine, "dt " + dt);
                Assert.AreEqual(Ids.Art.ThunderSpear, pv.JustArtId);
                Assert.AreEqual(1, pv.JustCost);
                Assert.AreEqual(Ids.Art.Spear, pv.ArtId);
                Assert.AreEqual(2, pv.Cost);
            }
        }

        [Test]
        public void S03_TV_B15b__PreviewOneLineWhenJustLocked()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, 0, new[] { Ids.Art.Fan, Ids.Art.Spear });
            kit.Seq("F, F+2304");
            var pv = kit.Arts.Preview(kit.W, kit.P, kit.Now);
            Assert.IsFalse(pv.HasJustLine);
            Assert.AreEqual(Ids.Art.Spear, pv.ArtId);
            Assert.AreEqual(2, pv.Cost);
        }

        // 6.1절: 초 = ceil(LateBy × 100 / 15360) / 100 (소수 둘째 자리 올림).
        [TestCase(153, 1)]
        [TestCase(307, 2)]
        [TestCase(1075, 7)]
        [TestCase(1, 1)]
        public void S03_TV_B16__LateTextRoundsUp(long lateByU, int hundredths)
        {
            Assert.AreEqual(hundredths, LateText.Hundredths(lateByU));
            Assert.AreEqual(hundredths / 100f, LateText.Seconds(lateByU), 1e-6f);
        }

        [Test]
        public void S03_TV_B37__PreviewShowsLateBloomFizzle()
        {
            // 10 요약서 Q-12: "이미 늦었다"는 커서 FBBF에서 읽는다.
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, B+2918, B+2611, F+2611");
            var pv = kit.Arts.Preview(kit.W, kit.P, kit.Now);
            Assert.IsTrue(pv.Fizzle);
            Assert.AreEqual(ArtFizzleReason.FullBloomLate, pv.Reason);
            Assert.AreEqual(2, pv.LateIndex);
            Assert.AreEqual(0.01f, pv.LateSeconds, 1e-6f);
            Assert.AreEqual(Ids.Art.Bloom, pv.ArtId);
            Assert.AreEqual(5, kit.P.Crystals);
        }

        [Test]
        public void S10_Q_12__PreviewAtFbbShowsFailLinkArt()
        {
            var c1 = new ArtKit(Ids.Char.C1, 5);
            c1.Seq("F, B+2918, B+2611");
            Assert.AreEqual(Ids.Art.Kiln, c1.Arts.Preview(c1.W, c1.P, c1.Now).ArtId);
            var c2 = new ArtKit(Ids.Char.C2, 5);
            c2.Seq("F, B+2918, B+2611");
            Assert.AreEqual(Ids.Art.Recoil, c2.Arts.Preview(c2.W, c2.P, c2.Now).ArtId);
        }

        [Test]
        public void S03_TV_B38__MissedJustReportsLateK()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, F+2304, K+1382");
            AssertFired(kit, Ids.Art.Spear, 3);
            Assert.IsFalse(kit.Last.IsJust);
            Assert.AreEqual(307, kit.Last.JustLateByU);
            Assert.AreEqual(2, LateText.Hundredths(kit.Last.JustLateByU));
        }

        [Test]
        public void S03_TV_B30__ShadowHandCrystalsAfterIsThree()
        {
            var kit = new ArtKit(Ids.Char.C5, 5);
            kit.Seq("L, R+2304, K+2304");
            AssertFired(kit, Ids.Art.ShadowHand, 3);
            Assert.AreEqual(3, kit.Last.CrystalsAfter);
        }

        [Test]
        public void S03_TV_B31__BloomWithSevenCrystals()
        {
            var kit = new ArtKit(Ids.Char.C5, 7);
            kit.Seq(ArtKit.BloomInput);
            AssertFired(kit, Ids.Art.Bloom, 0);
            Assert.AreEqual(7, kit.Last.Repeats);
            Assert.AreEqual(0, kit.Last.CrystalsAfter);
            Assert.AreEqual(0, kit.P.Debt);
            Assert.AreEqual(7f, kit.LastEvent(SimEventType.ArtFired).F);
        }

        [Test]
        public void S03_TV_B32__BloomWithFourCrystals()
        {
            var kit = new ArtKit(Ids.Char.C3, 4);
            kit.Seq(ArtKit.BloomInput);
            AssertFired(kit, Ids.Art.Bloom, 0);
            Assert.AreEqual(4, kit.Last.Repeats);
        }

        [Test]
        public void S03_U_35__WaveStartClearsCooldownButKeepsBufferedK()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.W.Tick = (int)(ArtKit.T0 / SimConst.UnitsPerTick);
            kit.K();
            kit.K(4700);
            Assert.IsTrue(kit.Parser.HasBuffered);
            kit.Arts.OnCommandReset(kit.W, kit.P, CommandResetReason.WaveStart);
            Assert.AreEqual(0, kit.Parser.CooldownEndU);
            Assert.IsTrue(kit.Parser.HasBuffered, "D-13: the buffered K survives a wave start");
            // 대기가 풀렸으므로 그 틱에 나간다(10 요약서 Q-11).
            kit.Stage5();
            Assert.AreEqual(2, kit.CountEvents(SimEventType.ArtFired));
        }

        [Test]
        public void S03_R_10__DirectionAfterTimeoutStartsFromRoot()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, F+4609");
            Assert.AreEqual(kit.Trie.Find(CmdDir.Forward).Id, kit.Parser.Cursor);
            kit.K(100);
            AssertFired(kit, Ids.Art.Fan, 4);
        }

        [Test]
        public void S03_R_10__DirectionAtExactly4608KeepsChain()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, F+4608");
            Assert.AreEqual(kit.Trie.Find(CmdDir.Forward, CmdDir.Forward).Id, kit.Parser.Cursor);
        }
    }
}
