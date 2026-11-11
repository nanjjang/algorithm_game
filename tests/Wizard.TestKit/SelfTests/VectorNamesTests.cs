using System;
using Wizard.TestKit.Shared.Vectors;
using Xunit;

namespace Wizard.TestKit.SelfTests
{
    // 테스트 이름 규칙 S<명세 번호>_<벡터 id>__<설명> (CLAUDE.md 8절)
    public sealed class VectorNamesTests
    {
        [Fact]
        public void TestName_MapsHyphensToUnderscores()
        {
            Assert.Equal("S01_T_H04__LockAtExactly100", VectorNames.TestName("01", "T-H04", "LockAtExactly100"));
            Assert.Equal("S11_TV_11_017__Join", VectorNames.TestName("11", "TV-11-017", "Join"));
            Assert.Equal("S03_B09c__Fallback", VectorNames.TestName("03", "B09c", "Fallback"));
            Assert.Equal("S01:T-H04", VectorNames.Key("01", "T-H04"));
        }

        [Theory]
        [InlineData("S01_T_H04__LockAtExactly100", "01", "T-H04")]
        [InlineData("S01_T_H04", "01", "T-H04")]
        [InlineData("Wizard.Sim.Tests.Heat.HeatTests.S01_T_H04__Lock", "01", "T-H04")]
        [InlineData("S05_TV_R1_01__Dodge(seed: 3, wave: 7)", "05", "TV-R1-01")]
        [InlineData("S12_T_AUTH_01__Two_Words", "12", "T-AUTH-01")]
        [InlineData("S04_N71__Given", "04", "N71")]
        public void TryParse_AcceptsVectorTestNames(string name, string spec, string id)
        {
            Assert.True(VectorNames.TryParseTestName(name, out string s, out string i));
            Assert.Equal(spec, s);
            Assert.Equal(id, i);
        }

        [Theory]
        [InlineData("S1_T_H04__X")]          // 명세 번호는 두 자리
        [InlineData("S01-T-H04__X")]         // '-' 는 식별자에 못 쓴다
        [InlineData("S01__X")]               // id 없음
        [InlineData("S01_T_H04___X")]        // 설명 앞 밑줄 셋
        [InlineData("S01_T_H04__")]          // 빈 설명
        [InlineData("s01_T_H04__X")]
        [InlineData("Fix64_RoundTrip")]
        public void TryParse_RejectsOtherNames(string name)
        {
            Assert.False(VectorNames.TryParseTestName(name, out _, out _));
        }

        [Fact]
        public void TestName_RejectsBadParts()
        {
            Assert.Throws<ArgumentException>(() => VectorNames.TestName("1", "T-H04", "X"));
            Assert.Throws<ArgumentException>(() => VectorNames.TestName("01", "T_H04", "X"));
            Assert.Throws<ArgumentException>(() => VectorNames.TestName("01", "T-H04", "a__b"));
            Assert.Throws<ArgumentException>(() => VectorNames.TestName("01", "T-H04", "has space"));
            Assert.Throws<ArgumentException>(() => VectorNames.TestName("01", "T-H04", ""));
        }
    }
}
