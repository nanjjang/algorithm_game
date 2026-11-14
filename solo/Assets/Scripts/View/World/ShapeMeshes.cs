using UnityEngine;

namespace Wizard.Solo.View.World
{
    /// <summary>
    /// 코드로 만드는 도형 메시(00 문서 7절: ISpritePool.RentMesh에 넣고, 돌려주기 전에 만든 쪽이 Destroy).
    /// 정점 색은 sRGB 그대로 넣는다(Solo/Shape 셰이더가 선형으로 바꾼다 — D-79).
    /// </summary>
    public static class ShapeMeshes
    {
        /// <summary>
        /// 고리의 차오른 부분(12시에서 시계 방향으로 ratio만큼). 가운데 = 원점(Unity 좌표, 칸). ratio ≤ 0이면 빈 메시.
        /// </summary>
        public static void FillArc(Mesh mesh, float radius, float width, float ratio, int segments, Color color)
        {
            mesh.Clear();
            ratio = Mathf.Clamp01(ratio);
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, segments) * ratio));
            if (ratio <= 0f) return;
            float inner = Mathf.Max(0f, radius - width);
            var v = new Vector3[(n + 1) * 2];
            var c = new Color[v.Length];
            var tri = new int[n * 6];
            float total = ratio * Mathf.PI * 2f;
            for (int i = 0; i <= n; i++)
            {
                // 12시(+y)에서 시계 방향.
                float a = Mathf.PI * 0.5f - total * i / n;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[i * 2] = dir * radius;
                v[i * 2 + 1] = dir * inner;
                c[i * 2] = color;
                c[i * 2 + 1] = color;
            }
            for (int i = 0; i < n; i++)
            {
                int o = i * 2, t = i * 6;
                tri[t] = o; tri[t + 1] = o + 2; tri[t + 2] = o + 1;
                tri[t + 3] = o + 1; tri[t + 4] = o + 2; tri[t + 5] = o + 3;
            }
            mesh.vertices = v;
            mesh.colors = c;
            mesh.triangles = tri;
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// 비네트: 바깥 사각형(w×h, 왼쪽 아래 원점)에서 안쪽 사각형(가로세로 inner 비율)으로 가며 색 → 투명이 되는 액자 메시.
        /// 깜빡이지 않는 고정 그림이다(08 14.1절 "깜빡이는 넓은 무늬 금지").
        /// </summary>
        public static void Vignette(Mesh mesh, float w, float h, float innerFraction, Color edge)
        {
            mesh.Clear();
            float ix = w * (1f - innerFraction) * 0.5f, iy = h * (1f - innerFraction) * 0.5f;
            var clear = new Color(edge.r, edge.g, edge.b, 0f);
            var v = new[]
            {
                new Vector3(0, 0), new Vector3(w, 0), new Vector3(w, h), new Vector3(0, h),
                new Vector3(ix, iy), new Vector3(w - ix, iy), new Vector3(w - ix, h - iy), new Vector3(ix, h - iy)
            };
            var c = new[] { edge, edge, edge, edge, clear, clear, clear, clear };
            // 네 변의 사다리꼴(바깥 i, i+1 / 안쪽 i+4, i+5).
            var tri = new int[24];
            for (int i = 0; i < 4; i++)
            {
                int a = i, b = (i + 1) % 4, ia = i + 4, ib = (i + 1) % 4 + 4, t = i * 6;
                tri[t] = a; tri[t + 1] = ib; tri[t + 2] = b;
                tri[t + 3] = a; tri[t + 4] = ia; tri[t + 5] = ib;
            }
            mesh.vertices = v;
            mesh.colors = c;
            mesh.triangles = tri;
            mesh.RecalculateBounds();
        }

        /// <summary>가는 사각 테(전장 가장자리 달빛 테두리). 왼쪽 아래 원점, 두께 thick.</summary>
        public static void Frame(Mesh mesh, float w, float h, float thick, Color color)
        {
            mesh.Clear();
            var v = new[]
            {
                new Vector3(0, 0), new Vector3(w, 0), new Vector3(w, h), new Vector3(0, h),
                new Vector3(thick, thick), new Vector3(w - thick, thick), new Vector3(w - thick, h - thick), new Vector3(thick, h - thick)
            };
            var c = new Color[8];
            for (int i = 0; i < 8; i++) c[i] = color;
            var tri = new int[24];
            for (int i = 0; i < 4; i++)
            {
                int a = i, b = (i + 1) % 4, ia = i + 4, ib = (i + 1) % 4 + 4, t = i * 6;
                tri[t] = a; tri[t + 1] = ib; tri[t + 2] = b;
                tri[t + 3] = a; tri[t + 4] = ia; tri[t + 5] = ib;
            }
            mesh.vertices = v;
            mesh.colors = c;
            mesh.triangles = tri;
            mesh.RecalculateBounds();
        }

        /// <summary>메시를 부순다(편집기 테스트에서도 돈다).</summary>
        public static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }
    }
}
