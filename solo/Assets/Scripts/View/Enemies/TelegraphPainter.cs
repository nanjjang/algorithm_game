using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>예고 한 장을 그릴 때의 그날 값(층이 매 프레임 정한다).</summary>
    public struct ZoneLook
    {
        /// <summary>차오름 0~1.</summary>
        public float Fill;
        /// <summary>빨강 채움 알파(= 설정의 예고 채움 진하기 70/80/90%).</summary>
        public float FillAlpha;
        /// <summary>색각 보조: 차오르는 채움 위에 줄무늬를 더한다(예고를 줄이지 않는다).</summary>
        public bool Colorblind;
        /// <summary>이번 프레임이 번쩍 틱이다.</summary>
        public bool Flash;
        /// <summary>번쩍 한도에 걸려 구역 전체 흰색 대신 흰 선 테두리로 강조한다.</summary>
        public bool FlashAsBorder;
        /// <summary>전체 알파 배율(취소된 구역이 흐려질 때). 평소 1.</summary>
        public float AlphaMul;
        /// <summary>고정 순간 떨림(칸).</summary>
        public Vector2 OffsetCells;

        public static ZoneLook Default(float fill, float fillAlpha) => new ZoneLook { Fill = fill, FillAlpha = fillAlpha, AlphaMul = 1f };
    }

    /// <summary>
    /// 예고 구역·남는 구역을 정점 색 메시로 그린다(05 요약서 1.2절, 10 요약서 3.1·3.2절).
    /// 예고는 두 번 그린다: 채움 층(SortBands.TelegraphFill — 바닥 40% 어둡게, 빨강 채움, 줄무늬, 앞선, 번쩍)과
    /// 테두리 층(SortBands.TelegraphEdge — 3겹 테두리 + 앞선을 한 번 더, 번쩍 한도 강조).
    /// 테두리 3겹(바깥 검정 2px / 빨강 4px / 안 흰 2px)은 구역 경계에서 안쪽으로 들어간다(판정 넓이 = 보이는 넓이).
    /// </summary>
    public sealed class TelegraphPainter
    {
        readonly EnemyViewTuning _t;
        readonly List<Vector2> _outer = new List<Vector2>(128), _inner = new List<Vector2>(128);
        readonly ShapeMeshBuilder _scratch = new ShapeMeshBuilder();
        public float ArenaHeight = 14f;

        public TelegraphPainter(EnemyViewTuning tuning)
        {
            _t = tuning ?? EnemyViewTuning.Parse(null);
            _scratch.CircleSegments = _t.CircleSegments;
        }

        float Px(float px) => px * EnemyViewTuning.Px;
        Vector2 ToView(Vector2 sim) => new Vector2(sim.x, ArenaHeight - sim.y);

        static Vector2 DirView(Vector2 simDir)
        {
            var d = new Vector2(simDir.x, -simDir.y);
            return d.sqrMagnitude < 1e-8f ? Vector2.right : d.normalized;
        }

        static bool IsFullCircle(in ZoneSnap z) => z.Shape == ZoneShape.Circle || (z.Shape == ZoneShape.Fan && z.FanDegrees >= 360f);

        // ------------------------------------------------------------ 예고

        public void PaintZoneFill(ShapeMeshBuilder b, in ZoneSnap z, in ZoneLook look)
        {
            b.CircleSegments = _t.CircleSegments;
            var o = ToView(z.Pos) + look.OffsetCells;
            var d = DirView(z.Dir);
            float am = look.AlphaMul;
            // 1) 구역 안 바닥 어둡게(무해 구역은 옅게)
            AddRegion(b, z, o, d, DangerColor.WithAlpha(_t.DarkBase, (z.Harmless ? _t.HarmlessBaseAlpha : _t.DarkBaseAlpha) * am));
            // 2) 차오르는 채움
            var fillCol = z.Harmless ? DangerColor.WithAlpha(_t.HarmlessFill, _t.HarmlessFillAlpha * am) : DangerColor.WithAlpha(_t.DangerRed, look.FillAlpha * am);
            int t0 = b.TriangleCount;
            AddFill(b, z, o, d, look.Fill, fillCol);
            int t1 = b.TriangleCount;
            // 3) 색각 보조 줄무늬: 차오른 채움 위에만 더한다
            if (look.Colorblind && t1 > t0)
                b.AddStripes(b, t0, t1, Px(_t.ColorblindPitchPx), Px(_t.ColorblindStripePx), _t.ColorblindAngle,
                    DangerColor.WithAlpha(_t.ColorblindStripe, _t.ColorblindStripeAlpha * am));
            // 4) 앞선
            AddFront(b, z, o, d, look.Fill, am);
            // 5) 번쩍(마지막 2프레임 구역 전체 흰색). 한도에 걸렸으면 테두리 층이 대신 강조한다. 무해 구역은 번쩍이지 않는다.
            if (look.Flash && !look.FlashAsBorder && !z.Harmless) AddRegion(b, z, o, d, DangerColor.WithAlpha(_t.FlashWhite, am));
        }

        public void PaintZoneEdge(ShapeMeshBuilder b, in ZoneSnap z, in ZoneLook look)
        {
            b.CircleSegments = _t.CircleSegments;
            var o = ToView(z.Pos) + look.OffsetCells;
            var d = DirView(z.Dir);
            float am = look.AlphaMul;
            BuildLoops(z, o, d);
            Color32 outer = DangerColor.WithAlpha(_t.BorderOuter, am);
            Color32 mid = z.Harmless ? DangerColor.WithAlpha(_t.HarmlessEdge, am) : DangerColor.WithAlpha(_t.BorderMid, am);
            Color32 inner = z.Harmless ? DangerColor.WithAlpha(_t.BorderOuter, am) : DangerColor.WithAlpha(_t.BorderInner, am);
            StrokeLoops(b, 0f, Px(_t.BorderOuterPx), outer);
            StrokeLoops(b, Px(_t.BorderOuterPx), Px(_t.BorderOuterPx + _t.BorderMidPx), mid);
            StrokeLoops(b, Px(_t.BorderOuterPx + _t.BorderMidPx), Px(_t.BorderOuterPx + _t.BorderMidPx + _t.BorderInnerPx), inner);
            // 조준선의 흰 심(처음부터 2px)
            if (z.Shape == ZoneShape.AimLine)
            {
                float hc = Px(_t.AimLineCorePx) * 0.5f;
                b.AddRect(o, d, 0f, Mathf.Max(0f, z.Length), -hc, hc, DangerColor.WithAlpha(_t.FrontWhite, am));
            }
            AddFront(b, z, o, d, look.Fill, am);
            if (look.Flash && look.FlashAsBorder && !z.Harmless) StrokeLoops(b, 0f, Px(_t.FlashBorderPx), DangerColor.WithAlpha(_t.FlashWhite, am));
        }

        void AddRegion(ShapeMeshBuilder b, in ZoneSnap z, Vector2 o, Vector2 d, Color32 col)
        {
            if (IsFullCircle(z)) { b.AddAnnulusSector(o, 0f, z.Radius, 0f, 360f, col); return; }
            switch (z.Shape)
            {
                case ZoneShape.Ring: b.AddAnnulusSector(o, z.InnerRadius, z.Radius, 0f, 360f, col); break;
                case ZoneShape.Fan: b.AddAnnulusSector(o, 0f, z.Radius, Angle(d) - z.FanDegrees * 0.5f, z.FanDegrees, col); break;
                case ZoneShape.Band:
                case ZoneShape.AimLine:
                    b.AddRect(o, d, 0f, Mathf.Max(0f, z.Length), -z.Width * 0.5f, z.Width * 0.5f, col); break;
            }
        }

        void AddFill(ShapeMeshBuilder b, in ZoneSnap z, Vector2 o, Vector2 d, float fill, Color32 col)
        {
            float e = TelegraphGeometry.FillExtent(IsFullCircle(z) ? ZoneShape.Circle : z.Shape, z.Radius, z.InnerRadius, z.Length, fill, _t.AimLineMinPx, _t.AimLineMaxPx);
            if (IsFullCircle(z)) { b.AddAnnulusSector(o, 0f, e, 0f, 360f, col); return; }
            switch (z.Shape)
            {
                case ZoneShape.Ring: b.AddAnnulusSector(o, z.InnerRadius, e, 0f, 360f, col); break;
                case ZoneShape.Fan: b.AddAnnulusSector(o, 0f, e, Angle(d) - z.FanDegrees * 0.5f, z.FanDegrees, col); break;
                case ZoneShape.Band: b.AddRect(o, d, 0f, e, -z.Width * 0.5f, z.Width * 0.5f, col); break;
                case ZoneShape.AimLine:
                    float hw = Mathf.Min(Px(e) * 0.5f, Mathf.Max(z.Width, Px(e)) * 0.5f);
                    b.AddRect(o, d, 0f, Mathf.Max(0f, z.Length), -hw, hw, col);
                    break;
            }
        }

        /// <summary>앞선: 채운 쪽 흰 2px + 앞쪽 검정 2px. 조준선은 앞선 대신 굵기가 자란다.</summary>
        void AddFront(ShapeMeshBuilder b, in ZoneSnap z, Vector2 o, Vector2 d, float fill, float am)
        {
            if (fill <= 0f || fill >= 1f || z.Shape == ZoneShape.AimLine) return;
            float w = Px(_t.FrontWhitePx), k = Px(_t.FrontBlackPx);
            var white = DangerColor.WithAlpha(_t.FrontWhite, am);
            var black = DangerColor.WithAlpha(_t.FrontBlack, am);
            float e = TelegraphGeometry.FillExtent(IsFullCircle(z) ? ZoneShape.Circle : z.Shape, z.Radius, z.InnerRadius, z.Length, fill, _t.AimLineMinPx, _t.AimLineMaxPx);
            if (IsFullCircle(z) || z.Shape == ZoneShape.Ring || z.Shape == ZoneShape.Fan)
            {
                float lo = IsFullCircle(z) || z.Shape == ZoneShape.Fan ? 0f : z.InnerRadius;
                float start = 0f, sweep = 360f;
                if (z.Shape == ZoneShape.Fan && !IsFullCircle(z)) { start = Angle(d) - z.FanDegrees * 0.5f; sweep = z.FanDegrees; }
                b.AddAnnulusSector(o, Mathf.Max(lo, e - w), e, start, sweep, white);
                b.AddAnnulusSector(o, e, Mathf.Min(z.Radius, e + k), start, sweep, black);
            }
            else if (z.Shape == ZoneShape.Band)
            {
                float hw = z.Width * 0.5f;
                b.AddRect(o, d, Mathf.Max(0f, e - w), e, -hw, hw, white);
                b.AddRect(o, d, e, Mathf.Min(z.Length, e + k), -hw, hw, black);
            }
        }

        // ------------------------------------------------------------ 남는 구역

        /// <summary>남는 위험 구역: 빨강 빗금(차오르지 않음). 서리밭(피해 없음)은 흰 빗금.</summary>
        public void PaintHazardFill(ShapeMeshBuilder b, in ZoneSnap h, bool frost, float alphaMul)
        {
            b.CircleSegments = _t.CircleSegments;
            var o = ToView(h.Pos);
            var d = DirView(h.Dir);
            _scratch.Clear();
            AddRegion(_scratch, h, o, d, new Color32(255, 255, 255, 255));
            var col = DangerColor.WithAlpha(frost ? _t.FrostHatch : _t.HazardHatch, _t.HatchAlpha * alphaMul);
            b.AddStripes(_scratch, 0, _scratch.TriangleCount, Px(_t.HatchPitchPx), Px(_t.HatchPx), _t.HatchAngle, col);
        }

        /// <summary>남는 위험 구역: 3겹 테두리. 서리밭: 검정 + 자홍 테두리.</summary>
        public void PaintHazardEdge(ShapeMeshBuilder b, in ZoneSnap h, bool frost, float alphaMul)
        {
            b.CircleSegments = _t.CircleSegments;
            BuildLoops(h, ToView(h.Pos), DirView(h.Dir));
            if (frost)
            {
                StrokeLoops(b, 0f, Px(_t.FrostEdgeOuterPx), DangerColor.WithAlpha(_t.BorderOuter, alphaMul));
                StrokeLoops(b, Px(_t.FrostEdgeOuterPx), Px(_t.FrostEdgeOuterPx + _t.FrostEdgeMidPx), DangerColor.WithAlpha(_t.FrostEdge, alphaMul));
                return;
            }
            StrokeLoops(b, 0f, Px(_t.BorderOuterPx), DangerColor.WithAlpha(_t.BorderOuter, alphaMul));
            StrokeLoops(b, Px(_t.BorderOuterPx), Px(_t.BorderOuterPx + _t.BorderMidPx), DangerColor.WithAlpha(_t.BorderMid, alphaMul));
            StrokeLoops(b, Px(_t.BorderOuterPx + _t.BorderMidPx), Px(_t.BorderOuterPx + _t.BorderMidPx + _t.BorderInnerPx), DangerColor.WithAlpha(_t.BorderInner, alphaMul));
        }

        // ------------------------------------------------------------ 그 밖의 도형

        /// <summary>원판 위에 줄무늬만(색각 보조의 적 탄 주황 줄무늬).</summary>
        public void PaintStripedDisc(ShapeMeshBuilder b, Vector2 viewCenter, float radius, float pitch, float width, float angle, Color32 col)
        {
            _scratch.Clear();
            _scratch.CircleSegments = 16;
            _scratch.AddAnnulusSector(viewCenter, 0f, radius, 0f, 360f, new Color32(255, 255, 255, 255));
            b.AddStripes(_scratch, 0, _scratch.TriangleCount, pitch, width, angle, col);
            _scratch.CircleSegments = _t.CircleSegments;
        }

        // ------------------------------------------------------------ 테두리 고리

        void BuildLoops(in ZoneSnap z, Vector2 o, Vector2 d)
        {
            _outer.Clear();
            _inner.Clear();
            int n = _t.CircleSegments;
            if (IsFullCircle(z) || z.Shape == ZoneShape.Ring)
            {
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    _outer.Add(o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * z.Radius);
                }
                if (z.Shape == ZoneShape.Ring && z.InnerRadius > 0f)
                    for (int i = n - 1; i >= 0; i--) // 시계 방향: 왼쪽이 고리 안
                    {
                        float a = i * Mathf.PI * 2f / n;
                        _inner.Add(o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * z.InnerRadius);
                    }
                return;
            }
            if (z.Shape == ZoneShape.Fan)
            {
                float sweep = Mathf.Clamp(z.FanDegrees, 1f, 359f), start = Angle(d) - sweep * 0.5f;
                int m = Mathf.Max(2, Mathf.CeilToInt(n * sweep / 360f));
                _outer.Add(o);
                for (int i = 0; i <= m; i++)
                {
                    float a = (start + sweep * i / m) * Mathf.Deg2Rad;
                    _outer.Add(o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * z.Radius);
                }
            }
            else
            {
                var side = new Vector2(-d.y, d.x);
                float hw = z.Width * 0.5f, len = Mathf.Max(0f, z.Length);
                _outer.Add(o - side * hw);
                _outer.Add(o + d * len - side * hw);
                _outer.Add(o + d * len + side * hw);
                _outer.Add(o + side * hw);
            }
            if (ShapeMeshBuilder.SignedArea(_outer) < 0f) _outer.Reverse();
        }

        void StrokeLoops(ShapeMeshBuilder b, float d0, float d1, Color32 col)
        {
            b.AddLoopStroke(_outer, d0, d1, col);
            if (_inner.Count > 0) b.AddLoopStroke(_inner, d0, d1, col);
        }

        static float Angle(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }
}
