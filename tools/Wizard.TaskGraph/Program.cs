using System;

namespace Wizard.TaskGraph
{
    // 작업 그래프 도구의 입구. 명령과 옵션은 Cli.cs 머리말.
    public static class Program
    {
        public static int Main(string[] args)
        {
            return Cli.Run(args, Console.Out, Console.Error);
        }
    }
}
