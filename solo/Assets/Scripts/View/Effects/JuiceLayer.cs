using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 타격감 덧층(AddEffects에서 EffectLayer 뒤). EffectLayer가 요약서대로 내는 효과 위에 표시 전용 연출을 더 얹는다:
    /// 적중 빛·불꽃, 처치 폭발(흰 실루엣 터짐·속성 폭발·충격파 고리·파편·불씨), 연속 처치 키우기, 내 탄 빛 꼬리·발사 섬광,
    /// 비기 번쩍·방사 충격파·줌 펀치, 폭발 덧빛, 드랍 반짝임·줍기 고리, 웨이브 시작·클리어, 보스 등장·처치.
    /// 지키는 것:
    /// - 입자는 전부 ParticleBudget에서 표를 받는다(분류 art/hit/kill/shot/environment). 넘치면 그리지 않는다.
    /// - 큰 반투명 효과(빛 덩이·고리)는 누적 가림 넓이 셈(OcclusionField)을 거쳐 예고 위에서 옅어진다.
    /// - 전체 화면 번쩍은 번쩍 관리기(FlashGovernor)를 거친다. 흔들림·그림 정지는 호스트(설정 배율·감도)를 거친다.
    /// - 연출 감도: 전체 1 / 줄임 0.5 / 최소 0(이 층 전체를 끈다). 처치 조각 옵션 smoke_only면 파편 없음. 번쩍 배율은 빛 덩이에도 곱한다.
    /// - 플레이어 쪽 색은 위험색(색상각 330~15도, 채도·명도 70% 이상)이 되지 않게 고친다(Safe).
    /// 수치는 Resources/DataSolo/Juice.json(표시 전용, 솔로 임시 — QA에서 사용자가 정한다).
    /// </summary>
    public sealed class JuiceLayer : IWorldViewLayer
    {
        sealed class Bit
        {
            public SpriteRenderer Sr;
            public SpriteAnim Anim;
            public int Token;
            public Vector2 Pos, Vel;
            public float Drag, Gravity, Age, Life, Delay;
            /// <summary>그림 원래 크기에 대한 배율(처음 → 끝, 끝으로 갈수록 느려진다).</summary>
            public float ScaleFrom, ScaleTo;
            public float Stretch;
            public float Rot, RotVel;
            public bool AlignToVel;
            public Color Color;
            public float Alpha;
            /// <summary>흰 덮기(실루엣 터짐).</summary>
            public float Flash;
            public bool FlipX;
            public int Occ;
            public float OccR;
            /// <summary>0 = 끝으로 갈수록 흐려짐, 1 = 반쯤까지 그대로 있다가 흐려짐.</summary>
            public float Hold;
        }

        sealed class ShotGlow
        {
            public SpriteRenderer Glow, Streak;
            public float ShedAcc;
            public int Stamp;
        }

        sealed class DropGlow
        {
            public SpriteRenderer Glow;
            public Vector2 Pos;
            public DropKind Kind;
            public float NextTwinkle;
            public int Stamp;
        }

        struct EnemyInfo
        {
            public string DefId;
            public float Radius;
            public bool Elite;
            public Vector2 Pos;
            public bool FlipX;
        }

        readonly EffectsShared _s;
        readonly EffectLayer _fx;
        readonly List<Bit> _bits = new List<Bit>();
        readonly Dictionary<int, ShotGlow> _shots = new Dictionary<int, ShotGlow>();
        readonly Dictionary<int, DropGlow> _drops = new Dictionary<int, DropGlow>();
        readonly Dictionary<int, EnemyInfo> _enemies = new Dictionary<int, EnemyInfo>();
        readonly Dictionary<int, string> _enemyElement = new Dictionary<int, string>();
        readonly List<int> _tmp = new List<int>();
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        ViewContext _vc;
        ISpritePool _pool;
        IArtLibrary _art;
        EffectsTuning _j;
        EffectsTuning _t;
        SimWorld _w;
        int _stamp;
        int _streak;
        float _lastKillTime = -100f;
        float _clock;

        public JuiceLayer(EffectsShared shared, EffectLayer effects)
        {
            _s = shared ?? new EffectsShared();
            _fx = effects;
        }

        // ---- 점검용 셈
        public int LiveBits => _bits.Count;
        public int ShotGlowCount => _shots.Count;
        public int DropGlowCount => _drops.Count;
        public int KillStreak => _streak;

        // ------------------------------------------------------------ IWorldViewLayer

        public void Build(ViewContext vc, SimWorld w)
        {
            ClearAll();
            _vc = vc;
            _w = w;
            _pool = vc != null ? vc.Pool : null;
            _art = vc != null && vc.Game != null ? vc.Game.Art : null;
            _s.Ensure(vc, w);
            _t = _s.Data != null ? _s.Data.Tuning : new EffectsTuning(null);
            var content = vc != null && vc.Game != null && vc.Game.Content != null ? vc.Game.Content : (w != null ? w.Content : null);
            Newtonsoft.Json.Linq.JObject root = null;
            try { root = content != null ? content.SoloExtra(JuiceName) : null; }
            catch (System.Exception) { root = null; }
            _j = new EffectsTuning(root);
            _streak = 0;
            _lastKillTime = -100f;
        }

        /// <summary>Resources/DataSolo/Juice.json.</summary>
        public const string JuiceName = "Juice";

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_vc == null || _pool == null) return;
            _w = w;
            if (Level <= 0f) return;
            switch (e.Type)
            {
                case SimEventType.EnemyDamaged: OnHit(w, e); break;
                case SimEventType.EnemyKilled: OnKill(w, e); break;
                case SimEventType.BossKilled: OnBossKilled(e); break;
                case SimEventType.ShotFired: OnShot(w, e); break;
                case SimEventType.Explosion: OnExplosion(e); break;
                case SimEventType.ArtFired: OnArt(w, e); break;
                case SimEventType.ArtPulse: OnArtPulse(w, e); break;
                case SimEventType.PlayerHit: Punch(J("player_hit", "punch")); break;
                case SimEventType.GrazeAwarded: OnGraze(w, e); break;
                case SimEventType.DropSpawned: OnDropSpawned(e); break;
                case SimEventType.DropPicked: OnDropPicked(w, e); break;
                case SimEventType.WaveStarted: OnWaveStarted(w); break;
                case SimEventType.WaveCleared: OnWaveCleared(w); break;
                case SimEventType.BossIntroBegin: OnBossIntro(w, e); break;
            }
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (_vc == null || _pool == null) return;
            _w = w;
            _clock += dt;
            _stamp++;
            CacheEnemies(w);
            if (Level <= 0f)
            {
                // 최소: 남은 것을 걷고 아무것도 그리지 않는다
                if (_bits.Count > 0 || _shots.Count > 0 || _drops.Count > 0) ClearAll();
                return;
            }
            UpdateShotGlows(w, alpha, dt);
            UpdateDropGlows(w, dt);
            UpdateBits(dt);
        }

        public void Dispose()
        {
            ClearAll();
            _vc = null;
            _pool = null;
        }

        void ClearAll()
        {
            for (int i = _bits.Count - 1; i >= 0; i--) KillBit(i);
            foreach (var kv in _shots) ReturnShot(kv.Value);
            _shots.Clear();
            foreach (var kv in _drops) ReturnDrop(kv.Value);
            _drops.Clear();
            _enemies.Clear();
            _enemyElement.Clear();
        }

        // ------------------------------------------------------------ 값

        float Level
        {
            get
            {
                int lv = _s.EffectsLevel;
                if (lv >= 2) return 0f;
                float master = _j != null && _j.Token("intensity") != null ? _j.F("intensity") : 1f;
                return (lv == 1 ? Or(J("reduced_scale"), 0.5f) : 1f) * master;
            }
        }

        float J(params string[] path) => _j != null ? _j.F(path) : 0f;
        /// <summary>그림 정지 예산·번쩍 관리기의 시계(틱 초) — EffectLayer와 같은 시계를 써야 1초 창이 맞는다.</summary>
        double NowSec => _w != null ? _w.Tick * (double)SimConst.TickSeconds : 0d;
        int Count(float n) => Mathf.Max(0, Mathf.RoundToInt(n * Level));
        static float Or(float v, float fallback) => v > 0f ? v : fallback;
        float FlashScale => Mathf.Clamp01(_s.Options.FlashScale);

        Color JCol(string a, string b, Color fallback)
        {
            string s = _j != null ? _j.S(a, b) : null;
            return Safe(EffectsTuning.ParseColor(s, fallback));
        }

        Color ElPrimary(string el) => Safe(_t.ElementPrimary(el ?? Ids.Element.Neutral));
        Color ElSecondary(string el) => Safe(_t.ElementSecondary(el ?? Ids.Element.Neutral));

        /// <summary>플레이어 쪽 색이 위험색이면 주황 쪽(25도)으로 옮긴다(루트 CLAUDE.md 2.5절).</summary>
        public static Color Safe(Color c)
        {
            if (!DangerColor.IsDanger((Color32)c)) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var r = Color.HSVToRGB(25f / 360f, s, v);
            r.a = c.a;
            return r;
        }

        string ElementOfSlot(SimWorld w, int slot)
        {
            if (w == null || slot < 0 || slot >= w.Players.Length) slot = 0;
            var p = w != null ? w.Players[slot] : null;
            return p != null && p.Def != null ? p.Def.Element : Ids.Element.Neutral;
        }

        PlayerState Player(SimWorld w, int slot) => w != null && slot >= 0 && slot < w.Players.Length ? w.Players[slot] : null;

        void Punch(float amount)
        {
            if (amount == 0f) return;
            var cf = CameraFx.Of(_vc);
            if (cf != null) cf.Punch(amount * Mathf.Min(1f, Level));
        }

        void ShakePx(float px)
        {
            if (px > 0f && _vc.Shake != null) _vc.Shake(px, _t.F("shake_seconds"));
        }

        void HitStop(float seconds, bool budgeted)
        {
            if (seconds <= 0f || _vc.HitStop == null) return;
            if (budgeted && _s.HitStopBudget != null) seconds = _s.HitStopBudget.Request(seconds, NowSec);
            if (seconds > 0f) _vc.HitStop(seconds);
        }

        void ScreenFlash(Color color, float opacity)
        {
            if (opacity <= 0f || _s.Flash == null || _fx == null || _fx.Overlay == null) return;
            var res = _s.Flash.Submit(new FlashRequest
            {
                Opacity = opacity, OnSeconds = _s.Flash.MaxOnSeconds, OffSeconds = Mathf.Max(_s.Flash.MinOffSeconds, J("flash_off_seconds")),
                AreaFraction = 1f
            }, NowSec, _s.EffectsLevel, _s.Options.FlashScale);
            _fx.Overlay.Flash(Safe(color), res);
        }

        // ------------------------------------------------------------ 조각 하나

        Bit Spawn(Sprite sprite, SpriteAnim anim, Vector2 simPos, Vector2 vel, float life, float scaleFrom, float scaleTo, Color color, int order,
            bool additive, string budget = null, float drag = 0f, float gravity = 0f, bool occlusion = false)
        {
            if (life <= 0f || _pool == null) return null;
            Sprite first = anim != null ? anim.At(0f) : sprite;
            if (first == null) return null;
            int token = 0;
            if (budget != null)
            {
                if (_s.Budget == null || !_s.Budget.TryAcquire(budget, _s.Time, life, out token)) return null;
            }
            float alpha = color.a;
            int occ = 0;
            float occR = 0f;
            if (occlusion && _s.Occlusion != null)
            {
                occR = Mathf.Max(first.rect.width, first.rect.height) / Mathf.Max(1f, first.pixelsPerUnit) * Mathf.Max(scaleFrom, scaleTo) * 0.5f;
                alpha = _s.Occlusion.Admit(simPos, occR, alpha);
                if (alpha <= 0f) { if (token != 0) _s.Budget.Release(token); return null; }
                occ = _s.Occlusion.Register(simPos, occR, alpha);
            }
            // 가산 혼합은 32px 이하 그림만(10 3.2절), 그리고 화면에서 지름 2칸 미만일 때만
            float shownCells = Mathf.Max(first.rect.width, first.rect.height) / Mathf.Max(1f, first.pixelsPerUnit) * Mathf.Max(scaleFrom, scaleTo);
            bool add = additive && shownCells < 2f;
            var mat = EffectOptionRules.MaterialFor(Mathf.RoundToInt(first.rect.width), Mathf.RoundToInt(first.rect.height), add, _t.I("additive_max_px") > 0 ? _t.I("additive_max_px") : 32);
            var sr = _pool.Rent(first, order, mat);
            if (sr == null)
            {
                if (token != 0) _s.Budget.Release(token);
                if (occ != 0) _s.Occlusion.Unregister(occ);
                return null;
            }
            var b = new Bit
            {
                Sr = sr, Anim = anim, Token = token, Pos = simPos, Vel = vel, Life = life, ScaleFrom = scaleFrom, ScaleTo = scaleTo,
                Color = color, Alpha = alpha, Drag = drag, Gravity = gravity, Occ = occ, OccR = occR
            };
            _bits.Add(b);
            ApplyBit(b);
            return b;
        }

        void UpdateBits(float dt)
        {
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var b = _bits[i];
                b.Age += dt;
                if (b.Age >= b.Delay + b.Life) { KillBit(i); continue; }
                if (b.Age >= b.Delay)
                {
                    b.Pos += b.Vel * dt;
                    if (b.Drag > 0f) b.Vel *= Mathf.Exp(-b.Drag * dt);
                    b.Vel.y += b.Gravity * dt; // 시뮬레이션 y는 아래가 +: 양수 = 화면에서 떨어진다
                    b.Rot += b.RotVel * dt;
                }
                ApplyBit(b);
            }
        }

        void ApplyBit(Bit b)
        {
            var sr = b.Sr;
            if (sr == null) return;
            if (b.Age < b.Delay) { sr.enabled = false; return; }
            sr.enabled = true;
            float k = b.Life > 0f ? Mathf.Clamp01((b.Age - b.Delay) / b.Life) : 1f;
            if (b.Anim != null)
            {
                var spr = b.Anim.At(b.Age - b.Delay);
                if (spr != null) sr.sprite = spr;
            }
            float ease = 1f - (1f - k) * (1f - k) * (1f - k); // 빠르게 커졌다 느려진다
            float sc = Mathf.LerpUnclamped(b.ScaleFrom, b.ScaleTo, ease);
            var t = sr.transform;
            t.localPosition = _vc.ToView(b.Pos);
            Vector2 vv = SimConst.DirToView(b.Vel);
            float sx = sc, sy = sc;
            if (b.AlignToVel && vv.sqrMagnitude > 1e-4f)
            {
                sx = sc * (1f + b.Stretch * vv.magnitude);
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vv.y, vv.x) * Mathf.Rad2Deg);
            }
            else t.localRotation = Quaternion.Euler(0f, 0f, b.Rot);
            t.localScale = new Vector3(sx, sy, 1f);
            sr.flipX = b.FlipX;
            float a = b.Alpha;
            float fk = b.Hold > 0f ? Mathf.Clamp01((k - b.Hold) / Mathf.Max(1e-3f, 1f - b.Hold)) : k;
            a *= 1f - fk * fk;
            if (b.Occ != 0 && _s.Occlusion != null)
            {
                float cap = _s.Occlusion.CapAt(b.Pos);
                if (a > cap) a = cap;
                _s.Occlusion.UpdateLive(b.Occ, b.Pos, b.OccR * Mathf.Max(0.01f, sc / Mathf.Max(0.01f, Mathf.Max(b.ScaleFrom, b.ScaleTo))), a);
            }
            var c = b.Color;
            c.a = a;
            sr.color = c;
            if (b.Flash > 0f)
            {
                _mpb.Clear();
                _mpb.SetColor(SpriteShaderProps.FlashColor, Color.white);
                _mpb.SetFloat(SpriteShaderProps.FlashAmount, b.Flash);
                sr.SetPropertyBlock(_mpb);
            }
        }

        void KillBit(int i)
        {
            var b = _bits[i];
            _bits.RemoveAt(i);
            if (b.Token != 0 && _s.Budget != null) _s.Budget.Release(b.Token);
            if (b.Occ != 0 && _s.Occlusion != null) _s.Occlusion.Unregister(b.Occ);
            if (b.Sr != null && _pool != null) _pool.Return(b.Sr);
        }

        /// <summary>그림의 원래 크기(칸) → 지름 cells로 보이게 하는 배율.</summary>
        static float Fit(Sprite s, float cells)
        {
            if (s == null) return 1f;
            float native = Mathf.Max(s.rect.width, s.rect.height) / Mathf.Max(1f, s.pixelsPerUnit);
            return native > 0f ? cells / native : 1f;
        }

        Sprite Ring => _art != null ? _art.Circle(Mathf.RoundToInt(Or(J("ring", "base_radius_px"), 128f)), true,
            Mathf.Max(1, Mathf.RoundToInt(Or(J("ring", "width_px"), 4f)))) : null;

        /// <summary>빛 덩이(가운데 밝은 원). 지름 2칸 이상이면 알파 혼합 + 가림 셈.</summary>
        Bit Glow(Vector2 pos, float fromCells, float toCells, float life, Color c, int order)
        {
            bool big = Mathf.Max(fromCells, toCells) >= 2f;
            // 큰 빛 덩이는 사용자 그림 bloom_glow(64px, 알파 혼합), 작은 것은 코드 빛 점(32px, 가산)
            var s = big ? (CombatFxArt.Single(_art, CombatFxArt.BloomGlow) ?? ProcSprites.SoftDot) : ProcSprites.SoftDot;
            if (big) c.a *= FlashScale;
            if (c.a <= 0f) return null;
            return Spawn(s, null, pos, Vector2.zero, life, Fit(s, fromCells), Fit(s, toCells), c, order, true, null, 0f, 0f, big);
        }

        /// <summary>퍼지는 고리(반지름 칸).</summary>
        Bit Shockwave(Vector2 pos, float fromR, float toR, float life, Color c, float delay = 0f, bool sheet = false)
        {
            if (sheet && life > 0f)
            {
                // 사용자 그림 shockwave_ring(128px 5장, 옅은 파랑 — 색은 입히지 않는다). 끝 장의 고리 = 그림 지름 거의 전부.
                var probe = CombatFxArt.Anim(_art, CombatFxArt.Shockwave, 12f);
                if (probe != null)
                {
                    var anim = CombatFxArt.Anim(_art, CombatFxArt.Shockwave, Mathf.Round(probe.Frames.Length / life));
                    var f0 = anim.At(0f);
                    float sc = Fit(f0, toR * 2f);
                    var sb = Spawn(null, anim, pos, Vector2.zero, life, sc * 0.85f, sc, new Color(1f, 1f, 1f, c.a), SortBands.Bursts + 5, false, null, 0f, 0f, true);
                    if (sb != null) { sb.Delay = delay; sb.Hold = 0.5f; ApplyBit(sb); }
                    return sb;
                }
            }
            var r = Ring;
            if (r == null) return null;
            var b = Spawn(r, null, pos, Vector2.zero, life, Fit(r, fromR * 2f), Fit(r, toR * 2f), c, SortBands.Bursts + 5, false, null, 0f, 0f, true);
            if (b != null) { b.Delay = delay; ApplyBit(b); }
            return b;
        }

        /// <summary>불꽃·파편 n개를 흩뿌린다. dir이 0이 아니면 그쪽 ±spreadDeg 안으로.</summary>
        int Sparks(string budget, int n, Vector2 pos, Vector2 dirSim, float spreadDeg, float speedMin, float speedMax, float life, float sizeCells,
            Color a, Color b, float drag, float gravity, float stretch, bool additive, Sprite sprite = null)
        {
            if (n <= 0) return 0;
            var spr = sprite != null ? sprite : (additive ? ProcSprites.SoftDot : ProcSprites.HardDot);
            float baseAng = dirSim.sqrMagnitude > 1e-6f ? Mathf.Atan2(dirSim.y, dirSim.x) : 0f;
            bool cone = dirSim.sqrMagnitude > 1e-6f;
            int got = 0;
            for (int i = 0; i < n; i++)
            {
                float ang = cone ? baseAng + Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad : Random.Range(0f, Mathf.PI * 2f);
                float sp = Random.Range(speedMin, speedMax);
                Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
                float l = life * Random.Range(0.7f, 1.15f);
                float sz = sizeCells * Random.Range(0.7f, 1.3f);
                var c = Color.Lerp(a, b, Random.value);
                var bit = Spawn(spr, null, pos, v, l, Fit(spr, sz), Fit(spr, sz * 0.4f), c, SortBands.Bursts + 10, additive, budget, drag, gravity);
                if (bit == null) break;
                bit.AlignToVel = stretch > 0f;
                bit.Stretch = stretch;
                ApplyBit(bit);
                got++;
            }
            return got;
        }

        // ------------------------------------------------------------ 적중

        void OnHit(SimWorld w, in SimEvent e)
        {
            string el = e.Id ?? Ids.Element.Neutral;
            _enemyElement[e.EntityId] = el;
            bool crit = (e.B & DamageFlags.Crit) != 0;
            bool weak = (e.B & DamageFlags.Weak) != 0;
            float big = Or(J("hit", "big_damage"), 30f);
            float k = 1f + Mathf.Clamp01(e.A / big) * J("hit", "damage_scale");
            if (crit) k *= Or(J("hit", "crit_scale"), 1.4f);
            Color c1 = ElSecondary(el), c0 = ElPrimary(el);

            Glow(e.Pos, J("hit", "glow_from_cells") * k, J("hit", "glow_to_cells") * k, Or(J("hit", "glow_seconds"), 0.12f), WithA(c1, J("hit", "glow_alpha")),
                SortBands.Bursts + 8);
            // 사용자 그림 hit_spark(32px 4장): 속성에 맞는 색 시트, 흰 시트는 속성 보조색으로 입힌다
            bool tint;
            var spark = CombatFxArt.Anim(_art, CombatFxArt.HitSparkFor(el, out tint), Or(J("hit", "sheet_fps"), 22f));
            if (spark != null)
            {
                var f0 = spark.At(0f);
                float sc = Fit(f0, Or(J("hit", "sheet_cells"), 0.9f) * k);
                var sb = Spawn(null, spark, e.Pos, Vector2.zero, spark.Duration, sc, sc * 1.1f, crit ? Color.white : (tint ? c1 : Color.white), SortBands.Bursts + 9, true);
                if (sb != null) { sb.Rot = Random.Range(0, 4) * 90f; sb.Hold = 0.7f; ApplyBit(sb); }
            }

            // 쏜 쪽에서 적 쪽으로 튀는 불꽃
            Vector2 dir = Vector2.zero;
            var p = Player(w, e.Slot);
            if (p != null) dir = e.Pos - p.Pos;
            int n = Count(J("hit", "sparks") + (crit ? J("hit", "crit_sparks") : 0f) + (weak ? J("hit", "weak_sparks") : 0f));
            Sparks(ParticleBudgetConfig.CatHit, n, e.Pos, dir, Or(J("hit", "spread_deg"), 70f), J("hit", "speed_min"), J("hit", "speed_max"),
                Or(J("hit", "spark_seconds"), 0.25f), Or(J("hit", "spark_cells"), 0.1f) * Mathf.Sqrt(k), crit ? Color.white : c1, c0,
                J("hit", "drag"), 0f, J("hit", "stretch"), true);
            if (crit)
            {
                Shockwave(e.Pos, 0.1f, Or(J("hit", "crit_ring_cells"), 0.9f), Or(J("hit", "crit_ring_seconds"), 0.16f), WithA(Color.white, 0.9f), 0f, true);
                Punch(J("hit", "crit_punch"));
            }
        }

        static Color WithA(Color c, float a) { c.a = a > 0f ? a : c.a; return c; }

        // ------------------------------------------------------------ 처치

        void CacheEnemies(SimWorld w)
        {
            if (w == null) return;
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var en = w.Enemies[i];
                if (en == null) continue;
                _enemies[en.Id] = new EnemyInfo { DefId = en.DefId, Radius = en.BodyRadius, Elite = en.IsElite, Pos = en.Pos, FlipX = en.Facing.x < 0f };
            }
            // 오래된 것 정리(가끔)
            if ((_stamp & 63) == 0 && _enemies.Count > w.Enemies.Count + 64)
            {
                _tmp.Clear();
                foreach (var kv in _enemies) if (w.FindEnemy(kv.Key) == null) _tmp.Add(kv.Key);
                for (int i = 0; i < _tmp.Count; i++) { _enemies.Remove(_tmp[i]); _enemyElement.Remove(_tmp[i]); }
            }
        }

        void OnKill(SimWorld w, in SimEvent e)
        {
            if (e.B == 2) return; // 보스는 BossKilled에서(몸은 EnemyLayer가 따로 무너뜨린다)
            string el;
            if (!_enemyElement.TryGetValue(e.EntityId, out el)) el = ElementOfSlot(w, 0);
            _enemyElement.Remove(e.EntityId);
            EnemyInfo info;
            bool known = _enemies.TryGetValue(e.EntityId, out info);
            var live = w != null ? w.FindEnemy(e.EntityId) : null;
            if (live != null) { info = new EnemyInfo { DefId = live.DefId, Radius = live.BodyRadius, Elite = live.IsElite, Pos = live.Pos, FlipX = live.Facing.x < 0f }; known = true; }
            _enemies.Remove(e.EntityId);
            bool elite = e.B == 1 || (known && info.Elite);
            bool noReward = e.B == 3;

            // 연속 처치: 창 안에 들어오면 하나씩 쌓이고, 쌓일수록 크게
            float window = Or(J("streak", "window_seconds"), 1.2f);
            _streak = _s.Time - _lastKillTime <= window ? _streak + 1 : 1;
            _lastKillTime = _s.Time;
            int cap = Mathf.Max(1, Mathf.RoundToInt(Or(J("streak", "cap"), 12f)));
            float mul = 1f + J("streak", "growth") * (Mathf.Min(_streak, cap) - 1);
            if (elite) mul *= Or(J("kill", "elite_scale"), 1.8f);
            if (noReward) mul *= 0.7f;
            Vector2 pos = e.Pos;
            Color c0 = ElPrimary(el), c1 = ElSecondary(el);
            string fxEl = ArtAliases.ElementFxKey(el);
            if (fxEl == null || el == Ids.Element.Neutral) fxEl = _t.S("fx_element_for_neutral") ?? "arcane";

            // ① 흰 실루엣이 부풀며 터진다
            if (known && _art != null && info.DefId != null)
            {
                var clip = _art.MonsterClip(info.DefId, "hit") ?? _art.MonsterClip(info.DefId, "idle");
                var spr = clip != null ? clip.At(0f) : null;
                if (spr != null)
                {
                    var b = Spawn(spr, null, pos, Vector2.zero, Or(J("kill", "silhouette_seconds"), 0.16f), 1f, Or(J("kill", "silhouette_scale"), 1.5f),
                        new Color(1f, 1f, 1f, 0.95f), SortBands.Bursts - 5, false);
                    if (b != null) { b.Flash = 1f; b.FlipX = info.FlipX; ApplyBit(b); }
                }
            }
            Vector2 center = pos - new Vector2(0f, known ? Mathf.Max(0.2f, info.Radius) : 0.3f); // 몸 가운데(발 위)

            // ② 속성 폭발 그림(64px 큰 불꽃, 엘리트는 256px 폭발)
            var burst = _art != null ? _art.Effect("player", (elite ? "explosion_" : "hit_large_") + fxEl) : null;
            if (burst != null)
            {
                float cells = (elite ? J("kill", "elite_burst_cells") : J("kill", "burst_cells")) * mul;
                var first = burst.At(0f);
                var b = Spawn(null, burst, center, Vector2.zero, burst.Duration > 0f ? burst.Duration : 0.3f, Fit(first, cells), Fit(first, cells * 1.15f),
                    Color.white, SortBands.Bursts + 2, true, null, 0f, 0f, true);
                if (b != null) { b.Rot = Random.Range(0f, 360f); b.Hold = 0.6f; ApplyBit(b); }
            }
            // ②' 사용자 그림 kill_burst(64px 6장, 일반 살구색 / 엘리트 금색)
            var kb = CombatFxArt.Anim(_art, elite ? CombatFxArt.KillElite : CombatFxArt.KillNormal, Or(J("kill", "sheet_fps"), 20f));
            if (kb != null)
            {
                var f0 = kb.At(0f);
                float cells = (elite ? Or(J("kill", "elite_sheet_cells"), 3.2f) : Or(J("kill", "sheet_cells"), 1.8f)) * mul;
                var b = Spawn(null, kb, center, Vector2.zero, kb.Duration, Fit(f0, cells), Fit(f0, cells * 1.1f), Color.white, SortBands.Bursts + 3, false, null, 0f, 0f, true);
                if (b != null) { b.Hold = 0.75f; ApplyBit(b); }
            }
            // ③ 빛 덩이와 충격파
            Glow(center, J("kill", "glow_from_cells") * mul, J("kill", "glow_to_cells") * mul, Or(J("kill", "glow_seconds"), 0.18f), WithA(c1, J("kill", "glow_alpha")),
                SortBands.Bursts + 1);
            Shockwave(center, 0.15f, Or(J("kill", "ring_cells"), 1.3f) * mul, Or(J("kill", "ring_seconds"), 0.3f), WithA(c1, J("kill", "ring_alpha")));
            if (_streak >= Mathf.Max(2, Mathf.RoundToInt(Or(J("streak", "second_ring_from"), 3f))))
                Shockwave(center, 0.1f, Or(J("kill", "ring_cells"), 1.3f) * mul * 0.6f, Or(J("kill", "ring_seconds"), 0.3f), WithA(Color.white, 0.8f), 0.05f);

            // ④ 파편(중력으로 떨어진다)과 위로 오르는 불씨
            int debris = EffectOptionRules.KillShardCount(_s.Options.KillShards,
                Count(J("kill", "debris") + J("streak", "debris_per_kill") * (Mathf.Min(_streak, cap) - 1) + (elite ? J("kill", "elite_debris") : 0f)));
            Sparks(ParticleBudgetConfig.CatKill, debris, center, Vector2.zero, 180f, J("kill", "debris_speed_min") * Mathf.Sqrt(mul), J("kill", "debris_speed_max") * Mathf.Sqrt(mul),
                Or(J("kill", "debris_seconds"), 0.6f), Or(J("kill", "debris_cells"), 0.12f), c0, Color.white, J("kill", "debris_drag"), J("kill", "debris_gravity"),
                J("kill", "debris_stretch"), false);
            int embers = EffectOptionRules.KillShardCount(_s.Options.KillShards, Count(J("kill", "embers") * mul));
            for (int i = 0; i < embers; i++)
            {
                var s = ProcSprites.SoftDot;
                Vector2 v = new Vector2(Random.Range(-1.2f, 1.2f), -Random.Range(1.5f, 3.5f));
                float sz = Or(J("kill", "ember_cells"), 0.22f) * Random.Range(0.6f, 1.2f);
                if (Spawn(s, null, center + Random.insideUnitCircle * 0.3f, v, Or(J("kill", "ember_seconds"), 0.8f) * Random.Range(0.7f, 1.2f), Fit(s, sz), Fit(s, sz * 0.2f),
                    Color.Lerp(c1, c0, Random.value), SortBands.Bursts + 9, true, ParticleBudgetConfig.CatKill, 1.5f, -1.5f) == null) break;
            }
            if (elite)
            {
                // 엘리트: 연기 둘 더, 큰 흔들림
                var smoke = _art != null ? _art.Effect("enemy", _t.S("kill", "smoke_key") ?? "death_smoke") : null;
                if (smoke != null)
                    for (int i = 0; i < 2; i++)
                    {
                        var f = smoke.At(0f);
                        Spawn(null, smoke, center + Random.insideUnitCircle * 0.6f, Random.insideUnitCircle * 0.6f, smoke.Duration > 0f ? smoke.Duration * 1.5f : 0.5f,
                            Fit(f, 1.6f), Fit(f, 2.2f), new Color(1f, 1f, 1f, 0.8f), SortBands.Bursts, false, null, 2f, -0.5f, true);
                    }
                ShakePx(J("kill", "elite_shake_px"));
                HitStop(J("kill", "elite_hitstop"), true);
                Punch(J("kill", "elite_punch"));
            }
            else Punch(J("kill", "punch") * mul);

            // ⑤ 연속 처치 마디(5, 10, 15 …): 흔들림·그림 정지·줌 펀치·큰 고리
            int every = Mathf.Max(2, Mathf.RoundToInt(Or(J("streak", "milestone_every"), 5f)));
            if (_streak % every == 0)
            {
                ShakePx(J("streak", "milestone_shake_px"));
                HitStop(J("streak", "milestone_hitstop"), true);
                Punch(J("streak", "milestone_punch"));
                var pp = Player(w, 0);
                Vector2 at = pp != null ? pp.Pos : center;
                Shockwave(at, 0.3f, Or(J("streak", "milestone_ring_cells"), 3.5f), Or(J("streak", "milestone_ring_seconds"), 0.45f), WithA(JCol("streak", "milestone_color", Color.white), 0.85f));
                Sparks(ParticleBudgetConfig.CatKill, Count(J("streak", "milestone_sparks")), at, Vector2.zero, 180f, 6f, 12f, 0.5f, 0.12f,
                    JCol("streak", "milestone_color", Color.white), c1, 3f, 0f, 0.05f, true);
            }
        }

        void OnBossKilled(in SimEvent e)
        {
            Vector2 pos = e.Pos;
            string el = ElementOfSlot(_w, 0);
            Color c0 = ElPrimary(el), c1 = ElSecondary(el);
            string fxEl = ArtAliases.ElementFxKey(el);
            if (fxEl == null || el == Ids.Element.Neutral) fxEl = _t.S("fx_element_for_neutral") ?? "arcane";
            var ex = _art != null ? _art.Effect("player", "explosion_" + fxEl) : null;
            if (ex != null)
                for (int i = 0; i < 3; i++)
                {
                    var f = ex.At(0f);
                    var b = Spawn(null, ex, pos + Random.insideUnitCircle * 1.2f * i, Vector2.zero, ex.Duration > 0f ? ex.Duration * 1.3f : 0.5f,
                        Fit(f, Or(J("boss_kill", "burst_cells"), 6f) * (1f - 0.2f * i)), Fit(f, Or(J("boss_kill", "burst_cells"), 6f) * 1.2f), Color.white,
                        SortBands.Bursts + 2, false, null, 0f, 0f, true);
                    if (b != null) { b.Delay = i * 0.12f; b.Hold = 0.6f; ApplyBit(b); }
                }
            for (int i = 0; i < 3; i++)
                Shockwave(pos, 0.3f, Or(J("boss_kill", "ring_cells"), 8f) * (1f - 0.25f * i), Or(J("boss_kill", "ring_seconds"), 0.7f), WithA(i == 1 ? c1 : Color.white, 0.9f), i * 0.1f);
            Sparks(ParticleBudgetConfig.CatKill, Count(J("boss_kill", "debris")), pos, Vector2.zero, 180f, 6f, 16f, 0.9f, 0.16f, c0, Color.white, 2f, 8f, 0.04f, false);
            Sparks(ParticleBudgetConfig.CatArt, Count(J("boss_kill", "sparks")), pos, Vector2.zero, 180f, 8f, 20f, 0.7f, 0.18f, c1, Color.white, 3f, 0f, 0.06f, true);
            ScreenFlash(JCol("boss_kill", "flash_color", Color.white), J("boss_kill", "flash_opacity"));
            ShakePx(J("boss_kill", "shake_px"));
            if (_vc.HitStop != null && J("boss_kill", "hitstop") > 0f) _vc.HitStop(J("boss_kill", "hitstop"));
            Punch(J("boss_kill", "punch"));
            var cf = CameraFx.Of(_vc);
            if (cf != null) { var v = _vc.ToView(pos); cf.Focus(new Vector2(v.x, v.y), Or(J("boss_kill", "focus_weight"), 0.6f), Or(J("boss_kill", "focus_seconds"), 1.2f)); }
        }

        // ------------------------------------------------------------ 내 탄

        void OnShot(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            if (p == null) return;
            string el = p.Def != null ? p.Def.Element : Ids.Element.Neutral;
            Vector2 f = Dir8Util.ToSimVector(p.Facing);
            Vector2 tip = p.Pos + f * Or(J("muzzle", "tip_cells"), 0.55f) - new Vector2(0f, J("muzzle", "lift_cells"));
            bool crit = e.B != 0;
            float k = crit ? Or(J("muzzle", "crit_scale"), 1.6f) : 1f;
            Glow(tip, J("muzzle", "glow_from_cells") * k, J("muzzle", "glow_to_cells") * k, Or(J("muzzle", "seconds"), 0.08f), WithA(ElSecondary(el), J("muzzle", "glow_alpha")),
                SortBands.PlayerShots + 5);
            bool tint;
            var mz = CombatFxArt.Anim(_art, CombatFxArt.MuzzleFor(el, out tint), Or(J("muzzle", "sheet_fps"), 30f));
            // 캐릭터 교체 묶음의 단계별 발사 효과(G16 character_fx — 제자 2 muzzle_band_N)가 있으면 그것(색을 입히지 않는다).
            string ownMuzzle = CharacterFx.Key(_t, p.CharId, "wand", "muzzle_" + ArtAliases.ElementFxKey(el), p.Wand != null ? p.Wand.EffectiveStage : 0);
            var own = CharacterFx.Own(_art, p.CharId, ownMuzzle);
            if (own != null) { mz = own; tint = false; }
            if (mz != null)
            {
                // 사용자 그림 muzzle_flash(32px 3장, 오른쪽 = 발사 방향, 손잡이 쪽 피벗)
                var f0 = mz.At(0f);
                float sc = Fit(f0, Or(J("muzzle", "sheet_cells"), 0.8f) * k);
                Vector2 vf = SimConst.DirToView(f);
                var mb = Spawn(null, mz, tip, Vector2.zero, mz.Duration, sc, sc, tint ? ElSecondary(el) : Color.white, SortBands.PlayerShots + 6, true);
                if (mb != null) { mb.Rot = Mathf.Atan2(vf.y, vf.x) * Mathf.Rad2Deg; mb.Hold = 0.8f; ApplyBit(mb); }
            }
            Sparks(ParticleBudgetConfig.CatShot, Count(J("muzzle", "sparks") * k), tip, f, Or(J("muzzle", "spread_deg"), 25f), J("muzzle", "speed_min"), J("muzzle", "speed_max"),
                Or(J("muzzle", "spark_seconds"), 0.12f), Or(J("muzzle", "spark_cells"), 0.08f), ElSecondary(el), ElPrimary(el), 8f, 0f, 0.06f, true);
            if (crit) Punch(J("muzzle", "crit_punch"));
        }

        void UpdateShotGlows(SimWorld w, float alpha, float dt)
        {
            if (w == null) return;
            float opacity = EffectOptionRules.MyBulletAlpha(1f, _s.Options.MyBulletOpacity);
            bool trail = _s.Options.BulletTrail != OptionValues.Off;
            float trailK = _s.Options.BulletTrail == OptionValues.Short ? 0.5f : 1f;
            int maxGlows = Mathf.Max(0, Mathf.RoundToInt(Or(J("shot_glow", "max"), 160f)));
            int shown = 0;
            for (int i = 0; i < w.Projectiles.Count; i++)
            {
                var pr = w.Projectiles[i];
                if (pr == null || pr.State == ProjectileState.Dead || pr.State == ProjectileState.Pending) continue;
                if (shown >= maxGlows) break;
                shown++;
                ShotGlow g;
                if (!_shots.TryGetValue(pr.Id, out g))
                {
                    g = new ShotGlow();
                    g.Glow = _pool.Rent(ProcSprites.SoftDot, SortBands.PlayerShots - 2, SpriteMaterialKind.SpriteAdditive);
                    if (trail) g.Streak = _pool.Rent(ProcSprites.SoftDot, SortBands.PlayerShots - 3, SpriteMaterialKind.Sprite);
                    _shots[pr.Id] = g;
                }
                g.Stamp = _stamp;
                string el = ElementOfSlot(w, pr.OwnerSlot);
                Color c0 = ElPrimary(el), c1 = ElSecondary(el);
                Vector2 sim = ViewMath.Lerp(pr.PrevPos, pr.Pos, alpha);
                Vector3 vp = _vc.ToView(sim);
                float pulse = 1f + Mathf.Sin(_clock * 18f + pr.Id) * 0.08f;
                float glowCells = Mathf.Clamp(pr.Radius * Or(J("shot_glow", "radius_mul"), 3.2f), J("shot_glow", "min_cells"), Or(J("shot_glow", "max_cells"), 1.6f)) * pulse;
                if (pr.IsCrit) glowCells *= 1.3f;
                if (g.Glow != null)
                {
                    g.Glow.transform.localPosition = vp;
                    float s = Fit(ProcSprites.SoftDot, glowCells);
                    g.Glow.transform.localScale = new Vector3(s, s, 1f);
                    g.Glow.color = WithA(c0, J("shot_glow", "alpha") * opacity * Mathf.Min(1f, Level + 0.3f));
                }
                Vector2 dir = pr.Dir.sqrMagnitude > 1e-6f ? pr.Dir.normalized : Vector2.zero;
                if (g.Streak != null)
                {
                    if (!trail || dir == Vector2.zero) g.Streak.enabled = false;
                    else
                    {
                        g.Streak.enabled = true;
                        float len = Mathf.Clamp(pr.Speed * J("shot_glow", "trail_seconds"), 0.2f, Or(J("shot_glow", "trail_max_cells"), 1.4f)) * trailK;
                        float width = glowCells * Or(J("shot_glow", "trail_width_mul"), 0.45f);
                        Vector2 back = sim - dir * (len * 0.5f);
                        Vector2 vd = SimConst.DirToView(dir);
                        var tr = g.Streak.transform;
                        tr.localPosition = _vc.ToView(back);
                        tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vd.y, vd.x) * Mathf.Rad2Deg);
                        tr.localScale = new Vector3(Fit(ProcSprites.SoftDot, len), Fit(ProcSprites.SoftDot, width), 1f);
                        g.Streak.color = WithA(c1, J("shot_glow", "trail_alpha") * opacity);
                    }
                }
                // 지나간 자리에 떨어지는 불티
                g.ShedAcc += dt * J("shot_glow", "shed_per_second") * Level;
                while (g.ShedAcc >= 1f)
                {
                    g.ShedAcc -= 1f;
                    var s = ProcSprites.SoftDot;
                    float sz = Or(J("shot_glow", "shed_cells"), 0.12f) * Random.Range(0.6f, 1.2f);
                    if (Spawn(s, null, sim - dir * 0.15f + Random.insideUnitCircle * 0.08f, -dir * Random.Range(0.3f, 1.2f) + Random.insideUnitCircle * 0.5f,
                        Or(J("shot_glow", "shed_seconds"), 0.25f), Fit(s, sz), Fit(s, sz * 0.2f), Color.Lerp(c0, c1, Random.value), SortBands.PlayerShots - 1, true,
                        ParticleBudgetConfig.CatShot, 3f) == null) { g.ShedAcc = 0f; break; }
                }
            }
            _tmp.Clear();
            foreach (var kv in _shots) if (kv.Value.Stamp != _stamp) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) { ReturnShot(_shots[_tmp[i]]); _shots.Remove(_tmp[i]); }
        }

        void ReturnShot(ShotGlow g)
        {
            if (_pool == null) return;
            if (g.Glow != null) _pool.Return(g.Glow);
            if (g.Streak != null) _pool.Return(g.Streak);
            g.Glow = g.Streak = null;
        }

        void OnExplosion(in SimEvent e)
        {
            string el = e.Id ?? Ids.Element.Neutral;
            float r = e.F > 0f ? e.F : Or(_t.F("explosion", "native_radius_cells"), 2f);
            Glow(e.Pos, r * 0.6f, r * Or(J("explosion", "glow_mul"), 2.2f), Or(J("explosion", "glow_seconds"), 0.2f), WithA(ElSecondary(el), J("explosion", "glow_alpha")),
                SortBands.Bursts + 1);
            Shockwave(e.Pos, r * 0.3f, r * Or(J("explosion", "ring_mul"), 1.3f), Or(J("explosion", "ring_seconds"), 0.3f), WithA(ElSecondary(el), 0.85f), 0f, true);
            Sparks(ParticleBudgetConfig.CatHit, Count(J("explosion", "sparks")), e.Pos, Vector2.zero, 180f, r * 3f, r * 6f, 0.4f, 0.12f, ElPrimary(el), Color.white, 4f, 0f, 0.05f, true);
            Punch(J("explosion", "punch"));
        }

        // ------------------------------------------------------------ 비기

        void OnArt(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            if (p == null) return;
            string el = p.Def != null ? p.Def.Element : Ids.Element.Neutral;
            var def = w != null && w.Content != null ? w.Content.Art(e.Id) : null;
            int diff = def != null ? def.Difficulty : 1;
            float k = 1f + J("art", "per_difficulty") * Mathf.Max(0, diff - 1);
            Color c0 = ElPrimary(el), c1 = ElSecondary(el);
            Vector2 pos = p.Pos;

            ScreenFlash(c1, J("art", "flash_opacity"));
            Glow(pos, 0.5f, Or(J("art", "glow_cells"), 4f) * k, Or(J("art", "glow_seconds"), 0.25f), WithA(c1, J("art", "glow_alpha")), SortBands.Bursts + 1);
            Shockwave(pos, 0.3f, Or(J("art", "ring_cells"), 5f) * k, Or(J("art", "ring_seconds"), 0.45f), WithA(c1, 0.95f));
            Shockwave(pos, 0.2f, Or(J("art", "ring_cells"), 5f) * 0.75f * k, Or(J("art", "ring_seconds"), 0.45f) * 0.9f, WithA(Color.white, 0.95f), 0.05f, true);
            Sparks(ParticleBudgetConfig.CatArt, Count(J("art", "sparks") * k), pos, Vector2.zero, 180f, J("art", "speed_min"), J("art", "speed_max"),
                Or(J("art", "spark_seconds"), 0.5f), Or(J("art", "spark_cells"), 0.14f), c1, c0, 3f, 0f, 0.06f, true);
            Punch(J("art", "punch") * Mathf.Min(1.5f, k));
            HitStop(J("art", "hitstop"), true);
        }

        void OnArtPulse(SimWorld w, in SimEvent e)
        {
            string el = ElementOfSlot(w, e.Slot);
            float r = e.F > 0f ? e.F : 1.5f;
            Shockwave(e.Pos, r * 0.2f, r * 1.1f, Or(J("art", "pulse_ring_seconds"), 0.3f), WithA(ElSecondary(el), 0.7f));
            Sparks(ParticleBudgetConfig.CatArt, Count(J("art", "pulse_sparks")), e.Pos, Vector2.zero, 180f, r * 2f, r * 4f, 0.35f, 0.1f, ElSecondary(el), ElPrimary(el), 4f, 0f, 0.05f, true);
            Punch(J("art", "pulse_punch"));
        }

        // ------------------------------------------------------------ 스침

        void OnGraze(SimWorld w, in SimEvent e)
        {
            var p = Player(w, e.Slot);
            if (p == null) return;
            Color c = JCol("graze", "color", new Color32(0x9F, 0xF6, 0xFF, 0xFF));
            Sparks(ParticleBudgetConfig.CatHit, Count(J("graze", "sparks")), p.Pos, e.Pos - p.Pos, 60f, 3f, 7f, 0.25f, 0.08f, c, Color.white, 6f, 0f, 0.05f, true);
        }

        // ------------------------------------------------------------ 드랍

        Color DropColor(DropKind k)
        {
            switch (k)
            {
                case DropKind.GoldCoin: return JCol("drops", "gold_color", new Color32(0xFF, 0xD2, 0x4A, 0xFF));
                case DropKind.HealthShard: return JCol("drops", "health_color", new Color32(0x8C, 0xFF, 0xB4, 0xFF));
                default: return JCol("drops", "material_color", new Color32(0x9F, 0xDC, 0xFF, 0xFF));
            }
        }

        void OnDropSpawned(in SimEvent e)
        {
            var c = DropColor((DropKind)e.A);
            Sparks(ParticleBudgetConfig.CatEnvironment, Count(J("drops", "spawn_stars")), e.Pos, Vector2.zero, 180f, 1.5f, 4f, 0.5f, 0.25f, c, Color.white, 4f, -1f, 0f, true, ProcSprites.Star);
            Shockwave(e.Pos, 0.1f, Or(J("drops", "spawn_ring_cells"), 0.8f), 0.3f, WithA(c, 0.8f));
        }

        void OnDropPicked(SimWorld w, in SimEvent e)
        {
            DropGlow d;
            Vector2 pos;
            if (_drops.TryGetValue(e.EntityId, out d)) pos = d.Pos;
            else { var p = Player(w, e.Slot); if (p == null) return; pos = p.Pos; }
            var c = DropColor((DropKind)e.A);
            Shockwave(pos, 0.1f, Or(J("drops", "pick_ring_cells"), 1.1f), 0.25f, WithA(c, 0.9f));
            Glow(pos, 0.4f, 1.4f, 0.15f, WithA(c, 0.8f), SortBands.Bursts + 1);
            for (int i = 0; i < Count(J("drops", "pick_stars")); i++)
            {
                var s = ProcSprites.Star;
                Vector2 v = new Vector2(Random.Range(-2f, 2f), -Random.Range(2f, 5f));
                float sz = Random.Range(0.2f, 0.35f);
                var b = Spawn(s, null, pos, v, Random.Range(0.35f, 0.6f), Fit(s, sz), Fit(s, sz * 0.3f), Color.Lerp(c, Color.white, 0.4f), SortBands.Bursts + 10, true,
                    ParticleBudgetConfig.CatEnvironment, 3f, 4f);
                if (b == null) break;
                b.RotVel = Random.Range(-360f, 360f);
            }
            Punch(J("drops", "pick_punch"));
        }

        void UpdateDropGlows(SimWorld w, float dt)
        {
            if (w == null) return;
            for (int i = 0; i < w.Drops.Count; i++)
            {
                var dr = w.Drops[i];
                if (dr == null || dr.Picked) continue;
                DropGlow g;
                if (!_drops.TryGetValue(dr.Id, out g))
                {
                    g = new DropGlow { Kind = dr.Kind, NextTwinkle = _clock + Random.Range(0.1f, 0.5f) };
                    g.Glow = _pool.Rent(ProcSprites.SoftDot, SortBands.Shadows + 5, SpriteMaterialKind.SpriteAdditive);
                    _drops[dr.Id] = g;
                }
                g.Stamp = _stamp;
                g.Pos = dr.Pos;
                var c = DropColor(dr.Kind);
                if (g.Glow != null)
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(_clock * 5f + dr.Id);
                    float cells = Or(J("drops", "glow_cells"), 1.1f) * (0.9f + 0.1f * pulse);
                    float s = Fit(ProcSprites.SoftDot, cells);
                    g.Glow.transform.localPosition = _vc.ToView(dr.Pos - new Vector2(0f, 0.15f));
                    g.Glow.transform.localScale = new Vector3(s, s, 1f);
                    g.Glow.color = WithA(c, J("drops", "glow_alpha") * pulse);
                }
                if (_clock >= g.NextTwinkle)
                {
                    g.NextTwinkle = _clock + Random.Range(J("drops", "twinkle_min_seconds"), Or(J("drops", "twinkle_max_seconds"), 0.7f));
                    var s = ProcSprites.Star;
                    float sz = Random.Range(0.18f, 0.32f);
                    var b = Spawn(s, null, dr.Pos + new Vector2(Random.Range(-0.3f, 0.3f), -Random.Range(0.1f, 0.5f)), new Vector2(0f, -0.3f), Random.Range(0.3f, 0.5f),
                        Fit(s, sz * 0.3f), Fit(s, sz), Color.Lerp(c, Color.white, 0.5f), SortBands.Bursts + 10, true, ParticleBudgetConfig.CatEnvironment);
                    if (b != null) { b.Hold = 0.4f; b.RotVel = 90f; }
                }
            }
            _tmp.Clear();
            foreach (var kv in _drops) if (kv.Value.Stamp != _stamp) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) { ReturnDrop(_drops[_tmp[i]]); _drops.Remove(_tmp[i]); }
        }

        void ReturnDrop(DropGlow g)
        {
            if (_pool != null && g.Glow != null) _pool.Return(g.Glow);
            g.Glow = null;
        }

        // ------------------------------------------------------------ 웨이브·보스

        Vector2 PlayersCenter(SimWorld w)
        {
            if (w == null) return new Vector2(12f, 7f);
            Vector2 s = Vector2.zero;
            int n = 0;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p == null || !p.Active) continue;
                s += p.Pos;
                n++;
            }
            return n > 0 ? s / n : new Vector2(w.ArenaWidth * 0.5f, w.ArenaHeight * 0.5f);
        }

        void OnWaveStarted(SimWorld w)
        {
            Vector2 at = PlayersCenter(w);
            Color c = JCol("wave_start", "color", new Color32(0xE8, 0xF4, 0xFF, 0xFF));
            Shockwave(at, 0.5f, Or(J("wave_start", "ring_cells"), 8f), Or(J("wave_start", "ring_seconds"), 0.7f), WithA(c, 0.8f));
            Shockwave(at, 0.3f, Or(J("wave_start", "ring_cells"), 8f) * 0.6f, Or(J("wave_start", "ring_seconds"), 0.7f), WithA(c, 0.6f), 0.12f);
            Sparks(ParticleBudgetConfig.CatArt, Count(J("wave_start", "sparks")), at, Vector2.zero, 180f, 4f, 10f, 0.6f, 0.12f, c, Color.white, 3f, 0f, 0.05f, true);
            Punch(J("wave_start", "punch"));
        }

        void OnWaveCleared(SimWorld w)
        {
            Vector2 at = PlayersCenter(w);
            Color gold = JCol("wave_clear", "color", new Color32(0xFF, 0xE0, 0x8A, 0xFF));
            ScreenFlash(gold, J("wave_clear", "flash_opacity"));
            Shockwave(at, 0.4f, Or(J("wave_clear", "ring_cells"), 9f), Or(J("wave_clear", "ring_seconds"), 0.8f), WithA(gold, 0.9f));
            Shockwave(at, 0.3f, Or(J("wave_clear", "ring_cells"), 9f) * 0.55f, Or(J("wave_clear", "ring_seconds"), 0.8f), WithA(Color.white, 0.8f), 0.15f);
            // 금빛 분수: 위로 솟았다 떨어진다
            int n = Count(J("wave_clear", "fountain"));
            for (int i = 0; i < n; i++)
            {
                var s = i % 3 == 0 ? ProcSprites.Star : ProcSprites.SoftDot;
                Vector2 v = new Vector2(Random.Range(-4f, 4f), -Random.Range(6f, 11f));
                float sz = Random.Range(0.15f, 0.32f);
                var b = Spawn(s, null, at, v, Random.Range(0.9f, 1.4f), Fit(s, sz), Fit(s, sz * 0.5f), Color.Lerp(gold, Color.white, Random.value * 0.6f),
                    SortBands.Bursts + 10, true, ParticleBudgetConfig.CatArt, 1.2f, 12f);
                if (b == null) break;
                b.RotVel = Random.Range(-240f, 240f);
                b.Hold = 0.5f;
            }
            // 사용자 그림 level_wave_pillar(64×256 4장, 발밑 피벗) — 플레이어 자리에 빛기둥
            var pillar = CombatFxArt.Anim(_art, CombatFxArt.WavePillar, Or(J("wave_clear", "pillar_fps"), 10f));
            if (pillar != null)
            {
                float sc = Or(J("wave_clear", "pillar_scale"), 1f);
                var pb = Spawn(null, pillar, at, Vector2.zero, Or(J("wave_clear", "pillar_seconds"), 1.0f), sc * 0.8f, sc, Color.white, SortBands.Bursts + 6, false, null, 0f, 0f, true);
                if (pb != null) { pb.Hold = 0.6f; ApplyBit(pb); }
            }
            Punch(J("wave_clear", "punch"));
            ShakePx(J("wave_clear", "shake_px"));
        }

        void OnBossIntro(SimWorld w, in SimEvent e)
        {
            var en = w != null ? w.FindEnemy(e.EntityId) : null;
            Vector2 pos = en != null ? en.Pos : PlayersCenter(w);
            Color c = JCol("boss_intro", "color", new Color32(0xC9, 0xD0, 0xDC, 0xFF));
            for (int i = 0; i < 3; i++)
                Shockwave(pos, 0.5f, Or(J("boss_intro", "ring_cells"), 7f), Or(J("boss_intro", "ring_seconds"), 0.8f), WithA(c, 0.7f), i * 0.25f);
            ShakePx(J("boss_intro", "shake_px"));
            Punch(J("boss_intro", "punch"));
        }
    }
}
