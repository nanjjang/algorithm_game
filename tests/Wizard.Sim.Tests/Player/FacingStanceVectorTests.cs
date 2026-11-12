using System.Collections.Generic;
using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;
using static Wizard.Sim.Tests.Player.PlayerRig;

namespace Wizard.Sim.Tests.Player
{
    // spec 01 14.4 시야와 자세(T-F01~T-F19). 처리 규칙은 spec 01 5.3(시야), 5.4(대각 유지 창), 5.5(자세 전이표).
    // 세계는 저장소 팩의 기준값 캐릭터. 시작 시야는 아래(S), 위치 (12, 7)(spec 01 3.7 [임시]).
    public sealed class FacingStanceVectorTests
    {
        private const long OneRaw = 4_294_967_296L;

        // 기준값 자세 속도 425센티의 틱당 이동량(CONTRACTS 1.1·spec 01 2.3): (425 × 1000 × 2^32 + 3,000,000) / 6,000,000
        private static readonly long StanceStepRaw = (425L * 1000L * OneRaw + 3_000_000L) / 6_000_000L;

        private static void AssertResets(PlayerRig rig, params CommandResetReason[] reasons)
        {
            List<SimEvent> resets = rig.Log.Of(SimEventType.CommandReset);
            Assert.Equal(reasons.Length, resets.Count);
            for (int i = 0; i < reasons.Length; i++)
            {
                Assert.Equal((int)reasons[i], resets[i].A);
            }
        }

