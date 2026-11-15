using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>
    /// 적·엘리트·졸개·보스 몸과 몸에 붙는 표시(05 요약서 1·4·7·8.3절, 09 요약서 4·5.4·5.7절, 03 요약서 10.3절, 10 요약서 3.7~3.9절).
    /// 시뮬레이션 상태를 읽기만 한다. 매 프레임 w.Enemies와 대조해 추적한다 — EnemySpawned를 못 본 개체(보조 판에서 돌아와 Build가 다시 불린 때)도
    /// 첫 프레임에 집어 든다. 이벤트는 한 번 일어나는 연출(적중 번쩍, 엘리트 기둥 빛, 격노 림, 보스 사망 느린 연출, 등장 예고)에만 쓴다.
    /// 몸 하나 = SortingGroup 하나(sortingOrder = Bodies + YOrder) — 그 안에서 몸·장식·상태 표시의 순서를 정한다. 그림자·둔덕은 Shadows 띠.
    /// 처치 조각·연기는 Effects 층(G16) 몫이라 여기서는 몸만 지운다.
    /// </summary>
    public sealed class EnemyLayer : IWorldViewLayer
    {
        // 몸 묶음 안의 순서
        const int OBush = 1, OBody = 10, OOverlay = 11, OHorn = 12, OSlow = 14, OCorrosion = 15, OCorrosionMark = 16, OFrozen = 18,
            OShatter = 19, OStun = 20, OMarks = 13, OBuff = 9, OColumn = 22, OHpBack = 24, OHpFill = 25, OSmoke = 26;

        ViewContext _vc;
        IArtLibrary _art;
        ISpritePool _pool;
        EnemyViewTuning _t;
        Transform _root;
        readonly Dictionary<int, EnemyView> _views = new Dictionary<int, EnemyView>();
        readonly List<int> _drop = new List<int>();
        readonly List<SpawnNotice> _notices = new List<SpawnNotice>();
        readonly Dictionary<string, SpriteAnim> _fx = new Dictionary<string, SpriteAnim>();
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        int _stamp;
        int _rimWave = int.MinValue, _rimPercent;
        float _clock;
        // 타격감 값(Juice.json enemy, 표시 전용 솔로 임시). 조각이 없으면 0 = 연출 없음.
        float _squash, _critSquash, _squashSec, _knockPx, _knockSec, _bossJuiceMul, _appearSec, _appearOver;

        public int TrackedCount => _views.Count;
        public EnemyViewTuning Tuning => _t;
        /// <summary>테스트·다른 층 확인용: 그 적의 몸 렌더러(없으면 null).</summary>
        public SpriteRenderer BodyOf(int enemyId) => _views.TryGetValue(enemyId, out var v) ? v.Body : null;
        public bool IsDying(int enemyId) => _views.TryGetValue(enemyId, out var v) && v.Dying;

        sealed class SpawnNotice
        {
            public Vector2 Pos;
            public string Species;
            public int StartTick, EndTick;
            public float Time;
            public SpriteRenderer[] Parts = new SpriteRenderer[3];
            public SpriteRenderer Crack;
        }

        sealed class Smoke
        {
            public SpriteRenderer Sr;
            public float Age, Life;
            public Vector2 Local;
        }

        sealed class EnemyView
        {
            public int Id;
            public string DefId;
            public bool IsBoss, IsElite, IsDummy;
            public GameObject Go;
            public SortingGroup Group;
            public SpriteRenderer Body, Shadow, Mound, Overlay, Horn, HpBack, HpFill, Slow, CorrosionMark, Frozen, Shatter, Stun, Buff, Column;
            public readonly SpriteRenderer[] Bush = new SpriteRenderer[3];
            public readonly List<SpriteRenderer> Marks = new List<SpriteRenderer>();
            public MeshSlot CorrosionRing;
            public readonly ShapeMeshBuilder CorrosionMesh = new ShapeMeshBuilder();
            // 클립
            public string Clip;
            public int ClipStartTick = int.MinValue;
            public SpriteAnim Anim;
            public float ClipTime;
            public bool WasFrozen;
            public float ShatterTime = -1f;
            // 한 번 연출
            public float HitFlashLeft;
            public Vector2 HitPushDir;
            // 타격감(Juice.json enemy): 찌그러짐·밀림·튀어나옴(그림만)
            public float SquashLeft, SquashAmp, KnockLeft, AppearLeft;
            public bool HadBody;
            public float ColumnLeft;
            public bool ColumnShown;
            public float RageRimElapsed = -1f;
            public float DummyHitLeft;
            // 보스
            public bool Dying;
            public float DyingTime;
            public Vector2 LastSim;
            public bool LastFlip;
            public readonly List<Smoke> Smokes = new List<Smoke>();
            public float SmokeAcc;
            public readonly List<SpriteRenderer> Segs = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> SegShadows = new List<SpriteRenderer>();
            public readonly List<Vector2> SegPrev = new List<Vector2>(), SegCur = new List<Vector2>();
            public int SegTick = int.MinValue;
            public float SegClock;
            public int Stamp;
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
            ReadJuice(w != null && w.Content != null ? w.Content : (vc != null && vc.Game != null ? vc.Game.Content : null));
            _rimWave = int.MinValue;
            // 이벤트 없이 지금 상태만 보고 집어 든다(00 문서 7절 Build 약속)
            if (w != null) Sync(w, 1f, 0f);
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_t == null) return;
            switch (e.Type)
            {
                case SimEventType.EnemyDamaged:
                {
                    if (!_views.TryGetValue(e.EntityId, out var v)) break;
                    v.HitFlashLeft = _t.HitFlashSeconds;
                    var en = w != null ? w.FindEnemy(e.EntityId) : null;
                    Vector2 away = en != null ? en.Pos - e.Pos : Vector2.zero;
                    v.HitPushDir = away.sqrMagnitude > 1e-6f ? SimConst.DirToView(away.normalized) : Vector2.zero;
                    if (JuiceOn)
                    {
                        float m = v.IsBoss ? _bossJuiceMul : 1f;
                        v.SquashAmp = Mathf.Max(v.SquashLeft > 0f ? v.SquashAmp : 0f, ((e.B & DamageFlags.Crit) != 0 ? _critSquash : _squash) * m);
                        v.SquashLeft = _squashSec;
                        v.KnockLeft = _knockSec;
                    }
                    if (v.IsDummy) v.DummyHitLeft = _t.DummyHitSeconds;
                    break;
                }
                case SimEventType.EnemyKilled:
                {
                    if (!_views.TryGetValue(e.EntityId, out var v)) break;
                    if (v.IsBoss || e.B == 2) BeginDying(v);
                    else RemoveView(e.EntityId);
                    break;
                }
                case SimEventType.BossKilled:
                    if (_views.TryGetValue(e.EntityId, out var bv)) BeginDying(bv);
                    break;
                case SimEventType.EnemySpawned:
                    if (e.A == 1)
                    {
                        var en = w != null ? w.FindEnemy(e.EntityId) : null;
                        var v = en != null ? GetOrCreate(en) : null;
                        if (v != null && !v.ColumnShown) { v.ColumnLeft = _t.EliteColumnSeconds; v.ColumnShown = true; }
                    }
                    break;
                case SimEventType.BossRageStack:
                    if (_views.TryGetValue(e.EntityId, out var rv)) rv.RageRimElapsed = 0f;
                    break;
                case SimEventType.EnemySpawnNotice:
                {
                    int end = e.A > e.Tick ? e.A : e.Tick + (e.A > 0 ? e.A : _t.SpawnDefaultTicks);
                    _notices.Add(new SpawnNotice { Pos = e.Pos, Species = e.Id, StartTick = e.Tick, EndTick = end });
                    break;
                }
            }
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (w == null || _t == null) return;
            Sync(w, alpha, dt);
        }

        public void Dispose()
        {
            foreach (var kv in _views) ReleaseView(kv.Value);
            _views.Clear();
            for (int i = 0; i < _notices.Count; i++) ReleaseNotice(_notices[i]);
            _notices.Clear();
            _fx.Clear();
        }

        // ------------------------------------------------------------ 추적

        void Sync(SimWorld w, float alpha, float dt)
        {
            _clock += dt;
            int tick = Mathf.Max(0, w.Tick - 1);
            int rim = RimPercent(w);
            _stamp++;
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (e == null || e.Dead || e.DuelProxyOf >= 0) continue; // 결투 과녁은 그리지 않는다
                var v = GetOrCreate(e);
                if (v == null) continue;
                v.Stamp = _stamp;
                if (v.Dying) continue; // 같은 id가 남아 있는 드문 경우: 사망 연출을 이어 간다
                UpdateView(w, v, e, tick, alpha, dt, rim);
            }
            _drop.Clear();
            foreach (var kv in _views)
            {
                var v = kv.Value;
                if (v.Dying)
                {
                    if (!UpdateDying(w, v, dt)) _drop.Add(kv.Key);
                }
                else if (v.Stamp != _stamp) _drop.Add(kv.Key); // 상태 목록에서 사라졌다(사망·제거) → 몸만 지운다
            }
            for (int i = 0; i < _drop.Count; i++) RemoveView(_drop[i]);
            UpdateNotices(w, tick, dt);
        }

        EnemyView GetOrCreate(EnemyState e)
        {
            if (_views.TryGetValue(e.Id, out var v)) return v;
            v = new EnemyView
            {
                Id = e.Id, DefId = e.DefId, IsBoss = e.IsBoss, IsElite = e.IsElite,
                IsDummy = e.DefId == Ids.Enemy.Dummy
            };
            v.Go = new GameObject("Enemy " + e.Id);
            if (_root != null) v.Go.transform.SetParent(_root, false);
            v.Group = v.Go.AddComponent<SortingGroup>();
            v.Group.sortingOrder = SortBands.Bodies;
            _views[e.Id] = v;
            return v;
        }

        void RemoveView(int id)
        {
            if (!_views.TryGetValue(id, out var v)) return;
            ReleaseView(v);
            _views.Remove(id);
        }

        void ReleaseView(EnemyView v)
        {
            // 풀 렌더러를 먼저 돌려준다(묶음 GameObject를 부수면 자식이 같이 부서진다)
            PoolUtil.Release(_pool, ref v.Body); PoolUtil.Release(_pool, ref v.Shadow); PoolUtil.Release(_pool, ref v.Mound);
            PoolUtil.Release(_pool, ref v.Overlay); PoolUtil.Release(_pool, ref v.Horn); PoolUtil.Release(_pool, ref v.HpBack);
            PoolUtil.Release(_pool, ref v.HpFill); PoolUtil.Release(_pool, ref v.Slow); PoolUtil.Release(_pool, ref v.CorrosionMark);
            PoolUtil.Release(_pool, ref v.Frozen); PoolUtil.Release(_pool, ref v.Shatter); PoolUtil.Release(_pool, ref v.Stun);
            PoolUtil.Release(_pool, ref v.Buff); PoolUtil.Release(_pool, ref v.Column);
            for (int i = 0; i < v.Bush.Length; i++) PoolUtil.Release(_pool, ref v.Bush[i]);
            for (int i = 0; i < v.Marks.Count; i++) { var m = v.Marks[i]; PoolUtil.Release(_pool, ref m); }
            v.Marks.Clear();
            for (int i = 0; i < v.Smokes.Count; i++) PoolUtil.Release(_pool, ref v.Smokes[i].Sr);
            v.Smokes.Clear();
            ReleaseSegments(v, 0);
            if (v.CorrosionRing != null) { v.CorrosionRing.Release(); v.CorrosionRing = null; }
            if (v.Go != null) MeshSlot.DestroyObj(v.Go);
            v.Go = null;
        }

        void ReleaseSegments(EnemyView v, int keep)
        {
            for (int i = v.Segs.Count - 1; i >= keep; i--)
            {
                var s = v.Segs[i]; PoolUtil.Release(_pool, ref s); v.Segs.RemoveAt(i);
                var sh = v.SegShadows[i]; PoolUtil.Release(_pool, ref sh); v.SegShadows.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------ 한 마리

        void UpdateView(SimWorld w, EnemyView v, EnemyState e, int tick, float alpha, float dt, int rim)
        {
            var t = _t;
            Vector2 sim = ViewMath.Lerp(e.PrevPos, e.Pos, alpha);
            v.LastSim = sim;
            Vector3 vp = SimConst.ToView(sim, w.ArenaHeight);
            v.Group.sortingOrder = SortBands.Bodies + SortBands.YOrder(sim.y);
            var tr = v.Go.transform;

            bool frozen = tick < e.FrozenUntilTick;
            bool stunned = tick < e.StunUntilTick;
            bool stagger = tick < e.StaggerUntilTick;
            bool spawning = e.Ai == EnemyAiState.Spawning && !e.IsBoss && !v.IsDummy;

            // 흔들림·밀림(그림만)
            Vector2 off = Vector2.zero;
            if (v.HitFlashLeft > 0f) { off += v.HitPushDir * (t.HitPushPx * EnemyViewTuning.Px); v.HitFlashLeft -= dt; }
            if (v.KnockLeft > 0f && _knockSec > 0f)
            {
                // 맞은 쪽 반대로 밀렸다가 돌아온다(그림만, 판정 위치는 그대로)
                float kk = v.KnockLeft / _knockSec;
                off += v.HitPushDir * (_knockPx * (v.IsBoss ? _bossJuiceMul : 1f) * EnemyViewTuning.Px * kk * kk);
                v.KnockLeft -= dt;
            }
            if (stagger) off.x += ((tick & 1) == 0 ? 1f : -1f) * t.StaggerShakePx * EnemyViewTuning.Px;
            tr.localPosition = new Vector3(vp.x + off.x, vp.y + off.y, 0f);

            // 클립(시뮬레이션이 정한 e.AnimClip, D-37 8fps). 빙결 중에는 그림이 멈춘다
            if (e.AnimClip != v.Clip || e.AnimStartTick != v.ClipStartTick)
            {
                v.Clip = e.AnimClip;
                v.ClipStartTick = e.AnimStartTick;
                v.Anim = v.IsDummy ? null : ResolveAnim(e.DefId, e.IsElite, e.AnimClip);
                v.ClipTime = Mathf.Max(0f, (tick - e.AnimStartTick) * SimConst.TickSeconds);
            }
            else if (!frozen) v.ClipTime += dt;

            Sprite frame;
            if (v.IsDummy)
            {
                var da = _art != null ? _art.World(t.DummyWorldAsset) : null;
                if (v.DummyHitLeft > 0f && da != null && da.Frames != null && da.Frames.Length > 1)
                {
                    float el = t.DummyHitSeconds - v.DummyHitLeft;
                    int idx = 1 + Mathf.Min(da.Frames.Length - 2, (int)(el / t.DummyHitSeconds * (da.Frames.Length - 1)));
                    frame = da.Frames[idx];
                    v.DummyHitLeft -= dt;
                }
                else frame = da != null && da.Frames != null && da.Frames.Length > 0 ? da.Frames[0] : (_art != null ? _art.Placeholder(64, 96) : null);
            }
            else frame = v.Anim != null ? v.Anim.At(v.ClipTime) : null;

            bool flying = e.Has(EnemyTags.Flying);
            float lift = flying ? t.FlyHeight + Mathf.Sin(_clock * Mathf.PI * 2f * t.FlyBobPerSecond + e.Id) * t.FlyBobCells : 0f;
            bool bodyVisible = !spawning && (!e.Burrowed || (v.Clip != null && t.BurrowVisibleClips.Contains(v.Clip) && HasClip(e.DefId, v.Clip)));

            // 몸
            if (bodyVisible && frame != null)
            {
                var body = PoolUtil.Ensure(_pool, ref v.Body, frame, OBody, tr);
                if (body != null)
                {
                    body.transform.localPosition = new Vector3(0f, lift, 0f);
                    v.LastFlip = EnemyLook.FlipX(e.Facing);
                    body.flipX = v.LastFlip;
                    body.color = Color.white;
                    int rp = e.IsBoss ? Mathf.Max(rim, t.BossRimPercentMin) : rim;
                    EnemyLook.Outline(e.IsElite, rp, t, out var oc, out var os);
                    if (e.IsBoss && v.RageRimElapsed >= 0f)
                    {
                        os = EnemyLook.RageRimPx(v.RageRimElapsed, t, os);
                        v.RageRimElapsed += dt;
                        if (v.RageRimElapsed >= t.RageRimSeconds) v.RageRimElapsed = -1f;
                    }
                    Color flashCol = Color.white;
                    float flashAmt = 0f;
                    if (v.HitFlashLeft > 0f) { flashCol = Color.white; flashAmt = 1f; }
                    else if (e.WeaponTint) { flashCol = t.WeaponTint; flashAmt = t.WeaponTintAmount; }
                    ApplyLook(body, oc, os, flashCol, flashAmt);
                    body.transform.localScale = JuiceScale(v, dt);
                }
            }
            else { PoolUtil.Release(_pool, ref v.Body); v.HadBody = false; }

            float top = lift + PoolUtil.TopAbovePivot(frame);
            float center = lift + PoolUtil.CenterAbovePivot(frame);
            float bodyW = PoolUtil.WidthCells(frame);

            // 그림자(발밑 = 판정 위치). 땅속이면 둔덕
            if (!spawning && !e.Burrowed)
            {
                var sh = PoolUtil.Ensure(_pool, ref v.Shadow, _art != null ? _art.Circle(32) : null, SortBands.Shadows, _root);
                if (sh != null)
                {
                    float sw = Mathf.Max(t.ShadowMinWidth, e.BodyRadius * t.ShadowWidthPerRadius);
                    sh.transform.localPosition = new Vector3(vp.x, vp.y, 0f);
                    sh.transform.localScale = new Vector3(sw, sw * t.ShadowHeightRatio, 1f);
                    sh.color = DangerColor.WithAlpha(t.Shadow, t.ShadowAlpha * (flying ? 0.7f : 1f));
                }
            }
            else PoolUtil.Release(_pool, ref v.Shadow);
            if (e.Burrowed && !spawning)
            {
                var mound = PoolUtil.Ensure(_pool, ref v.Mound, PoolUtil.LoopFrame(Fx("enemy", t.MoundKey), _clock), SortBands.Shadows + 1, _root);
                if (mound != null) mound.transform.localPosition = new Vector3(vp.x, vp.y, 0f);
            }
            else PoolUtil.Release(_pool, ref v.Mound);

            // 등장 예고: 덤불 흔들림(위험색 아님). 가시덩굴은 흙 금
            if (spawning) DrawSpawnBush(v.Bush, tr, e.DefId == Ids.Enemy.VineThorn, tick - e.AiStateStartTick);
            else for (int i = 0; i < v.Bush.Length; i++) PoolUtil.Release(_pool, ref v.Bush[i]);

            // 엘리트: 머리 장식, 뿔 표식, 작은 체력 바, 등장 기둥 빛
            if (e.IsElite && bodyVisible)
            {
                string key = e.DefId != null && e.DefId.StartsWith("enemy.", System.StringComparison.Ordinal) ? e.DefId.Substring(6) : e.DefId;
                var ov = Monster("elite." + key + ".overlay", "overlay");
                var ovs = ov != null && ov.Frames != null && ov.Frames.Length > 0 ? ov.Frames[0] : null;
                if (ovs != null)
                {
                    var o = PoolUtil.Ensure(_pool, ref v.Overlay, ovs, OOverlay, tr);
                    if (o != null) { o.flipX = v.LastFlip; o.transform.localPosition = new Vector3(0f, top - t.EliteOverlaySinkPx * EnemyViewTuning.Px, 0f); }
                }
                var horn = Monster("elite.horn", "idle");
                var hs = horn != null && horn.Frames != null && horn.Frames.Length > 0 ? horn.Frames[0] : null;
                float hornY = top + t.HornGapPx * EnemyViewTuning.Px;
                var h = PoolUtil.Ensure(_pool, ref v.Horn, hs, OHorn, tr);
                if (h != null) h.transform.localPosition = new Vector3(0f, hornY, 0f);
                float barY = hornY + PoolUtil.TopAbovePivot(hs) + t.EliteHpBarGapPx * EnemyViewTuning.Px;
                DrawHpBar(v, tr, barY, e.MaxHp > 0f ? Mathf.Clamp01(e.Hp / e.MaxHp) : 0f);
            }
            else
            {
                PoolUtil.Release(_pool, ref v.Overlay); PoolUtil.Release(_pool, ref v.Horn);
                PoolUtil.Release(_pool, ref v.HpBack); PoolUtil.Release(_pool, ref v.HpFill);
            }
            if (e.IsElite && !v.ColumnShown && e.Ai == EnemyAiState.Spawning && tick - e.AiStateStartTick < SimConst.Ticks(t.EliteColumnSeconds))
            {
                v.ColumnShown = true;
                v.ColumnLeft = t.EliteColumnSeconds - (tick - e.AiStateStartTick) * SimConst.TickSeconds;
            }
            if (v.ColumnLeft > 0f)
            {
                var ca = Fx("enemy", "elite_column");
                var c = PoolUtil.Ensure(_pool, ref v.Column, PoolUtil.LoopFrame(ca, t.EliteColumnSeconds - v.ColumnLeft), OColumn, tr);
                if (c != null && c.sprite != null) c.transform.localPosition = new Vector3(0f, c.sprite.pivot.y / Mathf.Max(1f, c.sprite.pixelsPerUnit), 0f); // 아래 끝이 발밑
                v.ColumnLeft -= dt;
            }
            else PoolUtil.Release(_pool, ref v.Column);

            DrawStatus(v, e, tr, tick, dt, frozen, stunned, bodyVisible, top, center, bodyW);

            // 보스: 격노 검은 김, 밤삼킴 마디
            if (e.IsBoss)
            {
                var boss = w.Boss != null && w.Boss.EnemyId == e.Id ? w.Boss : null;
                UpdateSmoke(v, tr, boss != null ? boss.RageStacks : 0, dt, center, bodyW);
                UpdateSegments(w, v, e, boss, alpha, dt, rim);
            }
        }

        bool JuiceOn => _vc != null && _vc.EffectsLevel < 2 && (_squash > 0f || _knockPx > 0f || _appearSec > 0f);

        void ReadJuice(Wizard.Solo.Data.IContentDb content)
        {
            _squash = _critSquash = _squashSec = _knockPx = _knockSec = _appearSec = _appearOver = 0f;
            _bossJuiceMul = 1f;
            Newtonsoft.Json.Linq.JObject root = null;
            try { root = content != null ? content.SoloExtra(Effects.JuiceLayer.JuiceName) : null; } catch (System.Exception) { root = null; }
            var j = new Effects.EffectsTuning(root);
            _squash = j.F("enemy", "squash");
            _critSquash = j.F("enemy", "crit_squash");
            _squashSec = j.F("enemy", "squash_seconds");
            _knockPx = j.F("enemy", "knock_px");
            _knockSec = j.F("enemy", "knock_seconds");
            _bossJuiceMul = j.Token("enemy", "boss_mul") != null ? j.F("enemy", "boss_mul") : 1f;
            _appearSec = j.F("enemy", "appear_seconds");
            _appearOver = j.F("enemy", "appear_overshoot");
        }

        /// <summary>몸 그림의 배율: 피격 찌그러짐(가로 +, 세로 −, 발밑 고정) × 처음 나타날 때 튀어나옴. 연출 감도 최소면 1.</summary>
        Vector3 JuiceScale(EnemyView v, float dt)
        {
            if (!v.HadBody)
            {
                v.HadBody = true;
                v.AppearLeft = v.IsBoss ? 0f : _appearSec;
            }
            if (!JuiceOn) { v.SquashLeft = v.AppearLeft = 0f; return Vector3.one; }
            float sx = 1f, sy = 1f;
            if (v.SquashLeft > 0f && _squashSec > 0f)
            {
                float k = v.SquashLeft / _squashSec;                // 1 → 0
                float wob = Mathf.Cos((1f - k) * Mathf.PI * 1.5f);   // 늘었다가 살짝 되튐
                float a = v.SquashAmp * k * wob;
                sx *= 1f + a;
                sy *= 1f - a * 0.8f;
                v.SquashLeft -= dt;
            }
            if (v.AppearLeft > 0f && _appearSec > 0f)
            {
                float k = 1f - v.AppearLeft / _appearSec;          // 0 → 1
                float s = k < 0.6f ? Mathf.Lerp(0.3f, 1f + _appearOver, k / 0.6f) : Mathf.Lerp(1f + _appearOver, 1f, (k - 0.6f) / 0.4f);
                sx *= s;
                sy *= s;
                v.AppearLeft -= dt;
            }
            return new Vector3(sx, sy, 1f);
        }

        void ApplyLook(SpriteRenderer sr, Color outline, float outlinePx, Color flashCol, float flashAmt)
        {
            sr.GetPropertyBlock(_mpb);
            _mpb.SetColor(SpriteShaderProps.OutlineColor, outline);
            _mpb.SetFloat(SpriteShaderProps.OutlineSize, outlinePx);
            _mpb.SetColor(SpriteShaderProps.FlashColor, flashCol);
            _mpb.SetFloat(SpriteShaderProps.FlashAmount, flashAmt);
            sr.SetPropertyBlock(_mpb);
        }

        void DrawHpBar(EnemyView v, Transform tr, float y, float ratio)
        {
            var px = _art != null ? _art.WhitePixel : null;
            float w = _t.EliteHpBarWidth, h = _t.EliteHpBarPx * EnemyViewTuning.Px;
            var back = PoolUtil.Ensure(_pool, ref v.HpBack, px, OHpBack, tr);
            if (back != null)
            {
                back.color = _t.HpBarBack;
                back.transform.localPosition = new Vector3(0f, y, 0f);
                back.transform.localScale = new Vector3(w + 2f * EnemyViewTuning.Px, h + 2f * EnemyViewTuning.Px, 1f);
            }
            var fill = PoolUtil.Ensure(_pool, ref v.HpFill, px, OHpFill, tr);
            if (fill != null)
            {
                fill.color = _t.HpBarFill;
                float fw = w * ratio;
                fill.transform.localPosition = new Vector3(-w * 0.5f + fw * 0.5f, y, 0f);
                fill.transform.localScale = new Vector3(fw, h, 1f);
                fill.enabled = fw > 0f;
            }
        }

        void DrawStatus(EnemyView v, EnemyState e, Transform tr, int tick, float dt, bool frozen, bool stunned, bool bodyVisible, float top, float center, float bodyW)
        {
            var t = _t;
            float px = EnemyViewTuning.Px;
            // 감속: 발목 서리 고리(−30% 두 줄, −20% 한 줄). 끝에서부터 녹아 짧아진다
            bool slowDouble = EnemyLook.SlowIsDouble(e, tick, t, out int remain);
            if (remain > 0 && bodyVisible)
            {
                var anim = Fx("status", slowDouble ? "slow_ankle_ring_double" : "slow_ankle_ring");
                var s = PoolUtil.Ensure(_pool, ref v.Slow, PoolUtil.LoopFrame(anim, _clock), OSlow, tr);
                if (s != null)
                {
                    float k = Mathf.Clamp01(remain / (float)Mathf.Max(1, t.SlowFullTicks));
                    float scale = Mathf.Max(1f, e.BodyRadius * 2f / 0.5f);
                    s.transform.localPosition = new Vector3(0f, 2f * px, 0f);
                    s.transform.localScale = new Vector3(scale * k, scale, 1f);
                }
            }
            else PoolUtil.Release(_pool, ref v.Slow);

            // 잠식: 연보라 점선 테 2px(시계 방향으로 줄어듦) + 보라 금
            int corr = e.CorrosionUntilTick - tick;
            if (corr > 0 && bodyVisible)
            {
                if (v.CorrosionRing == null) v.CorrosionRing = new MeshSlot(_pool, OCorrosion, tr);
                if (v.CorrosionRing.Renderer != null)
                {
                    v.CorrosionRing.Renderer.transform.SetParent(tr, false);
                    v.CorrosionRing.Renderer.transform.localPosition = Vector3.zero;
                    v.CorrosionRing.Renderer.sortingOrder = OCorrosion;
                    var b = v.CorrosionMesh;
                    b.Clear();
                    b.CircleSegments = 48;
                    float r = Mathf.Max(e.BodyRadius, bodyW * 0.5f);
                    float k = Mathf.Clamp01(corr / (float)Mathf.Max(1, t.CorrosionFullTicks));
                    float sweep = 360f * k;
                    // 12시에서 시작해 시계 방향으로 남은 만큼: 남은 호 = [90 − sweep, 90]
                    b.AddDashedArc(new Vector2(0f, center), r, r + t.CorrosionPx * px, 90f - sweep, sweep, t.CorrosionDashDegrees, t.CorrosionGapDegrees, t.CorrosionEdge);
                    v.CorrosionRing.Upload(b);
                }
                var cm = PoolUtil.Ensure(_pool, ref v.CorrosionMark, PoolUtil.LoopFrame(Fx("status", "corrosion"), _clock), OCorrosionMark, tr);
                if (cm != null) cm.transform.localPosition = new Vector3(0f, center, 0f);
            }
            else
            {
                if (v.CorrosionRing != null) { v.CorrosionRing.Release(); v.CorrosionRing = null; }
                PoolUtil.Release(_pool, ref v.CorrosionMark);
            }

            // 빙결: 얼음 껍질, 풀리면 깨짐
            if (frozen && bodyVisible)
            {
                bool large = bodyW * SimConst.PixelsPerUnit >= t.FrozenLargePx;
                var fa = Fx("status", large ? "frozen_shell_96" : "frozen_shell_64");
                var f = PoolUtil.Ensure(_pool, ref v.Frozen, fa != null && fa.Frames.Length > 0 ? fa.Frames[0] : null, OFrozen, tr);
                if (f != null) f.transform.localPosition = new Vector3(0f, center, 0f);
                v.WasFrozen = true;
            }
            else
            {
                PoolUtil.Release(_pool, ref v.Frozen);
                if (v.WasFrozen) { v.WasFrozen = false; v.ShatterTime = 0f; }
            }
            if (v.ShatterTime >= 0f)
            {
                var sa = Fx("status", "frozen_shatter");
                if (sa == null || sa.Frames == null || v.ShatterTime >= sa.Duration) { v.ShatterTime = -1f; PoolUtil.Release(_pool, ref v.Shatter); }
                else
                {
                    var s = PoolUtil.Ensure(_pool, ref v.Shatter, sa.At(v.ShatterTime), OShatter, tr);
                    if (s != null) s.transform.localPosition = new Vector3(0f, center, 0f);
                    v.ShatterTime += dt;
                }
            }

            // 기절: 머리 위 별
            if (stunned && bodyVisible)
            {
                var st = PoolUtil.Ensure(_pool, ref v.Stun, PoolUtil.LoopFrame(Fx("enemy", "stun_star"), _clock), OStun, tr);
                if (st != null) st.transform.localPosition = new Vector3(0f, top + 4f * px, 0f);
            }
            else PoolUtil.Release(_pool, ref v.Stun);

            // 북두꺼비 버프
            if (e.DrumBuffed && bodyVisible)
            {
                var bf = PoolUtil.Ensure(_pool, ref v.Buff, PoolUtil.LoopFrame(Fx("enemy", "toad_buff"), _clock), OBuff, tr);
                if (bf != null) bf.transform.localPosition = Vector3.zero;
            }
            else PoolUtil.Release(_pool, ref v.Buff);

            // 살얼음 서리 눈금(5개까지, 10 요약서 3.7절)
            int marks = frozen || !bodyVisible ? 0 : Mathf.Clamp(e.ThinIceCount, 0, t.ThinIceMaxMarks);
            var ma = Fx("status", "frost_mark");
            var msp = ma != null && ma.Frames != null && ma.Frames.Length > 0 ? ma.Frames[0] : null;
            while (v.Marks.Count > marks) { var m = v.Marks[v.Marks.Count - 1]; PoolUtil.Release(_pool, ref m); v.Marks.RemoveAt(v.Marks.Count - 1); }
            for (int i = 0; i < marks; i++)
            {
                SpriteRenderer m = i < v.Marks.Count ? v.Marks[i] : null;
                PoolUtil.Ensure(_pool, ref m, msp, OMarks, tr);
                if (m == null) break;
                if (i >= v.Marks.Count) v.Marks.Add(m);
                float step = 10f * px;
                m.transform.localPosition = new Vector3((i - (marks - 1) * 0.5f) * step, -4f * px, 0f);
            }
        }

        void DrawSpawnBush(SpriteRenderer[] parts, Transform tr, bool vine, int elapsedTicks)
        {
            var t = _t;
            float px = EnemyViewTuning.Px;
            float shake = Mathf.Sin(elapsedTicks * SimConst.TickSeconds * Mathf.PI * 2f * t.SpawnShakePerSecond) * t.SpawnShakePx * px;
            if (vine)
            {
                for (int i = 1; i < parts.Length; i++) PoolUtil.Release(_pool, ref parts[i]);
                var c = PoolUtil.Ensure(_pool, ref parts[0], PoolUtil.LoopFrame(Fx("enemy", t.VineNoticeKey), elapsedTicks * SimConst.TickSeconds), OBush, tr);
                if (c != null) c.transform.localPosition = new Vector3(shake, 0f, 0f);
                return;
            }
            var circle = _art != null ? _art.Circle(32) : null;
            for (int i = 0; i < parts.Length; i++)
            {
                var p = PoolUtil.Ensure(_pool, ref parts[i], circle, OBush + i, tr);
                if (p == null) continue;
                float d = t.SpawnBushRadius * 2f * (i == 1 ? 1f : 0.75f);
                float x = (i - 1) * t.SpawnBushRadius * 0.8f + shake * (i == 1 ? 1f : -1f);
                p.transform.localPosition = new Vector3(x, d * 0.4f, 0f);
                p.transform.localScale = new Vector3(d, d * 0.8f, 1f);
                var col = t.SpawnBush;
                if (i != 1) col = new Color32((byte)(col.r * 0.85f), (byte)(col.g * 0.85f), (byte)(col.b * 0.85f), 255);
                p.color = col;
            }
        }

        void UpdateNotices(SimWorld w, int tick, float dt)
        {
            for (int i = _notices.Count - 1; i >= 0; i--)
            {
                var n = _notices[i];
                if (tick >= n.EndTick || OverlapsSpawningEnemy(w, n.Pos)) { ReleaseNotice(n); _notices.RemoveAt(i); continue; }
                n.Time += dt;
                if (n.Parts[0] == null && _root == null) continue;
                // 예고 자리마다 작은 묶음: 부모 없이 띠 순서로
                var vp = SimConst.ToView(n.Pos, w.ArenaHeight);
                int order = SortBands.Bodies + SortBands.YOrder(n.Pos.y);
                bool vine = n.Species == Ids.Enemy.VineThorn;
                float shake = Mathf.Sin((tick - n.StartTick) * SimConst.TickSeconds * Mathf.PI * 2f * _t.SpawnShakePerSecond) * _t.SpawnShakePx * EnemyViewTuning.Px;
                if (vine)
                {
                    var c = PoolUtil.Ensure(_pool, ref n.Crack, PoolUtil.LoopFrame(Fx("enemy", _t.VineNoticeKey), n.Time), order, _root);
                    if (c != null) c.transform.localPosition = new Vector3(vp.x + shake, vp.y, 0f);
                    continue;
                }
                var circle = _art != null ? _art.Circle(32) : null;
                for (int k = 0; k < n.Parts.Length; k++)
                {
                    var p = PoolUtil.Ensure(_pool, ref n.Parts[k], circle, order, _root);
                    if (p == null) continue;
                    float d = _t.SpawnBushRadius * 2f * (k == 1 ? 1f : 0.75f);
                    float x = (k - 1) * _t.SpawnBushRadius * 0.8f + shake * (k == 1 ? 1f : -1f);
                    p.transform.localPosition = new Vector3(vp.x + x, vp.y + d * 0.4f, 0f);
                    p.transform.localScale = new Vector3(d, d * 0.8f, 1f);
                    p.color = _t.SpawnBush;
                }
            }
        }

        static bool OverlapsSpawningEnemy(SimWorld w, Vector2 pos)
        {
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (e.Ai == EnemyAiState.Spawning && !e.Dead && (e.Pos - pos).sqrMagnitude < 0.0625f) return true;
            }
            return false;
        }

        void ReleaseNotice(SpawnNotice n)
        {
            for (int i = 0; i < n.Parts.Length; i++) PoolUtil.Release(_pool, ref n.Parts[i]);
            PoolUtil.Release(_pool, ref n.Crack);
        }

        // ------------------------------------------------------------ 보스

        void UpdateSmoke(EnemyView v, Transform tr, int stacks, float dt, float center, float bodyW)
        {
            var t = _t;
            int lvl = Mathf.Clamp(_vc != null ? _vc.EffectsLevel : 0, 0, t.RageSmokeEffectsScale.Length - 1);
            float rate = stacks * t.RageSmokePerStackPerSecond * (t.RageSmokeEffectsScale.Length > 0 ? t.RageSmokeEffectsScale[lvl] : 1f);
            v.SmokeAcc += rate * dt;
            var anim = Fx("enemy", "rage_smoke");
            float life = anim != null && anim.Duration > 0f ? anim.Duration * 2f : 0.6f;
            while (v.SmokeAcc >= 1f)
            {
                v.SmokeAcc -= 1f;
                var s = new Smoke { Age = 0f, Life = life, Local = new Vector2(Random.Range(-0.5f, 0.5f) * bodyW, center + Random.Range(-0.3f, 0.3f)) };
                PoolUtil.Ensure(_pool, ref s.Sr, anim != null && anim.Frames.Length > 0 ? anim.Frames[0] : null, OSmoke, tr);
                if (s.Sr != null) v.Smokes.Add(s);
            }
            for (int i = v.Smokes.Count - 1; i >= 0; i--)
            {
                var s = v.Smokes[i];
                s.Age += dt;
                if (s.Age >= s.Life || s.Sr == null) { PoolUtil.Release(_pool, ref s.Sr); v.Smokes.RemoveAt(i); continue; }
                float k = s.Age / s.Life;
                if (anim != null && anim.Frames.Length > 0) s.Sr.sprite = anim.Frames[Mathf.Min(anim.Frames.Length - 1, (int)(k * anim.Frames.Length))];
                s.Sr.transform.localPosition = new Vector3(s.Local.x, s.Local.y + k * t.RageSmokeRiseCells, 0f);
                s.Sr.color = new Color(1f, 1f, 1f, 1f - k);
            }
        }

        void UpdateSegments(SimWorld w, EnemyView v, EnemyState e, BossState boss, float alpha, float dt, int rim)
        {
            var t = _t;
            int n = boss != null ? boss.Segments.Count : 0;
            if (n == 0) { ReleaseSegments(v, 0); return; }
            if (w.Tick != v.SegTick)
            {
                v.SegTick = w.Tick;
                v.SegPrev.Clear();
                if (v.SegCur.Count == n) v.SegPrev.AddRange(v.SegCur); else v.SegPrev.AddRange(boss.Segments);
                v.SegCur.Clear();
                v.SegCur.AddRange(boss.Segments);
            }
            v.SegClock += dt;
            bool warn = e.WeaponTint && boss.CurrentPattern == t.SerpentWarnPattern;
            EnemyLook.Outline(false, Mathf.Max(rim, t.BossRimPercentMin), t, out var oc, out var os);
            for (int i = 0; i < n; i++)
            {
                bool tail = i == n - 1;
                string entry = tail ? t.SerpentTailEntry : t.SerpentSegmentEntry;
                string clip = !tail && warn ? "warn" : "move";
                var anim = Monster(entry, clip);
                SpriteRenderer sr = i < v.Segs.Count ? v.Segs[i] : null;
                SpriteRenderer sh = i < v.SegShadows.Count ? v.SegShadows[i] : null;
                Vector2 sim = ViewMath.Lerp(i < v.SegPrev.Count ? v.SegPrev[i] : v.SegCur[i], v.SegCur[i], alpha);
                var vp = SimConst.ToView(sim, w.ArenaHeight);
                PoolUtil.Ensure(_pool, ref sr, anim != null ? anim.At(v.SegClock) : null, SortBands.Bodies + SortBands.YOrder(sim.y), _root);
                PoolUtil.Ensure(_pool, ref sh, _art != null ? _art.Circle(32) : null, SortBands.Shadows, _root);
                if (i >= v.Segs.Count) { v.Segs.Add(sr); v.SegShadows.Add(sh); }
                else { v.Segs[i] = sr; v.SegShadows[i] = sh; }
                if (sr != null)
                {
                    sr.transform.localPosition = new Vector3(vp.x, vp.y, 0f);
                    Vector2 d = i == 0 ? (Vector2)e.Pos - v.SegCur[0] : v.SegCur[i - 1] - v.SegCur[i];
                    sr.flipX = d.x < 0f;
                    Color fc = Color.white;
                    float fa = 0f;
                    if (v.HitFlashLeft > 0f) fa = 1f;
                    else if (warn) { fc = t.WeaponTint; fa = t.WeaponTintAmount; }
                    ApplyLook(sr, oc, os, fc, fa);
                }
                if (sh != null)
                {
                    float sw = Mathf.Max(t.ShadowMinWidth, 0.5f * t.ShadowWidthPerRadius);
                    sh.transform.localPosition = new Vector3(vp.x, vp.y, 0f);
                    sh.transform.localScale = new Vector3(sw, sw * t.ShadowHeightRatio, 1f);
                    sh.color = DangerColor.WithAlpha(t.Shadow, t.ShadowAlpha);
                }
            }
            ReleaseSegments(v, n);
        }

        void BeginDying(EnemyView v)
        {
            if (v.Dying) return;
            v.Dying = true;
            v.DyingTime = 0f;
            v.SquashLeft = v.KnockLeft = v.AppearLeft = 0f;
            if (v.Body != null) v.Body.transform.localScale = Vector3.one;
            v.Anim = Monster(v.DefId, "death");
            for (int i = 0; i < v.Smokes.Count; i++) PoolUtil.Release(_pool, ref v.Smokes[i].Sr);
            v.Smokes.Clear();
            PoolUtil.Release(_pool, ref v.Slow); PoolUtil.Release(_pool, ref v.Stun); PoolUtil.Release(_pool, ref v.Frozen);
            PoolUtil.Release(_pool, ref v.CorrosionMark);
            if (v.CorrosionRing != null) { v.CorrosionRing.Release(); v.CorrosionRing = null; }
        }

        /// <summary>보스 사망 느린 연출(05 요약서 8.3절: 1.0초 → 흰 번쩍). WaveClearing 동안 이어지고 w.Boss가 null이 되면 접는다. false = 접는다.</summary>
        bool UpdateDying(SimWorld w, EnemyView v, float dt)
        {
            var t = _t;
            if (!v.IsBoss) return false;
            if (w.Boss == null) return false;
            var ph = w.Run.Phase;
            if (ph == RunPhase.LootPick || ph == RunPhase.Camp || ph == RunPhase.StartCamp || ph == RunPhase.WavePrelude || ph == RunPhase.RunEnded) return false;
            v.DyingTime += dt;
            var a = v.Anim;
            Sprite frame = null;
            if (a != null && a.Frames != null && a.Frames.Length > 0)
            {
                float k = t.BossDeathSlowSeconds > 0f ? Mathf.Clamp01(v.DyingTime / t.BossDeathSlowSeconds) : 1f;
                frame = a.Frames[Mathf.Min(a.Frames.Length - 1, (int)(k * a.Frames.Length))];
            }
            float flashEnd = t.BossDeathSlowSeconds + t.BossDeathFlashSeconds;
            if (v.DyingTime >= flashEnd)
            {
                PoolUtil.Release(_pool, ref v.Body);
                PoolUtil.Release(_pool, ref v.Shadow);
                ReleaseSegments(v, 0);
                return true; // 몸은 다 사라졌지만 보스 상태가 남아 있는 동안 자리는 둔다
            }
            if (v.Go != null && frame != null)
            {
                var body = PoolUtil.Ensure(_pool, ref v.Body, frame, OBody, v.Go.transform);
                if (body != null)
                {
                    body.flipX = v.LastFlip;
                    bool flash = v.DyingTime >= t.BossDeathSlowSeconds;
                    EnemyLook.Outline(false, t.BossRimPercentMin, t, out var oc, out var os);
                    ApplyLook(body, oc, os, Color.white, flash ? 1f : 0f);
                }
            }
            return true;
        }

        // ------------------------------------------------------------ 그림 찾기

        SpriteAnim ResolveAnim(string defId, bool elite, string clip)
        {
            if (_art == null) return null;
            EnemyLook.ResolveClip(_art.MonsterClipNames, defId, elite, clip, out var entry, out var name);
            return _art.MonsterClip(entry, name);
        }

        bool HasClip(string defId, string clip)
        {
            if (_art == null || defId == null) return false;
            var names = _art.MonsterClipNames(defId);
            for (int i = 0; i < names.Count; i++) if (names[i] == clip) return true;
            return false;
        }

        SpriteAnim Monster(string entry, string clip) => _art != null ? _art.MonsterClip(entry, clip) : null;

        SpriteAnim Fx(string group, string key)
        {
            if (_art == null || key == null) return null;
            string k = group + "|" + key;
            if (_fx.TryGetValue(k, out var a)) return a;
            a = _art.Effect(group, key);
            _fx[k] = a;
            return a;
        }

        int RimPercent(SimWorld w)
        {
            int wave = w.Run != null && w.Run.Wave > 0 ? w.Run.Wave : (w.Wave != null && w.Wave.Wave > 0 ? w.Wave.Wave : 1);
            if (wave == _rimWave) return _rimPercent;
            _rimWave = wave;
            var tod = w.Content != null ? w.Content.Tod(DayCycle.Tod(wave)) : null;
            _rimPercent = tod != null ? tod.RimStrengthPercent : 0;
            return _rimPercent;
        }
    }
}
