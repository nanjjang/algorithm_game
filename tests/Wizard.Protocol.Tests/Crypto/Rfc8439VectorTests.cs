using System.Collections.Generic;
using System.IO;
using Wizard.Protocol.Tests.Crypto.Shared;
using Wizard.Server.Duel.Crypto;
using Xunit;

namespace Wizard.Protocol.Tests.Crypto
{
    // 수락 1: 두 구현이 RFC 8439 의 AEAD 시험 벡터(2.8.2, A.5)를 통과한다. 기대값은 tests/fixtures/rfc8439/aead.json(RFC 원문의 16진 덤프).
    public sealed class Rfc8439VectorTests
    {
        [Fact]
        public void Fixture_HasBothRfcAeadVectors()
        {
            IReadOnlyList<AeadVector> v = AeadImplementations.Vectors();
            Assert.Equal(2, v.Count);
            Assert.Equal("rfc8439-2.8.2", v[0].Id);
            Assert.Equal("rfc8439-a.5", v[1].Id);
            // 2.8.2 의 평문은 "Ladies and Gentlemen of the class of '99: ..." 114바이트, A.5 는 265바이트(RFC 원문)
            Assert.Equal(114, v[0].Plaintext.Length);
            Assert.Equal(265, v[1].Plaintext.Length);
            Assert.Equal("1ae10b594f09e26a7e902ecbd0600691", AeadHex.Format(v[0].Tag));
            Assert.Equal("eead9d67890cbb22392336fea1851f38", AeadHex.Format(v[1].Tag));
            Assert.StartsWith("Ladies and Gentlemen", System.Text.Encoding.ASCII.GetString(v[0].Plaintext));
            Assert.StartsWith("Internet-Drafts", System.Text.Encoding.ASCII.GetString(v[1].Plaintext));
        }

        [Fact]
        public void ServerRuntime_SupportsChaCha20Poly1305()
        {
            // 서버 구현은 이 런타임·OS 의 ChaCha20Poly1305 에 기댄다. 없으면 건너뛰지 않고 실패한다
            Assert.True(RuntimeAeadCipher.IsSupported, "System.Security.Cryptography.ChaCha20Poly1305 is not supported here");
        }

        [Theory]
        [MemberData(nameof(AeadImplementations.Names), MemberType = typeof(AeadImplementations))]
        public void PassesRfc8439Vectors(string implementation)
        {
            IAeadCipher cipher = AeadImplementations.Create(implementation);
            List<string> problems = AeadConformance.CheckVectors(implementation, cipher, AeadImplementations.Vectors());
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void Loader_RejectsMalformedFixtures()
        {
            Assert.Throws<InvalidDataException>(() => AeadVectorFile.Parse("{\"format\":1,\"vectors\":[]}", "t"));
            Assert.Throws<InvalidDataException>(() => AeadVectorFile.Parse("{\"format\":2,\"vectors\":[{}]}", "t"));
            string upper = "{\"format\":1,\"vectors\":[{\"id\":\"x\",\"section\":\"s\",\"key\":\"" + new string('A', 64) +
                           "\",\"nonce\":\"" + new string('0', 24) + "\",\"aad\":\"\",\"plaintext\":\"\",\"ciphertext\":\"\",\"tag\":\"" +
                           new string('0', 32) + "\"}]}";
            Assert.Throws<InvalidDataException>(() => AeadVectorFile.Parse(upper, "t"));
            string extra = upper.Replace(new string('A', 64), new string('a', 64)).Replace("\"id\"", "\"extra\":1,\"id\"");
            Assert.Throws<InvalidDataException>(() => AeadVectorFile.Parse(extra, "t"));
            string ok = upper.Replace(new string('A', 64), new string('a', 64));
            Assert.Single(AeadVectorFile.Parse(ok, "t"));
        }
    }
}
