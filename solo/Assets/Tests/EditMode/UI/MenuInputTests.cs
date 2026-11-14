using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.UI
{
    /// <summary>메뉴 키 대응(08 요약서 1.2절, 00 문서 D-35·4.1절).</summary>
    public class MenuInputTests
    {
        static List<MenuKey> Step(MenuInput mi, float dt = 1f / 60f)
        {
            var keys = new List<MenuKey>();
            mi.Update(dt, keys);
            return keys;
        }

        static List<MenuKey> Press(FakeKeyReader r, MenuInput mi, Key k)
        {
            r.Down.Add(k);
            var keys = Step(mi);
            r.Down.Remove(k);
            Step(mi);
            return keys;
        }

        [Test]
        public void FixedKeys_MapToMenuKeys()
        {
            var r = new FakeKeyReader();
            var mi = new MenuInput(r);
            CollectionAssert.AreEqual(new[] { MenuKey.Back }, Press(r, mi, Key.Escape));
            CollectionAssert.AreEqual(new[] { MenuKey.Confirm }, Press(r, mi, Key.J));
            CollectionAssert.AreEqual(new[] { MenuKey.Confirm }, Press(r, mi, Key.Z));
            CollectionAssert.AreEqual(new[] { MenuKey.Cancel }, Press(r, mi, Key.K));
            CollectionAssert.AreEqual(new[] { MenuKey.Cancel }, Press(r, mi, Key.X));
            CollectionAssert.AreEqual(new[] { MenuKey.PrevTab }, Press(r, mi, Key.Q));
            CollectionAssert.AreEqual(new[] { MenuKey.NextTab }, Press(r, mi, Key.E));
            CollectionAssert.AreEqual(new[] { MenuKey.Up }, Press(r, mi, Key.UpArrow));
            CollectionAssert.AreEqual(new[] { MenuKey.Left }, Press(r, mi, Key.LeftArrow));
            // 기본 이동 키 배치 W/A/S/D(D-35: 방향키와 이동 키 둘 다)
            CollectionAssert.AreEqual(new[] { MenuKey.Up }, Press(r, mi, Key.W));
            CollectionAssert.AreEqual(new[] { MenuKey.Left }, Press(r, mi, Key.A));
            CollectionAssert.AreEqual(new[] { MenuKey.Down }, Press(r, mi, Key.S));
            CollectionAssert.AreEqual(new[] { MenuKey.Right }, Press(r, mi, Key.D));
        }

        [Test]
        public void Esc_IsBackOnly_AndPressIsEdgeTriggered()
        {
            var r = new FakeKeyReader();
            var mi = new MenuInput(r);
            r.Down.Add(Key.Escape);
            CollectionAssert.AreEqual(new[] { MenuKey.Back }, Step(mi));
            // 누르고 있어도 다시 내지 않는다(Esc는 반복하지 않는다)
            for (int i = 0; i < 120; i++) Assert.IsEmpty(Step(mi));
            r.Down.Clear();
            Assert.IsEmpty(Step(mi));
            r.Down.Add(Key.Escape);
            CollectionAssert.AreEqual(new[] { MenuKey.Back }, Step(mi));
        }

        [Test]
        public void SettingsLayout_MovementKeysAndRebindPauseIgnored()
        {
            var r = new FakeKeyReader();
            var mi = new MenuInput(r);
            var s = new SettingsData();
            // 논리 키 순서: Up, Left, Down, Right, Fire, Skill, Stance, Reverse, Pause
            s.KeyPrimary.AddRange(new[] { (int)Key.I, (int)Key.H, (int)Key.N, (int)Key.L, (int)Key.J, (int)Key.K, (int)Key.Space, (int)Key.LeftShift, (int)Key.P });
            s.KeySecondary.AddRange(new[] { 0, 0, 0, 0, 0, 0, 0, 0, (int)Key.Escape });
            mi.ApplySettings(s);
            CollectionAssert.AreEqual(new[] { MenuKey.Up }, Press(r, mi, Key.I));
            CollectionAssert.AreEqual(new[] { MenuKey.Left }, Press(r, mi, Key.H));
            CollectionAssert.AreEqual(new[] { MenuKey.Down }, Press(r, mi, Key.N));
            CollectionAssert.AreEqual(new[] { MenuKey.Right }, Press(r, mi, Key.L));
            // 방향키는 언제나
            CollectionAssert.AreEqual(new[] { MenuKey.Down }, Press(r, mi, Key.DownArrow));
            // 설정에 이동 키가 있으면 W/A/S/D는 더하지 않는다
            Assert.IsEmpty(Press(r, mi, Key.W));
            // 재설정한 일시정지 키는 메뉴 길에서 잡지 않는다
            Assert.IsEmpty(Press(r, mi, Key.P));
            // Esc는 여전히 Back
            CollectionAssert.AreEqual(new[] { MenuKey.Back }, Press(r, mi, Key.Escape));
            // 설정을 비우면 기본으로
            mi.ApplySettings(null);
            CollectionAssert.AreEqual(new[] { MenuKey.Up }, Press(r, mi, Key.W));
        }

        [Test]
        public void KeyRepeat_DirectionAfterDelayThenInterval()
        {
            var r = new FakeKeyReader();
            var mi = new MenuInput(r) { RepeatDelay = 0.4f, RepeatInterval = 0.1f };
            r.Down.Add(Key.DownArrow);
            CollectionAssert.AreEqual(new[] { MenuKey.Down }, Step(mi, 0.05f));
            int count = 0;
            // 0.35초 더(누른 지 0.40초 직전): 아직 반복 없음
            for (int i = 0; i < 7; i++) count += Step(mi, 0.049f).Count;
            Assert.AreEqual(0, count);
            // 0.4초가 넘으면 첫 반복
            Assert.AreEqual(1, Step(mi, 0.06f).Count);
            // 그 뒤 0.1초마다
            count = 0;
            for (int i = 0; i < 10; i++) count += Step(mi, 0.05f).Count;
            Assert.AreEqual(5, count);
        }

        [Test]
        public void KeyRepeat_ConfirmDoesNotRepeat_LongFrameRepeatsOnce()
        {
            var r = new FakeKeyReader();
            var mi = new MenuInput(r);
            r.Down.Add(Key.J);
            Assert.AreEqual(1, Step(mi).Count);
            for (int i = 0; i < 100; i++) Assert.IsEmpty(Step(mi, 0.1f));
            r.Down.Clear();
            Step(mi);
            r.Down.Add(Key.RightArrow);
            Step(mi);
            Assert.AreEqual(1, Step(mi, 5f).Count, "a long frame yields at most one repeat");
        }

        [Test]
        public void TwoPhysicalKeys_OneLogicalPress()
        {
            var r = new FakeKeyReader();
            var mi = new MenuInput(r);
            r.Down.Add(Key.UpArrow);
            Assert.AreEqual(1, Step(mi).Count);
            r.Down.Add(Key.W);
            Assert.IsEmpty(Step(mi));
            r.Down.Remove(Key.UpArrow);
            Assert.IsEmpty(Step(mi), "still held through W");
        }

        [Test]
        public void SwallowHeld_NeedsReleaseBeforeNextPress()
        {
            var r = new FakeKeyReader();
            var mi = new MenuInput(r);
            r.Down.Add(Key.J);
            mi.SwallowHeld();
            Assert.IsEmpty(Step(mi));
            r.Down.Clear();
            Step(mi);
            r.Down.Add(Key.J);
            Assert.AreEqual(1, Step(mi).Count);
        }

        [Test]
        public void AnyKeyPressed_ForInputMode()
        {
            var r = new FakeKeyReader();
            var mi = new MenuInput(r);
            Step(mi);
            Assert.IsFalse(mi.AnyKeyPressed);
            r.AnyPressed = true;
            Step(mi);
            Assert.IsTrue(mi.AnyKeyPressed);
            r.AnyPressed = false;
            r.Down.Add(Key.J);
            Step(mi);
            Assert.IsTrue(mi.AnyKeyPressed);
        }

        [Test]
        public void NoKeyboard_DoesNotThrow()
        {
            // 배치 모드처럼 Keyboard.current가 없을 수 있다. 기본 읽개는 null을 "아무것도 안 눌림"으로 본다.
            var reader = new UiKeyboardReader();
            var mi = new MenuInput(reader);
            var keys = new List<MenuKey>();
            Assert.DoesNotThrow(() => mi.Update(0.016f, keys));
            if (Keyboard.current == null)
            {
                Assert.IsEmpty(keys);
                Assert.IsFalse(reader.IsDown(Key.J));
                Assert.IsFalse(reader.AnyKeyPressedThisFrame);
            }
            Assert.IsFalse(reader.IsDown(Key.None));
            Assert.DoesNotThrow(() => { var d = reader.MouseDelta; });
        }
    }
}
