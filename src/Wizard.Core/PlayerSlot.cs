using System;

namespace Wizard.Core
{
    // CONTRACTS 1.5. 0..3. 무한 웨이브는 0 하나, 결투는 0(왼쪽)·1(오른쪽). 없음 = 255
    public readonly struct PlayerSlot : IEquatable<PlayerSlot>
    {
        public readonly byte Value;

        public static readonly PlayerSlot None = new PlayerSlot(255);

        public PlayerSlot(byte value)
        {
            Value = value;
        }

        public bool IsNone => Value == 255;

        public bool Equals(PlayerSlot other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is PlayerSlot other && other.Value == Value;

        public override int GetHashCode() => Value;

        public static bool operator ==(PlayerSlot a, PlayerSlot b) => a.Value == b.Value;

        public static bool operator !=(PlayerSlot a, PlayerSlot b) => a.Value != b.Value;
    }
}
