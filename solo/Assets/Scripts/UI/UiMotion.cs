using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// UI 연출의 공용 곡선과 붙이기 도우미. 모든 연출은 표시만 바꾼다(화면 규칙·상태와 무관). 시간은 unscaled(일시정지 중에도 돈다).
    /// 에디터 모드(테스트)에서는 MonoBehaviour의 Update가 돌지 않으므로 연출 컴포넌트는 처음 모습(끝 상태)을 그대로 둔다.
    /// </summary>
    public static class UiMotion
    {
        public static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        public static float EaseInOutSine(float t) { t = Mathf.Clamp01(t); return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t); }

        /// <summary>살짝 넘쳤다 돌아오는 곡선(0→1, 끝에서 정확히 1).</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>튀기 곡선: 0에서 1로 올랐다가 0으로(정점 t = 0.3).</summary>
        public static float Punch(float t)
        {
            t = Mathf.Clamp01(t);
            const float peak = 0.3f;
            return t < peak ? EaseOutCubic(t / peak) : 1f - EaseInOutSine((t - peak) / (1f - peak));
        }

        public static T Ensure<T>(Component c) where T : Component
        {
            if (c == null) return null;
            var x = c.GetComponent<T>();
            return x != null ? x : c.gameObject.AddComponent<T>();
        }

        /// <summary>대상을 툭 튀게(배율 scale까지 커졌다 돌아옴).</summary>
        public static void Pop(Component target, float scale = UiTheme.NumberPopScale, float seconds = UiTheme.NumberPopSeconds)
        {
            var p = Ensure<UiPop>(target);
            if (p != null) p.Pop(scale, seconds);
        }

        /// <summary>대상을 좌우로 흔든다(px, 초). 흔들림 동안만 자리를 옮기고 끝나면 제자리.</summary>
        public static void Shake(Component target, float px, float seconds)
        {
            var p = Ensure<UiPop>(target);
            if (p != null) p.Shake(px, seconds);
        }

        /// <summary>켜질 때마다 페이드 + 밀려 들어옴 + 커짐을 하는 등장 연출을 붙인다.</summary>
        public static UiEntrance Entrance(Component target, float delay = 0f, float seconds = UiTheme.PanelPopSeconds,
            Vector2? slideFromPx = null, float fromScale = UiTheme.PanelPopFromScale)
        {
            var e = Ensure<UiEntrance>(target);
            if (e == null) return null;
            e.Delay = delay;
            e.Seconds = seconds;
            e.SlideFrom = slideFromPx ?? Vector2.zero;
            e.FromScale = fromScale;
            // 켜진 채로 붙이면 OnEnable이 기본값(지연 0)으로 이미 시작했으므로 새 값으로 다시 시작한다(꺼져 있으면 켜질 때 시작).
            e.Replay();
            return e;
        }

        /// <summary>부모의 자식들에 차례 등장을 붙인다(stagger 초 간격).</summary>
        public static void Stagger(RectTransform parent, float stagger = UiTheme.StaggerSeconds, float startDelay = 0f, Vector2? slideFromPx = null)
        {
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
                Entrance(parent.GetChild(i), startDelay + i * stagger, UiTheme.PanelPopSeconds, slideFromPx ?? new Vector2(0f, -16f), 0.96f);
        }
    }
}
