using System;
using System.Globalization;

namespace Wizard.TestKit.Shared.Golden
{
    // 기대 해시 목록의 한 칸: 틱 번호와 그 틱이 끝난 뒤의 상태 해시(spec 01 3.4, CONTRACTS 1.4).
    // Label 은 사람이 읽는 표시(예: "wave5", "final")이고 비교하지 않는다.
    public readonly struct GoldenHash : IEquatable<GoldenHash>
    {
        public GoldenHash(uint tick, ulong hash, string? label)
        {
            Tick = tick;
            Hash = hash;
            Label = label;
        }

        public uint Tick { get; }
        public ulong Hash { get; }
        public string? Label { get; }

        public bool Equals(GoldenHash other) => Tick == other.Tick && Hash == other.Hash;

        public override bool Equals(object? obj) => obj is GoldenHash other && Equals(other);

        public override int GetHashCode() => unchecked((int)Tick * 397) ^ Hash.GetHashCode();

        public override string ToString()
        {
            string s = "tick " + Tick.ToString(CultureInfo.InvariantCulture) + " = " + HashText.Format(Hash);
            return Label == null ? s : s + " (" + Label + ")";
        }
    }
}
