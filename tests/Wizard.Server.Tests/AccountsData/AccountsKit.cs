using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Wizard.Server.Data;
using Wizard.Server.Data.Accounts;
using Wizard.Server.Tests.Infra;

namespace Wizard.Server.Tests.AccountsData
{
    // 계정 묶음 시험의 준비물. 키는 시험마다 무작위로 만든다(저장소에 키를 남기지 않는다)
    internal static class AccountsKit
    {
        public const string PwParamsJson =
            "{\"alg\":\"argon2id\",\"v\":19,\"m_kib\":65536,\"t\":3,\"p\":1,\"salt_len\":16,\"out_len\":32,\"pepper_id\":1,\"pepper_mode\":\"secret\"}";

        public static readonly CancellationToken None = CancellationToken.None;

        public static EmailProtector NewProtector() => NewProtector(1, out _, out _);

        public static EmailProtector NewProtector(byte keyId, out byte[] encKey, out byte[] lookupKey)
        {
            encKey = RandomNumberGenerator.GetBytes(EmailProtector.KeyBytes);
            lookupKey = RandomNumberGenerator.GetBytes(EmailProtector.MinLookupKeyBytes);
            return new EmailProtector(keyId, new Dictionary<byte, byte[]> { [keyId] = encKey }, lookupKey);
        }

        // 비밀번호 원문은 저장소에 넘기지 않는다. 시험은 해시 자리에 아무 바이트나 쓴다(Argon2id 는 M10-ACC-02)
        public static byte[] FakePwHash(string seed) => SHA256.HashData(Encoding.UTF8.GetBytes("pw:" + seed));

        public static NewAccount Account(string loginId, string nickname, string email) =>
            new NewAccount(loginId, FakePwHash(loginId), PwParamsJson, email, nickname);

        public static async Task<long> CreateAsync(PgTestDatabase db, PgAccountStore store, string loginId, string nickname, string email)
        {
            await using IUnitOfWork uow = await db.App.BeginAsync(None);
            AccountCreateResult r = await store.CreateAsync(uow, Account(loginId, nickname, email), db.Clock.UtcNow);
            if (!r.Created)
            {
                throw new InvalidOperationException("test setup: account " + loginId + " was not created (" + r.Outcome + ")");
            }
            await uow.CommitAsync(None);
            return r.AccountId;
        }

        // 저장소를 거치지 않는 확인용 조회(같은 트랜잭션)
        public static async Task<T?> ScalarAsync<T>(IUnitOfWork uow, string sql, object? param = null) =>
            await uow.Connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, param, uow.Transaction));

        public static byte[] Hash(string s) => SHA256.HashData(Encoding.UTF8.GetBytes(s));
    }
}
