using System.Globalization;

namespace Wizard.ReplayCli
{
    // 두 상태에서 다른 절 하나. FirstByte 는 절 바이트(절 id 포함) 안에서 처음 다른 자리(길이만 다르면 짧은 쪽의 길이)
    public readonly struct SectionDifference
    {
        public SectionDifference(StateSectionInfo section, bool expectedPresent, bool actualPresent, int expectedLength, int actualLength, int firstByte)
        {
            Section = section;
            ExpectedPresent = expectedPresent;
            ActualPresent = actualPresent;
            ExpectedLength = expectedLength;
            ActualLength = actualLength;
            FirstByte = firstByte;
        }

        public StateSectionInfo Section { get; }

        public bool ExpectedPresent { get; }

        public bool ActualPresent { get; }

        public int ExpectedLength { get; }

        public int ActualLength { get; }

        public int FirstByte { get; }

        public override string ToString()
        {
            if (ExpectedPresent != ActualPresent)
            {
                return Section + ": " + (ExpectedPresent ? "missing in the second run" : "only in the second run");
            }
            return Section + ": first difference at byte " + FirstByte.ToString(CultureInfo.InvariantCulture) + " (lengths " +
                   ExpectedLength.ToString(CultureInfo.InvariantCulture) + " / " + ActualLength.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }
}
