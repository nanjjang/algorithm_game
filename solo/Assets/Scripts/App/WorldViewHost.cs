using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using Wizard.Solo.View;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 월드 표시 호스트(00 문서 7절). 정사영 카메라(평범한 UnityEngine.Camera만), 전장 루트, HUD 캔버스(UiCanvasFactory)와
    /// 그 첫 자식 WorldTextRoot, SpritePool을 갖고, 판(또는 보조 판)마다 ViewContext를 만들어 ViewRegistry의 층을 돌린다.
    /// 설정 옵션은 Build 때와 ISettingsStore.Changed 때 vc.Options.ReadFrom으로 갱신한다(같은 객체를 고친다).
    /// 호스트가 직접 적용하는 옵션: 흔들림 배율·그림 정지·연출 감도(ViewOptions 머리 주석).
    /// EventSystem은 만들지 않는다(F3).
    /// 판 중 카메라: CameraDirector(따라가기·전장 경계·정수 배 줌·동적 줌·줌 펀치)가 위치와 정사영 크기를 정한다(Juice.json camera 절).
    /// 판이 없을 때(메뉴)는 예전처럼 CameraHome + UI 배율 크기. 층은 CameraFx.Of(vc)로 줌 펀치·잠깐 보기를 요청한다.
    /// 후처리(Bloom·비네트·색 보정)는 Assets/DefaultVolumeProfile.asset(URP 기본 볼륨)이고, 카메라의 renderPostProcessing만
    /// 리플렉션으로 켠다(URP 형을 코드에 쓰지 않는다 — 00 문서 7절). 연출 감도 최소면 끈다.
    /// </summary>
    public sealed class WorldViewHost : IWorldViewHost
    {
        /// <summary>카메라 중심 = 월드 (12, 7.1875)(00 문서 7절: 좌우 192·위 104·아래 80px 여백). 판이 없을 때만 쓴다.</summary>
        public static readonly Vector3 CameraHome = new Vector3(12f, 7.1875f, -10f);
        /// <summary>먹색 #12131C(08 요약서 8.2절의 띠 색).</summary>
        public static readonly Color Background = new Color32(0x12, 0x13, 0x1C, 0xFF);
        /// <summary>표시 전용 튜닝 조각 이름(Resources/DataSolo/Juice.json).</summary>
        public const string JuiceTuningName = "Juice";

        readonly GameContext _ctx;
        readonly ViewOptions _options = new ViewOptions();
        readonly ShakeAccumulator _shake = new ShakeAccumulator();
        readonly HitStopTimer _hitStop = new HitStopTimer();
        readonly GameObject _cameraGo;
        readonly GameObject _worldRootGo;
        readonly GameObject _hudCanvasGo;
        readonly Camera _camera;
        readonly Transform _worldRoot;
        readonly RectTransform _hudRoot;
        readonly RectTransform _worldTextRoot;
        readonly SpritePool _pool;
        readonly CameraDirector _director = new CameraDirector();
        readonly CameraFx _cameraFx;
        readonly EffectsTuning _juice;

        float _bossZoom = 1f, _wideZoom = 1f, _wideCells, _shakeMul = 1f, _shakeFreq = 30f, _bossIntroWeight;
        bool _postFx = true;
        int _postFxApplied = -1;
        float _shakeClock;

        List<IWorldViewLayer> _layers;
        ViewContext _vc;
        int _screenW, _screenH;
        bool _destroyed;

        public WorldViewHost(GameContext ctx)
        {
            _ctx = ctx ?? new GameContext();

            _cameraGo = new GameObject("WorldCamera");
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(_cameraGo);
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _cameraGo.transform.position = CameraHome;
            UpdateCameraSize(true);

            JObjectOrNull(out var juiceRoot);
            _juice = new EffectsTuning(juiceRoot);
            ReadCameraTuning();
            _cameraFx = _cameraGo.AddComponent<CameraFx>();
            _cameraFx.Director = _director;
            // 가산 효과가 겹쳐 1을 넘으면 Bloom이 더 번지게(톤 매핑 없음 — 1 이하 색은 그대로)
            if (_postFx) _camera.allowHDR = true;

            _worldRootGo = new GameObject("WorldRoot");
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(_worldRootGo);
            _worldRoot = _worldRootGo.transform;

            _hudRoot = UiCanvasFactory.Create("HudCanvas", UiSortOrders.Hud);
            _hudCanvasGo = _hudRoot.parent != null ? _hudRoot.parent.gameObject : _hudRoot.gameObject;
            var wt = new GameObject("WorldTextRoot", typeof(RectTransform));
            _worldTextRoot = (RectTransform)wt.transform;
            _worldTextRoot.SetParent(_hudRoot, false);
            _worldTextRoot.anchorMin = _worldTextRoot.anchorMax = _worldTextRoot.pivot = new Vector2(0.5f, 0.5f);
            _worldTextRoot.sizeDelta = new Vector2(UiScale.RefWidth, UiScale.RefHeight);
            _worldTextRoot.anchoredPosition = Vector2.zero;
            _worldTextRoot.SetAsFirstSibling();

            _pool = new SpritePool(_ctx.Art, _worldRoot);

            ReadOptions();
            if (_ctx.Settings != null) _ctx.Settings.Changed += OnSettingsChanged;
        }

        void JObjectOrNull(out Newtonsoft.Json.Linq.JObject root)
        {
            root = null;
            try { root = _ctx.Content != null ? _ctx.Content.SoloExtra(JuiceTuningName) : null; }
            catch (Exception ex) { Debug.LogWarning("Juice tuning unreadable: " + ex.Message); }
        }

        public Camera Camera => _camera;
        public Transform WorldRoot => _worldRoot;
        public RectTransform HudRoot => _hudRoot;
        public RectTransform WorldTextRoot => _worldTextRoot;
        public ISpritePool Pool => _pool;
        public ViewOptions Options => _options;
        /// <summary>지금 판의 표시 문맥(층이 없으면 null).</summary>
        public ViewContext Context => _vc;
        public IReadOnlyList<IWorldViewLayer> Layers => _layers;
        /// <summary>판 중 카메라 계산(줌·따라가기). 스크린숏 도구가 같은 크기로 찍을 때 OrthographicSize를 읽는다.</summary>
        public CameraDirector Director => _director;

        /// <summary>
        /// 높이 pixelHeight px의 렌더 대상(예: 스크린숏용 1920×1080 RenderTexture)에 지금 화면과 같은 배율로 그리려면 넣을 정사영 크기.
        /// 판 중이면 줌(정수 배·동적 줌·펀치)을 넣고, 판이 없으면 UI 배율 1 크기. SmokeCapture가 orthographicSize를 덮어쓸 때 이 값을 쓰면 된다.
        /// </summary>
        public float OrthographicSizeFor(int pixelHeight)
        {
            if (pixelHeight <= 0) pixelHeight = UiScale.RefHeight;
            if (_layers == null || !_director.HasPosition) return UiScale.OrthographicSize(pixelHeight, 1f);
            float ppu = _director.BasePixelsPerUnit(1f) * _director.ZoomMul * (1f + _director.Punch);
            return pixelHeight / (2f * Mathf.Max(1f, ppu));
        }

        void OnSettingsChanged() => ReadOptions();

        void ReadOptions()
        {
            var s = _ctx.Settings != null ? _ctx.Settings.Current : null;
            if (s != null) _options.ReadFrom(s);
        }

        void ReadCameraTuning()
        {
            var t = _juice;
            if (t.Token("camera") == null) return; // 조각이 없으면 CameraDirector 기본값
            _director.Zoom = Or(t.F("camera", "zoom"), _director.Zoom);
            _director.IntegerZoom = t.B("camera", "integer_zoom");
            _director.FollowRate = Or(t.F("camera", "follow_rate"), _director.FollowRate);
            _director.LookAheadCells = t.F("camera", "look_ahead_cells");
            _director.LookAheadRate = Or(t.F("camera", "look_ahead_rate"), _director.LookAheadRate);
            _director.ZoomRate = Or(t.F("camera", "zoom_rate"), _director.ZoomRate);
            _director.PunchMax = Or(t.F("camera", "punch_max"), _director.PunchMax);
            _director.PunchDecay = Or(t.F("camera", "punch_decay"), _director.PunchDecay);
            _director.PixelSnap = t.B("camera", "pixel_snap");
            _director.HudBiasPx = t.F("camera", "hud_bias_px");
            _bossZoom = Or(t.F("camera", "boss_zoom"), 1f);
            _wideZoom = Or(t.F("camera", "wide_threat_zoom"), 1f);
            _wideCells = t.F("camera", "wide_threat_cells");
            _bossIntroWeight = t.F("camera", "boss_intro_focus_weight");
            _shakeMul = Or(t.F("camera", "shake_mul"), 1f);
            _shakeFreq = Or(t.F("camera", "shake_frequency"), _shakeFreq);
            _postFx = t.Token("camera", "post_processing") == null || t.B("camera", "post_processing");
        }

        static float Or(float v, float fallback) => v > 0f ? v : fallback;

        void UpdateCameraSize(bool force)
        {
            if (_layers != null && _director.HasPosition) return; // 판 중에는 CameraDirector가 크기를 정한다
            int w = Screen.width, h = Screen.height;
            if (!force && w == _screenW && h == _screenH) return;
            _screenW = w;
            _screenH = h;
            if (h <= 0) return;
            _camera.orthographicSize = UiScale.OrthographicSize(h, UiScale.Compute(w, h));
        }

        // ------------------------------------------------------------ IWorldViewHost

        public void Build(SimWorld w)
        {
            if (_destroyed) return;
            DisposeLayers();
            ReadOptions();
            _director.Reset();
            _vc = new ViewContext
            {
                Game = _ctx,
                World = w,
                WorldRoot = _worldRoot,
                Camera = _camera,
                HudRoot = _hudRoot,
                WorldTextRoot = _worldTextRoot,
                Pool = _pool,
                Options = _options,
                Shake = RequestShake,
                HitStop = RequestHitStop
            };
            // 층이 Build에서 WorldToHud를 쓸 수 있게 카메라를 먼저 판 자리에 둔다
            if (w != null)
            {
                FrameRun(w, 1f, 0f);
                _cameraGo.transform.position = new Vector3(_director.Position.x, _director.Position.y, CameraHome.z);
            }
            _layers = ViewRegistry.CreateAll();
            for (int i = 0; i < _layers.Count; i++)
            {
                try { _layers[i].Build(_vc, w); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_layers == null) return;
            for (int i = 0; i < _layers.Count; i++)
            {
                try { _layers[i].OnEvent(w, e); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (_destroyed) return;
            UpdateCameraSize(false);
            ApplyPostProcessing();

            // 흔들림: 전장 카메라만(HUD 캔버스는 Screen Space Overlay라 따라가지 않는다). 폭은 기준 64px = 1칸의 그림 px라
            // 확대하면 화면에서도 그만큼 커진다. 폭·길이 한도와 설정 배율은 ShakeAccumulator 그대로.
            _shake.Update(dt);
            _shakeClock += dt;
            float amp = _shake.AmplitudePx(_options.EffectsLevel, _options.ShakeScale);
            Vector3 pos = CameraHome;
            if (_layers != null && w != null)
            {
                try { FrameRun(w, alpha, dt); }
                catch (Exception ex) { Debug.LogException(ex); }
                pos = new Vector3(_director.Position.x, _director.Position.y, CameraHome.z);
            }
            if (amp > 0f)
            {
                // 매 프레임 무작위보다 덜 어지럽게 펄린 잡음으로 흔든다(회전·확대 떨림 없음)
                float f = _shakeClock * _shakeFreq;
                Vector2 o = new Vector2(Mathf.PerlinNoise(f, 0.37f) * 2f - 1f, Mathf.PerlinNoise(0.71f, f) * 2f - 1f);
                if (o.sqrMagnitude > 1f) o.Normalize();
                o *= amp / SimConst.PixelsPerUnit * _shakeMul;
                pos.x += o.x;
                pos.y += o.y;
            }
            _cameraGo.transform.position = pos;

            if (_layers == null || w == null) return;
            // 그림 정지 뒤 표시 시간 배율(쓰러짐 슬로모션 — CameraFx.TimeScale). 시뮬 틱은 위 Frame의 누적기가 그대로 돌린다.
            float layerDt = _hitStop.Filter(dt);
            if (_cameraFx != null) layerDt *= _cameraFx.TimeScale;
            for (int i = 0; i < _layers.Count; i++)
            {
                try { _layers[i].Render(w, alpha, layerDt); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        public void DisposeLayers()
        {
            if (_layers != null)
            {
                for (int i = 0; i < _layers.Count; i++)
                {
                    try { _layers[i].Dispose(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
            _layers = null;
            _vc = null;
            _shake.Reset();
            _hitStop.Reset();
            _director.Reset();
            if (_cameraFx != null) _cameraFx.ClearFocus();
            if (_cameraGo != null) _cameraGo.transform.position = CameraHome;
            if (_camera != null) UpdateCameraSize(true);
        }

        // ------------------------------------------------------------ 판 카메라

        /// <summary>판 중 카메라: 따라갈 점·바라보는 쪽·동적 줌을 세계에서 읽어 CameraDirector에 넘긴다.</summary>
        void FrameRun(SimWorld w, float alpha, float dt)
        {
            int sw = _camera.pixelWidth > 0 ? _camera.pixelWidth : Screen.width;
            int sh = _camera.pixelHeight > 0 ? _camera.pixelHeight : Screen.height;
            float ui = UiScale.Compute(Screen.width, Screen.height);
            float arenaH = w.ArenaHeight > 0f ? w.ArenaHeight : 14f;
            float arenaW = w.ArenaWidth > 0f ? w.ArenaWidth : 24f;

            Vector2 focus = Vector2.zero, look = Vector2.zero;
            int n = 0;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p == null || !p.Active) continue;
                Vector2 sim = ViewMath.Lerp(p.PrevPos, p.Pos, alpha);
                Vector3 v = SimConst.ToView(sim, arenaH);
                focus += new Vector2(v.x, v.y);
                look += SimConst.DirToView(Dir8Util.ToSimVector(p.Facing));
                n++;
            }
            if (n == 0) focus = new Vector2(arenaW * 0.5f, arenaH * 0.5f);
            else { focus /= n; look /= n; }

            // 잠깐 보기(보스 등장 등): 층이 요청했거나 보스가 등장 중이면 그쪽으로 당긴다
            float fw = _cameraFx != null ? _cameraFx.TickFocus(dt) : 0f;
            if (fw > 0f) focus = Vector2.Lerp(focus, _cameraFx.FocusPoint, fw);
            else if (w.Boss != null && w.Boss.State == BossFightState.Intro && _bossIntroWeight > 0f)
            {
                var be = w.FindEnemy(w.Boss.EnemyId);
                if (be != null)
                {
                    Vector3 bv = SimConst.ToView(be.Pos, arenaH);
                    focus = Vector2.Lerp(focus, new Vector2(bv.x, bv.y), _bossIntroWeight);
                }
            }

            // 줌 펀치는 움직임 연출: 연출 감도(전체 1 / 줄임 0.3 / 최소 0)와 흔들림 배율을 따른다
            _director.MotionScale = ShakeAccumulator.LevelScale(_options.EffectsLevel) * Mathf.Clamp01(_options.ShakeScale);
            _director.ClampSlackCells = _cameraFx != null ? _cameraFx.ClampSlack : 0f;
            _director.Step(dt, sw, sh, ui, focus, look, WantZoom(w), arenaW, arenaH);
            _camera.orthographicSize = _director.OrthographicSize;
        }

        /// <summary>동적 줌 목표: 보스가 살아 있으면 boss_zoom, 넓은 예고(반지름·길이 ≥ wide_threat_cells)가 떠 있으면 wide_threat_zoom.</summary>
        float WantZoom(SimWorld w)
        {
            float want = 1f;
            // 층이 붙잡은 줌(쓰러짐 줌인)은 다른 목표 위에 곱한다
            float hold = _cameraFx != null ? _cameraFx.ZoomHold : 1f;
            if (hold != 1f) return hold * (w.Boss != null && w.Boss.State != BossFightState.Dead && w.Boss.State != BossFightState.CollapseDone ? _bossZoom : 1f);
            if (w.Boss != null && w.Boss.State != BossFightState.Dead && w.Boss.State != BossFightState.CollapseDone)
                want = Mathf.Min(want, _bossZoom);
            if (_wideCells > 0f)
            {
                for (int i = 0; i < w.Zones.Count; i++)
                {
                    var z = w.Zones[i];
                    if (z == null || z.Harmless || z.Cancelled || z.Detonated) continue;
                    if (z.Radius >= _wideCells || z.Length >= _wideCells) { want = Mathf.Min(want, _wideZoom); break; }
                }
            }
            return want;
        }

        /// <summary>URP 카메라 후처리 켜기(리플렉션 — URP 형을 코드에 쓰지 않는다). 연출 감도 최소면 끈다.</summary>
        void ApplyPostProcessing()
        {
            int want = _postFx && _options.EffectsLevel < 2 ? 1 : 0;
            if (want == _postFxApplied || _camera == null) return;
            _postFxApplied = want;
            try
            {
                var t = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (t == null) return;
                var c = _cameraGo.GetComponent(t);
                if (c == null) c = _cameraGo.AddComponent(t);
                var prop = t.GetProperty("renderPostProcessing");
                if (c != null && prop != null && prop.CanWrite) prop.SetValue(c, want == 1, null);
            }
            catch (Exception ex) { Debug.LogWarning("post processing toggle failed: " + ex.Message); }
        }

        // ------------------------------------------------------------ 연출 요청(층 → 호스트)

        /// <summary>vc.Shake: 세기(px)·초. 광과민 한도와 흔들림 배율·연출 감도는 여기서 적용한다.</summary>
        public void RequestShake(float intensityPx, float seconds) => _shake.Add(intensityPx, seconds);

        /// <summary>vc.HitStop: 초. hitstop_enabled가 꺼져 있으면 무시, 줄임이면 절반, 최소면 0.</summary>
        public void RequestHitStop(float seconds) =>
            _hitStop.Request(seconds, _options.EffectsLevel, _options.HitStopEnabled);

        /// <summary>층·카메라·캔버스·풀을 전부 걷는다(다시 부팅할 때).</summary>
        public void Destroy()
        {
            if (_destroyed) return;
            DisposeLayers();
            _destroyed = true;
            if (_ctx.Settings != null) _ctx.Settings.Changed -= OnSettingsChanged;
            _pool.DestroyAll();
            UnityObjects.Destroy(_hudCanvasGo);
            UnityObjects.Destroy(_worldRootGo);
            UnityObjects.Destroy(_cameraGo);
        }
    }
}
