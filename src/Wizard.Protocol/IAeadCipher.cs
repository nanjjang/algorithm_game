using System;

namespace Wizard.Protocol
{
    // CONTRACTS 6.2. ChaCha20-Poly1305 (RFC 8439)
    public interface IAeadCipher
    {
        void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> plaintext, Span<byte> ciphertextAndTag);
        bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> ciphertextAndTag, Span<byte> plaintext);
    }
}
