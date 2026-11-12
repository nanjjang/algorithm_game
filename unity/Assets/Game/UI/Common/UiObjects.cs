#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Wizard.Unity.UI.Common
{
    // 코드로 만드는 uGUI 개체의 공통 손질(T-32: 프리팹과 장면 편집 없이 만든다).
    // 모든 그림 요소는 레이캐스트를 끈다: 누름 판정은 uGUI 의 이벤트 시스템이 아니라 UiInputRouter 가 위젯 사각형으로 한다.
    public static class UiObjects
    {
        // 왼쪽 위 기준(앵커·피벗 모두 왼쪽 위)으로 놓은 RectTransform 이 붙은 새 개체
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.localScale = Vector3.one;
            return rt;
        }

        // 부모 왼쪽 위 기준의 정수 px 사각형을 놓는다(y 는 아래로)
        public static void Place(RectTransform rt, PixelRect rect)
        {
            rt.anchoredPosition = new Vector2(rect.X, -rect.Y);
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
        }

        public static Image AddImage(GameObject go, Color32 color)
        {
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // 편집기(재생 아님)에서는 바로 지운다. 테스트와 배치 실행에서 개체가 남지 않게
        public static void Destroy(Object? obj)
        {
            if (obj == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
