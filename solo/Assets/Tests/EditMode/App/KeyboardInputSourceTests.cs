using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Wizard.Solo.App;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Tests.App
{
    /// <summary>
    /// 전투 입력원(02 요약서 3.1·3.2절, 00 문서 4.1절): 물리 키 자리, 논리 키 하나에 물리 키 둘이면 OR,
    /// 논리 상태가 바뀔 때만 이벤트, 같은 Poll은 같은 시각, Esc 없음, Resync의 삼킴, ReleaseAll.
    /// </summary>
    public class KeyboardInputSourceTests
    {
        FakeKeyReader _keys;
        KeyboardInputSource _src;
        List<InputEvent> _ev;

        [SetUp]
        public void SetUp()
        {
            _keys = new FakeKeyReader();
            _src = new KeyboardInputSource(KeyBindings.CreateDefault(), _keys);
            _ev = new List<InputEvent>();
        }

        List<InputEvent> Poll(long u)
        {
            _ev.Clear();
            _src.Poll(u, _ev);
            return _ev;
        }

        [Test]
        public void TwoPhysicalKeysOneLogical_OnePressOneRelease()
        {
            int downs = 0, ups = 0;
            void Count(List<InputEvent> l)
            {
                foreach (var e in l)
                {
                    Assert.AreEqual(LogicalKey.Up, e.Key);
                    if (e.Down) downs++; else ups++;
                }
            }
            _keys.Down(Key.W); Count(Poll(0));
            _keys.Down(Key.UpArrow); Count(Poll(256));
            _keys.Up(Key.W); Count(Poll(512));
            Assert.IsTrue(_src.IsDown(LogicalKey.Up), "↑ still held");
            _keys.Up(Key.UpArrow); Count(Poll(768));
            Assert.AreEqual(1, downs);
            Assert.AreEqual(1, ups);
            Assert.IsFalse(_src.IsDown(LogicalKey.Up));
        }

        [Test]
        public void NoChange_NoEvents()
        {
            _keys.Down(Key.D);
            Assert.AreEqual(1, Poll(0).Count);
            Assert.AreEqual(0, Poll(256).Count);
            Assert.AreEqual(0, Poll(512).Count);
        }

        [Test]
        public void SameFrameKeys_SameTime_CategoryOrder()
        {
            _keys.Down(Key.J);
            _keys.Down(Key.W);
            _keys.Down(Key.LeftShift);
            _keys.Down(Key.K);
            var l = Poll(1234);
            Assert.AreEqual(4, l.Count);
            foreach (var e in l) { Assert.AreEqual(1234L, e.TimeU); Assert.IsTrue(e.Down); }
            // 02 요약서 3.2절: 자세·반전 0 → 방향 1 → J 2 → K 3
            Assert.AreEqual(LogicalKey.Stance, l[0].Key);
            Assert.AreEqual(LogicalKey.Up, l[1].Key);
            Assert.AreEqual(LogicalKey.Fire, l[2].Key);
            Assert.AreEqual(LogicalKey.Skill, l[3].Key);
        }

        [Test]
        public void DefaultLayout_AllNineKeys()
        {
            var map = new Dictionary<Key, LogicalKey>
            {
                { Key.W, LogicalKey.Up }, { Key.A, LogicalKey.Left }, { Key.S, LogicalKey.Down }, { Key.D, LogicalKey.Right },
                { Key.J, LogicalKey.Fire }, { Key.K, LogicalKey.Skill }, { Key.LeftShift, LogicalKey.Stance }, { Key.Space, LogicalKey.Reverse },
                { Key.UpArrow, LogicalKey.Up }, { Key.LeftArrow, LogicalKey.Left }, { Key.DownArrow, LogicalKey.Down },
                { Key.RightArrow, LogicalKey.Right }, { Key.Z, LogicalKey.Fire }, { Key.X, LogicalKey.Skill },
                { Key.C, LogicalKey.Stance }, { Key.V, LogicalKey.Reverse }
            };
            long u = 0;
            foreach (var kv in map)
            {
                _keys.Down(kv.Key);
                var l = Poll(u += 256);
                Assert.AreEqual(1, l.Count, kv.Key.ToString());
                Assert.AreEqual(kv.Value, l[0].Key, kv.Key.ToString());
                _keys.Up(kv.Key);
                Assert.AreEqual(1, Poll(u += 256).Count);
            }
        }

        [Test]
        public void Escape_NeverEmitsPause_EvenIfBound()
        {
            _keys.Down(Key.Escape);
            Assert.AreEqual(0, Poll(0).Count);

            var s = new FakeSettingsStore();
            SettingsOptions.Set(s.Data, OptionKeys.KeyLayout, OptionValues.Custom);
            for (int i = 0; i < SimConst.LogicalKeyCount; i++) { s.Data.KeyPrimary.Add(0); s.Data.KeySecondary.Add(0); }
            s.Data.KeyPrimary[(int)LogicalKey.Pause] = (int)Key.Escape;
            s.Data.KeySecondary[(int)LogicalKey.Pause] = (int)Key.P;
            var src = new KeyboardInputSource(s, _keys);
            var ev = new List<InputEvent>();
            src.Poll(0, ev);
            Assert.AreEqual(0, ev.Count, "Esc bound to Pause is ignored");
            _keys.Down(Key.P);
            src.Poll(256, ev);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(LogicalKey.Pause, ev[0].Key);
            src.Dispose();
        }

        [Test]
        public void Resync_ReleasedDuringPauseIsUp_NewJIsSwallowed_RepressFires()
        {
            _keys.Down(Key.W);
            Poll(0);
            // 일시정지: Poll 없음. 그동안 W를 떼고 J를 누른다.
            _keys.Up(Key.W);
            _keys.Down(Key.J);
            _ev.Clear();
            _src.Resync(5000, _ev, swallowHeldActions: true);
            Assert.AreEqual(1, _ev.Count);
            Assert.AreEqual(LogicalKey.Up, _ev[0].Key);
            Assert.IsFalse(_ev[0].Down);
            Assert.AreEqual(5000L, _ev[0].TimeU);
            Assert.IsFalse(_src.IsDown(LogicalKey.Fire));

            Assert.AreEqual(0, Poll(5256).Count, "held J stays swallowed");
            _keys.Up(Key.J);
            Assert.AreEqual(0, Poll(5512).Count, "release of a swallowed key is silent");
            _keys.Down(Key.J);
            var l = Poll(5768);
            Assert.AreEqual(1, l.Count);
            Assert.AreEqual(LogicalKey.Fire, l[0].Key);
            Assert.IsTrue(l[0].Down);
        }

        [Test]
        public void Resync_NewStateKeyPressed_ActionHeldBefore_Kept()
        {
            _keys.Down(Key.J);
            Poll(0);
            _keys.Down(Key.D);
            _keys.Down(Key.Space);
            _ev.Clear();
            _src.Resync(256, _ev, swallowHeldActions: true);
            Assert.AreEqual(1, _ev.Count, "only the state key (Right) is pressed; Reverse swallowed; Fire unchanged");
            Assert.AreEqual(LogicalKey.Right, _ev[0].Key);
            Assert.IsTrue(_ev[0].Down);
            Assert.IsTrue(_src.IsDown(LogicalKey.Fire));
            Assert.IsFalse(_src.IsDown(LogicalKey.Reverse));
        }

        [Test]
        public void Resync_WithoutSwallow_PressesActions()
        {
            _keys.Down(Key.K);
            _ev.Clear();
            _src.Resync(0, _ev, swallowHeldActions: false);
            Assert.AreEqual(1, _ev.Count);
            Assert.AreEqual(LogicalKey.Skill, _ev[0].Key);
            Assert.IsTrue(_ev[0].Down);
        }

        [Test]
        public void ReleaseAll_EmitsUpForEveryHeldKey_ThenAllUp()
        {
            _keys.Down(Key.W);
            _keys.Down(Key.J);
            _keys.Down(Key.LeftShift);
            Poll(0);
            _ev.Clear();
            _src.ReleaseAll(300, _ev);
            Assert.AreEqual(3, _ev.Count);
            foreach (var e in _ev) { Assert.IsFalse(e.Down); Assert.AreEqual(300L, e.TimeU); }
            for (int i = 0; i < SimConst.LogicalKeyCount; i++) Assert.IsFalse(_src.IsDown((LogicalKey)i));
            _ev.Clear();
            _src.ReleaseAll(400, _ev);
            Assert.AreEqual(0, _ev.Count, "second ReleaseAll has nothing to release");
        }

        [Test]
        public void ReleaseAll_ThenResyncSwallow_StateKeysBackActionsSwallowed()
        {
            _keys.Down(Key.A);
            _keys.Down(Key.J);
            Poll(0);
            _ev.Clear();
            _src.ReleaseAll(256, _ev);
            _ev.Clear();
            _src.Resync(512, _ev, swallowHeldActions: true);
            Assert.AreEqual(1, _ev.Count);
            Assert.AreEqual(LogicalKey.Left, _ev[0].Key);
            Assert.IsTrue(_ev[0].Down);
            Assert.IsFalse(_src.IsDown(LogicalKey.Fire));
        }

        [Test]
        public void NoKeyboard_NothingPressed()
        {
            // 키보드가 없을 때 읽개는 모든 키에 false를 준다(배치 모드). 입력원은 이벤트 없이 돈다.
            Assert.AreEqual(0, Poll(0).Count);
            _ev.Clear();
            _src.Resync(0, _ev, true);
            _src.ReleaseAll(0, _ev);
            Assert.AreEqual(0, _ev.Count);
            Assert.IsFalse(new InputSystemKeyReader().IsPressed(Key.None));
        }

        [Test]
        public void SettingsChanged_ReloadsBindings()
        {
            var s = new FakeSettingsStore();
            var src = new KeyboardInputSource(s, _keys);
            Assert.AreEqual(1, s.ChangedSubscriberCount);
            var ev = new List<InputEvent>();
            _keys.Down(Key.L);
            src.Poll(0, ev);
            Assert.AreEqual(0, ev.Count);

            SettingsOptions.Set(s.Data, OptionKeys.KeyLayout, OptionValues.Custom);
            for (int i = 0; i < SimConst.LogicalKeyCount; i++)
            {
                s.Data.KeyPrimary.Add((int)KeyBindings.DefaultKey((LogicalKey)i));
                s.Data.KeySecondary.Add(0);
            }
            s.Data.KeyPrimary[(int)LogicalKey.Fire] = (int)Key.L;
            s.RaiseChanged();
            src.Poll(256, ev);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(LogicalKey.Fire, ev[0].Key);

            src.Dispose();
            Assert.AreEqual(0, s.ChangedSubscriberCount);
        }
    }
}
