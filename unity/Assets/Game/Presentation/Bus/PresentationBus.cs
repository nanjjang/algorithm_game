#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Netcode.Duel;
using Wizard.Sim.Events;
using Wizard.Unity.Presentation.Cameras;
using Wizard.Unity.Presentation.Fx;
using Wizard.Unity.Presentation.Sfx;
using Wizard.ViewLogic.Convert;
using Wizard.ViewLogic.Presentation;

namespace Wizard.Unity.Presentation.Bus
{
    // 표시 이벤트 버스의 Unity 쪽(ARCHITECTURE 9, CONTRACTS 13). 바인딩의 규칙은 ViewLogic 의 PresentationEventRouter 가 갖고(dotnet test),
    // 여기는 라우터에 그림 재생기(IFxPlayer)·소리 재생기(ISfxPlayer)·카메라 연출(CameraDirector)을 꽂고 HUD 모델에 같은 사건을 넘긴다.
    //
    //   OnEvents(이번 프레임의 SimEvent, 뷰)   — 무한 웨이브·연습: 그대로 Confirmed 로 Publish 한다. 순번(Ordinal)은 (틱, 종류, 슬롯)마다 센다(CONTRACTS 6.4)
    //   Publish(PresentationEvent, 뷰)        — 결투: 클라이언트의 EventLedger 가 낸 Predicted·Confirmed·Retracted 를 그대로 넣는다
    //   RaiseUi(열쇠, 인자)                    — 코어 밖 사건(UiEventKeys)
    //   Render(뷰, alpha)                     — 표시 시계로 카메라 연출(흔들림 줄기, 그림 정지 끝)과 디버그 표식을 나아가게 한다
    // 순서: 사건마다 라우터(그림·소리·카메라) → 등록 순서대로 HUD 모델. 단계 규칙(소리·흔들림·그림 정지는 Confirmed 에서만)은 라우터가 집행한다.
    // HUD 모델은 단계를 그대로 받는다(번복을 스스로 되돌릴 수 있게). 매 틱 읽는 연속 값은 HUD 가 ISimView 에서 읽는다.
    //
    // 세계(SimWorld)·세션·틱을 쥐지 않는다. Time.timeScale 을 건드리지 않는다. 그래서 그림 정지와 흔들림 중에도 틱은 그대로 간다.
    // 만들고 FixedTickDriver.AddPresenter 에 꽂고 치우는 것은 조립 루트(Wizard.Unity.App)다(T-54).
    public sealed class PresentationBus : IFramePresenter, IDisposable
    {
        public const string RootName = "PresentationDebug";

        private readonly Func<double> _nowSeconds;
        private readonly PresentationEventRouter _router;
        private readonly IFxPlayer _fx;
        private readonly ISfxPlayer _sfx;
        private readonly DebugFxPlayer? _debugFx;
        private readonly DebugSfxPlayer? _debugSfx;
        private readonly CameraDirector _camera;
        private readonly List<IPresentationEventHandler> _hud = new List<IPresentationEventHandler>();
        private readonly GameObject? _root;
        private readonly PresentationDebugOverlay? _overlay;
        private bool _disposed;

        public PresentationBus(PresentationBusSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            _nowSeconds = setup.NowSeconds;
            Func<long> nowMs = NowMs;
            if (setup.Fx != null)
            {
                _fx = setup.Fx;
            }
            else
            {
                _debugFx = new DebugFxPlayer(nowMs);
                _fx = _debugFx;
            }
            if (setup.Sfx != null)
            {
                _sfx = setup.Sfx;
            }
            else
            {
                _debugSfx = new DebugSfxPlayer(nowMs);
                _sfx = _debugSfx;
            }
            _camera = new CameraDirector(setup.WorldCamera != null ? setup.WorldCamera.transform : null, nowMs, setup.ShakeSeed)
            {
                Settings = setup.Motion,
            };
            _router = new PresentationEventRouter(setup.Table, _fx, _sfx, _camera, setup.OnUnbound ?? DefaultUnbound());

            if (setup.ShowDebugOverlay && (_debugFx != null || _debugSfx != null))
            {
                _root = new GameObject(RootName);
                if (setup.Parent != null)
                {
                    _root.transform.SetParent(setup.Parent, false);
                }
                _overlay = _root.AddComponent<PresentationDebugOverlay>();
                _overlay.Bind(_debugFx, _debugSfx, setup.WorldCamera, nowMs);
            }
        }

        public PresentationEventRouter Router => _router;

        public IFxPlayer Fx => _fx;

        public ISfxPlayer Sfx => _sfx;

        // 빈 재생기를 쓸 때만 있다
        public DebugFxPlayer? DebugFx => _debugFx;

        public DebugSfxPlayer? DebugSfx => _debugSfx;

        public CameraDirector Camera => _camera;

        public PresentationDebugOverlay? Overlay => _overlay;

        public int HudModelCount => _hud.Count;

        public void AddHudModel(IPresentationEventHandler model)
        {
            RequireOpen();
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            if (!_hud.Contains(model))
            {
                _hud.Add(model);
            }
        }

        public bool RemoveHudModel(IPresentationEventHandler model) => _hud.Remove(model);

        public void OnEvents(ReadOnlySpan<SimEvent> events, ISimView view)
        {
            RequireOpen();
            for (int i = 0; i < events.Length; i++)
            {
                var e = new PresentationEvent
                {
                    Event = events[i],
                    Phase = EventPhase.Confirmed,
                    Ordinal = OrdinalOf(events, i),
                };
                Publish(e, view);
            }
        }

        public void Publish(in PresentationEvent e, ISimView view)
        {
            RequireOpen();
            _router.Handle(e, view);
            for (int i = 0; i < _hud.Count; i++)
            {
                _hud[i].Handle(e, view);
            }
        }

        public void RaiseUi(string uiEventKey, in UiEventArgs args)
        {
            RequireOpen();
            _router.RaiseUi(uiEventKey, args);
        }

        public void Render(ISimView view, double alpha)
        {
            RequireOpen();
            long now = NowMs();
            _camera.Advance();
            _debugFx?.Advance(now);
            _debugSfx?.Advance(now);
        }

        // 새 판·새 경기·결투 재동기화: 예측 기억, 연속 스침, 그림, 흔들림, 그림 정지를 비운다
        public void Reset()
        {
            RequireOpen();
            _router.Reset();
            _fx.StopAll();
            _debugSfx?.Clear();
            _camera.Reset();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _fx.StopAll();
            _camera.Dispose();
            _hud.Clear();
            if (_root != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_root);
                }
            }
        }

        // 그 틱에서 같은 (종류, 슬롯)의 몇 번째인가(CONTRACTS 6.4의 열쇠). 사건은 틱 순으로 온다
        public static ushort OrdinalOf(ReadOnlySpan<SimEvent> events, int index)
        {
            SimEvent cur = events[index];
            int n = 0;
            for (int j = index - 1; j >= 0; j--)
            {
                SimEvent prev = events[j];
                if (prev.Tick != cur.Tick)
                {
                    break;
                }
                if (prev.Type == cur.Type && prev.Slot.Equals(cur.Slot))
                {
                    n++;
                }
            }
            return (ushort)n;
        }

        private long NowMs() => (long)Math.Floor(_nowSeconds() * DisplayUnits.MillisecondsPerSecond);

        private static Action<string>? DefaultUnbound()
        {
            if (!UnityEngine.Debug.isDebugBuild)
            {
                return null;
            }
            return key => UnityEngine.Debug.Log("presentation: no binding for " + key);
        }

        private void RequireOpen()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PresentationBus));
            }
        }
    }
}
