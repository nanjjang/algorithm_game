using UnityEngine;
using UnityEngine.UI;

// 캔버스 규격(00-architecture.md 8.2절). 화면 캔버스(F3)와 HUD 캔버스(F5)가 같은 함수로 만들어져 배율·PPU·순서가 어긋나지 않게 한다.
namespace Wizard.Solo.UI
{
    /// <summary>
    /// 화면 배율(순수 함수). 1920×1080 안전 영역이 창 안에 다 들어가는 가장 큰 배율(가로·세로 중 작은 쪽 비율).
    /// 예전에는 정수 배로 내림해서 2560×1440·맥 레티나(3024×1964) 같은 창에서 UI가 1배로 작게 가운데에만 떴다 — 이제 창을 꽉 채운다.
    /// 16:9가 아닌 창의 남는 둘레는 UiFullBleed가 바탕으로 채운다. 판 카메라는 CameraDirector가 따로 정수 줌으로 맞춘다.
    /// </summary>
    public static class UiScale
    {
        public const int RefWidth = 1920, RefHeight = 1080;

        public static float Compute(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0) return 1f;
            return Mathf.Min(screenWidth / (float)RefWidth, screenHeight / (float)RefHeight);
        }

        /// <summary>그 배율에서 1칸이 64px × 배율로 보이는 정사영 크기(1080p·1배 = 8.4375).</summary>
        public static float OrthographicSize(int screenHeight, float scale) =>
            screenHeight / (2f * SimConst.PixelsPerUnit * (scale > 0f ? scale : 1f));
    }

    /// <summary>캔버스의 sortingOrder. HUD는 모든 화면 층 아래에 깔린다(Pause·LootPick·Camp가 HUD를 덮는다).</summary>
    public static class UiSortOrders
    {
        public const int Hud = 0;
        /// <summary>화면 층별 순서: Fullscreen 100, Panel 200, HudOverlay 300, Modal 400, Toast 500.</summary>
        public static int Screen(ScreenLayer layer) => ((int)layer + 1) * 100;
    }

    public static class UiCanvasFactory
    {
        /// <summary>
        /// 캔버스 하나를 만들고 그 안의 1920×1080 루트(가운데 정렬)를 돌려준다. 화면·HUD의 모든 것은 이 루트 아래에 둔다.
        /// 규격: Screen Space Overlay, CanvasScaler ConstantPixelSize(scaleFactor = UiScale.Compute, 창 크기가 바뀌면 UiScaleDriver가 갱신),
        /// referencePixelsPerUnit = 64(그림을 PPU 64로 들이므로 9조각 테두리와 SetNativeSize가 1px = 1px로 맞는다), GraphicRaycaster.
        /// EventSystem은 여기서 만들지 않는다(F3의 CreateScreens가 InputSystemUIInputModule과 함께 하나만 만든다).
        /// </summary>
        public static RectTransform Create(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = SimConst.PixelsPerUnit;
            scaler.scaleFactor = UiScale.Compute(Screen.width, Screen.height);
            go.AddComponent<UiScaleDriver>();

            var rootGo = new GameObject("Root", typeof(RectTransform));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(go.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(UiScale.RefWidth, UiScale.RefHeight);
            root.anchoredPosition = Vector2.zero;
            return root;
        }
    }
}
