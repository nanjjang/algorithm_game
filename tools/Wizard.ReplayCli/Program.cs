using System;

namespace Wizard.ReplayCli
{
    // replaycli — .wzrc·.wzdr 재실행, 틱별 해시, 두 실행 비교, 골든(ARCHITECTURE 2, PIPELINE 4.4·7.3). 명령과 인자는 Cli.cs 의 도움말에 있다
    public static class Program
    {
        public static int Main(string[] args) => Cli.Run(args, Console.Out, Console.Error);
    }
}
