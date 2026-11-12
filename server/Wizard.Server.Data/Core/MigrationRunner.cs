using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace Wizard.Server.Data
{
    // DECISIONS-TECH T-24: server/migrations/ 의 번호 붙은 SQL 을 번호순으로 한 번씩만 적용한다. 소유자 역할의 연결로 돈다.
    //  - 적용 기록은 schema_migration(version, name, checksum, applied_at). 파일 하나 = 트랜잭션 하나(파일과 기록이 같이 커밋된다).
    //  - 먼저 전부 검사하고 하나라도 어긋나면 아무것도 적용하지 않는다: 적용된 파일의 체크섬·이름이 바뀌었다, 적용된 파일이 없어졌다,
    //    적용된 가장 큰 번호보다 작은 번호의 새 파일이 있다(번호순이 깨진다).
    //  - 여러 프로세스가 동시에 띄워도 권고 잠금으로 하나씩 돈다.
    public sealed class MigrationRunner
    {
        private const string LockKey = "wizard.schema_migration";

        private const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS schema_migration (" +
            "version integer PRIMARY KEY, name text NOT NULL, checksum bytea NOT NULL, applied_at timestamptz NOT NULL DEFAULT now())";

        private const string SelectAppliedSql = "SELECT version, name, checksum FROM schema_migration ORDER BY version";

        private const string InsertAppliedSql = "INSERT INTO schema_migration (version, name, checksum) VALUES (@Version, @Name, @Checksum)";

        private readonly NpgsqlDataSource _ownerDataSource;

        public MigrationRunner(NpgsqlDataSource ownerDataSource)
        {
            _ownerDataSource = ownerDataSource ?? throw new ArgumentNullException(nameof(ownerDataSource));
        }

        // 이번에 적용한 파일을 번호순으로 돌려준다(이미 다 적용돼 있으면 빈 목록)
        public Task<IReadOnlyList<MigrationFile>> ApplyAsync(string directory, CancellationToken ct) =>
            ApplyAsync(MigrationFile.LoadDirectory(directory), ct);

        public async Task<IReadOnlyList<MigrationFile>> ApplyAsync(IReadOnlyList<MigrationFile> files, CancellationToken ct)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }
            for (int i = 1; i < files.Count; i++)
            {
                if (files[i].Version <= files[i - 1].Version)
                {
                    throw new MigrationException("migrations must be given in strictly increasing number order");
                }
            }

            await using NpgsqlConnection conn = await _ownerDataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
            await conn.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_lock(hashtext(@Key))", new { Key = LockKey }, cancellationToken: ct))
                .ConfigureAwait(false);
            try
            {
                await conn.ExecuteAsync(new CommandDefinition(CreateTableSql, cancellationToken: ct)).ConfigureAwait(false);
                List<AppliedMigration> applied = (await conn.QueryAsync<AppliedMigration>(new CommandDefinition(SelectAppliedSql, cancellationToken: ct))
                    .ConfigureAwait(false)).ToList();
                List<MigrationFile> pending = Plan(files, applied);

                var done = new List<MigrationFile>(pending.Count);
                foreach (MigrationFile file in pending)
                {
                    await using NpgsqlTransaction tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
                    await using (var cmd = new NpgsqlCommand(file.Sql, conn, tx))
                    {
                        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    await conn.ExecuteAsync(new CommandDefinition(InsertAppliedSql,
                        new { file.Version, file.Name, file.Checksum }, tx, cancellationToken: ct)).ConfigureAwait(false);
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    done.Add(file);
                }
                return done;
            }
            finally
            {
                // 연결이 끊겼으면 서버가 세션 잠금을 이미 풀었다. 원래 예외를 가리지 않는다
                if (conn.State == System.Data.ConnectionState.Open)
                {
                    await conn.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_unlock(hashtext(@Key))", new { Key = LockKey }))
                        .ConfigureAwait(false);
                }
            }
        }

        private static List<MigrationFile> Plan(IReadOnlyList<MigrationFile> files, List<AppliedMigration> applied)
        {
            var byVersion = new Dictionary<int, MigrationFile>();
            foreach (MigrationFile f in files)
            {
                byVersion.Add(f.Version, f);
            }
            var appliedVersions = new HashSet<int>();
            int maxApplied = -1;
            foreach (AppliedMigration a in applied)
            {
                if (!byVersion.TryGetValue(a.Version, out MigrationFile? f))
                {
                    throw new MigrationException("applied migration " + a.Name + " is missing from the migration directory");
                }
                if (!string.Equals(f.Name, a.Name, StringComparison.Ordinal))
                {
                    throw new MigrationException("applied migration " + a.Name + " was renamed to " + f.Name);
                }
                if (!f.Checksum.AsSpan().SequenceEqual(a.Checksum))
                {
                    throw new MigrationException("applied migration " + a.Name + " was changed after it was applied (checksum mismatch). " +
                                                 "Add a new numbered migration instead of editing an applied one");
                }
                appliedVersions.Add(a.Version);
                maxApplied = Math.Max(maxApplied, a.Version);
            }
            var pending = new List<MigrationFile>();
            foreach (MigrationFile f in files)
            {
                if (appliedVersions.Contains(f.Version))
                {
                    continue;
                }
                if (f.Version < maxApplied)
                {
                    throw new MigrationException("new migration " + f.Name + " is numbered below an already applied migration");
                }
                pending.Add(f);
            }
            return pending;
        }
    }
}
