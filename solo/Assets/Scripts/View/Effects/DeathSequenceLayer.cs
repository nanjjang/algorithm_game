using UnityEngine;
using UnityEngine.UI;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.View.World;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 쓰러짐 연출 층(표시 전용, AddEffects에서 JuiceLayer 뒤). 체력 0으로 판이 끝나면(RunEnded, A = HpZero) 한 번:
    /// 마지막 피격 순간 그림 정지 → 짧은 슬로모션(CameraFx.TimeScale — 표시 층의 dt만 느려지고 시뮬 틱은 규칙대로 흐른다) →
    /// 회색 덮개(채도 빼기)·어둠 덮개·조여 드는 비네트(HUD 캔버스 위 uGUI) → 카메라 살짝 당김(CameraFx.ZoomHold) →
    /// 손에서 떨어져 바닥에 구르는 지팡이(쓰러짐 클립은 캐릭터 층이 고른다) → 낮은 쿵(player.death_thud).
    /// 사망 글의 페이드인은 BattleScreen이 같은 표(Juice.json death)로 한다.
    /// 지키는 것:
    /// - 색은 회색·남색만(위험색·빨강 없음, 루트 CLAUDE.md 2.5절). 번쩍은 없다(섬광 줄이기와 무관하게 0).
    /// - 연출 감도: 전체 / 줄임(슬로모션·줌 깊이 절반) / 최소(슬로모션·줌·조임 없이 덮개만). 그림 정지는 호스트가 설정(그림 정지 끔·감도)을 적용한다.
    /// - 이 층의 시간표는 실제 시간(Time.unscaledDeltaTime)으로 센다 — 자기가 건 그림 정지·슬로모션에 묶이지 않게.
    ///   떨어지는 지팡이만 표시 dt(느려진 시간)로 움직인다.
    /// 수치는 Resources/DataSolo/Juice.json death 절(표시 전용, 솔로 임시).
    /// </summary>
    public sealed class DeathSequenceLayer : IWorldViewLayer
    {
        public const string Section = "death";
        public const string ThudSoundId = "player.death_thud";
        const int VignetteTexPx = 128;

        ViewContext _vc;
        EffectsTuning _j;
        WorldTuning _wt;
        RectTransform _root;
        Image _gray, _veil, _vignette;
        Texture2D _vignetteTex;
        Sprite _vignetteSprite;
        SpriteRenderer _wand;
        bool _active, _thudPlayed;
        float _t = -1f, _wandT;
        int _slot;
        Vector2 _feetSim;
        string _charId;
        int _stage;
        float _side = 1f;

        // ---- 점검용
        public bool Active => _active;
        public float Elapsed => _t;
        public float VeilAlpha => _veil != null && _veil.enabled ? _veil.color.a : 0f;
        public float GrayAlpha => _gray != null && _gray.enabled ? _gray.color.a : 0f;
        public float VignetteAlpha => _vignette != null && _vignette.enabled ? _vignette.color.a : 0f;
        public float VignetteScale { get; private set; }
        public float TimeScaleNow { get; private set; } = 1f;
        public float ZoomNow { get; private set; } = 1f;
        public bool WandDropped => _wand != null && _wand.enabled;
        public bool ThudPlayed => _thudPlayed;
        /// <summary>테스트가 시계를 직접 넣는다(0 이하면 Time.unscaledDeltaTime).</summary>
        public float FixedRealDt;

        public void Build(ViewContext vc, SimWorld w)
        {
            Dispose();
            _vc = vc;
            var content = vc != null && vc.Game != null && vc.Game.Content != null ? vc.Game.Content : (w != null ? w.Content : null);
            Newtonsoft.Json.Linq.JObject root = null;
            try { root = content != null ? content.SoloExtra(JuiceLayer.JuiceName) : null; }
            catch (System.Exception) { root = null; }
            _j = new EffectsTuning(root);
            _wt = WorldTuning.From(content);
            // 다시 세울 때(보조 판에서 돌아옴 등) 이미 쓰러져 끝난 판이면 연출 끝 상태로 바로 보인다(그림 정지·소리 없이).
            if (w != null && w.Run.Phase == RunPhase.RunEnded && w.Run.EndReason == RunEndReason.HpZero)
            {
                Begin(w, false);
                _t = Mathf.Max(Seconds("slow_seconds"), Mathf.Max(Seconds("veil_seconds"), Seconds("vignette_seconds"))) + 1f;
                _wandT = 99f;
                _thudPlayed = true;
            }
        }

        float F(string key) => _j != null ? _j.F(Section, key) : 0f;
        float Seconds(string key) => Mathf.Max(0f, F(key));
        Color Col(string key, Color fallback) => _j != null ? EffectsTuning.ParseColor(_j.S(Section, key), fallback) : fallback;
        int Level => _vc != null && _vc.Options != null ? _vc.Options.EffectsLevel : 0;
        /// <summary>움직임 깊이: 전체 1 / 줄임 0.5 / 최소 0.</summary>
        float Depth => Level <= 0 ? 1f : Level == 1 ? 0.5f : 0f;

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_active || e.Type != SimEventType.RunEnded || (RunEndReason)e.A != RunEndReason.HpZero) return;
            if (_vc != null && _vc.World != null && w != _vc.World) return;
            Begin(w, true);
        }

        void Begin(SimWorld w, bool fresh)
        {
            _active = true;
            _t = 0f;
            _wandT = 0f;
            _thudPlayed = false;
            _slot = 0;
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i] != null && w.Players[i].Active && w.Players[i].Downed) { _slot = i; break; }
            var p = w.Players[_slot];
            _charId = p != null ? p.CharId : null;
            _stage = p != null && p.Wand != null ? WandSheet.ClampStage(p.Wand.EffectiveStage) : 0;
            _feetSim = p != null ? WorldViewRules.FeetSim(p.Pos, _wt != null ? _wt.FeetBelowCenterCells : 0.4f) : Vector2.zero;
            // 지팡이는 바라보던 쪽으로 떨어진다(왼쪽을 보면 왼쪽)
            _side = p != null && Dir8Util.ToSimVector(p.Facing).x < 0f ? -1f : 1f;
            BuildOverlay();
            if (fresh && _vc != null && _vc.HitStop != null) _vc.HitStop(Seconds("hitstop_seconds"));
        }

        void BuildOverlay()
        {
            if (_vc == null || _vc.HudRoot == null || _root != null) return;
            var go = new GameObject("DeathFx", typeof(RectTransform));
            _root = (RectTransform)go.transform;
            _root.SetParent(_vc.HudRoot, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            _gray = FullImage("DeathGray");
            _veil = FullImage("DeathVeil");
            _vignette = Img("DeathVignette");
            var vr = _vignette.rectTransform;
            vr.anchorMin = vr.anchorMax = vr.pivot = new Vector2(0.5f, 0.5f);
            _vignetteSprite = VignetteSprite();
            _vignette.sprite = _vignetteSprite;
            SetAlpha(_gray, 0f);
            SetAlpha(_veil, 0f);
            SetAlpha(_vignette, 0f);
        }

        Image Img(string name)
        {
            var g = new GameObject(name, typeof(RectTransform));
            var img = g.AddComponent<Image>();
            img.raycastTarget = false;
            img.rectTransform.SetParent(_root, false);
            return img;
        }

        Image FullImage(string name)
        {
            var img = Img(name);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }

        /// <summary>가운데가 투명하고 바깥으로 갈수록 짙어지는 둥근 비네트(코드로 만든 단순 도형, 그림 에셋 아님). 네모의 모서리 밖은 다 짙다.</summary>
        Sprite VignetteSprite()
        {
            int n = VignetteTexPx;
            _vignetteTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "DeathVignette" };
            var px = new Color32[n * n];
            float half = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - half) / half, dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - 0.25f) / 0.4f);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            _vignetteTex.SetPixels32(px);
            _vignetteTex.Apply(false, true);
            return Sprite.Create(_vignetteTex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        static void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            var c = img.color;
            c.a = a;
            img.color = c;
            img.enabled = a > 0.001f;
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (!_active || w == null) return;
            float real = FixedRealDt > 0f ? FixedRealDt : Mathf.Min(0.1f, Time.unscaledDeltaTime);
            _t += real;
            _wandT += dt;
            float depth = Depth;
            var cf = CameraFx.Of(_vc);

            // 슬로모션: slow_scale에서 slow_seconds 동안 부드럽게 1로(줄임이면 깊이 절반, 최소면 없음)
            float slowS = Seconds("slow_seconds");
            float scale = 1f;
            if (slowS > 0f && _t < slowS)
            {
                float k = UiEase(_t / slowS);
                float low = Mathf.Lerp(1f, Mathf.Clamp01(F("slow_scale")), depth);
                scale = Mathf.Lerp(low, 1f, k);
            }
            TimeScaleNow = scale;
            if (cf != null) cf.SetTimeScale(scale);

            // 카메라 당김(움직임 연출 — 깊이를 곱한다)
            float zoom = 1f + (Mathf.Max(1f, F("zoom")) - 1f) * depth;
            ZoomNow = zoom;
            if (cf != null)
            {
                cf.SetZoomHold(zoom);
                // 구석에 쓰러져도 화면 가운데 쪽으로 데려온다(경계 밖은 바탕색 — 덮개 아래라 눈에 띄지 않는다). 최소면 움직이지 않는다.
                cf.SetClampSlack(Mathf.Max(0f, F("camera_slack_cells")) * (depth > 0f ? 1f : 0f));
            }

            // 덮개: 회색(채도 빼기)·어둠(어둡게)이 차오른다
            float veilK = Ramp(_t, Seconds("veil_seconds"));
            if (_root != null) _root.SetAsLastSibling();
            SetColor(_gray, Col("gray", new Color(0.43f, 0.45f, 0.51f)), Mathf.Clamp01(F("gray_alpha")) * veilK);
            SetColor(_veil, Col("veil", new Color(0.08f, 0.09f, 0.15f)), Mathf.Clamp01(F("veil_alpha")) * veilK);

            // 비네트: 쓰러진 자리를 가운데로 조여 든다(최소면 조이지 않는다)
            float vigK = Ramp(_t, Seconds("vignette_seconds"));
            float from = Mathf.Max(0.1f, F("vignette_from")), to = Mathf.Max(0.1f, F("vignette_to"));
            float vs = depth > 0f ? Mathf.Lerp(from, to, UiEase(vigK)) : to;
            VignetteScale = vs;
            SetColor(_vignette, Col("vignette", new Color(0.03f, 0.04f, 0.08f)), Mathf.Clamp01(F("vignette_alpha")) * vigK);
            if (_vignette != null)
            {
                float side = 2f * UiScaleRef * vs;
                var vr = _vignette.rectTransform;
                vr.sizeDelta = new Vector2(side, side);
                vr.anchoredPosition = _vc != null ? _vc.WorldToHud(w.Players[_slot].Pos) : Vector2.zero;
            }

            // 낮은 쿵
            if (!_thudPlayed && _t >= Seconds("thud_delay"))
            {
                _thudPlayed = true;
                var sound = _vc != null && _vc.Game != null ? _vc.Game.Sound : null;
                if (sound != null)
                    sound.Play(ThudSoundId, SoundBus.Sfx, Mathf.Clamp01(F("thud_volume") > 0f ? F("thud_volume") : 1f),
                        F("thud_pitch") > 0f ? F("thud_pitch") : 1f);
            }

            RenderWand(w);
        }

        /// <summary>화면 긴 변 기준 반경(px): 1920 × 비네트 배율이 반지름이 되게 네모 한 변 = 2 × 1920 × 배율.</summary>
        const float UiScaleRef = 1920f;

        static float Ramp(float t, float seconds) => seconds > 0f ? Mathf.Clamp01(t / seconds) : 1f;
        static float UiEase(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        static void SetColor(Image img, Color c, float a)
        {
            if (img == null) return;
            c.a = a;
            img.color = c;
            img.enabled = a > 0.001f;
        }

        /// <summary>
        /// 손에서 떨어지는 지팡이: 손 높이(발 위 0.55칸)에서 바라보던 쪽 옆 바닥으로 wand_fall_seconds(표시 시간)에 떨어지며
        /// wand_angle도까지 눕고, 닿은 뒤 wand_bounce_cells만큼 한 번 튄다. 손의 지팡이는 쓰러짐 클립이 숨긴다(WorldViewRules.SelectClip).
        /// </summary>
        void RenderWand(SimWorld w)
        {
            var art = _vc != null && _vc.Game != null ? _vc.Game.Art : null;
            var pool = _vc != null ? _vc.Pool : null;
            if (art == null || pool == null || _charId == null) return;
            if (_wand == null)
            {
                var sprite = art.WandHand(_charId, _stage, Facing3.Down, false);
                if (sprite == null) return;
                _wand = pool.Rent(sprite, FxLayers.PlayerBody - 1);
            }
            float fall = Mathf.Max(0.05f, Seconds("wand_fall_seconds"));
            float k = Mathf.Clamp01(_wandT / fall);
            float e = k * k; // 떨어짐은 점점 빠르게
            float bounce = 0f;
            float after = _wandT - fall;
            float bounceS = fall * 0.45f;
            if (after > 0f && after < bounceS) bounce = Mathf.Sin(after / bounceS * Mathf.PI) * Mathf.Max(0f, F("wand_bounce_cells"));
            var feet = _vc.ToView(_feetSim);
            var hand = feet + new Vector3(_side * 0.12f, 0.55f, 0f);
            var ground = feet + new Vector3(_side * Mathf.Max(0f, F("wand_side_cells")), 0.04f, 0f);
            var pos = Vector3.Lerp(hand, ground, e) + new Vector3(0f, bounce, 0f);
            _wand.enabled = true;
            _wand.flipX = _side < 0f;
            _wand.sortingOrder = FxLayers.PlayerBody - 1;
            _wand.transform.localPosition = new Vector3(pos.x, pos.y, 0.01f);
            float ang = -_side * Mathf.Max(0f, F("wand_angle")) * UiEase(k);
            _wand.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            _wand.color = Color.white;
        }

        public void Dispose()
        {
            var cf = CameraFx.Of(_vc);
            if (cf != null && _active) { cf.SetTimeScale(1f); cf.SetZoomHold(1f); cf.SetClampSlack(0f); }
            if (_wand != null && _vc != null && _vc.Pool != null) _vc.Pool.Return(_wand);
            _wand = null;
            if (_root != null) FxUtil.Destroy(_root.gameObject);
            _root = null;
            _gray = _veil = _vignette = null;
            if (_vignetteSprite != null) FxUtil.Destroy(_vignetteSprite);
            if (_vignetteTex != null) FxUtil.Destroy(_vignetteTex);
            _vignetteSprite = null;
            _vignetteTex = null;
            _active = false;
            _t = -1f;
            TimeScaleNow = 1f;
            ZoomNow = 1f;
        }
    }
}
