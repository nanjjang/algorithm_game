using System.Collections.Generic;

namespace Wizard.DataBuild.Validate.Evasion
{
    // IEvasionSource 들이 모은 검산 대상
    public sealed class EvasionInput
    {
        public List<EvasionAttack> Attacks { get; } = new List<EvasionAttack>();

        public List<EvasionMover> Movers { get; } = new List<EvasionMover>();
    }
}
