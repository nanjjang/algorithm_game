using System;
using Wizard.Sim.Input;

namespace Wizard.Client
{
    // spec 01 4.2 [임시], T-28. 물리 키의 눌림/뗌 → 논리 키 상태 변화. IInputSource 구현(유니티 쪽)이 엔진 없이 쓰는 몸통이다.
    // - 한 논리 키에 묶인 물리 키 중 하나라도 눌려 있으면 그 논리 키가 눌린 것이다.
    // - 논리 키 상태가 바뀔 때만 이벤트를 낸다(W를 누른 채 방향키 위를 눌러도 이벤트 없음). 자동 반복 누름도 이벤트가 없다.
    // - 한 번의 변화로 생기는 이벤트는 LogicalKey 번호 오름차순이다(같은 시각이므로 뒤의 양자화가 KeyClass 순으로 다시 놓는다).
    // 시각은 받은 그대로 RawKeyEvent에 싣는다(하드웨어 시각. 실수 시각은 RawKeyEvent까지만, CONTRACTS 6.5).
    public sealed class LogicalKeyTracker
    {
        public const int MaxEventsPerChange = KeyState.KeyCount;   // 출력 칸은 이만큼 있어야 한다

        private const int BitsPerWord = 64;
        private ulong _physicalLo, _physicalHi;           // 눌린 물리 키(번호 0~127)
        private int _logical;                             // 지금의 논리 키 상태(KeyMask 비트)
        private KeyBindings _bindings;

        public LogicalKeyTracker(KeyBindings bindings)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        public KeyBindings Bindings => _bindings;

        public KeyMask Current => (KeyMask)_logical;

        public bool IsPhysicalDown(PhysicalKey key)
        {
            KeyBindings.CheckPhysical(key);
            int i = (int)key;
            return i < BitsPerWord ? (_physicalLo & (1UL << i)) != 0 : (_physicalHi & (1UL << (i - BitsPerWord))) != 0;
        }

        // 물리 키 하나의 변화. 돌려주는 값 = output에 쓴 이벤트 수(0~9)
        public int OnPhysicalKey(PhysicalKey key, bool down, double timeSeconds, Span<RawKeyEvent> output)
        {
            KeyBindings.CheckPhysical(key);
            CheckOutput(output);
            if (key == PhysicalKey.None)
            {
                return 0;
            }
            int i = (int)key;
            if (i < BitsPerWord)
            {
                _physicalLo = down ? _physicalLo | (1UL << i) : _physicalLo & ~(1UL << i);
            }
            else
            {
                _physicalHi = down ? _physicalHi | (1UL << (i - BitsPerWord)) : _physicalHi & ~(1UL << (i - BitsPerWord));
            }
            return Emit(timeSeconds, output);
        }

        // 키 재설정. 누르고 있는 물리 키는 새 배치로 다시 읽고 논리 상태의 차이를 이벤트로 낸다
        public int SetBindings(KeyBindings bindings, double timeSeconds, Span<RawKeyEvent> output)
        {
            CheckOutput(output);
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            return Emit(timeSeconds, output);
        }

        // 창의 초점을 잃었을 때 등. 모든 물리 키를 뗀 것으로 하고 눌려 있던 논리 키의 뗌을 낸다
        public int ReleaseAll(double timeSeconds, Span<RawKeyEvent> output)
        {
            CheckOutput(output);
            _physicalLo = 0;
            _physicalHi = 0;
            return Emit(timeSeconds, output);
        }

        private int Emit(double timeSeconds, Span<RawKeyEvent> output)
        {
            int next = 0;
            for (int k = 0; k < KeyState.KeyCount; k++)
            {
                for (int s = 0; s < KeyBindings.SlotsPerKey; s++)
                {
                    PhysicalKey p = _bindings.Get((LogicalKey)k, s);
                    if (p != PhysicalKey.None && IsPhysicalDown(p))
                    {
                        next |= 1 << k;
                        break;
                    }
                }
            }
            int changed = next ^ _logical;
            int n = 0;
            for (int k = 0; k < KeyState.KeyCount; k++)
            {
                if ((changed & (1 << k)) != 0)
                {
                    output[n++] = new RawKeyEvent((LogicalKey)k, (next & (1 << k)) != 0, timeSeconds);
                }
            }
            _logical = next;
            return n;
        }

        private static void CheckOutput(Span<RawKeyEvent> output)
        {
            if (output.Length < MaxEventsPerChange)
            {
                throw new ArgumentException("output must hold MaxEventsPerChange events", nameof(output));
            }
        }
    }
}
