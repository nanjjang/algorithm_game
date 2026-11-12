using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.World;
using Xunit;

namespace Wizard.Sim.Tests.World
{
    // spec 01 2.3(틱당 이동량), 5.1(능력치 표), 5.6(배율), 14.3(T-M03~T-M06, T-M10~T-M15), spec 05 TV-UNIT-05·06.
    // 기대값은 명세의 표에서 옮겼다(구현 출력에서 베끼지 않았다).
    public sealed class MovementSpeedTests
    {
        private const int Ticks = 60;                               // 1초

        // spec 01 2.3 표 전체: (센티 칸/초, 배율 천분율) → (축 vRaw, 대각 한 축 vDiagRaw)
        [Theory]
        [InlineData(470, 1000, 336_439_105L, 237_898_372L)]          // 달구 걷기
        [InlineData(376, 1000, 269_151_284L, 190_318_698L)]          // 달구 자세
        [InlineData(470, 1200, 403_726_926L, 285_478_047L)]          // 달구 걷기 +20%
        [InlineData(376, 1200, 322_981_541L, 228_382_437L)]          // 달구 자세 +20%
        [InlineData(470, 700, 235_507_373L, 166_528_860L)]           // 달구 걷기 −30%
        [InlineData(376, 700, 188_405_899L, 133_223_088L)]           // 달구 자세 −30%
        [InlineData(560, 1000, 400_863_614L, 283_453_379L)]          // 초롱 걷기
        [InlineData(504, 1000, 360_777_253L, 255_108_042L)]          // 초롱 자세
        [InlineData(560, 1200, 481_036_337L, 340_144_055L)]
        [InlineData(504, 1200, 432_932_703L, 306_129_650L)]
        [InlineData(560, 700, 280_604_530L, 198_417_365L)]
        [InlineData(504, 700, 252_544_077L, 178_575_629L)]
        [InlineData(500, 1000, 357_913_941L, 253_083_374L)]          // 갈피 / 기준값 걷기
        [InlineData(425, 1000, 304_226_850L, 215_120_868L)]          // 갈피 / 기준값 자세
        [InlineData(500, 1200, 429_496_730L, 303_700_050L)]
        [InlineData(425, 1200, 365_072_220L, 258_145_042L)]
        [InlineData(500, 700, 250_539_759L, 177_158_362L)]
        [InlineData(425, 700, 212_958_795L, 150_584_608L)]
        [InlineData(450, 1000, 322_122_547L, 227_775_037L)]          // 성에 걷기 = 자세
        [InlineData(450, 1200, 386_547_057L, 273_330_045L)]
        [InlineData(450, 700, 225_485_783L, 159_442_526L)]
        [InlineData(520, 1000, 372_230_499L, 263_206_710L)]          // 그믐 걷기
        [InlineData(442, 1000, 316_395_924L, 223_725_703L)]          // 그믐 자세
        [InlineData(520, 1200, 446_676_599L, 315_848_052L)]
        [InlineData(442, 1200, 379_675_109L, 268_470_844L)]
        [InlineData(520, 700, 260_561_349L, 184_244_696L)]
        [InlineData(442, 700, 221_477_147L, 156_607_992L)]
        public void SpeedTable_Spec01Section2_3_AxisAndDiagonal(int speedCenti, int mulPermille, long axisRaw, long diagRaw)
        {
            long v = Movement.AxisStepRaw(speedCenti, mulPermille);
            Assert.Equal(axisRaw, v);
            Assert.Equal(diagRaw, Movement.DiagStepRaw(v));
        }

        [Fact]
        public void AxisStep_Spec05TvUnit05_376To269151284()
        {
            Assert.Equal(269_151_284L, Movement.AxisStepRaw(376, 1000));
        }

        [Theory]
        [InlineData(300, 214_748_365L)]                             // spec 05 TV-UNIT-06: 3.0
        [InlineData(450, 322_122_547L)]                             // 4.5
        [InlineData(1800, 1_288_490_189L)]                          // 18
        [InlineData(1600, 1_145_324_612L)]                          // 16
        public void AxisStep_Spec05TvUnit06(int speedCenti, long axisRaw)
        {
            Assert.Equal(axisRaw, Movement.AxisStepRaw(speedCenti, 1000));
        }

        [Fact]
        public void AxisStep_FormulaIsRoundHalfUpOfCentiPermilleOverSixMillion()
        {
            // CONTRACTS 1.1: (SpeedCenti × mulPermille × 2^32 + 3,000,000) / 6,000,000
            for (int s = 0; s <= 1000; s += 7)
            {
                foreach (int m in new[] { 0, 700, 840, 1000, 1200, 1600 })
                {
                    long expected = ((long)s * m * WorldData.OneRaw + 3_000_000L) / 6_000_000L;
                    Assert.Equal(expected, Movement.AxisStepRaw(s, m));
                }
            }
        }

