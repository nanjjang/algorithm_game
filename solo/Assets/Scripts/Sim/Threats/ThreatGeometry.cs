using UnityEngine;

namespace Wizard.Solo.Sim.Threats
{
    /// <summary>모양 하나의 기하(예고 구역·남는 구역 공용). Pos = 원·고리 중심 / 띠·조준선 시작점 / 부채꼴 꼭짓점.</summary>
    public struct ShapeGeom
    {
        public ZoneShape Shape;
        public Vector2 Pos;
        public Vector2 Dir;
        public float Radius, InnerRadius, Width, Length, FanDegrees;

        public static ShapeGeom Of(ZoneEntity z)
        {
            return new ShapeGeom
            {
                Shape = z.Shape, Pos = z.Pos, Dir = z.Dir, Radius = z.Radius, InnerRadius = z.InnerRadius,
                Width = z.Width, Length = z.Length, FanDegrees = z.FanDegrees
            };
        }

        public static ShapeGeom Of(HazardEntity h)
        {
            return new ShapeGeom { Shape = h.Shape, Pos = h.Pos, Dir = h.Dir, Radius = h.Radius, Width = h.Width, Length = h.Length };
        }
    }

    /// <summary>위협 판정에 쓰는 순수 기하 함수. 05 요약서 1.2절(모양 5종), 2.2절(R5의 모양 판정).</summary>
    public static class ThreatGeometry
    {
        const float HalfTurnDegrees = 180f;   // 단위 환산(도)

        public static Vector2 NormalizeOr(Vector2 v, Vector2 fallback)
        {
            float m = v.magnitude;
            if (m > Mathf.Epsilon) return v / m;
            float f = fallback.magnitude;
            return f > Mathf.Epsilon ? fallback / f : new Vector2(1, 0);
        }

        /// <summary>시뮬레이션 벡터를 화면 기준 반시계로 돌린다(도).</summary>
        public static Vector2 Rotate(Vector2 dir, float degreesCcw)
        {
            if (degreesCcw == 0f) return dir;
            return Dir8Util.DegreesToSimVector(Dir8Util.SimVectorToDegrees(dir) + degreesCcw) * dir.magnitude;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            return (p - ClosestOnSegment(p, a, b, out _)).magnitude;
        }

        public static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            t = len2 > Mathf.Epsilon ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return a + ab * t;
        }

        /// <summary>점에서 모양까지의 거리. 모양 안이면 0, 밖이면 경계까지의 거리.
        /// 플레이어 판정원과의 겹침 = 이 값 &lt; 판정 반지름(05 요약서 0절 "거리 &lt; 반지름 합").</summary>
        public static float Distance(in ShapeGeom g, Vector2 p)
        {
            Vector2 v = p - g.Pos;
            switch (g.Shape)
            {
                case ZoneShape.Circle:
                    return Mathf.Max(0f, v.magnitude - g.Radius);
                case ZoneShape.Ring:
                {
                    float d = v.magnitude;
                    if (d < g.InnerRadius) return g.InnerRadius - d;
                    return Mathf.Max(0f, d - g.Radius);
                }
                case ZoneShape.Band:
                case ZoneShape.AimLine:
                {
                    Vector2 u = NormalizeOr(g.Dir, new Vector2(1, 0));
                    float t = Vector2.Dot(v, u);
                    float q = Mathf.Abs(Cross(v, u));
                    float dx = Mathf.Max(0f, Mathf.Max(-t, t - g.Length));
                    float dy = Mathf.Max(0f, q - g.Width / 2);
                    return Mathf.Sqrt(dx * dx + dy * dy);
                }
                case ZoneShape.Fan:
                {
                    float len = v.magnitude;
                    float half = g.FanDegrees / 2;
                    if (half >= HalfTurnDegrees) return Mathf.Max(0f, len - g.Radius);
                    Vector2 u = NormalizeOr(g.Dir, new Vector2(1, 0));
                    bool inAngle = len <= Mathf.Epsilon || Vector2.Dot(v, u) >= len * Mathf.Cos(half * Mathf.Deg2Rad);
                    if (inAngle) return Mathf.Max(0f, len - g.Radius);
                    float d1 = DistanceToSegment(p, g.Pos, g.Pos + Rotate(u, half) * g.Radius);
                    float d2 = DistanceToSegment(p, g.Pos, g.Pos + Rotate(u, -half) * g.Radius);
                    return Mathf.Min(d1, d2);
                }
            }
            return float.MaxValue;
        }

