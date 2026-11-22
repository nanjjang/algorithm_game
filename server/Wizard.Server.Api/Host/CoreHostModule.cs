using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wizard.Server.Data;

namespace Wizard.Server.Api.Host
{
    // 최소 호스트 모듈(M10-API-01 의 첫 조각): DB 연결과 GET /healthz(spec 12 §7, DB 연결까지 확인).
    // 인증·멱등 필터·요청 제한·구조화 로그는 M10-API-01 본 작업이 더한다.
    // 연결 문자열: ConnectionStrings:Wizard (환경 변수 ConnectionStrings__Wizard 로도 줄 수 있다).
    public sealed class CoreHostModule : Program.IHostModule
    {
        public void ConfigureServices(WebApplicationBuilder builder)
        {
            string connectionString = builder.Configuration.GetConnectionString("Wizard")
                ?? throw new InvalidOperationException("missing ConnectionStrings:Wizard");
            builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            builder.Services.AddSingleton<IUnitOfWorkFactory>(sp =>
                new PgUnitOfWorkFactory(sp.GetRequiredService<NpgsqlDataSource>(),
                    builder.Configuration["Wizard:SessionRole"]));
        }

        public void Configure(WebApplication app)
        {
            app.MapGet("/healthz", async (NpgsqlDataSource db, CancellationToken ct) =>
            {
                try
                {
                    await using NpgsqlCommand cmd = db.CreateCommand("SELECT 1");
                    await cmd.ExecuteScalarAsync(ct);
                    return Results.Ok(new { status = "ok", db = "ok" });
                }
                catch (NpgsqlException)
                {
                    return Results.Json(new { status = "degraded", db = "unreachable" }, statusCode: 503);
                }
            });
        }
    }
}
