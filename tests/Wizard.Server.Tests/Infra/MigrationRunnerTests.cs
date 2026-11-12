using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wizard.Server.Data;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // DECISIONS-TECH T-24, M10-DB-01 수락 1: 번호순으로 한 번씩만 적용하고, 체크섬이 바뀐 파일을 거절한다
    public sealed class MigrationRunnerTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "wz-mig-" + Guid.NewGuid().ToString("N"));

        public MigrationRunnerTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        private void Write(string name, string sql) => File.WriteAllText(Path.Combine(_dir, name), sql, new UTF8Encoding(false));

        [Fact]
        public async Task AppliesInNumberOrder_OnlyOnce()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            // 파일 이름의 사전순과 번호순이 같지만, 두 번째가 첫 번째의 표에 기대므로 순서가 틀리면 실패한다
            Write("0002_second.sql", "INSERT INTO t (v) VALUES ('second');");
            Write("0001_first.sql", "CREATE TABLE t (n serial PRIMARY KEY, v text NOT NULL); INSERT INTO t (v) VALUES ('first');");
            Write("0010_third.sql", "INSERT INTO t (v) VALUES ('third');");
            var runner = new MigrationRunner(db.DataSource);

            IReadOnlyList<MigrationFile> first = await runner.ApplyAsync(_dir, CancellationToken.None);
            Assert.Equal(new[] { 1, 2, 10 }, first.Select(f => f.Version).ToArray());
            IReadOnlyList<MigrationFile> again = await runner.ApplyAsync(_dir, CancellationToken.None);
            Assert.Empty(again);

            List<string> rows = await db.QueryAsync<string>("SELECT v FROM t ORDER BY n");
            Assert.Equal(new[] { "first", "second", "third" }, rows.ToArray());
            List<int> versions = await db.QueryAsync<int>("SELECT version FROM schema_migration ORDER BY version");
            Assert.Equal(new[] { 1, 2, 10 }, versions.ToArray());
        }

        [Fact]
        public async Task NewHigherNumber_IsAppliedLater()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            Write("0001_a.sql", "CREATE TABLE t (v int);");
            var runner = new MigrationRunner(db.DataSource);
            await runner.ApplyAsync(_dir, CancellationToken.None);
            Write("0002_b.sql", "INSERT INTO t VALUES (7);");
            IReadOnlyList<MigrationFile> applied = await runner.ApplyAsync(_dir, CancellationToken.None);
            Assert.Equal(new[] { 2 }, applied.Select(f => f.Version).ToArray());
            Assert.Equal(7, await db.ScalarAsync<int>("SELECT v FROM t"));
        }

        [Fact]
        public async Task ChangedChecksum_IsRejected_AndNothingElseIsApplied()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            Write("0001_a.sql", "CREATE TABLE t (v int);");
            var runner = new MigrationRunner(db.DataSource);
            await runner.ApplyAsync(_dir, CancellationToken.None);

            Write("0001_a.sql", "CREATE TABLE t (v int); -- edited after apply");
            Write("0002_b.sql", "CREATE TABLE u (v int);");
            MigrationException ex = await Assert.ThrowsAsync<MigrationException>(() => runner.ApplyAsync(_dir, CancellationToken.None));
            Assert.Contains("checksum", ex.Message, StringComparison.Ordinal);
            Assert.Null(await db.ScalarAsync<string?>("SELECT to_regclass('public.u')::text"));
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM schema_migration"));
        }

        [Fact]
        public async Task LineEndingsAndBom_DoNotChangeTheChecksum()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            File.WriteAllText(Path.Combine(_dir, "0001_a.sql"), "CREATE TABLE t (v int);\nINSERT INTO t VALUES (1);\n", new UTF8Encoding(false));
            var runner = new MigrationRunner(db.DataSource);
            await runner.ApplyAsync(_dir, CancellationToken.None);
            File.WriteAllText(Path.Combine(_dir, "0001_a.sql"), "CREATE TABLE t (v int);\r\nINSERT INTO t VALUES (1);\r\n", new UTF8Encoding(true));
            Assert.Empty(await runner.ApplyAsync(_dir, CancellationToken.None));
        }

        [Fact]
        public async Task MissingAppliedFile_IsRejected()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            Write("0001_a.sql", "CREATE TABLE t (v int);");
            var runner = new MigrationRunner(db.DataSource);
            await runner.ApplyAsync(_dir, CancellationToken.None);
            File.Delete(Path.Combine(_dir, "0001_a.sql"));
            Write("0002_b.sql", "CREATE TABLE u (v int);");
            await Assert.ThrowsAsync<MigrationException>(() => runner.ApplyAsync(_dir, CancellationToken.None));
            Assert.Null(await db.ScalarAsync<string?>("SELECT to_regclass('public.u')::text"));
        }

        [Fact]
        public async Task RenamedAppliedFile_IsRejected()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            Write("0001_a.sql", "CREATE TABLE t (v int);");
            var runner = new MigrationRunner(db.DataSource);
            await runner.ApplyAsync(_dir, CancellationToken.None);
            File.Move(Path.Combine(_dir, "0001_a.sql"), Path.Combine(_dir, "0001_renamed.sql"));
            await Assert.ThrowsAsync<MigrationException>(() => runner.ApplyAsync(_dir, CancellationToken.None));
        }

        [Fact]
        public async Task NewFileNumberedBelowApplied_IsRejected()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            Write("0002_b.sql", "CREATE TABLE t (v int);");
            var runner = new MigrationRunner(db.DataSource);
            await runner.ApplyAsync(_dir, CancellationToken.None);
            Write("0001_a.sql", "CREATE TABLE u (v int);");
            await Assert.ThrowsAsync<MigrationException>(() => runner.ApplyAsync(_dir, CancellationToken.None));
            Assert.Null(await db.ScalarAsync<string?>("SELECT to_regclass('public.u')::text"));
        }

        [Theory]
        [InlineData("1_a.sql")]
        [InlineData("0001-a.sql")]
        [InlineData("0001_A.sql")]
        [InlineData("00001_a.sql")]
        [InlineData("0001_.sql")]
        public void BadFileName_IsRejected(string name)
        {
            Write(name, "SELECT 1;");
            Assert.Throws<MigrationException>(() => MigrationFile.LoadDirectory(_dir));
        }

        [Fact]
        public void DuplicateNumber_IsRejected()
        {
            Write("0001_a.sql", "SELECT 1;");
            Write("0001_b.sql", "SELECT 1;");
            Assert.Throws<MigrationException>(() => MigrationFile.LoadDirectory(_dir));
        }

        [Fact]
        public async Task FailingMigration_LeavesNoTraceAndStopsTheRest()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            Write("0001_a.sql", "CREATE TABLE t (v int);");
            Write("0002_b.sql", "CREATE TABLE u (v int); SELECT no_such_function();");
            Write("0003_c.sql", "CREATE TABLE w (v int);");
            var runner = new MigrationRunner(db.DataSource);
            await Assert.ThrowsAnyAsync<Npgsql.PostgresException>(() => runner.ApplyAsync(_dir, CancellationToken.None));
            Assert.NotNull(await db.ScalarAsync<string?>("SELECT to_regclass('public.t')::text"));
            Assert.Null(await db.ScalarAsync<string?>("SELECT to_regclass('public.u')::text"));
            Assert.Null(await db.ScalarAsync<string?>("SELECT to_regclass('public.w')::text"));
            List<int> versions = await db.QueryAsync<int>("SELECT version FROM schema_migration ORDER BY version");
            Assert.Equal(new[] { 1 }, versions.ToArray());
        }

        [Fact]
        public async Task ConcurrentRunners_ApplyEachFileOnce()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            Write("0001_a.sql", "CREATE TABLE t (v int); SELECT pg_sleep(0.2);");
            Write("0002_b.sql", "INSERT INTO t VALUES (1);");
            Task<IReadOnlyList<MigrationFile>>[] runs = Enumerable.Range(0, 4)
                .Select(_ => Task.Run(() => new MigrationRunner(db.DataSource).ApplyAsync(_dir, CancellationToken.None)))
                .ToArray();
            IReadOnlyList<MigrationFile>[] results = await Task.WhenAll(runs);
            Assert.Equal(2, results.Sum(r => r.Count));
            Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM t"));
        }

        [Fact]
        public async Task RepositoryMigrations_ApplyToAnEmptyDatabase_AndAreIdempotent()
        {
            await using PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync();
            var runner = new MigrationRunner(db.DataSource);
            IReadOnlyList<MigrationFile> applied = await runner.ApplyAsync(PgTestDatabase.MigrationsDirectory, CancellationToken.None);
            Assert.Contains(applied, f => f.Name == "0001_core.sql");
            Assert.Empty(await runner.ApplyAsync(PgTestDatabase.MigrationsDirectory, CancellationToken.None));
        }
    }
}
