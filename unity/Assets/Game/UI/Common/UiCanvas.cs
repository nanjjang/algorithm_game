#nullable enable
using System;
using UnityEngine;
using UnityEngine.UI;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 화면 그림을 그리는 캔버스와 1920×1080 무대(spec 14 4.3 "배율과 격자"). 코드로 만든다(T-32).
    //   - 캔버스: 화면 겹침(Screen Space Overlay), 픽셀 맞춤. 배율은 UiLayout.UiScale(정수 배만)이다. 보간하지 않는다.
    //   - 무대: 창 안에서 UiLayout.UiOrigin 만큼 들어간 곳(가운데 맞춤)에 1920×1080 으로 놓는다. 화면 뷰는 무대 좌표만 쓴다.
    //   - 화면 그림은 화면 흔들림을 받지 않는다(카메라와 무관한 겹침 캔버스).
    // uGUI 의 이벤트 시스템과 레이캐스터는 두지 않는다. 입력은 UiInputRouter 가 무대 좌표로 판정한다.
    public sealed class UiCanvas : IDisposable
    {
        public const string ObjectName = "UiCanvas";
        public const int SortingOrder = 100;   // 전장 표시 위 [임시]

        private readonly CanvasScaler _scaler;
        private readonly GameObject _root;

        public UiCanvas(Transform? parent = null)
        {
            var go = new GameObject(ObjectName, typeof(RectTransform));
            _root = go;
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = true;
            Canvas.sortingOrder = SortingOrder;
            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.referencePixelsPerUnit = 1f;
            _scaler.scaleFactor = 1f;
            Stage = UiObjects.CreateRect("Stage", go.transform);
            UiObjects.Place(Stage, PixelRect.FromSize(UiLayout.ReferenceWidthPx, UiLayout.ReferenceHeightPx));
            ApplyWindowSize(UiLayout.ReferenceWidthPx, UiLayout.ReferenceHeightPx);
        }

        public Canvas Canvas { get; }

        public GameObject GameObject => _root;

        // 1920×1080 무대. 화면 뷰의 뿌리가 붙는다
        public RectTransform Stage { get; }

        public int WindowWidthPx { get; private set; }

        public int WindowHeightPx { get; private set; }

        public int Scale { get; private set; } = 1;

        // 무대 왼쪽 위가 창의 어디에 오는가(창 왼쪽 위 기준, 창 px)
        public PixelPoint Origin { get; private set; }

        // 창 크기가 바뀌면 부른다. 바뀐 것이 있으면 true
        public bool ApplyWindowSize(int windowWidthPx, int windowHeightPx)
        {
            if (windowWidthPx == WindowWidthPx && windowHeightPx == WindowHeightPx)
            {
                return false;
            }
            WindowWidthPx = windowWidthPx;
            WindowHeightPx = windowHeightPx;
            Scale = UiLayout.UiScale(windowWidthPx, windowHeightPx);
            Origin = UiLayout.UiOrigin(windowWidthPx, windowHeightPx, Scale);
            _scaler.scaleFactor = Scale;
            // 캔버스 단위 = 창 px / Scale. 무대는 캔버스 왼쪽 위에서 Origin 만큼 들어간다
            Stage.anchoredPosition = new Vector2((float)Origin.X / Scale, -(float)Origin.Y / Scale);
            return true;
        }

        // 창 px(Unity 화면 좌표: 왼쪽 아래 원점, y 위로) → 무대 px(왼쪽 위 원점, y 아래로). 무대 밖이면 무대 범위 밖의 값이 나온다
        public PixelPoint ScreenToStage(int screenX, int screenY)
        {
            int topY = WindowHeightPx - 1 - screenY;
            return new PixelPoint(FloorDiv(screenX - Origin.X, Scale), FloorDiv(topY - Origin.Y, Scale));
        }

        // 여러 번 불러도 된다
        public void Dispose() => UiObjects.Destroy(_root);

        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
