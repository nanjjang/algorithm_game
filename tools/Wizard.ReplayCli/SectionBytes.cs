using System;
using Wizard.Core.Hash;

namespace Wizard.ReplayCli
{
    // 한 세계 상태의 절 하나. Bytes 는 WriteState 안의 그 절의 바이트(절 id 포함). 생략된 풀 절은 Present == false, 빈 바이트
    public readonly struct SectionBytes
    {
        public SectionBytes(StateSectionInfo info, SectionShape shape, bool present, byte[] bytes)
        {
            Info = info;
            Shape = shape;
            Present = present;
            Bytes = bytes;
        }

        public StateSectionInfo Info { get; }

        public SectionShape Shape { get; }

        public bool Present { get; }

        public byte[] Bytes { get; }

        // 그 절 바이트의 FNV-1a 64(CONTRACTS 1.4 와 같은 알고리즘). 절이 생략되면 빈 입력의 값이다
        public ulong Digest => StateHasher.Compute(Bytes);

        public bool SameAs(in SectionBytes other) => Present == other.Present && Bytes.AsSpan().SequenceEqual(other.Bytes);
    }
}
