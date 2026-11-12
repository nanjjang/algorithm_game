#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wizard.Client;
using Wizard.Sim.Input;

namespace Wizard.Unity.Input.Tests
{
    // M1-UE-01 수락 조건 1·2(spec 01 4.2, T-28, CONTRACTS 6.5). Input System 테스트 고정 장치로 이벤트를 주입한다.
    // 실제 한글 입력기는 사람 확인 항목이다. 여기서는 입력기가 켜졌을 때 Input System 이 함께 보내는 글자 이벤트(TextEvent)와
    // 조합 이벤트(IMECompositionEvent)를 키 상태 이벤트 사이에 섞어 넣고, 논리 키가 물리 자리대로 나오는지 본다.
    // 한 번의 Update 에 키 여러 개를 넣을 때는 키보드 전체 상태 이벤트(KeyboardState)를 쓴다. 키보드 백엔드가 보내는 꼴이고,
    // 고정 장치의 Press(queueEventOnly) 델타 이벤트는 바이트 단위라서 아직 적용되지 않은 옆 키의 옛 값을 같이 싣기 때문이다.
    public sealed class UnityInputSourceTests : InputTestFixture
    {
        private const double Tolerance = 1e-9;

        private Keyboard _keyboard = null!;
        private UnityInputSource _source = null!;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            currentTime = 1.0;
            _source = new UnityInputSource(KeyBindings.Default, new InputSystemClock());
            _source.Enable();
        }

        public override void TearDown()
        {
            _source.Dispose();
            base.TearDown();
        }

