using Wizard.Core;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // 수락 조건 "부채꼴 각도는 BlockParams.FanHalfAngleCenti 에서 만든다(−반각 ~ +반각을 갈래 수로 고르게 나눈다. T-53)".
    // 기대값은 spec 02 9.3 표: 2갈래 −1000 / +1000, 3갈래 −1500 / 0 / +1500, 분열 없음 0.
    public sealed class FanAnglesTests
    {
        [Fact]
        public void ScopeFanOffsets_ComeFromSplitBlockHalfAngle()
        {
            SpellScope two = Dalgu1(Bolt, Split2, __, __, __).Scopes[0];
            Assert.Equal(Db.Block(Split2).Params.FanHalfAngleCenti, two.FanHalfAngleCenti);
            Assert.Equal(new[] { -1000, 1000 }, new[] { two.FanOffsetCenti(0), two.FanOffsetCenti(1) });

            SpellScope three = Dalgu1(Bolt, Split3, __, __, __).Scopes[0];
            Assert.Equal(Db.Block(Split3).Params.FanHalfAngleCenti, three.FanHalfAngleCenti);
            Assert.Equal(new[] { -1500, 0, 1500 }, new[] { three.FanOffsetCenti(0), three.FanOffsetCenti(1), three.FanOffsetCenti(2) });

            SpellScope one = Dalgu1(Bolt, __, __, __, __).Scopes[0];
            Assert.Equal(0, one.FanOffsetCenti(0));

            // 두 번째 분열은 무시되므로 반각도 첫 분열의 것이다
            SpellScope dup = Dalgu1(Bolt, Split2, Split3, __, __).Scopes[0];
            Assert.Equal(2, dup.SplitCount);
            Assert.Equal(1000, dup.FanOffsetCenti(1));
        }

        [Fact]
        public void OffsetFormula_EvenSpread_AndRangeChecked()
        {
            Assert.Equal(new[] { -1200, -400, 400, 1200 },
                         new[] { FanAngles.OffsetCenti(1200, 4, 0), FanAngles.OffsetCenti(1200, 4, 1), FanAngles.OffsetCenti(1200, 4, 2), FanAngles.OffsetCenti(1200, 4, 3) });
            Assert.Throws<SimInvariantException>(() => FanAngles.OffsetCenti(1000, 2, 2));
            Assert.Throws<SimInvariantException>(() => FanAngles.OffsetCenti(1000, 0, 0));
        }
    }
}
