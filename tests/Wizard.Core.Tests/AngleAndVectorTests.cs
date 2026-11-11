using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests
{
    // CONTRACTS 1.1, 0.3: 0 = +x, 화면에서 반시계가 +, y 는 아래가 +, 방향 벡터 = (cos a, −sin a)
    public sealed class AngleAndVectorTests
    {
        [Fact]
        public void FromCenti_Folds()
        {
            Assert.Equal(35999, Angle.FromCenti(-1).Centi);
            Assert.Equal(0, Angle.FromCenti(36000).Centi);
            Assert.Equal(100, Angle.FromCenti(36100).Centi);
            Assert.InRange(Angle.FromCenti(int.MinValue).Centi, 0, 35999);
            Assert.Equal(27000, Angle.FromDegrees(-90).Centi);
            Assert.Equal(9000, Angle.FromDegrees(450).Centi);
        }

        [Fact]
        public void Dir8_MatchesAngles()
        {
            Assert.Equal(0, Angle.FromDir8(Dir8.E).Centi);
            Assert.Equal(4500, Angle.FromDir8(Dir8.NE).Centi);
            Assert.Equal(9000, Angle.FromDir8(Dir8.N).Centi);
            Assert.Equal(31500, Angle.FromDir8(Dir8.SE).Centi);
        }

        [Fact]
        public void Delta_IsSignedShortest()
        {
            Assert.Equal(200, Angle.Delta(Angle.FromCenti(35900), Angle.FromCenti(100)));
            Assert.Equal(-200, Angle.Delta(Angle.FromCenti(100), Angle.FromCenti(35900)));
            Assert.Equal(-18000, Angle.Delta(Angle.FromCenti(0), Angle.FromCenti(18000)));   // [−18000, 18000)
            Assert.Equal(0, Angle.Delta(Angle.FromCenti(5), Angle.FromCenti(5)));
        }

        [Fact]
        public void RotateToward_StepsAndSnaps()
        {
            Assert.Equal(500, Angle.RotateToward(Angle.FromCenti(0), Angle.FromCenti(9000), 500).Centi);
            Assert.Equal(35500, Angle.RotateToward(Angle.FromCenti(0), Angle.FromCenti(27000), 500).Centi);
            Assert.Equal(9000, Angle.RotateToward(Angle.FromCenti(8800), Angle.FromCenti(9000), 500).Centi);
            Assert.Equal(100, Angle.RotateToward(Angle.FromCenti(100), Angle.FromCenti(200), -5).Centi);
        }

        [Fact]
        public void FromAngle_UsesScreenCoordinates()
        {
            FixVec2 up = FixVec2.FromAngle(Angle.FromDir8(Dir8.N));
            Assert.Equal(Fix64.Zero, up.X);
            Assert.Equal(-Fix64.One, up.Y);                      // 화면 위 = (0, −1)
            FixVec2 right = FixVec2.FromAngle(Angle.FromCenti(0));
            Assert.Equal(new FixVec2(Fix64.One, Fix64.Zero), right);
            Assert.Equal(9000, Angle.OfVector(up).Centi);
            Assert.Equal(27000, Angle.OfVector(new FixVec2(Fix64.Zero, Fix64.One)).Centi);
        }

        [Fact]
        public void OfVector_InvertsFromAngle()
        {
            for (int c = 0; c < Angle.Full; c += 13)
            {
                Assert.Equal(c, Angle.OfVector(FixVec2.FromAngle(Angle.FromCenti(c))).Centi);
            }
        }

        [Fact]
        public void LengthNormalizeDotCross()
        {
            var v = new FixVec2(Fix64.FromInt(3), Fix64.FromInt(4));
            Assert.Equal(Fix64.FromInt(25), v.LengthSq());
            Assert.Equal(Fix64.FromInt(5), v.Length());
            FixVec2 n = v.Normalized();
            Assert.Equal(2576980377L, n.X.Raw);                  // 0.6, 0 쪽 버림
            Assert.Equal(3435973836L, n.Y.Raw);                  // 0.8, 0 쪽 버림
            Assert.Equal(FixVec2.Zero, FixVec2.Zero.Normalized());
            var w = new FixVec2(Fix64.FromInt(-4), Fix64.FromInt(3));
            Assert.Equal(Fix64.Zero, FixVec2.Dot(v, w));
            Assert.Equal(Fix64.FromInt(25), FixVec2.Cross(v, w));
            Assert.Equal(Fix64.FromInt(50), FixVec2.DistanceSq(v, w));
        }
    }
}
