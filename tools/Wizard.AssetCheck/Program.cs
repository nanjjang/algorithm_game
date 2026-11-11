namespace Wizard.AssetCheck
{
    // 빈 진입점. 실행 파일 프로젝트는 진입점이 없으면 CS5001로 빌드가 깨진다(ARCHITECTURE 3.4).
    // 실제 내용은 M9-AST-02 이 채운다. 그 전에는 할 일이 없다는 것을 알리고 0이 아닌 코드로 끝난다.
    public static class Program
    {
        public static int Main(string[] args)
        {
            System.Console.Error.WriteLine("assetcheck: not implemented yet (M9-AST-02)");
            return 2;
        }
    }
}
