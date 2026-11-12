using System;

namespace Wizard.Protocol.Tests.Duel
{
    // 퍼징용 가짜 암호: 봉인은 평문 + 0 태그, 열기는 언제나 통과하고 평문을 그대로 옮긴다.
    // 무작위 바이트가 태그 검사에 막히지 않고 본문 해석까지 닿게 한다.
    internal sealed class AcceptAllCipher : IAeadCipher
    {
        public void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> plaintext,
                         Span<byte> ciphertextAndTag)
        {
            plaintext.CopyTo(ciphertextAndTag);
            ciphertextAndTag.Slice(plaintext.Length).Clear();
        }

        public bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> ciphertextAndTag,
                         Span<byte> plaintext)
        {
            if (ciphertextAndTag.Length < 16)
            {
                return false;
            }
            ciphertextAndTag.Slice(0, ciphertextAndTag.Length - 16).CopyTo(plaintext);
            return true;
        }
    }
}
