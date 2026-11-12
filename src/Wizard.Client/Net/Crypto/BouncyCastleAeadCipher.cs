using System;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Wizard.Protocol;
using Wizard.Protocol.Crypto;
// BouncyCastle 에도 IAeadCipher 가 있어 이름 공간 전체를 들이지 않는다
using BcChaCha20Poly1305 = Org.BouncyCastle.Crypto.Modes.ChaCha20Poly1305;

namespace Wizard.Client.Net.Crypto
{
    // 클라이언트의 ChaCha20-Poly1305(RFC 8439). CONTRACTS 6.2, 10.1, T-21, T-37.
    // netstandard2.1 에는 런타임 클래스가 없어 BouncyCastle 을 쓴다. Unity 가 싣는 것은 lib/netstandard2.0 판이라
    // Span API 가 없다. 그래서 인스턴스가 가진 작업 배열로 옮겨 처리하고 끝나면 지운다.
    //
    // 인자 규칙과 겹침 규칙은 AeadGuard 와 같다(서버 구현과 똑같이 군다). 모든 입력을 작업 배열로 옮긴 뒤에야 출력을 쓰므로
    // 입력과 출력이 겹쳐도 된다. 열기는 작업 배열에서 태그를 확인한 뒤에만 평문을 출력에 옮긴다(실패면 출력은 그대로).
    //
    // 한 인스턴스는 한 스레드에서만 쓴다(작업 배열을 가진다). 정적 상태는 없다.
    public sealed class BouncyCastleAeadCipher : IAeadCipher
    {
        private const int MacBits = AeadSizes.TagBytes * 8;

        private readonly byte[] _key = new byte[AeadSizes.KeyBytes];
        private readonly byte[] _nonce = new byte[AeadSizes.NonceBytes];
        private readonly BcChaCha20Poly1305 _opener = new BcChaCha20Poly1305();
        private byte[] _aad = Array.Empty<byte>();
        private byte[] _input = Array.Empty<byte>();
        private byte[] _output = Array.Empty<byte>();

        public void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
                         ReadOnlySpan<byte> plaintext, Span<byte> ciphertextAndTag)
        {
            AeadGuard.CheckSeal(key, nonce12, plaintext.Length, ciphertextAndTag.Length);
            int n = plaintext.Length;
            int total = ciphertextAndTag.Length;
            Load(key, nonce12, aad);
            Ensure(ref _input, n);
            Ensure(ref _output, total);
            plaintext.CopyTo(_input);
            try
            {
                // BouncyCastle 은 같은 인스턴스에서 직전과 같은 (키, nonce)로 봉인을 시작하면 ArgumentException 을 던진다.
                // 이 계약의 Seal 은 상태 없는 함수라(같은 입력 → 같은 출력, 서버 구현과 같다) 봉인마다 새 엔진을 쓴다.
                // nonce 재사용을 막는 것은 패킷 번호 쪽 규칙이다(CONTRACTS 10.1).
                var sealer = new BcChaCha20Poly1305();
                sealer.Init(true, new ParametersWithIV(new KeyParameter(_key), _nonce));
                sealer.ProcessAadBytes(_aad, 0, aad.Length);
                int written = sealer.ProcessBytes(_input, 0, n, _output, 0);
                written += sealer.DoFinal(_output, written);
                if (written != total)
                {
                    throw new InvalidOperationException("ChaCha20-Poly1305 produced an unexpected length");
                }
                new ReadOnlySpan<byte>(_output, 0, total).CopyTo(ciphertextAndTag);
            }
            finally
            {
                Wipe(aad.Length, n, total);
            }
        }

        public bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
                         ReadOnlySpan<byte> ciphertextAndTag, Span<byte> plaintext)
        {
            if (!AeadGuard.CheckOpen(key, nonce12, ciphertextAndTag.Length, plaintext.Length))
            {
                return false;
            }
            int total = ciphertextAndTag.Length;
            int n = plaintext.Length;
            Load(key, nonce12, aad);
            Ensure(ref _input, total);
            Ensure(ref _output, n);
            ciphertextAndTag.CopyTo(_input);
            try
            {
                _opener.Init(false, new ParametersWithIV(new KeyParameter(_key), _nonce));
                _opener.ProcessAadBytes(_aad, 0, aad.Length);
                int written = _opener.ProcessBytes(_input, 0, total, _output, 0);
                try
                {
                    written += _opener.DoFinal(_output, written);
                }
                catch (InvalidCipherTextException)
                {
                    // 태그가 틀렸다. 작업 배열에 풀린 바이트는 finally 에서 지운다. 출력은 건드리지 않았다.
                    return false;
                }
                if (written != n)
                {
                    throw new InvalidOperationException("ChaCha20-Poly1305 produced an unexpected length");
                }
                new ReadOnlySpan<byte>(_output, 0, n).CopyTo(plaintext);
                return true;
            }
            finally
            {
                _opener.Reset();
                Wipe(aad.Length, total, n);
            }
        }

        private void Load(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad)
        {
            key.CopyTo(_key);
            nonce12.CopyTo(_nonce);
            Ensure(ref _aad, aad.Length);
            aad.CopyTo(_aad);
        }

        // 작업 배열을 키운다(줄이지 않는다). 패킷 크기(1,200바이트 이하)에서 금방 자라고 멈춘다.
        private static void Ensure(ref byte[] buffer, int length)
        {
            if (buffer.Length < length)
            {
                buffer = new byte[length];
            }
        }

        // 키, 평문, 풀린 바이트를 작업 배열에 남기지 않는다.
        private void Wipe(int aadLength, int inputLength, int outputLength)
        {
            Array.Clear(_key, 0, _key.Length);
            Array.Clear(_nonce, 0, _nonce.Length);
            Array.Clear(_aad, 0, aadLength);
            Array.Clear(_input, 0, inputLength);
            Array.Clear(_output, 0, outputLength);
        }
    }
}
