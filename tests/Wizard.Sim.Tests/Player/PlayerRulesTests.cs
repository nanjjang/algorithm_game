using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Input;
using Wizard.Sim.Player;
using Xunit;

namespace Wizard.Sim.Tests.Player
{
    // spec 01 5.2·5.5 의 상태 없는 규칙: 상대 방향 표, SnapCardinal, 키 → 방향, 지금의 이동 방향
    public sealed class PlayerRulesTests
    {
        internal static Dir8 MoveDirOf(PlayerRig rig)
        {
            KeyState keys = rig.P.Keys;
            Assert.True(PlayerRules.TryMoveDir(in keys, out Dir8 d));
            return d;
        }

        // spec 01 5.5 의 표(잠긴 시야 × W/A/S/D) 16칸 그대로
        [Theory]
        [InlineData(Dir8.N, Dir8.N, CmdDir.Forward)]
        [InlineData(Dir8.N, Dir8.W, CmdDir.Left)]
        [InlineData(Dir8.N, Dir8.S, CmdDir.Back)]
        [InlineData(Dir8.N, Dir8.E, CmdDir.Right)]
        [InlineData(Dir8.W, Dir8.N, CmdDir.Right)]
        [InlineData(Dir8.W, Dir8.W, CmdDir.Forward)]
        [InlineData(Dir8.W, Dir8.S, CmdDir.Left)]
        [InlineData(Dir8.W, Dir8.E, CmdDir.Back)]
        [InlineData(Dir8.S, Dir8.N, CmdDir.Back)]
        [InlineData(Dir8.S, Dir8.W, CmdDir.Right)]
        [InlineData(Dir8.S, Dir8.S, CmdDir.Forward)]
        [InlineData(Dir8.S, Dir8.E, CmdDir.Left)]
        [InlineData(Dir8.E, Dir8.N, CmdDir.Left)]
        [InlineData(Dir8.E, Dir8.W, CmdDir.Back)]
        [InlineData(Dir8.E, Dir8.S, CmdDir.Right)]
        [InlineData(Dir8.E, Dir8.E, CmdDir.Forward)]
        public void RelativeDirectionTable(Dir8 locked, Dir8 pressed, CmdDir expected)
        {
            Assert.Equal(expected, PlayerRules.Relative(locked, pressed));
        }

        [Fact]
        public void RelativeOfDiagonalIsAnInvariantViolation()
        {
            Assert.Throws<SimInvariantException>(() => PlayerRules.Relative(Dir8.NE, Dir8.N));
        }

        [Fact]
        public void KeysMapToCardinals()
        {
            Assert.Equal(Dir8.N, PlayerRules.KeyDir(LogicalKey.Up));
            Assert.Equal(Dir8.W, PlayerRules.KeyDir(LogicalKey.Left));
            Assert.Equal(Dir8.S, PlayerRules.KeyDir(LogicalKey.Down));
            Assert.Equal(Dir8.E, PlayerRules.KeyDir(LogicalKey.Right));
            Assert.Throws<SimInvariantException>(() => PlayerRules.KeyDir(LogicalKey.Fire));
            Assert.Equal(Dir8.SW, PlayerRules.Flip(Dir8.NE));
        }

        private static KeyState Pressed(params (LogicalKey key, long time)[] presses)
        {
            var k = new KeyState();
            uint seq = 0;
            foreach ((LogicalKey key, long time) in presses)
            {
                k.Apply(new InputEvent((uint)(time / 256), (byte)(time % 256), key, true, ++seq));
            }
            return k;
        }

        [Fact]
        public void SnapCardinal_AxisFacingStays()
        {
            KeyState k = default;
            foreach (Dir8 d in new[] { Dir8.E, Dir8.N, Dir8.W, Dir8.S })
            {
                Assert.Equal(d, PlayerRules.SnapCardinal(d, Dir8.E, in k));
            }
        }

        [Fact]
        public void SnapCardinal_DiagonalPrefersLastPressedComponent()
        {
            KeyState k = Pressed((LogicalKey.Up, 10), (LogicalKey.Left, 20));
            Assert.Equal(Dir8.W, PlayerRules.SnapCardinal(Dir8.NW, Dir8.W, in k));
            Assert.Equal(Dir8.N, PlayerRules.SnapCardinal(Dir8.NW, Dir8.N, in k));
            // 마지막에 누른 축 방향이 이 대각의 성분이 아니면 두 성분 키 중 누름 시각이 늦은 쪽 [임시] U4
            Assert.Equal(Dir8.W, PlayerRules.SnapCardinal(Dir8.NW, Dir8.E, in k));
            KeyState k2 = Pressed((LogicalKey.Left, 10), (LogicalKey.Up, 20));
            Assert.Equal(Dir8.N, PlayerRules.SnapCardinal(Dir8.NW, Dir8.S, in k2));
        }

        [Fact]
        public void TryMoveDir_LaterPressedOppositeWins()
        {
            KeyState k = Pressed((LogicalKey.Left, 5), (LogicalKey.Right, 9), (LogicalKey.Down, 12));
            Assert.True(PlayerRules.TryMoveDir(in k, out Dir8 d));
            Assert.Equal(Dir8.SE, d);
            KeyState none = default;
            Assert.False(PlayerRules.TryMoveDir(in none, out _));
        }

        [Fact]
        public void AxisComponents()
        {
            Assert.True(PlayerRules.IsAxisComponent(Dir8.NE, Dir8.N));
            Assert.True(PlayerRules.IsAxisComponent(Dir8.NE, Dir8.E));
            Assert.False(PlayerRules.IsAxisComponent(Dir8.NE, Dir8.S));
            Assert.False(PlayerRules.IsAxisComponent(Dir8.N, Dir8.N));
            Assert.Equal(1_229L, PlayerRules.DiagHoldWindowUnits);
            Assert.Equal(1_229L, SimTime.InputUnitsFromDecimalSeconds(8, 2));    // 0.08초(spec 01 2.1, T-C01)
        }
    }
}
