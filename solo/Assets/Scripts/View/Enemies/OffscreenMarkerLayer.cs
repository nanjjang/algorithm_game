using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>
    /// 화면 밖 표시(카메라가 플레이어를 따라가며 전장 일부만 보이게 된 뒤로 필요해진 것). 화면 가장자리 안쪽에 화살 촉을 둔다:
    /// - 예고 구역: 모양이 화면에 한 점도 걸리지 않으면 위험색 그대로(#FF3B30 — 적의 공격 예고라 허용된 곳), 판정이 다가올수록 빨리 깜빡인다.
    /// - 보스: 흰 큰 화살 + 깜빡임. 엘리트: 금색. 보통 적: 옅은 회백(가까울수록 진하게).
    /// - 화면 밖 등장 예고(EnemySpawnNotice — 출현 직전 덤불 흔들림 동안): 그 가장자리에 작은 화살 + 흔들리는 잎 셋. 이벤트가 없으면 위의 표식만.
    /// 줄일 수 없는 표시다(예고·적 위치 — 연출 감도와 무관). 위아래는 HUD 띠만큼 더 들어와 HUD에 가리지 않는다.
    /// 그리는 띠: 전장의 모든 것 위(FxLayers.Offscreen — 적 탄·예고 테두리보다도 위, HUD 캔버스 아래).
    /// 수치는 Juice.json offscreen 절(표시 전용, 솔로 임시).
    /// </summary>
    public sealed class OffscreenMarkerLayer : IWorldViewLayer
    {
        struct Target
        {
            public Vector2 View;   // 가리킬 점(view 좌표)
            public Color Color;
            public float Cells;
            public float Blink;    // 초당 깜빡임(0 = 없음)
            public int Order;
            public float Priority; // 작을수록 먼저
        }

        struct Incoming { public Vector2 Sim; public int StartTick, EndTick; }

        readonly List<Incoming> _incoming = new List<Incoming>();
        readonly List<SpriteRenderer> _spawnArrows = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _leaves = new List<SpriteRenderer>();
        public int SpawnWarningsVisible { get; private set; }
        public int PendingSpawnNotices => _incoming.Count;

        ViewContext _vc;
        ISpritePool _pool;
        EffectsTuning _j;
        readonly List<SpriteRenderer> _arrows = new List<SpriteRenderer>();
        readonly List<Target> _targets = new List<Target>();
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        float _clock;

        public int VisibleCount { get; private set; }

        public void Build(ViewContext vc, SimWorld w)
        {
            Dispose();
            _vc = vc;
            _pool = vc != null ? vc.Pool : null;
            var content = vc != null && vc.Game != null && vc.Game.Content != null ? vc.Game.Content : (w != null ? w.Content : null);
            Newtonsoft.Json.Linq.JObject root = null;
            try { root = content != null ? content.SoloExtra(JuiceLayer.JuiceName) : null; } catch (System.Exception) { root = null; }
            _j = new EffectsTuning(root);
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (e.Type != SimEventType.EnemySpawnNotice) return;
            // A = 예고 길이(틱) 또는 끝 틱(보스 졸개 부르기) — EnemyLayer와 같은 읽기
            int end = e.A > e.Tick ? e.A : e.Tick + (e.A > 0 ? e.A : SimConst.Ticks(0.5f));
            _incoming.Add(new Incoming { Sim = e.Pos, StartTick = e.Tick, EndTick = end });
        }

        float J(string key, float fallback)
        {
            float v = _j != null ? _j.F("offscreen", key) : 0f;
            return v > 0f ? v : fallback;
        }

        Color C(string key, Color fallback) => EffectsTuning.ParseColor(_j != null ? _j.S("offscreen", key) : null, fallback);

        public void Render(SimWorld w, float alpha, float dt)
        {
            _clock += Mathf.Max(dt, Time.unscaledDeltaTime);
            VisibleCount = 0;
            var cam = _vc != null ? _vc.Camera : null;
            if (cam == null || _pool == null || w == null || !cam.orthographic) { HideFrom(0); return; }

            float halfH = cam.orthographicSize;
            float pw = cam.pixelWidth > 0 ? cam.pixelWidth : Screen.width, ph = cam.pixelHeight > 0 ? cam.pixelHeight : Screen.height;
            if (ph <= 0f) { HideFrom(0); return; }
            float halfW = halfH * pw / ph;
            Vector3 cp = cam.transform.position;
            Vector2 center = new Vector2(cp.x, cp.y);
            // 전장 루트 기준이 아니라 월드 기준 — 전장 루트는 원점에 있다(WorldViewHost)
            float unitPx = ph / (2f * halfH);
            float ui = Wizard.Solo.UI.UiScale.Compute(Screen.width, Screen.height);
            float edge = J("edge_px", 34f) * ui / unitPx;
            float top = J("hud_top_px", 120f) * ui / unitPx;
            float bottom = J("hud_bottom_px", 92f) * ui / unitPx;
            Rect view = new Rect(center.x - halfW, center.y - halfH, halfW * 2f, halfH * 2f);
            Rect inner = Rect.MinMaxRect(view.xMin + edge, view.yMin + bottom, view.xMax - edge, view.yMax - top);
            if (inner.width <= 0f || inner.height <= 0f) { HideFrom(0); return; }

            _targets.Clear();
            CollectTelegraphs(w, center, view);
            CollectEnemies(w, center, view);
            _targets.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            int maxTotal = Mathf.RoundToInt(J("max_enemies", 24f) + J("max_telegraphs", 12f));
            int n = Mathf.Min(_targets.Count, maxTotal);
            var arrow = ProcSprites.Arrow;
            for (int i = 0; i < n; i++)
            {
                var t = _targets[i];
                Vector2 d = t.View - center;
                if (d.sqrMagnitude < 1e-6f) continue;
                // 가운데 → 대상 선이 안쪽 사각형 테두리와 만나는 점
                float sx = d.x != 0f ? ((d.x > 0f ? inner.xMax : inner.xMin) - center.x) / d.x : float.MaxValue;
                float sy = d.y != 0f ? ((d.y > 0f ? inner.yMax : inner.yMin) - center.y) / d.y : float.MaxValue;
                float s = Mathf.Max(0f, Mathf.Min(sx, sy));
                Vector2 at = center + d * s;

                var sr = Arrow(VisibleCount, arrow, t.Order);
                if (sr == null) break;
                VisibleCount++;
                float scale = Fit(arrow, t.Cells);
                float a = t.Color.a;
                if (t.Blink > 0f) a *= 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_clock * t.Blink * Mathf.PI * 2f));
                sr.transform.position = new Vector3(at.x, at.y, 0f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                var c = t.Color;
                c.a = a;
                sr.color = c;
            }
            HideFrom(VisibleCount);
            RenderIncoming(w, center, view, inner);
        }

        /// <summary>화면 밖에서 곧 나올 적: 가장자리 작은 화살(맥박) + 흔들리는 잎 셋. 화면 안이면 전장의 덤불 흔들림(ArenaLayer)이 맡는다.</summary>
        void RenderIncoming(SimWorld w, Vector2 center, Rect view, Rect inner)
        {
            SpawnWarningsVisible = 0;
            for (int i = _incoming.Count - 1; i >= 0; i--) if (w.Tick > _incoming[i].EndTick) _incoming.RemoveAt(i);
            int max = Mathf.RoundToInt(J("max_spawn_notices", 8f));
            Color arrowC = C("spawn_color", new Color32(0xFF, 0xD9, 0xA0, 0xFF));
            Color leafC = C("spawn_leaf_color", new Color32(0x9C, 0xC8, 0x7E, 0xFF));
            float cells = J("spawn_cells", 0.3f);
            int used = 0;
            for (int i = 0; i < _incoming.Count && used < max; i++)
            {
                var n = _incoming[i];
                Vector3 v3 = _vc.ToView(n.Sim);
                Vector2 v = new Vector2(v3.x, v3.y);
                if (view.Contains(v)) continue;
                Vector2 d = v - center;
                if (d.sqrMagnitude < 1e-6f) continue;
                float sx = d.x != 0f ? ((d.x > 0f ? inner.xMax : inner.xMin) - center.x) / d.x : float.MaxValue;
                float sy = d.y != 0f ? ((d.y > 0f ? inner.yMax : inner.yMin) - center.y) / d.y : float.MaxValue;
                Vector2 at = center + d * Mathf.Max(0f, Mathf.Min(sx, sy));
                float k = Mathf.Clamp01((w.Tick - n.StartTick) / (float)Mathf.Max(1, n.EndTick - n.StartTick));
                Vector2 dir = d.normalized;
                var arrow = Rent(_spawnArrows, used, ProcSprites.Arrow, FxLayers.Offscreen + 5);
                if (arrow == null) break;
                float pulse = 1f + 0.18f * Mathf.Sin(_clock * 14f);
                arrow.transform.position = new Vector3(at.x, at.y, 0f);
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                float sc = Fit(ProcSprites.Arrow, cells * pulse);
                arrow.transform.localScale = new Vector3(sc, sc, 1f);
                var ac = arrowC; ac.a = Mathf.Lerp(0.65f, 1f, k);
                arrow.color = ac;
                // 잎 셋: 화살 뒤(안쪽)에서 부채꼴로, 출현이 다가올수록 세게 흔들린다
                Vector2 back = at - dir * cells * 0.9f;
                Vector2 side = new Vector2(-dir.y, dir.x);
                for (int l = 0; l < 3; l++)
                {
                    var leaf = Rent(_leaves, used * 3 + l, ProcSprites.HardDot, FxLayers.Offscreen + 4);
                    if (leaf == null) break;
                    float off = (l - 1) * cells * 0.45f;
                    float jig = Mathf.Sin(_clock * (22f + l * 5f) + l * 1.7f) * cells * Mathf.Lerp(0.06f, 0.16f, k);
                    Vector2 p = back + side * (off + jig) - dir * (l == 1 ? cells * 0.15f : 0f);
                    leaf.transform.position = new Vector3(p.x, p.y, 0f);
                    float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (l - 1) * 35f + jig * 120f;
                    leaf.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                    float ls = Fit(ProcSprites.HardDot, cells * 0.42f);
                    leaf.transform.localScale = new Vector3(ls * 1.7f, ls * 0.75f, 1f);
                    var lc = leafC; lc.a = Mathf.Lerp(0.7f, 1f, k);
                    leaf.color = lc;
                }
                used++;
            }
            SpawnWarningsVisible = used;
            for (int i = used; i < _spawnArrows.Count; i++) if (_spawnArrows[i] != null) _spawnArrows[i].enabled = false;
            for (int i = used * 3; i < _leaves.Count; i++) if (_leaves[i] != null) _leaves[i].enabled = false;
        }

        SpriteRenderer Rent(List<SpriteRenderer> list, int i, Sprite sprite, int order)
        {
            while (list.Count <= i)
            {
                var sr = _pool.Rent(sprite, order);
                if (sr == null) return null;
                _mpb.Clear();
                _mpb.SetColor(SpriteShaderProps.OutlineColor, new Color(0.05f, 0.05f, 0.08f, 1f));
                _mpb.SetFloat(SpriteShaderProps.OutlineSize, 1f);
                sr.SetPropertyBlock(_mpb);
                list.Add(sr);
            }
            var r = list[i];
            r.enabled = true;
            return r;
        }

        static float Fit(Sprite s, float cells)
        {
            if (s == null) return 1f;
            float native = Mathf.Max(s.rect.width, s.rect.height) / Mathf.Max(1f, s.pixelsPerUnit);
            return native > 0f ? cells / native : 1f;
        }

        SpriteRenderer Arrow(int i, Sprite sprite, int order)
        {
            while (_arrows.Count <= i)
            {
                var sr = _pool.Rent(sprite, order);
                if (sr == null) return null;
                // 어두운 테두리로 어느 바닥 위에서도 읽히게
                _mpb.Clear();
                _mpb.SetColor(SpriteShaderProps.OutlineColor, new Color(0.05f, 0.05f, 0.08f, 1f));
                _mpb.SetFloat(SpriteShaderProps.OutlineSize, 1f);
                sr.SetPropertyBlock(_mpb);
                _arrows.Add(sr);
            }
            var r = _arrows[i];
            r.enabled = true;
            if (r.sortingOrder != order) r.sortingOrder = order;
            return r;
        }

        void HideFrom(int i)
        {
            for (int k = i; k < _arrows.Count; k++) if (_arrows[k] != null) _arrows[k].enabled = false;
        }

        // ------------------------------------------------------------ 대상

        void CollectEnemies(SimWorld w, Vector2 center, Rect view)
        {
            float near = J("near_cells", 6f);
            float farA = J("far_alpha", 0.55f);
            int max = Mathf.RoundToInt(J("max_enemies", 24f));
            int added = 0;
            for (int i = 0; i < w.Enemies.Count && added < max; i++)
            {
                var e = w.Enemies[i];
                if (e == null || e.Hp <= 0f || e.Ai == EnemyAiState.Spawning) continue;
                Vector3 v3 = _vc.ToView(e.Pos);
                Vector2 v = new Vector2(v3.x, v3.y);
                float r = Mathf.Max(0.2f, e.BodyRadius);
                if (view.Overlaps(new Rect(v.x - r, v.y - r, r * 2f, r * 2f))) continue;
                float dist = DistanceOutside(view, v);
                var t = new Target { View = v, Order = FxLayers.Offscreen };
                if (e.IsBoss)
                {
                    t.Color = C("boss_color", Color.white);
                    t.Cells = J("boss_cells", 0.6f);
                    t.Blink = 2f;
                    t.Priority = 1f;
                }
                else if (e.IsElite)
                {
                    t.Color = C("elite_color", new Color32(0xFF, 0xD2, 0x4A, 0xFF));
                    t.Cells = J("elite_cells", 0.42f);
                    t.Priority = 2f + dist * 0.01f;
                }
                else
                {
                    t.Color = C("enemy_color", new Color32(0xE8, 0xEC, 0xF4, 0xFF));
                    float k = Mathf.Clamp01(1f - dist / Mathf.Max(0.1f, near));
                    t.Color.a = Mathf.Lerp(farA, 1f, k);
                    t.Cells = J("enemy_cells", 0.32f) * Mathf.Lerp(0.8f, 1.15f, k);
                    t.Priority = 10f + dist;
                }
                _targets.Add(t);
                added++;
            }
        }

        void CollectTelegraphs(SimWorld w, Vector2 center, Rect view)
        {
            Color danger = C("telegraph_color", new Color32(0xFF, 0x3B, 0x30, 0xFF));
            int max = Mathf.RoundToInt(J("max_telegraphs", 12f));
            int added = 0;
            for (int i = 0; i < w.Zones.Count && added < max; i++)
            {
                var z = w.Zones[i];
                if (z == null || z.Harmless || z.Cancelled || z.Detonated) continue;
                Vector2 closest;
                if (!ZoneOffscreen(z, center, view, out closest)) continue;
                float fill = z.Fill(w.Tick);
                _targets.Add(new Target
                {
                    View = closest, Color = danger, Cells = J("telegraph_cells", 0.5f) * (1f + 0.3f * fill),
                    Blink = Mathf.Lerp(2f, 8f, fill), Order = FxLayers.OffscreenTelegraph, Priority = -1f + (1f - fill) * 0.5f
                });
                added++;
            }
        }

        /// <summary>구역이 화면에 한 점도 걸리지 않으면 true와 화면 가운데에서 가장 가까운 구역 점(view 좌표).</summary>
        bool ZoneOffscreen(ZoneEntity z, Vector2 center, Rect view, out Vector2 closest)
        {
            Vector3 p3 = _vc.ToView(z.Pos);
            Vector2 a = new Vector2(p3.x, p3.y);
            Vector2 b = a;
            float half;
            bool line = z.Shape == ZoneShape.Band || z.Shape == ZoneShape.AimLine;
            if (line && z.Length > 0f)
            {
                Vector2 dir = SimConst.DirToView(z.Dir.sqrMagnitude > 1e-6f ? z.Dir.normalized : Vector2.right);
                b = a + dir * z.Length;
                half = Mathf.Max(0.1f, z.Width * 0.5f);
            }
            else half = Mathf.Max(0.1f, z.Radius);
            // 선분 위에서 화면 가운데에 가장 가까운 점, 그다음 반폭만큼 가운데 쪽으로
            Vector2 ab = b - a;
            float tt = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(center - a, ab) / ab.sqrMagnitude) : 0f;
            Vector2 q = a + ab * tt;
            Vector2 toC = center - q;
            float dist = toC.magnitude;
            closest = dist > half ? q + toC / dist * half : center;
            // 구역의 경계 상자가 화면과 겹치면 보이는 것으로 친다(보수적으로 — 화살이 덜 나오는 쪽보다 구역이 이미 보이는 쪽)
            Rect box = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - half, Mathf.Min(a.y, b.y) - half, Mathf.Max(a.x, b.x) + half, Mathf.Max(a.y, b.y) + half);
            if (!view.Overlaps(box)) return true;
            return !view.Contains(closest) && DistanceOutside(view, closest) > 0.05f && !SegmentNearRect(a, b, half, view);
        }

        static bool SegmentNearRect(Vector2 a, Vector2 b, float half, Rect r)
        {
            // 선분을 몇 점으로 나눠 그 점의 원이 화면에 걸리는지 본다(구역 모양이 화면 안에 들어오면 화살 없음)
            for (int i = 0; i <= 8; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / 8f);
                if (DistanceOutside(r, p) <= half) return true;
            }
            return false;
        }

        static float DistanceOutside(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public void Dispose()
        {
            if (_pool != null) for (int i = 0; i < _arrows.Count; i++) if (_arrows[i] != null) _pool.Return(_arrows[i]);
            if (_pool != null) for (int i = 0; i < _spawnArrows.Count; i++) if (_spawnArrows[i] != null) _pool.Return(_spawnArrows[i]);
            if (_pool != null) for (int i = 0; i < _leaves.Count; i++) if (_leaves[i] != null) _pool.Return(_leaves[i]);
            _arrows.Clear();
            _spawnArrows.Clear();
            _leaves.Clear();
            _incoming.Clear();
            _targets.Clear();
            _vc = null;
            _pool = null;
        }
    }
}
