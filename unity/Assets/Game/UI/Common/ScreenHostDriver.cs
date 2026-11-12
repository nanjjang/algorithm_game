#nullable enable
using System;
using UnityEngine;

namespace Wizard.Unity.UI.Common
{
    // 렌더 프레임마다 화면 쪽을 돌리는 구성 요소. 조립 루트가 개체에 붙이고 Attach 로 넘긴다(장면 편집 없음, T-32).
    // 프레임마다: 창 크기 → 캔버스 배율, 화면 틱과 Sync(ScreenHost.Frame), 멈춰 있는 포인터의 올림 맞추기.
    // Time.timeScale 과 무관하다(단조 시계를 받는다). 입력은 여기서 읽지 않는다(UiInputRouter 에 조립 루트가 넣는다).
    public sealed class ScreenHostDriver : MonoBehaviour
    {
        private ScreenHost? _host;
        private UiCanvas? _canvas;
        private UiInputRouter? _router;
        private Func<double>? _now;

        public ScreenHost? Host => _host;

        public void Attach(ScreenHost host, UiCanvas canvas, UiInputRouter router, Func<double> nowSeconds)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _now = nowSeconds ?? throw new ArgumentNullException(nameof(nowSeconds));
        }

        public void Detach()
        {
            _host = null;
            _canvas = null;
            _router = null;
            _now = null;
        }

        // 프레임 하나. Update 가 부르고, 테스트가 직접 부를 수 있다
        public void Step(int windowWidthPx, int windowHeightPx)
        {
            if (_host == null || _canvas == null || _router == null || _now == null)
            {
                return;
            }
            _canvas.ApplyWindowSize(windowWidthPx, windowHeightPx);
            _host.Frame(_now());
            _router.SyncHover();
        }

        private void Update() => Step(Screen.width, Screen.height);
    }
}
