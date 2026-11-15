using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 이펙트 층(AddEffects, 00 문서 7절, 10 요약서 3절): 적중 불꽃·피해 숫자 5종·약점 알아냄 문양, 폭발, 처치 조각·골드 조각,
    /// 바닥 흔적, 비기 연출(발동 문양·기술 이름·주 효과·고유 비기), 스침 고리, 피격(그림 정지 0.12초 + 화면 가장자리 0.3초), 드랍,
    /// 적 탄 소거 김, 화면 흔들림·그림 정지 요청(바인딩 표), 번쩍 관리기, 누적 가림 넓이 셈, 입자 예산.
    /// 상태로 그리는 것(드랍)은 Build에서 지금 상태만 보고 집어 든다. 일회 효과는 다시 내지 않는다.
    /// 피격 표시는 연출 감도·옵션과 무관하다(10 3.10절).
    /// </summary>
    public sealed class EffectLayer : IWorldViewLayer
    {
        sealed class NumView
        {
            public int Handle;
            public readonly List<SpriteRenderer> Glyphs = new List<SpriteRenderer>();
            public SpriteRenderer Icon, Corroded;
            public WorldTextPopups.Popup Prefix;
            public Vector2 Pos;
            public float Age, Life;
            public DamageNumberSpec Spec;
            public Color Color;
            /// <summary>튀어 오르는 피해 숫자(Juice.json damage_pop): 옆 흐름(칸/초), 크게 터지는 배율.</summary>
            public float Drift, Pop, Size;
        }

        sealed class AffinityMarker
        {
            public int EnemyId, StartTick;
            public Vector2 Pos;
            public SpriteRenderer Icon, Mark;
            public Affinity Affinity;
        }

        sealed class DropView
        {
            public DropKind Kind;
            public SpriteRenderer Main, Pillar;
            public SpriteAnim Anim, PillarAnim;
            public int SpawnTick;
            public bool Seen;
        }

        struct PendingKill { public SimEvent E; }

        readonly EffectsShared _s;
        readonly ScreenOverlay _overlay = new ScreenOverlay();
        readonly List<NumView> _numbers = new List<NumView>();
        readonly List<AffinityMarker> _markers = new List<AffinityMarker>();
        readonly Dictionary<int, DropView> _drops = new Dictionary<int, DropView>();
        readonly Dictionary<int, string> _enemyElement = new Dictionary<int, string>();
        readonly Dictionary<int, List<int>> _recentHits = new Dictionary<int, List<int>>();
        readonly Dictionary<int, int> _bigHitUntil = new Dictionary<int, int>();
        readonly Dictionary<int, Vector2> _shotSnapshot = new Dictionary<int, Vector2>();
        readonly HashSet<int> _styleChains = new HashSet<int>();
        readonly List<PendingKill> _kills = new List<PendingKill>();
        readonly List<int> _tmp = new List<int>();
        readonly int[] _artTick = new int[SimConst.MaxPlayers];
        readonly int[] _debtTick = new int[SimConst.MaxPlayers];
        readonly int[] _lineTick = new int[SimConst.MaxPlayers];
        readonly Vector2[] _artPos = new Vector2[SimConst.MaxPlayers];
        ViewContext _vc;
        EffectsTuning _t;
        IArtLibrary _art;
        INameTable _names;
        DamageNumberMerger _merger;
        GapGate _critShake;
        KillBurstCounter _killBurst;
        SimWorld _w;
        EffectsTuning _juice;
        bool _popOn;

        public EffectLayer(EffectsShared shared) { _s = shared ?? new EffectsShared(); }

        public EffectsShared Shared => _s;
        public ScreenOverlay Overlay => _overlay;

        // ---- 테스트·점검용 셈
        public int DamageNumberCount => _numbers.Count;
        public int AffinityMarkerCount => _markers.Count;
        public int DropViewCount => _drops.Count;
        public int GroundMarksSpawned { get; private set; }
        public int NamePopupsShown { get; private set; }
        public int KillShardsSpawned { get; private set; }
        public int KillSmokesSpawned { get; private set; }
        public int FlashesSubmitted { get; private set; }
        public bool HasDropPillar(int dropId) { DropView d; return _drops.TryGetValue(dropId, out d) && d.Pillar != null; }
        public string DamageNumberGlyphs(int index) => index >= 0 && index < _numbers.Count ? _numbers[index].Spec.Glyphs : null;
        /// <summary>점검용: index번째 숫자의 첫 글리프 배율(0 = 없음).</summary>
        public float DamageNumberScaleAt(int index) => index >= 0 && index < _numbers.Count && _numbers[index].Glyphs.Count > 0 && _numbers[index].Glyphs[0] != null
            ? _numbers[index].Glyphs[0].transform.localScale.x : 0f;
        public Vector2 DamageNumberPosAt(int index) => index >= 0 && index < _numbers.Count ? _numbers[index].Pos : Vector2.zero;
        public DamageNumberKind DamageNumberKindAt(int index) => index >= 0 && index < _numbers.Count ? _numbers[index].Spec.Kind : DamageNumberKind.Normal;

        public void Build(ViewContext vc, SimWorld w)
        {
            ClearAll();
            _vc = vc;
            _w = w;
            _s.Ensure(vc, w);
            _s.EffectLayerAttached = true;
            _t = _s.Data.Tuning;
            _art = vc != null && vc.Game != null ? vc.Game.Art : null;
            var content = vc != null && vc.Game != null && vc.Game.Content != null ? vc.Game.Content : (w != null ? w.Content : null);
            Newtonsoft.Json.Linq.JObject juice = null;
            try { juice = content != null ? content.SoloExtra(JuiceLayer.JuiceName) : null; } catch (System.Exception) { juice = null; }
            _juice = new EffectsTuning(juice);
            _popOn = _juice.B("damage_pop", "enabled");
            _names = vc != null && vc.Game != null ? vc.Game.Names : null;
            _merger = new DamageNumberMerger(_s.Data.Budget.DamageMergeWindowSeconds, _s.Data.Budget.DamageNumbersMax);
            _critShake = new GapGate(_t.F("crit_shake_gap_seconds"));
            _killBurst = new KillBurstCounter(_t.F("multi_kill", "window_seconds"), _t.I("multi_kill", "count"));
            for (int i = 0; i < _artTick.Length; i++) { _artTick[i] = _debtTick[i] = _lineTick[i] = int.MinValue; }
            _overlay.Build(vc != null ? vc.HudRoot : null, _t.F("player_hit", "edge_px"), _t.F("flash", "border_px"));
            // 지금 상태만 보고: 깔린 드랍과 떠 있는 적 탄 위치.
            SyncDrops(w);
            Snapshot(w);
        }

        // ------------------------------------------------------------ 이벤트

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_vc == null) return;
            _w = w;
            switch (e.Type)
            {
                case SimEventType.EnemyDamaged: OnEnemyDamaged(w, e); break;
                case SimEventType.AffinityDiscovered: OnAffinity(w, e); break;
                case SimEventType.ProjectilePassed:
                    // 그림자 구체가 적을 지날 때마다 물결(event_bindings projectile.passed).
                    SpawnAnim(Fx("player", "shadow_orb_ripple"), e.Pos, 1f, 0f, Color.white, SortBands.Bursts, true, true);
                    break;
                case SimEventType.EnemyDashCancelled:
                    // "끊음" 표식 + 흔들림(event_bindings art.recoil_shot.charge_broken). 그림이 없으면 대체 그림(09 요약서 9절).
                    SpawnAnim(Fx("player", "art_break_mark"), e.Pos, 1f, 0f, Color.white, SortBands.Bursts + 1, true, true);
                    InterruptPlate(e.Pos);
                    ShakeFor("art.recoil_shot.charge_broken");
                    break;
                case SimEventType.Explosion: if (!StyleFxRules.IsStyleExplosion(e)) OnExplosion(e); break; // 유형 폭발(B = 1)은 StyleFxLayer가 그린다
                case SimEventType.ChainLightningArc: if (!_styleChains.Contains(e.EntityId)) OnChainArc(e); break;
                case SimEventType.ProjectileSpawned:
                    // 사슬 번개 유형(block.core.chain_bolt)의 선은 StyleFxLayer가 전용 그림(chain_link)으로 그린다
                    if (IsStyleChain(w, e.Id)) _styleChains.Add(e.EntityId);
                    break;
                case SimEventType.ProjectileExpired: _styleChains.Remove(e.EntityId); break;
                case SimEventType.EnemyKilled:
                    _kills.Add(new PendingKill { E = e });
                    // 0.3초 안 5마리 이상 처치 → 흔들림 소(enemy.multi_kill)
                    if (e.B != 2 && _killBurst.Kill(e.Tick) == KillBurstCounter.Result.Merged) ShakeFor("enemy.multi_kill");
                    break;
                case SimEventType.ShotFired: if (e.B != 0) OnCritShot(w, e); break;
                case SimEventType.HeatLockEntered: OnLockSteam(w, e); break;
                case SimEventType.GoldGained: OnGold(e); break;
                case SimEventType.ArtFired: OnArtFired(w, e); break;
                case SimEventType.ArtLine: OnArtLine(w, e); break;
                case SimEventType.ArtPulse: OnArtPulse(w, e); break;
                case SimEventType.PlayerMovedByArt: OnMovedByArt(w, e); break;
                case SimEventType.ArtFizzled: OnFizzled(w, e); break;
                case SimEventType.DebtAdded: OnDebt(w, e); break;
                case SimEventType.BoiloverTriggered: OnBoilover(w, e); break;
                case SimEventType.LightningRodStrike: OnLightningRod(w, e); break;
                case SimEventType.UnstableCoreWarn: OnUnstableWarn(w, e); break;
                case SimEventType.UnstableCoreExploded: OnUnstableExploded(e); break;
                case SimEventType.ThinIceFroze: OnThinIce(w, e); break;
                case SimEventType.GrazeAwarded: OnGraze(w, e); break;
                case SimEventType.PlayerHit: OnPlayerHit(e); break;
                case SimEventType.EnemyShotsCleared: OnShotsCleared(w, e); break;
                case SimEventType.BossPhaseChanged: ShakeFor("boss.phase_changed"); break;
                case SimEventType.DropSpawned: SyncDrops(w); break;
            }
        }

        // ------------------------------------------------------------ 그리기

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (_vc == null) return;
            _w = w;
            if (_s.Governor != null) _s.Governor.BeginFrame(OnScreenEnemies(w));
            _s.Advance(dt);
            if (_s.Occlusion != null) _s.Occlusion.RebuildMask(w);
            FlushKills(w);
            UpdateNumbers(dt);
            UpdateMarkers(w);
            SyncDrops(w);
            _overlay.Update(dt);
            Snapshot(w);
        }

        public void Dispose()
        {
            ClearAll();
            _overlay.Dispose();
            _s.DisposeAll();
            _s.EffectLayerAttached = false;
            _vc = null;
        }

        void ClearAll()
        {
            for (int i = _numbers.Count - 1; i >= 0; i--) KillNumber(i);
            for (int i = _markers.Count - 1; i >= 0; i--) KillMarker(i);
            foreach (var kv in _drops) ReturnDrop(kv.Value);
            _drops.Clear();
            _enemyElement.Clear();
            _recentHits.Clear();
            _bigHitUntil.Clear();
            _bloomReps.Clear();
            _shotSnapshot.Clear();
            _styleChains.Clear();
            _kills.Clear();
            if (_merger != null) _merger.Clear();
            if (_killBurst != null) _killBurst.Reset();
        }

        // ------------------------------------------------------------ 도우미

        /// <summary>카메라에 보이는 적 수(군중 감쇠의 입력). 카메라가 없으면 전체 수.</summary>
        int OnScreenEnemies(SimWorld w)
        {
            if (w == null) return 0;
            var cam = _vc != null ? _vc.Camera : null;
            if (cam == null || !cam.orthographic) return w.Enemies.Count;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector3 cp = cam.transform.position;
            int n = 0;
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var en = w.Enemies[i];
                if (en == null || en.Hp <= 0f) continue;
                Vector3 v = _vc.ToView(en.Pos);
                if (Mathf.Abs(v.x - cp.x) <= halfW && Mathf.Abs(v.y - cp.y) <= halfH) n++;
            }
            return n;
        }

        string FxEl(string elementId)
        {
            string k = ArtAliases.ElementFxKey(elementId);
            return k == null || elementId == Ids.Element.Neutral ? _t.S("fx_element_for_neutral") : k;
        }

        SpriteAnim Fx(string group, string key) => _art != null && key != null ? _art.Effect(group, key) : null;

        /// <summary>그 플레이어 캐릭터의 교체 그림(G16 character_fx, 묶음 "char.N")이 있으면 그것, 없으면 공용 그림.</summary>
        SpriteAnim CharFx(PlayerState p, string group, string key) =>
            p != null ? CharacterFx.Resolve(_art, _t, p.CharId, group, key, p.Wand != null ? p.Wand.EffectiveStage : 0) : Fx(group, key);

        PlayerState Player(SimWorld w, int slot) => w != null && slot >= 0 && slot < w.Players.Length ? w.Players[slot] : null;

        static string ElementOf(PlayerState p) => p != null && p.Def != null ? p.Def.Element : Ids.Element.Neutral;

        double Now(int tick) => tick * (double)SimConst.TickSeconds;

        void ShakeFor(string bindingKey)
        {
            string s = _s.Data.Bindings.Shake(bindingKey);
            Shake(s);
        }

        void Shake(string level)
        {
            if (string.IsNullOrEmpty(level) || level == EventBinding.ShakeNone) return;
            float px = _t.F("shake_px", level);
            // 프레임 상한: 짧은 창 안에서는 이미 보낸 것보다 센 것만(겹쳐 쌓이지 않는다), 세기 상한
            if (px > 0f && _s.Governor != null) px = _s.Governor.Shake(px, _s.Time);
            if (px > 0f && _vc.Shake != null) _vc.Shake(px, _t.F("shake_seconds"));
        }

        /// <summary>그림 정지 요청: 피격 외에는 1초에 0.1초 예산을 거친다(10 3.4절).</summary>
        void HitStop(string bindingKey, int tick, bool unbudgeted)
        {
            int ms = _s.Data.Bindings.HitstopMs(bindingKey);
            if (ms <= 0 || _vc.HitStop == null) return;
            float sec = ms / 1000f;
            // 무게 비례: 피격 = 줄이지 않음, 비기 = 군중 감쇠 없음, 치명 타격 = 붐비면 줄임. 한 번 상한.
            if (_s.Governor != null)
            {
                bool hurt = bindingKey == "player.hit";
                bool crit = bindingKey == "heat.crit_hit";
                sec = _s.Governor.HitStop(sec, hurt ? FxPriority.PlayerHurt : FxPriority.Crit, !crit);
                if (sec <= 0f) return;
            }
            if (!unbudgeted) sec = _s.HitStopBudget.Request(sec, Now(tick));
            if (sec > 0f) _vc.HitStop(sec);
        }

        bool SpawnAnim(SpriteAnim anim, Vector2 pos, float scale, float rot, Color color, int order, bool counts, bool additive = false,
            float life = 0f, bool flipX = false, float fadeOut = 0f, System.Func<Vector2?> follow = null, float scaleTo = 0f)
        {
            if (anim == null) return false;
            return _s.Sprites.Spawn(new FxSpec
            {
                Anim = anim, SimPos = pos, ScaleFrom = new Vector2(scale, scale), ScaleTo = scaleTo > 0f ? new Vector2(scaleTo, scaleTo) : Vector2.zero,
                RotationDeg = rot, Color = color, SortingOrder = order, CountsOcclusion = counts, Additive = additive, Life = life, FlipX = flipX,
                FadeOut = fadeOut, Follow = follow
            });
        }

        /// <summary>선 효과(이어 붙임 그림을 a→b로 늘인다).</summary>
        bool SpawnLine(SpriteAnim anim, Vector2 a, Vector2 b, float heightCells, Color color, float life, int order, bool counts)
        {
            if (anim == null) return false;
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len <= 0f) return false;
            var first = anim.At(0f);
            if (first == null) return false;
            float h = heightCells > 0f ? heightCells : first.rect.height / SimConst.PixelsPerUnit;
            return _s.Sprites.Spawn(new FxSpec
            {
                Anim = anim, SimPos = (a + b) * 0.5f, RotationDeg = FxUtil.ViewAngle(d), Color = color, Life = life, SortingOrder = order,
                CountsOcclusion = counts, OcclusionRadius = len * 0.5f, TiledLength = len, TiledHeight = h, FadeOut = life * 0.5f
            });
        }

        /// <summary>도형 고리(퍼지거나 오므라듦).</summary>
        bool SpawnRing(Vector2 pos, float fromRadius, float toRadius, int ringPx, Color color, float life, int order, bool counts, System.Func<Vector2?> follow = null)
        {
            // 내 이펙트 고리(7·8층)는 얇은 안티에일리어스 선(RingMeshFx: 동시 상한·합치기·예고 위 옅어짐). 나머지(스침·내 표시)는 그림 고리.
            if (counts && _s.Rings != null) return _s.Rings.Add(pos, fromRadius, toRadius, life, color, 0f, follow, ringPx * 0.5f);
            if (_art == null) return false;
            float maxR = Mathf.Max(fromRadius, toRadius);
            int rPx = Mathf.Max(1, Mathf.RoundToInt(maxR * SimConst.PixelsPerUnit));
            var spr = _art.Circle(rPx, true, Mathf.Max(1, ringPx));
            float from = maxR > 0f ? fromRadius / maxR : 1f;
            float to = maxR > 0f ? toRadius / maxR : 1f;
            return _s.Sprites.Spawn(new FxSpec
            {
                Sprite = spr, SimPos = pos, ScaleFrom = new Vector2(Mathf.Max(from, 1e-3f), Mathf.Max(from, 1e-3f)),
                ScaleTo = new Vector2(Mathf.Max(to, 1e-3f), Mathf.Max(to, 1e-3f)), Color = color, Life = life, SortingOrder = order,
                CountsOcclusion = counts, OcclusionRadius = maxR, FadeOut = life * 0.5f, Follow = follow
            });
        }

        void GroundMark(string group, string key, Vector2 pos, float scale, float rot, float life)
        {
            if (!EffectOptionRules.DrawGroundMark(_s.Options.GroundMarks)) return;
            var anim = Fx(group, key);
            if (anim == null) return;
            float maxA = _s.Data.Budget.GroundMarkMaxOpacityPercent / 100f;
            if (SpawnAnim(anim, pos, scale, rot, new Color(1f, 1f, 1f, maxA > 0f ? maxA : 1f), SortBands.FloorMarks, false, false, life, false, life * 0.5f))
                GroundMarksSpawned++;
        }

        /// <summary>"끊음!" 판(보충 그림 special/interrupt_plate) 위에 이름 표 글자를 얹어 늑대 머리 위에 띄운다(FX 20.4 ②). 값은 G16 interrupt.</summary>
        void InterruptPlate(Vector2 pos)
        {
            if (_names == null || _s.Texts == null) return;
            var plate = Fx(_t.S("interrupt", "plate_group"), _t.S("interrupt", "plate_key"));
            float life = _t.F("interrupt", "seconds");
            _s.Texts.Show(_names.Get(_t.S("interrupt", "name_key")), pos - new Vector2(0f, _t.F("interrupt", "head_offset_cells")), UiFontId.F16,
                Color.white, life, 0f, life * 0.5f, plate != null ? plate.At(0f) : null, Color.white);
        }

        Vector2 HeadOf(SimWorld w, PlayerState p, float offsetCells) => (p != null ? p.Pos : Vector2.zero) - new Vector2(0f, offsetCells);

        // ------------------------------------------------------------ 적중·피해 숫자

        void OnEnemyDamaged(SimWorld w, in SimEvent e)
        {
            string elId = e.Id ?? Ids.Element.Neutral;
            _enemyElement[e.EntityId] = elId;
            bool crit = (e.B & DamageFlags.Crit) != 0, weak = (e.B & DamageFlags.Weak) != 0, resist = (e.B & DamageFlags.Resist) != 0;
            bool armored = (e.B & DamageFlags.Armored) != 0, corroded = (e.B & DamageFlags.Corroded) != 0;
            string fxEl = FxEl(elId);
            Color elColor = _t.ElementPrimary(elId);
            Vector2 pos = e.Pos;

            // 같은 적에게 0.05초 안에 3번 넘게 맞으면 큰 불꽃 하나(입자 8)
            bool big = false, suppressed = false;
            int window = SimConst.Ticks(_s.Data.Budget.HitMergeWindowSeconds);
            List<int> hits;
            if (!_recentHits.TryGetValue(e.EntityId, out hits)) { hits = new List<int>(); _recentHits[e.EntityId] = hits; }
            for (int i = hits.Count - 1; i >= 0; i--) if (e.Tick - hits[i] > window) hits.RemoveAt(i);
            hits.Add(e.Tick);
            int until;
            if (_bigHitUntil.TryGetValue(e.EntityId, out until) && e.Tick <= until) suppressed = true;
            else if (hits.Count > _s.Data.Budget.HitMergeOverHits) { big = true; _bigHitUntil[e.EntityId] = e.Tick + window; }

            if (!suppressed)
            {
                SpriteAnim spark;
                Color tint = Color.white;
                if (big) spark = Fx("player", _t.S("hits", "big_hit_key_prefix") + fxEl);
                else if (weak) spark = Fx("player", "weakness_" + fxEl);
                else if (resist) spark = Fx("player", "resist_" + fxEl);
                else spark = CharFx(Player(w, e.Slot), "player", "hit_" + fxEl);
                if (armored) tint = _t.Col("colors", "armored");
                else if (elId == Ids.Element.Neutral) tint = elColor;
                SpawnAnim(spark, pos, 1f, UnityEngine.Random.Range(0f, 360f), tint, SortBands.Bursts, true, true);
                if (crit) SpawnAnim(Fx("player", _t.S("hits", "crit_key")), pos, 1f, 0f, Color.white, SortBands.Bursts + 1, true, true);

                // 적중 장식 입자(분류 hit)
                // 입자 수 단계는 때린 플레이어의 지팡이 단계(환경 피해면 슬롯 0).
                var p0 = Player(w, e.Slot >= 0 ? e.Slot : 0);
                var tier = _s.Data.Budget.TierFor(p0 != null ? p0.Wand.EffectiveStage : 0);
                int myBullets = w != null ? w.Projectiles.Count : 0;
                var load = _s.Data.Budget.LoadFor(myBullets);
                int n = big ? _s.Data.Budget.HitMergedParticles
                    : EffectOptionRules.HitParticles(_s.Data.Budget, tier.Hit, crit, weak, resist, load.HitDecor, _s.Options.WandFxLevel);
                Color pc = crit ? Color.white : armored ? _t.Col("colors", "armored") : weak ? _t.ElementSecondary(elId) : elColor;
                if (_s.Governor != null && !crit) n = Mathf.RoundToInt(n * _s.Governor.CountScale(FxPriority.NormalHit));
                _s.Particles.Burst(ParticleBudgetConfig.CatHit, n, pos, _t.F("hits", "particle_speed_cells"), _s.Data.Budget.LifeHit, pc,
                    _t.F("hits", "particle_px"), SortBands.Bursts, null, true);
            }

            if (crit)
            {
                // 임계 적중: 맞은 적 그림 정지 0.03초(예산 경유), 흔들림 소는 0.3초에 한 번만
                HitStop("heat.crit_hit", e.Tick, false);
                if (_critShake.TryPass(0, e.Tick)) ShakeFor("heat.crit_hit");
            }

            AddDamageNumber(e.EntityId, e.Tick, DamageNumberRules.KindOf(e.B), e.A, armored, corroded, elId, pos);
        }

        void AddDamageNumber(int enemyId, int tick, DamageNumberKind kind, int amount, bool armored, bool corroded, string elId, Vector2 pos)
        {
            if (!DamageNumberRules.Visible(_s.Options.DamageNumberMode, kind)) return;
            float sizeOpt = DamageNumberSizeScale;
            if (sizeOpt <= 0f) return; // 설정 '피해 숫자' 끄기
            // 프레임당 새 숫자 상한: 이미 떠 있는 같은 적의 숫자에 합치는 것·치명·약점·큰 피해·골드는 늘 된다
            bool important = kind != DamageNumberKind.Normal && kind != DamageNumberKind.Resist
                             || (_juice.F("damage_pop", "big_damage") > 0f && amount >= _juice.F("damage_pop", "big_damage"));
            if (!important && _s.Governor != null && !_merger.HasMergeTarget(enemyId, tick) && !_s.Governor.AllowNumber(false)) return;
            bool merged; int evicted;
            var live = _merger.Add(enemyId, tick, kind, amount, armored, corroded, elId, out merged, out evicted);
            if (evicted != 0)
                for (int i = 0; i < _numbers.Count; i++) if (_numbers[i].Handle == evicted) { KillNumber(i); break; }
            NumView v = null;
            if (merged) for (int i = 0; i < _numbers.Count; i++) if (_numbers[i].Handle == live.Handle) { v = _numbers[i]; break; }
            if (v == null) { v = new NumView { Handle = live.Handle, Pos = StackedPos(pos) }; _numbers.Add(v); }
            v.Age = 0f;
            v.Life = _t.F("damage_numbers", "life_seconds");
            v.Spec = DamageNumberRules.Build(_t, live.Kind, live.Amount, live.Armored, live.Corroded, live.ElementId);
            v.Color = ColorFor(v.Spec);
            if (_popOn)
            {
                // 튀어 오르는 숫자: 합쳐질 때마다 다시 터진다. 임계는 흰색 그대로(TV-054) 크기만, 큰 피해는 크기 + 색.
                bool crit = live.Kind == DamageNumberKind.Crit;
                bool big = live.Kind != DamageNumberKind.Gold && live.Amount >= _juice.F("damage_pop", "big_damage") && _juice.F("damage_pop", "big_damage") > 0f;
                v.Life *= _juice.F("damage_pop", "life_mul") > 0f ? _juice.F("damage_pop", "life_mul") : 1f;
                v.Pop = crit ? _juice.F("damage_pop", "crit_pop") : _juice.F("damage_pop", "pop");
                float size = _juice.F("damage_pop", "base_scale");
                if (size <= 0f) size = 1f;
                if (crit) size *= _juice.F("damage_pop", "crit_scale") > 0f ? _juice.F("damage_pop", "crit_scale") : 1f;
                if (big) size *= _juice.F("damage_pop", "big_scale") > 0f ? _juice.F("damage_pop", "big_scale") : 1f;
                v.Size = size * sizeOpt;
                if (!merged) v.Drift = UnityEngine.Random.Range(-1f, 1f) * _juice.F("damage_pop", "drift");
                if (big && !crit && live.Kind == DamageNumberKind.Normal && !live.Armored)
                    v.Color = JuiceLayer.Safe(EffectsTuning.ParseColor(_juice.S("damage_pop", "big_color"), v.Color));
            }
            if (!_popOn) v.Size = sizeOpt;
            LayoutNumber(v);
        }

        /// <summary>설정 '피해 숫자'(작게 = 기본 / 크게 / 끄기 = 0)의 크기 배율. 값은 Juice.json damage_pop.small_scale / large_scale.</summary>
        float DamageNumberSizeScale => ReadabilityOptions.DamageNumberSizeScale(_s.Options.DamageNumberSize,
            _juice.F("damage_pop", "small_scale"), _juice.F("damage_pop", "large_scale"));

        /// <summary>
        /// 숫자 겹침 방지: 막 뜬(stack_seconds 안) 다른 숫자가 stack_cells 안에 있으면 한 칸씩 위로 올린다(최대 stack_max번).
        /// 같은 적의 연속 피해는 합쳐서 하나로 남는다(DamageNumberMerger).
        /// </summary>
        Vector2 StackedPos(Vector2 pos)
        {
            float cell = _juice.F("damage_pop", "stack_cells"), young = _juice.F("damage_pop", "stack_seconds");
            int max = _juice.I("damage_pop", "stack_max");
            if (cell <= 0f || max <= 0) return pos;
            for (int step = 0; step < max; step++)
            {
                bool hit = false;
                for (int i = 0; i < _numbers.Count; i++)
                {
                    var o = _numbers[i];
                    if (o.Age > young) continue;
                    Vector2 d = o.Pos - pos;
                    if (Mathf.Abs(d.x) < cell * 1.6f && Mathf.Abs(d.y) < cell) { hit = true; break; }
                }
                if (!hit) break;
                pos.y -= cell; // 시뮬레이션 y는 아래가 + → 빼면 위로
            }
            return pos;
        }

        Color ColorFor(DamageNumberSpec s)
        {
            if (s.Armored) return _t.Col("colors", "armored");
            switch (s.Kind)
            {
                case DamageNumberKind.Weak: return _t.ElementSecondary(s.ElementId);
                case DamageNumberKind.Resist: return _t.Col("colors", "resist");
                case DamageNumberKind.Gold: return _t.Col("colors", "gold");
                default: return Color.white; // 임계도 흰색(금색 금지 — TV-054)
            }
        }

        void LayoutNumber(NumView v)
        {
            if (_art == null || _vc.Pool == null) return;
            var glyphs = Fx("status", "damage_glyphs");
            string order = _t.S("damage_numbers", "glyph_order");
            string text = v.Spec.Glyphs ?? string.Empty;
            // 글리프 수 맞추기
            while (v.Glyphs.Count > text.Length) { _vc.Pool.Return(v.Glyphs[v.Glyphs.Count - 1]); v.Glyphs.RemoveAt(v.Glyphs.Count - 1); }
            while (v.Glyphs.Count < text.Length) v.Glyphs.Add(_vc.Pool.Rent(null, FxLayers.DamageNumbers + 1));
            for (int i = 0; i < text.Length; i++)
            {
                int gi = DamageNumberRules.GlyphIndex(order, text[i]);
                Sprite s = glyphs != null && glyphs.Frames != null && gi >= 0 && gi < glyphs.Frames.Length ? glyphs.Frames[gi] : null;
                var sr = v.Glyphs[i];
                sr.sprite = s;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(SpriteShaderProps.OutlineColor, Color.black);
                mpb.SetFloat(SpriteShaderProps.OutlineSize, v.Spec.OutlinePx);
                sr.SetPropertyBlock(mpb);
            }
            if (v.Spec.Kind == DamageNumberKind.Weak && v.Spec.ElementId != null)
            {
                if (v.Icon == null) v.Icon = _vc.Pool.Rent(_art.Icon(v.Spec.ElementId, _t.I("damage_numbers", "weak_icon_px")), FxLayers.DamageNumbers + 1);
            }
            else if (v.Icon != null) { _vc.Pool.Return(v.Icon); v.Icon = null; }
            if (v.Spec.Corroded && glyphs != null && glyphs.Frames != null)
            {
                int ui = DamageNumberRules.GlyphIndex(order, DamageNumberRules.Up);
                if (v.Corroded == null && ui >= 0 && ui < glyphs.Frames.Length) v.Corroded = _vc.Pool.Rent(glyphs.Frames[ui], FxLayers.DamageNumbers + 2);
            }
            else if (v.Corroded != null) { _vc.Pool.Return(v.Corroded); v.Corroded = null; }
            if (v.Spec.Prefix != null && v.Prefix == null)
                v.Prefix = _s.Texts.Show(v.Spec.Prefix, v.Pos, UiFontId.F16, v.Color, v.Life, _t.F("damage_numbers", "rise_cells"), _t.F("damage_numbers", "fade_seconds"));
            PlaceNumber(v);
        }

        void PlaceNumber(NumView v)
        {
            float k = v.Life > 0f ? Mathf.Clamp01(v.Age / v.Life) : 1f;
            float rise = _t.F("damage_numbers", "rise_cells") * k;
            float driftX = 0f, popMul = 1f;
            if (_popOn)
            {
                // 위로 튀었다가 떨어지는 길(바닥은 처음 자리 조금 위에서 멈춘다) + 처음에 크게 터졌다 줄어든다
                float g = _juice.F("damage_pop", "gravity"), v0 = _juice.F("damage_pop", "rise_speed");
                rise = Mathf.Max(0.15f, v0 * v.Age - 0.5f * g * v.Age * v.Age);
                driftX = v.Drift * v.Age;
                float pt = _juice.F("damage_pop", "pop_seconds");
                float pk = pt > 0f ? Mathf.Clamp01(v.Age / pt) : 1f;
                float pop = v.Pop > 0f ? v.Pop : 1f;
                popMul = (1f + (pop - 1f) * (1f - pk) * (1f - pk)) * (v.Size > 0f ? v.Size : 1f);
            }
            float fade = _t.F("damage_numbers", "fade_seconds");
            float left = v.Life - v.Age;
            float a = fade > 0f && left < fade ? Mathf.Clamp01(left / fade) : 1f;
            a = EffectOptionRules.DamageNumberAlpha(a, _s.Options.DamageNumberOpacity);
            float glyphPx = _t.F("damage_numbers", "glyph_px");
            float scale = (v.Spec.Scale > 0f ? v.Spec.Scale : 1f) * (_popOn ? popMul : (v.Size > 0f ? v.Size : 1f));
            Vector3 basePos = _vc.ToView(v.Pos) + new Vector3(driftX, rise, 0f);
            int n = v.Glyphs.Count;
            int total = n + (v.Icon != null ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                var sr = v.Glyphs[i];
                float x = DamageNumberRules.GlyphOffsetPx(i, total, glyphPx, scale) / SimConst.PixelsPerUnit;
                sr.transform.localPosition = basePos + new Vector3(x, 0f, 0f);
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                var c = v.Color; c.a = a;
                sr.color = c;
            }
            if (v.Icon != null)
            {
                float x = DamageNumberRules.GlyphOffsetPx(n, total, glyphPx, scale) / SimConst.PixelsPerUnit;
                v.Icon.transform.localPosition = basePos + new Vector3(x, 0f, 0f);
                v.Icon.color = new Color(1f, 1f, 1f, a);
            }
            if (v.Corroded != null)
            {
                float cs = scale * _t.F("damage_numbers", "corroded_mark_scale");
                float x = DamageNumberRules.GlyphOffsetPx(total, total, glyphPx, scale) / SimConst.PixelsPerUnit;
                v.Corroded.transform.localPosition = basePos + new Vector3(x, glyphPx * scale * 0.5f / SimConst.PixelsPerUnit, 0f);
                v.Corroded.transform.localScale = new Vector3(cs, cs, 1f);
                var cc = _t.Col("colors", "corrosion"); cc.a = a;
                v.Corroded.color = cc;
            }
            if (v.Prefix != null && v.Prefix.Active)
            {
                float x = DamageNumberRules.GlyphOffsetPx(-1, total, glyphPx, scale) / SimConst.PixelsPerUnit;
                v.Prefix.SimPos = v.Pos + new Vector2(x + driftX, 0f);
            }
        }

        void UpdateNumbers(float dt)
        {
            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                var v = _numbers[i];
                v.Age += dt;
                if (v.Age >= v.Life) { KillNumber(i); continue; }
                PlaceNumber(v);
            }
        }

        void KillNumber(int i)
        {
            var v = _numbers[i];
            _numbers.RemoveAt(i);
            if (_merger != null) _merger.Remove(v.Handle);
            if (_vc != null && _vc.Pool != null)
            {
                for (int g = 0; g < v.Glyphs.Count; g++) _vc.Pool.Return(v.Glyphs[g]);
                if (v.Icon != null) _vc.Pool.Return(v.Icon);
                if (v.Corroded != null) _vc.Pool.Return(v.Corroded);
            }
            v.Glyphs.Clear();
        }

        void OnGold(in SimEvent e)
        {
            if (e.Pos.x < 0f || e.A <= 0) return; // 자리 없는 골드(정산·야영)는 숫자를 띄우지 않는다
            AddDamageNumber(0, e.Tick, DamageNumberKind.Gold, e.A, false, false, null, e.Pos);
        }

        // ------------------------------------------------------------ 약점·저항 처음 알아냄 (D-54)

        void OnAffinity(SimWorld w, in SimEvent e)
        {
            // 그 적 머리 위에 속성 문양 1.5초(약점 ▲ / 저항 ▼ 표식과 함께). 이벤트 하나에 표식 하나.
            var m = new AffinityMarker { EnemyId = e.EntityId, StartTick = e.Tick, Pos = e.Pos, Affinity = (Affinity)e.A };
            if (_art != null && _vc.Pool != null)
            {
                m.Icon = _vc.Pool.Rent(_art.Icon(e.Id ?? Ids.Element.Neutral, _t.I("affinity_glyph", "icon_px")), FxLayers.DamageNumbers + 3);
                var glyphs = Fx("status", "damage_glyphs");
                int gi = DamageNumberRules.GlyphIndex(_t.S("damage_numbers", "glyph_order"), m.Affinity == Affinity.Resist ? DamageNumberRules.Down : DamageNumberRules.Up);
                if (glyphs != null && glyphs.Frames != null && gi >= 0 && gi < glyphs.Frames.Length)
                    m.Mark = _vc.Pool.Rent(glyphs.Frames[gi], FxLayers.DamageNumbers + 3);
            }
            _markers.Add(m);
            PlaceMarker(w, m);
        }

        void UpdateMarkers(SimWorld w)
        {
            int life = SimConst.Ticks(_t.F("affinity_glyph", "seconds"));
            int now = w != null ? w.Tick : 0;
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                var m = _markers[i];
                if (now - m.StartTick >= life) { KillMarker(i); continue; }
                PlaceMarker(w, m);
            }
        }

        void PlaceMarker(SimWorld w, AffinityMarker m)
        {
            var en = w != null ? w.FindEnemy(m.EnemyId) : null;
            float body = en != null ? en.BodyRadius : 0f;
            if (en != null) m.Pos = en.Pos;
            Vector2 head = m.Pos - new Vector2(0f, body + _t.F("affinity_glyph", "head_offset_cells"));
            Vector3 vp = _vc.ToView(head);
            float iconCells = _t.F("affinity_glyph", "icon_px") / SimConst.PixelsPerUnit;
            if (m.Icon != null) m.Icon.transform.localPosition = vp;
            if (m.Mark != null)
            {
                m.Mark.transform.localPosition = vp + new Vector3(iconCells, 0f, 0f);
                m.Mark.color = m.Affinity == Affinity.Resist ? _t.Col("colors", "resist") : Color.white;
            }
        }

        void KillMarker(int i)
        {
            var m = _markers[i];
            _markers.RemoveAt(i);
            if (_vc != null && _vc.Pool != null)
            {
                if (m.Icon != null) _vc.Pool.Return(m.Icon);
                if (m.Mark != null) _vc.Pool.Return(m.Mark);
            }
        }

        // ------------------------------------------------------------ 폭발·연쇄

        void OnExplosion(in SimEvent e)
        {
            string elId = e.Id ?? Ids.Element.Neutral;
            float r = e.F > 0f ? e.F : _t.F("explosion", "native_radius_cells");
            float native = Mathf.Max(1e-3f, _t.F("explosion", "native_radius_cells"));
            float life = _t.F("explosion", "seconds");
            SpawnAnim(Fx("player", "explosion_" + FxEl(elId)), e.Pos, r / native, 0f, elId == Ids.Element.Neutral ? _t.ElementPrimary(elId) : Color.white,
                SortBands.Bursts, true, false, life);
            // 안쪽 채움 ≤ 30% = 국소 번쩍. 세기 × flash_scale. 화면 25% 이상이면 번쩍 관리기를 거친다(TV-184).
            float fill = EffectOptionRules.FlashAlpha(_t.F("explosion", "fill_opacity"), _s.Options.FlashScale);
            float area = Mathf.PI * r * r;
            float screen = (UiScale.RefWidth / (float)SimConst.PixelsPerUnit) * (UiScale.RefHeight / (float)SimConst.PixelsPerUnit);
            float frac = screen > 0f ? area / screen : 0f;
            if (frac >= _s.Flash.FullscreenAreaFraction)
            {
                FlashesSubmitted++;
                var res = _s.Flash.Submit(new FlashRequest { Opacity = _t.F("explosion", "fill_opacity"), OnSeconds = _s.Flash.MaxOnSeconds, OffSeconds = life, AreaFraction = frac },
                    Now(e.Tick), _s.EffectsLevel, _s.Options.FlashScale);
                if (res.Kind == FlashKind.Border) { _overlay.Border(res.BorderSeconds); fill = 0f; }
                else fill = res.Opacity;
            }
            if (fill > 0f && _art != null)
            {
                int rPx = Mathf.Max(1, Mathf.RoundToInt(r * SimConst.PixelsPerUnit));
                var c = _t.ElementPrimary(elId); c.a = fill;
                _s.Sprites.Spawn(new FxSpec
                {
                    Sprite = _art.Circle(rPx), SimPos = e.Pos, ScaleFrom = new Vector2(0.25f, 0.25f), ScaleTo = Vector2.one, Color = c, Life = life,
                    FadeOut = life, SortingOrder = SortBands.Bursts - 1, CountsOcclusion = true, OcclusionRadius = r
                });
            }
            float traceNative = 1f;
            var trace = Fx("player", "trace_" + FxEl(elId));
            if (trace != null && trace.At(0f) != null) traceNative = trace.At(0f).rect.width / SimConst.PixelsPerUnit;
            GroundMark("player", "trace_" + FxEl(elId), e.Pos, traceNative > 0f ? Mathf.Min(1f, r * 2f / traceNative) : 1f, 0f, _t.F("ground_marks", "trace_seconds"));
        }

        /// <summary>기본 공격 유형의 사슬(동작 chain)이고 전용 그림(styles.char.2/chain_link)이 있는가.</summary>
        bool IsStyleChain(SimWorld w, string blockId)
        {
            if (blockId == null || w == null || w.Content == null || _art == null) return false;
            var b = w.Content.Block(blockId);
            return b != null && b.Style != null && b.Style.Kind == AttackBehavior.Chain && _art.EffectInfo(StyleFxLayer.Group(2), "chain_link") != null;
        }

        void OnChainArc(in SimEvent e)
        {
            var anim = Fx("player", _t.S("chain_arc", "key"));
            var first = anim != null ? anim.At(0f) : null;
            float h = first != null ? first.rect.height / SimConst.PixelsPerUnit : 0f;
            float k = Mathf.Max(_t.F("chain_arc", "min_width"), 1f - e.A * _t.F("chain_arc", "thin_per_hop"));
            SpawnLine(anim, e.Pos, e.Pos2, h * k, Color.white, _t.F("chain_arc", "seconds"), SortBands.Bursts, true);
            SpawnAnim(Fx("player", "hit_lightning"), e.Pos2, 1f, 0f, Color.white, SortBands.Bursts, true, true);
        }

        // ------------------------------------------------------------ 처치

        void FlushKills(SimWorld w)
        {
            if (_kills.Count == 0) return;
            // 한 프레임에 5마리 넘게 죽으면 조각 절반(10 3.3절)
            bool halve = _kills.Count > _s.Data.Budget.KillHalveOverPerFrame && _s.Data.Budget.KillHalveOverPerFrame > 0;
            for (int i = 0; i < _kills.Count; i++) KillFx(w, _kills[i].E, halve);
            _kills.Clear();
        }

        void KillFx(SimWorld w, SimEvent e, bool halve)
        {
            // 사망 → 연기 → 조각 4~6개. 처치 조각 옵션 smoke_only면 연기만.
            if (SpawnAnim(Fx("enemy", _t.S("kill", "smoke_key")), e.Pos, 1f, 0f, Color.white, SortBands.Bursts, true)) KillSmokesSpawned++;
            string elId;
            if (!_enemyElement.TryGetValue(e.EntityId, out elId)) elId = Ids.Element.Neutral;
            _enemyElement.Remove(e.EntityId);
            _recentHits.Remove(e.EntityId);
            _bigHitUntil.Remove(e.EntityId);
            int want = UnityEngine.Random.Range(_t.I("kill", "shards_min"), _t.I("kill", "shards_max") + 1);
            if (halve) want = (want + 1) / 2;
            int shards = EffectOptionRules.KillShardCount(_s.Options.KillShards, want);
            bool critKill = false; // 임계 처치 조각 = 흰 + 검정 테 — 처치 이벤트에는 임계가 없어 마지막 적중을 본다
            var en = w != null ? w.FindEnemy(e.EntityId) : null;
            if (en != null) critKill = en.KillingHit.IsCrit;
            Color sc = critKill ? Color.white : _t.ElementPrimary(elId);
            float life = _t.F("kill", "shard_seconds");
            if (shards > 0)
                KillShardsSpawned += _s.Particles.Burst(ParticleBudgetConfig.CatKill, shards, e.Pos, _t.F("hits", "particle_speed_cells"), life, sc,
                    _t.F("kill", "shard_px"), SortBands.Bursts, _art != null ? _art.WhitePixel : null, false, critKill ? (Color?)Color.black : null, critKill ? 1f : 0f);
            // 골드 조각(연출)
            if (e.A > 0 && shards > 0)
            {
                var coin = Fx("loot", _t.S("kill", "gold_coin_key"));
                var cs = coin != null ? coin.At(0f) : null;
                int coins = _t.I("kill", "gold_coins");
                if (halve) coins = (coins + 1) / 2;
                _s.Particles.Burst(ParticleBudgetConfig.CatKill, coins, e.Pos, _t.F("hits", "particle_speed_cells"), life, Color.white,
                    cs != null ? cs.rect.width : _t.F("kill", "shard_px"), SortBands.Bursts, cs);
            }
            // 엘리트·보스 처치: 금색 선 고리
            if (e.B == 1 || e.B == 2)
                SpawnRing(e.Pos, 0f, _t.F("kill", "elite_ring_radius_cells"), 2, _t.Col("colors", "gold"), _t.F("kill", "elite_ring_seconds"), SortBands.Bursts, true);
        }

        // ------------------------------------------------------------ 비기

        static int ArtIndex(string artId)
        {
            for (int i = 0; i < Ids.Art.All.Length; i++) if (Ids.Art.All[i] == artId) return i;
            return -1;
        }

        void OnArtFired(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            if (p == null) return;
            int slot = e.Slot >= 0 && e.Slot < _artTick.Length ? e.Slot : 0;
            _artTick[slot] = e.Tick;
            _artPos[slot] = p.Pos;
            if (e.Id == Ids.Art.Bloom) _bloomReps[e.Slot] = e.F;
            string el = ElementOf(p);
            string fxEl = FxEl(el);
            Color main = _t.ElementPrimary(el);
            var def = w != null && w.Content != null ? w.Content.Art(e.Id) : null;
            int difficulty = def != null ? def.Difficulty : 0;
            int idx = ArtIndex(e.Id);

            // 발밑 기술 문양 고리(속성색, 0.2초)
            if (idx >= 0)
            {
                string seal = "cast_seal_" + (idx + 1).ToString("00", CultureInfo.InvariantCulture);
                SpawnAnim(CharFx(p, "special", seal), p.Pos, 1f, 0f, main, SortBands.PlayerZones + 5, false, false, _t.F("art", "glyph_ring_seconds"), false,
                    _t.F("art", "glyph_ring_seconds") * 0.5f);
            }
            if (_debtTick[slot] == e.Tick) DebtRing(p.Pos);

            // 기술 이름(옵션: on / difficulty5_only / off). 난이도 5는 큰 띠(속성 주색).
            int large = _t.I("art", "name_large_difficulty");
            if (EffectOptionRules.ShowArtName(_s.Options.ArtNamePopup, difficulty, large) && _names != null && e.Id != null)
            {
                Sprite ribbon = null;
                if (difficulty >= large)
                {
                    var rb = Fx("special", _t.S("art", "name_ribbon_large_key"));
                    ribbon = rb != null ? rb.At(0f) : null;
                }
                int s2 = slot;
                var shown = _s.Texts.Show(_names.Get(e.Id), HeadOf(w, p, _t.F("art", "name_head_offset_cells")),
                    difficulty >= large ? UiFontId.F24 : UiFontId.F16, Color.white, _t.F("art", "name_seconds"), 0f, _t.F("art", "name_seconds") * 0.5f,
                    ribbon, main, () => { var pp = Player(_w, s2); return pp != null ? HeadOf(_w, pp, _t.F("art", "name_head_offset_cells")) : (Vector2?)null; });
                if (shown != null) NamePopupsShown++;
            }

            // 난이도별 화면 효과: 바인딩 줄의 흔들림이 우선, 없으면 난이도 표(1~2 없음 / 3 소 / 5 중). 그림 정지는 바인딩(뇌광창 0.08초).
            string fireKey;
            var fireMap = _t.StringMap("sound", "art_fire");
            fireMap.TryGetValue(e.Id ?? string.Empty, out fireKey);
            string shake = fireKey != null ? _s.Data.Bindings.Shake(fireKey) : EventBinding.ShakeNone;
            if (shake == EventBinding.ShakeNone || shake == null) shake = _t.S("art", "difficulty_shake", difficulty.ToString(CultureInfo.InvariantCulture));
            if (e.Id != Ids.Art.Bloom) Shake(shake); // 만개는 파동마다(ArtPulse)
            if (fireKey != null) HitStop(fireKey, e.Tick, false);

            // 주 효과(비기별 그림 — G16.json art.fx)
            Vector2 facing = Dir8Util.ToSimVector(p.Facing);
            float rot = FxUtil.ViewAngle(facing);
            Vector2 tip = p.Pos + facing * _t.F("art", "glyph_ring_native_radius_cells");
            string cast = _t.S("art", "fx", e.Id ?? string.Empty, "cast");
            if (cast != null)
            {
                var anim = Fx("special", FxUtil.WithElement(cast, fxEl));
                if (_t.B("art", "fx", e.Id, "mirror"))
                {
                    Vector2 side = new Vector2(-facing.y, facing.x);
                    SpawnAnim(anim, p.Pos + side * _t.F("art", "glyph_ring_native_radius_cells"), 1f, rot + 90f, Color.white, SortBands.Bursts, true);
                    SpawnAnim(anim, p.Pos - side * _t.F("art", "glyph_ring_native_radius_cells"), 1f, rot - 90f, Color.white, SortBands.Bursts, true, false, 0f, true);
                }
                else SpawnAnim(anim, tip, 1f, rot, Color.white, SortBands.Bursts, true);
            }
            string extra = _t.S("art", "fx", e.Id ?? string.Empty, "extra");
            if (extra != null) SpawnAnim(Fx("special", extra), p.Pos, 1f, rot + 180f, Color.white, SortBands.Bursts, true);
            string just = _t.S("art", "fx", e.Id ?? string.Empty, "just");
            if (just != null && e.B != 0) SpawnAnim(Fx("special", just), tip, 1f, 0f, Color.white, SortBands.Bursts + 2, false);
        }

        void DebtRing(Vector2 pos)
        {
            // 외상으로 나간 그믐 비기: 발동 문양 고리에 보라 테 한 겹 더(10 3.6절)
            float r = _t.F("art", "glyph_ring_native_radius_cells");
            SpawnRing(pos, r, r, 2, _t.Col("colors", "debt_ring"), _t.F("art", "glyph_ring_seconds"), SortBands.PlayerZones + 6, false);
        }

        void OnDebt(SimWorld w, in SimEvent e)
        {
            int slot = e.Slot >= 0 && e.Slot < _debtTick.Length ? e.Slot : 0;
            _debtTick[slot] = e.Tick;
            if (_artTick[slot] == e.Tick) DebtRing(_artPos[slot]);
        }

        void OnArtLine(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            string fxEl = FxEl(ElementOf(p));
            int slot = e.Slot >= 0 && e.Slot < _lineTick.Length ? e.Slot : 0;
            _lineTick[slot] = e.Tick;
            string line = _t.S("art", "fx", e.Id ?? string.Empty, "line");
            float life = _t.F("art", "line_seconds");
            if (line != null)
            {
                var anim = Fx("special", FxUtil.WithElement(line, fxEl));
                SpawnLine(anim, e.Pos, e.Pos2, e.F > 0f ? e.F : 0f, Color.white, life, SortBands.Bursts, true);
            }
            // 뇌광창 가운데 흰 심(보충 그림 special/thunderlance_core 64×16, 속성 없이 한 벌) — 몸통 위에 겹친다(FX 20.4 ⑨).
            string core = _t.S("art", "fx", e.Id ?? string.Empty, "core");
            if (core != null) SpawnLine(Fx("special", core), e.Pos, e.Pos2, e.F > 0f ? e.F : 0f, Color.white, life, SortBands.Bursts + 1, false);
            string tip = _t.S("art", "fx", e.Id ?? string.Empty, "tip");
            if (tip != null) SpawnAnim(Fx("special", FxUtil.WithElement(tip, fxEl)), e.Pos2, 1f, FxUtil.ViewAngle(e.Pos2 - e.Pos), Color.white, SortBands.Bursts + 1, true, false, life);
            if (_t.B("art", "fx", e.Id ?? string.Empty, "scratch"))
            {
                // ⑥ 바닥 긁힘 0.5초 — 보충 그림 special/spear_scratch(묶음·열쇠·배율은 G16 ground_marks)
                Vector2 d = e.Pos2 - e.Pos;
                GroundMark(_t.S("ground_marks", "scratch_group") ?? "enemy", _t.S("ground_marks", "scratch_key"), (e.Pos + e.Pos2) * 0.5f, _t.F("ground_marks", "scratch_mark_scale"), FxUtil.ViewAngle(d),
                    _t.F("ground_marks", "scratch_seconds"));
            }
        }

        void OnArtPulse(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            string el = ElementOf(p);
            string fxEl = FxEl(el);
            float life = _t.F("art", "pulse_seconds");
            float r = e.F;
            string pulse = _t.S("art", "fx", e.Id ?? string.Empty, "pulse");
            if (pulse != null)
            {
                var anim = Fx("special", FxUtil.WithElement(pulse, fxEl));
                var first = anim != null ? anim.At(0f) : null;
                float s = r > 0f && first != null ? FxUtil.FitScale(first, r * 2f) : 1f;
                SpawnAnim(anim, e.Pos, s, 0f, Color.white, SortBands.Bursts, true, false, life);
            }
            if (_t.B("art", "fx", e.Id ?? string.Empty, "pulse_ring") && r > 0f)
                SpawnRing(e.Pos, r * 0.25f, r, 2, _t.ElementPrimary(el), life, SortBands.Bursts, true);
            string petal = _t.S("art", "fx", e.Id ?? string.Empty, "petal");
            if (petal != null)
            {
                var pa = Fx("special", FxUtil.WithElement(petal, fxEl));
                var ps = pa != null ? pa.At(0f) : null;
                int n = _t.I("art", "bloom_petals");
                for (int i = 0; i < n; i++)
                {
                    float ang = i * Mathf.PI * 2f / Mathf.Max(1, n);
                    Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    _s.Particles.Spawn(ParticleBudgetConfig.CatArt, e.Pos, dir * (r > 0f ? r / life : _t.F("hits", "particle_speed_cells")), life, Color.white,
                        ps != null ? ps.rect.width : _t.F("hits", "particle_px"), SortBands.Bursts, ps);
                }
            }
            // 그림자 손: 범위 안 적마다 팔을 뻗는다(8개까지) + 가운데 주먹
            string hand = _t.S("art", "fx", e.Id ?? string.Empty, "hand");
            if (hand != null && w != null)
            {
                var arm = Fx("special", "shadow_arm");
                var handAnim = Fx("special", hand);
                int count = 0;
                for (int i = 0; i < w.Enemies.Count && count < _t.I("art", "bloom_petals"); i++)
                {
                    var en = w.Enemies[i];
                    if (!en.Targetable || (en.Pos - e.Pos).magnitude > r) continue;
                    SpawnLine(arm, e.Pos, en.Pos, 0f, Color.white, life, SortBands.Bursts, true);
                    SpawnAnim(handAnim, en.Pos, 1f, 0f, Color.white, SortBands.Bursts + 1, true, false, life);
                    count++;
                }
                string fist = _t.S("art", "fx", e.Id ?? string.Empty, "fist");
                if (fist != null) SpawnAnim(Fx("special", fist), e.Pos, 1f, 0f, Color.white, SortBands.Bursts + 2, true);
            }
            if (e.Id == Ids.Art.Kiln)
            {
                // 불가마 안의 적 0.5초마다 작은 불꽃
                _s.Particles.Burst(ParticleBudgetConfig.CatArt, 2, e.Pos, _t.F("hits", "particle_speed_cells") * 0.5f, life, _t.ElementSecondary(Ids.Element.Fire),
                    _t.F("hits", "particle_px"), SortBands.Bursts, null, true);
            }
            if (e.Id == Ids.Art.Bloom)
            {
                bool last = IsLastBloomWave(w, e);
                ShakeFor(last ? "art.full_bloom.final_wave" : "art.full_bloom.wave");
                if (last)
                {
                    // 마지막 파동: 번쩍 1회(속성 보조색 20%) — 번쩍 관리기 경유. 탄 소거 고리.
                    FlashesSubmitted++;
                    var res = _s.Flash.Submit(new FlashRequest
                    {
                        Opacity = _t.F("art", "bloom_final_flash_opacity"), OnSeconds = _s.Flash.MaxOnSeconds, OffSeconds = _s.Flash.MinOffSeconds, AreaFraction = 1f
                    }, Now(e.Tick), _s.EffectsLevel, _s.Options.FlashScale);
                    _overlay.Flash(_t.ElementSecondary(el), res);
                    if (r > 0f) SpawnRing(e.Pos, r * 0.5f, r, 2, Color.white, life, SortBands.Bursts, true);
                }
            }
        }

        readonly Dictionary<int, float> _bloomReps = new Dictionary<int, float>();

        bool IsLastBloomWave(SimWorld w, in SimEvent e)
        {
            float reps;
            return _bloomReps.TryGetValue(e.Slot, out reps) && reps > 0f && e.A >= Mathf.RoundToInt(reps);
        }

        void OnMovedByArt(SimWorld w, in SimEvent e)
        {
            string move = _t.S("art", "fx", e.Id ?? string.Empty, "move");
            if (move != null)
            {
                var dust = Fx("special", move);
                SpawnAnim(dust, e.Pos, 1f, 0f, Color.white, SortBands.Bursts, true);
                SpawnAnim(dust, e.Pos2, 1f, 0f, Color.white, SortBands.Bursts, true);
            }
            if (e.Id == Ids.Art.BoltDash)
            {
                int slot = e.Slot >= 0 && e.Slot < _lineTick.Length ? e.Slot : 0;
                if (_lineTick[slot] != e.Tick)
                {
                    var p = Player(w, e.Slot);
                    SpawnLine(CharFx(p, "special", _t.S("art", "fx", Ids.Art.BoltDash, "line")), e.Pos, e.Pos2, 0f, Color.white, _t.F("art", "line_seconds"), SortBands.Bursts, true);
                    // 제자 2 교체 묶음: 질주 잔광(선)과 도착 번쩍임 — 그 캐릭터 묶음에 있을 때만(G16 character_fx.<char>.bolt_dash)
                    string cid = p != null ? p.CharId : null;
                    var glow = CharacterFx.Own(_art, cid, _t.S(CharacterFx.Section, cid ?? string.Empty, "bolt_dash", "afterglow"));
                    if (glow != null)
                        SpawnLine(glow, e.Pos, e.Pos2, 0f, Color.white, _t.F(CharacterFx.Section, cid, "bolt_dash", "afterglow_seconds"), SortBands.Bursts - 1, false);
                    var arrive = CharacterFx.Own(_art, cid, _t.S(CharacterFx.Section, cid ?? string.Empty, "bolt_dash", "arrival"));
                    if (arrive != null) SpawnAnim(arrive, e.Pos2, 1f, 0f, Color.white, SortBands.Bursts + 1, true, true);
                }
            }
        }

        void OnFizzled(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            if (p == null) return;
            // 지팡이 연기 한 점
            _s.Particles.Spawn(ParticleBudgetConfig.CatArt, p.Pos, new Vector2(0f, -_t.F("hits", "particle_speed_cells") * 0.25f), _t.F("art", "pulse_seconds"),
                _t.Col("colors", "smoke"), _t.F("kill", "shard_px"), SortBands.Bursts);
            // 간격 초과: 머리 위 "n번째 입력이 s초 늦음"
            if (e.F > 0f && _names != null)
            {
                string text = _names.Format(_t.S("art", "late_text_key"), "n", e.B, "sec", e.F.ToString("0.00", CultureInfo.InvariantCulture));
                _s.Texts.Show(text, HeadOf(w, p, _t.F("art", "name_head_offset_cells")), UiFontId.F16, Color.white, _t.F("art", "name_seconds") * 2f, 0f,
                    _t.F("art", "name_seconds"));
            }
        }

        // ------------------------------------------------------------ 임계 사격·잠금

        void OnCritShot(SimWorld w, in SimEvent e)
        {
            // 임계 사격: 지팡이 끝 흰 고리 + 검정 테(critical_muzzle 32×32 3프레임). 금색·빨강 금지.
            var p = Player(w, e.Slot);
            if (p == null) return;
            Vector2 f = Dir8Util.ToSimVector(p.Facing);
            var anim = Fx("player", "critical_muzzle");
            if (anim == null) return;
            _s.Sprites.Spawn(new FxSpec
            {
                Anim = anim, SimPos = p.Pos + f * _t.F("art", "glyph_ring_native_radius_cells"), Color = Color.white, SortingOrder = SortBands.Bursts,
                CountsOcclusion = true, Additive = true, OutlineColor = Color.black, OutlinePx = 1f
            });
        }

        void OnLockSteam(SimWorld w, in SimEvent e)
        {
            // 잠금에 들어가는 발사: 지팡이에서 김 터짐(10 3.4절)
            var p = Player(w, e.Slot);
            if (p == null) return;
            SpawnAnim(Fx("player", "heat_lock_steam"), p.Pos, 1f, 0f, Color.white, SortBands.Bursts, true);
        }

        // ------------------------------------------------------------ 패시브·변이

        void OnBoilover(SimWorld w, in SimEvent e)
        {
            // 끊어진 호 6조각이 반경 2.5칸까지(닫힌 원 금지) — 그림 overflow_steam_arc 320×320(제자 1 교체본이 있으면 그것)
            var anim = CharFx(Player(w, e.Slot), "player", _t.S("boilover", "key"));
            float native = Mathf.Max(1e-3f, _t.F("boilover", "native_radius_cells"));
            SpawnAnim(anim, e.Pos, _t.F("boilover", "native_radius_cells") / native, 0f, Color.white, SortBands.Bursts, true, false, _t.F("boilover", "seconds"));
        }

        void OnLightningRod(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            SpawnAnim(CharFx(p, "player", _t.S("lightning_rod", "key")), e.Pos, 1f, 0f, Color.white, SortBands.Bursts, true);
            SpawnAnim(CharFx(p, "player", "hit_lightning"), e.Pos, 1f, 0f, Color.white, SortBands.Bursts, true, true);
        }

        void OnUnstableWarn(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            if (p == null) return;
            int s2 = e.Slot;
            SpawnAnim(Fx("player", _t.S("unstable_core", "warn_key")), HeadOf(w, p, _t.F("art", "name_head_offset_cells")), 1f, 0f, Color.white,
                SortBands.PlayerMarks, false, false, _t.F("art", "pulse_seconds"), false, 0f,
                () => { var pp = Player(_w, s2); return pp != null ? HeadOf(_w, pp, _t.F("art", "name_head_offset_cells")) : (Vector2?)null; });
        }

        void OnUnstableExploded(in SimEvent e)
        {
            // 내 자리 연두 테두리 폭발(10 3.7절) + 잠금 김
            float r = _t.F("unstable_core", "explode_radius_cells");
            SpawnRing(e.Pos, r * 0.25f, r, 2, _t.Col("colors", "mutation_lime"), _t.F("explosion", "seconds"), SortBands.Bursts, true);
            SpawnAnim(Fx("player", "heat_lock_steam"), e.Pos, 1f, 0f, Color.white, SortBands.Bursts, true);
        }

        void OnThinIce(SimWorld w, in SimEvent e)
        {
            var en = w != null ? w.FindEnemy(e.EntityId) : null;
            Vector2 pos = en != null ? en.Pos : e.Pos;
            var fp = Fx("status", "frost_particle");
            var fs = fp != null ? fp.At(0f) : null;
            _s.Particles.Burst(ParticleBudgetConfig.CatHit, _s.Data.Budget.HitMergedParticles, pos, _t.F("hits", "particle_speed_cells"), _s.Data.Budget.LifeHit,
                _t.ElementSecondary(Ids.Element.Ice), fs != null ? fs.rect.width : _t.F("hits", "particle_px"), SortBands.Bursts, fs);
        }

        // ------------------------------------------------------------ 스침·피격

        void OnGraze(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            if (p == null) return;
            // 감지 반지름에서 흰 고리가 캐릭터 쪽으로 오므라듦(0.12초). 스친 자리에 흰 테 한 번. 흔들림·그림 정지 금지.
            float gr = p.Def != null ? p.Def.GrazeRadius : _t.F("graze", "ring_start_scale");
            int s2 = e.Slot;
            SpawnRing(p.Pos, gr, 0f, 2, Color.white, _t.F("graze", "ring_seconds"), SortBands.PlayerMarks, false,
                () => { var pp = Player(_w, s2); return pp != null ? pp.Pos : (Vector2?)null; });
            SpawnRing(e.Pos, gr * 0.25f, gr * 0.25f, 2, Color.white, _t.F("graze", "ring_seconds"), SortBands.Bursts, false);
        }

        void OnPlayerHit(in SimEvent e)
        {
            // 피격: 그림 정지 0.12초(예산 밖 — 줄일 수 없다), 흔들림 중(2칸 피해는 대), 화면 가장자리 위험색 0.3초.
            HitStop("player.hit", e.Tick, true);
            int cells = e.A / SimConst.HpPerCell;
            Shake(cells >= _t.I("player_hit", "large_shake_min_cells") ? "large" : _s.Data.Bindings.Shake("player.hit"));
            _overlay.HitEdge(_t.Col("colors", "danger_edge"), _t.F("player_hit", "edge_opacity"), _t.F("player_hit", "edge_seconds"));
        }

        void OnShotsCleared(SimWorld w, in SimEvent e)
        {
            // 지운 적 탄은 김으로(30개까지): 지난 프레임에 본 자리 중 이제 없는 것
            var steam = Fx("special", _t.S("shots_cleared", "steam_key"));
            int max = _t.I("shots_cleared", "max_steam");
            int n = 0;
            _tmp.Clear();
            foreach (var kv in _shotSnapshot)
            {
                if (n >= max) break;
                bool alive = false;
                if (w != null) for (int i = 0; i < w.EnemyShots.Count; i++) if (w.EnemyShots[i].Id == kv.Key && !w.EnemyShots[i].Dead) { alive = true; break; }
                if (alive) continue;
                if (e.F > 0f && (kv.Value - e.Pos).magnitude > e.F) continue;
                SpawnAnim(steam, kv.Value, _t.F("ground_marks", "scratch_scale"), 0f, Color.white, SortBands.Bursts, true);
                _tmp.Add(kv.Key);
                n++;
            }
            for (int i = 0; i < _tmp.Count; i++) _shotSnapshot.Remove(_tmp[i]);
        }

        void Snapshot(SimWorld w)
        {
            _shotSnapshot.Clear();
            if (w == null) return;
            for (int i = 0; i < w.EnemyShots.Count; i++)
            {
                var s = w.EnemyShots[i];
                if (!s.Dead) _shotSnapshot[s.Id] = s.Pos;
            }
        }

        // ------------------------------------------------------------ 드랍 (상태로 그린다)

        void SyncDrops(SimWorld w)
        {
            if (w == null || _vc == null || _vc.Pool == null) return;
            foreach (var kv in _drops) kv.Value.Seen = false;
            for (int i = 0; i < w.Drops.Count; i++)
            {
                var d = w.Drops[i];
                if (d.Picked) continue;
                DropView v;
                if (!_drops.TryGetValue(d.Id, out v)) { v = CreateDrop(w, d); _drops[d.Id] = v; }
                v.Seen = true;
                UpdateDrop(w, d, v);
            }
            _tmp.Clear();
            foreach (var kv in _drops) if (!kv.Value.Seen) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) { ReturnDrop(_drops[_tmp[i]]); _drops.Remove(_tmp[i]); }
        }

        DropView CreateDrop(SimWorld w, DropEntity d)
        {
            var v = new DropView { Kind = d.Kind, SpawnTick = d.SpawnTick };
            string key = d.Kind == DropKind.HealthShard ? _t.S("drops", "health_key") : d.Kind == DropKind.MaterialBundle ? _t.S("drops", "material_key") : _t.S("drops", "gold_key");
            v.Anim = Fx("loot", key);
            v.Main = _vc.Pool.Rent(v.Anim != null ? v.Anim.At(0f) : (_art != null ? _art.Placeholder(SimConst.PixelsPerUnit / 2, SimConst.PixelsPerUnit / 2) : null),
                SortBands.Bodies + SortBands.YOrder(d.Pos.y));
            // 재료 꾸러미는 빛기둥 1.0초(나온 때부터 — 이미 지난 꾸러미는 다시 세우지 않는다)
            if (d.Kind == DropKind.MaterialBundle && (w.Tick - d.SpawnTick) < SimConst.Ticks(_t.F("drops", "pillar_seconds")))
            {
                v.PillarAnim = Fx("loot", _t.S("drops", "pillar_key"));
                v.Pillar = _vc.Pool.Rent(v.PillarAnim != null ? v.PillarAnim.At(0f) : null, SortBands.Bodies + SortBands.YOrder(d.Pos.y) + 1);
            }
            return v;
        }

        void UpdateDrop(SimWorld w, DropEntity d, DropView v)
        {
            float age = (w.Tick - d.SpawnTick) * SimConst.TickSeconds;
            Vector3 vp = _vc.ToView(d.Pos);
            if (v.Main != null)
            {
                if (v.Anim != null) { var s = v.Anim.At(age); if (s != null) v.Main.sprite = s; }
                v.Main.transform.localPosition = vp;
            }
            if (v.Pillar != null)
            {
                float life = _t.F("drops", "pillar_seconds");
                if (age >= life) { _vc.Pool.Return(v.Pillar); v.Pillar = null; }
                else
                {
                    if (v.PillarAnim != null && v.PillarAnim.Frames != null && v.PillarAnim.Frames.Length > 0)
                        v.Pillar.sprite = v.PillarAnim.Frames[Mathf.Abs((int)(age * v.PillarAnim.Fps)) % v.PillarAnim.Frames.Length];
                    var ps = v.Pillar.sprite;
                    float up = ps != null ? ps.rect.height / SimConst.PixelsPerUnit * 0.5f : 0f;
                    v.Pillar.transform.localPosition = vp + new Vector3(0f, up, 0f);
                    v.Pillar.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - age / life));
                }
            }
        }

        void ReturnDrop(DropView v)
        {
            if (_vc == null || _vc.Pool == null) return;
            if (v.Main != null) _vc.Pool.Return(v.Main);
            if (v.Pillar != null) _vc.Pool.Return(v.Pillar);
            v.Main = v.Pillar = null;
        }
    }
}
