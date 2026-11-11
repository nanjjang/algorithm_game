using Wizard.Core;

namespace Wizard.ViewLogic
{
    // CONTRACTS 13, T-27·T-31. 고정소수점 → 실수는 코어 세 어셈블리 밖에서 여기에만 있다.
    // 표시 전용이다. 결과를 코어로 되돌려 보내지 않는다. Unity 어셈블리는 이 결과를 엔진 타입으로 옮기기만 한다.
    public static class FixConvert
    {
        private const double RawToUnit = 1.0 / 4294967296.0;   // 2^-32

        public static float ToFloat(Fix64 v) => (float)(v.Raw * RawToUnit);

        // 칸 → 표시 좌표(1칸 = 1유닛, y 뒤집기: 전장은 y가 아래로 늘고 표시 좌표는 위로 는다)
        public static System.Numerics.Vector2 ToWorld(FixVec2 cellPos) =>
            new System.Numerics.Vector2(ToFloat(cellPos.X), -ToFloat(cellPos.Y));

        // 센티도 → 도. 화면에서 반시계가 + 인 것은 같다
        public static float ToDegrees(Angle a) => a.Centi / 100f;
    }
}
