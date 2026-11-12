using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Wizard.Content;
using Wizard.Server.Data;

namespace Wizard.Server.Tests.Infra
{
    // spec 12 14.11절 벡터를 이 작업의 기반(UnitOfWork, IIdempotency, ILedger, IRowLocks)만으로 돌리는 시험용 유스케이스 모양.
    // 진짜 강화·우편·장터(M10-ENH-01, M13-MAIL-01, M13-MKT-01)와 같은 트랜잭션 순서를 따르되, 그 작업들의 표 대신
    // probe_* 표를 쓴다. 이 표들은 테스트 DB 에만 있다.
    public static class Probes
    {
        public const string Stone = "mat.stone_low";
        public const long EnhanceGoldCost = 100;

        private const string Sql = @"
CREATE TABLE probe_enhance_log (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, account_id bigint NOT NULL, request_id uuid NOT NULL, roll integer NOT NULL);
CREATE TABLE probe_mail (id bigint PRIMARY KEY, account_id bigint NOT NULL, gold bigint NOT NULL, claimed_at timestamptz NULL);
CREATE TABLE probe_listing (id bigint PRIMARY KEY, seller_id bigint NOT NULL, qty bigint NOT NULL CHECK (qty >= 0), unit_price bigint NOT NULL);
GRANT SELECT, INSERT ON probe_enhance_log TO wizard_app;
GRANT SELECT, UPDATE ON probe_mail, probe_listing TO wizard_app;";

        public static Task CreateAsync(PgTestDatabase db) => db.ExecAsync(Sql);

        public static async Task AddAccountsAsync(PgTestDatabase db, params long[] ids)
        {
            foreach (long id in ids)
            {
                await db.ExecAsync("INSERT INTO account (id) VALUES (@Id) ON CONFLICT DO NOTHING", new { Id = id });
            }
        }

        // 시작 잔액을 원장으로 준다(원장 합 = 잔액이 처음부터 맞게)
        public static async Task GrantAsync(PgTestDatabase db, long accountId, long gold, long stones)
        {
            await using IUnitOfWork uow = await db.App.BeginAsync(CancellationToken.None);
            var r = new LedgerRef(LedgerReason.Admin, "test", "setup", null);
            if (gold != 0)
            {
                await db.Ledger.AddCurrencyAsync(uow, accountId, CurrencyCode.Gold, CurrencyBucket.Earned, gold, false, r);
            }
            if (stones != 0)
            {
                await db.Ledger.AddItemAsync(uow, accountId, ItemRefs.Stack(Stone), stones, false, r);
            }
            await uow.CommitAsync(CancellationToken.None);
        }

        public static byte[] Fingerprint(string kind, string canonicalBody) =>
            SHA256.HashData(Encoding.UTF8.GetBytes(kind + "\n" + canonicalBody));

        // 강화의 트랜잭션 모양(ARCHITECTURE 6.3절): 계정 잠금 → roll → 골드·강화석 차감 → 기록 1줄. roll 은 실행마다 새 값이라 다시 굴리면 드러난다
        public static async Task<ProbeEnhanceResult> EnhanceAsync(PgTestDatabase db, IUnitOfWork uow, long accountId, Guid requestId, int roll)
        {
            await db.Locks.LockAccountsAsync(uow, accountId);
            var r = new LedgerRef(LedgerReason.Enhance, "probe_enhance", requestId.ToString("D"), requestId);
            long goldAfter = await db.Ledger.AddCurrencyAsync(uow, accountId, CurrencyCode.Gold, CurrencyBucket.Earned, -EnhanceGoldCost, false, r);
            await db.Ledger.AddItemAsync(uow, accountId, ItemRefs.Stack(Stone), -1, false, r);
            await uow.Connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO probe_enhance_log (account_id, request_id, roll) VALUES (@A, @R, @Roll)",
                new { A = accountId, R = requestId, Roll = roll }, uow.Transaction));
            return new ProbeEnhanceResult { Roll = roll, GoldAfter = goldAfter, Outcome = "success" };
        }

        // 우편 수령의 모양(spec 12 8.2절 mail 주석): UPDATE … WHERE claimed_at IS NULL 이 1행일 때만 지급
        public static async Task<bool> ClaimMailAsync(PgTestDatabase db, IUnitOfWork uow, long mailId)
        {
            MailRow? mail = await uow.Connection.QuerySingleOrDefaultAsync<MailRow>(new CommandDefinition(
                "UPDATE probe_mail SET claimed_at = now() WHERE id = @Id AND claimed_at IS NULL RETURNING account_id AS AccountId, gold AS Gold",
                new { Id = mailId }, uow.Transaction));
            if (mail == null)
            {
                return false;
            }
            await db.Ledger.AddCurrencyAsync(uow, mail.AccountId, CurrencyCode.Gold, CurrencyBucket.Earned, mail.Gold, false,
                new LedgerRef(LedgerReason.Mail, "mail", mailId.ToString(System.Globalization.CultureInfo.InvariantCulture), null));
            return true;
        }

        // 장터 구매의 모양: 두 계정을 번호 오름차순으로 잠그고 → 매물 행 FOR UPDATE → 수량 확인 → 대금 이동
        public static async Task<string> BuyAsync(PgTestDatabase db, IUnitOfWork uow, long buyerId, long listingId)
        {
            long sellerId = await uow.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT seller_id FROM probe_listing WHERE id = @Id", new { Id = listingId }, uow.Transaction));
            await db.Locks.LockAccountsAsync(uow, buyerId, sellerId);
            ListingRow listing = await uow.Connection.QuerySingleAsync<ListingRow>(new CommandDefinition(
                "SELECT qty AS Qty, unit_price AS UnitPrice FROM probe_listing WHERE id = @Id FOR UPDATE", new { Id = listingId }, uow.Transaction));
            if (listing.Qty < 1)
            {
                return "listing_gone";
            }
            await uow.Connection.ExecuteAsync(new CommandDefinition(
                "UPDATE probe_listing SET qty = qty - 1 WHERE id = @Id", new { Id = listingId }, uow.Transaction));
            var r = new LedgerRef(LedgerReason.MarketSale, "listing", listingId.ToString(System.Globalization.CultureInfo.InvariantCulture), null);
            await db.Ledger.AddCurrencyAsync(uow, buyerId, CurrencyCode.Gold, CurrencyBucket.Earned, -listing.UnitPrice, false, r);
            await db.Ledger.AddCurrencyAsync(uow, sellerId, CurrencyCode.Gold, CurrencyBucket.Earned, listing.UnitPrice, false, r);
            await db.Ledger.AddItemAsync(uow, buyerId, ItemRefs.Stack(Stone), 1, false, r);
            return "ok";
        }

        public static Task<long> GoldAsync(PgTestDatabase db, long accountId) => db.ScalarAsync<long>(
            "SELECT COALESCE((SELECT amount FROM currency_balance WHERE account_id = @A AND currency = 'gold' AND bucket = 'earned'), 0)",
            new { A = accountId });

        public static Task<long> StonesAsync(PgTestDatabase db, long accountId) => db.ScalarAsync<long>(
            "SELECT COALESCE((SELECT qty FROM item_stack WHERE account_id = @A AND item_code = @C), 0)", new { A = accountId, C = Stone });

        public sealed class MailRow
        {
            public long AccountId { get; set; }
            public long Gold { get; set; }
        }

        public sealed class ListingRow
        {
            public long Qty { get; set; }
            public long UnitPrice { get; set; }
        }
    }
}
