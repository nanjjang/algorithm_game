using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Wizard.Server.Api
{
    // wizard-api 의 진입점(ARCHITECTURE 3.4, T-33, T-43). 이 파일은 M0-BT-01 만 고친다.
    // 기능은 모듈 등록으로 붙는다: 이 어셈블리에서 IHostModule 을 구현한 클래스를 찾아 이름(서수) 순으로 부른다.
    //   호스트(인증, 멱등 필터, 오류 대응, 건강 검사, 엔드포인트·서비스 모듈 찾기)는 M10-API-01 이 Host/ 에 IHostModule 하나로 더한다.
    // 이 프로젝트는 Wizard.Server.Payments.Fakes 를 참조하지 않는다(T-43). 개발용 호스트(Api.DevHost)만
    // 가짜 제공자 어셈블리를 startupAssemblies 로 넘기고, 그 어셈블리의 [assembly: HostingStartup] 이 서비스를 더한다.
    public static class Program
    {
        // 호스트 모듈. 공개 매개변수 없는 생성자가 있어야 한다.
        public interface IHostModule
        {
            void ConfigureServices(WebApplicationBuilder builder);

            void Configure(WebApplication app);
        }

        public static int Main(string[] args)
        {
            if (FindModules(typeof(Program).Assembly).Count == 0)
            {
                Console.Error.WriteLine("wizard-api: no host modules registered yet (M10-API-01)");
                return 2;
            }
            BuildApp(args).Run();
            return 0;
        }

        // startupAssemblies: 그 어셈블리의 HostingStartup 을 모듈 다음에 적용한다(개발용 호스트의 가짜 제공자 등록)
        public static WebApplication BuildApp(string[] args, params Assembly[] startupAssemblies)
        {
            IReadOnlyList<IHostModule> modules = FindModules(typeof(Program).Assembly);
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            foreach (IHostModule module in modules)
            {
                module.ConfigureServices(builder);
            }
            foreach (Assembly assembly in startupAssemblies)
            {
                ApplyHostingStartups(assembly, builder.WebHost);
            }
            WebApplication app = builder.Build();
            foreach (IHostModule module in modules)
            {
                module.Configure(app);
            }
            return app;
        }

        public static IReadOnlyList<IHostModule> FindModules(Assembly assembly)
        {
            var types = new List<Type>();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IHostModule).IsAssignableFrom(type))
                {
                    types.Add(type);
                }
            }
            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            var modules = new List<IHostModule>(types.Count);
            foreach (Type type in types)
            {
                modules.Add((IHostModule)(Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException("cannot create host module " + type.FullName)));
            }
            return modules;
        }

        private static void ApplyHostingStartups(Assembly assembly, IWebHostBuilder webHost)
        {
            var types = new List<Type>();
            foreach (HostingStartupAttribute attribute in assembly.GetCustomAttributes<HostingStartupAttribute>())
            {
                types.Add(attribute.HostingStartupType);
            }
            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            foreach (Type type in types)
            {
                var startup = (IHostingStartup)(Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException("cannot create hosting startup " + type.FullName));
                startup.Configure(webHost);
            }
        }
    }
}
