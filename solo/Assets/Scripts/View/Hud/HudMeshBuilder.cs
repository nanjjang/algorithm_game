using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 캐릭터 둘레 도형(열 링, 눈금, 시야 화살표, 경고 삼각)을 정점 색 메시로 쌓는다. 좌표는 px(가운데 원점, y 위 +),
    /// 각도는 12시에서 시계 방향(도). 정점 색은 sRGB 그대로 넣는다(Solo/Shape가 선형으로 바꾼다, 00 문서 D-79).
    /// 삼각형은 넣은 순서대로 그려진다(한 메시·한 머티리얼) — 뒤에 넣은 것이 위에 보인다.
    /// </summary>
    public sealed class HudMeshBuilder
    {
        /// <summary>한 바퀴를 나누는 조각 수(7.5도마다).</summary>
        public const int SegmentsPerTurn = 48;

        public readonly List<Vector3> Vertices = new List<Vector3>(512);
        public readonly List<Color32> Colors = new List<Color32>(512);
        public readonly List<int> Triangles = new List<int>(1024);

        public int TriangleCount => Triangles.Count / 3;

        public void Clear()
        {
            Vertices.Clear();
            Colors.Clear();
            Triangles.Clear();
        }

        /// <summary>12시에서 시계 방향 deg도, 반지름 r의 점.</summary>
        public static Vector2 OnCircle(float deg, float r)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
        }

        /// <summary>방향 벡터(y 위 +) → 12시 기준 시계 방향 각(도, 0~360).</summary>
        public static float ClockDegrees(Vector2 dir)
        {
            float d = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            return d < 0f ? d + 360f : d;
        }

        /// <summary>고리 띠(안 반지름 ~ 바깥 반지름)를 startDeg부터 시계 방향 sweepDeg만큼.</summary>
        public void Arc(Vector2 center, float rIn, float rOut, float startDeg, float sweepDeg, Color color)
        {
            if (sweepDeg <= 0f || rOut <= rIn || color.a <= 0f) return;
            if (sweepDeg > 360f) sweepDeg = 360f;
            int seg = Mathf.Max(2, Mathf.CeilToInt(sweepDeg / 360f * SegmentsPerTurn));
            Color32 c = color;
            int baseIndex = Vertices.Count;
            for (int i = 0; i <= seg; i++)
            {
                float deg = startDeg + sweepDeg * i / seg;
                Vertices.Add(center + OnCircle(deg, rIn));
                Vertices.Add(center + OnCircle(deg, rOut));
                Colors.Add(c);
                Colors.Add(c);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = baseIndex + i * 2;
                // 시계 방향 정점 순서(앞면). Shape 셰이더는 양면이라 감김 방향에 기대지 않는다.
                Triangles.Add(a); Triangles.Add(a + 1); Triangles.Add(a + 3);
                Triangles.Add(a); Triangles.Add(a + 3); Triangles.Add(a + 2);
            }
        }

        public void Ring(Vector2 center, float rIn, float rOut, Color color) => Arc(center, rIn, rOut, 0f, 360f, color);

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            if (color.a <= 0f) return;
            int i = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            Color32 k = color;
            Colors.Add(k); Colors.Add(k); Colors.Add(k);
            Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            if (color.a <= 0f) return;
            int i = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            Color32 k = color;
            Colors.Add(k); Colors.Add(k); Colors.Add(k); Colors.Add(k);
            Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
            Triangles.Add(i); Triangles.Add(i + 2); Triangles.Add(i + 3);
        }

        /// <summary>a→b 선분(두께 width).</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Quad(a + n, b + n, b - n, a - n, color);
        }

        /// <summary>중심 둘레의 각 deg 자리에 반지름 방향 눈금(rIn~rOut, 폭 width).</summary>
        public void RadialTick(Vector2 center, float deg, float rIn, float rOut, float width, Color color) =>
            Line(center + OnCircle(deg, rIn), center + OnCircle(deg, rOut), width, color);

        /// <summary>각 deg를 향하는 화살 삼각형: 밑변 가운데가 반지름 r, 끝이 r + size.</summary>
        public void Arrow(Vector2 center, float deg, float r, float size, Color color)
        {
            Vector2 dir = OnCircle(deg, 1f);
            Vector2 side = new Vector2(-dir.y, dir.x) * (size * 0.6f);
            Vector2 baseMid = center + dir * r;
            Triangle(baseMid + side, center + dir * (r + size), baseMid - side, color);
        }

        /// <summary>가운데 (x, y)의 정사각형.</summary>
        public void Square(Vector2 c, float size, Color color)
        {
            float h = size * 0.5f;
            Quad(c + new Vector2(-h, -h), c + new Vector2(-h, h), c + new Vector2(h, h), c + new Vector2(h, -h), color);
        }

        public void Apply(Mesh mesh)
        {
            if (mesh == null) return;
            mesh.Clear();
            mesh.SetVertices(Vertices);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
        }
    }
}
