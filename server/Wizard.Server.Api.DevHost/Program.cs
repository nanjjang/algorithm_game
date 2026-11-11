namespace Wizard.Server.Api.DevHost
{
    // 개발용 호스트: Api 의 호스트에 가짜 결제 제공자를 등록해 띄운다(T-43). 배포하지 않는다.
    // 가짜 제공자 등록은 M17-PAY-01 이 더한다.
    public static class Program
    {
        public static void Main(string[] args)
        {
            Wizard.Server.Api.Program.BuildApp(args).Run();
        }
    }
}
