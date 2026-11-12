using Wizard.Core;

namespace Wizard.Sim.Input
{
    // spec 01 4.4, CONTRACTS 3.1. 논리 키 9개의 눌림 비트 + 키마다 마지막 누름 시각(입력 시각 단위).
    // 플레이어 상태의 일부라 값 형식이고(스냅숏은 값 복사), 세계가 State_Write/Read 로 그대로 직렬화한다.
    // 눌린 키의 누름, 안 눌린 키의 뗌은 무시한다(spec 01 4.3, online 4.11. 서버의 키 상태 검사와 같은 규칙).
    public struct KeyState
    {
        public const int KeyCount = (int)LogicalKey.Pause + 1;

        private ushort _down;          // 비트 번호 = (int)LogicalKey
        private ushort _everPressed;   // 한 번이라도 눌렸는가(마지막 누름 시각의 "없음"을 0과 가르려고)
        private long _t0, _t1, _t2, _t3, _t4, _t5, _t6, _t7, _t8;    // 키마다 마지막 누름 시각. 배열을 두지 않아 값 복사가 깊은 복사다

        public KeyMask Mask => (KeyMask)_down;

        public static KeyMask Bit(LogicalKey key) => (KeyMask)(1 << CheckedIndex(key));

        public bool IsDown(LogicalKey key) => (_down & (1 << CheckedIndex(key))) != 0;

        // 그 키를 한 번도 누르지 않았으면 거짓
        public bool TryGetLastPress(LogicalKey key, out long time)
        {
            int i = CheckedIndex(key);
            time = Get(i);
            return (_everPressed & (1 << i)) != 0;
        }

        // 상태를 바꿨으면 참. 무시한 이벤트(이미 눌린 키의 누름, 안 눌린 키의 뗌)는 거짓이고 상태가 그대로다.
        // 판정기·자세·발사에는 참을 돌려받은 이벤트만 넘긴다(spec 01 4.4, T-I07·T-I08).
        public bool Apply(in InputEvent e)
        {
            int i = CheckedIndex(e.Key);
            int bit = 1 << i;
            bool isDown = (_down & bit) != 0;
            if (e.Down)
            {
                if (isDown)
                {
                    return false;
                }
                _down = (ushort)(_down | bit);
                _everPressed = (ushort)(_everPressed | bit);
                Set(i, e.Time);
                return true;
            }
            if (!isDown)
            {
                return false;
            }
            _down = (ushort)(_down & ~bit);
            return true;
        }

        // 라운드 시작, 일시정지 해제 같은 "모두 뗌" 처리는 호출하는 쪽이 뗌 이벤트로 만든다. 이것은 세계 생성 때의 초기화용이다.
        public void Clear()
        {
            this = default;
        }

        public void Write(ref StateWriter w)
        {
            w.U32(_down);
            w.U32(_everPressed);
            for (int i = 0; i < KeyCount; i++)
            {
                w.I64(Get(i));
            }
        }

        public void Read(ref StateReader r)
        {
            uint down = r.U32();
            uint ever = r.U32();
            const uint valid = (1u << KeyCount) - 1u;
            if ((down & ~valid) != 0 || (ever & ~valid) != 0 || (down & ~ever) != 0)
            {
                throw new SimInvariantException("KeyState: bad key bits");
            }
            _down = (ushort)down;
            _everPressed = (ushort)ever;
            for (int i = 0; i < KeyCount; i++)
            {
                Set(i, r.I64());
            }
        }

        private static int CheckedIndex(LogicalKey key)
        {
            int i = (int)key;
            if (i < 0 || i >= KeyCount)
            {
                throw new SimInvariantException("unknown LogicalKey");
            }
            return i;
        }

        private long Get(int i)
        {
            switch (i)
            {
                case 0: return _t0;
                case 1: return _t1;
                case 2: return _t2;
                case 3: return _t3;
                case 4: return _t4;
                case 5: return _t5;
                case 6: return _t6;
                case 7: return _t7;
                default: return _t8;
            }
        }

        private void Set(int i, long time)
        {
            switch (i)
            {
                case 0: _t0 = time; break;
                case 1: _t1 = time; break;
                case 2: _t2 = time; break;
                case 3: _t3 = time; break;
                case 4: _t4 = time; break;
                case 5: _t5 = time; break;
                case 6: _t6 = time; break;
                case 7: _t7 = time; break;
                default: _t8 = time; break;
            }
        }
    }
}
