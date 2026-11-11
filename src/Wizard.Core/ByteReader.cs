using System;

namespace Wizard.Core
{
    // CONTRACTS 1.4. 신뢰할 수 없는 바이트를 읽는다. 모자라면 false, 예외 없음. 실패해도 위치는 그대로다.
    public ref struct ByteReader
    {
        private const int MaxVarUIntBytes = 10;

        private readonly ReadOnlySpan<byte> _data;
        private int _pos;

        public ByteReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _pos = 0;
        }

        public int Remaining => _data.Length - _pos;

        public int Position => _pos;

        public bool TryU8(out byte v)
        {
            if (Remaining < 1)
            {
                v = 0;
                return false;
            }
            v = _data[_pos++];
            return true;
        }

        public bool TryU16(out ushort v)
        {
            if (Remaining < 2)
            {
                v = 0;
                return false;
            }
            v = (ushort)(_data[_pos] | (_data[_pos + 1] << 8));
            _pos += 2;
            return true;
        }

        public bool TryU32(out uint v)
        {
            if (Remaining < 4)
            {
                v = 0;
                return false;
            }
            uint r = 0;
            for (int i = 0; i < 4; i++)
            {
                r |= (uint)_data[_pos + i] << (8 * i);
            }
            _pos += 4;
            v = r;
            return true;
        }

        public bool TryU64(out ulong v)
        {
            if (Remaining < 8)
            {
                v = 0;
                return false;
            }
            ulong r = 0;
            for (int i = 0; i < 8; i++)
            {
                r |= (ulong)_data[_pos + i] << (8 * i);
            }
            _pos += 8;
            v = r;
            return true;
        }

        public bool TryI16(out short v)
        {
            bool ok = TryU16(out ushort u);
            v = unchecked((short)u);
            return ok;
        }

        public bool TryI32(out int v)
        {
            bool ok = TryU32(out uint u);
            v = unchecked((int)u);
            return ok;
        }

        public bool TryI64(out long v)
        {
            bool ok = TryU64(out ulong u);
            v = unchecked((long)u);
            return ok;
        }

        // LEB128. 10바이트를 넘거나 64비트를 넘치면 false.
        public bool TryVarUInt(out ulong v)
        {
            ulong r = 0;
            int p = _pos;
            for (int i = 0; i < MaxVarUIntBytes; i++)
            {
                if (p >= _data.Length)
                {
                    break;
                }
                byte b = _data[p++];
                ulong part = (ulong)(b & 0x7F);
                if (i == MaxVarUIntBytes - 1 && part > 1UL)
                {
                    break;   // 64비트를 넘는다
                }
                r |= part << (7 * i);
                if ((b & 0x80) == 0)
                {
                    _pos = p;
                    v = r;
                    return true;
                }
            }
            v = 0;
            return false;
        }

        public bool TryBytes(int count, out ReadOnlySpan<byte> v)
        {
            if (count < 0 || Remaining < count)
            {
                v = default;
                return false;
            }
            v = _data.Slice(_pos, count);
            _pos += count;
            return true;
        }
    }
}
