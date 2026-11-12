using System;
using System.Security.Cryptography;
using Wizard.Protocol;
using Wizard.Protocol.Crypto;

namespace Wizard.Server.Duel.Crypto
{
    // 서버(wizard-duel)의 ChaCha20-Poly1305(RFC 8439). 런타임의 System.Security.Cryptography.ChaCha20Poly1305 를 쓴다
    // (CONTRACTS 6.2, 10.1, T-21, T-37). 클라이언트의 BouncyCastle 구현과 RFC 8439 벡터와 교차 검증으로 묶는다.
    //
    // 인자 규칙은 AeadGuard 와 같다. 런타임의 Decrypt 는 태그가 틀리면 평문 버퍼를 0으로 지우고, Encrypt 는 부분 겹침을
    // 거절한다. 이 계약은 "실패하면 출력을 건드리지 않는다", "겹쳐도 된다"이므로 둘 다 작업 배열을 거쳐 처리한 뒤 옮긴다.
    //
    // 키마다 런타임 객체를 새로 만들면 키 준비와 할당이 패킷마다 생긴다. 마지막 키 하나의 객체를 들고 있다가 같은 키면 다시 쓴다.
    // 한 인스턴스는 한 스레드에서만 쓴다(작업 배열과 객체 캐시를 가진다). 정적 상태는 없다.
    public sealed class RuntimeAeadCipher : IAeadCipher, IDisposable
    {
        private readonly byte[] _cachedKey = new byte[AeadSizes.KeyBytes];
        private ChaCha20Poly1305? _cached;
        private byte[] _scratch = Array.Empty<byte>();
        private bool _disposed;

        // 이 런타임·운영체제가 ChaCha20-Poly1305 를 지원하지 않으면 시작할 때 실패한다(조용히 다른 길로 가지 않는다).
        public RuntimeAeadCipher()
        {
            if (!ChaCha20Poly1305.IsSupported)
            {
                throw new PlatformNotSupportedException("ChaCha20-Poly1305 is not supported by this runtime/OS");
            }
        }

        public static bool IsSupported => ChaCha20Poly1305.IsSupported;

        public void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
                         ReadOnlySpan<byte> plaintext, Span<byte> ciphertextAndTag)
        {
            AeadGuard.CheckSeal(key, nonce12, plaintext.Length, ciphertextAndTag.Length);
            ChaCha20Poly1305 aead = ForKey(key);
            int n = plaintext.Length;
            int total = ciphertextAndTag.Length;
            Span<byte> work = Scratch(total);
            try
            {
                aead.Encrypt(nonce12, plaintext, work.Slice(0, n), work.Slice(n, AeadSizes.TagBytes), aad);
                work.CopyTo(ciphertextAndTag);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(work);
            }
        }

        public bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
                         ReadOnlySpan<byte> ciphertextAndTag, Span<byte> plaintext)
        {
            if (!AeadGuard.CheckOpen(key, nonce12, ciphertextAndTag.Length, plaintext.Length))
            {
                return false;
            }
            ChaCha20Poly1305 aead = ForKey(key);
            int n = plaintext.Length;
            Span<byte> work = Scratch(n);
            try
            {
                aead.Decrypt(nonce12, ciphertextAndTag.Slice(0, n), ciphertextAndTag.Slice(n, AeadSizes.TagBytes), work, aad);
            }
            catch (AuthenticationTagMismatchException)
            {
                CryptographicOperations.ZeroMemory(work);
                return false;
            }
            work.CopyTo(plaintext);
            CryptographicOperations.ZeroMemory(work);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _cached?.Dispose();
            _cached = null;
            CryptographicOperations.ZeroMemory(_cachedKey);
        }

        private ChaCha20Poly1305 ForKey(ReadOnlySpan<byte> key)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cached != null && CryptographicOperations.FixedTimeEquals(_cachedKey, key))
            {
                return _cached;
            }
            _cached?.Dispose();
            _cached = null;
            var next = new ChaCha20Poly1305(key);
            key.CopyTo(_cachedKey);
            _cached = next;
            return next;
        }

        private Span<byte> Scratch(int length)
        {
            if (_scratch.Length < length)
            {
                _scratch = new byte[length];
            }
            return _scratch.AsSpan(0, length);
        }
    }
}
