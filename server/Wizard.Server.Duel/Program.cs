namespace Wizard.Server.Duel
{
    // wizard-duel 의 빈 진입점(ARCHITECTURE 3.4). UDP 루프와 DuelHostCore 연결은 M2-NET-02 가 채운다.
    // 이 프로세스는 DB 에 접속하지 않는다(Wizard.Server.Data 를 참조하지 않는다).
    public static class Program
    {
        public static int Main(string[] args)
        {
            System.Console.Error.WriteLine("wizard-duel: not implemented yet (M2-NET-02)");
            return 2;
        }
    }
}
