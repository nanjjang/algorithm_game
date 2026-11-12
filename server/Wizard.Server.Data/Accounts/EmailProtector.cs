using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Wizard.Server.Data.Accounts
{
    // spec 12 2.14절: 이메일 주소는 암호문과 찾기용 HMAC 으로만 DB 에 들어간다. 이 클래스가 그 두 값의 저장 형식을 정한다.
    //   암호문 — AES-256-GCM. 형식 [임시]: key_id(1B) ‖ nonce(12B) ‖ ciphertext ‖ tag(16B). nonce 는 행마다 CSPRNG.
    //            AAD [임시] = account_id 8바이트 빅엔디언(암호문을 다른 행에 옮겨 붙이면 풀리지 않는다).
    //            account.email_enc 와 account_token.new_email_enc 가 같은 키·같은 AAD 를 쓴다.
    //   찾기용 — HMAC-SHA256(email_lookup_key, Trim(email).ToLowerInvariant()). 암호 키와 다른 키. 되돌릴 수 없다.
    // 키는 DB 밖 파일에 둔다(pepper 와 같은 방식). 읽어서 넘기는 것은 호스트가 한다. 키 교체는 key_id 로: 새 키로 쓰고 옛 키로도 읽는다.
    // 암호화하는 주소는 Trim 만 한 원문이다(대소문자를 보존한다). 비교는 호출자가 Normalize 한 값끼리 한다(2.11절).
    public sealed class EmailProtector
    {
        public const int KeyBytes = 32;
        public const int NonceBytes = 12;
        public const int TagBytes = 16;
        public const int HeaderBytes = 1 + NonceBytes;
        public const int MinLookupKeyBytes = 32;

        private const int AadBytes = sizeof(long);

        private readonly byte _currentKeyId;
        private readonly Dictionary<byte, byte[]> _keys;
        private readonly byte[] _lookupKey;

        public EmailProtector(byte currentKeyId, IReadOnlyDictionary<byte, byte[]> encryptionKeys, byte[] lookupKey)
        {
            if (encryptionKeys == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeys));
            }
            if (lookupKey == null)
            {
                throw new ArgumentNullException(nameof(lookupKey));
            }
            if (lookupKey.Length < MinLookupKeyBytes)
            {
                throw new ArgumentException("the email lookup key must be at least " + MinLookupKeyBytes + " bytes", nameof(lookupKey));
            }
            _keys = new Dictionary<byte, byte[]>();
            foreach (KeyValuePair<byte, byte[]> kv in encryptionKeys)
            {
                if (kv.Value == null || kv.Value.Length != KeyBytes)
                {
                    throw new ArgumentException("email encryption key " + kv.Key + " must be " + KeyBytes + " bytes", nameof(encryptionKeys));
                }
                // 2.14절: 찾기용 키는 암호 키와 다른 키다
                if (CryptographicOperations.FixedTimeEquals(kv.Value, lookupKey))
                {
                    throw new ArgumentException("the email lookup key must differ from every encryption key", nameof(lookupKey));
                }
                _keys.Add(kv.Key, (byte[])kv.Value.Clone());
            }
            if (!_keys.ContainsKey(currentKeyId))
            {
                throw new ArgumentException("no encryption key for the current key id " + currentKeyId, nameof(currentKeyId));
            }
            _currentKeyId = currentKeyId;
            _lookupKey = (byte[])lookupKey.Clone();
        }

        public byte CurrentKeyId => _currentKeyId;

        // 2.1절 Normalize: Trim(e).ToLowerInvariant() (찾기용 값과 비교에 쓴다)
        public static string Normalize(string email)
        {
            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }
            return email.Trim().ToLowerInvariant();
        }

        public byte[] LookupHmac(string email)
        {
            byte[] message = Encoding.UTF8.GetBytes(Normalize(email));
            return HMACSHA256.HashData(_lookupKey, message);
        }

        public byte[] Encrypt(long accountId, string email)
        {
            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }
            byte[] plain = Encoding.UTF8.GetBytes(email.Trim());
            byte[] blob = new byte[HeaderBytes + plain.Length + TagBytes];
            blob[0] = _currentKeyId;
            Span<byte> nonce = blob.AsSpan(1, NonceBytes);
            RandomNumberGenerator.Fill(nonce);
            Span<byte> aad = stackalloc byte[AadBytes];
            BinaryPrimitives.WriteInt64BigEndian(aad, accountId);
            using (var gcm = new AesGcm(_keys[_currentKeyId], TagBytes))
            {
                gcm.Encrypt(nonce, plain, blob.AsSpan(HeaderBytes, plain.Length), blob.AsSpan(HeaderBytes + plain.Length, TagBytes), aad);
            }
            CryptographicOperations.ZeroMemory(plain);
            return blob;
        }

        // 형식이 틀렸거나, 모르는 key_id 거나, 다른 행(account_id)의 암호문이거나, 고쳐진 암호문이면 CryptographicException
        public string Decrypt(long accountId, byte[] blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException(nameof(blob));
            }
            if (blob.Length < HeaderBytes + TagBytes)
            {
                throw new CryptographicException("email ciphertext is too short");
            }
            if (!_keys.TryGetValue(blob[0], out byte[]? key))
            {
                throw new CryptographicException("unknown email key id " + blob[0]);
            }
            int length = blob.Length - HeaderBytes - TagBytes;
            byte[] plain = new byte[length];
            Span<byte> aad = stackalloc byte[AadBytes];
            BinaryPrimitives.WriteInt64BigEndian(aad, accountId);
            using (var gcm = new AesGcm(key, TagBytes))
            {
                gcm.Decrypt(blob.AsSpan(1, NonceBytes), blob.AsSpan(HeaderBytes, length), blob.AsSpan(HeaderBytes + length, TagBytes), plain, aad);
            }
            string email = Encoding.UTF8.GetString(plain);
            CryptographicOperations.ZeroMemory(plain);
            return email;
        }

        // 옛 key_id 로 쓴 암호문인가(키 교체 뒤 다시 암호화할 행을 고를 때)
        public bool IsCurrentKey(byte[] blob) => blob != null && blob.Length > 0 && blob[0] == _currentKeyId;
    }
}