        [Fact]
        public void DiagStep_IsFloorOfProductWithInvSqrt2Raw()
        {
            // spec 01 2.3: (vRaw × INV_SQRT2) >> 32, INV_SQRT2 = 3,037,000,500 (CONTRACTS 0.3)
            Assert.Equal(3_037_000_500L, FixMath.InvSqrt2Raw);
            for (long v = 0; v < 2_000_000_000L; v += 97_654_321L)
            {
                var exact = System.Numerics.BigInteger.Multiply(v, 3_037_000_500L) >> 32;
                Assert.Equal((long)exact, Movement.DiagStepRaw(v));
            }
        }

        [Fact]
        public void S01_T_M03__Char1StanceOneSecondIs60Times269151284()
        {
            CharacterDef c = WorldData.D1;
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(c, inStance: true), Movement.MovePermille(c, false, 1000));
            Assert.Equal(60L * 269_151_284L, OneSecondAlong(Dir8.E, v));
            AssertAboutCells(376, OneSecondAlong(Dir8.E, v));             // ≈ 3.76칸
        }

        [Fact]
        public void S01_T_M04__Char4StanceEqualsWalk()
        {
            CharacterDef c = WorldData.D4;
            long walk = Movement.AxisStepRaw(Movement.SpeedCenti(c, false), 1000);
            long stance = Movement.AxisStepRaw(Movement.SpeedCenti(c, true), 1000);
            Assert.Equal(60L * 322_122_547L, OneSecondAlong(Dir8.E, walk));
            Assert.Equal(60L * 322_122_547L, OneSecondAlong(Dir8.E, stance));
        }

