using System;
using Wizard.BalanceLab.Core;

namespace Wizard.BalanceLab.Feel
{
    // balancelab feel — GDD 16절 1단계 "손맛" 통과 기준 네 가지를 재는 명령(M1-TE-02).
    internal sealed class FeelCommand : Program.ICommand
    {
        public string Name => "feel";

        public int Run(string[] args) => FeelCli.Run(args, Console.Out, Console.Error);
    }
}
