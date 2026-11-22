using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Player
{
    /// <summary>이동·벽·비기 이동. 02 요약서 4.1·4.5·4.6절.</summary>
    public class PlayerMoveTests
    {
        const float Eps = 0.002f;

        /// <summary>오른쪽으로 60틱(1초) 걸은 거리 = 속력(칸/초).</summary>
        static float RunOneSecond(Kit k, bool stance)
        {
            k.P.Pos = new Vector2(3f, 7f);
            if (stance) k.Press(LogicalKey.Stance);
            k.Press(LogicalKey.Right);
            float x0 = k.P.Pos.x;
            k.Step(60);
            return k.P.Pos.x - x0;
        }

        // 02 요약서 4.5절 속도 검산표(S04 14.2): 걷기 / 자세·반전
        [TestCase("char.1", 4.7f, 3.76f)]
        [TestCase("char.2", 5.6f, 5.04f)]
        [TestCase("char.3", 5.0f, 4.25f)]
        [TestCase("char.4", 4.5f, 4.5f)]
        [TestCase("char.5", 5.2f, 4.42f)]
        public void S04_14_2__WalkAndStanceSpeed(string charId, float walk, float stance)
        {
            Assert.AreEqual(walk, RunOneSecond(Kit.Create(charId), false), Eps);
            Assert.AreEqual(stance, RunOneSecond(Kit.Create(charId), true), Eps);
        }

        // T-M14: 가속 중 걷기 / 자세
        [TestCase("char.1", 5.64f, 4.512f)]
        [TestCase("char.2", 6.72f, 6.048f)]
        [TestCase("char.3", 6.0f, 5.1f)]
        [TestCase("char.4", 5.4f, 5.4f)]
        [TestCase("char.5", 6.24f, 5.304f)]
        public void S01_T_M14__GrazeBuffSpeed(string charId, float walk, float stance)
        {
            var a = Kit.Create(charId); a.P.GrazeBuffUntilTick = 1000;
            Assert.AreEqual(walk, RunOneSecond(a, false), Eps);
            var b = Kit.Create(charId); b.P.GrazeBuffUntilTick = 1000;
            Assert.AreEqual(stance, RunOneSecond(b, true), Eps);
        }

        // T-M15: −30% 걷기 / 자세
        [TestCase("char.1", 3.29f, 2.632f)]
        [TestCase("char.2", 3.92f, 3.528f)]
        [TestCase("char.3", 3.5f, 2.975f)]
        [TestCase("char.4", 3.15f, 3.15f)]
        [TestCase("char.5", 3.64f, 3.094f)]
        public void S01_T_M15__FrostSlowSpeed(string charId, float walk, float stance)
        {
            var a = Kit.Create(charId); a.P.EnvSpeedMul = 0.7f;
            Assert.AreEqual(walk, RunOneSecond(a, false), Eps);
            var b = Kit.Create(charId); b.P.EnvSpeedMul = 0.7f;
            Assert.AreEqual(stance, RunOneSecond(b, true), Eps);
        }

        // 4.5절: 가속과 감속이 겹치면 곱(1.2 × 0.7 = 0.84) [임시]. 기준값 걷기 5.0 → 4.2
        [Test]
        public void S01_5_6__BuffAndSlowMultiply()
        {
            var k = Kit.Create();
            k.P.GrazeBuffUntilTick = 1000;
            k.P.EnvSpeedMul = 0.7f;
            Assert.AreEqual(4.2f, RunOneSecond(k, false), Eps);
        }

        // 4.1절: 대각 이동 속력은 축 이동과 같게 정규화. N = 화면 위 = 시뮬레이션 y−.
        [Test]
        public void S01_U33__DiagonalIsNormalized()
        {
            var k = Kit.Create();
            Vector2 start = k.P.Pos;
            k.Press(LogicalKey.Up);
            k.Press(LogicalKey.Right);
            k.Step(60);
            Vector2 d = k.P.Pos - start;
            Assert.AreEqual(5.0f, d.magnitude, Eps);
            Assert.AreEqual(5.0f / Mathf.Sqrt(2f), d.x, Eps);
            Assert.AreEqual(-5.0f / Mathf.Sqrt(2f), d.y, Eps);
            Assert.IsTrue(k.P.Moving);
            Assert.AreEqual(Dir8.NE, k.P.MoveDir);
        }

        // 4.1절: 가로축 둘 다 눌리면 나중에 누른 쪽.
        [Test]
        public void S01_4_1__OppositeKeysLaterPressWins()
        {
            var k = Kit.Create();
            float x0 = k.P.Pos.x;
            k.Press(LogicalKey.Left, 0);
            k.Press(LogicalKey.Right, 10);
            k.Step(6);
            Assert.Greater(k.P.Pos.x, x0);
            // 나중에 누른 쪽을 떼면 남은 쪽으로.
            k.Release(LogicalKey.Right);
            float x1 = k.P.Pos.x;
            k.Step(6);
            Assert.Less(k.P.Pos.x, x1);
        }

        // 4.6절: 몸 반지름 0.30으로 [0.30, 23.70] × [0.30, 13.70]에 축별로 자른다(벽 따라 미끄러짐).
        [Test]
        public void S01_5_6__WallClampPerAxisAndSlide()
        {
            var k = Kit.Create();
            k.P.Pos = new Vector2(0.5f, 7f);
            k.Press(LogicalKey.Left);
            k.Press(LogicalKey.Up);
            k.Step(30);
            Assert.AreEqual(0.30f, k.P.Pos.x, 1e-5f);
            Assert.Less(k.P.Pos.y, 7f - 1f, "벽에 붙어도 다른 축으로는 미끄러진다");

            var b = Kit.Create();
            b.P.Pos = new Vector2(23.5f, 13.5f);
            b.Press(LogicalKey.Right);
            b.Press(LogicalKey.Down);
            b.Step(30);
            Assert.AreEqual(23.70f, b.P.Pos.x, 1e-5f);
            Assert.AreEqual(13.70f, b.P.Pos.y, 1e-5f);
        }

        // 00 문서 5.1절: 전리품 고르기·판 종료에는 이동 없음.
        [TestCase(RunPhase.LootPick)]
        [TestCase(RunPhase.RunEnded)]
        [TestCase(RunPhase.NotStarted)]
        public void Arch_5_1__NoMoveWhenControlInactive(RunPhase phase)
        {
            var k = Kit.Create();
            k.W.Run.Phase = phase;
            Vector2 start = k.P.Pos;
            k.Press(LogicalKey.Right);
            k.Step(10);
            Assert.AreEqual(start, k.P.Pos);
            Assert.IsFalse(k.P.Moving);
        }

        [Test]
        public void Arch_5_1__CampScreenStopsMoveButFireIdleMoves()
        {
            var k = Kit.Create();
            k.W.Run.Phase = RunPhase.Camp;
            k.W.Run.CampSub = CampSubState.CampScreen;
            Vector2 start = k.P.Pos;
            k.Press(LogicalKey.Right);
            k.Step(5);
            Assert.AreEqual(start, k.P.Pos);
            k.W.Run.CampSub = CampSubState.FireIdle;
            k.Step(5);
            Assert.Greater(k.P.Pos.x, start.x);
        }

        // 4단계: 장애물(Threats.SegmentHitsObstacle)에 막히면 막히지 않은 축으로만 간다.
        [Test]
        public void Arch_5_1__ObstacleBlocksAxisAndSlides()
        {
            var k = Kit.Create();
            // x = 12.5의 세로 벽: 선분이 넘으면 막힌다.
            k.Threats.Blocks = (a, b) => a.x < 12.5f && b.x >= 12.5f;
            k.Press(LogicalKey.Right);
            k.Press(LogicalKey.Down);
            k.Step(60);
            Assert.Less(k.P.Pos.x, 12.5f);
            Assert.Greater(k.P.Pos.y, 7f + 1f);
        }

        [Test]
        public void PrevPos_IsPositionBeforeMove()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Right);
            k.Step(2);
            Assert.AreEqual(5.0f / 60f, k.P.Pos.x - k.P.PrevPos.x, 1e-4f);
        }

        // ------------------------------------------------------------ 비기 이동

        // 02 요약서 8.3절 벼락 질주: 벽 앞에서 멈춘다.
        [Test]
        public void S04_BoltDash__MoveByArtStopsAtWall()
        {
            var k = Kit.Create();
            k.P.Pos = new Vector2(22f, 7f);
            Vector2 end = k.Sys.MoveByArt(k.W, k.P, new Vector2(4f, 0f), false, false);
            Assert.AreEqual(23.70f, end.x, 1e-4f);
            Assert.AreEqual(7f, end.y, 1e-4f);
            Assert.AreEqual(end, k.P.Pos);

            k.P.Pos = new Vector2(12f, 1f);
            end = k.Sys.MoveByArt(k.W, k.P, new Vector2(0f, -4f), false, false);
            Assert.AreEqual(0.30f, end.y, 1e-4f);
        }

        [Test]
        public void S04_BoltDash__MoveByArtFullDistanceWhenFree()
        {
            var k = Kit.Create();
            Vector2 end = k.Sys.MoveByArt(k.W, k.P, new Vector2(4f, 0f), false, false);
            Assert.AreEqual(16f, end.x, 1e-4f);
        }

        // 잔상 걸음: 적 몸 앞에서 멈춘다. 죽어 가는 적(PendingDeath)은 없는 것으로 본다(공통 규칙 2).
        [Test]
        public void S04_Afterimage__MoveByArtStopsBeforeAliveEnemyOnly()
        {
            var k = Kit.Create();
            var e = new EnemyState { Id = 5, Pos = new Vector2(15f, 7f), BodyRadius = 0.5f, Hp = 10f };
            k.W.Enemies.Add(e);
            Vector2 end = k.Sys.MoveByArt(k.W, k.P, new Vector2(4f, 0f), true, false);
            // 적 몸 0.5 + 내 몸 0.30 앞.
            Assert.AreEqual(15f - 0.5f - 0.30f, end.x, 1e-3f);

            k.P.Pos = new Vector2(12f, 7f);
            end = k.Sys.MoveByArt(k.W, k.P, new Vector2(4f, 0f), false, false);
            Assert.AreEqual(16f, end.x, 1e-4f, "stopAtEnemies가 아니면 뚫고 지나간다");

            k.P.Pos = new Vector2(12f, 7f);
            e.PendingDeath = true;
            end = k.Sys.MoveByArt(k.W, k.P, new Vector2(4f, 0f), true, false);
            Assert.AreEqual(16f, end.x, 1e-4f);
        }

        [Test]
        public void S04_BoltDash__MoveByArtStopsBeforeObstacle()
        {
            var k = Kit.Create();
            k.Threats.Obstacle = new ObstacleEntity { Id = 9, Pos = new Vector2(15f, 7f), Radius = 0.5f };
            k.Threats.Blocks = (a, b) => a.x < 14.5f && b.x >= 14.5f;
            Vector2 end = k.Sys.MoveByArt(k.W, k.P, new Vector2(4f, 0f), false, true);
            Assert.AreEqual(15f - 0.5f - 0.30f, end.x, 1e-3f);

            k.P.Pos = new Vector2(12f, 7f);
            end = k.Sys.MoveByArt(k.W, k.P, new Vector2(4f, 0f), false, false);
            Assert.AreEqual(16f, end.x, 1e-4f);
        }
    }
}
