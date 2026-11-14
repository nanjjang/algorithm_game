using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>
    /// 예고 구역(채움 층 + 테두리 층 두 번), 적 탄, 남는 구역, 장애물, 별똥(05 요약서 1절, 10 요약서 3.1·3.2·3.10절, 01 요약서 7.3절).
    /// 상태를 매 프레임 읽어 그린다: 이미 있는 구역·탄·남는 구역은 Build 뒤 첫 프레임에 집어 든다(TelegraphStarted를 못 봐도 된다).
    /// 구역 값은 볼 때마다 복사해 둔다 — 판정·취소 틱에는 목록에서 이미 빠져 있다(SimWorld.Compact).
    /// 적 예고·적 탄은 연출 감도(EffectsLevel)와 무관하게 줄이지 않는다. 설정 중 이 층이 적용하는 것: 예고 채움 진하기, 색각 보조.
    /// 메시는 코드로 만들어 풀의 MeshRenderer(SpriteMaterialKind.Shape)에 넣고, 정점 색이 곧 색이다(가산 혼합 없음).
    /// </summary>
    public sealed class ThreatLayer : IWorldViewLayer
    {
        const int OHazardSprite = 0, OHazardFill = 5, OZoneFill = 10, OStarRing = 20, OEdgeHazard = 5, OEdgeZone = 10, OShotStripe = 50, OBlocked = 60, OImpact = 10;

        ViewContext _vc;
        IArtLibrary _art;
        ISpritePool _pool;
        EnemyViewTuning _t;
        TelegraphPainter _painter;
        FlashLimiter _flash;
        Transform _root;

        readonly ShapeMeshBuilder _fill = new ShapeMeshBuilder(), _edge = new ShapeMeshBuilder(), _stripes = new ShapeMeshBuilder(), _star = new ShapeMeshBuilder();
        MeshSlot _fillSlot, _edgeSlot, _stripeSlot, _starSlot;

        readonly Dictionary<int, ZoneView> _zones = new Dictionary<int, ZoneView>();
        readonly Dictionary<int, ShotView> _shots = new Dictionary<int, ShotView>();
        readonly Dictionary<int, HazardView> _hazards = new Dictionary<int, HazardView>();
        readonly Dictionary<int, ObstacleView> _obstacles = new Dictionary<int, ObstacleView>();
        readonly List<Transient> _transients = new List<Transient>();
        readonly List<int> _drop = new List<int>();
        readonly List<ZoneView> _zoneOrder = new List<ZoneView>();
        readonly Dictionary<string, SpriteAnim> _fx = new Dictionary<string, SpriteAnim>();
        SpriteRenderer _starBody, _starFx;
        float _starClock;
        int _stamp;

        public int TrackedZones => _zones.Count;
        public int TrackedShots => _shots.Count;
        public int TrackedHazards => _hazards.Count;
        public int TrackedObstacles => _obstacles.Count;
        public EnemyViewTuning Tuning => _t;
        /// <summary>테스트·확인용: 이번 프레임에 쌓은 채움·테두리·적 탄 줄무늬 메시.</summary>
        public ShapeMeshBuilder FillMesh => _fill;
        public ShapeMeshBuilder EdgeMesh => _edge;
        public ShapeMeshBuilder ShotStripeMesh => _stripes;
        public ShapeMeshBuilder StarMesh => _star;
        public SpriteRenderer ShotRenderer(int id) => _shots.TryGetValue(id, out var s) ? s.Sr : null;

        sealed class ZoneView
        {
            public ZoneSnap Snap;
            public bool FlashDecided, FlashAsBorder;
            public bool Cancelled;
            public float FadeLeft;
            public int Stamp;
            public float ShakeLeft;
            public bool LockSeen;
        }

        sealed class ShotView
        {
            public SpriteRenderer Sr, Shadow;
            public string Key;
            public SpriteAnim Anim;
            public float Time;
            public int Stamp;
        }

        sealed class HazardView
        {
            public SpriteRenderer Sr;
            public string Key;
            public float Time;
            public int Stamp;
        }

        sealed class ObstacleView
        {
            public SpriteRenderer Sr;
            public string Kind;
            public float Time;
            public bool Breaking, Fading;
            public float EndTime;
            public int Stamp;
            public Vector2 Pos;
            public float Radius;
        }

        /// <summary>한 번 지나가는 그림(판정 터짐, 막힌 탄).</summary>
        sealed class Transient
        {
            public SpriteRenderer Sr;
            public SpriteAnim Anim;
            public float Time, Life;
            public bool Puff;
            public float PuffRadius;
        }

        // ------------------------------------------------------------ IWorldViewLayer

        public void Build(ViewContext vc, SimWorld w)
        {
            Dispose();
            _vc = vc;
            _art = vc != null && vc.Game != null ? vc.Game.Art : null;
            _pool = vc != null ? vc.Pool : null;
            _root = vc != null ? vc.WorldRoot : null;
            _t = EnemyViewTuning.Load(w != null ? w.Content : (vc != null && vc.Game != null ? vc.Game.Content : null));
            _painter = new TelegraphPainter(_t);
            _flash = new FlashLimiter(_t.FlashLimitPx2, _t.FlashWindowTicks);
            foreach (var b in new[] { _fill, _edge, _stripes, _star }) b.CircleSegments = _t.CircleSegments;
            _fillSlot = new MeshSlot(_pool, SortBands.TelegraphFill + OZoneFill, _root);
            // 예고 테두리는 적 탄 위(가독성: 적 예고 > 적 탄 > 내 것 — View/Effects/FxLayers)
            _edgeSlot = new MeshSlot(_pool, Effects.FxLayers.TelegraphEdge + OEdgeZone, _root);
            _stripeSlot = new MeshSlot(_pool, SortBands.EnemyShots + OShotStripe, _root);
            _starSlot = new MeshSlot(_pool, SortBands.TelegraphFill + OStarRing, _root);
            if (w != null) Sync(w, 1f, 0f); // 이벤트 없이 지금 상태에서 집어 든다
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_t == null) return;
            switch (e.Type)
            {
                case SimEventType.TelegraphDetonated:
                    if (_zones.TryGetValue(e.EntityId, out var z))
                    {
                        if (!z.Snap.Harmless) SpawnImpact(w, z.Snap);
                        _zones.Remove(e.EntityId);
                    }
                    break;
                case SimEventType.TelegraphCancelled:
                    if (_zones.TryGetValue(e.EntityId, out var cz) && !cz.Cancelled) { cz.Cancelled = true; cz.FadeLeft = _t.CancelFadeSeconds; }
                    break;
                case SimEventType.EnemyShotBlocked:
                    SpawnBlockedPuff(w, e.Pos);
                    break;
                case SimEventType.ObstacleDestroyed:
                    if (_obstacles.TryGetValue(e.EntityId, out var o) && !o.Breaking && !o.Fading)
                    {
                        if (e.A == 1) o.Breaking = true; else o.Fading = true;
                        o.Time = 0f;
                        o.EndTime = e.A == 1 ? BreakDuration(o.Kind) : _t.ObstacleExpireFadeSeconds;
                    }
                    break;
            }
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (w == null || _t == null) return;
            Sync(w, alpha, dt);
        }

        public void Dispose()
        {
            foreach (var kv in _shots) ReleaseShot(kv.Value);
            _shots.Clear();
            foreach (var kv in _hazards) PoolUtil.Release(_pool, ref kv.Value.Sr);
            _hazards.Clear();
            foreach (var kv in _obstacles) PoolUtil.Release(_pool, ref kv.Value.Sr);
            _obstacles.Clear();
            for (int i = 0; i < _transients.Count; i++) PoolUtil.Release(_pool, ref _transients[i].Sr);
            _transients.Clear();
            _zones.Clear();
            PoolUtil.Release(_pool, ref _starBody);
            PoolUtil.Release(_pool, ref _starFx);
            if (_fillSlot != null) _fillSlot.Release();
            if (_edgeSlot != null) _edgeSlot.Release();
            if (_stripeSlot != null) _stripeSlot.Release();
            if (_starSlot != null) _starSlot.Release();
            _fillSlot = _edgeSlot = _stripeSlot = _starSlot = null;
            _flash?.Reset();
            _fx.Clear();
        }

        // ------------------------------------------------------------ 매 프레임

        void Sync(SimWorld w, float alpha, float dt)
        {
            int tick = Mathf.Max(0, w.Tick - 1);
            float now = tick + alpha;
            _stamp++;
            _painter.ArenaHeight = w.ArenaHeight;
            _fill.Clear();
            _edge.Clear();
            _stripes.Clear();
            _star.Clear();

            var opt = _vc != null ? _vc.Options : null;
            int fillPct = opt != null ? opt.TelegraphFillPct : _t.FillPctMin;
            bool cb = opt != null && opt.ColorblindAssist;
            float fillAlpha = TelegraphGeometry.FillAlpha(fillPct, _t.FillPctMin, _t.FillPctMax);

            // 남는 구역이 먼저(예고가 그 위에 그려진다)
            SyncHazards(w, tick, dt);
            SyncZones(w, tick, now, dt, fillAlpha, cb);
            SyncShots(w, tick, alpha, dt, cb);
            SyncObstacles(w, tick, dt);
            SyncStarfall(w, tick, dt);
            UpdateTransients(dt);

            _fillSlot?.Upload(_fill);
            _edgeSlot?.Upload(_edge);
            _stripeSlot?.Upload(_stripes);
            _starSlot?.Upload(_star);
        }

        // ------------------------------------------------------------ 예고 구역

        void SyncZones(SimWorld w, int tick, float now, float dt, float fillAlpha, bool cb)
        {
            for (int i = 0; i < w.Zones.Count; i++)
            {
                var z = w.Zones[i];
                if (z == null || z.Cancelled || z.Detonated) continue;
                if (!_zones.TryGetValue(z.Id, out var v)) { v = new ZoneView(); _zones[z.Id] = v; }
                v.Snap = ZoneSnap.Of(z);
                v.Stamp = _stamp;
            }
            _drop.Clear();
            _zoneOrder.Clear();
            foreach (var kv in _zones)
            {
                var v = kv.Value;
                if (v.Cancelled)
                {
                    v.FadeLeft -= dt;
                    if (v.FadeLeft <= 0f) { _drop.Add(kv.Key); continue; }
                }
                else if (v.Stamp != _stamp) { _drop.Add(kv.Key); continue; } // 이벤트 없이 사라짐
                _zoneOrder.Add(v);
            }
            for (int i = 0; i < _drop.Count; i++) _zones.Remove(_drop[i]);
            // 먼저 터질 것이 위에 그려지게: 판정 틱 내림차순 → 같은 틱이면 id 오름차순
            _zoneOrder.Sort((a, b) => a.Snap.DetonateTick != b.Snap.DetonateTick ? b.Snap.DetonateTick.CompareTo(a.Snap.DetonateTick) : a.Snap.Id.CompareTo(b.Snap.Id));

            for (int i = 0; i < _zoneOrder.Count; i++)
            {
                var v = _zoneOrder[i];
                var s = v.Snap;
                var look = ZoneLook.Default(TelegraphGeometry.Fill(s.StartTick, s.DetonateTick, now), fillAlpha);
                look.Colorblind = cb && !s.Harmless;
                if (v.Cancelled) look.AlphaMul = _t.CancelFadeSeconds > 0f ? Mathf.Clamp01(v.FadeLeft / _t.CancelFadeSeconds) : 0f;
                // 번쩍(마지막 2프레임) + 번쩍 한도(최근 1초 넓이 합 > 화면 25% → 흰 선 8px 테두리)
                if (!v.Cancelled && !s.Harmless && TelegraphGeometry.IsFlashTick(tick, s.DetonateTick, _t.FlashTicksBefore))
                {
                    if (!v.FlashDecided)
                    {
                        v.FlashDecided = true;
                        float area = TelegraphGeometry.AreaPx2(TelegraphGeometry.AreaCells(s.Shape, s.Radius, s.InnerRadius, s.Width, s.Length, s.FanDegrees));
                        v.FlashAsBorder = !_flash.TryFlash(area, tick);
                    }
                    look.Flash = true;
                    look.FlashAsBorder = v.FlashAsBorder;
                }
                // 고정 순간 떨림(lock = last_n_ticks: 시작 뒤에 고정되는 것만)
                if (s.DirLocked && s.LockTick > s.StartTick && !v.LockSeen && tick >= s.LockTick)
                {
                    v.LockSeen = true;
                    v.ShakeLeft = _t.LockShakeSeconds;
                }
                if (v.ShakeLeft > 0f)
                {
                    v.ShakeLeft -= dt;
                    float px = _t.LockShakePx * EnemyViewTuning.Px;
                    look.OffsetCells = new Vector2(Random.Range(-px, px), Random.Range(-px, px));
                }
                _painter.PaintZoneFill(_fill, s, look);
                _painter.PaintZoneEdge(_edge, s, look);
            }
        }

        void SpawnImpact(SimWorld w, in ZoneSnap s)
        {
            if (!_t.ImpactSprites.TryGetValue(s.Shape.ToString(), out var key)) return;
            var anim = Fx("enemy", key);
            if (anim == null || anim.Frames == null || anim.Frames.Length == 0) return;
            var tr = new Transient { Anim = anim, Life = anim.Duration > 0f ? anim.Duration : 0.25f };
            PoolUtil.Ensure(_pool, ref tr.Sr, anim.Frames[0], SortBands.Bursts + OImpact, _root);
            if (tr.Sr == null) return;
            Vector2 cells = _t.ImpactSpriteCells.TryGetValue(key, out var c) ? c : new Vector2(2f, 2f);
            var d = new Vector2(s.Dir.x, -s.Dir.y);
            if (d.sqrMagnitude < 1e-6f) d = Vector2.right; else d.Normalize();
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Vector3 p = SimConst.ToView(s.Pos, w.ArenaHeight);
            var t = tr.Sr.transform;
            switch (s.Shape)
            {
                case ZoneShape.Band:
                case ZoneShape.AimLine:
                    p += (Vector3)(d * (s.Length * 0.5f));
                    t.localScale = new Vector3(s.Length / Mathf.Max(0.01f, cells.x), Mathf.Max(s.Width, 0.25f) / Mathf.Max(0.01f, cells.y), 1f);
                    t.localRotation = Quaternion.Euler(0f, 0f, ang);
                    break;
                case ZoneShape.Fan:
                    t.localScale = Vector3.one * (s.Radius * 2f / Mathf.Max(0.01f, cells.x));
                    t.localRotation = Quaternion.Euler(0f, 0f, ang);
                    break;
                default:
                    t.localScale = Vector3.one * (s.Radius * 2f / Mathf.Max(0.01f, cells.x));
                    break;
            }
            t.localPosition = new Vector3(p.x, p.y, 0f);
            _transients.Add(tr);
        }

        // ------------------------------------------------------------ 적 탄

        void SyncShots(SimWorld w, int tick, float alpha, float dt, bool cb)
        {
            for (int i = 0; i < w.EnemyShots.Count; i++)
            {
                var s = w.EnemyShots[i];
                if (s == null || s.Dead) continue;
                if (!_shots.TryGetValue(s.Id, out var v)) { v = new ShotView(); _shots[s.Id] = v; }
                v.Stamp = _stamp;
                string key = !string.IsNullOrEmpty(s.SpriteKey) ? s.SpriteKey : (s.Radius >= _t.ShotLargeMinRadius ? _t.ShotLargeKey : _t.ShotSmallKey);
                if (key != v.Key) { v.Key = key; v.Anim = Fx("enemy", key); v.Time = Mathf.Max(0f, (tick - s.SpawnTick) * SimConst.TickSeconds); }
                else v.Time += dt;
                var frame = PoolUtil.LoopFrame(v.Anim, v.Time);
                // 적 탄은 항상 내 탄·폭발보다 위(SortBands.EnemyShots)
                PoolUtil.Ensure(_pool, ref v.Sr, frame, SortBands.EnemyShots, _root);
                if (v.Sr == null) continue;
                Vector2 sim = ViewMath.Lerp(s.PrevPos, s.Pos, alpha);
                Vector3 vp = SimConst.ToView(sim, w.ArenaHeight);
                float lift = 0f;
                if (s.Lob && s.LandTick > s.SpawnTick)
                {
                    float p = Mathf.Clamp01((tick + alpha - s.SpawnTick) / (s.LandTick - s.SpawnTick));
                    lift = 4f * _t.LobPeakHeight * p * (1f - p); // 포물선 높이 호
                    var sh = PoolUtil.Ensure(_pool, ref v.Shadow, PoolUtil.LoopFrame(Fx("enemy", _t.LobShadowKey), 0f), SortBands.EnemyShots - 1, _root);
                    if (sh != null) sh.transform.localPosition = new Vector3(vp.x, vp.y, 0f);
                }
                else PoolUtil.Release(_pool, ref v.Shadow);
                var tr = v.Sr.transform;
                tr.localPosition = new Vector3(vp.x, vp.y + lift, 0f);
                // 둥근 기본 탄은 판정 반지름에 맞춰 키우고, 모양 있는 탄(깃털·솔침 …)은 날아가는 쪽으로 돌린다
                bool round = _t.RoundShotKeys.Contains(key);
                float baseR = key == _t.ShotLargeKey ? _t.ShotLargeRadius : _t.ShotSmallRadius;
                float scale = round && s.Radius > baseR * 1.25f ? s.Radius / baseR : 1f;
                tr.localScale = new Vector3(scale, scale, 1f);
                if (!round && s.Vel.sqrMagnitude > 1e-6f)
                {
                    var dv = SimConst.DirToView(s.Vel);
                    tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg);
                }
                else tr.localRotation = Quaternion.identity;
                // 막힌 탄: 회색으로 옅게(스침 아님)
                v.Sr.color = s.Blocked ? new Color(0.75f, 0.75f, 0.75f, _t.BlockedAlpha) : Color.white;
                // 색각 보조: 적 탄에 주황 줄무늬(탄 몸은 그대로 두고 더한다)
                if (cb && !s.Blocked && frame != null)
                {
                    float r = PoolUtil.WidthCells(frame) * 0.5f * scale;
                    _painter.PaintStripedDisc(_stripes, new Vector2(vp.x, vp.y + lift), r, _t.ShotStripePitchPx * EnemyViewTuning.Px,
                        _t.ShotStripePx * EnemyViewTuning.Px, _t.ColorblindAngle, _t.ColorblindShotStripe);
                }
            }
            _drop.Clear();
            foreach (var kv in _shots) if (kv.Value.Stamp != _stamp) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) { ReleaseShot(_shots[_drop[i]]); _shots.Remove(_drop[i]); }
        }

        void ReleaseShot(ShotView v)
        {
            PoolUtil.Release(_pool, ref v.Sr);
            PoolUtil.Release(_pool, ref v.Shadow);
        }

        void SpawnBlockedPuff(SimWorld w, Vector2 simPos)
        {
            if (_art == null) return;
            var tr = new Transient { Life = Mathf.Max(0.01f, _t.BlockedSeconds), Puff = true, PuffRadius = _t.BlockedRadiusPx * EnemyViewTuning.Px };
            PoolUtil.Ensure(_pool, ref tr.Sr, _art.Circle(16, true, 2), SortBands.EnemyShots + OBlocked, _root);
            if (tr.Sr == null) return;
            var p = SimConst.ToView(simPos, w != null ? w.ArenaHeight : 14f);
            tr.Sr.transform.localPosition = new Vector3(p.x, p.y, 0f);
            tr.Sr.color = _t.BlockedPuff;
            _transients.Add(tr);
        }

        void UpdateTransients(float dt)
        {
            for (int i = _transients.Count - 1; i >= 0; i--)
            {
                var tr = _transients[i];
                tr.Time += dt;
                if (tr.Time >= tr.Life || tr.Sr == null) { PoolUtil.Release(_pool, ref tr.Sr); _transients.RemoveAt(i); continue; }
                float k = tr.Time / tr.Life;
                if (tr.Puff)
                {
                    float d = Mathf.Lerp(tr.PuffRadius, tr.PuffRadius * 2f, k) * 2f / 0.5f; // 원 그림 지름 0.5칸(16px 반지름)
                    tr.Sr.transform.localScale = new Vector3(d, d, 1f);
                    var c = (Color)_t.BlockedPuff;
                    c.a = 1f - k;
                    tr.Sr.color = c;
                }
                else if (tr.Anim != null) tr.Sr.sprite = tr.Anim.At(tr.Time);
            }
        }

        // ------------------------------------------------------------ 남는 구역

        void SyncHazards(SimWorld w, int tick, float dt)
        {
            for (int i = 0; i < w.Hazards.Count; i++)
            {
                var h = w.Hazards[i];
                if (h == null || h.Dead) continue;
                if (!_hazards.TryGetValue(h.Id, out var v)) { v = new HazardView(); _hazards[h.Id] = v; }
                v.Stamp = _stamp;
                v.Time += dt;
                bool frost = h.SlowOnly || h.Kind == HazardKind.FrostField;
                bool visible = TelegraphGeometry.HazardVisible(tick, h.UntilTick, _t.HazardBlinkLeadSeconds, _t.HazardBlinkPerSecond);
                float am = visible ? 1f : _t.HazardBlinkDimAlpha;
                var snap = ZoneSnap.Of(h);
                // 바닥 그림(가시밭·불씨 띠·포자 안개·서리밭)
                string key = _t.HazardSprites.TryGetValue(h.Kind.ToString(), out var k) ? k : null;
                if (key != null && (h.Shape == ZoneShape.Circle || h.Shape == ZoneShape.Band || h.Shape == ZoneShape.AimLine))
                {
                    v.Key = key;
                    var anim = Fx("enemy", key);
                    var frame = PoolUtil.LoopFrame(anim, v.Time);
                    PoolUtil.Ensure(_pool, ref v.Sr, frame, SortBands.TelegraphFill + OHazardSprite, _root);
                    if (v.Sr != null && frame != null) PlaceHazardSprite(w, v.Sr, h, frame, am);
                }
                else PoolUtil.Release(_pool, ref v.Sr);
                _painter.PaintHazardFill(_fill, snap, frost, am);
                _painter.PaintHazardEdge(_edge, snap, frost, am);
            }
            _drop.Clear();
            foreach (var kv in _hazards) if (kv.Value.Stamp != _stamp) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) { var v = _hazards[_drop[i]]; PoolUtil.Release(_pool, ref v.Sr); _hazards.Remove(_drop[i]); }
        }

        void PlaceHazardSprite(SimWorld w, SpriteRenderer sr, HazardEntity h, Sprite frame, float am)
        {
            var tr = sr.transform;
            float wCells = PoolUtil.WidthCells(frame);
            Vector3 p = SimConst.ToView(h.Pos, w.ArenaHeight);
            sr.color = new Color(1f, 1f, 1f, am);
            if (h.Shape == ZoneShape.Circle)
            {
                sr.drawMode = SpriteDrawMode.Simple;
                float s = h.Radius * 2f / Mathf.Max(0.01f, wCells);
                tr.localScale = new Vector3(s, s, 1f);
                tr.localRotation = Quaternion.identity;
                tr.localPosition = new Vector3(p.x, p.y, 0f);
                return;
            }
            var d = SimConst.DirToView(h.Dir);
            if (d.sqrMagnitude < 1e-6f) d = Vector2.right; else d.Normalize();
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(Mathf.Max(0.01f, h.Length), Mathf.Max(0.01f, h.Width));
            tr.localScale = Vector3.one;
            tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            tr.localPosition = new Vector3(p.x + d.x * h.Length * 0.5f, p.y + d.y * h.Length * 0.5f, 0f);
        }

        // ------------------------------------------------------------ 장애물

        void SyncObstacles(SimWorld w, int tick, float dt)
        {
            for (int i = 0; i < w.Obstacles.Count; i++)
            {
                var o = w.Obstacles[i];
                if (o == null || o.Dead) continue;
                if (!_obstacles.TryGetValue(o.Id, out var v)) { v = new ObstacleView { Kind = o.Kind }; _obstacles[o.Id] = v; }
                if (v.Breaking || v.Fading) continue;
                v.Stamp = _stamp;
                v.Pos = o.Pos;
                v.Radius = o.Radius;
                v.Time += dt;
                DrawObstacle(w, v, 1f);
            }
            _drop.Clear();
            foreach (var kv in _obstacles)
            {
                var v = kv.Value;
                if (v.Breaking || v.Fading)
                {
                    v.Time += dt;
                    if (v.Time >= v.EndTime) { _drop.Add(kv.Key); continue; }
                    DrawObstacle(w, v, v.Fading ? 1f - v.Time / Mathf.Max(0.01f, v.EndTime) : 1f);
                }
                else if (v.Stamp != _stamp) _drop.Add(kv.Key);
            }
            for (int i = 0; i < _drop.Count; i++) { var v = _obstacles[_drop[i]]; PoolUtil.Release(_pool, ref v.Sr); _obstacles.Remove(_drop[i]); }
        }

        void DrawObstacle(SimWorld w, ObstacleView v, float alphaMul)
        {
            Sprite frame = null;
            if (v.Kind != null && _t.Obstacles.TryGetValue(v.Kind, out var f))
            {
                var anim = Fx("enemy", f.Effect);
                if (v.Breaking) frame = PoolUtil.SeqFrame(anim, f.Break, v.Time, false);
                else if (v.Time < PoolUtil.SeqDuration(anim, f.Spawn)) frame = PoolUtil.SeqFrame(anim, f.Spawn, v.Time, false);
                else frame = PoolUtil.SeqFrame(anim, f.Idle, v.Time, true);
            }
            bool fallback = frame == null;
            if (fallback && _art != null) frame = _art.Circle(32);
            int order = SortBands.Bodies + SortBands.YOrder(v.Pos.y);
            PoolUtil.Ensure(_pool, ref v.Sr, frame, order, _root);
            if (v.Sr == null) return;
            Vector3 p = SimConst.ToView(v.Pos, w.ArenaHeight);
            var tr = v.Sr.transform;
            if (fallback)
            {
                float d = Mathf.Max(0.1f, v.Radius * 2f);
                tr.localScale = new Vector3(d, d, 1f);
                tr.localPosition = new Vector3(p.x, p.y, 0f);
                v.Sr.color = new Color(0.55f, 0.56f, 0.6f, alphaMul); // 회색 대체 도형(위험색·금색 아님)
                return;
            }
            tr.localScale = Vector3.one;
            // 가운데 기준 그림: 아래쪽 정사각 칸의 가운데가 판정 위치에 오게 올린다(얼음 기둥 64×96)
            float lift = Mathf.Max(0f, (frame.rect.height - frame.rect.width) * 0.5f) / Mathf.Max(1f, frame.pixelsPerUnit);
            tr.localPosition = new Vector3(p.x, p.y + lift, 0f);
            v.Sr.color = new Color(1f, 1f, 1f, alphaMul);
        }

        float BreakDuration(string kind)
        {
            if (kind != null && _t.Obstacles.TryGetValue(kind, out var f)) return PoolUtil.SeqDuration(Fx("enemy", f.Effect), f.Break);
            return _t.ObstacleExpireFadeSeconds;
        }

        // ------------------------------------------------------------ 별똥

        void SyncStarfall(SimWorld w, int tick, float dt)
        {
            var sf = w.Starfall;
            var def = w.Content != null ? w.Content.Starfall : null;
            bool tele = sf != null && sf.Phase == StarfallPhase.Telegraph;
            bool landed = sf != null && sf.Phase == StarfallPhase.Landed;
            if (!tele && !landed)
            {
                PoolUtil.Release(_pool, ref _starBody);
                PoolUtil.Release(_pool, ref _starFx);
                _starClock = 0f;
                return;
            }
            _starClock += dt;
            Vector3 p = SimConst.ToView(sf.Pos, w.ArenaHeight);
            float radius = def != null && def.PickRadius > 0f ? def.PickRadius : 0.5f;
            float elapsed = (tick - sf.PhaseStartTick) * SimConst.TickSeconds;
            if (tele)
            {
                // 흰 금빛 점선 고리(위험색 아님): 차오르지 않고 점선이 돈다
                float rot = _starClock * _t.StarRotateDegPerSecond;
                _star.AddDashedArc(new Vector2(p.x, p.y), radius - _t.StarRingPx * EnemyViewTuning.Px, radius, rot, 360f, _t.StarDashDegrees, _t.StarGapDegrees, _t.StarfallRing);
                float dur = def != null && def.TelegraphSeconds > 0f ? def.TelegraphSeconds : 1.5f;
                float k = Mathf.Clamp01(elapsed / dur);
                var fall = _art != null ? _art.World(_t.StarFallAsset) : null;
                PoolUtil.Ensure(_pool, ref _starBody, PoolUtil.LoopFrame(fall, _starClock), SortBands.Bodies + SortBands.YOrder(sf.Pos.y), _root);
                if (_starBody != null)
                {
                    _starBody.transform.localPosition = new Vector3(p.x, p.y + (1f - k) * _t.StarFallHeight, 0f);
                    _starBody.color = Color.white;
                }
                PoolUtil.Release(_pool, ref _starFx);
                return;
            }
            // 떨어짐: 남아 있는 동안 반짝, 마지막 blink초는 깜빡임
            float life = def != null && def.LandedSeconds > 0f ? def.LandedSeconds : 8f;
            float blink = def != null ? def.BlinkSeconds : 2f;
            bool on = elapsed < life - blink || TelegraphGeometry.BlinkOn(elapsed - (life - blink), _t.StarBlinkPerSecond);
            var ground = Fx("status", _t.StarGroundEffect);
            PoolUtil.Ensure(_pool, ref _starBody, PoolUtil.LoopFrame(ground, _starClock), SortBands.Bodies + SortBands.YOrder(sf.Pos.y), _root);
            if (_starBody != null)
            {
                _starBody.transform.localPosition = new Vector3(p.x, p.y, 0f);
                _starBody.enabled = on;
            }
            var sparkle = _art != null ? _art.World(_t.StarSparkleAsset) : null;
            PoolUtil.Ensure(_pool, ref _starFx, PoolUtil.LoopFrame(sparkle, _starClock), SortBands.Bodies + SortBands.YOrder(sf.Pos.y) + 1, _root);
            if (_starFx != null)
            {
                _starFx.transform.localPosition = new Vector3(p.x, p.y + 0.25f, 0f);
                _starFx.enabled = on;
            }
        }

        SpriteAnim Fx(string group, string key)
        {
            if (_art == null || key == null) return null;
            string k = group + "|" + key;
            if (_fx.TryGetValue(k, out var a)) return a;
            a = _art.Effect(group, key);
            _fx[k] = a;
            return a;
        }
    }
}
