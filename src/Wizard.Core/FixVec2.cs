using System;

namespace Wizard.Core
{
    // CONTRACTS 1.1. +x 오른쪽, +y 아래(전장 좌표: (0,0) 왼쪽 위, (24,14) 오른쪽 아래)
    public readonly struct FixVec2 : IEquatable<FixVec2>
    {
        public readonly Fix64 X, Y;

        public static readonly FixVec2 Zero = new FixVec2(Fix64.Zero, Fix64.Zero);

        public FixVec2(Fix64 x, Fix64 y)
        {
            X = x;
            Y = y;
        }

        public static FixVec2 operator +(FixVec2 a, FixVec2 b) => new FixVec2(a.X + b.X, a.Y + b.Y);

        public static FixVec2 operator -(FixVec2 a, FixVec2 b) => new FixVec2(a.X - b.X, a.Y - b.Y);

        public static FixVec2 operator *(FixVec2 a, Fix64 s) => new FixVec2(a.X * s, a.Y * s);

        public static bool operator ==(FixVec2 a, FixVec2 b) => a.X == b.X && a.Y == b.Y;

        public static bool operator !=(FixVec2 a, FixVec2 b) => !(a == b);

        public static Fix64 Dot(FixVec2 a, FixVec2 b) => a.X * b.X + a.Y * b.Y;

        public static Fix64 Cross(FixVec2 a, FixVec2 b) => a.X * b.Y - a.Y * b.X;

        public Fix64 LengthSq() => X * X + Y * Y;

        public Fix64 Length() => FixMath.Sqrt(LengthSq());

        // 길이 0이면 (0,0). 성분 = (성분.Raw << 32) / len.Raw 를 128비트로, 0 쪽 버림
        public FixVec2 Normalized()
        {
            Fix64 len = Length();
            if (len.Raw == 0L)
            {
                return Zero;
            }
            return new FixVec2(X / len, Y / len);
        }

        public static Fix64 DistanceSq(FixVec2 a, FixVec2 b) => (a - b).LengthSq();

        // (cos a, −sin a). y가 아래로 늘기 때문에 부호를 뒤집는다
        public static FixVec2 FromAngle(Angle a) => new FixVec2(FixMath.Cos(a), -FixMath.Sin(a));

        public bool Equals(FixVec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is FixVec2 other && Equals(other);

        public override int GetHashCode() => unchecked(X.GetHashCode() * 31 + Y.GetHashCode());
    }
}
