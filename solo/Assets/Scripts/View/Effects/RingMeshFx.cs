using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 퍼지는 고리(충격파)를 얇은 안티에일리어스 선으로 그린다 — 예전 코드 원 그림(Circle 스프라이트를 키우던 것)은 반경에 비례해 굵어지고 계단졌다.
    /// - 두께는 반경과 무관한 그림 px(1칸 = 64px): 가운데 선 width_px + 바깥·안쪽 깃털 feather_px(알파 0으로 녹아든다) + 안쪽 은은한 빛 띠 glow_px.
    /// - 큰 고리(min_cells 이상)는 BigRingMerger를 거친다: 동시에 max_live개까지, 겹치면 하나로 합쳐 더 크게 한 번. 안쪽에 얇은 메아리 선 하나.
    /// - 정점마다 누적 가림 상한(OcclusionField.CapAt)을 건다 → 예고 구역을 지나는 부분만 옅어진다(0%면 그 자리에서 끊긴다).
    /// - '내 이펙트 불투명도'를 곱한다. 가산 혼합 없음(지름 2칸 이상 금지 — 10 3.1절). 정점 색은 sRGB(Solo/Shape가 선형으로 바꾼다).
    /// 모든 고리를 메시 하나(작은 고리)와 하나(큰 고리)에 쌓아 그린다(그리기 호출 둘).
    /// 수치는 Juice.json readability.big_ring / small_ring(표시 전용, 솔로 임시).
    /// </summary>
    public sealed class RingMeshFx
    {
        sealed class R
        {
            public Vector2 Pos;
            public float FromR, ToR, Life, Age, Delay;
            public Color Color;
            public bool Big;
            public int BigId;
            public bool Shrink;
            public System.Func<Vector2?> Follow;
            public float WidthMul = 1f;
            public bool Mine = true;
        }

        public struct Style
        {
            public float WidthPx, FeatherPx, GlowPx, GlowAlpha, EchoAlpha;
            public int Segments, MaxLive;
        }

        readonly EffectsShared _s;
        readonly List<R> _live = new List<R>();
        readonly ShapeMeshBuilder _small = new ShapeMeshBuilder(), _big = new ShapeMeshBuilder();
        readonly BigRingMerger _merger = new BigRingMerger();
        MeshSlot _smallSlot, _bigSlot;
        float _clock;
        bool _styled;
        public Style SmallStyle, BigStyle;
        public float BigMinCells = 2.5f;
        /// <summary>그리는 띠(내 이펙트 띠 안).</summary>
        public int SortingOrder = SortBands.Bursts + 5;

        public RingMeshFx(EffectsShared shared) { _s = shared; }

        public int LiveCount => _live.Count;
        public int BigLiveCount { get { int n = 0; for (int i = 0; i < _live.Count; i++) if (_live[i].Big) n++; return n; } }
        public int Merged { get; private set; }
        public BigRingMerger Merger => _merger;
        public ShapeMeshBuilder BigMesh => _big;
        public ShapeMeshBuilder SmallMesh => _small;

        void EnsureStyle()
        {
            if (_styled) return;
            _styled = true;
            var j = _s != null ? _s.Juice : null;
            SmallStyle = ReadStyle(j, "small_ring", new Style { WidthPx = 1.5f, FeatherPx = 1.25f, GlowPx = 6f, GlowAlpha = 0.16f, Segments = 48, MaxLive = 24 });
            BigStyle = ReadStyle(j, "big_ring", new Style { WidthPx = 2.5f, FeatherPx = 1.75f, GlowPx = 20f, GlowAlpha = 0.2f, EchoAlpha = 0.4f, Segments = 112, MaxLive = 2 });
            if (j != null)
            {
                if (j.F("readability", "big_ring", "min_cells") > 0f) BigMinCells = j.F("readability", "big_ring", "min_cells");
                if (j.F("readability", "big_ring", "merge_seconds") > 0f) _merger.MergeWindow = j.F("readability", "big_ring", "merge_seconds");
                if (j.F("readability", "big_ring", "merge_grow") > 0f) _merger.MergeGrow = j.F("readability", "big_ring", "merge_grow");
                if (j.F("readability", "big_ring", "max_cells") > 0f) _merger.MaxRadius = j.F("readability", "big_ring", "max_cells");
            }
            _merger.MaxLive = Mathf.Max(1, BigStyle.MaxLive);
        }

        static Style ReadStyle(EffectsTuning j, string key, Style d)
        {
            if (j == null) return d;
            float F(string k, float v) => j.Token("readability", key, k) != null ? j.F("readability", key, k) : v;
            d.WidthPx = F("width_px", d.WidthPx);
            d.FeatherPx = F("feather_px", d.FeatherPx);
            d.GlowPx = F("glow_px", d.GlowPx);
            d.GlowAlpha = F("glow_alpha", d.GlowAlpha);
            d.EchoAlpha = F("echo_alpha", d.EchoAlpha);
            d.Segments = Mathf.Max(12, Mathf.RoundToInt(F("segments", d.Segments)));
            d.MaxLive = Mathf.Max(1, Mathf.RoundToInt(F("max_live", d.MaxLive)));
            return d;
        }

        /// <summary>
        /// 고리 하나(반지름 칸, 시뮬레이션 좌표). 큰 고리는 합치기·동시 상한을 거친다. 그리면 true(합쳐졌어도 true).
        /// shrink = 바깥에서 안으로 오므라든다(fromR &gt; toR도 그대로 된다 — 이것은 깃털 방향만 바꾼다).
        /// </summary>
        public bool Add(Vector2 pos, float fromR, float toR, float life, Color color, float delay = 0f, System.Func<Vector2?> follow = null,
            float widthMul = 1f, bool mine = true)
        {
            EnsureStyle();
            if (life <= 0f || color.a <= 0f) return false;
            bool big = Mathf.Max(fromR, toR) >= BigMinCells && follow == null;
            if (big)
            {
                bool merged;
                int id = _merger.Request(pos, fromR, toR, life + delay, color, _clock, out merged);
                if (merged)
                {
                    Merged++;
                    if (!_merger.LastGrew) return true; // 작은 메아리는 앞 고리에 흡수
                    BigRingMerger.Ring mr;
                    if (_merger.TryGet(id, out mr))
                        for (int i = 0; i < _live.Count; i++)
                            if (_live[i].Big && _live[i].BigId == id)
                            {
                                var r0 = _live[i];
                                r0.Pos = mr.Pos; r0.FromR = mr.FromR; r0.ToR = mr.ToR; r0.Life = mr.Life; r0.Color = mr.Color;
                                r0.Age = 0f; r0.Delay = 0f;
                                return true;
                            }
                    // 합칠 고리가 그려지고 있지 않으면(지운 뒤) 새로 그린다
                }
                if (BigLiveCount >= _merger.MaxLive) return false;
                _live.Add(new R { Pos = pos, FromR = fromR, ToR = toR, Life = life, Delay = delay, Color = color, Big = true, BigId = id, Mine = mine });
                return true;
            }
            int max = Mathf.Max(1, SmallStyle.MaxLive);
            int small = _live.Count - BigLiveCount;
            if (small >= max)
            {
                // 가장 오래된 작은 고리를 내보낸다(새 사건은 늘 보인다)
                for (int i = 0; i < _live.Count; i++) if (!_live[i].Big) { _live.RemoveAt(i); break; }
            }
            _live.Add(new R { Pos = pos, FromR = fromR, ToR = toR, Life = life, Delay = delay, Color = color, Follow = follow, Shrink = toR < fromR, WidthMul = widthMul, Mine = mine });
            return true;
        }

        public void Update(float dt)
        {
            EnsureStyle();
            _clock += dt;
            _merger.Prune(_clock);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var r = _live[i];
                r.Age += dt;
                if (r.Age >= r.Delay + r.Life) { _live.RemoveAt(i); continue; }
                if (r.Follow != null) { var p = r.Follow(); if (p.HasValue) r.Pos = p.Value; }
            }
            Build();
        }

        void Build()
        {
            var vc = _s != null ? _s.Vc : null;
            _small.Clear();
            _big.Clear();
            if (vc == null) { Upload(); return; }
            float my = _s.MyFxAlpha;
            for (int i = 0; i < _live.Count; i++)
            {
                var r = _live[i];
                if (r.Age < r.Delay) continue;
                float k = Mathf.Clamp01((r.Age - r.Delay) / Mathf.Max(1e-3f, r.Life));
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                float radius = Mathf.LerpUnclamped(r.FromR, r.ToR, ease);
                // 처음 15%는 또렷이, 그 뒤로 부드럽게 꺼진다(제곱근 곡선)
                float fade = k < 0.15f ? 1f : Mathf.Pow(1f - (k - 0.15f) / 0.85f, 1.6f);
                float a = r.Color.a * fade * (r.Mine ? my : 1f);
                if (a <= 0.004f || radius <= 0.01f) continue;
                var st = r.Big ? BigStyle : SmallStyle;
                // 퍼질수록 조금 가늘어진다(두께는 반경과 무관)
                float px = 1f / SimConst.PixelsPerUnit;
                float w = st.WidthPx * r.WidthMul * px * Mathf.Lerp(1.25f, 0.7f, k);
                float f = st.FeatherPx * px;
                float g = st.GlowPx * px * Mathf.Lerp(0.6f, 1f, ease);
                var b = r.Big ? _big : _small;
                Vector3 c3 = vc.ToView(r.Pos);
                AddRing(b, r.Pos, new Vector2(c3.x, c3.y), radius, w, f, r.Shrink ? -g : g, a, st.GlowAlpha, r.Color, st.Segments, r.Mine);
                if (r.Big && st.EchoAlpha > 0f && radius * 0.86f > 0.2f)
                    AddRing(b, r.Pos, new Vector2(c3.x, c3.y), radius * 0.86f, w * 0.55f, f, 0f, a * st.EchoAlpha, 0f, Color.Lerp(r.Color, Color.white, 0.35f), st.Segments, r.Mine);
            }
            Upload();
        }

        /// <summary>
        /// 고리 하나를 쌓는다: 바깥 깃털(0) → 선(a) → 선(a) → 안 깃털 끝(a·glow) → 빛 띠 끝(0). 시뮬레이션 y는 아래가 +라 화면 y를 뒤집는다.
        /// glow가 음수면 빛 띠를 바깥에 둔다(오므라드는 고리).
        /// </summary>
        void AddRing(ShapeMeshBuilder b, Vector2 simCenter, Vector2 viewCenter, float r, float w, float f, float glow, float a, float glowA, Color col, int seg, bool mine)
        {
            float h = w * 0.5f;
            float outer = glow < 0f ? -glow : 0f, inner = glow > 0f ? glow : 0f;
            float[] radii = _radii;
            radii[0] = r + h + f + outer;
            radii[1] = r + h + f;
            radii[2] = r + h;
            radii[3] = r - h;
            radii[4] = Mathf.Max(0f, r - h - f);
            radii[5] = Mathf.Max(0f, r - h - f - inner);
            float[] alpha = _alpha;
            alpha[0] = 0f;
            alpha[1] = outer > 0f ? glowA : 0f;
            alpha[2] = 1f;
            alpha[3] = 1f;
            alpha[4] = inner > 0f ? glowA : 0f;
            alpha[5] = 0f;
            int rings = 6;
            int baseIdx = b.Vertices.Count;
            var occ = _s.Occlusion;
            for (int sIdx = 0; sIdx <= seg; sIdx++)
            {
                float ang = sIdx * Mathf.PI * 2f / seg;
                float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                float capA = a;
                if (mine && occ != null)
                {
                    // 선 위의 그 자리 상한(시뮬레이션 좌표 — y는 아래가 +)
                    float cap = occ.CapAt(simCenter + new Vector2(cs * r, -sn * r));
                    if (capA > cap) capA = cap;
                }
                for (int k = 0; k < rings; k++)
                {
                    b.Vertices.Add(new Vector3(viewCenter.x + cs * radii[k], viewCenter.y + sn * radii[k], 0f));
                    var c = col;
                    c.a = capA * alpha[k];
                    b.Colors.Add(c);
                }
            }
            for (int sIdx = 0; sIdx < seg; sIdx++)
            {
                int i0 = baseIdx + sIdx * rings, i1 = i0 + rings;
                for (int k = 0; k + 1 < rings; k++)
                {
                    if (radii[k] - radii[k + 1] <= 1e-5f) continue;
                    if (alpha[k] <= 0f && alpha[k + 1] <= 0f) continue;
                    b.Triangles.Add(i0 + k); b.Triangles.Add(i1 + k); b.Triangles.Add(i1 + k + 1);
                    b.Triangles.Add(i0 + k); b.Triangles.Add(i1 + k + 1); b.Triangles.Add(i0 + k + 1);
                }
            }
        }

        readonly float[] _radii = new float[6], _alpha = new float[6];

        void Upload()
        {
            var vc = _s != null ? _s.Vc : null;
            if (vc == null || vc.Pool == null) return;
            if (_small.TriangleCount > 0 && _smallSlot == null) _smallSlot = NewSlot(vc, SortingOrder);
            if (_big.TriangleCount > 0 && _bigSlot == null) _bigSlot = NewSlot(vc, SortingOrder + 1);
            if (_smallSlot != null) _smallSlot.Upload(_small);
            if (_bigSlot != null) _bigSlot.Upload(_big);
        }

        static MeshSlot NewSlot(ViewContext vc, int order)
        {
            var slot = new MeshSlot(vc.Pool, order, vc.WorldRoot);
            if (slot.Renderer != null)
            {
                var t = slot.Renderer.transform;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }
            return slot;
        }

        public void DisposeAll()
        {
            _live.Clear();
            _merger.Clear();
            if (_smallSlot != null) _smallSlot.Release();
            if (_bigSlot != null) _bigSlot.Release();
            _smallSlot = _bigSlot = null;
            _small.Clear();
            _big.Clear();
        }
    }
}
