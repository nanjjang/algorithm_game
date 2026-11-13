#nullable enable
using System;
using UnityEngine;
using Wizard.ViewLogic.Presentation;
using Wizard.ViewLogic.Presentation.Limits;

namespace Wizard.Unity.Presentation.Bus
{
    // PresentationBus 를 만들 때 주는 것. 조립 루트(App)가 채운다:
    //   Table        = EventBindingTable.Load(data/presentation/event_bindings.json, event_bindings.none.json)
    //   NowSeconds   = 표시 시계(초, 단조). 구동기의 IMonotonicClock.NowSeconds 를 준다. Time.timeScale 의 영향을 받지 않아야 한다
    //   WorldCamera  = 흔들 전장 카메라(WorldPresenter.Camera.Camera). HUD·메뉴 카메라나 캔버스를 주지 않는다
    //   Fx, Sfx      = 실제 재생기(V-13, V-14). 비우면 디버그 표시를 하는 빈 재생기(DebugFxPlayer·DebugSfxPlayer)를 쓴다
    public sealed class PresentationBusSetup
    {
        public PresentationBusSetup(EventBindingTable table, Func<double> nowSeconds)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            NowSeconds = nowSeconds ?? throw new ArgumentNullException(nameof(nowSeconds));
        }

        public EventBindingTable Table { get; }

        public Func<double> NowSeconds { get; }

        public UnityEngine.Camera? WorldCamera { get; set; }

        public IFxPlayer? Fx { get; set; }

        public ISfxPlayer? Sfx { get; set; }

        public MotionSettings Motion { get; set; } = MotionSettings.Default;

        // 흔들림 방향의 표시 전용 난수 씨앗(코어 난수기와 무관하다)
        public uint ShakeSeed { get; set; } = 1;

        // 디버그 겹침 화면(빈 재생기의 표식과 소리 줄). 빈 재생기를 쓸 때만 뜻이 있다
        public bool ShowDebugOverlay { get; set; } = true;

        public Transform? Parent { get; set; }

        // 바인딩이 없는 열쇠를 열쇠마다 한 번 알린다(CONTRACTS 13 "개발 빌드는 한 번 기록한다"). 비우면 개발 빌드에서만 Debug.Log 로 남긴다
        public Action<string>? OnUnbound { get; set; }
    }
}
