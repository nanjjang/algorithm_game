using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.World
{
    // spec 01 2.3·5.2·5.6, CONTRACTS 1.1(속도 변환은 FixMath 가 아니라 여기). 상태가 없는 순수 함수만 둔다.
    // 이 함수들을 4단계(플레이어 이동과 벽)에 붙이는 것은 Player 모듈(Stage04_Player)의 일이다.
    //
    // 좌표: x 는 오른쪽, y 는 아래가 +(spec 01 5.2). N = 화면 위 = (0, −1).
    public static class Movement
    {
        // 단위 환산 상수. 틱당 이동량 = SpeedCenti × mulPermille × 2^32 ÷ (60틱 × 100센티 × 1000천분율)
        private const long CentiPermillePerTickDivisor = (long)SimVersion.TickRate * 100L * 1000L;   // 6,000,000
        private const long CentiPermillePerTickHalf = CentiPermillePerTickDivisor / 2L;                 // 3,000,000(반올림)
        private const int PermilleOne = 1000;

        // 축 방향 틱당 이동량(Raw). CONTRACTS 1.1: (SpeedCenti × mulPermille × 2^32 + 3,000,000) / 6,000,000.
        // 반올림의 동률은 올림이다(spec 05 TV-UNIT-05: 3.76칸/초 → 269,151,284).
        // 전제: speedCenti ≥ 0, mulPermille ≥ 0, speedCenti × mulPermille < 2^31(넘으면 래핑). 데이터의 값은 이 범위다
        // (가장 빠른 672센티 × 1200 = 806,400). 음수를 넣어도 결과는 결정적이지만 뜻이 없다.
        public static long AxisStepRaw(int speedCenti, int mulPermille)
        {
            long numerator = unchecked((long)speedCenti * mulPermille * Fix64.OneRaw + CentiPermillePerTickHalf);
            return numerator / CentiPermillePerTickDivisor;
        }

        // 대각선 이동의 한 축 이동량(Raw). spec 01 2.3: (vRaw × InvSqrt2Raw) >> 32 (128비트 곱, 내림).
        // 대각선 이동의 실제 속력은 축 이동과 같다(정규화) [임시].
        public static long DiagStepRaw(long axisStepRaw)
        {
            Wide.Mul64(axisStepRaw, FixMath.InvSqrt2Raw, out long hi, out ulong lo);
            return Wide.ShiftRight32(hi, lo);
        }

        // 두 배율을 곱한다(spec 01 5.6: 스침 가속 1200 × 서리밭 700 / 1000 = 840) [임시: 나머지는 0 쪽 버림]
        public static int CombinePermille(int a, int b) => (int)unchecked((long)a * b / PermilleOne);

        // 이번 틱에 쓸 속도(센티 칸/초). 자세·반전 자세는 배율이 아니라 값 교체다(spec 01 5.6)
        public static int SpeedCenti(CharacterDef character, bool inStance) =>
            inStance ? character.StanceSpeedCenti : character.WalkSpeedCenti;

        // 이번 틱의 배율(천분율). 스침 가속은 캐릭터의 GrazeBuffPermille(+20% = 1200, 중첩 없음),
        // 느려짐은 적 명세가 넣는 PlayerSlowPermille(없으면 1000). 둘이 겹치면 곱한다(spec 01 5.6)
        public static int MovePermille(CharacterDef character, bool grazeBuffActive, int slowPermille)
        {
            int buff = grazeBuffActive ? character.GrazeBuffPermille : PermilleOne;
            return CombinePermille(buff, slowPermille);
        }

        // ── 방향(spec 01 5.2)

        // 한 축의 입력 → −1, 0, +1. 한쪽만 눌리면 그쪽, 둘 다면 나중에 누른 쪽(누름 시각이 큰 쪽) [임시, spec 01 16.1 U2].
        // 누름 시각이 같으면 0이다 [임시]: 어느 한쪽을 고르면 좌우 거울 입력의 결과가 거울이 되지 않는다.
        // 시각은 InputTime(tick × 256 + offset) 같은 단조 값이면 된다.
        public static int ResolveAxis(bool negativeHeld, long negativePressedAt, bool positiveHeld, long positivePressedAt)
        {
            if (negativeHeld && positiveHeld)
            {
                if (negativePressedAt == positivePressedAt)
                {
                    return 0;
                }
                return negativePressedAt > positivePressedAt ? -1 : 1;
            }
            if (negativeHeld)
            {
                return -1;
            }
            return positiveHeld ? 1 : 0;
        }

        // (가로, 세로) → Dir8. 가로 +1 = 오른쪽, 세로 +1 = 아래. (0, 0)이면 false(이동 없음).
        // 값은 부호만 본다(−1·0·+1 밖의 값도 부호로 읽는다).
        public static bool TryDirFromAxes(int x, int y, out Dir8 dir)
        {
            int sx = x > 0 ? 1 : (x < 0 ? -1 : 0);
            int sy = y > 0 ? 1 : (y < 0 ? -1 : 0);
            if (sy < 0)
            {
                dir = sx > 0 ? Dir8.NE : (sx < 0 ? Dir8.NW : Dir8.N);
                return true;
            }
            if (sy > 0)
            {
                dir = sx > 0 ? Dir8.SE : (sx < 0 ? Dir8.SW : Dir8.S);
                return true;
            }
            if (sx != 0)
            {
                dir = sx > 0 ? Dir8.E : Dir8.W;
                return true;
            }
            dir = Dir8.E;
            return false;
        }

        // Dir8 의 가로 성분 부호(−1·0·+1). E 쪽이 +1
        public static int DirX(Dir8 d)
        {
            switch (d)
            {
                case Dir8.E:
                case Dir8.NE:
                case Dir8.SE:
                    return 1;
                case Dir8.W:
                case Dir8.NW:
                case Dir8.SW:
                    return -1;
                default:
                    return 0;
            }
        }

        // Dir8 의 세로 성분 부호(−1·0·+1). y 가 아래로 늘기 때문에 N 쪽이 −1
        public static int DirY(Dir8 d)
        {
            switch (d)
            {
                case Dir8.N:
                case Dir8.NE:
                case Dir8.NW:
                    return -1;
                case Dir8.S:
                case Dir8.SE:
                case Dir8.SW:
                    return 1;
                default:
                    return 0;
            }
        }

        // 대각(NE·NW·SW·SE)인가. Dir8 은 반시계 45도 단위라 홀수가 대각이다
        public static bool IsDiagonal(Dir8 d) => ((int)d & 1) != 0;

        // 180도 뒤집기(spec 01 5.2): (d + 4) & 7
        public static Dir8 Flip(Dir8 d) => (Dir8)(((int)d + 4) & 7);

        // 한 틱의 이동 벡터. 축 방향은 (±v, 0)·(0, ±v), 대각은 (±vDiag, ±vDiag)
        public static FixVec2 StepDelta(Dir8 dir, long axisStepRaw)
        {
            long step = IsDiagonal(dir) ? DiagStepRaw(axisStepRaw) : axisStepRaw;
            long dx = unchecked(DirX(dir) * step);
            long dy = unchecked(DirY(dir) * step);
            return new FixVec2(Fix64.FromRaw(dx), Fix64.FromRaw(dy));
        }

        // 이동 적분 한 틱(spec 01 5.6 [임시]): Pos += 방향 단위 × 틱당 이동량, 그 뒤 벽 자르기와 기둥 밀어내기.
        // 할당이 없다
        public static FixVec2 Integrate(ArenaGeometry arena, FixVec2 pos, Dir8 dir, long axisStepRaw, Fix64 bodyRadius)
        {
            return arena.ResolveBody(pos + StepDelta(dir, axisStepRaw), bodyRadius);
        }

        // 다섯 캐릭터 중 가장 느린 자세 속도(spec 01 5.1: CharacterTable.MinStanceSpeedCenti(), 적 공격의 회피 기준 V).
        // 적 명세의 검산 테스트가 읽는다. 빈 목록이면 0
        public static int MinStanceSpeedCenti(CharacterDef[] characters)
        {
            if (characters.Length == 0)
            {
                return 0;
            }
            int min = characters[0].StanceSpeedCenti;
            for (int i = 1; i < characters.Length; i++)
            {
                int v = characters[i].StanceSpeedCenti;
                if (v < min)
                {
                    min = v;
                }
            }
            return min;
        }
    }
}
