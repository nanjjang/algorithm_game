using System;
using System.Security.Cryptography;

namespace Wizard.Solo.Meta.Enhance
{
    /// <summary>
    /// 강화 난수의 출처(07 요약서 4.1절: 판 씨앗을 쓰지 않는 기기 난수). 판정은 Next(10000) 한 번,
    /// 변이 후보 추첨은 Next(후보 수) 한 번. 테스트는 정해 둔 값을 내는 구현을 끼운다.
    /// </summary>
    public interface IEnhanceRandom
    {
        /// <summary>[0, maxExclusive) 균등 정수.</summary>
        int Next(int maxExclusive);
    }

    /// <summary>Pcg32로 뽑는다. 씨앗을 주지 않으면 운영체제의 암호용 난수로 씨앗을 만든다(S8 U-05).</summary>
    public sealed class Pcg32EnhanceRandom : IEnhanceRandom
    {
        readonly Pcg32 _rng;

        public Pcg32EnhanceRandom(Pcg32 rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public Pcg32EnhanceRandom() : this(FromDevice()) { }

        public int Next(int maxExclusive) => _rng.Range(0, maxExclusive);

        static Pcg32 FromDevice()
        {
            var bytes = new byte[sizeof(ulong) + sizeof(ulong)];
            using (var gen = RandomNumberGenerator.Create()) gen.GetBytes(bytes);
            ulong seed = BitConverter.ToUInt64(bytes, 0);
            ulong sequence = BitConverter.ToUInt64(bytes, sizeof(ulong));
            return new Pcg32(seed, sequence);
        }
    }
}
