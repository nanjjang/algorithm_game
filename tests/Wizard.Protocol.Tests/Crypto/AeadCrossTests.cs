using Wizard.Client.Net.Crypto;
using Wizard.Protocol.Tests.Crypto.Shared;
using Wizard.Server.Duel.Crypto;
using Wizard.TestKit;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Protocol.Tests.Crypto
{
    // 수락 2·4: 두 구현이 서로의 암호문을 연다(무작위 키·nonce·AAD·길이 1만 건, 양방향). 봉인 결과도 바이트 단위로 같다.
    // 사례 생성과 성질 검사는 Shared/(TestKit.Shared 퍼저 틀)라서 실패하면 재현용 사례 씨앗이 보고된다.
    public sealed class AeadCrossTests
    {
        private readonly ITestOutputHelper _output;

        public AeadCrossTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ClientSeals_ServerOpens_TenThousandRandomCases()
        {
            var client = new BouncyCastleAeadCipher();
            using var server = new RuntimeAeadCipher();
            var result = FuzzAssert.Holds(_output, AeadConformance.DefaultFuzzOptions(), AeadConformance.DefaultCases(),
                c => AeadConformance.CheckCross(client, server, c), null, AeadConformance.Describe);
            Assert.Equal(AeadConformance.FuzzCases, result.CasesRun);
        }

        [Fact]
        public void ServerSeals_ClientOpens_TenThousandRandomCases()
        {
            var client = new BouncyCastleAeadCipher();
            using var server = new RuntimeAeadCipher();
            var result = FuzzAssert.Holds(_output, AeadConformance.DefaultFuzzOptions(), AeadConformance.DefaultCases(),
                c => AeadConformance.CheckCross(server, client, c), null, AeadConformance.Describe);
            Assert.Equal(AeadConformance.FuzzCases, result.CasesRun);
        }

        [Fact]
        public void CrossCheck_OnRfcVectors_BothDirections()
        {
            var client = new BouncyCastleAeadCipher();
            using var server = new RuntimeAeadCipher();
            foreach (AeadVector v in AeadImplementations.Vectors())
            {
                var c = new AeadCase(v.Key, v.Nonce, v.Aad, v.Plaintext, 0, 1);
                Assert.Null(AeadConformance.CheckCross(client, server, c));
                Assert.Null(AeadConformance.CheckCross(server, client, c));
                // 봉인 → 상대 구현으로 열기의 결과가 RFC 의 평문이고, 봉인 결과가 RFC 의 암호문 ‖ 태그다
                var sealedByClient = new byte[v.Plaintext.Length + 16];
                client.Seal(v.Key, v.Nonce, v.Aad, v.Plaintext, sealedByClient);
                Assert.Equal(v.CiphertextAndTag(), sealedByClient);
                var openedByServer = new byte[v.Plaintext.Length];
                Assert.True(server.Open(v.Key, v.Nonce, v.Aad, sealedByClient, openedByServer));
                Assert.Equal(v.Plaintext, openedByServer);
                var sealedByServer = new byte[v.Plaintext.Length + 16];
                server.Seal(v.Key, v.Nonce, v.Aad, v.Plaintext, sealedByServer);
                Assert.Equal(v.CiphertextAndTag(), sealedByServer);
                var openedByClient = new byte[v.Plaintext.Length];
                Assert.True(client.Open(v.Key, v.Nonce, v.Aad, sealedByServer, openedByClient));
                Assert.Equal(v.Plaintext, openedByClient);
            }
        }

        [Fact]
        public void FuzzCases_AreFixedBySeed()
        {
            // 같은 사례 씨앗이면 같은 사례(실패 보고의 재현 경로). 길이가 0~1200, AAD 0~64 안에 든다
            ulong s = TestKit.Shared.Fuzz.Fuzzer.CaseSeed(AeadConformance.FuzzSeed, 17);
            AeadCase a = TestKit.Shared.Fuzz.Fuzzer.Generate(s, AeadConformance.DefaultCases());
            AeadCase b = TestKit.Shared.Fuzz.Fuzzer.Generate(s, AeadConformance.DefaultCases());
            Assert.Equal(a.ToString(), b.ToString());
            for (int i = 0; i < 200; i++)
            {
                AeadCase c = TestKit.Shared.Fuzz.Fuzzer.Generate(TestKit.Shared.Fuzz.Fuzzer.CaseSeed(AeadConformance.FuzzSeed, i),
                    AeadConformance.DefaultCases());
                Assert.InRange(c.Plaintext.Length, 0, AeadConformance.MaxPlaintextBytes);
                Assert.InRange(c.Aad.Length, 0, AeadConformance.MaxAadBytes);
                Assert.NotEqual(0, c.FlipMask);
            }
        }
    }
}