        [Fact]
        public void S01_T_M05__Char2StanceAbout5_04Cells()
        {
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(WorldData.D2, true), 1000);
            Assert.Equal(60L * 360_777_253L, OneSecondAlong(Dir8.E, v));
            AssertAboutCells(504, OneSecondAlong(Dir8.E, v));
        }

        [Fact]
        public void S01_T_M06__Char5StanceAbout4_42Cells()
        {
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(WorldData.D5, true), 1000);
            Assert.Equal(60L * 316_395_924L, OneSecondAlong(Dir8.E, v));
            AssertAboutCells(442, OneSecondAlong(Dir8.E, v));
        }

        [Fact]
        public void S01_T_M10__Char3GrazeBuffWalkAbout6Cells()
        {
            CharacterDef c = WorldData.D3;
            int mul = Movement.MovePermille(c, grazeBuffActive: true, slowPermille: 1000);
            Assert.Equal(1200, mul);
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(c, false), mul);
            Assert.Equal(60L * 429_496_730L, OneSecondAlong(Dir8.E, v));
            AssertAboutCells(600, OneSecondAlong(Dir8.E, v));

            // 기준값 캐릭터(팩의 실제 데이터)도 같다(spec 01 15절 C16: 이동·자세·스침은 갈피와 기준값이 같다)
            CharacterDef b = WorldData.Baseline;
            Assert.Equal(v, Movement.AxisStepRaw(Movement.SpeedCenti(b, false), Movement.MovePermille(b, true, 1000)));
        }

        [Fact]
        public void S01_T_M11__Char1GrazeBuffStanceAbout4_512Cells()
        {
            CharacterDef c = WorldData.D1;
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(c, true), Movement.MovePermille(c, true, 1000));
            Assert.Equal(60L * 322_981_541L, OneSecondAlong(Dir8.E, v));
            AssertAboutCellsMilli(4512, OneSecondAlong(Dir8.E, v));       // 원문 4.51
        }

        [Fact]
        public void S01_T_M12__Char1FrostSlowStanceAbout2_632Cells()
        {
            CharacterDef c = WorldData.D1;
            int mul = Movement.MovePermille(c, grazeBuffActive: false, slowPermille: 700);
            Assert.Equal(700, mul);
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(c, true), mul);
            Assert.Equal(60L * 188_405_899L, OneSecondAlong(Dir8.E, v));
            AssertAboutCellsMilli(2632, OneSecondAlong(Dir8.E, v));       // 원문 2.63, 회피 기준 V
        }

        [Fact]
        public void S01_T_M13__MinStanceSpeedIs376()
        {
            var five = new[] { WorldData.D1, WorldData.D2, WorldData.D3, WorldData.D4, WorldData.D5 };
            Assert.Equal(376, Movement.MinStanceSpeedCenti(five));
            // 순서와 상관없다
            Assert.Equal(376, Movement.MinStanceSpeedCenti(new[] { five[4], five[2], five[0], five[3], five[1] }));
            Assert.Equal(0, Movement.MinStanceSpeedCenti(new CharacterDef[0]));
        }

        // T-M14: 가속 중 걷기/자세(센티). 451.2 같은 값은 SpeedCenti × mulPermille(천분의 1 센티)로 정확히 담긴다
        [Theory]
        [InlineData(1, 564_000, 451_200, 403_726_926L, 322_981_541L)]
        [InlineData(2, 672_000, 604_800, 481_036_337L, 432_932_703L)]
        [InlineData(3, 600_000, 510_000, 429_496_730L, 365_072_220L)]
        [InlineData(4, 540_000, 540_000, 386_547_057L, 386_547_057L)]
        [InlineData(5, 624_000, 530_400, 446_676_599L, 379_675_109L)]
        public void S01_T_M14__GrazeBuffWalkAndStance(int who, int walkMilliCenti, int stanceMilliCenti, long walkRaw, long stanceRaw)
        {
            CharacterDef c = ByNumber(who);
            int mul = Movement.MovePermille(c, grazeBuffActive: true, slowPermille: 1000);
            Assert.Equal(walkMilliCenti, Movement.SpeedCenti(c, false) * mul);
            Assert.Equal(stanceMilliCenti, Movement.SpeedCenti(c, true) * mul);
            Assert.Equal(walkRaw, Movement.AxisStepRaw(Movement.SpeedCenti(c, false), mul));
            Assert.Equal(stanceRaw, Movement.AxisStepRaw(Movement.SpeedCenti(c, true), mul));
        }

        // T-M15: −30% 걷기/자세
        [Theory]
        [InlineData(1, 329_000, 263_200, 235_507_373L, 188_405_899L)]
        [InlineData(2, 392_000, 352_800, 280_604_530L, 252_544_077L)]
        [InlineData(3, 350_000, 297_500, 250_539_759L, 212_958_795L)]
        [InlineData(4, 315_000, 315_000, 225_485_783L, 225_485_783L)]
        [InlineData(5, 364_000, 309_400, 260_561_349L, 221_477_147L)]
        public void S01_T_M15__SlowedWalkAndStance(int who, int walkMilliCenti, int stanceMilliCenti, long walkRaw, long stanceRaw)
        {
            CharacterDef c = ByNumber(who);
            int mul = Movement.MovePermille(c, grazeBuffActive: false, slowPermille: 700);
            Assert.Equal(walkMilliCenti, Movement.SpeedCenti(c, false) * mul);
            Assert.Equal(stanceMilliCenti, Movement.SpeedCenti(c, true) * mul);
            Assert.Equal(walkRaw, Movement.AxisStepRaw(Movement.SpeedCenti(c, false), mul));
            Assert.Equal(stanceRaw, Movement.AxisStepRaw(Movement.SpeedCenti(c, true), mul));
        }

        [Fact]
        public void BuffAndSlowMultiply()
        {
            // spec 01 5.6: 둘이 겹치면 곱한다 1200 × 700 / 1000 = 840
            Assert.Equal(840, Movement.CombinePermille(1200, 700));
            Assert.Equal(840, Movement.MovePermille(WorldData.D3, grazeBuffActive: true, slowPermille: 700));
            Assert.Equal(1000, Movement.MovePermille(WorldData.D3, grazeBuffActive: false, slowPermille: 1000));
        }

        [Fact]
        public void StanceReplacesSpeedInsteadOfScaling()
        {
            // spec 01 5.6: 자세·반전 자세는 StanceSpeedCenti(값 교체)
            Assert.Equal(376, Movement.SpeedCenti(WorldData.D1, inStance: true));
            Assert.Equal(470, Movement.SpeedCenti(WorldData.D1, inStance: false));
            Assert.Equal(425, Movement.SpeedCenti(WorldData.Baseline, inStance: true));
            Assert.Equal(500, Movement.SpeedCenti(WorldData.Baseline, inStance: false));
        }

        private static CharacterDef ByNumber(int who)
        {
            switch (who)
            {
                case 1: return WorldData.D1;
                case 2: return WorldData.D2;
                case 3: return WorldData.D3;
                case 4: return WorldData.D4;
                default: return WorldData.D5;
            }
        }

        // 전장 한가운데에서 1초 동안 그 방향으로 움직인 x 이동량(Raw)
        private static long OneSecondAlong(Dir8 dir, long axisStepRaw)
        {
            ArenaGeometry field = WorldData.Field.Geometry;
            FixVec2 start = WorldData.At(WorldData.Cells(12), WorldData.Cells(7));
            FixVec2 p = start;
            for (int t = 0; t < Ticks; t++)
            {
                p = Movement.Integrate(field, p, dir, axisStepRaw, Fix64.FromRaw(WorldData.BodyRadiusRaw));
            }
            Assert.Equal(start.Y.Raw, p.Y.Raw);
            return p.X.Raw - start.X.Raw;
        }

        // 센티 칸으로 적힌 "≈" 값: 틱마다의 반올림이 60번 쌓인 차이(틱당 0.5 Raw 이하) 안에 있다
        private static void AssertAboutCells(int centiCells, long raw)
        {
            long exact = (long)centiCells * WorldData.OneRaw / 100L;
            Assert.InRange(raw - exact, -Ticks, Ticks);
        }

        private static void AssertAboutCellsMilli(int milliCells, long raw)
        {
            long exact = (long)milliCells * WorldData.OneRaw / 1000L;
            Assert.InRange(raw - exact, -Ticks, Ticks);
        }
    }
}
