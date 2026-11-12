namespace Wizard.Protocol.Crypto
{
    // ChaCha20-Poly1305 의 고정 크기(RFC 8439 2.8절). CONTRACTS 6.2 IAeadCipher, 10.1 겉 형식(태그 16바이트)이 이 값을 쓴다.
    public static class AeadSizes
    {
        public const int KeyBytes = 32;
        public const int NonceBytes = 12;
        public const int TagBytes = 16;
    }
}