        /// <summary>
        /// R5의 모양 판정(S5 3.5절): 점이 모양을 e만큼 넓힌(음수면 줄인) 것 안에 있는가.
        /// e ≥ 0: 모양까지 거리 &lt; e. e &lt; 0: 원·부채꼴은 반지름만, 띠는 폭만, 고리는 두께만 줄인다(길이 끝과 각은 그대로).
        /// </summary>
        public static bool InsideExpanded(in ShapeGeom g, Vector2 p, float e)
        {
            if (e >= 0f)
            {
                float d = Distance(g, p);
                return d <= 0f || d < e;
            }
            Vector2 v = p - g.Pos;
            switch (g.Shape)
            {
                case ZoneShape.Circle:
                    return v.magnitude < g.Radius + e;
                case ZoneShape.Ring:
                {
                    float d = v.magnitude;
                    return d > g.InnerRadius - e && d < g.Radius + e;
                }
                case ZoneShape.Band:
                case ZoneShape.AimLine:
                {
                    Vector2 u = NormalizeOr(g.Dir, new Vector2(1, 0));
                    float t = Vector2.Dot(v, u);
                    float q = Mathf.Abs(Cross(v, u));
                    return t >= 0f && t <= g.Length && q < g.Width / 2 + e;
                }
                case ZoneShape.Fan:
                {
                    float len = v.magnitude;
                    if (len >= g.Radius + e) return false;
                    float half = g.FanDegrees / 2;
                    if (half >= HalfTurnDegrees || len <= Mathf.Epsilon) return true;
                    Vector2 u = NormalizeOr(g.Dir, new Vector2(1, 0));
                    return Vector2.Dot(v, u) >= len * Mathf.Cos(half * Mathf.Deg2Rad);
                }
            }
            return false;
        }

        /// <summary>전장 안의 점에서 방향으로 쏜 선이 벽에 닿을 때까지의 거리(밖에서 시작하면 0).</summary>
        public static float RayToWall(Vector2 origin, Vector2 dir, float arenaWidth, float arenaHeight)
        {
            if (origin.x < 0f || origin.y < 0f || origin.x > arenaWidth || origin.y > arenaHeight) return 0f;
            Vector2 u = NormalizeOr(dir, new Vector2(1, 0));
            float best = float.MaxValue;
            if (u.x > Mathf.Epsilon) best = Mathf.Min(best, (arenaWidth - origin.x) / u.x);
            else if (u.x < -Mathf.Epsilon) best = Mathf.Min(best, -origin.x / u.x);
            if (u.y > Mathf.Epsilon) best = Mathf.Min(best, (arenaHeight - origin.y) / u.y);
            else if (u.y < -Mathf.Epsilon) best = Mathf.Min(best, -origin.y / u.y);
            return best == float.MaxValue ? 0f : Mathf.Max(0f, best);
        }

        /// <summary>선분 a→b가 원 안으로 들어가나. 들어가면 처음 겉면에 닿는 선분 위 위치(0~1). 안에서 시작하면 0.</summary>
        public static bool SegmentEntersCircle(Vector2 a, Vector2 b, Vector2 center, float radius, out float t)
        {
            t = 0f;
            Vector2 m = a - center;
            float inside = m.sqrMagnitude - radius * radius;
            if (inside < 0f) return true;
            Vector2 ab = b - a;
            float len = ab.magnitude;
            if (len <= Mathf.Epsilon) return false;
            Vector2 d = ab / len;
            float along = Vector2.Dot(m, d);
            float disc = along * along - inside;
            if (disc <= 0f) return false;   // 스치기만 하는 것은 닿지 않은 것으로 본다
            float enter = -along - Mathf.Sqrt(disc);
            if (enter < 0f || enter >= len) return false;
            t = enter / len;
            return true;
        }
    }
}
