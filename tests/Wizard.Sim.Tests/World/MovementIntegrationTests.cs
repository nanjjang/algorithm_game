using System;
using System.Numerics;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.World;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.World
{
    // spec 01 5.2(방향), 5.6(전장과 벽, 이동 적분), 14.3(T-M01·T-M02·T-M07~T-M09). 전장은 저장소 data/sim/arena.json,
    // 캐릭터는 data/sim/characters.json 의 기준값(= 갈피의 이동 수치, spec 01 15절 C16).
    public sealed class MovementIntegrationTests
    {
        private static readonly Fix64 Body = Fix64.FromRaw(WorldData.BodyRadiusRaw);

        // ── 명세 벡터

        [Fact]
        public void S01_T_M01__Char3RightSixtyTicks()
        {
            CharacterDef c = WorldData.Baseline;
            Assert.Equal(WorldData.BodyRadiusRaw, c.BodyRadius.Raw);           // spec 01 5.6 [임시] 0.30
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(c, false), Movement.MovePermille(c, false, 1000));

            // Right 만 눌림 → 가로 +1, 세로 0 → E
            int ax = Movement.ResolveAxis(false, 0, true, 10);
            int ay = Movement.ResolveAxis(false, 0, false, 0);
            Assert.True(Movement.TryDirFromAxes(ax, ay, out Dir8 dir));
            Assert.Equal(Dir8.E, dir);

            FixVec2 p = Run(WorldData.Field.Geometry, Centre(), dir, v, c.BodyRadius, 60);
            Assert.Equal(73_014_444_012L, p.X.Raw);                            // 12 × 2^32 + 60 × 357,913,941
            Assert.Equal(5L * WorldData.OneRaw - 20L, p.X.Raw - 12L * WorldData.OneRaw);   // 5칸보다 Raw 20 적다
            Assert.Equal(7L * WorldData.OneRaw, p.Y.Raw);
        }

        [Fact]
        public void S01_T_M02__Char3UpRightSixtyTicksDiagonalSpeedEqualsAxis()
        {
            CharacterDef c = WorldData.Baseline;
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(c, false), 1000);
            int ax = Movement.ResolveAxis(false, 0, true, 10);                 // Right
            int ay = Movement.ResolveAxis(true, 10, false, 0);                 // Up(−y)
            Assert.True(Movement.TryDirFromAxes(ax, ay, out Dir8 dir));
            Assert.Equal(Dir8.NE, dir);

            FixVec2 p = Run(WorldData.Field.Geometry, Centre(), dir, v, c.BodyRadius, 60);
            Assert.Equal(15_185_002_440L, 60L * 253_083_374L);
            Assert.Equal(66_724_609_992L, p.X.Raw);
            Assert.Equal(14_879_768_632L, p.Y.Raw);

            // 대각 속력 = 축 속력: 한 틱 이동 벡터의 길이가 축 이동량과 (반올림 안에서) 같다
            FixVec2 d = Movement.StepDelta(Dir8.NE, v);
            BigInteger lenSq = (BigInteger)d.X.Raw * d.X.Raw + (BigInteger)d.Y.Raw * d.Y.Raw;
            BigInteger vSq = (BigInteger)v * v;
            // vDiag = floor(v/√2) 이므로 길이는 (v − √2, v] 안: |길이² − v²| < 2√2·v
            Assert.True(BigInteger.Abs(lenSq - vSq) <= 3 * (BigInteger)v);
        }

        [Fact]
        public void S01_T_M07__LaterPressedOppositeKeyWins()
        {
            // Left 를 누른 상태(시각 100)에서 Right 누름(시각 200) → E
            int ax = Movement.ResolveAxis(true, 100, true, 200);
            Assert.Equal(1, ax);
            Assert.True(Movement.TryDirFromAxes(ax, 0, out Dir8 d1));
            Assert.Equal(Dir8.E, d1);

            // Right 뗌 → Left 만 남는다 → 다시 W
            ax = Movement.ResolveAxis(true, 100, false, 200);
            Assert.Equal(-1, ax);
            Assert.True(Movement.TryDirFromAxes(ax, 0, out Dir8 d2));
            Assert.Equal(Dir8.W, d2);

            // 세로축도 같은 규칙(Up = −, Down = +)
            Assert.Equal(1, Movement.ResolveAxis(true, 5, true, 6));
            Assert.Equal(-1, Movement.ResolveAxis(true, 7, true, 6));
        }

        [Fact]
        public void S01_T_M08__WallStopsAtBodyRadius()
        {
            CharacterDef c = WorldData.Baseline;
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(c, false), 1000);
            FixVec2 start = WorldData.At(WorldData.Ratio(31, 100), WorldData.Cells(7));
            FixVec2 p = Movement.Integrate(WorldData.Field.Geometry, start, Dir8.W, v, c.BodyRadius);
            Assert.Equal(WorldData.BodyRadiusRaw, p.X.Raw);                    // x = 0.30
            Assert.Equal(7L * WorldData.OneRaw, p.Y.Raw);
        }

        [Fact]
        public void S01_T_M09__WallSlideKeepsXAndMovesY()
        {
            CharacterDef c = WorldData.Baseline;
            long v = Movement.AxisStepRaw(Movement.SpeedCenti(c, false), 1000);
            FixVec2 start = WorldData.At(Fix64.FromRaw(WorldData.BodyRadiusRaw), WorldData.Cells(7));
            int ax = Movement.ResolveAxis(true, 1, false, 0);                  // Left
            int ay = Movement.ResolveAxis(true, 1, false, 0);                  // Up
            Assert.True(Movement.TryDirFromAxes(ax, ay, out Dir8 dir));
            Assert.Equal(Dir8.NW, dir);
            FixVec2 p = start;
            for (int t = 1; t <= 10; t++)
            {
                p = Movement.Integrate(WorldData.Field.Geometry, p, dir, v, c.BodyRadius);
                Assert.Equal(WorldData.BodyRadiusRaw, p.X.Raw);                // x 는 0.30 그대로
                Assert.Equal(7L * WorldData.OneRaw - t * 253_083_374L, p.Y.Raw);  // y 만 줄어든다(대각 한 축만큼)
            }
        }

        // ── 방향 도우미(spec 01 5.2)

        [Fact]
        public void DirFromAxes_AllNineCases()
        {
            Assert.False(Movement.TryDirFromAxes(0, 0, out _));
            AssertDir(1, 0, Dir8.E);
            AssertDir(1, -1, Dir8.NE);
            AssertDir(0, -1, Dir8.N);
            AssertDir(-1, -1, Dir8.NW);
            AssertDir(-1, 0, Dir8.W);
            AssertDir(-1, 1, Dir8.SW);
            AssertDir(0, 1, Dir8.S);
            AssertDir(1, 1, Dir8.SE);
            for (int d = 0; d < 8; d++)
            {
                Assert.True(Movement.TryDirFromAxes(Movement.DirX((Dir8)d), Movement.DirY((Dir8)d), out Dir8 back));
                Assert.Equal((Dir8)d, back);
                Assert.Equal(d % 2 == 1, Movement.IsDiagonal((Dir8)d));
                Assert.Equal((Dir8)((d + 4) & 7), Movement.Flip((Dir8)d));
            }
        }

        [Fact]
        public void ResolveAxis_SingleNoneAndTie()
        {
            Assert.Equal(0, Movement.ResolveAxis(false, 0, false, 0));
            Assert.Equal(-1, Movement.ResolveAxis(true, 3, false, 9));
            Assert.Equal(1, Movement.ResolveAxis(false, 9, true, 3));
            // 같은 시각에 둘 다 눌렸으면 0 [임시]: 좌우 거울 입력의 결과가 거울이 되게
            Assert.Equal(0, Movement.ResolveAxis(true, 42, true, 42));
        }

        [Fact]
        public void StepDelta_NorthIsScreenUp()
        {
            FixVec2 d = Movement.StepDelta(Dir8.N, 1000);
            Assert.Equal(0L, d.X.Raw);
            Assert.Equal(-1000L, d.Y.Raw);
            FixVec2 se = Movement.StepDelta(Dir8.SE, 357_913_941L);
            Assert.Equal(253_083_374L, se.X.Raw);
            Assert.Equal(253_083_374L, se.Y.Raw);
        }

        // ── 벽과 모서리

        [Theory]
        [InlineData(Dir8.NW, false, false)]
        [InlineData(Dir8.NE, true, false)]
        [InlineData(Dir8.SW, false, true)]
        [InlineData(Dir8.SE, true, true)]
        public void Corner_ClampsBothAxes(Dir8 dir, bool right, bool bottom)
        {
            ArenaGeometry field = WorldData.Field.Geometry;
            long v = Movement.AxisStepRaw(500, 1000);
            FixVec2 p = Run(field, Centre(), dir, v, Body, 60 * 10);
            long r = WorldData.BodyRadiusRaw;
            Assert.Equal(right ? 24L * WorldData.OneRaw - r : r, p.X.Raw);    // [0.30, 23.70]
            Assert.Equal(bottom ? 14L * WorldData.OneRaw - r : r, p.Y.Raw);   // [0.30, 13.70]
        }

        [Fact]
        public void ResolveBody_PutsAnyPointInsideTheWalls()
        {
            ArenaGeometry field = WorldData.Field.Geometry;
            long r = WorldData.BodyRadiusRaw;
            FixVec2 far = field.ResolveBody(WorldData.At(WorldData.Cells(-5), WorldData.Cells(40)), Body);
            Assert.Equal(r, far.X.Raw);
            Assert.Equal(14L * WorldData.OneRaw - r, far.Y.Raw);
        }

        // ── 기둥(결투장 돌마당, world-characters 2.6: 칸 (9,3) = x 9~10, y 3~4)

        [Fact]
        public void Pillar_StopsAtFaceAndSlides()
        {
            ArenaGeometry duel = WorldData.DuelArena.Geometry;
            Assert.Equal(4, duel.PillarCount);
            long v = Movement.AxisStepRaw(500, 1000);
            long r = WorldData.BodyRadiusRaw;

            // 왼쪽에서 오른쪽으로: 왼쪽 면(x = 9 − r)에서 멈춘다, y 그대로
            FixVec2 start = WorldData.At(WorldData.Cells(8), WorldData.Ratio(35, 10));
            FixVec2 p = Run(duel, start, Dir8.E, v, Body, 60);
            Assert.Equal(9L * WorldData.OneRaw - r, p.X.Raw);
            Assert.Equal(start.Y.Raw, p.Y.Raw);

            // 그 자리에서 SE 로: 면을 따라 아래로 미끄러진다(x 는 면에 붙은 채, y 는 대각 한 축만큼).
            // 8틱 뒤 y ≈ 3.97 이라 아직 면(y 3~4) 안이다. 그 뒤는 모서리를 돌아 나간다
            long diag = Movement.DiagStepRaw(v);
            FixVec2 q = p;
            for (int t = 1; t <= 8; t++)
            {
                q = Movement.Integrate(duel, q, Dir8.SE, v, Body);
                Assert.Equal(9L * WorldData.OneRaw - r, q.X.Raw);
                Assert.Equal(p.Y.Raw + t * diag, q.Y.Raw);
            }
        }

        [Fact]
        public void Pillar_CornerPushIsRadialAndSymmetric()
        {
            // (9,3) 기둥의 왼쪽 위 모서리로 정확히 대각선에서 다가간다: 모서리에서 대각선 방향으로 거리 r 에 선다
            ArenaGeometry duel = WorldData.DuelArena.Geometry;
            long v = Movement.AxisStepRaw(500, 1000);
            long r = WorldData.BodyRadiusRaw;
            FixVec2 start = WorldData.At(WorldData.Ratio(85, 10), WorldData.Ratio(25, 10));
            FixVec2 p = Run(duel, start, Dir8.SE, v, Body, 60);
            long dx = 9L * WorldData.OneRaw - p.X.Raw;
            long dy = 3L * WorldData.OneRaw - p.Y.Raw;
            Assert.Equal(dx, dy);
            BigInteger distSq = (BigInteger)dx * dx + (BigInteger)dy * dy;
            // 밀어낸 거리는 r 에서 몇 Raw 안(제곱근과 곱셈의 내림)
            Assert.True(distSq <= (BigInteger)(r + 4) * (r + 4));
            Assert.True(distSq >= (BigInteger)(r - 4) * (r - 4));
        }

        [Fact]
        public void Pillar_InsideCentreIsPushedOutThroughShallowestFace()
        {
            ArenaGeometry duel = WorldData.DuelArena.Geometry;
            long r = WorldData.BodyRadiusRaw;
            // 기둥 (9,3) 안, 왼쪽 면에서 0.1칸: 왼쪽으로
            FixVec2 p = duel.ResolveBody(WorldData.At(WorldData.Ratio(91, 10), WorldData.Ratio(36, 10)), Body);
            Assert.Equal(9L * WorldData.OneRaw - r, p.X.Raw);
            // 아래 면에서 0.1칸: 아래로
            FixVec2 q = duel.ResolveBody(WorldData.At(WorldData.Ratio(96, 10), WorldData.Ratio(39, 10)), Body);
            Assert.Equal(4L * WorldData.OneRaw + r, q.Y.Raw);
            Assert.True(duel.IsInsidePillar(WorldData.At(WorldData.Ratio(95, 10), WorldData.Ratio(35, 10))));
            Assert.False(duel.IsInsidePillar(Centre()));
        }

        // ── 거울 대칭(수락 조건 1): 거울 입력이면 매 틱 거울 결과

        [Theory]
        [InlineData(SimMode.Endless, 0x5EED_0001UL)]
        [InlineData(SimMode.Duel, 0x5EED_0002UL)]
        [InlineData(SimMode.Duel, 0x5EED_0003UL)]
        public void MirrorInputGivesMirrorResult_LeftRightAndUpDown(SimMode mode, ulong seed)
        {
            ArenaGeometryFor a = mode == SimMode.Duel ? WorldData.DuelArena : WorldData.Field;
            ArenaGeometry g = a.Geometry;
            long w = a.Def.Width.Raw, h = a.Def.Height.Raw;
            var rng = new Pcg32(seed, 54);
            int[] speeds = { 376, 500, 672 };
            int[] muls = { 700, 1000, 1200 };
            for (int trial = 0; trial < 400; trial++)
            {
                long x = (long)(((ulong)rng.NextU32() << 32 | rng.NextU32()) % (ulong)w);
                long y = (long)(((ulong)rng.NextU32() << 32 | rng.NextU32()) % (ulong)h);
                var dir = (Dir8)rng.NextInt(8);
                long v = Movement.AxisStepRaw(speeds[rng.NextInt(3)], muls[rng.NextInt(3)]);
                FixVec2 p = WorldData.At(Fix64.FromRaw(x), Fix64.FromRaw(y));
                FixVec2 px = WorldData.At(Fix64.FromRaw(w - x), Fix64.FromRaw(y));
                FixVec2 py = WorldData.At(Fix64.FromRaw(x), Fix64.FromRaw(h - y));
                for (int t = 0; t < 90; t++)
                {
                    if (t == 45)
                    {
                        dir = (Dir8)rng.NextInt(8);                          // 도중에 방향을 바꿔 벽·기둥에 여러 각도로 닿게
                    }
                    p = Movement.Integrate(g, p, dir, v, Body);
                    px = Movement.Integrate(g, px, MirrorX(dir), v, Body);
                    py = Movement.Integrate(g, py, MirrorY(dir), v, Body);
                    Assert.Equal(w - p.X.Raw, px.X.Raw);
                    Assert.Equal(p.Y.Raw, px.Y.Raw);
                    Assert.Equal(p.X.Raw, py.X.Raw);
                    Assert.Equal(h - p.Y.Raw, py.Y.Raw);
                    AssertOutsideObstacles(a, p);
                }
            }
        }

        [Fact]
        public void MirrorSymmetry_WallCornersAndPillarCorners()
        {
            // 네 벽 모서리와 네 기둥의 여덟 모서리 근처에서 모서리 쪽 대각선으로 몰기: 거울 쌍의 결과가 거울이다
            ArenaGeometryFor a = WorldData.DuelArena;
            long w = a.Def.Width.Raw, h = a.Def.Height.Raw;
            long v = Movement.AxisStepRaw(672, 1200);
            var starts = new[]
            {
                WorldData.At(WorldData.Ratio(13, 10), WorldData.Ratio(11, 10)),       // 왼쪽 위 벽 모서리 근처
                WorldData.At(WorldData.Ratio(84, 10), WorldData.Ratio(23, 10)),       // 기둥 (9,3) 왼쪽 위
                WorldData.At(WorldData.Ratio(103, 10), WorldData.Ratio(47, 10)),      // 기둥 (9,3) 오른쪽 아래
                WorldData.At(WorldData.Ratio(87, 10), WorldData.Ratio(44, 10)),       // 기둥 (9,3) 왼쪽 아래
            };
            foreach (FixVec2 s in starts)
            {
                foreach (Dir8 dir in new[] { Dir8.NE, Dir8.NW, Dir8.SW, Dir8.SE })
                {
                    FixVec2 p = Run(a.Geometry, s, dir, v, Body, 60);
                    FixVec2 px = Run(a.Geometry, WorldData.At(Fix64.FromRaw(w - s.X.Raw), s.Y), MirrorX(dir), v, Body, 60);
                    FixVec2 py = Run(a.Geometry, WorldData.At(s.X, Fix64.FromRaw(h - s.Y.Raw)), MirrorY(dir), v, Body, 60);
                    FixVec2 pxy = Run(a.Geometry, WorldData.At(Fix64.FromRaw(w - s.X.Raw), Fix64.FromRaw(h - s.Y.Raw)),
                                      MirrorY(MirrorX(dir)), v, Body, 60);
                    Assert.Equal(w - p.X.Raw, px.X.Raw);
                    Assert.Equal(p.Y.Raw, px.Y.Raw);
                    Assert.Equal(p.X.Raw, py.X.Raw);
                    Assert.Equal(h - p.Y.Raw, py.Y.Raw);
                    Assert.Equal(w - p.X.Raw, pxy.X.Raw);
                    Assert.Equal(h - p.Y.Raw, pxy.Y.Raw);
                }
            }
        }

        // ── 할당 0(Step 안에서 부른다)

        // 같은 각본을 여러 번 돌려 가장 작은 값이 0인지 본다(AllocationProbe. 한 번 재기는 병렬 실행에서 가끔 흔들렸다, FIX-1)
        [Fact]
        public void Integrate_AllocatesNothing()
        {
            ArenaGeometry duel = WorldData.DuelArena.Geometry;
            long v = Movement.AxisStepRaw(672, 1200);
            FixVec2 p = default;
            long least = AllocationProbe.LeastBytes(() =>
            {
                p = WorldData.At(WorldData.Ratio(84, 10), WorldData.Ratio(23, 10));
                p = Run(duel, p, Dir8.SE, v, Body, 10);                          // 데우기
                return () =>
                {
                    for (int t = 0; t < 2000; t++)
                    {
                        p = Movement.Integrate(duel, p, (Dir8)(t / 37 % 8), v, Body);
                    }
                };
            });
            Assert.Equal(0L, least);
        }

        [Fact]
        public void Geometry_RejectsNullAndEmptyArena()
        {
            Assert.Throws<ArgumentNullException>(() => new ArenaGeometry(null!));
            Assert.Throws<ArgumentException>(() => new ArenaGeometry(new ArenaDef()));
        }

        // ── 도우미

        private static FixVec2 Centre() => WorldData.At(WorldData.Cells(12), WorldData.Cells(7));

        private static FixVec2 Run(ArenaGeometry g, FixVec2 p, Dir8 dir, long v, Fix64 body, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                p = Movement.Integrate(g, p, dir, v, body);
            }
            return p;
        }

        private static void AssertDir(int x, int y, Dir8 expected)
        {
            Assert.True(Movement.TryDirFromAxes(x, y, out Dir8 d));
            Assert.Equal(expected, d);
        }

        // 좌우 거울: E↔W, NE↔NW, SE↔SW
        private static Dir8 MirrorX(Dir8 d) => (Dir8)((4 - (int)d) & 7);

        // 상하 거울: N↔S, NE↔SE, NW↔SW
        private static Dir8 MirrorY(Dir8 d) => (Dir8)((8 - (int)d) & 7);

        // 몸의 중심이 벽 안에 있고, 모든 기둥에서 r 에서 몇 Raw 모자란 거리 이상 떨어져 있다(밀어내기의 내림 몫)
        private static void AssertOutsideObstacles(ArenaGeometryFor a, FixVec2 p)
        {
            long r = WorldData.BodyRadiusRaw;
            Assert.InRange(p.X.Raw, r, a.Def.Width.Raw - r);
            Assert.InRange(p.Y.Raw, r, a.Def.Height.Raw - r);
            foreach (FixVec2 c in a.Def.Pillars)
            {
                long minX = c.X.Raw, maxX = c.X.Raw + WorldData.OneRaw;
                long minY = c.Y.Raw, maxY = c.Y.Raw + WorldData.OneRaw;
                long qx = Math.Clamp(p.X.Raw, minX, maxX);
                long qy = Math.Clamp(p.Y.Raw, minY, maxY);
                BigInteger dx = p.X.Raw - qx, dy = p.Y.Raw - qy;
                BigInteger distSq = dx * dx + dy * dy;
                Assert.True(distSq >= (BigInteger)(r - 4) * (r - 4), "body overlaps a pillar");
            }
        }
    }
}