        [TestCase(Key.W, LogicalKey.Up)]
        [TestCase(Key.A, LogicalKey.Left)]
        [TestCase(Key.S, LogicalKey.Down)]
        [TestCase(Key.D, LogicalKey.Right)]
        [TestCase(Key.J, LogicalKey.Fire)]
        [TestCase(Key.K, LogicalKey.Skill)]
        [TestCase(Key.LeftShift, LogicalKey.Stance)]
        [TestCase(Key.RightShift, LogicalKey.Stance)]
        [TestCase(Key.Space, LogicalKey.Reverse)]
        [TestCase(Key.Escape, LogicalKey.Pause)]
        public void DefaultBindings_PressAndRelease_EmitLogicalKeyWithEventTime(Key key, LogicalKey logical)
        {
            Press(_keyboard[key], time: 2.0);
            List<RawKeyEvent> down = DrainAll();
            Assert.That(down.Count, Is.EqualTo(1));
            AssertEvent(down[0], logical, true, 2.0);
            Assert.That(_source.CurrentKeys, Is.EqualTo(Mask(logical)));

            Release(_keyboard[key], time: 2.25);
            List<RawKeyEvent> up = DrainAll();
            Assert.That(up.Count, Is.EqualTo(1));
            AssertEvent(up[0], logical, false, 2.25);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));
        }

        [Test]
        public void ImeOn_TextAndCompositionEventsMixedIn_BattleKeysStillMapByPosition()
        {
            // 한글 두벌식에서 W A S D J K 자리의 글자(ㅈ ㅁ ㄴ ㅇ ㅓ ㅏ). Shift 는 글자가 없고 Space 는 공백
            Key[] keys = { Key.W, Key.A, Key.S, Key.D, Key.J, Key.K, Key.LeftShift, Key.Space };
            LogicalKey[] expected =
            {
                LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right,
                LogicalKey.Fire, LogicalKey.Skill, LogicalKey.Stance, LogicalKey.Reverse,
            };
            char[] text = { (char)0x3148, (char)0x3141, (char)0x3134, (char)0x3147, (char)0x3153, (char)0x314F, '\0', ' ' };

            _keyboard.SetIMEEnabled(true);
            double t = 3.0;
            for (int i = 0; i < keys.Length; i++)
            {
                if (text[i] != '\0')
                {
                    var composition = IMECompositionEvent.Create(_keyboard.deviceId, new string(text[i], 1), t);
                    InputSystem.QueueEvent(ref composition);
                    InputSystem.QueueTextEvent(_keyboard, text[i], t);
                }
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(true, Prefix(keys, i + 1)), t);
                t += 0.01;
            }
            InputSystem.Update();

            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(keys.Length));
            for (int i = 0; i < keys.Length; i++)
            {
                AssertEvent(events[i], expected[i], true, 3.0 + 0.01 * i);
            }

            // 떼는 쪽도 같다
            t = 4.0;
            for (int i = 0; i < keys.Length; i++)
            {
                if (text[i] != '\0')
                {
                    var composition = IMECompositionEvent.Create(_keyboard.deviceId, string.Empty, t);
                    InputSystem.QueueEvent(ref composition);
                }
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(true, Suffix(keys, i + 1)), t);
                t += 0.01;
            }
            InputSystem.Update();
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(keys.Length));
            for (int i = 0; i < keys.Length; i++)
            {
                AssertEvent(events[i], expected[i], false, 4.0 + 0.01 * i);
            }
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));
        }

        [Test]
        public void TextAndCompositionEventsAlone_EmitNothing()
        {
            _keyboard.SetIMEEnabled(true);
            var composition = IMECompositionEvent.Create(_keyboard.deviceId, new string((char)0x3148, 1), 5.0);
            InputSystem.QueueEvent(ref composition);
            InputSystem.QueueTextEvent(_keyboard, (char)0x3148, 5.0);
            InputSystem.Update();

            Assert.That(DrainAll(), Is.Empty);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));
        }

        [Test]
        public void KeyRepeat_SameStateAgain_EmitsOnlyOnChange()
        {
            // OS 의 키 반복과 같은 꼴: 같은 상태의 전체 상태 이벤트가 거듭 온다
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W), 6.0);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W), 6.03);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W), 6.06);
            InputSystem.Update();
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Up, true, 6.0);

            // W 를 누른 채 J: J 만 나온다
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.J), 6.1);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.J), 6.13);
            InputSystem.Update();
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Fire, true, 6.1);

            // 같은 키를 두 번 누름(델타 이벤트)도 한 번이다
            Press(_keyboard.wKey, time: 6.2);
            Press(_keyboard.wKey, time: 6.25);
            Assert.That(DrainAll(), Is.Empty);

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.J), 6.3);
            InputSystem.Update();
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Up, false, 6.3);
        }

        [Test]
        public void TwoPhysicalKeysOnOneLogicalKey_EmitOnlyWhenLogicalStateChanges()
        {
            Press(_keyboard.leftShiftKey, time: 7.0);
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Stance, true, 7.0);

            Press(_keyboard.rightShiftKey, time: 7.1);
            Assert.That(DrainAll(), Is.Empty);
            Release(_keyboard.leftShiftKey, time: 7.2);
            Assert.That(DrainAll(), Is.Empty);
            Assert.That(_source.CurrentKeys, Is.EqualTo(Mask(LogicalKey.Stance)));

            Release(_keyboard.rightShiftKey, time: 7.3);
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Stance, false, 7.3);
        }

        [Test]
        public void TwoKeyboards_ReleaseOnOneDoesNotReleaseTheOther()
        {
            Keyboard second = InputSystem.AddDevice<Keyboard>();
            Press(_keyboard.wKey, time: 8.0);
            Assert.That(DrainAll().Count, Is.EqualTo(1));
            Press(second.wKey, time: 8.1);
            Assert.That(DrainAll(), Is.Empty);
            Release(_keyboard.wKey, time: 8.2);
            Assert.That(DrainAll(), Is.Empty);
            Assert.That(_source.CurrentKeys, Is.EqualTo(Mask(LogicalKey.Up)));

            Release(second.wKey, time: 8.3);
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Up, false, 8.3);
        }

        [Test]
        public void UnboundKeyAndOtherDevices_EmitNothing()
        {
            Press(_keyboard.qKey, time: 9.0);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.leftButton, time: 9.1);
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Press(pad.buttonSouth, time: 9.2);

            Assert.That(DrainAll(), Is.Empty);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));
        }

        [Test]
        public void EventTime_IsTheEventsOwnTime_NotTheUpdateTime()
        {
            currentTime = 10.0;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W), 10.001);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.J), 10.009);
            currentTime = 10.016;                          // 프레임이 늦게 와도
            InputSystem.Update();

            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(2));
            AssertEvent(events[0], LogicalKey.Up, true, 10.001);
            AssertEvent(events[1], LogicalKey.Fire, true, 10.009);
        }

        [Test]
        public void Drain_SmallBuffer_ReturnsEventsInOrderAcrossCalls()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W), 11.0);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.A), 11.01);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.A, Key.S), 11.02);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.A, Key.S, Key.D), 11.03);
            InputSystem.Update();
            Assert.That(_source.PendingCount, Is.EqualTo(4));

            var buffer = new RawKeyEvent[3];
            Assert.That(_source.Drain(buffer), Is.EqualTo(3));
            AssertEvent(buffer[0], LogicalKey.Up, true, 11.0);
            AssertEvent(buffer[1], LogicalKey.Left, true, 11.01);
            AssertEvent(buffer[2], LogicalKey.Down, true, 11.02);
            Assert.That(_source.Drain(buffer), Is.EqualTo(1));
            AssertEvent(buffer[0], LogicalKey.Right, true, 11.03);
            Assert.That(_source.Drain(buffer), Is.EqualTo(0));
            Assert.That(_source.Drain(Span<RawKeyEvent>.Empty), Is.EqualTo(0));
        }

        [Test]
        public void ManyEventsBetweenDrains_AreAllKept()
        {
            double t = 12.0;
            for (int i = 0; i < 100; i++)
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.J), t);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(), t + 0.001);
                t += 0.002;
            }
            InputSystem.Update();
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(200));
            for (int i = 0; i < events.Count; i++)
            {
                Assert.That(events[i].Key, Is.EqualTo(LogicalKey.Fire));
                Assert.That(events[i].Down, Is.EqualTo(i % 2 == 0));
            }
        }

        [Test]
        public void SetBindings_WhileHeld_EmitsTheLogicalDifferenceAtThatTime()
        {
            Press(_keyboard.wKey, time: 13.0);
            DrainAll();
            currentTime = 13.5;
            _source.SetBindings(KeyBindings.Alternate);    // 방향키 배치: W 는 묶이지 않는다
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Up, false, 13.5);

            Press(_keyboard.upArrowKey, time: 13.6);
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Up, true, 13.6);

            Press(_keyboard.zKey, time: 13.7);
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Fire, true, 13.7);
            Assert.That(_source.Bindings, Is.SameAs(KeyBindings.Alternate));
        }

        [Test]
        public void Enable_WithKeyAlreadyHeld_EmitsDownAtEnableTime()
        {
            _source.Dispose();
            Press(_keyboard.spaceKey, time: 14.0);         // 듣는 입력원이 없을 때 눌림
            currentTime = 14.4;
            _source = new UnityInputSource(KeyBindings.Default, new InputSystemClock());
            _source.Enable();

            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Reverse, true, 14.4);
        }

        [Test]
        public void Disable_StopsListening_EnableEmitsKeysPressedWhileOff()
        {
            _source.Disable();
            Assert.That(_source.IsEnabled, Is.False);
            Press(_keyboard.kKey, time: 15.0);
            Assert.That(DrainAll(), Is.Empty);

            currentTime = 15.5;
            _source.Enable();
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Skill, true, 15.5);
        }

        [Test]
        public void Disable_KeyReleasedWhileOff_EnableReleasesItAtEnableTime()
        {
            Press(_keyboard.wKey, time: 1.0);
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Up, true, 1.0);

            _source.Disable();
            Release(_keyboard.wKey, time: 1.1);
            Assert.That(DrainAll(), Is.Empty);
            Assert.That(_source.CurrentKeys, Is.EqualTo(Mask(LogicalKey.Up)));   // 떨어져 있는 동안은 모른다

            currentTime = 1.2;
            _source.Enable();
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Up, false, 1.2);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));

            // 다시 누르면 평소대로 나온다(물리 키 기록이 남지 않았다)
            Press(_keyboard.wKey, time: 1.3);
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Up, true, 1.3);
        }

        [Test]
        public void Disable_KeyHeldThroughout_EnableEmitsNothing()
        {
            Press(_keyboard.jKey, time: 1.0);
            Assert.That(DrainAll().Count, Is.EqualTo(1));
            _source.Disable();
            currentTime = 1.5;
            _source.Enable();

            Assert.That(DrainAll(), Is.Empty);
            Assert.That(_source.CurrentKeys, Is.EqualTo(Mask(LogicalKey.Fire)));
        }

        [Test]
        public void Disable_KeyboardRemovedWhileOff_EnableReleasesAndForgetsIt()
        {
            Press(_keyboard.dKey, time: 1.0);
            Assert.That(DrainAll().Count, Is.EqualTo(1));

            _source.Disable();
            InputSystem.RemoveDevice(_keyboard);
            Assert.That(DrainAll(), Is.Empty);

            currentTime = 1.4;
            _source.Enable();
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Right, false, 1.4);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));

            // 빠진 키보드의 물리 키 기록이 남아 있지 않다: 새 키보드에서 같은 키를 누르고 떼면 둘 다 나온다
            Keyboard next = InputSystem.AddDevice<Keyboard>();
            Press(next.dKey, time: 1.5);
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Right, true, 1.5);
            Release(next.dKey, time: 1.6);
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Right, false, 1.6);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));
        }

        [Test]
        public void Disable_KeyboardDisabledWhileOff_EnableReleasesItsKeys()
        {
            Press(_keyboard.sKey, time: 1.0);
            Assert.That(DrainAll().Count, Is.EqualTo(1));

            _source.Disable();
            InputSystem.DisableDevice(_keyboard);
            currentTime = 1.3;
            _source.Enable();

            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Down, false, 1.3);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));
        }

        [Test]
        public void Disable_KeyMovedToOtherKeyboardWhileOff_EnableKeepsLogicalKeyWithoutEvents()
        {
            Keyboard second = InputSystem.AddDevice<Keyboard>();
            Press(_keyboard.aKey, time: 1.0);
            Assert.That(DrainAll().Count, Is.EqualTo(1));

            _source.Disable();
            Press(second.aKey, time: 1.1);
            Release(_keyboard.aKey, time: 1.2);
            currentTime = 1.3;
            _source.Enable();

            // 논리 키는 줄곧 눌려 있었다: 뗌·누름 쌍이 나오지 않는다
            Assert.That(DrainAll(), Is.Empty);
            Assert.That(_source.CurrentKeys, Is.EqualTo(Mask(LogicalKey.Left)));

            // 이제 그 키를 쥔 것은 두 번째 키보드다
            Release(second.aKey, time: 1.4);
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Left, false, 1.4);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));
        }

        [Test]
        public void DeviceRemoved_ReleasesItsKeysAtThatTime()
        {
            Press(_keyboard.dKey, time: 16.0);
            DrainAll();
            currentTime = 16.5;
            InputSystem.RemoveDevice(_keyboard);

            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Right, false, 16.5);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));
        }

        [Test]
        public void DeviceReset_ReleasesItsKeysAtThatTime()
        {
            Press(_keyboard.kKey, time: 17.0);
            DrainAll();
            currentTime = 17.5;
            InputSystem.ResetDevice(_keyboard);

            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(1));
            AssertEvent(events[0], LogicalKey.Skill, false, 17.5);
        }

        [Test]
        public void ReleaseAll_ForFocusLoss_ReleasesEveryHeldKey()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.J, Key.S), 18.0);
            InputSystem.Update();
            Assert.That(DrainAll().Count, Is.EqualTo(2));
            currentTime = 18.5;
            _source.ReleaseAll();

            // 물리 키 번호 순(J = 10, S = 19)
            List<RawKeyEvent> events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(2));
            AssertEvent(events[0], LogicalKey.Fire, false, 18.5);
            AssertEvent(events[1], LogicalKey.Down, false, 18.5);
            Assert.That(_source.CurrentKeys, Is.EqualTo(KeyMask.None));

            // 장치는 여전히 눌린 상태다. 다음 전체 상태 이벤트가 다시 알린다(초점을 되찾았을 때의 동기화)
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.J, Key.S), 19.0);
            InputSystem.Update();
            events = DrainAll();
            Assert.That(events.Count, Is.EqualTo(2));
            AssertEvent(events[0], LogicalKey.Fire, true, 19.0);
            AssertEvent(events[1], LogicalKey.Down, true, 19.0);
        }

        [Test]
        public void SetDuelPolling_IsRecorded()
        {
            Assert.That(_source.DuelPolling, Is.False);
            _source.SetDuelPolling(true);
            Assert.That(_source.DuelPolling, Is.True);
            _source.SetDuelPolling(false);
            Assert.That(_source.DuelPolling, Is.False);
        }

        [Test]
        public void EveryMappedKey_ExistsOnTheKeyboardWithThatKeyCode()
        {
            int seen = 0;
            foreach (PhysicalKey p in (PhysicalKey[])Enum.GetValues(typeof(PhysicalKey)))
            {
                if (p == PhysicalKey.None)
                {
                    continue;
                }
                Key key = PhysicalKeyMap.ToKey(p);
                Assert.That(_keyboard[key].keyCode, Is.EqualTo(key), p.ToString());
                seen++;
            }
            Assert.That(seen, Is.EqualTo(PhysicalKeyMap.MappedCount));
        }

        [Test]
        public void InputSystemClock_FollowsInputTime_IndependentOfTimeScale()
        {
            float saved = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                currentTime = 20.0;
                var clock = new InputSystemClock();
                Assert.That(clock.NowSeconds, Is.EqualTo(20.0).Within(Tolerance));
                currentTime = 20.5;
                Assert.That(clock.NowSeconds, Is.EqualTo(20.5).Within(Tolerance));
            }
            finally
            {
                Time.timeScale = saved;
            }
        }

        private List<RawKeyEvent> DrainAll()
        {
            var list = new List<RawKeyEvent>();
            var buffer = new RawKeyEvent[4];
            int n;
            do
            {
                n = _source.Drain(buffer);
                for (int i = 0; i < n; i++)
                {
                    list.Add(buffer[i]);
                }
            }
            while (n == buffer.Length);
            return list;
        }

        private static KeyMask Mask(LogicalKey key) => (KeyMask)(1 << (int)key);

        // 앞의 count 개(누른 키가 하나씩 늘어난다)
        private static Key[] Prefix(Key[] keys, int count)
        {
            var result = new Key[count];
            Array.Copy(keys, result, count);
            return result;
        }

        // 앞의 count 개를 뗀 나머지
        private static Key[] Suffix(Key[] keys, int count)
        {
            var result = new Key[keys.Length - count];
            Array.Copy(keys, count, result, 0, result.Length);
            return result;
        }

        private static void AssertEvent(RawKeyEvent e, LogicalKey key, bool down, double time)
        {
            Assert.That(e.Key, Is.EqualTo(key), "key");
            Assert.That(e.Down, Is.EqualTo(down), "down");
            Assert.That(e.TimeSeconds, Is.EqualTo(time).Within(Tolerance), "time");
        }
    }
}
