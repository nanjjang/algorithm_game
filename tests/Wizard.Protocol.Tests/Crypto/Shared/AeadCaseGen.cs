using Wizard.Core;
using Wizard.Protocol.Crypto;
using Wizard.TestKit.Shared.Fuzz;

namespace Wizard.Protocol.Tests.Crypto.Shared
{
    // AeadCase 생성기(TestKit.Shared 의 FuzzGen). 난수는 넘겨받은 Pcg32 만 쓴다(같은 사례 씨앗이면 모든 런타임에서 같은 사례).
    public static class AeadCaseGen
    {
        public static FuzzGen<AeadCase> Any(int maxPlaintextBytes, int maxAadBytes)
        {
            return (ref Pcg32 rng) =>
            {
                byte[] key = Fill(ref rng, AeadSizes.KeyBytes);
                byte[] nonce = Fill(ref rng, AeadSizes.NonceBytes);
                byte[] aad = Fill(ref rng, rng.NextRange(0, maxAadBytes));
                byte[] plaintext = Fill(ref rng, rng.NextRange(0, maxPlaintextBytes));
                int flipIndex = rng.NextRange(0, int.MaxValue);
                byte flipMask = (byte)rng.NextRange(1, byte.MaxValue);
                return new AeadCase(key, nonce, aad, plaintext, flipIndex, flipMask);
            };
        }

        private static byte[] Fill(ref Pcg32 rng, int length)
        {
            var b = new byte[length];
            for (int i = 0; i < length; i++)
            {
                b[i] = (byte)rng.NextInt(byte.MaxValue + 1);
            }
            return b;
        }
    }
}
