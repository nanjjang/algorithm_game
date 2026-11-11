namespace Wizard.Core
{
    // CONTRACTS 1.1. FixMath.PowTable 의 표 선택. 1.15^n, 0.85^d, 0.92^d, 0.7^k
    public enum PowBase : byte { Enhance115 = 0, Decay085 = 1, Decay092 = 2, Chain07 = 3 }
}
