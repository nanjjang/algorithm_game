using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace Wizard.Server.Data.Accounts
{
    // 계정 묶음 저장소들이 같이 쓰는 작은 도우미. 모든 문장은 호출자의 트랜잭션에서 돈다
    internal static class StoreSql
    {
        public static void Require(IUnitOfWork uow)
        {
            if (uow == null)
            {
                throw new ArgumentNullException(nameof(uow));
            }
        }

        // 서버 시각은 UTC 다(IClock.UtcNow). timestamptz 에 다른 Kind 를 넣으면 Npgsql 이 거절하므로 먼저 같은 뜻으로 막는다
        public static DateTime Utc(DateTime at)
        {
            if (at.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("server times are UTC (IClock.UtcNow)", nameof(at));
            }
            return at;
        }

        // inet 열. Dapper 가 IPAddress 매개변수를 모르므로 문자열로 넘기고 SQL 에서 ::inet 으로 바꾼다
        public static string IpText(System.Net.IPAddress ip)
        {
            if (ip == null)
            {
                throw new ArgumentNullException(nameof(ip));
            }
            return ip.ToString();
        }

        public static DateTime? UtcOrNull(DateTime? at) => at.HasValue ? Utc(at.Value) : (DateTime?)null;

        public static async Task<T?> One<T>(IUnitOfWork uow, string sql, object? param)
            where T : class
        {
            Require(uow);
            return await uow.Connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, param, uow.Transaction)).ConfigureAwait(false);
        }

        public static async Task<IReadOnlyList<T>> Many<T>(IUnitOfWork uow, string sql, object? param)
        {
            Require(uow);
            IEnumerable<T> rows = await uow.Connection.QueryAsync<T>(new CommandDefinition(sql, param, uow.Transaction)).ConfigureAwait(false);
            return rows.ToList();
        }

        public static async Task<int> Execute(IUnitOfWork uow, string sql, object? param)
        {
            Require(uow);
            return await uow.Connection.ExecuteAsync(new CommandDefinition(sql, param, uow.Transaction)).ConfigureAwait(false);
        }

        public static async Task<bool> Exactly1(IUnitOfWork uow, string sql, object? param) => await Execute(uow, sql, param).ConfigureAwait(false) == 1;

        public static async Task<T> Scalar<T>(IUnitOfWork uow, string sql, object? param)
            where T : struct
        {
            Require(uow);
            return await uow.Connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, param, uow.Transaction)).ConfigureAwait(false);
        }

        public static async Task<int> Count(IUnitOfWork uow, string sql, object? param)
        {
            long n = await Scalar<long>(uow, sql, param).ConfigureAwait(false);
            return checked((int)n);
        }
    }
}
