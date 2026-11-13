using System;
using Wizard.Protocol;
using Wizard.Protocol.Crypto;

namespace Wizard.Netcode.Tests.Duel.Client
{
    // 시험 대역. 보안이 아니다: 키·nonce 로 섞은 XOR 과, (키, nonce, AAD, 암호문)을 FNV-1a 64 두 갈래로 접은 16바이트 태그.
    // 넷코드 코어의 규칙(태그가 틀리면 열지 않는다, 재전송 창, 방향별 nonce)을 시험하는 데에 필요한 성질만 가진다.
    // 진짜 ChaCha20-Poly1305 의 적합성은 M2-NET-02 의 Protocol 시험이 RFC 8439 벡터로 본다. 인자 규칙은 AeadGuard 와 같다.
    internal sealed class FakeAeadCipher : IAeadCipher
    {
        private const ulong FnvOffset = 0xcbf29ce484222325UL;
        private const ulong FnvPrime = 0x100000001b3UL;

        public int OpenFailures { get; private set; }

        public void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
                         ReadOnlySpan<byte> plaintext, Span<byte> ciphertextAndTag)
        {
            AeadGuard.CheckSeal(key, nonce12, plaintext.Length, ciphertextAndTag.Length);
            byte[] cipher = new byte[plaintext.Length];
            for (int i = 0; i < plaintext.Length; i++)
            {
                cipher[i] = (byte)(plaintext[i] ^ Stream(key, nonce12, i));
            }
            byte[] tag = Tag(key, nonce12, aad, cipher);
            cipher.CopyTo(ciphertextAndTag);
            tag.CopyTo(ciphertextAndTag.Slice(cipher.Length));
        }

        public bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
                         ReadOnlySpan<byte> ciphertextAndTag, Span<byte> plaintext)
        {
            if (!AeadGuard.CheckOpen(key, nonce12, ciphertextAndTag.Length, plaintext.Length))
            {
                OpenFailures++;
                return false;
            }
            int n = ciphertextAndTag.Length - AeadSizes.TagBytes;
            byte[] cipher = ciphertextAndTag.Slice(0, n).ToArray();
            byte[] tag = Tag(key, nonce12, aad, cipher);
            if (!ciphertextAndTag.Slice(n).SequenceEqual(tag))
            {
                OpenFailures++;
                return false;
            }
            for (int i = 0; i < n; i++)
            {
                plaintext[i] = (byte)(cipher[i] ^ Stream(key, nonce12, i));
            }
            return true;
        }

        private static byte Stream(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, int i) =>
            (byte)(key[i % key.Length] ^ nonce[i % nonce.Length] ^ (byte)(i * 31 + 7));

        private static byte[] Tag(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> cipher)
        {
            ulong a = Fold(FnvOffset, key);
            a = Fold(a, nonce);
            a = Fold(a, aad);
            a = Fold(a, cipher);
            ulong b = Fold(a ^ 0x9E3779B97F4A7C15UL, cipher);
            b = Fold(b, key);
            byte[] tag = new byte[AeadSizes.TagBytes];
            for (int i = 0; i < 8; i++)
            {
                tag[i] = (byte)(a >> (8 * i));
                tag[8 + i] = (byte)(b >> (8 * i));
            }
            return tag;
        }

        private static ulong Fold(ulong h, ReadOnlySpan<byte> data)
        {
            foreach (byte x in data)
            {
                h ^= x;
                h *= FnvPrime;
            }
            return h;
        }
    }
}
