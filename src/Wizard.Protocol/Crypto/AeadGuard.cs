using System;

namespace Wizard.Protocol.Crypto
{
    // IAeadCipher 구현들이 같은 인자 규칙을 쓰도록 모은 검사(클라이언트 BouncyCastle 구현과 서버 런타임 구현이 똑같이 군다).
    //
    // 규칙:
    // - 키 32바이트, nonce 12바이트가 아니면 ArgumentException. 호출자의 잘못이다(CONTRACTS 0.2: 예외는 프로그래머 오류에만).
    // - Seal: 출력 길이는 정확히 평문 + 16.
    // - Open: 암호문+태그가 16바이트보다 짧으면 false(네트워크에서 온 값이라 예외가 아니다). 그 밖에는 평문 버퍼 길이가 정확히
    //   암호문+태그 − 16 이어야 한다.
    // - 구현은 모든 입력을 다 읽은 뒤에만 출력을 쓴다. 그래서 입력과 출력이 겹쳐도 된다(제자리 봉인·열기 포함).
    // - Open 이 false 이면 출력 버퍼는 한 바이트도 바뀌지 않는다.
    public static class AeadGuard
    {
        public static void CheckSeal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, int plaintextLength, int outputLength)
        {
            CheckKeyAndNonce(key, nonce12);
            if (outputLength != plaintextLength + AeadSizes.TagBytes)
            {
                throw new ArgumentException("ciphertextAndTag must be exactly plaintext.Length + 16 bytes", "ciphertextAndTag");
            }
        }

        // 열어 볼 수 있는 길이면 true. 너무 짧으면 false(그때 호출자는 출력을 건드리지 않고 false 를 돌려준다).
        public static bool CheckOpen(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, int ciphertextAndTagLength, int plaintextLength)
        {
            CheckKeyAndNonce(key, nonce12);
            if (ciphertextAndTagLength < AeadSizes.TagBytes)
            {
                return false;
            }
            if (plaintextLength != ciphertextAndTagLength - AeadSizes.TagBytes)
            {
                throw new ArgumentException("plaintext must be exactly ciphertextAndTag.Length - 16 bytes", "plaintext");
            }
            return true;
        }

        private static void CheckKeyAndNonce(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12)
        {
            if (key.Length != AeadSizes.KeyBytes)
            {
                throw new ArgumentException("key must be 32 bytes", "key");
            }
            if (nonce12.Length != AeadSizes.NonceBytes)
            {
                throw new ArgumentException("nonce must be 12 bytes", "nonce12");
            }
        }
    }
}
