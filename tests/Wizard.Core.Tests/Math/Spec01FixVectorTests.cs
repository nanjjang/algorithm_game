using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests.Math
{
    // spec 01 14.1 T-C04 ~ T-C08. 기대값은 명세 표 그대로다.
    public sealed class Spec01FixVectorTests
    {
        [Fact]
        public void S01_T_C04__FromInt3Raw()
        {
            Assert.Equal(12884901888L, Fix64.FromInt(3).Raw);
        }

        [Fact]
        public void S01_T_C05__OnePointFiveTimesMinusTwo()
        {
            Fix64 a = Fix64.FromRatio(3, 2);    // 1.5
            Fix64 b = Fix64.FromInt(-2);        // −2.0
            Assert.Equal(6442450944L, a.Raw);
            Assert.Equal(-12884901888L, (a * b).Raw);
            Assert.Equal(Fix64.FromInt(-3), a * b);
        }

        [Fact]
        public void S01_T_C06__EpsilonSquaredFloorsToZero()
        {
            Assert.Equal(0L, (Fix64.FromRaw(1) * Fix64.FromRaw(1)).Raw);
        }

        [Fact]
        public void S01_T_C07__NegativeEpsilonProductFloorsToMinusOne()
        {
            Assert.Equal(-1L, (Fix64.FromRaw(-1) * Fix64.FromRaw(1)).Raw);
        }

        [Fact]
        public void S01_T_C08__InvSqrt2Raw()
        {
            Assert.Equal(3037000500L, FixMath.InvSqrt2Raw);
        }
    }
}
