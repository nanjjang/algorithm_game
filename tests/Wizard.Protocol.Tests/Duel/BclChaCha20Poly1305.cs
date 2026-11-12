using System;
using System.Security.Cryptography;

namespace Wizard.Protocol.Tests.Duel
{
    // 시험 전용 IAeadCipher: .NET 의 ChaCha20Poly1305. 제품 구현은 M2-NET-02 의 몫이고 공유 패키지는 이 타입을 쓰지 못한다(분석기).
    // 이 기기에서 지원되지 않으면 쓰는 테스트가 실패한다(건너뛰지 않는다).
    internal sealed class BclChaCha20Poly1305 : IAeadCipher
    {
        private const int TagBytes = 16;

        public void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> plaintext,
                         Span<byte> ciphertextAndTag)
        {
            using var c = new ChaCha20Poly1305(key);
            c.Encrypt(nonce12, plaintext, ciphertextAndTag.Slice(0, plaintext.Length), ciphertextAndTag.Slice(plaintext.Length, TagBytes), aad);
        }

        public bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> ciphertextAndTag,
                         Span<byte> plaintext)
        {
            if (ciphertextAndTag.Length < TagBytes)
            {
                return false;
            }
            int n = ciphertextAndTag.Length - TagBytes;
            using var c = new ChaCha20Poly1305(key);
            try
            {
                c.Decrypt(nonce12, ciphertextAndTag.Slice(0, n), ciphertextAndTag.Slice(n, TagBytes), plaintext.Slice(0, n), aad);
                return true;
            }
            catch (AuthenticationTagMismatchException)
            {
                plaintext.Slice(0, n).Clear();
                return false;
            }
        }
    }
}
