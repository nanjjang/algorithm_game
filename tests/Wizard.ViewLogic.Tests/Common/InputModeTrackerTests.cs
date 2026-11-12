using System.Collections.Generic;
using Wizard.ViewLogic.Common;
using Xunit;

namespace Wizard.ViewLogic.Tests.Common
{
    // spec 13 1.3절 InputModeTracker: 마지막으로 쓴 장치를 따른다
    public sealed class InputModeTrackerTests
    {
        [Fact]
        public void FollowsTheLastUsedDevice()
        {
            var t = new InputModeTracker(InputMode.Mouse);
            var changes = new List<InputMode>();
            t.Changed += m => changes.Add(m);
            Assert.True(t.OnKeyAny());
            Assert.Equal(InputMode.Keyboard, t.Mode);
            Assert.False(t.OnKeyAny());
            Assert.True(t.OnMouseMove(0, -1));
            Assert.Equal(InputMode.Mouse, t.Mode);
            Assert.False(t.OnMouseMove(4, 4));
            Assert.Equal(new[] { InputMode.Keyboard, InputMode.Mouse }, changes);
        }

        [Fact]
        public void MouseMoveWithoutDeltaDoesNotSwitch()
        {
            var t = new InputModeTracker(InputMode.Keyboard);
            Assert.False(t.OnMouseMove(0, 0));
            Assert.Equal(InputMode.Keyboard, t.Mode);
        }

        [Fact]
        public void DefaultsToKeyboard()
        {
            Assert.Equal(InputMode.Keyboard, new InputModeTracker().Mode);
        }
    }
}
