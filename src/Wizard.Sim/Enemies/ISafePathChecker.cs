using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Enemies
{
    // CONTRACTS 3.6. R5. 정수만 쓴다(spec 05 10절)
    public interface ISafePathChecker
    {
        bool HasSafePath(FixVec2 playerPos, CharacterId character, ReadOnlySpan<TelegraphZone> zones,
        ReadOnlySpan<LingeringHazard> hazards, in TelegraphRequest candidate, uint now);
    }
}
