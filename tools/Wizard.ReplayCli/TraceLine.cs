using System.Collections.Generic;

namespace Wizard.ReplayCli
{
    // 추적 파일의 한 줄: world.Tick, 그때의 상태 해시, 있는 절의 다이제스트(절 id 오름차순)
    public sealed class TraceLine
    {
        public TraceLine(uint tick, ulong hash, SortedList<ushort, ulong> sections)
        {
            Tick = tick;
            Hash = hash;
            Sections = sections;
        }

        public uint Tick { get; }

        public ulong Hash { get; }

        public IReadOnlyDictionary<ushort, ulong> Sections { get; }
    }
}
