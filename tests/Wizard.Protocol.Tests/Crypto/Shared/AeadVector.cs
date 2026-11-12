using System;

namespace Wizard.Protocol.Tests.Crypto.Shared
{
    // RFC 8439 의 AEAD 시험 벡터 하나(tests/fixtures/rfc8439/aead.json).
    public sealed class AeadVector
    {
        public AeadVector(string id, string section, byte[] key, byte[] nonce, byte[] aad, byte[] plaintext, byte[] ciphertext, byte[] tag)
        {
            Id = id;
            Section = section;
            Key = key;
            Nonce = nonce;
            Aad = aad;
            Plaintext = plaintext;
            Ciphertext = ciphertext;
            Tag = tag;
        }

        public string Id { get; }

        // RFC 의 절 번호(2.8.2, A.5)
        public string Section { get; }

        public byte[] Key { get; }

        public byte[] Nonce { get; }

        public byte[] Aad { get; }

        public byte[] Plaintext { get; }

        public byte[] Ciphertext { get; }

        public byte[] Tag { get; }

        // IAeadCipher 의 출력 꼴(암호문 ‖ 태그)
        public byte[] CiphertextAndTag()
        {
            var b = new byte[Ciphertext.Length + Tag.Length];
            Array.Copy(Ciphertext, 0, b, 0, Ciphertext.Length);
            Array.Copy(Tag, 0, b, Ciphertext.Length, Tag.Length);
            return b;
        }

        public override string ToString() => Id + " (RFC 8439 " + Section + ")";
    }
}
