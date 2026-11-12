using System;
using System.Collections.Generic;
using Wizard.Client.Net.Crypto;
using Wizard.Protocol.Tests.Crypto.Shared;
using Wizard.Server.Duel.Crypto;
using Wizard.TestKit;

namespace Wizard.Protocol.Tests.Crypto
{
    // 검사할 두 구현(클라이언트 BouncyCastle, 서버 런타임)과 RFC 벡터 픽스처.
    internal static class AeadImplementations
    {
        public const string Client = "client-bouncycastle";
        public const string Server = "server-runtime";

        public static IEnumerable<object[]> Names()
        {
            yield return new object[] { Client };
            yield return new object[] { Server };
        }

        public static IAeadCipher Create(string name)
        {
            switch (name)
            {
                case Client: return new BouncyCastleAeadCipher();
                case Server: return new RuntimeAeadCipher();
                default: throw new ArgumentException("unknown implementation " + name);
            }
        }

        public static IReadOnlyList<AeadVector> Vectors() => AeadVectorFile.LoadFromRepo(TestRepo.Root);
    }
}
