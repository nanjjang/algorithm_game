using System;

namespace Wizard.Core
{
    // CONTRACTS 1.5. 세계 하나에 카운터 하나. 생성 순서대로 1씩. 0 = 없음
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public readonly int Value;

        public static readonly EntityId None = new EntityId(0);

        public EntityId(int value)
        {
            Value = value;
        }

        public bool IsNone => Value == 0;

        public bool Equals(EntityId other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is EntityId other && other.Value == Value;

        public override int GetHashCode() => Value;

        public int CompareTo(EntityId other) => Value < other.Value ? -1 : (Value > other.Value ? 1 : 0);

        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;

        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    }
}
