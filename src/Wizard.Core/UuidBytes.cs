using System;

namespace Wizard.Core
{
    // CONTRACTS 1.5, 0.3. uuid ↔ 16바이트, RFC 4122 네트워크 순서(문자열 hex 순서).
    // Guid.ToByteArray()는 앞 세 필드가 리틀 엔디언인 혼합 순서라서 그대로 쓰지 않는다.
    public static class UuidBytes
    {
        public const int Size = 16;

        public static void Write(Guid value, Span<byte> destination16)
        {
            if (destination16.Length < Size || !value.TryWriteBytes(destination16))
            {
                throw new ArgumentException("destination must hold 16 bytes", nameof(destination16));
            }
            SwapMixed(destination16);
        }

        public static Guid Read(ReadOnlySpan<byte> source16)
        {
            if (source16.Length < Size)
            {
                throw new ArgumentException("source must hold 16 bytes", nameof(source16));
            }
            Span<byte> tmp = stackalloc byte[Size];
            source16.Slice(0, Size).CopyTo(tmp);
            SwapMixed(tmp);
            return new Guid(tmp);
        }

        // 혼합 순서 ↔ 네트워크 순서(같은 변환이 양방향이다)
        private static void SwapMixed(Span<byte> b)
        {
            Swap(b, 0, 3);
            Swap(b, 1, 2);
            Swap(b, 4, 5);
            Swap(b, 6, 7);
        }

        private static void Swap(Span<byte> b, int i, int j)
        {
            byte t = b[i];
            b[i] = b[j];
            b[j] = t;
        }
    }
}
