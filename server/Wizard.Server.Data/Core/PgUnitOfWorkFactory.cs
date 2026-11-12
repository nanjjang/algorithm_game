using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2. 트랜잭션 하나 = 연결 하나(READ COMMITTED). 행은 SELECT … FOR UPDATE 로 먼저 잠근다(spec 12 1.3절).
    // sessionRole 을 주면 트랜잭션마다 SET LOCAL ROLE 을 한다: 로그인 역할이 소유자이거나 슈퍼유저여도
    // 그 트랜잭션은 그 역할(보통 wizard_app)의 권한으로만 돈다. 로그인 역할이 이미 wizard_app 의 구성원이면 null 로 둔다.
    public sealed class PgUnitOfWorkFactory : IUnitOfWorkFactory
    {
        // 역할 이름은 SQL 매개변수가 될 수 없는 식별자라 꼴을 먼저 확인하고 따옴표로 감싼다(설정 값이지 요청 값이 아니다).
        private static readonly Regex RoleName = new Regex("^[a-z_][a-z0-9_]*$", RegexOptions.CultureInvariant);

        private readonly NpgsqlDataSource _dataSource;
        private readonly string? _setRoleSql;

        public PgUnitOfWorkFactory(NpgsqlDataSource dataSource, string? sessionRole)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            if (sessionRole != null)
            {
                if (!RoleName.IsMatch(sessionRole))
                {
                    throw new ArgumentException("invalid role name", nameof(sessionRole));
                }
                _setRoleSql = "SET LOCAL ROLE \"" + sessionRole + "\"";
            }
        }

        public async Task<IUnitOfWork> BeginAsync(CancellationToken ct)
        {
            NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
            try
            {
                NpgsqlTransaction transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
                if (_setRoleSql != null)
                {
                    await using var cmd = new NpgsqlCommand(_setRoleSql, connection, transaction);
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                return new PgUnitOfWork(connection, transaction);
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
