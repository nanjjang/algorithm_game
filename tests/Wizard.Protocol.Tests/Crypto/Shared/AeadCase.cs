using System.Globalization;

namespace Wizard.Protocol.Tests.Crypto.Shared
{
    // 퍼저가 만드는 사례 하나: 무작위 키, nonce, AAD, 평문과, 위조 검사에서 뒤집을 자리(FlipIndex)와 비트(FlipMask, 0 이 아님).
    public sealed class AeadCase
    {
        public AeadCase(byte[] key, byte[] nonce, byte[] aad, byte[] plaintext, int flipIndex, byte flipMask)
        {
            Key = key;
            Nonce = nonce;
            Aad = aad;
            Plaintext = plaintext;
            FlipIndex = flipIndex;
            FlipMask = flipMask;
        }

        public byte[] Key { get; }

        public byte[] Nonce { get; }

        public byte[] Aad { get; }

        public byte[] Plaintext { get; }

        // 0 이상. 위조 검사는 이 값을 (암호문 ‖ 태그 ‖ AAD ‖ nonce ‖ 키) 길이로 나눈 나머지 자리를 바꾼다
        public int FlipIndex { get; }

        public byte FlipMask { get; }

        public override string ToString()
        {
            return "key=" + AeadHex.Format(Key) + " nonce=" + AeadHex.Format(Nonce) + " aad=" + AeadHex.Format(Aad) +
                   " plaintext[" + Plaintext.Length.ToString(CultureInfo.InvariantCulture) + "]=" + AeadHex.Format(Plaintext) +
                   " flip=" + FlipIndex.ToString(CultureInfo.InvariantCulture) + "/0x" + FlipMask.ToString("x2", CultureInfo.InvariantCulture);
        }
    }
}
