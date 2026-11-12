using Wizard.Content;
using Wizard.Sim.Arts;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // 수락 조건 2: 섬(0.07초 = 1,075u), 만개(0.18초 = 2,765u), 0.2초(3,072u) 구간이 입력 시각 단위(틱 × 256 + 오프셋)로
    // 경계값까지 정확하다. 비교는 "경과 ≤ 한도면 통과"(spec 03 1.2, 17절 1번). 같은 쪽 0.25초·기본 0.3초 경계도 같이 본다.
    public sealed class WindowBoundaryTests
    {
        private static ArtDecision Run(CharacterId c, params In[] steps)
        {
            CommandParser p = ParserRig.InStance(c);
            TestWallet w = SpecArts.Wallet(c, SpecArts.Cap(c), 0);
            return ParserRig.Feed(p, w, Unlocks.All.For(c), steps);
        }

        private static void AssertArt(ArtId art, in ArtDecision v)
        {
            Assert.Equal(ArtOutcome.Fire, v.Outcome);
            Assert.Equal(art, v.Art);
        }

        // 섬: 마지막 방향(틱 400, 오프셋 100)에서 1,075u 뒤 = 틱 404 오프셋 151 은 섬, 오프셋 152 는 일반 기술
        [Fact]
        public void Just_AtTickOffsetBoundary()
        {
            long lastDir = SimTime.InputTime(400, 100);
            Assert.Equal(SimTime.InputTime(404, 151), lastDir + SpecArts.JustWindow);
            foreach ((byte offset, ArtId want) in new[] { ((byte)150, ArtId.ThunderSpear), ((byte)151, ArtId.ThunderSpear), ((byte)152, ArtId.Spear) })
            {
                CommandParser p = ParserRig.InStance(CharacterId.Char1);
                TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
                p.OnDirection(CmdDir.Forward, lastDir - 2304);
                p.OnDirection(CmdDir.Forward, lastDir);
                Assert.True(p.OnSkillPress(SimTime.InputTime(404, offset), w, Unlocks.All.For(CharacterId.Char1), out ArtDecision v));
                AssertArt(want, v);
                Assert.Equal(want == ArtId.ThunderSpear ? 0L : 1L, v.JustLateByUnits);
            }
        }

        [Fact]
        public void Just_ZeroAndExactWindowPass_OneMoreFails()
        {
            AssertArt(ArtId.ThunderSpear, Run(CharacterId.Char1, In.F(0), In.F(2304), In.K(0)));
            AssertArt(ArtId.ThunderSpear, Run(CharacterId.Char1, In.F(0), In.F(2304), In.K(1075)));
            AssertArt(ArtId.Spear, Run(CharacterId.Char1, In.F(0), In.F(2304), In.K(1076)));
        }

        // 만개: 간격 넷(앞→뒤, 뒤→뒤, 뒤→앞, 앞→K)이 각각 2,765u 면 통과, 그중 하나라도 2,766u 면 그 입력이 1u 늦은 불발
        [Fact]
        public void FullBloom_EachGapAtBoundary()
        {
            AssertArt(ArtId.Bloom, Run(CharacterId.Char1, In.F(0), In.B(2765), In.B(2765), In.F(2765), In.K(2765)));
            for (int late = 0; late < 4; late++)
            {
                long[] g = { 2765, 2765, 2765, 2765 };
                g[late] = 2766;
                ArtDecision v = Run(CharacterId.Char1, In.F(0), In.B(g[0]), In.B(g[1]), In.F(g[2]), In.K(g[3]));
                Assert.Equal(ArtOutcome.Fizzle, v.Outcome);
                Assert.Equal(FizzleReason.FullBloomLate, v.Reason);
                Assert.Equal(late + 2, v.LateIndex);
                Assert.Equal(1, v.LateByUnits);
                Assert.Equal(1, LateBy.HundredthsCeil(v.LateByUnits));
            }
        }

        // 0.2초(3,072u) 간선: 앞→뒤(팔방), 좌→우(그림자 손), char.1 의 뒤→뒤(불가마), 옆→앞(벼락 질주), 앞→옆(베껴 쓰기).
        // 3,072u 는 통과, 3,073u 는 실패 링크로 내려간다
        [Theory]
        [InlineData(CharacterId.Char1, "FB", ArtId.Octastar, ArtId.Recoil)]
        [InlineData(CharacterId.Char5, "LR", ArtId.ShadowHand, ArtId.Wings)]
        [InlineData(CharacterId.Char1, "BB", ArtId.Kiln, ArtId.Recoil)]
        [InlineData(CharacterId.Char2, "LF", ArtId.BoltDash, ArtId.Fan)]
        [InlineData(CharacterId.Char3, "FL", ArtId.CopyCircle, ArtId.Wings)]
        public void GapShort_AtBoundary(CharacterId c, string path, ArtId pass, ArtId fail)
        {
            CmdDir[] d = ParserRig.Path(path);
            AssertArt(pass, Run(c, Dir(d[0], 0), Dir(d[1], 3072), In.K(2304)));
            AssertArt(fail, Run(c, Dir(d[0], 0), Dir(d[1], 3073), In.K(2304)));
        }

        // 같은 쪽 두 번(0.25초 = 3,840u)
        [Fact]
        public void GapSameSide_AtBoundary()
        {
            AssertArt(ArtId.Afterimage, Run(CharacterId.Char3, In.R(0), In.R(3840), In.K(2304)));
            AssertArt(ArtId.Wings, Run(CharacterId.Char3, In.R(0), In.R(3841), In.K(2304)));
        }

        // 기본 0.3초(4,608u): 간선(뒤→앞)과 마지막 방향 → K 의 초기화(R-10)
        [Fact]
        public void GapDefault_AtBoundary()
        {
            AssertArt(ArtId.Vent, Run(CharacterId.Char1, In.B(0), In.F(4608), In.K(2304)));
            AssertArt(ArtId.Fan, Run(CharacterId.Char1, In.B(0), In.F(4609), In.K(2304)));   // 초기화 뒤 F 하나
            AssertArt(ArtId.Fan, Run(CharacterId.Char1, In.F(0), In.K(4608)));
            AssertArt(ArtId.Burst, Run(CharacterId.Char1, In.F(0), In.K(4609)));
        }

        // 방향 입력이 0.3초를 넘겨 오면 그 입력 전에 커서가 처음으로 돌아간다(R-10)
        [Fact]
        public void DirectionAfterTimeout_StartsAFreshCommand()
        {
            CommandParser p = ParserRig.InStance(CharacterId.Char1);
            p.OnDirection(CmdDir.Forward, ParserRig.T0);
            p.OnDirection(CmdDir.Forward, ParserRig.T0 + 4609);
            Assert.Equal("F", ParserRig.Letters(p.Trie.Path(p.Cursor)));
            Assert.Equal(1, p.DirCount);
            p.OnDirection(CmdDir.Forward, ParserRig.T0 + 4609 + 4608);
            Assert.Equal("FF", ParserRig.Letters(p.Trie.Path(p.Cursor)));
            Assert.True(p.LastDirChained);
        }

        private static In Dir(CmdDir d, long gap)
        {
            switch (d)
            {
                case CmdDir.Forward: return In.F(gap);
                case CmdDir.Back: return In.B(gap);
                case CmdDir.Left: return In.L(gap);
                default: return In.R(gap);
            }
        }
    }
}
