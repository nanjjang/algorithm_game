using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 내 탄 층(AddShots, 00 문서 7절): w.Projectiles를 id로 따라 그린다 — 블록별 탄 그림(09 8절), 방향 회전, 크기 변형 배율,
    /// 꼬리(bullet_trail), 깊이 각주 점(제자 3 지팡이 특성, 02 8.3절), 임계 탄 이중 테(흰 + 검정, 금색·빨강 금지), 비기 탄 흰 심,
    /// 서리·잠식 표식, 밤 빛무리(10 3.9절), 내 탄 투명도(my_bullet_opacity) → 누적 가림 자리 상한.
    /// 플레이어 지대(w.PlayerZones): 불가마 원(3층), 사본 마법진(3층), 얼음 병풍(6층 몸 띠) — 투명도 × clone_zone_opacity.
    /// 상태만 보고 그리므로 Build에서 이미 날고 있는 탄·깔린 지대를 그대로 집어 든다.
    /// </summary>
    public sealed class ShotLayer : IWorldViewLayer
    {
        sealed class ShotView
        {
            public SpriteRenderer Body, Trail, Glow, RimIn, RimOut, Chevron, Core;
            public readonly SpriteRenderer[] Dots = new SpriteRenderer[8];
            public int DotCount;
            public SpriteAnim Anim;
            public float ShedTimer;
            public bool Seen;
        }

        sealed class ZoneView
        {
            public PlayerZoneKind Kind;
            public SpriteRenderer Main, Fill;
            public SpriteAnim Anim;
            public Vector2 Pos;
            public Dir8 Facing;
            public float Radius, Width;
            public int StartTick, UntilTick, OwnerSlot;
            public float EmberTimer;
            public bool Seen;
        }

        readonly EffectsShared _s;
        readonly Dictionary<int, ShotView> _shots = new Dictionary<int, ShotView>();
        readonly Dictionary<int, ZoneView> _zones = new Dictionary<int, ZoneView>();
        readonly List<int> _drop = new List<int>();
        ViewContext _vc;
        EffectsTuning _t;
        IArtLibrary _art;

        public ShotLayer(EffectsShared shared) { _s = shared ?? new EffectsShared(); }

        public EffectsShared Shared => _s;
        public int ShotViewCount => _shots.Count;
        public int ZoneViewCount => _zones.Count;
        public bool HasShotView(int projectileId) => _shots.ContainsKey(projectileId);
        public bool HasZoneView(int zoneId) => _zones.ContainsKey(zoneId);

        /// <summary>테스트용: 탄 몸의 지금 불투명도(없으면 −1).</summary>
        public float BodyAlpha(int projectileId)
        {
            ShotView v;
            return _shots.TryGetValue(projectileId, out v) && v.Body != null ? v.Body.color.a : -1f;
        }

        /// <summary>테스트용: 지대 그림의 지금 불투명도(없으면 −1).</summary>
        public float ZoneAlpha(int zoneId)
        {
            ZoneView v;
            return _zones.TryGetValue(zoneId, out v) && v.Main != null ? v.Main.color.a : -1f;
        }

        /// <summary>테스트용: 꼬리 길이(칸, 없으면 0).</summary>
        public float TrailLengthCells(int projectileId)
        {
            ShotView v;
            if (!_shots.TryGetValue(projectileId, out v) || v.Trail == null || !v.Trail.enabled || v.Trail.sprite == null) return 0f;
            return v.Trail.sprite.rect.width / SimConst.PixelsPerUnit * v.Trail.transform.localScale.x;
        }

        public int DepthDotCount(int projectileId)
        {
            ShotView v;
            return _shots.TryGetValue(projectileId, out v) ? v.DotCount : 0;
        }

        public bool HasCritRim(int projectileId)
        {
            ShotView v;
            return _shots.TryGetValue(projectileId, out v) && v.RimIn != null && v.RimOut != null;
        }

        public void Build(ViewContext vc, SimWorld w)
        {
            ClearViews();
            _vc = vc;
            _s.Ensure(vc, w);
            _t = _s.Data.Tuning;
            _unrotated = null;
            _bodyScale = null;
            _art = vc != null && vc.Game != null ? vc.Game.Art : null;
            // 이벤트 없이 지금 상태만 보고 집어 든다.
            Sync(w, 1f, 0f);
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (e.Type == SimEventType.PlayerZoneExpired)
            {
                ZoneView z;
                if (_zones.TryGetValue(e.EntityId, out z))
                {
                    ZoneExpiredFx(z, e.B != 0);
                    ReturnZone(z);
                    _zones.Remove(e.EntityId);
                }
            }
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (_vc == null) return;
            if (!_s.EffectLayerAttached) _s.Advance(dt);
            if (_s.Occlusion != null) _s.Occlusion.RebuildMask(w);
            Sync(w, alpha, dt);
        }

        public void Dispose()
        {
            ClearViews();
            if (!_s.EffectLayerAttached) _s.DisposeAll();
            _vc = null;
        }

        // ------------------------------------------------------------ 동기화

        void Sync(SimWorld w, float alpha, float dt)
        {
            if (w == null || _vc == null) return;
            foreach (var kv in _shots) kv.Value.Seen = false;
            foreach (var kv in _zones) kv.Value.Seen = false;

            int myBullets = 0;
            for (int i = 0; i < w.Projectiles.Count; i++)
            {
                var p = w.Projectiles[i];
                if (!p.Dead && p.State != ProjectileState.Pending && p.State != ProjectileState.Dead) myBullets++;
            }
            var load = _s.Data.Budget.LoadFor(myBullets);

            for (int i = 0; i < w.Projectiles.Count; i++)
            {
                var p = w.Projectiles[i];
                if (p.Dead || p.State == ProjectileState.Pending || p.State == ProjectileState.Dead) continue;
                if (p.Block != null && p.Block.Instant && _t.B("shots", "chain_projectile_hidden")) continue; // 순간 탄(연쇄 번개)은 ChainLightningArc 선으로
                if (StyleBodyHidden(p)) continue; // 기본 공격 유형의 몸 없는 동작(포탄·부채꼴·낙뢰·고드름·룬)은 StyleFxLayer가 그린다
                ShotView v;
                if (!_shots.TryGetValue(p.Id, out v)) { v = CreateShot(p); _shots[p.Id] = v; }
                v.Seen = true;
                UpdateShot(w, p, v, alpha, dt, load);
            }
            for (int i = 0; i < w.PlayerZones.Count; i++)
            {
                var z = w.PlayerZones[i];
                if (z.Dead) continue;
                ZoneView v;
                if (!_zones.TryGetValue(z.Id, out v)) { v = CreateZone(w, z); _zones[z.Id] = v; }
                v.Seen = true;
                v.Pos = z.Pos; v.Radius = z.Radius; v.Width = z.Width; v.UntilTick = z.UntilTick; v.Facing = z.Facing;
                UpdateZone(w, v, dt);
            }

            _drop.Clear();
            foreach (var kv in _shots) if (!kv.Value.Seen) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) { ReturnShot(_shots[_drop[i]]); _shots.Remove(_drop[i]); }
            _drop.Clear();
            foreach (var kv in _zones) if (!kv.Value.Seen) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) { ReturnZone(_zones[_drop[i]]); _zones.Remove(_drop[i]); }
        }

        // ------------------------------------------------------------ 내 탄

        HashSet<string> _unrotated;

        /// <summary>G16 shots.style_body_hidden(동작 글자): 탄 몸을 내 탄 층이 그리지 않는 기본 공격 유형.</summary>
        bool StyleBodyHidden(ProjectileEntity p) => StyleFxRules.BodyHidden(_t, p);

        /// <summary>G16 shots.unrotated_blocks: 진행 방향으로 돌리지 않는 탄(그림이 스스로 도는 책, 둥근 구체·눈알).</summary>
        bool Unrotated(ProjectileEntity p)
        {
            if (p.Block == null) return false;
            if (_unrotated == null) _unrotated = StringSet("shots", "unrotated_blocks");
            return _unrotated.Contains(p.Block.Id);
        }

        Dictionary<string, float> _bodyScale;

        /// <summary>G16 shots.body_scale: 판정보다 작게 그려진 유형 탄 그림의 배율(없으면 1).</summary>
        float BodyScale(ProjectileEntity p)
        {
            if (p.Block == null) return 1f;
            if (_bodyScale == null)
            {
                _bodyScale = new Dictionary<string, float>();
                var o = _t != null ? _t.Token("shots", "body_scale") as Newtonsoft.Json.Linq.JObject : null;
                if (o != null)
                    foreach (var prop in o.Properties())
                        if (prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.Float || prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                            _bodyScale[prop.Name] = (float)prop.Value;
            }
            float k;
            return _bodyScale.TryGetValue(p.Block.Id, out k) && k > 0f ? k : 1f;
        }

        HashSet<string> StringSet(params string[] path)
        {
            var set = new HashSet<string>();
            var arr = _t != null ? _t.Token(path) as Newtonsoft.Json.Linq.JArray : null;
            if (arr != null)
                foreach (var x in arr)
                    if (x.Type == Newtonsoft.Json.Linq.JTokenType.String) set.Add((string)x);
            return set;
        }

        ShotView CreateShot(ProjectileEntity p)
        {
            var v = new ShotView();
            string group, key;
            if (_art != null && p.Block != null && _s.Data.Shots.TryGet(p.Block.Id, out group, out key)) v.Anim = _art.Effect(group, key);
            Sprite first = v.Anim != null ? v.Anim.At(0f) : null;
            if (first == null && _art != null) first = _art.Circle(Mathf.Max(1, Mathf.RoundToInt(p.Radius * SimConst.PixelsPerUnit)));
            v.Body = _vc.Pool.Rent(first, SortBands.PlayerShots + 2);
            return v;
        }

        void UpdateShot(SimWorld w, ProjectileEntity p, ShotView v, float alpha, float dt, BulletLoadStep load)
        {
            var opt = _s.Options;
            var owner = p.OwnerSlot >= 0 && p.OwnerSlot < w.Players.Length ? w.Players[p.OwnerSlot] : null;
            string blockEl = p.Block != null ? p.Block.Element : null;
            string charEl = owner != null && owner.Def != null ? owner.Def.Element : null;
            string el = blockEl == null || blockEl == Ids.Element.Neutral ? (charEl ?? Ids.Element.Neutral) : blockEl;

            Vector2 pos = ViewMath.Lerp(p.PrevPos, p.Pos, alpha);
            Vector2 dir = p.Dir.sqrMagnitude > 0f ? p.Dir.normalized : Vector2.right;
            float angle = FxUtil.ViewAngle(dir);
            float baseR = p.Block != null && p.Block.HitRadius > 0f ? p.Block.HitRadius : p.Radius;
            float sizeMul = baseR > 0f ? p.Radius / baseR : 1f; // 크기 변형 배율(크기 두 배 블록)

            // 투명도: 사본 탄 70% × 사본·지대 옵션, 그다음 내 탄 투명도, 마지막에 자리 상한(옵션을 곱한 뒤 — 10 3.2절)
            float a = 1f;
            if (p.Source == ShotSource.CopyCircle) a = EffectOptionRules.CloneZoneAlpha(_t.F("shots", "copy_shot_opacity"), opt.CloneZoneOpacity);
            a = EffectOptionRules.MyBulletAlpha(a, opt.MyBulletOpacity);
            if (_s.Occlusion != null) a = Mathf.Min(a, _s.Occlusion.CapAt(pos));

            Vector3 vp = _vc.ToView(pos);
            float age = (w.Tick - p.SpawnTick) * SimConst.TickSeconds;
            if (v.Body != null)
            {
                if (v.Anim != null) { var spr = v.Anim.At(age); if (spr != null) v.Body.sprite = spr; }
                var t = v.Body.transform;
                t.localPosition = vp;
                t.localRotation = Quaternion.Euler(0f, 0f, Unrotated(p) ? 0f : angle);
                float bodyK = BodyScale(p);
                t.localScale = new Vector3(sizeMul * bodyK, sizeMul * bodyK, 1f);
                var c = v.Anim == null ? _t.ElementPrimary(el) : Color.white;
                // 내 탄 색이 위험색(적 공격·예고 전용)에 들면 디버그 경고 + 주황 쪽으로 고친다
                if (_s.ColorGuard != null && !_s.ColorGuard.Check(c, "shot.body." + el)) c = JuiceLayer.Safe(c);
                c.a = a;
                v.Body.color = c;
                // 서리 붙은 탄 = 흰 하늘 테(03 14절)
                if (p.Mods.Frost) SetOutline(v.Body, _t.Col("colors", "ice_sky_rim"), 1f);
            }

            // 빛무리: 밤(보스 포함)에 내 탄 둘레 24px 15%, 암흑 탄은 해질녘에도 25%(10 3.9절). 적 탄엔 없음.
            string tod = w.Wave != null && w.Wave.Wave > 0 ? DayCycle.Tod(w.Wave.Wave) : null;
            float glow = 0f;
            bool dark = blockEl == Ids.Element.Dark;
            if (tod == Ids.Tod.Night || tod == Ids.Tod.Boss) glow = dark ? _t.F("shots", "dark_dusk_glow_opacity") : _t.F("shots", "night_glow_opacity");
            else if (tod == Ids.Tod.Dusk && dark) glow = _t.F("shots", "dark_dusk_glow_opacity");
            if (glow > 0f && _art != null)
            {
                if (v.Glow == null) v.Glow = _vc.Pool.Rent(_art.Circle(Mathf.RoundToInt(_t.F("shots", "night_glow_radius_px"))), SortBands.PlayerShots);
                v.Glow.transform.localPosition = vp;
                var gc = _t.ElementSecondary(el);
                if (_s.ColorGuard != null && !_s.ColorGuard.Check(gc, "shot.glow." + el)) gc = JuiceLayer.Safe(gc);
                gc.a = glow * a;
                v.Glow.color = gc;
            }
            else ReturnSr(ref v.Glow);

            // 꼬리
            var tier = _s.Data.Budget.TierFor(owner != null ? owner.Wand.EffectiveStage : 0);
            float trailPx = EffectOptionRules.TrailLengthPx(opt.BulletTrail, tier.TrailLengthPx, load.TrailLengthPercent,
                _t.F("shots", "trail_short_scale"), _s.Data.Budget.TrailMaxLengthPx, opt.WandFxLevel);
            if (trailPx > 0f && _art != null)
            {
                SpriteAnim ta = p.Mods.Corrosion ? _art.Effect("status", "corrosion_tail")
                    : CharacterFx.Resolve(_art, _t, owner != null ? owner.CharId : null, "wand", "trail_" + FxElement(el), owner != null ? owner.Wand.EffectiveStage : 0);
                Sprite ts = ta != null ? ta.At(age) : null;
                if (ts != null)
                {
                    if (v.Trail == null) v.Trail = _vc.Pool.Rent(ts, SortBands.PlayerShots + 1);
                    v.Trail.sprite = ts;
                    v.Trail.enabled = true;
                    float lenCells = trailPx / SimConst.PixelsPerUnit;
                    float nativeW = ts.rect.width / SimConst.PixelsPerUnit;
                    float nativeH = ts.rect.height / SimConst.PixelsPerUnit;
                    float hCells = _t.F("shots", "trail_height_px") / SimConst.PixelsPerUnit * sizeMul;
                    var tt = v.Trail.transform;
                    tt.localPosition = _vc.ToView(pos - dir * (lenCells * 0.5f));
                    tt.localRotation = Quaternion.Euler(0f, 0f, angle);
                    tt.localScale = new Vector3(nativeW > 0f ? lenCells / nativeW : 1f, nativeH > 0f ? hCells / nativeH : 1f, 1f);
                    var tc = p.Mods.Corrosion ? _t.Col("colors", "corrosion") : Color.white;
                    tc.a = a * _s.Data.Budget.TrailMaxOpacityPercent / 100f;
                    v.Trail.color = tc;
                }
            }
            else ReturnSr(ref v.Trail);

            // 임계 탄: 안 흰 2px + 바깥 검정 2px 이중 테 + 앞 쐐기(흰 + 검정 테). 판정 크기는 같다.
            if (p.IsCrit && _art != null)
            {
                int inPx = _t.I("shots", "crit_inner_ring_px"), outPx = _t.I("shots", "crit_outer_ring_px");
                int r0 = Mathf.Max(1, Mathf.RoundToInt(baseR * SimConst.PixelsPerUnit));
                if (v.RimIn == null) v.RimIn = _vc.Pool.Rent(_art.Circle(r0 + inPx, true, inPx), SortBands.PlayerShots + 3);
                if (v.RimOut == null) v.RimOut = _vc.Pool.Rent(_art.Circle(r0 + inPx + outPx, true, outPx), SortBands.PlayerShots + 3);
                Place(v.RimIn, vp, sizeMul, Color.white, a);
                Place(v.RimOut, vp, sizeMul, Color.black, a);
                var chev = _art.Effect("player", _t.S("shots", "crit_chevron_key"));
                var cs = chev != null ? chev.At(0f) : null;
                if (cs != null)
                {
                    if (v.Chevron == null) { v.Chevron = _vc.Pool.Rent(cs, SortBands.PlayerShots + 6); SetOutline(v.Chevron, Color.black, 1f); }
                    var ct = v.Chevron.transform;
                    ct.localPosition = _vc.ToView(pos + dir * (p.Radius + cs.rect.width / SimConst.PixelsPerUnit));
                    ct.localRotation = Quaternion.Euler(0f, 0f, angle);
                    var cc = Color.white; cc.a = a;
                    v.Chevron.color = cc;
                }
            }
            else { ReturnSr(ref v.RimIn); ReturnSr(ref v.RimOut); ReturnSr(ref v.Chevron); }

            // 비기 탄 = 일반 탄 + 흰 중심(임계 표식·금색 금지 — 10 3.6절)
            if ((p.Source == ShotSource.ManaDischarge || p.Source == ShotSource.ArtOther) && _art != null)
            {
                if (v.Core == null) v.Core = _vc.Pool.Rent(_art.Circle(Mathf.Max(1, _t.I("shots", "art_core_px") / 2)), SortBands.PlayerShots + 5);
                Place(v.Core, vp, 1f, Color.white, a);
            }
            else ReturnSr(ref v.Core);

            // 깊이 각주 점: 깊이 감쇠 특성 지팡이(제자 3)의 하위 스코프 탄 오른쪽 위 2×2 흰 민트 점 1~3개(02 8.3절)
            int dots = 0;
            if (owner != null && owner.Def != null && owner.Def.WandTrait == Ids.WandTrait.DepthDecay && p.ScopeDepth > 0)
                dots = Mathf.Min(p.ScopeDepth, _t.I("shots", "depth_dot_max"));
            dots = Mathf.Min(dots, v.Dots.Length);
            float dotCells = _t.F("shots", "depth_dot_px") / SimConst.PixelsPerUnit;
            for (int i = 0; i < v.Dots.Length; i++)
            {
                if (i < dots && _art != null)
                {
                    if (v.Dots[i] == null) v.Dots[i] = _vc.Pool.Rent(_art.WhitePixel, SortBands.PlayerShots + 7);
                    var dt2 = v.Dots[i].transform;
                    float off = p.Radius * sizeMul + dotCells;
                    dt2.localPosition = vp + new Vector3(off + i * dotCells * 2f, off, 0f);
                    dt2.localScale = new Vector3(dotCells, dotCells, 1f);
                    var mc = _t.Col("colors", "mint_dot"); mc.a = a;
                    v.Dots[i].color = mc;
                }
                else ReturnSr(ref v.Dots[i]);
            }
            v.DotCount = dots;

            // 탄이 흘리는 입자(분류 shot, 수명 0.3초): 화면 탄 수 단계·지팡이 효과 옵션을 따른다.
            int shed = load.Shed && opt.WandFxLevel != OptionValues.Minimal ? tier.ShedPerBullet : 0;
            if (opt.WandFxLevel == OptionValues.Reduced) shed = (shed + 1) / 2;
            float life = _s.Data.Budget.LifeShed;
            if (shed > 0 && life > 0f && dt > 0f)
            {
                v.ShedTimer -= dt;
                if (v.ShedTimer <= 0f)
                {
                    v.ShedTimer = life / shed;
                    var sc = _t.ElementSecondary(el); sc.a = a;
                    _s.Particles.Spawn(ParticleBudgetConfig.CatShot, pos, -dir * _t.F("hits", "particle_speed_cells") * 0.5f, life, sc,
                        _t.F("hits", "particle_px"), SortBands.PlayerShots, null, true);
                }
            }
        }

        string FxElement(string elementId)
        {
            string k = ArtAliases.ElementFxKey(elementId);
            return k == null || elementId == Ids.Element.Neutral ? _t.S("fx_element_for_neutral") : k;
        }

        static void Place(SpriteRenderer sr, Vector3 vp, float scale, Color c, float a)
        {
            if (sr == null) return;
            sr.transform.localPosition = vp;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            c.a = a;
            sr.color = c;
        }

        static void SetOutline(SpriteRenderer sr, Color c, float px)
        {
            var mpb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(mpb);
            mpb.SetColor(SpriteShaderProps.OutlineColor, c);
            mpb.SetFloat(SpriteShaderProps.OutlineSize, px);
            sr.SetPropertyBlock(mpb);
        }

        void ReturnSr(ref SpriteRenderer sr)
        {
            if (sr == null) return;
            if (_vc != null && _vc.Pool != null) _vc.Pool.Return(sr);
            sr = null;
        }

        void ReturnShot(ShotView v)
        {
            ReturnSr(ref v.Body); ReturnSr(ref v.Trail); ReturnSr(ref v.Glow);
            ReturnSr(ref v.RimIn); ReturnSr(ref v.RimOut); ReturnSr(ref v.Chevron); ReturnSr(ref v.Core);
            for (int i = 0; i < v.Dots.Length; i++) ReturnSr(ref v.Dots[i]);
        }

        // ------------------------------------------------------------ 플레이어 지대

        /// <summary>화면 기준 폭(px, 64PPU 기준). 해상도 배수가 있는 그림(pixelScale)도 원본 자리 크기로 센다.</summary>
        static float LogicalWidthPx(Sprite s) => s.rect.width / s.pixelsPerUnit * SimConst.PixelsPerUnit;

        static string CharOf(SimWorld w, int slot) =>
            w != null && w.Players != null && slot >= 0 && slot < w.Players.Length && w.Players[slot] != null ? w.Players[slot].CharId : null;

        ZoneView CreateZone(SimWorld w, PlayerZoneEntity z)
        {
            var v = new ZoneView
            {
                Kind = z.Kind, Pos = z.Pos, Facing = z.Facing, Radius = z.Radius, Width = z.Width,
                StartTick = z.StartTick, UntilTick = z.UntilTick, OwnerSlot = z.OwnerSlot
            };
            if (_art == null) return v;
            switch (z.Kind)
            {
                case PlayerZoneKind.Kiln:
                    // 시전자 캐릭터의 교체 그림(G16 character_fx — 제자 1 불가마 테두리)이 있으면 그것.
                    v.Anim = CharacterFx.Resolve(_art, _t, CharOf(w, z.OwnerSlot), "special", _t.S("zones", "kiln_key"));
                    v.Fill = _vc.Pool.Rent(_art.Circle(Mathf.Max(1, Mathf.RoundToInt(z.Radius * SimConst.PixelsPerUnit))), SortBands.PlayerZones);
                    v.Main = _vc.Pool.Rent(v.Anim != null ? v.Anim.At(0f) : null, SortBands.PlayerZones + 1);
                    break;
                case PlayerZoneKind.CopyCircle:
                    v.Anim = _art.Effect("special", _t.S("zones", "copy_key"));
                    v.Main = _vc.Pool.Rent(v.Anim != null ? v.Anim.At(0f) : null, SortBands.PlayerZones + 2);
                    break;
                case PlayerZoneKind.IceScreen:
                    v.Anim = _art.Effect("special", IceVertical(z.Facing) ? _t.S("zones", "ice_key_vertical") : _t.S("zones", "ice_key_horizontal"));
                    v.Main = _vc.Pool.Rent(v.Anim != null ? v.Anim.At(0f) : null, SortBands.Bodies + SortBands.YOrder(z.Pos.y));
                    break;
            }
            return v;
        }

        /// <summary>병풍은 시야에 수직 — 동서를 보면 세로 벽, 남북·대각이면 가로 벽(대각은 돌린다).</summary>
        static bool IceVertical(Dir8 f) => f == Dir8.E || f == Dir8.W;

        void UpdateZone(SimWorld w, ZoneView v, float dt)
        {
            if (v.Main == null) return;
            var opt = _s.Options;
            float now = w.Tick * SimConst.TickSeconds;
            float age = (w.Tick - v.StartTick) * SimConst.TickSeconds;
            float total = Mathf.Max(SimConst.TickSeconds, (v.UntilTick - v.StartTick) * SimConst.TickSeconds);
            float left = (v.UntilTick - w.Tick) * SimConst.TickSeconds;
            float fadeLast = _t.F("zones", "fade_last_seconds");
            var t = v.Main.transform;
            t.localPosition = _vc.ToView(v.Pos);
            Sprite spr = null;
            float a;
            switch (v.Kind)
            {
                case PlayerZoneKind.Kiln:
                {
                    // 지름 5칸 고리(주황 점선 + 벽돌 무늬가 천천히 돈다), 안 주황 ≤ 20%, 끝나기 1초 전 테두리 흐려짐. 차오르지 않음.
                    if (v.Anim != null && v.Anim.Frames != null && v.Anim.Frames.Length > 0)
                        spr = v.Anim.Frames[Mathf.Abs((int)(now * v.Anim.Fps)) % v.Anim.Frames.Length];
                    float s = v.Radius / Mathf.Max(1e-3f, _t.F("zones", "kiln_native_radius_cells"));
                    t.localScale = new Vector3(s, s, 1f);
                    float fade = left < fadeLast && fadeLast > 0f ? Mathf.Clamp01(left / fadeLast) : 1f;
                    a = EffectOptionRules.CloneZoneAlpha(fade, opt.CloneZoneOpacity);
                    string tod = w.Wave != null && w.Wave.Wave > 0 ? DayCycle.Tod(w.Wave.Wave) : null;
                    var bc = tod == Ids.Tod.Dusk ? _t.Col("colors", "kiln_dusk") : Color.white;
                    bc.a = a;
                    v.Main.color = bc;
                    if (v.Fill != null)
                    {
                        v.Fill.transform.localPosition = _vc.ToView(v.Pos);
                        var fc = _t.ElementPrimary(Ids.Element.Fire);
                        fc.a = EffectOptionRules.CloneZoneAlpha(_t.F("zones", "kiln_fill_opacity"), opt.CloneZoneOpacity) * fade;
                        v.Fill.color = fc;
                    }
                    // 불티 초당 8(내 비기 몫)
                    float rate = _t.F("zones", "kiln_ember_per_second");
                    if (rate > 0f && dt > 0f && opt.EffectsLevel < 2)
                    {
                        v.EmberTimer -= dt;
                        if (v.EmberTimer <= 0f)
                        {
                            v.EmberTimer = 1f / rate;
                            var ember = CharacterFx.Resolve(_art, _t, CharOf(w, v.OwnerSlot), "special", _t.S("zones", "kiln_ember_key"));
                            Vector2 off = UnityEngine.Random.insideUnitCircle * v.Radius;
                            var ec = Color.white; ec.a = a;
                            _s.Particles.Spawn(ParticleBudgetConfig.CatArt, v.Pos + off, new Vector2(0f, -_t.F("hits", "particle_speed_cells") * 0.5f),
                                _t.F("art", "pulse_seconds") * 2f, ec, ember != null && ember.At(0f) != null ? LogicalWidthPx(ember.At(0f)) : _t.F("hits", "particle_px"),
                                SortBands.PlayerZones + 3, ember != null ? ember.At(0f) : null, true);
                        }
                    }
                    break;
                }
                case PlayerZoneKind.CopyCircle:
                {
                    // 글자 고리 마법진, 지속 시간 동안 고리가 줄어든다.
                    if (v.Anim != null && v.Anim.Frames != null && v.Anim.Frames.Length > 0)
                        spr = v.Anim.Frames[Mathf.Abs((int)(now * v.Anim.Fps)) % v.Anim.Frames.Length];
                    float rest = Mathf.Clamp01(left / total);
                    float s = (v.Radius > 0f ? v.Radius : _t.F("zones", "copy_native_radius_cells")) / Mathf.Max(1e-3f, _t.F("zones", "copy_native_radius_cells")) * rest;
                    t.localScale = new Vector3(s, s, 1f);
                    a = EffectOptionRules.CloneZoneAlpha(1f, opt.CloneZoneOpacity);
                    var c = Color.white; c.a = a;
                    v.Main.color = c;
                    break;
                }
                default:
                {
                    // 얼음 병풍: 0.15초에 솟음(0-2), 서 있음(3), 끝나기 1초 전 금(4-5). 불투명도 75%, 윗선 흰 하늘 2px(그림).
                    int[] rise = _t.Ints("zones", "ice_rise_frames");
                    int[] crack = _t.Ints("zones", "ice_crack_frames");
                    int stand = _t.I("zones", "ice_stand_frame");
                    float riseS = _t.F("zones", "ice_rise_seconds");
                    int frame = stand;
                    if (age < riseS && rise.Length >= 2 && riseS > 0f)
                        frame = rise[0] + Mathf.Min(rise[1] - rise[0], (int)(age / riseS * (rise[1] - rise[0] + 1)));
                    else if (left < fadeLast && crack.Length >= 2)
                        frame = ((int)(now * (v.Anim != null ? v.Anim.Fps : 1f)) & 1) == 0 ? crack[0] : crack[1];
                    if (v.Anim != null && v.Anim.Frames != null && frame >= 0 && frame < v.Anim.Frames.Length) spr = v.Anim.Frames[frame];
                    float nativeW = spr != null ? Mathf.Max(spr.rect.width, spr.rect.height) / SimConst.PixelsPerUnit : 1f;
                    float s = v.Width > 0f && nativeW > 0f ? v.Width / nativeW : 1f;
                    t.localScale = new Vector3(s, s, 1f);
                    t.localRotation = IceVertical(v.Facing) ? Quaternion.identity
                        : Quaternion.Euler(0f, 0f, FxUtil.ViewAngle(Dir8Util.ToSimVector(v.Facing)) - 90f);
                    v.Main.sortingOrder = SortBands.Bodies + SortBands.YOrder(v.Pos.y);
                    a = EffectOptionRules.CloneZoneAlpha(_t.F("zones", "ice_opacity"), opt.CloneZoneOpacity);
                    var c = Color.white; c.a = a;
                    v.Main.color = c;
                    break;
                }
            }
            if (spr != null) v.Main.sprite = spr;
        }

        void ZoneExpiredFx(ZoneView v, bool broken)
        {
            if (_art == null) return;
            var opt = _s.Options;
            switch (v.Kind)
            {
                case PlayerZoneKind.CopyCircle:
                {
                    // 끝나면 책장 흩어짐
                    var pages = _art.Effect("special", _t.S("zones", "copy_pages_key"));
                    var ps = pages != null ? pages.At(0f) : null;
                    _s.Particles.Burst(ParticleBudgetConfig.CatArt, _t.I("zones", "copy_pages_count"), v.Pos, _t.F("hits", "particle_speed_cells"),
                        _t.F("art", "pulse_seconds") * 2f, new Color(1f, 1f, 1f, EffectOptionRules.CloneZoneAlpha(1f, opt.CloneZoneOpacity)),
                        ps != null ? ps.rect.width : _t.F("hits", "particle_px"), SortBands.Bursts, ps);
                    break;
                }
                case PlayerZoneKind.IceScreen:
                {
                    // 깨짐(6-9) 한 번 + 얼음 조각 12개가 앞쪽 3×2칸 직사각형으로
                    int[] brk = _t.Ints("zones", "ice_break_frames");
                    if (v.Anim != null && v.Anim.Frames != null && brk.Length >= 2 && brk[1] < v.Anim.Frames.Length)
                    {
                        int n = brk[1] - brk[0] + 1;
                        var frames = new Sprite[n];
                        for (int i = 0; i < n; i++) frames[i] = v.Anim.Frames[brk[0] + i];
                        var sub = new SpriteAnim { Key = v.Anim.Key, Frames = frames, Fps = v.Anim.Fps, Loop = false };
                        float nativeW = frames[0] != null ? Mathf.Max(frames[0].rect.width, frames[0].rect.height) / SimConst.PixelsPerUnit : 1f;
                        float s = v.Width > 0f && nativeW > 0f ? v.Width / nativeW : 1f;
                        _s.Sprites.Spawn(new FxSpec
                        {
                            Anim = sub, SimPos = v.Pos, ScaleFrom = new Vector2(s, s), Color = new Color(1f, 1f, 1f, EffectOptionRules.CloneZoneAlpha(_t.F("zones", "ice_opacity"), opt.CloneZoneOpacity)),
                            RotationDeg = IceVertical(v.Facing) ? 0f : FxUtil.ViewAngle(Dir8Util.ToSimVector(v.Facing)) - 90f,
                            SortingOrder = SortBands.Bodies + SortBands.YOrder(v.Pos.y)
                        });
                    }
                    var shard = _art.Effect("special", _t.S("zones", "ice_shard_key"));
                    var ss = shard != null ? shard.At(0f) : null;
                    float[] rect = _t.Floats("zones", "ice_shard_rect_cells");
                    Vector2 fwd = Dir8Util.ToSimVector(v.Facing);
                    Vector2 side = new Vector2(-fwd.y, fwd.x);
                    int count = _t.I("zones", "ice_shard_count");
                    for (int i = 0; i < count; i++)
                    {
                        float u = rect.Length > 0 ? UnityEngine.Random.Range(-rect[0] * 0.5f, rect[0] * 0.5f) : 0f;
                        float d = rect.Length > 1 ? UnityEngine.Random.Range(0f, rect[1]) : 0f;
                        Vector2 target = v.Pos + side * u + fwd * d;
                        float life = _t.F("art", "pulse_seconds") * 2f;
                        _s.Particles.Spawn(ParticleBudgetConfig.CatArt, v.Pos, (target - v.Pos) / Mathf.Max(life, 1e-3f), life,
                            _t.ElementSecondary(Ids.Element.Ice), ss != null ? ss.rect.width : _t.F("hits", "particle_px"), SortBands.Bursts, ss);
                    }
                    break;
                }
            }
        }

        void ReturnZone(ZoneView v)
        {
            ReturnSr(ref v.Main);
            ReturnSr(ref v.Fill);
        }

        void ClearViews()
        {
            foreach (var kv in _shots) ReturnShot(kv.Value);
            foreach (var kv in _zones) ReturnZone(kv.Value);
            _shots.Clear();
            _zones.Clear();
        }
    }
}
