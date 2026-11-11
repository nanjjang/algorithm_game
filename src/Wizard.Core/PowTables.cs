namespace Wizard.Core
{
    // 표 값은 Generated/PowTables.g.cs(tools/Wizard.TableGen)에 있다. 여기는 고르는 길만 둔다.
    internal static partial class PowTables
    {
        internal static long[] For(PowBase which)
        {
            switch (which)
            {
                case PowBase.Enhance115: return Enhance115;
                case PowBase.Decay085: return Decay085;
                case PowBase.Decay092: return Decay092;
                case PowBase.Chain07: return Chain07;
                default: throw new SimInvariantException("unknown PowBase");
            }
        }
    }
}
