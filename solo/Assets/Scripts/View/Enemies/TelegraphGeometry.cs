using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>
    /// 예고 구역 하나를 그리는 데 필요한 값(구역을 처음 본 때부터 매 프레임 복사해 둔다 — 사라지는 틱에는 상태에 없다. 00 문서 6절).
    /// 좌표는 시뮬레이션 좌표(칸, y 아래 +).
    /// </summary>
    public struct ZoneSnap
    {
        public int Id;
        public ZoneShape Shape;
        public Vector2 Pos, Dir;
        public float Radius, InnerRadius, Width, Length, FanDegrees;
        public int StartTick, DetonateTick, LockTick;
        public bool DirLocked, Harmless;

        public static ZoneSnap Of(ZoneEntity z) => new ZoneSnap
        {
            Id = z.Id, Shape = z.Shape, Pos = z.Pos, Dir = z.Dir, Radius = z.Radius, InnerRadius = z.InnerRadius, Width = z.Width,
            Length = z.Length, FanDegrees = z.FanDegrees, StartTick = z.StartTick, DetonateTick = z.DetonateTick, LockTick = z.LockTick,
            DirLocked = z.DirLocked, Harmless = z.Harmless
        };

        public static ZoneSnap Of(HazardEntity h) => new ZoneSnap
        {
            Id = h.Id, Shape = h.Shape, Pos = h.Pos, Dir = h.Dir, Radius = h.Radius,
            // 남는 구역에는 안 반지름이 없다: 고리 모양이면 Width를 고리 두께로 본다(G15 가정 — 결과 보고)
            InnerRadius = h.Shape == ZoneShape.Ring && h.Width > 0f ? Mathf.Max(0f, h.Radius - h.Width) : 0f,
            Width = h.Width, Length = h.Length, FanDegrees = 360f, StartTick = h.StartTick, DetonateTick = h.StartTick
        };
    }

    /// <summary>예고의 시간·모양 규칙(05 요약서 1.2절). 전부 순수 함수.</summary>
    public static class TelegraphGeometry
    {
        /// <summary>차오름 = (지금 − s) / N, 0~1. 판정 틱 s + N에서 1(차오름 끝과 판정은 같은 틱).</summary>
        public static float Fill(int startTick, int detonateTick, float nowTick)
        {
            if (detonateTick <= startTick) return 1f;
            return Mathf.Clamp01((nowTick - startTick) / (detonateTick - startTick));
        }

        /// <summary>
        /// 차오른 정도의 기하 값:
        /// Circle = 채운 반지름(r × fill, 중심 → 밖), Band = 채운 길이(L × fill, 적에게서 먼 쪽으로), Fan = 채운 반지름(꼭짓점 → 호),
        /// AimLine = 빨강 굵기 px(min → max), Ring = 채움의 바깥 반지름(안 → 밖: r_in + (r − r_in) × fill).
        /// </summary>
        public static float FillExtent(ZoneShape shape, float radius, float innerRadius, float length, float fill, float aimMinPx, float aimMaxPx)
        {
            fill = Mathf.Clamp01(fill);
            switch (shape)
            {
                case ZoneShape.Circle: return radius * fill;
                case ZoneShape.Band: return length * fill;
                case ZoneShape.Fan: return radius * fill;
                case ZoneShape.AimLine: return aimMinPx + (aimMaxPx - aimMinPx) * fill;
                case ZoneShape.Ring: return innerRadius + (radius - innerRadius) * fill;
            }
            return 0f;
        }

        /// <summary>번쩍 틱: 판정 틱 바로 앞의 n 틱(n = 2 → s+N−2, s+N−1).</summary>
        public static bool IsFlashTick(int tick, int detonateTick, int flashTicks) => tick >= detonateTick - flashTicks && tick < detonateTick;

        /// <summary>구역 넓이(칸²).</summary>
        public static float AreaCells(ZoneShape shape, float radius, float innerRadius, float width, float length, float fanDegrees)
        {
            switch (shape)
            {
                case ZoneShape.Circle: return Mathf.PI * radius * radius;
                case ZoneShape.Ring: return Mathf.PI * Mathf.Max(0f, radius * radius - innerRadius * innerRadius);
                case ZoneShape.Fan: return Mathf.Clamp(fanDegrees, 0f, 360f) / 360f * Mathf.PI * radius * radius;
                case ZoneShape.Band:
                case ZoneShape.AimLine: return Mathf.Max(0f, width) * Mathf.Max(0f, length);
            }
            return 0f;
        }

        /// <summary>넓이(칸²) → 기준 화면 px²(1칸 = 64px).</summary>
        public static float AreaPx2(float areaCells) => areaCells * SimConst.PixelsPerUnit * SimConst.PixelsPerUnit;

        /// <summary>채움 진하기 옵션(70/80/90%) → 알파. 범위 밖은 범위 안으로 자른다(진하기는 줄일 수 없는 것 — 70 아래로 내리지 않는다).</summary>
        public static float FillAlpha(int telegraphFillPct, int minPct, int maxPct) => Mathf.Clamp(telegraphFillPct, minPct, maxPct) / 100f;

        /// <summary>
        /// 남는 구역의 깜박(05 요약서 1.2절: 사라지기 lead초 전부터 초당 perSecond회). 깜박 한 번 = 꺼짐 반 주기 + 켜짐 반 주기.
        /// 깜박 구간에 들어선 첫 틱에 꺼진다. untilTick = int.MaxValue(웨이브 끝까지)면 깜박이지 않는다.
        /// </summary>
        public static bool HazardVisible(int tick, int untilTick, float leadSeconds, float perSecond)
        {
            if (untilTick == int.MaxValue || perSecond <= 0f) return true;
            int lead = SimConst.Ticks(leadSeconds);
            int start = untilTick - lead;
            if (tick < start) return true;
            int half = Mathf.Max(1, Mathf.RoundToInt(SimConst.TicksPerSecond / (perSecond * 2f)));
            return ((tick - start) / half) % 2 == 1;
        }

        /// <summary>일정 주기 깜박(켜짐 반 주기 먼저). 별똥 마지막 2초.</summary>
        public static bool BlinkOn(float elapsedSeconds, float perSecond)
        {
            if (perSecond <= 0f) return true;
            int k = Mathf.FloorToInt(elapsedSeconds * perSecond * 2f);
            return (k & 1) == 0;
        }
    }
}
