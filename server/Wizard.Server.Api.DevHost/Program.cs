using System;
using System.Reflection;

namespace Wizard.Server.Api.DevHost
{
    // 개발용 호스트(T-43): wizard-api 의 호스트에 가짜 결제 제공자를 더해 띄운다. 배포하지 않는다. 이 파일은 M0-BT-01 만 고친다.
    // 가짜 제공자의 등록은 Wizard.Server.Payments.Fakes(M17-PAY-02)가 자기 어셈블리의 [assembly: HostingStartup(...)] 으로 한다.
    // 여기서는 그 어셈블리를 이름으로 읽어 넘기기만 한다(이 파일이 Fakes 의 타입 이름에 기대지 않는다).
    public static class Program
    {
        public const string FakesAssemblyName = "Wizard.Server.Payments.Fakes";

        public static int Main(string[] args)
        {
            if (Wizard.Server.Api.Program.FindModules(typeof(Wizard.Server.Api.Program).Assembly).Count == 0)
            {
                Console.Error.WriteLine("wizard-api (dev host): no host modules registered yet (M10-API-01)");
                return 2;
            }
            Wizard.Server.Api.Program.BuildApp(args, LoadFakes()).Run();
            return 0;
        }

        public static Assembly LoadFakes() => Assembly.Load(new AssemblyName(FakesAssemblyName));
    }
}
