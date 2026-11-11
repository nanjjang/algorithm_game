using System;

namespace Wizard.Core.Hash
{
    // spec 01 3.4, CONTRACTS 1.4, DECISIONS-TECH T-10. FNV-1a 64 를 이미 만든 바이트(ByteWriter.Written, 규칙 데이터 직렬화 등)에 건다.
    // 세계 상태의 해시는 StateWriter.ForHash 로 센다. 두 길이 같은 바이트에서 같은 값을 내도록 상수는 StateWriter 의 것을 그대로 쓴다.
    // 상태가 없는 순수 함수다(가변 정적 필드 없음). 할당하지 않는다.
    public static class StateHasher
    {
        public const ulong Offset = StateWriter.FnvOffset;
        public const ulong Prime = StateWriter.FnvPrime;

        // 바이트열 전체의 FNV-1a 64
        public static ulong Compute(ReadOnlySpan<byte> data) => Append(Offset, data);

        // 이어 세기: h ^= b; h *= prime (넘침은 래핑)
        public static ulong Append(ulong hash, byte b) => unchecked((hash ^ b) * Prime);

        public static ulong Append(ulong hash, ReadOnlySpan<byte> data)
        {
            ulong h = hash;
            for (int i = 0; i < data.Length; i++)
            {
                h = unchecked((h ^ data[i]) * Prime);
            }
            return h;
        }
    }
}
