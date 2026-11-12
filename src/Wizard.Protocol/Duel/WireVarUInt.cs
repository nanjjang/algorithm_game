using Wizard.Core;

namespace Wizard.Protocol.Duel
{
    // 결투 패킷의 varuint(LEB128). 패킷의 varuint 는 모두 32비트 안의 값이다(Δtick, Δseq, seq).
    // ByteReader.TryVarUInt 는 64비트와 긴 꼴을 받으므로 여기서 32비트 넘침과 긴 꼴(0x80 0x00 같은 것)을 따로 거절한다.
    internal static class WireVarUInt
    {
        private const int MaxBytes32 = 5;

        public static DuelCodecError Read32(ref ByteReader r, out uint value)
        {
            value = 0;
            uint acc = 0;
            for (int i = 0; i < MaxBytes32; i++)
            {
                if (!r.TryU8(out byte b))
                {
                    return DuelCodecError.Truncated;
                }
                uint part = (uint)(b & 0x7F);
                if (i == MaxBytes32 - 1 && (b & 0x80) == 0 && part > 0x0F)
                {
                    return DuelCodecError.OutOfRange;     // 다섯째 바이트는 아랫 4비트만 쓴다
                }
                acc |= part << (7 * i);
                if ((b & 0x80) == 0)
                {
                    if (i > 0 && b == 0)
                    {
                        return DuelCodecError.NonCanonical;   // 마지막 바이트가 0이면 한 바이트 짧게 적을 수 있었다
                    }
                    value = acc;
                    return DuelCodecError.None;
                }
            }
            return DuelCodecError.OutOfRange;             // 다섯째 바이트에도 이어짐 비트: 32비트를 넘는다
        }

        public static int Size(uint v)
        {
            int n = 1;
            while (v >= 0x80)
            {
                v >>= 7;
                n++;
            }
            return n;
        }
    }
}
