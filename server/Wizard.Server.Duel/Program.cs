using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Builder;

namespace Wizard.Server.Duel
{
    // wizard-duel 의 진입점(ARCHITECTURE 3.4, T-33). 이 파일은 M0-BT-01 만 고친다.
    // 기능은 모듈 등록으로 붙는다: 이 어셈블리에서 IHostModule 을 구현한 클래스를 찾아 이름(서수) 순으로 부른다.
    //   UDP 소켓 루프, 경기 수명주기, 내부 API, 웹소켓 대체 경로는 M14-SRV-01 이 Host/ 에 더한다.
    // 이 프로세스는 DB 에 접속하지 않는다(Wizard.Server.Data 를 참조하지 않는다. 계층 검사).
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
                Console.Error.WriteLine("wizard-duel: no host modules registered yet (M14-SRV-01)");
                return 2;
            }
            BuildApp(args).Run();
            return 0;
        }

        public static WebApplication BuildApp(string[] args)
        {
            IReadOnlyList<IHostModule> modules = FindModules(typeof(Program).Assembly);
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            foreach (IHostModule module in modules)
            {
                module.ConfigureServices(builder);
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
    }
}
