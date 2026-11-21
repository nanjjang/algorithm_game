using UnityEngine;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>G24 화면들이 같이 쓰는 작은 도우미(그리기만).</summary>
    public static class MetaUi
    {
        /// <summary>자식을 모두 걷는다(플레이 중이면 Destroy, 에디터 테스트면 DestroyImmediate).</summary>
        public static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var go = t.GetChild(i).gameObject;
                go.transform.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
            }
        }
    }
}
