using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 기본 공격 유형 15종과 고유 비기 강화의 그림 층(AddEffects에서 JuiceLayer 뒤). 정본: Docs/12-attack-styles.md 3·4절 SimEvent 표.
    /// 그림은 Resources/Art/effects/styles(묶음 styles.char.N·styles.common, Tools/godot-fx)와 다시 쓰는 있는 그림(styles.reuse, 기존 효과 묶음).
    /// - 상태로 그리는 것(매 프레임 탄 상태를 읽는다): 포탄 포물선·그림자·떨어질 자리, 불바닥 반복, 룬 지뢰 반복, 불꽃 방사·눈보라 부채꼴 반복(발사 중 이어짐),
    ///   추적탄 둘레, 화상·냉기 겹의 불씨·눈송이.
    /// - 이벤트로 그리는 것(한 번): 착탄·낙뢰·룬·고드름 폭발, 그림자 파동·낫 휩쓸기, 낙뢰·고드름 예고 → 낙하, 사슬·지짐 번개 선, 튐 불티, 깨짐, 갈라짐, 비기 단계.
    /// 지키는 것(JuiceLayer와 같다): 입자는 입자 예산(분류)·프레임 상한(우선순위)을 거친다. 큰 반투명 그림은 누적 가림 셈(예고 위 옅어짐).
    /// '내 이펙트 불투명도'를 곱한 뒤 자리 상한. 흔들림·그림 정지·줌 펀치는 FxGovernor(창·상한·군중 감쇠)와 호스트(연출 감도·설정)를 거친다.
    /// 플레이어 쪽 색은 위험색이 되지 않게 고친다(PlayerColorGuard + JuiceLayer.Safe). 유형 그림 자체는 속성색만 쓴다(그림 작업의 규칙).
    /// 수치는 Resources/DataSolo/Juice.json "styles"(표시 전용, 솔로 임시).
    /// </summary>
    public sealed partial class StyleFxLayer : IWorldViewLayer
    {
        sealed class Bit
        {
            public SpriteRenderer Sr;
            public SpriteAnim Anim;
            public Sprite Sprite;
            public int Token, Occ;
            public float OccR;
            public Vector2 Pos, Vel;
            /// <summary>땅 위 높이(칸, 화면 위쪽). 포물선·튀는 파편·떠 있는 책.</summary>
            public float Lift, LiftVel, LiftGrav;
            public float Drag;
            public float Age, Life, Delay;
            public float Sx0, Sy0, Sx1, Sy1;
            public float Rot, RotVel;
            public bool AlignToVel;
            public Color Color;
            public float Alpha;
            public float FadeIn, FadeOut;
            public bool FlipX;
            public float AnimSpeed = 1f;
            /// <summary>Tiled로 늘이는 선(칸, 0 = 안 함).</summary>
            public float TiledLength, TiledHeight;
            /// <summary>매 프레임 자리를 다시 받는다(null = 제자리).</summary>
            public System.Func<Vector2?> Follow;
            public bool Persistent;
        }

        readonly EffectsShared _s;
        readonly EffectLayer _fx;
        readonly List<Bit> _bits = new List<Bit>();
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        ViewContext _vc;
        ISpritePool _pool;
        IArtLibrary _art;
        EffectsTuning _j;
        EffectsTuning _t;
        SimWorld _w;
        float _clock;
        float _alpha = 1f;
        readonly Dictionary<string, SpriteAnim> _clips = new Dictionary<string, SpriteAnim>();
        readonly Dictionary<int, string> _shotBlock = new Dictionary<int, string>();

        public StyleFxLayer(EffectsShared shared, EffectLayer effects)
        {
            _s = shared ?? new EffectsShared();
            _fx = effects;
        }

        // ---- 점검용 셈
        public int LiveBits => _bits.Count;
        public int LobViews => _lobs.Count;
        public int ConeStreams => _streams.Count;
        public int GroundViews => _grounds.Count;
        public int StageEvents { get; private set; }
        public int StyleEvents { get; private set; }
        /// <summary>그린 것의 이름(점검·스크린숏 기록용, 최근 64개).</summary>
        public readonly List<string> Drawn = new List<string>();

        public static string Group(int character) => ArtLibrary.StyleGroupPrefix + "char." + character;
        static readonly string G1 = Group(1), G2 = Group(2), G3 = Group(3), G4 = Group(4), G5 = Group(5);
        const string GCommon = ArtLibrary.StyleCommonGroup, GReuse = ArtLibrary.StyleReuseGroup;

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
            try { root = content != null ? content.SoloExtra(JuiceLayer.JuiceName) : null; }
            catch (System.Exception) { root = null; }
            _j = new EffectsTuning(root);
            _clips.Clear();
            BuildDim();
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_vc == null || _pool == null) return;
            _w = w;
            switch (e.Type)
            {
                case SimEventType.ProjectileSpawned:
                    if (e.Id != null && e.Id.StartsWith("block.core.", System.StringComparison.Ordinal)) _shotBlock[e.EntityId] = e.Id;
                    break;
                case SimEventType.ProjectileExpired: OnShotExpired(e); break;
                case SimEventType.StyleLobLaunched: StyleEvents++; break; // 포물선은 상태로 그린다(Render)
                case SimEventType.StyleBlast: StyleEvents++; OnBlast(w, e); break;
                case SimEventType.StyleGroundSpawned: StyleEvents++; OnGroundSpawned(w, e); break;
                case SimEventType.StyleGroundExpired: StyleEvents++; OnGroundExpired(e); break;
                case SimEventType.StyleConeStarted: StyleEvents++; OnConeStarted(w, e); break;
                case SimEventType.StyleStrikeWarned: StyleEvents++; OnStrikeWarned(w, e); break;
                case SimEventType.StyleRicochet: StyleEvents++; OnRicochet(e); break;
                case SimEventType.StyleZap: StyleEvents++; OnZap(e); break;
                case SimEventType.StyleSplit: StyleEvents++; OnSplit(e); break;
                case SimEventType.StyleShatter: StyleEvents++; OnShatter(e); break;
                case SimEventType.StyleStatus: StyleEvents++; OnStatus(e); break;
                case SimEventType.StyleMineArmed: StyleEvents++; OnMineArmed(e); break;
                case SimEventType.ChainLightningArc: OnChainArc(w, e); break;
                case SimEventType.ArtStage: StageEvents++; OnArtStage(w, e); break;
            }
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (_vc == null || _pool == null) return;
            _w = w;
            _alpha = alpha;
            _clock += dt;
            if (w != null)
            {
                SyncLobs(w, alpha, dt);
                SyncGrounds(w, dt);
                SyncStreams(w, alpha, dt);
                SyncSweeps(w, alpha, dt);
                SyncAuras(w, alpha, dt);
                SyncStatuses(w, dt);
                SyncArtHolds(w, dt);
            }
            UpdateBits(dt);
            UpdateDim(dt);
        }

        public void Dispose()
        {
            ClearAll();
            DisposeDim();
            _vc = null;
            _pool = null;
        }

        void ClearAll()
        {
            for (int i = _bits.Count - 1; i >= 0; i--) KillBit(i);
            foreach (var kv in _lobs) ReturnLob(kv.Value);
            _lobs.Clear();
            _grounds.Clear();
            _streams.Clear();
            _sweeps.Clear();
            _auras.Clear();
            _statusNext.Clear();
            _shotBlock.Clear();
            ClearArtHolds();
            Drawn.Clear();
        }

        // ------------------------------------------------------------ 값

        /// <summary>장식(입자·흔들림 덤)의 배율: 연출 감도 전체 1 / 줄임 0.5 / 최소 0. 유형의 몸 그림(부채꼴·폭발·예고)은 최소에서도 그린다(어디를 때리는지 보여야 한다).</summary>
        float Level
        {
            get
            {
                int lv = _s.EffectsLevel;
                if (lv >= 2) return 0f;
                return lv == 1 ? Or(J("reduced_scale"), 0.5f) : 1f;
            }
        }

        float J(params string[] path) => _j != null ? _j.F(path) : 0f;
        float S(params string[] path)
        {
            var p = new string[path.Length + 1];
            p[0] = "styles";
            for (int i = 0; i < path.Length; i++) p[i + 1] = path[i];
            return _j != null ? _j.F(p) : 0f;
        }
        string SStr(params string[] path)
        {
            var p = new string[path.Length + 1];
            p[0] = "styles";
            for (int i = 0; i < path.Length; i++) p[i + 1] = path[i];
            return _j != null ? _j.S(p) : null;
        }
        static float Or(float v, float fallback) => v > 0f ? v : fallback;
        int Count(float n, FxPriority p) => Mathf.Max(0, Mathf.RoundToInt(n * Level * (_s.Governor != null ? _s.Governor.CountScale(p) : 1f)));
        double NowSec => _w != null ? _w.Tick * (double)SimConst.TickSeconds : 0d;
        float TickNow(float alpha) => _w != null ? _w.Tick - 1 + alpha : 0f;

        Color Guard(Color c, string where)
        {
            if (_s.ColorGuard != null) _s.ColorGuard.Check(c, where);
            return JuiceLayer.Safe(c);
        }

        Color ElPrimary(string el) => Guard(_t.ElementPrimary(el ?? Ids.Element.Neutral), "style.primary." + el);
        Color ElSecondary(string el) => Guard(_t.ElementSecondary(el ?? Ids.Element.Neutral), "style.secondary." + el);
        Color Col(string key, Color fallback) => Guard(EffectsTuning.ParseColor(SStr("colors", key), fallback), "Juice.styles.colors." + key);
        static Color WithA(Color c, float a) { c.a = a; return c; }

        PlayerState Player(SimWorld w, int slot) => w != null && slot >= 0 && slot < w.Players.Length ? w.Players[slot] : null;

        BlockDef BlockOf(string id) => id != null && _w != null && _w.Content != null ? _w.Content.Block(id) : null;

        ProjectileEntity FindShot(int id)
        {
            if (_w == null) return null;
            var list = _w.Projectiles;
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id && !list[i].Dead) return list[i];
            return null;
        }

        void Note(string what)
        {
            if (Drawn.Count >= 64) Drawn.RemoveAt(0);
            Drawn.Add(what);
        }

        // ------------------------------------------------------------ 그림 찾기

        /// <summary>효과 그림(없으면 null — 대체 회색 그림을 내지 않는다).</summary>
        SpriteAnim A(string group, string key)
        {
            if (_art == null || key == null || _art.EffectInfo(group, key) == null) return null;
            return _art.Effect(group, key);
        }

        /// <summary>manifest clips의 이름 붙은 구간(끝 포함)을 따로 된 애니로. fps ≤ 0이면 원래 fps.</summary>
        SpriteAnim Clip(string group, string key, string clip, float fps = 0f, bool loop = false)
        {
            string ck = group + "|" + key + "|" + clip + "|" + fps.ToString(System.Globalization.CultureInfo.InvariantCulture) + (loop ? "L" : "");
            SpriteAnim a;
            if (_clips.TryGetValue(ck, out a)) return a;
            var info = _art != null ? _art.EffectInfo(group, key) : null;
            var all = A(group, key);
            int[] r;
            if (all == null || info == null || info.Clips == null || !info.Clips.TryGetValue(clip, out r) || all.Frames == null) { _clips[ck] = null; return null; }
            int from = Mathf.Clamp(r[0], 0, all.Frames.Length - 1), to = Mathf.Clamp(r[1], from, all.Frames.Length - 1);
            var frames = new Sprite[to - from + 1];
            for (int i = 0; i < frames.Length; i++) frames[i] = all.Frames[from + i];
            a = new SpriteAnim { Key = ck, Frames = frames, Fps = fps > 0f ? fps : all.Fps, Loop = loop };
            _clips[ck] = a;
            return a;
        }

        /// <summary>같은 그림을 다른 fps·반복으로(원래 애니는 바꾸지 않는다).</summary>
        SpriteAnim Retime(SpriteAnim src, float fps, bool loop)
        {
            if (src == null) return null;
            string ck = "rt|" + src.Key + "|" + fps.ToString(System.Globalization.CultureInfo.InvariantCulture) + (loop ? "L" : "");
            SpriteAnim a;
            if (_clips.TryGetValue(ck, out a)) return a;
            a = new SpriteAnim { Key = ck, Frames = src.Frames, Fps = fps, Loop = loop };
            _clips[ck] = a;
            return a;
        }

        static float NativeW(SpriteAnim a) { var f = a != null ? a.At(0f) : null; return f != null ? f.rect.width / Mathf.Max(1f, f.pixelsPerUnit) : 1f; }
        static float NativeH(SpriteAnim a) { var f = a != null ? a.At(0f) : null; return f != null ? f.rect.height / Mathf.Max(1f, f.pixelsPerUnit) : 1f; }

        // ------------------------------------------------------------ 조각 하나

        Bit Spawn(SpriteAnim anim, Sprite sprite, Vector2 simPos, float life, float sx, float sy, Color color, int order,
            bool occlusion = false, string budget = null, FxPriority priority = FxPriority.Decor, bool additive = false)
        {
            if (life <= 0f || _pool == null) return null;
            Sprite first = anim != null ? anim.At(0f) : sprite;
            if (first == null) return null;
            int token = 0;
            if (budget != null)
            {
                if (_s.Governor != null && !_s.Governor.AllowParticles(priority)) return null;
                if (_s.Budget == null || !_s.Budget.TryAcquire(budget, _s.Time, life, out token)) return null;
            }
            float alpha = color.a;
            int occ = 0;
            float occR = 0f;
            // 땅 띠(예고 채움 아래)에 까는 것은 적 예고·적 탄을 가리지 않는다 → 누적 가림 셈에 넣지 않는다(셈을 바닥 무늬가 먼저 써 버리지 않게).
            if (order < SortBands.TelegraphFill) occlusion = false;
            if (occlusion && _s.Occlusion != null)
            {
                occR = Mathf.Max(first.rect.width * Mathf.Abs(sx), first.rect.height * Mathf.Abs(sy)) / Mathf.Max(1f, first.pixelsPerUnit) * 0.5f;
                alpha = _s.Occlusion.Admit(simPos, occR, alpha);
                if (alpha <= 0f) { if (token != 0) _s.Budget.Release(token); return null; }
                occ = _s.Occlusion.Register(simPos, occR, alpha);
            }
            float shownCells = Mathf.Max(first.rect.width * Mathf.Abs(sx), first.rect.height * Mathf.Abs(sy)) / Mathf.Max(1f, first.pixelsPerUnit);
            bool add = additive && shownCells < 2f;
            var mat = EffectOptionRules.MaterialFor(Mathf.RoundToInt(first.rect.width), Mathf.RoundToInt(first.rect.height), add,
                _t.I("additive_max_px") > 0 ? _t.I("additive_max_px") : 32);
            var sr = _pool.Rent(first, order, mat);
            if (sr == null)
            {
                if (token != 0) _s.Budget.Release(token);
                if (occ != 0) _s.Occlusion.Unregister(occ);
                return null;
            }
            var b = new Bit
            {
                Sr = sr, Anim = anim, Sprite = sprite, Token = token, Occ = occ, OccR = occR, Pos = simPos, Life = life,
                Sx0 = sx, Sy0 = sy, Sx1 = sx, Sy1 = sy, Color = color, Alpha = alpha
            };
            _bits.Add(b);
            ApplyBit(b);
            return b;
        }

        /// <summary>계속 그리는 조각(Release로 흐려지며 끝난다).</summary>
        Bit Hold(SpriteAnim anim, Sprite sprite, Vector2 simPos, float sx, float sy, Color color, int order, bool occlusion)
        {
            var b = Spawn(anim, sprite, simPos, 3600f, sx, sy, color, order, occlusion);
            if (b != null) b.Persistent = true;
            return b;
        }

        void Release(Bit b, float fade)
        {
            if (b == null || !_bits.Contains(b)) return;
            float shown = Mathf.Max(0f, b.Age - b.Delay);
            b.Life = shown + Mathf.Max(0.01f, fade);
            b.FadeOut = Mathf.Max(0.01f, fade);
            b.Persistent = false;
        }

        bool Alive(Bit b) => b != null && _bits.Contains(b);

        void UpdateBits(float dt)
        {
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var b = _bits[i];
                b.Age += dt;
                if (!b.Persistent && b.Age >= b.Delay + b.Life) { KillBit(i); continue; }
                if (b.Age >= b.Delay)
                {
                    b.Pos += b.Vel * dt;
                    if (b.Drag > 0f) b.Vel *= Mathf.Exp(-b.Drag * dt);
                    if (b.LiftVel != 0f || b.LiftGrav != 0f)
                    {
                        b.Lift += b.LiftVel * dt;
                        b.LiftVel -= b.LiftGrav * dt;
                        if (b.Lift < 0f) { b.Lift = 0f; b.LiftVel = -b.LiftVel * 0.25f; if (Mathf.Abs(b.LiftVel) < 0.3f) { b.LiftVel = 0f; b.LiftGrav = 0f; } }
                    }
                    b.Rot += b.RotVel * dt;
                }
                if (b.Follow != null)
                {
                    var p = b.Follow();
                    if (p.HasValue) b.Pos = p.Value;
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
            float shown = b.Age - b.Delay;
            float k = b.Persistent ? 0f : (b.Life > 0f ? Mathf.Clamp01(shown / b.Life) : 1f);
            if (b.Anim != null)
            {
                var spr = b.Anim.At(shown * b.AnimSpeed);
                if (spr != null) sr.sprite = spr;
            }
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            float sx = Mathf.LerpUnclamped(b.Sx0, b.Sx1, ease), sy = Mathf.LerpUnclamped(b.Sy0, b.Sy1, ease);
            var t = sr.transform;
            Vector3 vp = _vc.ToView(b.Pos);
            vp.y += b.Lift;
            t.localPosition = vp;
            if (b.TiledLength > 0f)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(b.TiledLength, b.TiledHeight > 0f ? b.TiledHeight : sr.sprite.rect.height / Mathf.Max(1f, sr.sprite.pixelsPerUnit));
            }
            if (b.AlignToVel && b.Vel.sqrMagnitude > 1e-4f)
            {
                Vector2 vv = SimConst.DirToView(b.Vel);
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vv.y, vv.x) * Mathf.Rad2Deg);
            }
            else t.localRotation = Quaternion.Euler(0f, 0f, b.Rot);
            t.localScale = new Vector3(sx, sy, 1f);
            sr.flipX = b.FlipX;
            float a = b.Alpha;
            if (b.FadeIn > 0f && shown < b.FadeIn) a *= Mathf.Clamp01(shown / b.FadeIn);
            if (!b.Persistent)
            {
                float left = b.Life - shown;
                if (b.FadeOut > 0f) { if (left < b.FadeOut) a *= Mathf.Clamp01(left / b.FadeOut); }
                else a *= 1f - k * k;
            }
            a = ReadabilityOptions.MyFxAlpha(a, _s.MyFxAlpha);
            if (b.Occ != 0 && _s.Occlusion != null)
            {
                float r = b.OccR * Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(sx), Mathf.Abs(sy)) / Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(b.Sx0), Mathf.Abs(b.Sy0))));
                float cap = r >= 1f ? _s.Occlusion.CapAtDisc(b.Pos, r) : _s.Occlusion.CapAt(b.Pos);
                if (a > cap) a = cap;
                _s.Occlusion.UpdateLive(b.Occ, b.Pos, r, a);
            }
            var c = b.Color;
            c.a = a;
            sr.color = c;
        }

        void KillBit(int i)
        {
            var b = _bits[i];
            _bits.RemoveAt(i);
            if (b.Token != 0 && _s.Budget != null) _s.Budget.Release(b.Token);
            if (b.Occ != 0 && _s.Occlusion != null) _s.Occlusion.Unregister(b.Occ);
            if (b.Sr != null && _pool != null)
            {
                b.Sr.drawMode = SpriteDrawMode.Simple;
                _pool.Return(b.Sr);
            }
            b.Sr = null;
        }

        // ------------------------------------------------------------ 그리기 도우미

        /// <summary>한 번 재생(그림 길이 또는 life). 지름 cells(그림의 긴 변 기준)로 맞춘다. 0이면 원래 크기 × scale.</summary>
        Bit Play(SpriteAnim anim, Vector2 pos, float scale, int order, Color color, float life = 0f, bool occlusion = true, float rot = 0f)
        {
            if (anim == null) return null;
            float l = life > 0f ? life : Mathf.Max(SimConst.TickSeconds, anim.Duration);
            var b = Spawn(anim, null, pos, l, scale, scale, color, order, occlusion);
            if (b != null) { b.Rot = rot; b.FadeOut = Mathf.Min(0.08f, l * 0.3f); ApplyBit(b); }
            return b;
        }

        /// <summary>빛 덩이(가운데 밝은 원). 큰 것(지름 2칸 이상)은 bloom_glow 그림 + 가림 셈, 섬광 배율을 곱한다.</summary>
        Bit Glow(Vector2 pos, float fromCells, float toCells, float life, Color c, int order)
        {
            bool big = Mathf.Max(fromCells, toCells) >= 2f;
            var s = big ? (CombatFxArt.Single(_art, CombatFxArt.BloomGlow) ?? ProcSprites.SoftDot) : ProcSprites.SoftDot;
            float flash = Mathf.Clamp01(_s.Options.FlashScale);
            if (_s.Options.ReduceFlash) flash *= 0.5f;
            if (big) c.a *= flash;
            if (c.a <= 0f) return null;
            float n = Mathf.Max(s.rect.width, s.rect.height) / Mathf.Max(1f, s.pixelsPerUnit);
            // 큰 빛 덩이는 땅에 깐다(바닥 띠 맨 위 — 적·내 캐릭터·적 탄·적 예고를 덮어 흐리게 만들지 않는다)
            if (big) order = SortBands.TelegraphFill - 1;
            var b = Spawn(null, s, pos, life, fromCells / n, fromCells / n, c, order, big, null, FxPriority.Decor, true);
            if (b != null) { b.Sx1 = b.Sy1 = toCells / n; ApplyBit(b); }
            return b;
        }

        /// <summary>입자 n개(분류 budget, 우선순위 pri). dir이 0이 아니면 그쪽 ±spread도 안으로. lift = 위로 튀는 속도(칸/초).</summary>
        int Sparks(string budget, FxPriority pri, int n, Vector2 pos, Vector2 dirSim, float spreadDeg, float speedMin, float speedMax, float life, float sizeCells,
            Color a, Color b, float drag, Sprite sprite = null, SpriteAnim anim = null, float liftMin = 0f, float liftMax = 0f, float grav = 0f, bool align = false,
            float spawnRadius = 0f, bool additive = true)
        {
            if (n <= 0) return 0;
            var spr = sprite != null ? sprite : (additive ? ProcSprites.SoftDot : ProcSprites.HardDot);
            bool cone = dirSim.sqrMagnitude > 1e-6f;
            float baseAng = cone ? Mathf.Atan2(dirSim.y, dirSim.x) : 0f;
            int got = 0;
            for (int i = 0; i < n; i++)
            {
                float ang = cone ? baseAng + Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad : Random.Range(0f, Mathf.PI * 2f);
                Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 at = pos + (spawnRadius > 0f ? Random.insideUnitCircle * spawnRadius : Vector2.zero);
                float l = life * Random.Range(0.75f, 1.2f);
                Sprite first = anim != null ? anim.At(0f) : spr;
                float native = first != null ? Mathf.Max(first.rect.width, first.rect.height) / Mathf.Max(1f, first.pixelsPerUnit) : 1f;
                float sz = sizeCells * Random.Range(0.75f, 1.25f) / Mathf.Max(1e-3f, native);
                var bit = Spawn(anim, anim != null ? null : spr, at, l, sz, sz, Color.Lerp(a, b, Random.value), SortBands.Bursts + 12, false, budget, pri, additive);
                if (bit == null) break;
                bit.Vel = d * Random.Range(speedMin, speedMax);
                bit.Drag = drag;
                if (liftMax > 0f) { bit.LiftVel = Random.Range(liftMin, liftMax); bit.LiftGrav = grav; }
                bit.Sx1 = bit.Sy1 = sz * 0.45f;
                bit.AlignToVel = align;
                bit.Rot = Random.Range(0f, 360f);
                if (anim != null) bit.RotVel = Random.Range(-360f, 360f);
                ApplyBit(bit);
                got++;
            }
            return got;
        }

        /// <summary>이어 붙이는 선 그림(chain_link 등, 늘이지 않고 반복)을 a → b로.</summary>
        Bit Line(SpriteAnim anim, Vector2 a, Vector2 b, float heightCells, Color c, float life, int order)
        {
            if (anim == null) return null;
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len <= 0.02f) return null;
            var first = anim.At(0f);
            if (first == null) return null;
            // Tiled는 그림의 기준점을 늘인 사각형에도 그대로 쓴다(chain_link는 왼쪽 끝 기준) → 기준점 비율만큼 a에서 나아간 자리에 둔다.
            float px = first.rect.width > 0f ? first.pivot.x / first.rect.width : 0.5f;
            var bit = Spawn(anim, null, a + d * px, life, 1f, 1f, c, order, true);
            if (bit == null) return null;
            bit.OccR = len * 0.5f;
            bit.TiledLength = len;
            bit.TiledHeight = heightCells > 0f ? heightCells : first.rect.height / Mathf.Max(1f, first.pixelsPerUnit);
            bit.Rot = FxUtil.ViewAngle(d);
            bit.FadeOut = life * 0.5f;
            ApplyBit(bit);
            return bit;
        }

        void Ring(Vector2 pos, float fromR, float toR, float life, Color c, float delay = 0f)
        {
            if (_s.Rings != null) _s.Rings.Add(pos, fromR, toR, life, c, delay);
        }

        // ------------------------------------------------------------ 화면 반응(흔들림·정지·줌·어둡힘)

        /// <summary>방향 흔들림 + 짧은 일반 흔들림. viewDir = 화면 내용이 밀리는 쪽. 프레임 창·세기 상한·장식 감쇠는 FxGovernor.</summary>
        void Impact(float px, Vector2 viewDir, FxPriority pri)
        {
            if (px <= 0f) return;
            if (_s.Governor != null) px = _s.Governor.Shake(px, _s.Time, pri);
            if (px <= 0f) return;
            var cf = CameraFx.Of(_vc);
            if (cf != null && viewDir.sqrMagnitude > 1e-6f) cf.Kick(viewDir, px, Or(S("kick_decay"), 16f), Or(S("kick_frequency"), 38f));
            float rest = S("kick_noise_share");
            if (_vc.Shake != null && rest > 0f) _vc.Shake(px * rest, _t.F("shake_seconds"));
        }

        /// <summary>그림 정지: 무게 비례(FxGovernor — 일반 공격은 0, 비기는 값대로·한 번 상한) + 1초 0.1초 예산.</summary>
        void HitStop(float seconds, FxPriority p)
        {
            if (seconds <= 0f || _vc.HitStop == null) return;
            if (_s.Governor != null) seconds = _s.Governor.HitStop(seconds, p, p == FxPriority.Crit);
            if (seconds <= 0f) return;
            if (_s.HitStopBudget != null) seconds = _s.HitStopBudget.Request(seconds, NowSec);
            if (seconds > 0f) _vc.HitStop(seconds);
        }

        void Punch(float amount)
        {
            if (amount == 0f) return;
            var cf = CameraFx.Of(_vc);
            if (cf != null) cf.Punch(amount * Mathf.Max(Level, _s.EffectsLevel >= 2 ? 0f : 0.5f));
        }

        // ------------------------------------------------------------ 탄 끝(책장 적중·책 돌아옴)

        void OnShotExpired(in SimEvent e)
        {
            string blk;
            if (!_shotBlock.TryGetValue(e.EntityId, out blk)) return;
            _shotBlock.Remove(e.EntityId);
            if (e.A != 2 && e.A != 3) return;
            if (blk == "block.core.page_flurry")
            {
                var hit = A(GReuse, "hit_page");
                Play(hit, e.Pos, 0.9f, SortBands.Bursts + 4, Color.white, 0f, false, Random.Range(-20f, 20f));
                var page = A(GReuse, "page_small");
                Sparks(ParticleBudgetConfig.CatHit, FxPriority.NormalHit, Count(2, FxPriority.NormalHit), e.Pos, Vector2.zero, 180f, 1.2f, 2.6f, 0.35f, 0.16f,
                    Color.white, Color.white, 3f, null, page, 1.2f, 2.4f, 9f, false, 0f, false);
                Note("page_hit");
            }
            else if (blk == "block.core.tome_throw" && e.A == 3)
            {
                // 돌아온 책을 받는 순간: 작은 먼지 + 빛
                Glow(e.Pos, 0.3f, 0.9f, 0.14f, WithA(ElSecondary(Ids.Element.Arcane), 0.7f), SortBands.Bursts + 3);
                Note("tome_catch");
            }
        }
    }
}
