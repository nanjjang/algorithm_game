using UnityEngine;

namespace Wizard.Solo.Sim.Player
{
    /// <summary>플레이어 규칙의 순수 함수(상태를 바꾸지 않는다). 02 요약서 3·4·6절.</summary>
    public static class PlayerRules
    {
        /// <summary>같은 시각 입력의 정렬 분류: 자세·반전 0 → 방향 1 → J 2 → K 3 → Pause 4 (02 요약서 3.2절).</summary>
        public static int KeyClass(LogicalKey k)
        {
            switch (k)
            {
                case LogicalKey.Stance:
                case LogicalKey.Reverse: return 0;
                case LogicalKey.Up:
                case LogicalKey.Left:
                case LogicalKey.Down:
                case LogicalKey.Right: return 1;
                case LogicalKey.Fire: return 2;
                case LogicalKey.Skill: return 3;
                default: return 4;
            }
        }

        public static bool IsDirectionKey(LogicalKey k) =>
            k == LogicalKey.Up || k == LogicalKey.Left || k == LogicalKey.Down || k == LogicalKey.Right;

        /// <summary>방향키 → 그 키의 축 방향.</summary>
        public static Dir8 CardinalOf(LogicalKey k)
        {
            switch (k)
            {
                case LogicalKey.Up: return Dir8.N;
                case LogicalKey.Left: return Dir8.W;
                case LogicalKey.Down: return Dir8.S;
                default: return Dir8.E;
            }
        }

        /// <summary>축 방향 → 그 방향의 키.</summary>
        public static LogicalKey KeyOf(Dir8 cardinal)
        {
            switch (cardinal)
            {
                case Dir8.N: return LogicalKey.Up;
                case Dir8.W: return LogicalKey.Left;
                case Dir8.S: return LogicalKey.Down;
                default: return LogicalKey.Right;
            }
        }

        /// <summary>한 축의 값(−1, 0, +1). 둘 다 눌렸으면 나중에 누른 쪽(02 요약서 4.1절 [임시]).</summary>
        public static int ResolveAxis(KeyState keys, LogicalKey negative, LogicalKey positive)
        {
            bool n = keys.IsDown(negative), p = keys.IsDown(positive);
            if (n && p) return keys.LastPressU[(int)positive] >= keys.LastPressU[(int)negative] ? 1 : -1;
            if (n) return -1;
            if (p) return 1;
            return 0;
        }

        /// <summary>지금 키 상태의 이동 방향. 없으면 false.</summary>
        public static bool TryMoveDir(KeyState keys, out Dir8 dir)
        {
            int x = ResolveAxis(keys, LogicalKey.Left, LogicalKey.Right);
            // 시뮬레이션 좌표는 y 아래가 +이지만 Dir8의 N은 "화면 위"다: Up = N.
            int up = ResolveAxis(keys, LogicalKey.Down, LogicalKey.Up);
            return TryDirFromAxes(x, up, out dir);
        }

        /// <summary>x(오른쪽 +), up(화면 위 +) → 8방향.</summary>
        public static bool TryDirFromAxes(int x, int up, out Dir8 dir)
        {
            dir = Dir8.S;
            if (x == 0 && up == 0) return false;
            if (x > 0) dir = up > 0 ? Dir8.NE : up < 0 ? Dir8.SE : Dir8.E;
            else if (x < 0) dir = up > 0 ? Dir8.NW : up < 0 ? Dir8.SW : Dir8.W;
            else dir = up > 0 ? Dir8.N : Dir8.S;
            return true;
        }

        /// <summary>axis가 대각 diag의 두 축 성분 중 하나인가.</summary>
        public static bool IsAxisComponentOf(Dir8 axis, Dir8 diag)
        {
            if (Dir8Util.IsCardinal(diag) || !Dir8Util.IsCardinal(axis)) return false;
            return axis == Dir8Util.Rotate(diag, 1) || axis == Dir8Util.Rotate(diag, -1);
        }

        /// <summary>
        /// 자세에 들어갈 때 시야를 상하좌우로 맞춘다(02 요약서 4.4절): 축 방향이면 그대로, 대각이면 두 축 성분 중
        /// 마지막으로 누른 축과 같은 쪽, 둘 다 아니면 마지막 누름 시각이 늦은 쪽 [임시].
        /// </summary>
        public static Dir8 SnapCardinal(Dir8 facing, Dir8 lastPressedCardinal, KeyState keys)
        {
            if (Dir8Util.IsCardinal(facing)) return facing;
            Dir8 a = Dir8Util.Rotate(facing, -1), b = Dir8Util.Rotate(facing, 1);
            if (lastPressedCardinal == a) return a;
            if (lastPressedCardinal == b) return b;
            long ta = keys.LastPressU[(int)KeyOf(a)], tb = keys.LastPressU[(int)KeyOf(b)];
            return tb > ta ? b : a;
        }

        /// <summary>
        /// 임계 사격 가능 횟수(02 요약서 6.6절): n = (12000 − critStartHu) / shotHu + 1.
        /// 발사 열이 0이면 −1(무한), 발사 열이 열 끝을 넘으면 0(불가 → 경고).
        /// </summary>
        public static int CritShotCount(int critStartHu, int shotHu)
        {
            if (shotHu <= 0) return -1;
            if (shotHu > PlayerTuning.HeatMaxHu) return 0;
            return (PlayerTuning.HeatMaxHu - critStartHu) / shotHu + 1;
        }

        /// <summary>열 링 상태(02 요약서 6.6절).</summary>
        public static HeatRingState Ring(bool locked, int heatHu, int critStartHu, int imminentBelowCritHu, int brinkFromHu)
        {
            if (locked) return HeatRingState.Locked;
            if (heatHu <= 0) return HeatRingState.Cold;
            if (heatHu < critStartHu - imminentBelowCritHu) return HeatRingState.Rising;
            if (heatHu < critStartHu) return HeatRingState.Imminent;
            if (heatHu < brinkFromHu) return HeatRingState.Critical;
            return HeatRingState.Brink;
        }

        /// <summary>
        /// 선분 o → o + d가 원(c, r)에 **들어가는** 지점의 비율 t(0~1). 시작점이 이미 원 안이거나 닿지 않으면 false.
        /// 비기 이동이 적·장애물 앞에서 멈출 자리를 찾는 데 쓴다.
        /// </summary>
        public static bool SegmentEntersCircle(Vector2 o, Vector2 d, Vector2 c, float r, out float t)
        {
            t = 0f;
            Vector2 m = o - c;
            float a = Vector2.Dot(d, d);
            if (a <= 0f) return false;
            float cc = Vector2.Dot(m, m) - r * r;
            if (cc <= 0f) return false;
            float b = Vector2.Dot(m, d);
            if (b >= 0f) return false;
            float disc = b * b - a * cc;
            if (disc < 0f) return false;
            float root = (-b - Mathf.Sqrt(disc)) / a;
            if (root < 0f || root > 1f) return false;
            t = root;
            return true;
        }
    }
}
