using System;

namespace Wizard.Core
{
    // CONTRACTS 1.4. 패킷과 파일용. 모든 수는 리틀 엔디언, 시프트로만 쓴다(BitConverter·MemoryMarshal 금지).
    // 버퍼는 재사용한다(Reset). 세계나 호출자가 가진다(정적 버퍼 없음).
    public sealed class ByteWriter
    {
        private byte[] _buffer;
        private int _length;

        public ByteWriter() : this(256)
        {
        }

        public ByteWriter(int initialCapacity)
        {
            _buffer = new byte[initialCapacity < 16 ? 16 : initialCapacity];
            _length = 0;
        }

        public int Length => _length;

        public ReadOnlySpan<byte> Written => new ReadOnlySpan<byte>(_buffer, 0, _length);

        public void Reset() => _length = 0;

        public byte[] ToArray() => Written.ToArray();

        public void U8(byte v)
        {
            Ensure(1);
            _buffer[_length++] = v;
        }

        public void U16(ushort v)
        {
            Ensure(2);
            _buffer[_length] = (byte)v;
            _buffer[_length + 1] = (byte)(v >> 8);
            _length += 2;
        }

        public void U32(uint v)
        {
            Ensure(4);
            for (int i = 0; i < 4; i++)
            {
                _buffer[_length + i] = (byte)(v >> (8 * i));
            }
            _length += 4;
        }

        public void U64(ulong v)
        {
            Ensure(8);
            for (int i = 0; i < 8; i++)
            {
                _buffer[_length + i] = (byte)(v >> (8 * i));
            }
            _length += 8;
        }

        public void I16(short v) => U16(unchecked((ushort)v));

        public void I32(int v) => U32(unchecked((uint)v));

        public void I64(long v) => U64(unchecked((ulong)v));

        // LEB128(부호 없음)
        public void VarUInt(ulong v)
        {
            do
            {
                byte b = (byte)(v & 0x7FUL);
                v >>= 7;
                if (v != 0UL)
                {
                    b |= 0x80;
                }
                U8(b);
            }
            while (v != 0UL);
        }

        public void Bytes(ReadOnlySpan<byte> v)
        {
            Ensure(v.Length);
            v.CopyTo(new Span<byte>(_buffer, _length, v.Length));
            _length += v.Length;
        }

        private void Ensure(int extra)
        {
            int need = _length + extra;
            if (need <= _buffer.Length)
            {
                return;
            }
            int size = _buffer.Length;
            while (size < need)
            {
                size *= 2;
            }
            byte[] next = new byte[size];
            new ReadOnlySpan<byte>(_buffer, 0, _length).CopyTo(next);
            _buffer = next;
        }
    }
}
