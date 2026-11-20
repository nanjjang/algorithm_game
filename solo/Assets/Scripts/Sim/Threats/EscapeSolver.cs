using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Sim.Threats
{
    /// <summary>R5에 넣는 구역 하나: 모양, 판정 틱, 터진 뒤 남기는 막힘(가시밭 등).</summary>
    public struct EscapeZone
    {
        public ShapeGeom Geom;
        public int DetonateTick;
        /// <summary>터진 뒤 지나갈 수 없는 구역을 남긴다(서리밭은 아니다).</summary>
        public bool LeavesBlock;
        public ShapeGeom BlockGeom;
    }

    /// <summary>
    /// R5 안전 경로 검사(05 요약서 2.2절, S5 3.5절). 전장을 칸 0.5의 격자로, 시간을 0.05초 단계로 끊어
    /// "0.25초 뒤부터 V로 움직여 각 구역이 터지는 단계에 그 구역 밖에 있을 수 있는 길"이 있는지 본다.
    /// 격자의 한 줄을 ulong 하나(비트 = 칸)로 들고 8방향 이동을 줄 단위 시프트로 한다. 버퍼는 이 객체가 들고 다시 쓴다.
    /// </summary>
    public sealed class EscapeSolver
    {
        const int MaxColumns = 64;                 // 자료구조 크기(ulong 한 줄)
        const float UnitsPerCell = 256f;           // 단위 환산(1/256칸. 이동량 내림을 원문과 같게 하려고)
        static readonly int[] DirX = { 1, 1, 0, -1, -1, -1, 0, 1 };   // →, ↗, ↑, ↖, ←, ↙, ↓, ↘ (S5 3.5절의 고정 순서)
        static readonly int[] DirY = { 0, -1, -1, -1, 0, 1, 1, 1 };

        ulong[][] _free = new ulong[0][];
        ulong[][] _reach = new ulong[0][];
        ulong[][] _doom = new ulong[0][];
        ulong[][] _blocked = new ulong[0][];
        ulong[] _frost = new ulong[0];
        readonly List<int> _kz = new List<int>();

        /// <summary>마지막 검사에서 쓴 지평선 K(테스트·진단용. 검사를 건너뛰었으면 0).</summary>
        public int LastHorizon { get; private set; }

        /// <param name="shrink">구역을 줄이는 양(칸, 양수). 원문 보정 e = −shrink.</param>
        public bool HasEscape(ThreatConfig cfg, float arenaWidth, float arenaHeight, Vector2 player, int now,
            List<EscapeZone> zones, List<HazardEntity> hazards, List<ObstacleEntity> obstacles, float shrink)
        {
            LastHorizon = 0;
            if (cfg == null || cfg.R5CellSize <= 0f || cfg.R5StepTicks <= 0 || cfg.EscapeSpeed <= 0f) return true;
            float cell = cfg.R5CellSize;
            int cols = Mathf.Min(MaxColumns, Mathf.RoundToInt(arenaWidth / cell));
            int rows = Mathf.RoundToInt(arenaHeight / cell);
            if (cols <= 0 || rows <= 0) return true;

            // 반응 창 안(kz ≤ 5)에 터지는 구역은 뺀다. 남는 것이 없으면 통과.
            _kz.Clear();
            int maxKz = -1;
            for (int z = 0; z < zones.Count; z++)
            {
                int left = zones[z].DetonateTick - now;
                int kz = left < 0 ? -1 : left / cfg.R5StepTicks;
                if (kz <= cfg.R5ReactionSteps) kz = -1;
                _kz.Add(kz);
                if (kz > maxKz) maxKz = kz;
            }
            if (maxKz < 0) return true;
            int horizon = maxKz + cfg.R5HorizonExtraSteps;
            LastHorizon = horizon;
            Ensure(horizon + 1, rows);

            ulong mask = cols >= MaxColumns ? ulong.MaxValue : ((1UL << cols) - 1UL);
            float margin = cfg.R5StaticMargin;

            // 정적 막힘(장애물, 남는 위험 구역)과 서리밭 칸
            for (int j = 0; j < rows; j++)
            {
                ulong blocked = 0UL, frost = 0UL;
                for (int i = 0; i < cols; i++)
                {
                    Vector2 c = new Vector2((i + 0.5f) * cell, (j + 0.5f) * cell);
                    ulong bit = 1UL << i;
                    if (obstacles != null)
                    {
                        for (int o = 0; o < obstacles.Count; o++)
                        {
                            var ob = obstacles[o];
                            if (ob.Dead) continue;
                            if ((c - ob.Pos).magnitude - ob.Radius < margin) { blocked |= bit; break; }
                        }
                    }
                    if (hazards != null)
                    {
                        for (int h = 0; h < hazards.Count; h++)
                        {
                            var hz = hazards[h];
                            if (hz.Dead) continue;
                            var g = ShapeGeom.Of(hz);
                            if (hz.SlowOnly) { if (ThreatGeometry.Distance(g, c) <= 0f) frost |= bit; }
                            else if (ThreatGeometry.InsideExpanded(g, c, margin)) blocked |= bit;
                        }
                    }
                }
                _frost[j] = frost;
                for (int k = 0; k <= horizon; k++)
                {
                    _blocked[k][j] = blocked;
                    _doom[k][j] = 0UL;
                    _reach[k][j] = 0UL;
                }
            }

            // 시간 막힘: 구역 z는 단계 kz에 터진다(구역을 줄인 모양). 남는 구역을 남기면 kz부터 막힘.
            for (int z = 0; z < zones.Count; z++)
            {
                int kz = _kz[z];
                if (kz < 0) continue;
                var zone = zones[z];
                for (int j = 0; j < rows; j++)
                {
                    ulong doom = 0UL, block = 0UL;
                    for (int i = 0; i < cols; i++)
                    {
                        Vector2 c = new Vector2((i + 0.5f) * cell, (j + 0.5f) * cell);
                        if (ThreatGeometry.InsideExpanded(zone.Geom, c, -shrink)) doom |= 1UL << i;
                        if (zone.LeavesBlock && ThreatGeometry.InsideExpanded(zone.BlockGeom, c, margin)) block |= 1UL << i;
                    }
                    _doom[kz][j] |= doom;
                    if (block != 0UL)
                        for (int k = kz; k <= horizon; k++) _blocked[k][j] |= block;
                }
            }

            // 시작 칸: 정적 막힘이어도 출발을 허용한다(가시밭 안에 서 있어도 빠져나갈 수 있게).
            int si = Mathf.Clamp(Mathf.FloorToInt(player.x / cell), 0, cols - 1);
            int sj = Mathf.Clamp(Mathf.FloorToInt(player.y / cell), 0, rows - 1);
            ulong startBit = 1UL << si;
            for (int k = 0; k <= horizon; k++)
            {
                for (int j = 0; j < rows; j++) _free[k][j] = ~(_blocked[k][j] | _doom[k][j]) & mask;
                if ((_doom[k][sj] & startBit) == 0UL) _free[k][sj] |= startBit;
            }

            // 이웃 칸으로 가는 데 드는 단계 수(V: 직교 3·대각 4 / 서리밭: 직교 4·대각 6)
            int orthoUnits = Mathf.RoundToInt(cell * UnitsPerCell);
            int diagUnits = Mathf.CeilToInt(orthoUnits * Mathf.Sqrt(2));
            int move = Mathf.Max(1, Mathf.FloorToInt(cfg.EscapeSpeed * UnitsPerCell * cfg.R5StepSeconds));
            int moveFrost = Mathf.Max(1, Mathf.FloorToInt(cfg.EscapeSpeed * (1f - cfg.FrostSlowRatio) * UnitsPerCell * cfg.R5StepSeconds));
            int nOrtho = CeilDiv(orthoUnits, move), nDiag = CeilDiv(diagUnits, move);
            int nOrthoFrost = CeilDiv(orthoUnits, moveFrost), nDiagFrost = CeilDiv(diagUnits, moveFrost);

            _reach[0][sj] = startBit;
            for (int k = 0; k < horizon; k++)
            {
                bool any = false;
                for (int j = 0; j < rows; j++)
                {
                    ulong r = _reach[k][j];
                    if (r == 0UL) continue;
                    any = true;
                    _reach[k + 1][j] |= r & _free[k + 1][j];   // 제자리
                }
                if (!any || k < cfg.R5ReactionSteps) continue;

                for (int frostClass = 0; frostClass < 2; frostClass++)
                {
                    for (int d = 0; d < DirX.Length; d++)
                    {
                        int dx = DirX[d], dy = DirY[d];
                        bool diag = dx != 0 && dy != 0;
                        int n = frostClass == 0 ? (diag ? nDiag : nOrtho) : (diag ? nDiagFrost : nOrthoFrost);
                        if (k + n > horizon) continue;   // 지평선을 넘는 이동은 잘라 당기지 않는다
                        for (int j = 0; j < rows; j++)
                        {
                            int tj = j + dy;
                            if (tj < 0 || tj >= rows) continue;
                            ulong src = _reach[k][j] & (frostClass == 0 ? ~_frost[j] : _frost[j]);
                            for (int t = k + 1; t < k + n && src != 0UL; t++) src &= _free[t][j];     // 이동 중 출발 칸이 안전
                            if (src == 0UL) continue;
                            ulong moved = ShiftX(src, dx, mask);
                            for (int t = k + 1; t <= k + n && moved != 0UL; t++) moved &= _free[t][tj];   // 이동 중과 도착 때 도착 칸이 안전
                            if (moved == 0UL) continue;
                            if (diag)
                            {
                                // 모서리 자르기 금지: 가로 이웃과 세로 이웃이 둘 다 막히지 않아야 한다.
                                ulong sideX = ~_blocked[k + n][j] & mask;
                                ulong sideY = ShiftX(~_blocked[k + n][tj] & mask, dx, mask);
                                moved &= sideX & sideY;
                            }
                            _reach[k + n][tj] |= moved;
                        }
                    }
                }
            }

            for (int j = 0; j < rows; j++) if (_reach[horizon][j] != 0UL) return true;
            return false;
        }

        static ulong ShiftX(ulong row, int dx, ulong mask)
        {
            if (dx > 0) return (row << 1) & mask;
            if (dx < 0) return row >> 1;
            return row;
        }

        static int CeilDiv(int a, int b) => (a + b - 1) / b;

        void Ensure(int steps, int rows)
        {
            _free = Grow(_free, steps, rows);
            _reach = Grow(_reach, steps, rows);
            _doom = Grow(_doom, steps, rows);
            _blocked = Grow(_blocked, steps, rows);
            if (_frost.Length < rows) _frost = new ulong[rows];
        }

        static ulong[][] Grow(ulong[][] buf, int steps, int rows)
        {
            if (buf.Length < steps) Array.Resize(ref buf, steps);
            for (int k = 0; k < steps; k++)
                if (buf[k] == null || buf[k].Length < rows) buf[k] = new ulong[rows];
            return buf;
        }
    }
}