        // 자세(잠긴 E)를 만든다: Right 누름 → 시야 E, Right 뗌 → E 그대로, Stance 누름
        private static PlayerRig StanceLockedEast()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Right));
            rig.Step(Release(LogicalKey.Right));
            rig.Step(Press(LogicalKey.Stance));
            Assert.Equal(StanceMode.Stance, rig.P.Stance);
            Assert.Equal(Dir8.E, rig.P.StanceLockedFacing);
            rig.Log.Clear();
            return rig;
        }

        // 자세 밖, Up 을 먼저 누르고 Right 를 나중에(또는 그 반대) 눌러 시야 NE
        private static PlayerRig NorthEast(bool rightLater)
        {
            PlayerRig rig = Endless();
            if (rightLater)
            {
                rig.Step(Press(LogicalKey.Up, 0), Press(LogicalKey.Right, 10));
            }
            else
            {
                rig.Step(Press(LogicalKey.Right, 0), Press(LogicalKey.Up, 10));
            }
            Assert.Equal(Dir8.NE, rig.P.Facing);
            rig.Log.Clear();
            return rig;
        }

        [Fact]
        public void S01_T_F01__RightPressFacesEast()
        {
            PlayerRig rig = Endless();
            rig.Step();
            Assert.Equal(Dir8.S, rig.P.Facing);              // 시작 시야(spec 01 3.7)
            rig.Step(Press(LogicalKey.Right));
            Assert.Equal(Dir8.E, rig.P.Facing);
            Assert.Equal((int)Dir8.E, rig.Log.Single(SimEventType.FacingChanged).A);
        }

        [Fact]
        public void S01_T_F02__StanceSnapsDiagonalToLaterPressedRight()
        {
            PlayerRig rig = NorthEast(rightLater: true);
            rig.Step(Press(LogicalKey.Stance));
            Assert.Equal(StanceMode.Stance, rig.P.Stance);
            Assert.Equal(Dir8.E, rig.P.StanceLockedFacing);
            Assert.Equal(Dir8.E, rig.P.Facing);
            Assert.Equal((int)StanceMode.Stance, rig.Log.Single(SimEventType.StanceEntered).A);
            AssertResets(rig, CommandResetReason.StanceEnter);                    // 커맨드 초기화 1회
            Assert.True(rig.LastInput.StanceChanged);
            Assert.True(rig.LastInput.InStance);
        }

        [Fact]
        public void S01_T_F03__StanceSnapsDiagonalToLaterPressedUp()
        {
            PlayerRig rig = NorthEast(rightLater: false);
            rig.Step(Press(LogicalKey.Stance));
            Assert.Equal(Dir8.N, rig.P.StanceLockedFacing);
            Assert.Equal(Dir8.N, rig.P.Facing);
        }

        [Fact]
        public void S01_T_F04__StanceUpMovesUpKeepsFacingAndFeedsLeftSide()
        {
            PlayerRig rig = StanceLockedEast();
            FixVec2 start = rig.P.Pos;                                            // (12, 7)에서 Right 1틱만큼 옮긴 자리
            Assert.Equal(7L * OneRaw, start.Y.Raw);

            rig.Step(Press(LogicalKey.Up));
            Assert.True(rig.LastInput.HasDirection);
            Assert.Equal(CmdDir.Left, rig.LastInput.Direction);                  // 판정기 입력 = 왼옆
            for (int t = 1; t < 60; t++)
            {
                rig.Step();
            }
            Assert.Equal(Dir8.E, rig.P.Facing);
            Assert.Equal(StanceMode.Stance, rig.P.Stance);
            Assert.Equal(0, rig.Log.Count(SimEventType.FacingChanged));
            Assert.Equal(start.X.Raw, rig.P.Pos.X.Raw);
            Assert.Equal(304_226_850L, StanceStepRaw);
            Assert.Equal(start.Y.Raw - 60L * StanceStepRaw, rig.P.Pos.Y.Raw);    // 위로(−y) 자세 속도 1초
        }

        [Fact]
        public void S01_T_F05__StanceFiresAlongLockedAxisEvenWhileMovingDiagonally()
        {
            PlayerRig rig = StanceLockedEast();
            rig.Step(Press(LogicalKey.Up), Press(LogicalKey.Right, 5));
            rig.Step(Press(LogicalKey.Fire));
            Assert.True(rig.LastInput.Accepted);
            Assert.Equal(Dir8.E, rig.World.PlayerFireDirection(0));             // 대각 사격 없음
            Assert.Equal(Dir8.E, rig.P.Facing);
            Assert.Equal(0, rig.Log.Count(SimEventType.FacingChanged));
        }

        [Fact]
        public void S01_T_F06__ReverseFromNormalLocksOppositeFacing()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Right));
            rig.Step(Release(LogicalKey.Right));
            rig.Log.Clear();
            rig.Step(Press(LogicalKey.Reverse));
            Assert.Equal(StanceMode.Reverse, rig.P.Stance);
            Assert.Equal(Dir8.W, rig.P.StanceLockedFacing);
            Assert.Equal(Dir8.W, rig.P.Facing);
            Assert.Equal((int)StanceMode.Reverse, rig.Log.Single(SimEventType.StanceEntered).A);
            AssertResets(rig, CommandResetReason.StanceEnter);
        }

        [Fact]
        public void S01_T_F07__ReverseReleaseWithMoveKeyFacesMoveDirection()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Right));
            rig.Step(Press(LogicalKey.Reverse));
            Assert.Equal(Dir8.W, rig.P.StanceLockedFacing);
            rig.Log.Clear();
            rig.Step(Release(LogicalKey.Reverse));
            Assert.Equal(StanceMode.Normal, rig.P.Stance);
            Assert.Equal(Dir8.E, rig.P.Facing);
            rig.Log.Single(SimEventType.StanceExited);
            AssertResets(rig, CommandResetReason.StanceExit);
        }

        [Fact]
        public void S01_T_F08__ReverseReleaseWithoutMoveKeyKeepsFlippedFacing()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Right));
            rig.Step(Release(LogicalKey.Right));
            rig.Step(Press(LogicalKey.Reverse));
            rig.Log.Clear();
            rig.Step(Release(LogicalKey.Reverse));
            Assert.Equal(StanceMode.Normal, rig.P.Stance);
            Assert.Equal(Dir8.W, rig.P.Facing);
            Assert.Equal(0, rig.Log.Count(SimEventType.FacingChanged));
        }

        [Fact]
        public void S01_T_F09__ReverseInStanceFlipsLockedFacing()
        {
            PlayerRig rig = StanceLockedEast();
            rig.Step(Press(LogicalKey.Reverse));
            Assert.Equal(StanceMode.Reverse, rig.P.Stance);
            Assert.Equal(Dir8.W, rig.P.StanceLockedFacing);
            Assert.Equal(Dir8.W, rig.P.Facing);
            SimEvent flipped = rig.Log.Single(SimEventType.ReverseFlipped);
            Assert.Equal((int)StanceMode.Reverse, flipped.A);
            Assert.Equal(0, rig.Log.Count(SimEventType.StanceEntered));
            AssertResets(rig, CommandResetReason.Reverse);
        }

        [Fact]
        public void S01_T_F10__ReverseReleaseWhileHoldingStanceStaysLockedFlipped()
        {
            PlayerRig rig = StanceLockedEast();
            rig.Step(Press(LogicalKey.Reverse));
            rig.Log.Clear();
            rig.Step(Release(LogicalKey.Reverse));
            Assert.Equal(StanceMode.Stance, rig.P.Stance);
            Assert.Equal(Dir8.W, rig.P.StanceLockedFacing);
            Assert.Equal(Dir8.W, rig.P.Facing);
            Assert.Equal((int)StanceMode.Stance, rig.Log.Single(SimEventType.ReverseFlipped).A);
            AssertResets(rig, CommandResetReason.Reverse);                         // [임시] U5
        }

        [Fact]
        public void S01_T_F11__StanceReleaseWithUpHeldFacesNorth()
        {
            PlayerRig rig = StanceLockedEast();
            rig.Step(Press(LogicalKey.Up));
            Assert.Equal(Dir8.E, rig.P.Facing);
            rig.Log.Clear();
            rig.Step(Release(LogicalKey.Stance));
            Assert.Equal(StanceMode.Normal, rig.P.Stance);
            Assert.Equal(Dir8.N, rig.P.Facing);
            rig.Log.Single(SimEventType.StanceExited);
            AssertResets(rig, CommandResetReason.StanceExit);
            Assert.True(rig.LastInput.StanceChanged);
            Assert.False(rig.LastInput.InStance);
        }

        // T-F12~T-F17 의 바탕: 자세 밖, Up+Right(시야 NE). Right 뗌은 틱 100 오프셋 0(입력 시각 25,600)
        private static PlayerRig WindowBase(bool rightLater = true)
        {
            PlayerRig rig = NorthEast(rightLater);
            rig.RunTo(100);
            rig.Step(Release(LogicalKey.Right, 0));
            Assert.Equal(25_600L, rig.LastInput.Time);
            Assert.True(rig.P.Pending.Active);
            Assert.Equal(25_600L + 1_229L, rig.P.Pending.ExpireTime);
            Assert.Equal(Dir8.NE, rig.P.Facing);
            return rig;
        }

        [Fact]
        public void S01_T_F12__SecondReleaseWithinWindowKeepsDiagonal()
        {
            PlayerRig rig = WindowBase();
            uint tick = (uint)((25_600 + 1_000) / 256);                           // 26,600 = 틱 103 오프셋 232
            byte offset = (byte)((25_600 + 1_000) % 256);
            rig.RunTo(tick);
            rig.Step(Release(LogicalKey.Up, offset));
            Assert.Equal(26_600L, rig.LastInput.Time);
            Assert.Equal(Dir8.NE, rig.P.Facing);
            Assert.False(rig.P.Pending.Active);
            rig.RunTo(200);
            Assert.Equal(Dir8.NE, rig.P.Facing);
        }

        [Fact]
        public void S01_T_F13__WindowExpiresAtTick105WhileUpHeld()
        {
            PlayerRig rig = WindowBase();
            long xAtRelease = rig.P.Pos.X.Raw;
            for (uint t = 101; t <= 104; t++)
            {
                rig.Step();
                Assert.Equal(Dir8.NE, rig.P.Facing);                              // 틱 104까지는 NE
            }
            Assert.Equal(xAtRelease, rig.P.Pos.X.Raw);                            // 창 동안 이동은 이미 N
            Assert.Equal(105U, rig.Tick);
            Assert.True(25_600L + 1_229L < 105L * 256L);
            rig.Log.Clear();
            rig.Step();                                                           // 틱 105의 3단계
            Assert.Equal(Dir8.N, rig.P.Facing);
            Assert.False(rig.P.Pending.Active);
            SimEvent changed = rig.Log.Single(SimEventType.FacingChanged);
            Assert.Equal(105U, changed.Tick);
            Assert.Equal((int)Dir8.N, changed.A);
        }

        [Fact]
        public void S01_T_F14__SecondReleaseExactlyAtWindowEndKeepsDiagonal()
        {
            PlayerRig rig = WindowBase();
            long at = 25_600L + 1_229L;                                           // 26,829 = 틱 104 오프셋 205
            rig.RunTo((uint)(at / 256));
            rig.Step(Release(LogicalKey.Up, (byte)(at % 256)));
            Assert.Equal(at, rig.LastInput.Time);
            Assert.Equal(Dir8.NE, rig.P.Facing);
            rig.RunTo(150);
            Assert.Equal(Dir8.NE, rig.P.Facing);
        }

        [Theory]
        [InlineData(25_600L + 1_230L)]          // 같은 틱(104) 안에서 창 끝을 넘은 뒤의 뗌: 이벤트가 먼저 만료시킨다
        [InlineData(105L * 256L + 10L)]         // 틱 경계를 넘었다(틱 105의 2단계 이벤트가 3단계보다 먼저)
        [InlineData(106L * 256L + 3L)]          // 틱 105의 3단계가 이미 만료시켰다
        public void S01_T_F15__SecondReleaseAfterWindowFacesNorth(long at)
        {
            PlayerRig rig = WindowBase();
            rig.RunTo((uint)(at / 256));
            rig.Step(Release(LogicalKey.Up, (byte)(at % 256)));
            Assert.Equal(at, rig.LastInput.Time);
            Assert.Equal(Dir8.N, rig.P.Facing);
            Assert.False(rig.P.Pending.Active);
            rig.RunTo(200);
            Assert.Equal(Dir8.N, rig.P.Facing);
        }

        [Fact]
        public void S01_T_F16__FireDuringWindowGoesDiagonal()
        {
            PlayerRig rig = WindowBase();
            rig.Step(Press(LogicalKey.Fire, 0));
            Assert.True(rig.P.Pending.Active);                                    // J 는 창을 닫지 않는다
            Assert.Equal(Dir8.NE, rig.World.PlayerFireDirection(0));
            Assert.True(PlayerRulesTests.MoveDirOf(rig) == Dir8.N);              // 이동 N, 시야 NE
        }

        [Theory]
        [InlineData(true, Dir8.E)]
        [InlineData(false, Dir8.N)]
        public void S01_T_F17__StanceDuringWindowCancelsItAndSnaps(bool rightLater, Dir8 expectedLocked)
        {
            PlayerRig rig = WindowBase(rightLater);
            rig.Log.Clear();
            rig.Step(Press(LogicalKey.Stance, 0));
            Assert.False(rig.P.Pending.Active);
            Assert.Equal(StanceMode.Stance, rig.P.Stance);
            Assert.Equal(expectedLocked, rig.P.StanceLockedFacing);              // SnapCardinal(NE)
            AssertResets(rig, CommandResetReason.StanceEnter);

            // 창이 취소되었으므로 자세를 풀면 이동 키(Up) 방향으로 돌아간다(대각 유지가 되살아나지 않는다)
            rig.Step(Release(LogicalKey.Stance));
            Assert.Equal(Dir8.N, rig.P.Facing);
            Assert.False(rig.P.Pending.Active);
        }

        [Fact]
        public void S01_T_F18__LockedSouthLeftIsRightSideAndRightIsLeftSide()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Stance));                                   // 시작 시야 S → 잠긴 S
            Assert.Equal(Dir8.S, rig.P.StanceLockedFacing);
            rig.Step(Press(LogicalKey.Left));                                     // A
            Assert.True(rig.LastInput.HasDirection);
            Assert.Equal(CmdDir.Right, rig.LastInput.Direction);                 // 오른옆
            rig.Step(Release(LogicalKey.Left));
            Assert.False(rig.LastInput.HasDirection);                             // 뗌은 판정기에 가지 않는다
            rig.Step(Press(LogicalKey.Right));                                    // D
            Assert.Equal(CmdDir.Left, rig.LastInput.Direction);                  // 왼옆
            Assert.Equal(Dir8.S, rig.P.Facing);
        }

        [Fact]
        public void S01_T_F19__PauseResetsCommandOnce()
        {
            PlayerRig rig = StanceLockedEast();
            rig.Step(Press(LogicalKey.Pause));
            AssertResets(rig, CommandResetReason.Pause);
            Assert.True(rig.LastInput.HasReset);
            Assert.Equal(CommandResetReason.Pause, rig.LastInput.ResetReason);
            Assert.Equal(StanceMode.Stance, rig.P.Stance);                       // 자세·시야는 그대로
            Assert.Equal(Dir8.E, rig.P.Facing);
            rig.Step(Release(LogicalKey.Pause));
            Assert.Single(rig.Log.Of(SimEventType.CommandReset));                // 뗌은 초기화하지 않는다
            // 틱 정지는 세계 밖의 일이다(호출자가 Step 을 멈춘다, CONTRACTS 3.1). 코어는 초기화 신호만 낸다
        }
    }
}
