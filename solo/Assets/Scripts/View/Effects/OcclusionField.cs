using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 누적 가림 상한(순수, 10 요약서 3.2절 — 오프스크린 버퍼 없이 넓이 셈, 00 문서 7절).
    /// 7·8층(내 탄, 꼬리, 적중 불꽃, 폭발, 처치 조각, 비기 효과)의 반투명 일회 효과를 원(중심·반지름·불투명도)으로 센다.
    /// - 자리별 상한: 예고 마스크 밖 80% / 안 40% / 안이면서 그 예고가 번쩍이는 마지막 2프레임 0%.
    ///   예고 마스크 = 떠 있는 예고 구역 + 남는 위험 구역을 바깥으로 0.25칸 넓힌 모양.
    /// - 새 효과: 그 원 안에 이미 떠 있는 효과들의 평균 덮임(Σ 불투명도 × 겹친 넓이 / 새 원 넓이)을 상한에서 빼고 남은 만큼만 준다.
    ///   남은 것이 아주 작으면(min_visible_alpha 미만) 생략(0).
    /// - 상한은 내 탄 투명도 옵션을 곱한 뒤에 적용한다(옵션으로 못 올림) — 부르는 쪽이 옵션을 먼저 곱해 넘긴다.
    /// 3층(내 지대)·적 탄(10층)은 세지 않는다.
    /// </summary>
    public sealed class OcclusionField
    {
        public struct MaskShape
        {
            public ZoneShape Shape;
            public Vector2 Pos, Dir;
            public float Radius, InnerRadius, Width, Length, FanDegrees;
            public bool Flashing;
        }

        struct Live { public int Handle; public Vector2 Center; public float Radius; public float Alpha; }

        public float OutsideCap, InsideCap, FlashCap, MaskPad, MinVisibleAlpha;
        public int FlashTicks;

        readonly List<MaskShape> _mask = new List<MaskShape>();
        readonly List<Live> _live = new List<Live>();
        int _nextHandle = 1;

        public static OcclusionField FromTuning(EffectsTuning t) => new OcclusionField
        {
            OutsideCap = t.F("occlusion", "outside_cap"),
            InsideCap = t.F("occlusion", "inside_cap"),
            FlashCap = t.F("occlusion", "flash_cap"),
            MaskPad = t.F("occlusion", "mask_pad_cells"),
            FlashTicks = t.I("occlusion", "flash_ticks"),
            MinVisibleAlpha = t.F("occlusion", "min_visible_alpha")
        };

        public int MaskCount => _mask.Count;
        public int LiveCount => _live.Count;

        // ------------------------------------------------------------ 마스크

        public void ClearMask() => _mask.Clear();
        public void AddMask(MaskShape s) => _mask.Add(s);

        /// <summary>지금 세계의 예고 구역·남는 위험 구역으로 마스크를 다시 만든다(매 프레임).</summary>
        public void RebuildMask(SimWorld w)
        {
            _mask.Clear();
            if (w == null) return;
            for (int i = 0; i < w.Zones.Count; i++)
            {
                var z = w.Zones[i];
                if (z.Cancelled || z.Detonated || z.Harmless) continue;
                _mask.Add(new MaskShape
                {
                    Shape = z.Shape, Pos = z.Pos, Dir = z.Dir, Radius = z.Radius, InnerRadius = z.InnerRadius,
                    Width = z.Width, Length = z.Length, FanDegrees = z.FanDegrees,
                    Flashing = z.DetonateTick - w.Tick <= FlashTicks
                });
            }
            for (int i = 0; i < w.Hazards.Count; i++)
            {
                var h = w.Hazards[i];
                if (h.Dead) continue;
                _mask.Add(new MaskShape { Shape = h.Shape, Pos = h.Pos, Dir = h.Dir, Radius = h.Radius, Width = h.Width, Length = h.Length });
            }
        }

        /// <summary>그 자리의 불투명도 상한.</summary>
        public float CapAt(Vector2 p)
        {
            bool inside = false;
            for (int i = 0; i < _mask.Count; i++)
            {
                if (!Contains(_mask[i], p, MaskPad)) continue;
                if (_mask[i].Flashing) return FlashCap;
                inside = true;
            }
            return inside ? InsideCap : OutsideCap;
        }

        public static bool Contains(MaskShape s, Vector2 p, float pad)
        {
            Vector2 d = p - s.Pos;
            float dist = d.magnitude;
            switch (s.Shape)
            {
                case ZoneShape.Circle:
                    return dist <= s.Radius + pad;
                case ZoneShape.Ring:
                    return dist <= s.Radius + pad && dist >= s.InnerRadius - pad;
                case ZoneShape.Band:
                case ZoneShape.AimLine:
                {
                    Vector2 dir = s.Dir.sqrMagnitude > 0f ? s.Dir.normalized : Vector2.right;
                    float along = Vector2.Dot(d, dir);
                    float across = Mathf.Abs(dir.x * d.y - dir.y * d.x);
                    return along >= -pad && along <= s.Length + pad && across <= s.Width * 0.5f + pad;
                }
                case ZoneShape.Fan:
                {
                    if (dist > s.Radius + pad) return false;
                    if (dist <= pad) return true;
                    Vector2 dir = s.Dir.sqrMagnitude > 0f ? s.Dir.normalized : Vector2.right;
                    float ang = Vector2.Angle(dir, d);
                    float padDeg = Mathf.Atan2(pad, dist) * Mathf.Rad2Deg;
                    return ang <= s.FanDegrees * 0.5f + padDeg;
                }
            }
            return false;
        }

        // ------------------------------------------------------------ 떠 있는 효과

        /// <summary>새 효과의 불투명도를 상한에 맞춰 준다(넘는 몫을 줄이고, 너무 작으면 0 = 생략).</summary>
        public float Admit(Vector2 center, float radius, float wanted)
        {
            if (wanted <= 0f || radius <= 0f) return 0f;
            float cap = CapAt(center);
            float covered = Coverage(center, radius);
            float allowed = cap - covered;
            if (allowed > wanted) allowed = wanted;
            if (allowed < MinVisibleAlpha) return 0f;
            return allowed;
        }

        /// <summary>그 원 안의 평균 덮임(이미 떠 있는 효과들의 불투명도 × 겹친 넓이 / 원 넓이).</summary>
        public float Coverage(Vector2 center, float radius)
        {
            float area = Mathf.PI * radius * radius;
            if (area <= 0f) return 0f;
            float s = 0f;
            for (int i = 0; i < _live.Count; i++)
            {
                var l = _live[i];
                s += l.Alpha * CircleOverlap(center, radius, l.Center, l.Radius);
            }
            return s / area;
        }

        public int Register(Vector2 center, float radius, float alpha)
        {
            int h = _nextHandle++;
            _live.Add(new Live { Handle = h, Center = center, Radius = radius, Alpha = alpha });
            return h;
        }

        public void UpdateLive(int handle, Vector2 center, float radius, float alpha)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i].Handle != handle) continue;
                _live[i] = new Live { Handle = handle, Center = center, Radius = radius, Alpha = alpha };
                return;
            }
        }

        public void Unregister(int handle)
        {
            for (int i = 0; i < _live.Count; i++) if (_live[i].Handle == handle) { _live.RemoveAt(i); return; }
        }

        public void Clear() { _live.Clear(); _mask.Clear(); }

        /// <summary>두 원이 겹친 넓이.</summary>
        public static float CircleOverlap(Vector2 c1, float r1, Vector2 c2, float r2)
        {
            float d = Vector2.Distance(c1, c2);
            if (d >= r1 + r2) return 0f;
            float rs = Mathf.Min(r1, r2), rb = Mathf.Max(r1, r2);
            if (d <= rb - rs) return Mathf.PI * rs * rs;
            float a1 = r1 * r1 * Mathf.Acos(Mathf.Clamp((d * d + r1 * r1 - r2 * r2) / (2f * d * r1), -1f, 1f));
            float a2 = r2 * r2 * Mathf.Acos(Mathf.Clamp((d * d + r2 * r2 - r1 * r1) / (2f * d * r2), -1f, 1f));
            float k = 0.5f * Mathf.Sqrt(Mathf.Max(0f, (-d + r1 + r2) * (d + r1 - r2) * (d - r1 + r2) * (d + r1 + r2)));
            return a1 + a2 - k;
        }
    }
}
