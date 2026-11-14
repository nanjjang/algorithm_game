using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // M10-DB-01 수락 4, DECISIONS-TECH T-38: tools/ci/pg-test.sh 는 Docker 가 없거나 돌지 않으면 건너뛰지 않고 무엇이 없는지 적고 실패한다.
    // 테스트마다 임시 데이터베이스를 만들고 지운다.
    public sealed class PgTestScriptTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "wz-pgt-" + Guid.NewGuid().ToString("N"));

        public PgTestScriptTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        [Theory]
        [InlineData("up", "--print")]
        [InlineData("status", null)]
        [InlineData("down", null)]
        public async Task NoDocker_FailsWithWhatIsMissing_AndDoesNotSkip(string mode, string? arg)
        {
            var env = new Dictionary<string, string?> { ["WIZARD_DOCKER"] = Path.Combine(_dir, "no-such-docker") };
            ScriptResult r = arg == null ? await PgTestServer.RunScriptAsync(env, mode) : await PgTestServer.RunScriptAsync(env, mode, arg);
            Assert.Equal(1, r.ExitCode);
            Assert.Contains("FAIL", r.Stderr, StringComparison.Ordinal);
            Assert.Contains("Docker 가 없다", r.Stderr, StringComparison.Ordinal);
            Assert.DoesNotContain("skip", r.Stdout + r.Stderr, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("", r.Stdout);   // 연결 문자열 비슷한 것도 내지 않는다
        }

        [Fact]
        public async Task DockerNotRunning_FailsWithWhatToDo()
        {
            // docker info 가 실패하는 가짜 docker(데몬이 꺼진 Docker Desktop 과 같은 모양).
            // 맥·리눅스는 실행 권한이 있는 스크립트, Windows 는 같은 일을 하는 .cmd 다(T-67 1·7번. pg-test.sh 는 .cmd 도 실행 파일로 받는다)
            string fake;
            if (OperatingSystem.IsWindows())
            {
                fake = Path.Combine(_dir, "docker.cmd").Replace(Path.DirectorySeparatorChar, '/');
                File.WriteAllText(fake, "@echo off\r\necho Cannot connect to the Docker daemon 1>&2\r\nexit /b 1\r\n", new UTF8Encoding(false));
            }
            else
            {
                fake = Path.Combine(_dir, "docker");
                File.WriteAllText(fake, "#!/bin/bash\necho 'Cannot connect to the Docker daemon' >&2\nexit 1\n", new UTF8Encoding(false));
                File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            var env = new Dictionary<string, string?> { ["WIZARD_DOCKER"] = fake };
            ScriptResult r = await PgTestServer.RunScriptAsync(env, "up", "--print");
            Assert.Equal(1, r.ExitCode);
            Assert.Contains("FAIL", r.Stderr, StringComparison.Ordinal);
            Assert.Contains("Docker 가 돌고 있지 않다", r.Stderr, StringComparison.Ordinal);
            Assert.Equal("", r.Stdout);
        }

        [Fact]
        public async Task UnknownMode_IsAUsageError()
        {
            ScriptResult r = await PgTestServer.RunScriptAsync("bogus");
            Assert.Equal(2, r.ExitCode);
        }

        [Fact]
        public async Task Up_PrintsAConnectionToPostgres17OnLoopback()
        {
            ScriptResult r = await PgTestServer.RunScriptAsync("up", "--print");
            Assert.True(r.ExitCode == 0, r.Stderr);
            string[] lines = r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Single(lines);
            var b = new NpgsqlConnectionStringBuilder(lines[0]);
            Assert.Equal("127.0.0.1", b.Host);
            Assert.NotEqual(5432, b.Port);   // 빈 포트를 Docker 가 고른다(작업 트리마다 따로)
            await using NpgsqlDataSource ds = NpgsqlDataSource.Create(lines[0]);
            await using NpgsqlConnection c = await ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SHOW server_version_num", c);
            string version = (string)(await cmd.ExecuteScalarAsync())!;
            Assert.StartsWith("17", version, StringComparison.Ordinal);   // 배치용과 같은 주 판(T-38)
        }

        [Fact]
        public async Task EachTestDatabase_IsCreatedAndDropped()
        {
            string name;
            await using (PgTestDatabase db = await PgTestDatabase.CreateEmptyAsync())
            {
                name = db.Name;
                Assert.Equal(name, await db.ScalarAsync<string>("SELECT current_database()"));
            }
            await using NpgsqlDataSource admin = NpgsqlDataSource.Create(await PgTestServer.AdminConnectionStringAsync());
            await using NpgsqlConnection c = await admin.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @N", c);
            cmd.Parameters.AddWithValue("N", name);
            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
        }
    }
}
