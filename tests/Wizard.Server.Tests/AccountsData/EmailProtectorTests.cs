using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Wizard.Server.Data.Accounts;
using Xunit;

namespace Wizard.Server.Tests.AccountsData
{
    // spec 12 2.14절: AES-256-GCM 저장 형식 key_id ‖ nonce(12) ‖ 암호문 ‖ tag(16), AAD = account_id(8바이트 빅엔디언), 찾기용 HMAC-SHA256(Trim·소문자)
    public sealed class EmailProtectorTests
    {
        [Fact]
        public void Encrypt_RoundTrips_AndHasTheStorageLayout()
        {
            EmailProtector p = AccountsKit.NewProtector(7, out _, out _);
            byte[] blob = p.Encrypt(42, "  Alice@Daum.net ");
            Assert.Equal(1 + 12 + "Alice@Daum.net".Length + 16, blob.Length);
            Assert.Equal(7, blob[0]);
            Assert.Equal("Alice@Daum.net", p.Decrypt(42, blob));   // Trim 만 하고 대소문자는 그대로
        }

        [Fact]
        public void Encrypt_MatchesAnIndependentAesGcmWithTheAccountIdAsAad()
        {
            EmailProtector p = AccountsKit.NewProtector(3, out byte[] key, out _);
            const long accountId = 0x0102030405060708;
            byte[] blob = p.Encrypt(accountId, "bob@mail.example.co.kr");
            byte[] aad = { 1, 2, 3, 4, 5, 6, 7, 8 };
            int n = blob.Length - 1 - 12 - 16;
            byte[] plain = new byte[n];
            using (var gcm = new AesGcm(key, 16))
            {
                gcm.Decrypt(blob.AsSpan(1, 12), blob.AsSpan(13, n), blob.AsSpan(13 + n, 16), plain, aad);
            }
            Assert.Equal("bob@mail.example.co.kr", Encoding.UTF8.GetString(plain));
        }

        [Fact]
        public void TheSameAddressEncryptsDifferentlyEachTime()
        {
            EmailProtector p = AccountsKit.NewProtector();
            Assert.NotEqual(p.Encrypt(1, "a@b.co"), p.Encrypt(1, "a@b.co"));
        }

        [Fact]
        public void CiphertextMovedToAnotherAccount_DoesNotDecrypt()
        {
            EmailProtector p = AccountsKit.NewProtector();
            byte[] blob = p.Encrypt(1, "a@b.co");
            Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(2, blob));
        }

        [Fact]
        public void TamperedOrShortOrUnknownKeyCiphertext_DoesNotDecrypt()
        {
            EmailProtector p = AccountsKit.NewProtector(1, out _, out _);
            byte[] blob = p.Encrypt(1, "a@b.co");
            byte[] flipped = (byte[])blob.Clone();
            flipped[14] ^= 1;
            Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(1, flipped));
            Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(1, new byte[28]));
            byte[] otherKey = (byte[])blob.Clone();
            otherKey[0] = 9;
            Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(1, otherKey));
        }

        [Fact]
        public void LookupHmac_IsHmacSha256OfTheTrimmedLowercaseAddress()
        {
            EmailProtector p = AccountsKit.NewProtector(1, out _, out byte[] lookup);
            byte[] expected = HMACSHA256.HashData(lookup, Encoding.UTF8.GetBytes("alice@daum.net"));
            Assert.Equal(expected, p.LookupHmac("alice@daum.net"));
            Assert.Equal(expected, p.LookupHmac("  ALICE@Daum.Net\t"));
            Assert.NotEqual(expected, p.LookupHmac("alice2@daum.net"));
            Assert.Equal("alice@daum.net", EmailProtector.Normalize(" Alice@DAUM.net "));
        }

        [Fact]
        public void KeyRotation_WritesWithTheCurrentKey_AndReadsOldOnes()
        {
            byte[] k1 = RandomNumberGenerator.GetBytes(32);
            byte[] k2 = RandomNumberGenerator.GetBytes(32);
            byte[] lookup = RandomNumberGenerator.GetBytes(32);
            var before = new EmailProtector(1, new Dictionary<byte, byte[]> { [1] = k1 }, lookup);
            byte[] old = before.Encrypt(5, "a@b.co");
            var after = new EmailProtector(2, new Dictionary<byte, byte[]> { [1] = k1, [2] = k2 }, lookup);
            Assert.Equal("a@b.co", after.Decrypt(5, old));
            Assert.False(after.IsCurrentKey(old));
            byte[] fresh = after.Encrypt(5, "a@b.co");
            Assert.Equal(2, fresh[0]);
            Assert.True(after.IsCurrentKey(fresh));
            Assert.Equal(before.LookupHmac("a@b.co"), after.LookupHmac("a@b.co"));   // 찾기용 값은 암호 키 교체와 무관하다
        }

        [Fact]
        public void Keys_AreValidated_AndTheLookupKeyDiffersFromTheEncryptionKeys()
        {
            byte[] k = RandomNumberGenerator.GetBytes(32);
            Assert.Throws<ArgumentException>(() => new EmailProtector(1, new Dictionary<byte, byte[]> { [1] = k }, k));
            Assert.Throws<ArgumentException>(() => new EmailProtector(1, new Dictionary<byte, byte[]> { [1] = new byte[16] }, RandomNumberGenerator.GetBytes(32)));
            Assert.Throws<ArgumentException>(() => new EmailProtector(1, new Dictionary<byte, byte[]> { [1] = k }, new byte[8]));
            Assert.Throws<ArgumentException>(() => new EmailProtector(2, new Dictionary<byte, byte[]> { [1] = k }, RandomNumberGenerator.GetBytes(32)));
        }
    }
}
