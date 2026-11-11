using System;
using Wizard.Core;
using Xunit;

namespace Wizard.Core.Tests.Angles
{
    // M1-CORE-02 수락 조건. CONTRACTS 1.1·0.3, DECISIONS-TECH T-07.
    // (1) 0, 90.00, 180.00, 270.00도와 Dir8 여덟 방향의 sin/cos 가 정확값이고 방향 벡터는 (cos a, −sin a)
    // (2) 36,000개 각도 전부에서 Angle → 벡터 → Atan2 왕복 오차 1센티도 이하
    // (3) 보간이 없다: 모든 각도의 sin/cos 가 9,001칸 표의 한 칸(또는 그 부호 반전)과 정확히 같다
    public sealed class AngleTrigAcceptanceTests
    {
        private const long One = 1L << 32;
        private const long H = FixMath.InvSqrt2Raw;   // round(2^32 / √2) = 3,037,000,500

        [Fact]
        public void Reference_TableIsTrustworthy()
        {
            // 기준 계산의 오차는 2^-200 단위로 수천 이하다. 동률 근처(여유가 작은 칸)가 없어야 반올림 판정이 확실하다.
            Assert.True(TrigReference.MinHalfMargin > (System.Numerics.BigInteger.One << 100),
                "reference rounding margin too small: " + TrigReference.MinHalfMargin);
            Assert.Equal(0L, TrigReference.SinRaw[0]);
            Assert.Equal(One >> 1, TrigReference.SinRaw[3000]);
            Assert.Equal(H, TrigReference.SinRaw[4500]);
            Assert.Equal(One, TrigReference.SinRaw[9000]);
            Assert.Equal(749613L, TrigReference.SinRaw[1]);
        }

        // (1) 축 네 방향
        [Theory]
        [InlineData(0, 0L, One)]
        [InlineData(9000, One, 0L)]
        [InlineData(18000, 0L, -One)]
        [InlineData(27000, -One, 0L)]
        public void CardinalAngles_SinCosAreExact(int centi, long sin, long cos)
        {
            Angle a = Angle.FromCenti(centi);
            Assert.Equal(sin, FixMath.Sin(a).Raw);
            Assert.Equal(cos, FixMath.Cos(a).Raw);
            FixVec2 v = FixVec2.FromAngle(a);
            Assert.Equal(cos, v.X.Raw);
            Assert.Equal(-sin, v.Y.Raw);
            Assert.Equal(a, Angle.OfVector(v));
        }

        // (1) Dir8 여덟 방향. 화면 좌표(y 아래가 +)의 단위 벡터. 대각 성분은 1/√2 의 Raw 하나(CONTRACTS 0.3)
        [Theory]
        [InlineData(Dir8.E, 0, One, 0L)]
        [InlineData(Dir8.NE, 4500, H, -H)]
        [InlineData(Dir8.N, 9000, 0L, -One)]
        [InlineData(Dir8.NW, 13500, -H, -H)]
        [InlineData(Dir8.W, 18000, -One, 0L)]
        [InlineData(Dir8.SW, 22500, -H, H)]
        [InlineData(Dir8.S, 27000, 0L, One)]
        [InlineData(Dir8.SE, 31500, H, H)]
        public void Dir8_SinCosAndVectorAreExact(Dir8 dir, int centi, long vx, long vy)
        {
            Angle a = Angle.FromDir8(dir);
            Assert.Equal(centi, a.Centi);
            Assert.Equal(vx, FixMath.Cos(a).Raw);
            Assert.Equal(-vy, FixMath.Sin(a).Raw);
            Assert.Equal(TrigReference.Cos(centi), FixMath.Cos(a).Raw);
            Assert.Equal(TrigReference.Sin(centi), FixMath.Sin(a).Raw);

            FixVec2 v = FixVec2.FromAngle(a);
            Assert.Equal(vx, v.X.Raw);
            Assert.Equal(vy, v.Y.Raw);
            Assert.Equal(a, Angle.OfVector(v));

            // 길이가 1이 아닌 격자 벡터(예: (1, −1))도 같은 방향으로 돌아온다
            var grid = new FixVec2(Fix64.FromInt(Sign(vx) * 3), Fix64.FromInt(Sign(vy) * 3));
            Assert.Equal(a, Angle.OfVector(grid));
        }

