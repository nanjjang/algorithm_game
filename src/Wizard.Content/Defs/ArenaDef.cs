using System;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. 전장 24×14칸, 사방이 막혀 있다(DECISIONS 2.3)
    public sealed class ArenaDef
    {
        public Fix64 Width, Height;
        public FixVec2[] Pillars = Array.Empty<FixVec2>();
        public FixVec2[] SpawnPoints = Array.Empty<FixVec2>();
    }
}
