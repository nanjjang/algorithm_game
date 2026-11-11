using System;

namespace Wizard.Client.Save
{
    // CONTRACTS 6.2
    public sealed class SaveLoad
    {
        public ReadOnlyMemory<byte> Utf8Json;
        public bool Restored;                             // 깨져서 직전 세대를 읽었다
    }
}
