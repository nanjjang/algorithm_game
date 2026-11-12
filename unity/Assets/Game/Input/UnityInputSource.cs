using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using Wizard.Client;
using Wizard.Sim.Input;

namespace Wizard.Unity.Input
{
    // CONTRACTS 6.5 IInputSource 의 Unity 구현. spec 01 4.2, T-28, ARCHITECTURE 6.1.
    //
    // - Input System 의 저수준 이벤트(InputSystem.onEvent)에서 키보드의 상태 이벤트(StateEvent, DeltaStateEvent)만 읽는다.
    //   이벤트 안의 값으로 물리 키(Key = 자리)의 눌림을 읽고, 시각은 그 이벤트의 시각(InputEvent.time: OS 가 준 하드웨어 시각)을 쓴다.
    //   Update 에서 프레임 단위로 읽지 않는다(GetKeyDown 금지). 글자 이벤트(TextEvent)와 입력기 조합(IMECompositionEvent)은 보지 않으므로
    //   한글 입력기가 켜져 있어도 같은 자리의 키는 같은 논리 키다.
    // - 물리 키 → 논리 키와 "논리 키 상태가 바뀔 때만 이벤트"는 엔진 없는 LogicalKeyTracker(Wizard.Client)가 한다.
    //   여기서는 물리 키의 눌림이 실제로 바뀐 경우에만 넘긴다. 같은 상태가 다시 오는 이벤트(키 반복, 다른 키의 변화가 실린 전체 상태 이벤트)는 넘기지 않는다.
    // - 키보드가 둘 이상이면 물리 키는 어느 한 키보드에서라도 눌려 있으면 눌린 것이다(한 키보드의 뗌이 다른 키보드의 누름을 지우지 않는다).
    // - 키보드가 빠지거나 꺼지거나 리셋되면(초점을 잃으면 Input System 이 리셋한다) 그 키보드의 키를 그때의 시각에 뗀 것으로 한다.
    //   다시 초점을 얻으면 Input System 이 보내는 전체 상태 이벤트가 눌린 키를 다시 알린다.
    // - 이벤트 처리 경로는 할당하지 않는다(대기 줄이 늘 때만 늘린다). 콜백은 Input System 이 주 스레드에서 부른다.
    public sealed class UnityInputSource : IInputSource, IDisposable
    {
        private const int InitialPendingCapacity = 64;    // 자료구조 크기

        private readonly IMonotonicClock _clock;
        private readonly LogicalKeyTracker _tracker;
        private readonly RawKeyEvent[] _scratch = new RawKeyEvent[LogicalKeyTracker.MaxEventsPerChange];
        private readonly byte[] _holders = new byte[KeyBindings.PhysicalKeyCapacity];   // 물리 키마다 눌러 둔 키보드 수
        private readonly List<KeyboardKeys> _keyboards = new List<KeyboardKeys>();
        private readonly Action<InputEventPtr, InputDevice> _onEvent;
        private readonly Action<InputDevice, InputDeviceChange> _onDeviceChange;
        private readonly Action<bool> _onFocusChanged;

        private RawKeyEvent[] _pending = new RawKeyEvent[InitialPendingCapacity];
        private int _head;
        private int _count;
        private bool _enabled;
        private bool _disposed;

        public UnityInputSource(KeyBindings bindings, IMonotonicClock clock)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _tracker = new LogicalKeyTracker(bindings);
            _onEvent = OnEvent;
            _onDeviceChange = OnDeviceChange;
            _onFocusChanged = OnFocusChanged;
        }

        public KeyBindings Bindings => _tracker.Bindings;

        public KeyMask CurrentKeys => _tracker.Current;

        // 아직 Drain 하지 않은 이벤트 수
        public int PendingCount => _count;

        public bool IsEnabled => _enabled;

        // 결투 중인가(CONTRACTS 6.5 의 2번). [임시] 지금은 표지만 남긴다. 키보드는 폴링 장치가 아니라서 InputSystem.pollingFrequency 로는
        // 프레임 사이의 이벤트를 꺼낼 수 없다. 프레임과 떨어진 송신은 결투 세션(M14)이 이 표지를 보고 붙인다. OPEN-QUESTIONS.
        public bool DuelPolling { get; private set; }

