using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Wizard.Server.Api
{
    // wizard-api 의 빈 호스트(ARCHITECTURE 3.4). 엔드포인트·서비스는 뒤 작업이 모듈 등록으로 더한다(T-33).
    // 이 프로젝트는 Wizard.Server.Payments.Fakes 를 참조하지 않는다(T-43). 가짜 결제는 Api.DevHost 에서만 등록한다.
    public static class Program
    {
        public static void Main(string[] args)
        {
            WebApplication app = BuildApp(args);
            app.Run();
        }

        public static WebApplication BuildApp(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            WebApplication app = builder.Build();
            app.MapGet("/healthz", () => Results.Ok());
            return app;
        }
    }
}
