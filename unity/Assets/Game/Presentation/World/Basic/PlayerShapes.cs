#nullable enable
using UnityEngine;
using Wizard.Sim.Events;
using Wizard.Unity.Presentation.Core;
using Wizard.ViewLogic.Convert;

namespace Wizard.Unity.Presentation.World.Basic
{
    // 플레이어의 회색 도형: 몸(원판), 발밑의 열 링(effects 10)과 임계 시작 눈금, 잠금 X, 열 면제의 가는 링, 시야 화살표(GDD 3).
    // 위치는 보간한 도형(ShapeSample)에서, 링과 화살표의 값은 그 틱의 PlayerView 에서만 읽는다(HeatRingSample·FacingArrowSample).
    // 각도는 반시계 + 도(0 = 오른쪽, 90 = 위). 링은 맨 위(90도)에서 시계 방향으로 찬다.
    public static class PlayerShapes
    {
        private const float TopDeg = 90f;                 // 링이 시작하는 자리(맨 위)
        private const float TurnDeg = 360f;               // 단위 환산
        private const float CrossDeg = 45f;               // 잠금 X 의 두 막대 각도(±45도)

        public static void Draw(ShapeCanvas canvas, in ShapeSample body, in PlayerView view, uint tick)
        {
            Vector2 c = UnityConvert.ToUnity(body.Pos);
            canvas.Disk(c, body.RadiusCells, view.Invulnerable ? ShapePalette.PlayerBodyInvulnerable : ShapePalette.PlayerBody,
                        ShapeStyle.OrderPlayer);
            DrawHeatRing(canvas, c, HeatRingSample.From(view), tick);
            DrawFacingArrow(canvas, c, FacingArrowSample.From(view));
        }

        public static void DrawHeatRing(ShapeCanvas canvas, Vector2 center, in HeatRingSample ring, uint tick)
        {
            float radius = ShapeStyle.Cells(ShapeStyle.HeatRingRadiusPx);
            float thick = ShapeStyle.Cells(ShapeStyle.HeatRingThickPx);
            float outline = ShapeStyle.Cells(ShapeStyle.HeatRingOutlinePx);
            int seg = ShapeStyle.RingSegments;
            int order = ShapeStyle.OrderHeatRing;

            // 임계 시작 눈금: 차가움이면 희미하게(effects 10 "보이지 않음(눈금만 희미하게)")
            float markDeg = TopDeg - TurnDeg * ring.CritMarkRatio;
            float half = ShapeStyle.Cells(ShapeStyle.HeatMarkLengthPx) / 2f;
            canvas.Bar(ShapeCanvas.OnCircle(center, radius - half, markDeg), ShapeCanvas.OnCircle(center, radius + half, markDeg),
                       ShapeStyle.Cells(ShapeStyle.HeatMarkWidthPx), ring.RingVisible ? ShapePalette.HeatMark : ShapePalette.HeatMarkFaint,
                       order + 2);
            if (!ring.RingVisible)
            {
                return;
            }

            if (ring.Locked)
            {
                // 가득 찼다가 남은 잠금만큼 남는다. 지워지는 쪽이 맨 위에서 시계 방향으로 나아간다
                float r = ring.LockRemainRatio;
                float start = TopDeg - TurnDeg * (1f - r);
                float sweep = -TurnDeg * r;
                canvas.Arc(center, radius, thick + outline * 2f, start, sweep, ShapePalette.Outline, order, seg);
                canvas.Arc(center, radius, thick, start, sweep, ShapePalette.HeatLocked, order + 1, seg);
                float x = ShapeStyle.Cells(ShapeStyle.LockCrossPx) / 2f;
                float w = ShapeStyle.Cells(ShapeStyle.LockCrossWidthPx);
                canvas.Bar(ShapeCanvas.OnCircle(center, x, CrossDeg), ShapeCanvas.OnCircle(center, x, CrossDeg + TurnDeg / 2f), w,
                           ShapePalette.LockCross, ShapeStyle.OrderLockCross);
                canvas.Bar(ShapeCanvas.OnCircle(center, x, -CrossDeg), ShapeCanvas.OnCircle(center, x, TurnDeg / 2f - CrossDeg), w,
                           ShapePalette.LockCross, ShapeStyle.OrderLockCross);
                return;
            }

            float fillSweep = -TurnDeg * ring.FillRatio;
            if (ring.CriticalMark)
            {
                // 임계 표식: 흰 6px + 바깥 검정 2px. 넘치기 직전에는 2px 떤다(틱의 홀짝으로 좌우)
                Vector2 c = center;
                if (ring.State == HeatRingState.Brink)
                {
                    float shake = ShapeStyle.Cells(ShapeStyle.BrinkShakePx);
                    c += new Vector2((tick & 1U) == 0U ? shake : -shake, 0f);
                }
                canvas.Arc(c, radius, thick + outline * 2f, TopDeg, fillSweep, ShapePalette.Outline, order, seg);
                canvas.Arc(c, radius, thick, TopDeg, fillSweep, ShapePalette.HeatCritical, order + 1, seg);
            }
            else if (ring.State == HeatRingState.Imminent)
            {
                canvas.Arc(center, radius, thick, TopDeg, fillSweep, ShapePalette.HeatImminent, order + 1, seg);
            }
            else
            {
                canvas.Arc(center, radius, ShapeStyle.Cells(ShapeStyle.HeatRingThinPx), TopDeg, fillSweep, ShapePalette.HeatRising,
                           order + 1, seg);
            }

            if (ring.ExemptArmed)
            {
                float er = radius + thick / 2f + outline + ShapeStyle.Cells(ShapeStyle.HeatExemptGapPx);
                canvas.Arc(center, er, ShapeStyle.Cells(ShapeStyle.HeatExemptPx), TopDeg, -TurnDeg, ShapePalette.HeatExempt, order + 1, seg);
            }
        }

        public static void DrawFacingArrow(ShapeCanvas canvas, Vector2 center, in FacingArrowSample arrow)
        {
            Color color = arrow.Reversed ? ShapePalette.FacingReverse : (arrow.Opaque ? ShapePalette.FacingStance : ShapePalette.FacingNormal);
            float start = ShapeStyle.Cells(ShapeStyle.FacingArrowStartPx);
            float len = ShapeStyle.Cells(ShapeStyle.FacingArrowLengthPx);
            float head = ShapeStyle.Cells(ShapeStyle.FacingArrowHeadPx);
            Vector2 from = ShapeCanvas.OnCircle(center, start, arrow.AngleDegrees);
            Vector2 to = ShapeCanvas.OnCircle(center, start + len - head / 2f, arrow.AngleDegrees);
            canvas.Bar(from, to, ShapeStyle.Cells(ShapeStyle.FacingArrowWidthPx), color, ShapeStyle.OrderFacing);
            canvas.Triangle(ShapeCanvas.OnCircle(center, start + len - head / 2f, arrow.AngleDegrees), head, arrow.AngleDegrees, color,
                            ShapeStyle.OrderFacing);
        }
    }
}