        // (1) 모든 각도에서 방향 벡터 = (cos a, −sin a), 그리고 그 값이 기준 표와 같다
        [Fact]
        public void FromAngle_IsCosMinusSin_ForEveryCentidegree()
        {
            for (int c = 0; c < Angle.Full; c++)
            {
                Angle a = Angle.FromCenti(c);
                FixVec2 v = FixVec2.FromAngle(a);
                Assert.Equal(FixMath.Cos(a).Raw, v.X.Raw);
                Assert.Equal(-FixMath.Sin(a).Raw, v.Y.Raw);
                Assert.Equal(TrigReference.Cos(c), v.X.Raw);
                Assert.Equal(-TrigReference.Sin(c), v.Y.Raw);
            }
        }

        // (2) 36,000개 전부 왕복 오차 ≤ 1센티도. 단위 벡터와, 성분을 정수배한 벡터(같은 방향, 다른 크기) 둘 다
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(1000)]
        [InlineData(-1)]
        public void RoundTrip_AngleVectorAtan2_WithinOneCentidegree(int scale)
        {
            int worst = 0;
            for (int c = 0; c < Angle.Full; c++)
            {
                Angle a = Angle.FromCenti(c);
                FixVec2 v = FixVec2.FromAngle(a);
                var scaled = new FixVec2(v.X * scale, v.Y * scale);
                Angle expected = scale < 0 ? a + 18000 : a;
                int err = Abs(Angle.Delta(expected, Angle.OfVector(scaled)));
                if (err > worst)
                {
                    worst = err;
                }
                Assert.True(err <= 1, "round trip error " + err + " at centi " + c + " scale " + scale);
            }
            Assert.InRange(worst, 0, 1);
        }

        // (2) 수학 좌표의 Atan2(sin, cos) 도 같은 조건
        [Fact]
        public void RoundTrip_Atan2OfSinCos_WithinOneCentidegree()
        {
            for (int c = 0; c < Angle.Full; c++)
            {
                Angle a = Angle.FromCenti(c);
                Angle back = FixMath.Atan2(FixMath.Sin(a), FixMath.Cos(a));
                Assert.InRange(Abs(Angle.Delta(a, back)), 0, 1);
            }
        }