        // Input System 에 붙는다. 붙는 순간 기록한 물리 키를 장치의 지금 상태로 맞춘다(물리 키와 논리 키가 어긋나지 않게).
        // 떨어져 있는 동안 생긴 차이는 붙는 순간의 시각에 일어난 것으로 한다(CONTRACTS 6.5 CurrentKeys, spec 01 4.2):
        // - 새로 눌린 키는 누름, 기록은 눌림인데 지금은 떼어진 키는 뗌
        // - 빠진 키보드(added 아님)와 꺼진 키보드는 모든 키를 뗀 것으로 하고, 빠진 키보드는 목록에서 지운다
        // 누름을 먼저, 뗌을 나중에 낸다. 같은 물리 키가 떨어져 있는 동안 한 키보드에서 다른 키보드로 옮겨 갔으면 논리 키는 계속 눌린 것이라
        // 뗐다 누르는 이벤트 쌍이 나오지 않는다.
        public void Enable()
        {
            ThrowIfDisposed();
            if (_enabled)
            {
                return;
            }
            InputSystem.onEvent += _onEvent;
            InputSystem.onDeviceChange += _onDeviceChange;
            Application.focusChanged += _onFocusChanged;
            _enabled = true;

            double now = _clock.NowSeconds;
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is Keyboard keyboard && keyboard.enabled)
                {
                    Find(keyboard);                        // 떨어져 있는 동안 더해진 키보드
                }
            }
            for (int i = 0; i < _keyboards.Count; i++)
            {
                Resync(_keyboards[i], true, now);
            }
            for (int i = 0; i < _keyboards.Count; i++)
            {
                Resync(_keyboards[i], false, now);
            }
            for (int i = _keyboards.Count - 1; i >= 0; i--)
            {
                if (!_keyboards[i].Device.added)
                {
                    _keyboards.RemoveAt(i);
                }
            }
        }

        // 떨어진다. 기록한 키는 그대로 둔다(다시 붙을 때 Enable 이 장치 상태로 맞춘다)
        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }
            InputSystem.onEvent -= _onEvent;
            InputSystem.onDeviceChange -= _onDeviceChange;
            Application.focusChanged -= _onFocusChanged;
            _enabled = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            Disable();
            _disposed = true;
        }

        public int Drain(Span<RawKeyEvent> buffer)
        {
            int n = _count < buffer.Length ? _count : buffer.Length;
            new ReadOnlySpan<RawKeyEvent>(_pending, _head, n).CopyTo(buffer);
            _head += n;
            _count -= n;
            if (_count == 0)
            {
                _head = 0;
            }
            return n;
        }

        // 키 재설정. 누르고 있는 물리 키를 새 배치로 다시 읽고 논리 상태의 차이를 지금 시각의 이벤트로 낸다
        public void SetBindings(KeyBindings bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }
            Append(_tracker.SetBindings(bindings, _clock.NowSeconds, _scratch));
        }

        public void SetDuelPolling(bool on) => DuelPolling = on;

        // 모든 키보드의 모든 키를 지금 뗀 것으로 한다(창의 초점을 잃었을 때. Application.focusChanged 가 부른다)
        public void ReleaseAll()
        {
            double now = _clock.NowSeconds;
            for (int i = 0; i < _keyboards.Count; i++)
            {
                ReleaseDevice(_keyboards[i], now);
            }
        }

        private void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!(device is Keyboard keyboard))
            {
                return;
            }
            FourCC type = eventPtr.type;
            if (type != StateEvent.Type && type != DeltaStateEvent.Type)
            {
                return;                                    // TextEvent, IMECompositionEvent 등: 글자는 보지 않는다
            }
            KeyboardKeys keys = Find(keyboard);
            double time = eventPtr.time;
            for (int p = 1; p < keys.Controls.Length; p++)
            {
                KeyControl? control = keys.Controls[p];
                if (control == null || !control.ReadValueFromEvent(eventPtr, out float value))
                {
                    continue;                              // 이 이벤트에 실리지 않은 키(일부만 담은 DeltaStateEvent)
                }
                SetDeviceKey(keys, (PhysicalKey)p, control.IsValueConsideredPressed(value), time);
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Keyboard))
            {
                return;
            }
            switch (change)
            {
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                case InputDeviceChange.SoftReset:
                case InputDeviceChange.HardReset:
                    int i = IndexOf(device);
                    if (i >= 0)
                    {
                        ReleaseDevice(_keyboards[i], _clock.NowSeconds);
                        if (change == InputDeviceChange.Removed)
                        {
                            _keyboards.RemoveAt(i);
                        }
                    }
                    break;
            }
        }

        private void OnFocusChanged(bool focused)
        {
            if (!focused)
            {
                ReleaseAll();
            }
        }

        private void SetDeviceKey(KeyboardKeys keys, PhysicalKey physical, bool down, double time)
        {
            if (keys.IsDown(physical) == down)
            {
                return;                                    // 물리 키의 상태가 그대로다(키 반복 등)
            }
            keys.Set(physical, down);
            int p = (int)physical;
            int holders = _holders[p];
            if (down)
            {
                _holders[p] = (byte)(holders + 1);
                if (holders == 0)
                {
                    Append(_tracker.OnPhysicalKey(physical, true, time, _scratch));
                }
            }
            else
            {
                _holders[p] = (byte)(holders - 1);
                if (holders == 1)
                {
                    Append(_tracker.OnPhysicalKey(physical, false, time, _scratch));
                }
            }
        }

        // 키보드 하나의 기록을 장치의 지금 상태로 맞춘다. pressPass 면 누름만, 아니면 뗌만 낸다.
        // 빠졌거나 꺼진 키보드는 컨트롤 값을 믿을 수 없으니 모든 키가 떼어진 것으로 본다.
        private void Resync(KeyboardKeys keys, bool pressPass, double time)
        {
            Keyboard device = keys.Device;
            bool live = device.added && device.enabled;
            for (int p = 1; p < keys.Controls.Length; p++)
            {
                KeyControl? control = keys.Controls[p];
                if (control == null)
                {
                    continue;
                }
                bool pressed = live && control.isPressed;
                if (pressed == pressPass)
                {
                    SetDeviceKey(keys, (PhysicalKey)p, pressed, time);
                }
            }
        }

        private void ReleaseDevice(KeyboardKeys keys, double time)
        {
            for (int p = 1; p < keys.Controls.Length; p++)
            {
                if (keys.IsDown((PhysicalKey)p))
                {
                    SetDeviceKey(keys, (PhysicalKey)p, false, time);
                }
            }
        }

        private void Append(int n)
        {
            if (n == 0)
            {
                return;
            }
            if (_head + _count + n > _pending.Length)
            {
                if (_count + n <= _pending.Length)
                {
                    Array.Copy(_pending, _head, _pending, 0, _count);
                }
                else
                {
                    int size = _pending.Length;
                    while (size < _count + n)
                    {
                        size *= 2;
                    }
                    var grown = new RawKeyEvent[size];
                    Array.Copy(_pending, _head, grown, 0, _count);
                    _pending = grown;
                }
                _head = 0;
            }
            Array.Copy(_scratch, 0, _pending, _head + _count, n);
            _count += n;
        }

        private KeyboardKeys Find(Keyboard keyboard)
        {
            int i = IndexOf(keyboard);
            if (i >= 0)
            {
                return _keyboards[i];
            }
            var keys = new KeyboardKeys(keyboard);
            _keyboards.Add(keys);
            return keys;
        }

        // 장치 번호가 아니라 개체로 찾는다(빠진 키보드의 기록이 남아 있는 동안 번호가 겹쳐도 섞이지 않게)
        private int IndexOf(InputDevice device)
        {
            for (int i = 0; i < _keyboards.Count; i++)
            {
                if (ReferenceEquals(_keyboards[i].Device, device))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UnityInputSource));
            }
        }

        // 키보드 하나의 물리 키 컨트롤(PhysicalKey 번호로 찾는다)과 그 키보드에서 눌린 물리 키
        private sealed class KeyboardKeys
        {
            private const int BitsPerWord = 64;

            public readonly Keyboard Device;
            public readonly KeyControl?[] Controls = new KeyControl?[KeyBindings.PhysicalKeyCapacity];
            private ulong _lo, _hi;

            public KeyboardKeys(Keyboard keyboard)
            {
                Device = keyboard;
                for (int p = 1; p < Controls.Length; p++)
                {
                    Key key = PhysicalKeyMap.ToKey((PhysicalKey)p);
                    if (key != Key.None)
                    {
                        Controls[p] = keyboard[key];
                    }
                }
            }

            public bool IsDown(PhysicalKey physical)
            {
                int i = (int)physical;
                return i < BitsPerWord ? (_lo & (1UL << i)) != 0 : (_hi & (1UL << (i - BitsPerWord))) != 0;
            }

            public void Set(PhysicalKey physical, bool down)
            {
                int i = (int)physical;
                if (i < BitsPerWord)
                {
                    _lo = down ? _lo | (1UL << i) : _lo & ~(1UL << i);
                }
                else
                {
                    _hi = down ? _hi | (1UL << (i - BitsPerWord)) : _hi & ~(1UL << (i - BitsPerWord));
                }
            }
        }
    }
}
