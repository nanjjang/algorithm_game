using UnityEngine;

namespace Wizard.Solo.App
{
    /// <summary>플레이 중이면 Destroy, 에디터 모드(EditMode 테스트)면 DestroyImmediate.</summary>
    public static class UnityObjects
    {
        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        /// <summary>바로 부순다(다시 부팅할 때 같은 프레임에 새것을 만들기 전에 앞의 것을 치운다).</summary>
        public static void DestroyNow(Object o)
        {
            if (o == null) return;
            Object.DestroyImmediate(o);
        }
    }
}
