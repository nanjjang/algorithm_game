using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Wizard.Content;
using Wizard.Core;
using Wizard.Server.Data;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // M10-DB-01 수락 6: LedgerRef, ItemOrigin, WandRow 가 CONTRACTS 7.2절과 같다. ILedger 는 Wizard.Content 의 CurrencyCode·CurrencyBucket,
    // 오류는 Wizard.Core 의 ErrorCode 를 쓴다. 기대값은 CONTRACTS 7.2절·12.2절의 글 그대로다.
    public sealed class ContractShapeTests
    {
        [Fact]
        public void LedgerRef_IsAReadonlyStructWithTheFourContractFields()
        {
            Type t = typeof(LedgerRef);
            Assert.True(t.IsValueType);
            Assert.NotNull(t.GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>());
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance).ToDictionary(f => f.Name, f => f);
            Assert.Equal(new[] { "Reason", "RefId", "RefType", "RequestId" }, fields.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(typeof(LedgerReason), fields["Reason"].FieldType);
            Assert.Equal(typeof(string), fields["RefType"].FieldType);
            Assert.Equal(typeof(string), fields["RefId"].FieldType);
            Assert.Equal(typeof(Guid?), fields["RequestId"].FieldType);
            Assert.All(fields.Values, f => Assert.True(f.IsInitOnly));
        }

        [Fact]
        public void LedgerReason_HasTheContractValues()
        {
            string[] contract =
            {
                "Enhance", "ShopBuy", "RunSettle", "RunRevoke", "Trade", "MarketSale", "MarketFee", "Mail", "DuelReward", "SeasonReward",
                "SeasonCoinConvert", "NicknameChange", "Admin", "Payment", "Refund",
            };
            Assert.Equal(contract, Enum.GetNames(typeof(LedgerReason)).Take(contract.Length).ToArray());   // 추가만(뒤에 붙는다)
        }

        [Theory]
        [InlineData(LedgerReason.RunSettle, "run_settle")]
        [InlineData(LedgerReason.SeasonCoinConvert, "season_coin_convert")]
        [InlineData(LedgerReason.Refund, "refund")]
        [InlineData(LedgerReason.Admin, "admin")]
        public void LedgerReason_IsStoredAsSnakeCase(LedgerReason reason, string db)
        {
            Assert.Equal(db, DbNames.Reason(reason));
        }

        [Fact]
        public void ItemOrigin_IsAByteEnumWithTheContractNumbers()
        {
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(ItemOrigin)));
            var expected = new (string, byte)[] { ("Earned", 0), ("Shop", 1), ("Trade", 2), ("Season", 3), ("Revenge", 4), ("Admin", 5), ("Paid", 6) };
            var actual = Enum.GetValues(typeof(ItemOrigin)).Cast<ItemOrigin>().Select(v => (v.ToString(), (byte)v)).ToArray();
            Assert.Equal(expected, actual);
        }

        // CONTRACTS 12.2절 wand 행의 핵심 열(spec 12 8.2절 + 12.1절)
        private static readonly string[] WandColumns =
        {
            "id", "owner_id", "character", "stage", "floor_peak", "record_peak", "drop_count", "aura", "slots_extra", "carve_bonus", "mutations",
            "pending_offer", "loadout", "skin_id", "show_scars", "origin", "owner_count", "trade_locked_until", "lock_state", "pending_run_id",
            "debt_locked", "acquired_at", "version",
        };

        [Fact]
        public void WandRow_HasOnePropertyPerWandColumn()
        {
            string[] props = typeof(WandRow).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            string[] expected = WandColumns.Select(Pascal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, props);
            Assert.Equal(typeof(Guid?), typeof(WandRow).GetProperty("PendingRunId")!.PropertyType);   // 12.1절: uuid
            Assert.True(typeof(WandRow).IsSealed);
        }

        [Fact]
        public async Task WandStandIn_MatchesTheContractColumns()
        {
            // 대역 표가 계약과 어긋나면 LockWand 시험이 엉뚱한 표를 시험한다
            await using PgTestDatabase db = await PgTestDatabase.CreateMigratedAsync();
            List<string> cols = await db.QueryAsync<string>(
                "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'wand' ORDER BY column_name");
            Assert.Equal(WandColumns.OrderBy(c => c, StringComparer.Ordinal).ToArray(), cols.ToArray());
        }

        [Fact]
        public void ILedger_UsesContentCurrencyTypes_AndTheContractSignatures()
        {
            MethodInfo add = typeof(ILedger).GetMethod(nameof(ILedger.AddCurrencyAsync))!;
            Assert.Equal(typeof(Task<long>), add.ReturnType);
            Assert.Equal(new[] { typeof(IUnitOfWork), typeof(long), typeof(CurrencyCode), typeof(CurrencyBucket), typeof(long), typeof(bool), typeof(LedgerRef) },
                add.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.Equal(typeof(CurrencyCode).Assembly, typeof(Wizard.Content.ContentDb).Assembly);

            MethodInfo item = typeof(ILedger).GetMethod(nameof(ILedger.AddItemAsync))!;
            Assert.Equal(typeof(Task), item.ReturnType);
            Assert.Equal(new[] { typeof(IUnitOfWork), typeof(long), typeof(string), typeof(long), typeof(bool), typeof(LedgerRef) },
                item.GetParameters().Select(p => p.ParameterType).ToArray());

            MethodInfo spend = typeof(ILedger).GetMethod(nameof(ILedger.SpendStarlightAsync))!;
            Assert.Equal(typeof(Task), spend.ReturnType);
            Assert.Equal(new[] { typeof(IUnitOfWork), typeof(long), typeof(long), typeof(LedgerRef) },
                spend.GetParameters().Select(p => p.ParameterType).ToArray());

            Assert.Equal(3, typeof(ILedger).GetMethods().Length);
            Assert.True(typeof(ILedger).IsAssignableFrom(typeof(PgLedger)));
        }

        [Fact]
        public void LedgerErrors_UseCoreErrorCode()
        {
            Assert.Equal(typeof(ErrorCode), typeof(LedgerException).GetProperty(nameof(LedgerException.Code))!.PropertyType);
            Assert.Equal(typeof(ErrorCode), typeof(RowNotFoundException).GetProperty(nameof(RowNotFoundException.Code))!.PropertyType);
            Assert.Equal("Wizard.Core", typeof(ErrorCode).Assembly.GetName().Name);
            Assert.Equal((ushort)12, (ushort)ErrorCode.IdempotencyConflict);   // IdempotentResult.Conflict 를 유스케이스가 이 코드로 바꾼다
        }

        [Fact]
        public void IIdempotency_RunAsync_HasTheContractSignature()
        {
            MethodInfo run = typeof(IIdempotency).GetMethod(nameof(IIdempotency.RunAsync))!;
            Assert.True(run.IsGenericMethodDefinition);
            Type tResult = run.GetGenericArguments()[0];
            Assert.Equal(typeof(Task<>).MakeGenericType(typeof(IdempotentResult<>).MakeGenericType(tResult)), run.ReturnType);
            Assert.Equal(new[]
            {
                typeof(long), typeof(Guid), typeof(string), typeof(ReadOnlyMemory<byte>),
                typeof(Func<,>).MakeGenericType(typeof(IUnitOfWork), typeof(Task<>).MakeGenericType(tResult)), typeof(CancellationToken),
            }, run.GetParameters().Select(p => p.ParameterType).ToArray());

            Type r = typeof(IdempotentResult<int>);
            Assert.True(r.IsValueType);
            Assert.Equal(new[] { "Conflict", "Replayed", "Value" },
                r.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void IRowLocks_And_IUnitOfWork_HaveTheContractMembers()
        {
            MethodInfo accounts = typeof(IRowLocks).GetMethod(nameof(IRowLocks.LockAccountsAsync))!;
            ParameterInfo ids = accounts.GetParameters()[1];
            Assert.Equal(typeof(long[]), ids.ParameterType);
            Assert.NotNull(ids.GetCustomAttribute<ParamArrayAttribute>());
            Assert.Equal(typeof(Task<WandRow>), typeof(IRowLocks).GetMethod(nameof(IRowLocks.LockWandAsync))!.ReturnType);

            Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IUnitOfWork)));
            Assert.Equal(typeof(NpgsqlConnection), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Connection))!.PropertyType);
            Assert.Equal(typeof(NpgsqlTransaction), typeof(IUnitOfWork).GetProperty(nameof(IUnitOfWork.Transaction))!.PropertyType);
            Assert.Equal(typeof(Task<IUnitOfWork>), typeof(IUnitOfWorkFactory).GetMethod(nameof(IUnitOfWorkFactory.BeginAsync))!.ReturnType);
            Assert.Equal(typeof(DateTime), typeof(IClock).GetProperty(nameof(IClock.UtcNow))!.PropertyType);
        }

        private static string Pascal(string snake)
        {
            var sb = new StringBuilder(snake.Length);
            bool up = true;
            foreach (char c in snake)
            {
                if (c == '_')
                {
                    up = true;
                    continue;
                }
                sb.Append(up ? char.ToUpperInvariant(c) : c);
                up = false;
            }
            return sb.ToString();
        }
    }
}
