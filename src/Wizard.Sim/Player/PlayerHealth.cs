using Wizard.Content;

namespace Wizard.Sim.Player
{
    // spec 01 6.1~6.2 의 상태 없는 규칙(무한 웨이브·연습). 결투의 피격(12.3)은 Duel 모듈의 것이다.
    internal static class PlayerHealth
    {
        // 체력 단위(spec 01 6.1 [임시], U29): 1칸 = 100점. 결투 체력 100 × 칸과 같은 단위다(online 4.1). 단위 환산 상수
        public const int PointsPerCell = 100;

        // MaxHpPoints = MaxHpCells × 100(spec 01 5.1)
        public static int MaxHpPoints(CharacterDef character) => unchecked(character.MaxHpCells * PointsPerCell);

        // 피해 점수 = 공격 칸 × 100(spec 01 6.2). 칸에는 이미 적 쪽 3칸 상한이 걸려 있다(mon 4.2, T-P06): 여기서 자르지 않는다.
        // debtBonus: 그믐(D5)이 빚이 있는 동안 +1칸. 상한 뒤에 더한다(최대 4칸, wc 4.5, spec 01 15절 C21)
        public static int DamagePoints(int damageCells, bool debtBonus)
        {
            int points = unchecked(damageCells * PointsPerCell);
            return debtBonus ? unchecked(points + PointsPerCell) : points;
        }

        // 피해를 깎은 뒤의 체력. 0 아래로 내려가지 않는다(spec 01 6.2 max(0, …))
        public static int HpAfter(int hpPoints, int damagePoints)
        {
            int after = unchecked(hpPoints - damagePoints);
            return after < 0 || after > hpPoints ? 0 : after;
        }
    }
}
