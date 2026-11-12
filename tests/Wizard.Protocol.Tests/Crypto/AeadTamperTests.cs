using System;
using Wizard.Client.Net.Crypto;
using Wizard.Protocol.Crypto;
using Wizard.Protocol.Tests.Crypto.Shared;
using Wizard.Server.Duel.Crypto;
using Wizard.TestKit;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Protocol.Tests.Crypto
{
    // 수락 3: 태그(또는 암호문, AAD, nonce, 키)가 틀리면 Open 이 false 이고 출력 버퍼를 건드리지 않는다.
    public sealed class AeadTamperTests
    {
        private readonly ITestOutputHelper _output;

        public AeadTamperTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(AeadImplementations.Names), MemberType = typeof(AeadImplementations))]
        public void WrongTag_ReturnsFalse_AndLeavesOutputUntouched(string implementation)
        {
            IAeadCipher cipher = AeadImplementations.Create(implementation);
            foreach (AeadVector v in AeadImplementations.Vectors())
            {
                for (int i = 0; i < AeadSizes.TagBytes; i++)
                {
                    byte[] ct = v.CiphertextAndTag();
                    ct[v.Ciphertext.Length + i] ^= (byte)(1 << (i & 7));
                    byte[] output = AeadConformance.Sentinel(v.Plaintext.Length);
                    Assert.False(cipher.Open(v.Key, v.Nonce, v.Aad, ct, output));
                    Assert.Equal(AeadConformance.Sentinel(v.Plaintext.Length), output);
                }
                // 태그 전체가 0 인 경우
                byte[] zeroTag = v.CiphertextAndTag();
                Array.Clear(zeroTag, v.Ciphertext.Length, AeadSizes.TagBytes);
                byte[] o = AeadConformance.Sentinel(v.Plaintext.Length);
                Assert.False(cipher.Open(v.Key, v.Nonce, v.Aad, zeroTag, o));
                Assert.Equal(AeadConformance.Sentinel(v.Plaintext.Length), o);
            }
        }

        [Fact]
        public void ClientSeals_ServerRejectsTampered_TenThousandRandomCases()
        {
            var client = new BouncyCastleAeadCipher();
            using var server = new RuntimeAeadCipher();
            FuzzAssert.Holds(_output, AeadConformance.DefaultFuzzOptions(), AeadConformance.DefaultCases(),
                c => AeadConformance.CheckTamper(client, server, c), null, AeadConformance.Describe);
        }

        [Fact]
        public void ServerSeals_ClientRejectsTampered_TenThousandRandomCases()
        {
            var client = new BouncyCastleAeadCipher();
            using var server = new RuntimeAeadCipher();
            FuzzAssert.Holds(_output, AeadConformance.DefaultFuzzOptions(), AeadConformance.DefaultCases(),
                c => AeadConformance.CheckTamper(server, client, c), null, AeadConformance.Describe);
        }

        [Theory]
        [MemberData(nameof(AeadImplementations.Names), MemberType = typeof(AeadImplementations))]
        public void EdgeCases_ShortInputs_ArgumentErrors_InPlace(string implementation)
        {
            var problems = AeadConformance.CheckEdges(implementation, AeadImplementations.Create(implementation));
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void TamperProperty_CatchesACipherThatIgnoresTheTag()
        {
            // 검사 자체가 헛돌지 않는지: 태그를 보지 않고 무조건 여는 가짜 구현은 위조 성질에 걸린다
            var broken = new TagIgnoringCipher(new BouncyCastleAeadCipher());
            var r = TestKit.Shared.Fuzz.Fuzzer.Run(AeadConformance.DefaultFuzzOptions(), AeadConformance.DefaultCases(),
                c => AeadConformance.CheckTamper(broken, broken, c), null, AeadConformance.Describe);
            Assert.False(r.Passed);
            Assert.NotEmpty(AeadConformance.CheckVectors("broken", broken, AeadImplementations.Vectors()));
        }

        // 태그 검증을 하지 않는 가짜(성질 검사가 실제로 실패를 잡는지 보는 용도). 봉인은 진짜에 맡긴다
        private sealed class TagIgnoringCipher : IAeadCipher
        {
            private readonly IAeadCipher _inner;

            public TagIgnoringCipher(IAeadCipher inner)
            {
                _inner = inner;
            }

            public void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> plaintext,
                             Span<byte> ciphertextAndTag)
            {
                _inner.Seal(key, nonce12, aad, plaintext, ciphertextAndTag);
            }

            public bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> ciphertextAndTag,
                             Span<byte> plaintext)
            {
                if (ciphertextAndTag.Length < AeadSizes.TagBytes)
                {
                    return false;
                }
                // 태그를 보지 않고 아무 바이트나 써 넣는다
                plaintext.Fill(0);
                return true;
            }
        }
    }
}
