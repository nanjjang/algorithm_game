using System;

namespace Wizard.Server.Data.Accounts
{
    // 가입 한 건(spec 12 2.1절). 입력 규칙 검사(ValidateSignup)와 Argon2id 해시는 호출자(App)가 끝낸 뒤다.
    // Email 은 저장되지 않는다: PgAccountStore.CreateAsync 가 새 계정 번호로 암호화하고 찾기용 HMAC 을 만들어 그 둘만 넣는다.
    public sealed class NewAccount
    {
        public NewAccount(string loginId, byte[] pwHash, string pwParamsJson, string email, string nickname)
        {
            LoginId = loginId ?? throw new ArgumentNullException(nameof(loginId));
            PwHash = pwHash ?? throw new ArgumentNullException(nameof(pwHash));
            PwParamsJson = pwParamsJson ?? throw new ArgumentNullException(nameof(pwParamsJson));
            Email = email ?? throw new ArgumentNullException(nameof(email));
            Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
        }

        public string LoginId { get; }
        public byte[] PwHash { get; }
        public string PwParamsJson { get; }
        public string Email { get; }
        public string Nickname { get; }
    }
}
