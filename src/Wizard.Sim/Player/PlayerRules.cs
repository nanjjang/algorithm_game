using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Input;
using Wizard.Sim.World;

namespace Wizard.Sim.Player
{
    // spec 01 5.2~5.5 의 상태 없는 규칙: 키 → 방향, 지금의 이동 방향, 자세 진입 때의 시야 맞추기, 판정기로 넘길 상대 방향.
    internal static class PlayerRules
    {
        // 대각 시야 유지 창(spec 01 5.4, 2.5의 환산표): 0.08초 = round(0.08 × 15,360) = 1,229 입력 시각 단위.
        // [제안] GDD 3 / [임시] U3. 기획 수치는 data/ 에 두어야 하지만 이 값을 담을 데이터 칸이 아직 없다
        // (CharacterDef·SimLimitsDef 어디에도 없다). 결과 보고의 계약 변경 요청: data/sim 의 칸과 정의 필드를 더하면 그 값으로 바꾼다.
        public const long DiagHoldWindowUnits = 1229;

        public static bool IsDiagonal(Dir8 d) => Movement.IsDiagonal(d);

        // 180도 뒤집기(spec 01 5.2)
        public static Dir8 Flip(Dir8 d) => Movement.Flip(d);

        public static bool IsDirectionKey(LogicalKey key) =>
            key == LogicalKey.Up || key == LogicalKey.Left || key == LogicalKey.Down || key == LogicalKey.Right;

        // 방향키 → 축 방향(spec 01 4.2: Up = W, Left = A, Down = S, Right = D). N 이 화면 위다(5.2)
        public static Dir8 KeyDir(LogicalKey key)
        {
            switch (key)
            {
                case LogicalKey.Up: return Dir8.N;
                case LogicalKey.Left: return Dir8.W;
                case LogicalKey.Down: return Dir8.S;
                case LogicalKey.Right: return Dir8.E;
                default: throw new SimInvariantException("player: not a direction key");
            }
        }

        public static LogicalKey CardinalKey(Dir8 cardinal)
        {
            switch (cardinal)
            {
                case Dir8.N: return LogicalKey.Up;
                case Dir8.W: return LogicalKey.Left;
                case Dir8.S: return LogicalKey.Down;
                case Dir8.E: return LogicalKey.Right;
                default: throw new SimInvariantException("player: not a cardinal direction");
            }
        }

        // 지금 눌린 방향키로 정한 이동 방향(spec 01 5.2). 가로·세로 축마다 한쪽만이면 그쪽, 둘 다면 나중에 누른 쪽.
        // 아무 방향도 아니면 false(이동 없음)
        public static bool TryMoveDir(in KeyState keys, out Dir8 dir)
        {
            int x = Axis(in keys, LogicalKey.Left, LogicalKey.Right);
            int y = Axis(in keys, LogicalKey.Up, LogicalKey.Down);
            return Movement.TryDirFromAxes(x, y, out dir);
        }

        // 대각 d 의 가로 성분(E 또는 W)과 세로 성분(N 또는 S)
        public static Dir8 HorizontalOf(Dir8 d) => Movement.DirX(d) > 0 ? Dir8.E : Dir8.W;

        public static Dir8 VerticalOf(Dir8 d) => Movement.DirY(d) < 0 ? Dir8.N : Dir8.S;

        // c 가 대각 diag 를 이루는 두 축 방향 중 하나인가(spec 01 5.4 "prevDir 의 축 성분")
        public static bool IsAxisComponent(Dir8 diag, Dir8 c) =>
            IsDiagonal(diag) && !IsDiagonal(c) && (c == HorizontalOf(diag) || c == VerticalOf(diag));

        // spec 01 5.5 SnapCardinal: 축 방향이면 그대로. 대각이면 두 축 성분 중 LastPressedCardinal 과 같은 쪽,
        // 둘 다 아니면 두 축 성분의 키 중 마지막 누름 시각이 늦은 쪽 [임시, U4].
        // 그 시각마저 같거나 둘 다 누른 적이 없으면 가로 성분 [임시: 명세가 정하지 않았다. 결과 보고의 OPEN-QUESTIONS 후보]
        public static Dir8 SnapCardinal(Dir8 facing, Dir8 lastPressedCardinal, in KeyState keys)
        {
            if (!IsDiagonal(facing))
            {
                return facing;
            }
            Dir8 h = HorizontalOf(facing);
            Dir8 v = VerticalOf(facing);
            if (lastPressedCardinal == h || lastPressedCardinal == v)
            {
                return lastPressedCardinal;
            }
            bool hasH = keys.TryGetLastPress(CardinalKey(h), out long th);
            bool hasV = keys.TryGetLastPress(CardinalKey(v), out long tv);
            if (hasV && (!hasH || tv > th))
            {
                return v;
            }
            return h;
        }

        // spec 01 5.5 Relative: 잠긴 시야와 누른 방향(둘 다 축 방향)의 차 → 판정기의 상대 방향.
        // diff = (pressed − locked) & 7: 0 앞, 4 뒤, 2 왼옆(반시계 90도), 6 오른옆
        public static CmdDir Relative(Dir8 locked, Dir8 pressed)
        {
            switch (((int)pressed - (int)locked) & 7)
            {
                case 0: return CmdDir.Forward;
                case 2: return CmdDir.Left;
                case 4: return CmdDir.Back;
                case 6: return CmdDir.Right;
                default: throw new SimInvariantException("player: relative direction of a diagonal");
            }
        }

        private static int Axis(in KeyState keys, LogicalKey negative, LogicalKey positive)
        {
            bool negHeld = keys.IsDown(negative);
            bool posHeld = keys.IsDown(positive);
            keys.TryGetLastPress(negative, out long negAt);
            keys.TryGetLastPress(positive, out long posAt);
            return Movement.ResolveAxis(negHeld, negAt, posHeld, posAt);
        }
    }
}
