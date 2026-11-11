using Wizard.Core;

namespace Wizard.Core.Tests.Random
{
    // 시험용 도우미. 구현(Pcg32)과 따로 spec 01 3.3절의 글을 그대로 옮긴 참조 모형과,
    // 원하는 출력을 내는 상태를 만드는 역함수를 둔다.
    internal static class RngTestSupport
    {
        public const ulong Multiplier = 6364136223846793005UL;

        // spec 01 3.3: inc = (seq << 1) | 1; state = 0; next(); state += seed; next();
        public static void ReferenceSeed(ulong seed, ulong seq, out ulong state, out ulong inc)
        {
            state = 0UL;
            inc = (seq << 1) | 1UL;
            ReferenceNext(ref state, inc);
            state = unchecked(state + seed);
            ReferenceNext(ref state, inc);
        }

        // spec 01 3.3: old = state; state = old × M + inc; xs = ((old >> 18) ^ old) >> 27; rot = old >> 59;
        //              return (xs >> rot) | (xs << ((−rot) & 31))
        public static uint ReferenceNext(ref ulong state, ulong inc)
        {
            ulong old = state;
            state = unchecked(old * Multiplier + inc);
            uint xs = unchecked((uint)(((old >> 18) ^ old) >> 27));
            int rot = (int)(old >> 59);
            return (xs >> rot) | (xs << ((32 - rot) & 31));
        }

        // XSH-RR 출력 함수의 역. rot(위 5비트)를 고르고 xs = rotl(output, rot)이 되게
        // y = old ^ (old >> 18)의 27..58비트를 위에서부터 채운다(old_i = y_i ^ old_{i+18}).
        // 아래 27비트는 출력에 들지 않으므로 low로 채운다. 나눗셈 경계 시험에서 정확한 r을 만들려고 쓴다.
        public static ulong StateForOutput(uint output, int rot, ulong low27)
        {
            uint xs = (output << rot) | (output >> ((32 - rot) & 31));
            ulong old = (ulong)(uint)rot << 59;
            for (int i = 58; i >= 27; i--)
            {
                ulong yi = (xs >> (i - 27)) & 1UL;
                ulong upper = i + 18 <= 63 ? (old >> (i + 18)) & 1UL : 0UL;
                old |= (yi ^ upper) << i;
            }
            old |= low27 & ((1UL << 27) - 1UL);
            return old;
        }

        // 다음 NextU32()가 output을 내는 생성기
        public static Pcg32 WithNextOutput(uint output, ulong inc = 1UL, int rot = 5, ulong low27 = 0x5A5A5A5UL)
        {
            var rng = new Pcg32(0UL, 0UL);
            rng.State = StateForOutput(output, rot, low27);
            rng.Inc = inc;
            return rng;
        }

        // 생성기가 앞으로 나간 NextU32 호출 수. 같은 출발점의 사본을 한 걸음씩 밀어 상태가 같아지는 곳을 찾는다.
        public static int StepsBetween(Pcg32 before, Pcg32 after, int limit = 64)
        {
            for (int i = 0; i <= limit; i++)
            {
                if (before.State == after.State && before.Inc == after.Inc)
                {
                    return i;
                }
                before.NextU32();
            }
            return -1;
        }
    }
}
