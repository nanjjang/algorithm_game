using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Wizard.Server.Data;
using Wizard.TestKit;

namespace Wizard.Server.Tests.Infra
{
    // 테스트 하나의 임시 데이터베이스(T-38). 만들 때 이름을 무작위로 정하고 DisposeAsync 가 지운다(DROP DATABASE … WITH (FORCE)).
    //   CreateEmptyAsync    — 빈 DB(마이그레이션 실행기 시험)
    //   CreateMigratedAsync — server/migrations 전부 + 아직 다른 작업이 만들지 않은 표의 대역(StandInTables)
    // App 은 SET LOCAL ROLE wizard_app 으로 도는 서버 프로세스의 트랜잭션, Owner 는 소유자(관리 연결 = 슈퍼유저)의 트랜잭션이다.
    public sealed class PgTestDatabase : IAsyncDisposable
    {
        private static readonly Lazy<Task<NpgsqlDataSource>> AdminSource = new Lazy<Task<NpgsqlDataSource>>(async () =>
            NpgsqlDataSource.Create(await PgTestServer.AdminConnectionStringAsync().ConfigureAwait(false)));

        private PgTestDatabase(string name, string connectionString)
        {
            Name = name;
            ConnectionString = connectionString;
            DataSource = NpgsqlDataSource.Create(connectionString);
            Owner = new PgUnitOfWorkFactory(DataSource, null);
            App = new PgUnitOfWorkFactory(DataSource, DbRoles.App);
            Clock = new FakeClock(new DateTime(2026, 11, 12, 3, 0, 0, DateTimeKind.Utc));
            Ledger = new PgLedger(Clock);
            Idempotency = new PgIdempotency(App, Clock);
            Locks = new PgRowLocks();
        }

        public string Name { get; }
        public string ConnectionString { get; }
        public NpgsqlDataSource DataSource { get; }
        public IUnitOfWorkFactory Owner { get; }
        public IUnitOfWorkFactory App { get; }
        public FakeClock Clock { get; }
        public PgLedger Ledger { get; }
        public PgIdempotency Idempotency { get; }
        public PgRowLocks Locks { get; }

        public static string MigrationsDirectory => TestRepo.Path("server/migrations");

        public static async Task<PgTestDatabase> CreateEmptyAsync()
        {
            NpgsqlDataSource admin = await AdminSource.Value.ConfigureAwait(false);
            string name = "wz_t_" + Guid.NewGuid().ToString("N");
            await using (NpgsqlConnection c = await admin.OpenConnectionAsync().ConfigureAwait(false))
            {
                // 이름은 위에서 만든 영숫자뿐이다(식별자는 매개변수가 될 수 없다)
                await c.ExecuteAsync("CREATE DATABASE \"" + name + "\"").ConfigureAwait(false);
            }
            var b = new NpgsqlConnectionStringBuilder(await PgTestServer.AdminConnectionStringAsync().ConfigureAwait(false)) { Database = name };
            return new PgTestDatabase(name, b.ConnectionString);
        }

        public static async Task<PgTestDatabase> CreateMigratedAsync()
        {
            PgTestDatabase db = await CreateEmptyAsync().ConfigureAwait(false);
            try
            {
                await new MigrationRunner(db.DataSource).ApplyAsync(MigrationsDirectory, CancellationToken.None).ConfigureAwait(false);
                await StandInTables.EnsureAsync(db).ConfigureAwait(false);
                return db;
            }
            catch
            {
                await db.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        // 소유자 연결로 한 문장(테스트의 준비와 확인)
        public async Task<int> ExecAsync(string sql, object? param = null)
        {
            await using NpgsqlConnection c = await DataSource.OpenConnectionAsync().ConfigureAwait(false);
            return await c.ExecuteAsync(sql, param).ConfigureAwait(false);
        }

        public async Task<T?> ScalarAsync<T>(string sql, object? param = null)
        {
            await using NpgsqlConnection c = await DataSource.OpenConnectionAsync().ConfigureAwait(false);
            return await c.ExecuteScalarAsync<T>(sql, param).ConfigureAwait(false);
        }

        public async Task<System.Collections.Generic.List<T>> QueryAsync<T>(string sql, object? param = null)
        {
            await using NpgsqlConnection c = await DataSource.OpenConnectionAsync().ConfigureAwait(false);
            return new System.Collections.Generic.List<T>(await c.QueryAsync<T>(sql, param).ConfigureAwait(false));
        }

        // 다른 세션을 강제로 끊는다(서버 강제 종료의 흉내, T-IDEM-03)
        public static async Task TerminateBackendAsync(int pid)
        {
            NpgsqlDataSource admin = await AdminSource.Value.ConfigureAwait(false);
            await using NpgsqlConnection c = await admin.OpenConnectionAsync().ConfigureAwait(false);
            await c.ExecuteAsync("SELECT pg_terminate_backend(@Pid)", new { Pid = pid }).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await DataSource.DisposeAsync().ConfigureAwait(false);
            NpgsqlDataSource admin = await AdminSource.Value.ConfigureAwait(false);
            await using NpgsqlConnection c = await admin.OpenConnectionAsync().ConfigureAwait(false);
            await c.ExecuteAsync("DROP DATABASE IF EXISTS \"" + Name + "\" WITH (FORCE)").ConfigureAwait(false);
        }
    }
}
