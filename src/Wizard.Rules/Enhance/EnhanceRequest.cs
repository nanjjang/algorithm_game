using System;
using System.Collections.Generic;

namespace Wizard.Rules.Enhance
{
    // CONTRACTS 4.2
    public sealed class EnhanceRequest
    {
        public IReadOnlyList<SacrificeItem> Sacrifices = Array.Empty<SacrificeItem>();
        public bool UseCharm;
    }
}
