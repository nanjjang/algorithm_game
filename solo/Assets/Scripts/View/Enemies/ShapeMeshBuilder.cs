using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>
    /// 정점 색 메시를 코드로 쌓는 그릇(SpriteMaterialKind.Shape용). 좌표는 Unity 월드(표시) 좌표, 1 = 1칸.
    /// 색은 sRGB 바이트를 그대로 넣는다(00 문서 7절 "색 공간": Solo/Shape가 선형으로 바꾼다).
    /// 같은 그릇 안에서는 넣은 순서대로 그려진다(뒤에 넣은 삼각형이 위).
    /// </summary>
    public sealed class ShapeMeshBuilder
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(1024);
        public readonly List<Color32> Colors = new List<Color32>(1024);
        public readonly List<int> Triangles = new List<int>(3072);
        /// <summary>한 바퀴의 조각 수(원·고리·부채).</summary>
        public int CircleSegments = 64;

        readonly Vector2[] _clipA = new Vector2[16], _clipB = new Vector2[16];
        readonly List<Vector2> _offsets = new List<Vector2>(256);

        public int VertexCount => Vertices.Count;
        public int TriangleCount => Triangles.Count / 3;

        public void Clear()
        {
            Vertices.Clear();
            Colors.Clear();
            Triangles.Clear();
        }

        int V(Vector2 p, Color32 c)
        {
            Vertices.Add(new Vector3(p.x, p.y, 0f));
            Colors.Add(c);
            return Vertices.Count - 1;
        }

        public void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            if (col.a == 0) return;
            int i = V(a, col), j = V(b, col), k = V(c, col);
            // 감김 방향과 무관하게 보이도록(Shape 셰이더는 컬링을 끄지만, 혹시 몰라 반시계로 맞춘다)
            if (Cross(b - a, c - a) < 0f) { Triangles.Add(i); Triangles.Add(k); Triangles.Add(j); }
            else { Triangles.Add(i); Triangles.Add(j); Triangles.Add(k); }
        }

        /// <summary>a-b-c-d 차례의 볼록 사각형.</summary>
        public void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
        {
            AddTriangle(a, b, c, col);
            AddTriangle(a, c, d, col);
        }

        /// <summary>볼록 다각형(부채꼴로 나눈다).</summary>
        public void AddConvex(Vector2[] pts, int n, Color32 col)
        {
            for (int i = 1; i + 1 < n; i++) AddTriangle(pts[0], pts[i], pts[i + 1], col);
        }

        /// <summary>고리 조각. r0 = 안 반지름(0이면 부채꼴), r1 = 바깥 반지름, 각은 도(반시계 +, 0 = +x).</summary>
        public void AddAnnulusSector(Vector2 center, float r0, float r1, float startDeg, float sweepDeg, Color32 col)
        {
            if (r1 <= r0 || sweepDeg <= 0f || col.a == 0) return;
            if (r0 < 0f) r0 = 0f;
            if (sweepDeg > 360f) sweepDeg = 360f;
            int n = Mathf.Max(1, Mathf.CeilToInt(CircleSegments * sweepDeg / 360f));
            float step = sweepDeg / n * Mathf.Deg2Rad, a0 = startDeg * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                float a = a0 + step * i, b = a + step;
                var da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                if (r0 <= 0f) AddTriangle(center, center + da * r1, center + db * r1, col);
                else AddQuad(center + da * r0, center + da * r1, center + db * r1, center + db * r0, col);
            }
        }

        /// <summary>방향 dir(단위)을 따라 a0..a1, 옆으로 w0..w1(왼쪽 +) 범위의 직사각형.</summary>
        public void AddRect(Vector2 origin, Vector2 dir, float a0, float a1, float w0, float w1, Color32 col)
        {
            if (a1 <= a0 || w1 <= w0) return;
            var side = new Vector2(-dir.y, dir.x);
            AddQuad(origin + dir * a0 + side * w0, origin + dir * a1 + side * w0, origin + dir * a1 + side * w1, origin + dir * a0 + side * w1, col);
        }

        /// <summary>닫힌 고리(안쪽이 진행 방향의 왼쪽)를 안쪽으로 d0..d1만큼 들어간 띠로 그린다. 테두리용.</summary>
        public void AddLoopStroke(List<Vector2> loop, float d0, float d1, Color32 col)
        {
            int n = loop.Count;
            if (n < 3 || d1 <= d0 || col.a == 0) return;
            _offsets.Clear();
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = loop[(i - 1 + n) % n], cur = loop[i], next = loop[(i + 1) % n];
                Vector2 n0 = LeftNormal(cur - prev), n1 = LeftNormal(next - cur);
                float k = 1f + Vector2.Dot(n0, n1);
                Vector2 m = k > 0.1f ? (n0 + n1) / k : n1 * 4f;
                if (m.sqrMagnitude > 16f) m = m.normalized * 4f; // 뾰족한 꼭짓점의 이음 길이 제한
                _offsets.Add(m);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 pi = loop[i], pj = loop[j], mi = _offsets[i], mj = _offsets[j];
                AddQuad(pi + mi * d0, pj + mj * d0, pj + mj * d1, pi + mi * d1, col);
            }
        }

        /// <summary>점선 고리(조각 dashDeg, 틈 gapDeg). sweep 안에서만.</summary>
        public void AddDashedArc(Vector2 center, float r0, float r1, float startDeg, float sweepDeg, float dashDeg, float gapDeg, Color32 col)
        {
            if (dashDeg <= 0f) { AddAnnulusSector(center, r0, r1, startDeg, sweepDeg, col); return; }
            float period = dashDeg + Mathf.Max(0f, gapDeg);
            for (float a = 0f; a < sweepDeg - 0.001f; a += period)
                AddAnnulusSector(center, r0, r1, startDeg + a, Mathf.Min(dashDeg, sweepDeg - a), col);
        }

        /// <summary>
        /// region 그릇의 삼각형 [triStart, triEnd)를 줄무늬 띠(굵기 width, 간격 pitch, 각 angleDeg)로 잘라 이 그릇에 더한다.
        /// region이 이 그릇 자신이어도 된다(triEnd를 미리 정해 두므로 새로 더한 삼각형은 다시 자르지 않는다).
        /// </summary>
        public void AddStripes(ShapeMeshBuilder region, int triStart, int triEnd, float pitch, float width, float angleDeg, Color32 col)
        {
            if (pitch <= 0f || width <= 0f || col.a == 0) return;
            float r = (angleDeg + 90f) * Mathf.Deg2Rad;
            var nrm = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            for (int t = triStart; t < triEnd; t++)
            {
                Vector2 a = region.Vertices[region.Triangles[t * 3]], b = region.Vertices[region.Triangles[t * 3 + 1]], c = region.Vertices[region.Triangles[t * 3 + 2]];
                float ua = Vector2.Dot(a, nrm), ub = Vector2.Dot(b, nrm), uc = Vector2.Dot(c, nrm);
                float umin = Mathf.Min(ua, Mathf.Min(ub, uc)), umax = Mathf.Max(ua, Mathf.Max(ub, uc));
                int k0 = Mathf.FloorToInt((umin - width) / pitch), k1 = Mathf.FloorToInt(umax / pitch);
                for (int k = k0; k <= k1; k++)
                {
                    float lo = k * pitch, hi = lo + width;
                    if (hi <= umin || lo >= umax) continue;
                    _clipA[0] = a; _clipA[1] = b; _clipA[2] = c;
                    int cnt = ClipHalf(_clipA, 3, _clipB, nrm, lo);
                    cnt = ClipHalf(_clipB, cnt, _clipA, -nrm, -hi);
                    if (cnt >= 3) AddConvex(_clipA, cnt, col);
                }
            }
        }

        /// <summary>dot(p, n) ≥ c 쪽만 남긴다(Sutherland–Hodgman).</summary>
        static int ClipHalf(Vector2[] src, int n, Vector2[] dst, Vector2 nrm, float c)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = src[i], q = src[(i + 1) % n];
                float dp = Vector2.Dot(p, nrm) - c, dq = Vector2.Dot(q, nrm) - c;
                if (dp >= 0f) { if (m < dst.Length) dst[m++] = p; }
                if ((dp >= 0f) != (dq >= 0f))
                {
                    float t = dp / (dp - dq);
                    if (m < dst.Length) dst[m++] = p + (q - p) * t;
                }
            }
            return m;
        }

        /// <summary>색이 col인 삼각형 넓이 합(테스트·넓이 셈).</summary>
        public float AreaOfColor(Color32 col)
        {
            float s = 0f;
            for (int t = 0; t < TriangleCount; t++)
            {
                int i = Triangles[t * 3];
                var c = Colors[i];
                if (c.r != col.r || c.g != col.g || c.b != col.b || c.a != col.a) continue;
                s += TriArea(t);
            }
            return s;
        }

        public float TotalArea()
        {
            float s = 0f;
            for (int t = 0; t < TriangleCount; t++) s += TriArea(t);
            return s;
        }

        float TriArea(int t)
        {
            Vector2 a = Vertices[Triangles[t * 3]], b = Vertices[Triangles[t * 3 + 1]], c = Vertices[Triangles[t * 3 + 2]];
            return Mathf.Abs(Cross(b - a, c - a)) * 0.5f;
        }

        /// <summary>쌓은 것을 메시에 올린다(할당 없이 목록 판 API).</summary>
        public void Upload(Mesh mesh)
        {
            if (mesh == null) return;
            mesh.Clear();
            mesh.indexFormat = Vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0, false);
            mesh.RecalculateBounds();
        }

        static Vector2 LeftNormal(Vector2 d)
        {
            float m = d.magnitude;
            return m <= 1e-6f ? Vector2.zero : new Vector2(-d.y / m, d.x / m);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>닫힌 고리의 부호 넓이(반시계 +).</summary>
        public static float SignedArea(List<Vector2> loop)
        {
            float s = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector2 p = loop[i], q = loop[(i + 1) % loop.Count];
                s += p.x * q.y - q.x * p.y;
            }
            return s * 0.5f;
        }
    }
}
