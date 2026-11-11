using System;

namespace Wizard.Core
{
    // CONTRACTS 1.4, T-10. 정규 직렬화와 상태 해시(FNV-1a 64)를 한 길로 쓴다. 해시는 State_Write 를 해시 모드로 부른 것이다.
    // 규칙: 필드 순서는 코드에 고정, 개체는 id 오름차순, 선택 값은 존재 바이트 뒤에 값, 표시 전용 값은 넣지 않는다, 패딩 없음.
    public ref struct StateWriter
    {
        public const ulong FnvOffset = 0xcbf29ce484222325UL;
        public const ulong FnvPrime = 0x100000001b3UL;

        private readonly ByteWriter? _target;
        private ulong _hash;

        private StateWriter(ByteWriter? target)
        {
            _target = target;
            _hash = FnvOffset;
        }

        // 해시만(버퍼 없음)
        public static StateWriter ForHash() => new StateWriter(null);

        // 바이트로 쓰기(상태 전체 전송). 해시도 같이 센다.
        public static StateWriter ForBytes(ByteWriter target) => new StateWriter(target ?? throw new ArgumentNullException(nameof(target)));

        public ulong Hash => _hash;

        public void U8(byte v)
        {
            _hash = unchecked((_hash ^ v) * FnvPrime);
            _target?.U8(v);
        }

        // bool = 1바이트 0/1
        public void Bool(bool v) => U8(v ? (byte)1 : (byte)0);

        public void I32(int v) => U32(unchecked((uint)v));

        public void U32(uint v)
        {
            for (int i = 0; i < 4; i++)
            {
                U8((byte)(v >> (8 * i)));
            }
        }

        public void I64(long v) => U64(unchecked((ulong)v));

        public void U64(ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                U8((byte)(v >> (8 * i)));
            }
        }

        public void Fix(Fix64 v) => I64(v.Raw);

        public void Vec(FixVec2 v)
        {
            I64(v.X.Raw);
            I64(v.Y.Raw);
        }

        public void Ang(Angle v) => I32(v.Centi);

        // 컬렉션 앞에 개수(i32)
        public void Count(int n) => I32(n);
    }
}