        // Atan2 가 계약의 정의(전수 탐색 기준 구현)와 비트 단위로 같다. 팔분면 경계, 축, 극단 크기, 의사 난수 벡터
        [Fact]
        public void Atan2_MatchesContractDefinition()
        {
            long[] specials =
            {
                0L, 1L, -1L, 2L, -2L, One, -One, H, -H, One - 1, One + 1,
                long.MaxValue, long.MinValue, long.MinValue + 1, long.MaxValue - 1, 1L << 62, -(1L << 62),
            };
            foreach (long y in specials)
            {
                foreach (long x in specials)
                {
                    Assert.Equal(TrigReference.Atan2(y, x), FixMath.Atan2(Fix64.FromRaw(y), Fix64.FromRaw(x)).Centi);
                }
            }

            ulong state = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < 3000; i++)
            {
                long x = NextComponent(ref state);
                long y = NextComponent(ref state);
                Assert.Equal(TrigReference.Atan2(y, x), FixMath.Atan2(Fix64.FromRaw(y), Fix64.FromRaw(x)).Centi);
            }
        }

        // (3) 보간 없음: 36,000개 모든 각도의 sin/cos 가 기준 표의 칸 하나와 대칭만으로 정확히 정해진다.
        // 1/4바퀴 표의 칸 값 자체도 고정밀 기준값의 가장 가까운 정수와 같다(1센티도 사이의 값을 만들지 않는다).
        [Fact]
        public void SinCos_UseOnlyTableCellsAndSymmetry()
        {
            for (int k = 0; k <= 9000; k++)
            {
                Assert.Equal(TrigReference.SinRaw[k], FixMath.Sin(Angle.FromCenti(k)).Raw);
            }
            for (int c = 0; c < Angle.Full; c++)
            {
                Angle a = Angle.FromCenti(c);
                long s = FixMath.Sin(a).Raw;
                Assert.Equal(TrigReference.Sin(c), s);
                Assert.Equal(TrigReference.Cos(c), FixMath.Cos(a).Raw);
                Assert.Equal(FixMath.Sin(a + 9000).Raw, FixMath.Cos(a).Raw);
                // 대칭: sin(180° − a) = sin a, sin(−a) = −sin a
                Assert.Equal(s, FixMath.Sin(Angle.FromCenti(18000 - c)).Raw);
                Assert.Equal(-s, FixMath.Sin(Angle.FromCenti(-c)).Raw);
            }
        }

        // Angle 의 접기, 차이, 회전이 모든 입력에서 계약의 범위를 지킨다
        [Fact]
        public void Angle_FoldDeltaRotate_Properties()
        {
            int[] edges = { int.MinValue, int.MinValue + 1, -36001, -36000, -1, 0, 1, 35999, 36000, 36001, int.MaxValue };
            foreach (int e in edges)
            {
                Assert.InRange(Angle.FromCenti(e).Centi, 0, Angle.Full - 1);
                Assert.InRange(Angle.FromDegrees(e).Centi, 0, Angle.Full - 1);
                Assert.InRange((Angle.FromCenti(35999) + e).Centi, 0, Angle.Full - 1);
                Assert.Equal(Angle.FromCenti((int)(((long)e % 36000 + 36000) % 36000)), Angle.FromCenti(e));
            }

            ulong state = 0xD1B54A32D192ED03UL;
            for (int i = 0; i < 20000; i++)
            {
                Angle from = Angle.FromCenti((int)(Next(ref state) % 36000UL));
                Angle to = Angle.FromCenti((int)(Next(ref state) % 36000UL));
                int max = (int)(Next(ref state) % 20000UL) - 100;
                int d = Angle.Delta(from, to);
                Assert.InRange(d, -18000, 17999);
                Assert.Equal(to, from + d);

                Angle r = Angle.RotateToward(from, to, max);
                int step = max < 0 ? 0 : max;
                int left = Abs(Angle.Delta(r, to));
                if (Abs(d) <= step)
                {
                    Assert.Equal(to, r);
                }
                else
                {
                    Assert.Equal(step, Abs(Angle.Delta(from, r)));
                    Assert.Equal(Abs(d) - step, left);
                }
            }
        }

        // Step 안에서 부르는 경로다. 할당 0
        [Fact]
        public void TrigPaths_DoNotAllocate()
        {
            long acc = RunTrig(36);   // JIT 데우기
            long before = GC.GetAllocatedBytesForCurrentThread();
            acc ^= RunTrig(36000);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
            Assert.NotEqual(long.MinValue, acc);
        }

        private static long RunTrig(int count)
        {
            long acc = 0;
            for (int c = 0; c < count; c++)
            {
                Angle a = Angle.FromCenti(c * 7);
                FixVec2 v = FixVec2.FromAngle(a);
                acc += FixMath.Sin(a).Raw ^ FixMath.Cos(a).Raw;
                acc += Angle.OfVector(v).Centi;
                acc += FixMath.Atan2(v.X, v.Y).Centi;
                acc += Angle.Delta(a, Angle.FromDir8((Dir8)(c & 7)));
                acc += Angle.RotateToward(a, Angle.FromCenti(c), 300).Centi;
            }
            return acc;
        }

        private static int Sign(long v) => v > 0L ? 1 : (v < 0L ? -1 : 0);

        private static int Abs(int v) => v < 0 ? -v : v;

        // SplitMix64. 코어 난수와 독립인 테스트용 수열
        private static ulong Next(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // 크기를 고르게 퍼뜨린다(1비트 ~ 63비트, 부호 섞음)
        private static long NextComponent(ref ulong state)
        {
            ulong r = Next(ref state);
            int shift = (int)(Next(ref state) % 64UL);
            long v = unchecked((long)(r >> shift));
            return (Next(ref state) & 1UL) == 0UL ? v : unchecked(0L - v);
        }
    }
}
