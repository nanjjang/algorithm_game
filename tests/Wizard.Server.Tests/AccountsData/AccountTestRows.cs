using System;
using System.Globalization;
using System.Threading.Tasks;
using Wizard.Server.Tests.Infra;

namespace Wizard.Server.Tests.AccountsData
{
    // 다른 묶음의 시험이 "번호가 정해진 계정 행"만 필요할 때 쓰는 준비 도구(행 잠금, 원장, 멱등 시험).
    // 0010_accounts.sql 이 들어온 뒤 account 는 id 가 GENERATED ALWAYS 이고 NOT NULL 열이 있어서 "INSERT INTO account (id) VALUES (…)" 로는 행을 만들 수 없다.
    // 이 도구는 스키마를 지키는 자리 값(아이디 규칙을 따르는 login_id, 서로 다른 닉네임, 빈 해시·암호문)으로 그 번호의 행을 만들고,
    // 뒤에 저장소가 받는 번호가 겹치지 않게 시퀀스를 가장 큰 번호 뒤로 옮긴다. 이미 있는 번호는 그대로 둔다.
    // 자리 값의 이메일 열은 실제 주소의 암호문이 아니다(복호화하지 않는 시험 전용).
    public static class AccountTestRows
    {
        private const string InsertSql =
            "INSERT INTO account (id, login_id, pw_hash, pw_params, email_enc, email_hmac, nickname) OVERRIDING SYSTEM VALUE " +
            "VALUES (@Id, @LoginId, '\\x00', '{}', '\\x00', '\\x00', @Nickname) ON CONFLICT (id) DO NOTHING";

        private const string BumpSequenceSql =
            "SELECT setval(pg_get_serial_sequence('account', 'id'), GREATEST((SELECT max(id) FROM account), 1))";

        public static async Task EnsureAsync(PgTestDatabase db, params long[] ids)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            foreach (long id in ids)
            {
                if (id <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(ids), id, "account ids are positive");
                }
                string n = id.ToString(CultureInfo.InvariantCulture);
                // login_id ^[a-z0-9]{4,16}$: "t" + 번호(3자리 이상, 15자리 이하)
                string digits = n.Length > 15 ? n.Substring(n.Length - 15) : n.PadLeft(3, '0');
                await db.ExecAsync(InsertSql, new { Id = id, LoginId = "t" + digits, Nickname = "t" + n }).ConfigureAwait(false);
            }
            await db.ExecAsync(BumpSequenceSql).ConfigureAwait(false);
        }
    }
}
