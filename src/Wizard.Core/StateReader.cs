using System;

namespace Wizard.Core
{
    // CONTRACTS 1.4. StateWriter.ForBytes 로 쓴 것을 같은 순서로 읽는다. 모자라면 상태가 깨진 것이다(SimInvariantException).
    public ref struct StateReader
    {
        private ByteReader _reader;

        public StateReader(ReadOnlySpan<byte> state)
        {
            _reader = new ByteReader(state);
        }

        public int Remaining => _reader.Remaining;

        public byte U8()
        {
            if (!_reader.TryU8(out byte v))
            {
                throw Underrun();
            }
            return v;
        }

        public bool Bool()
        {
            byte b = U8();
            if (b > 1)
            {
                throw new SimInvariantException("state: bool byte out of range");
            }
            return b == 1;
        }

        public int I32()
        {
            if (!_reader.TryI32(out int v))
            {
                throw Underrun();
            }
            return v;
        }

        public uint U32()
        {
            if (!_reader.TryU32(out uint v))
            {
                throw Underrun();
            }
            return v;
        }

        public long I64()
        {
            if (!_reader.TryI64(out long v))
            {
                throw Underrun();
            }
            return v;
        }

        public ulong U64()
        {
            if (!_reader.TryU64(out ulong v))
            {
                throw Underrun();
            }
            return v;
        }

        public Fix64 Fix() => Fix64.FromRaw(I64());

        public FixVec2 Vec()
        {
            Fix64 x = Fix();
            Fix64 y = Fix();
            return new FixVec2(x, y);
        }

        public Angle Ang() => Angle.FromCenti(I32());

        public int Count()
        {
            int n = I32();
            if (n < 0)
            {
                throw new SimInvariantException("state: negative count");
            }
            return n;
        }

        private static SimInvariantException Underrun() => new SimInvariantException("state: unexpected end");
    }
}
