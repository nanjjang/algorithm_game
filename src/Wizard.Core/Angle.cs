using System;

namespace Wizard.Core
{
    // CONTRACTS 1.1, T-07. 센티도 정수(한 바퀴 36,000). 0 = +x(오른쪽), 값이 커질수록 화면에서 반시계.
    // y 는 아래가 + 이므로 방향 벡터는 (cos a, −sin a) 다.
    public readonly struct Angle : IEquatable<Angle>
    {
        public readonly int Centi;   // 0..35999

        public const int Full = 36000;
        private const int Half = 18000;
        private const int Dir8Step = 4500;

        private Angle(int centi)
        {
            Centi = centi;
        }

        // 음수와 36000 이상을 접는다.
        public static Angle FromCenti(int centi)
        {
            int c = centi % Full;
            return new Angle(c < 0 ? c + Full : c);
        }

        public static Angle FromDegrees(int degrees) => FromCenti((degrees % 360) * 100);

        // E 0, NE 4500, N 9000(화면 위 = (0, −1)), …
        public static Angle FromDir8(Dir8 dir) => new Angle((int)dir * Dir8Step);

        // FixMath.Atan2(−v.Y, v.X). FixVec2.FromAngle 의 역
        public static Angle OfVector(FixVec2 v) => FixMath.Atan2(-v.Y, v.X);

        public static Angle operator +(Angle a, int centi) => FromCenti(a.Centi + centi % Full);

        // 부호 있는 최단 차이 [−18000, 18000)
        public static int Delta(Angle from, Angle to)
        {
            int d = to.Centi - from.Centi;   // (−36000, 36000)
            if (d < 0)
            {
                d += Full;
            }
            return d >= Half ? d - Full : d;
        }

        public static Angle RotateToward(Angle from, Angle to, int maxCenti)
        {
            int max = maxCenti < 0 ? 0 : maxCenti;
            int d = Delta(from, to);
            if (d <= max && d >= -max)
            {
                return to;
            }
            return from + (d > 0 ? max : -max);
        }

        public static bool operator ==(Angle a, Angle b) => a.Centi == b.Centi;

        public static bool operator !=(Angle a, Angle b) => a.Centi != b.Centi;

        public bool Equals(Angle other) => Centi == other.Centi;

        public override bool Equals(object? obj) => obj is Angle other && other.Centi == Centi;

        public override int GetHashCode() => Centi;
    }
}
