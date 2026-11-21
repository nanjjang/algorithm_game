using UnityEngine;

namespace Wizard.Solo.View.Effects
{
    /// <summary>G16 표시 공용 도우미.</summary>
    public static class FxUtil
    {
        /// <summary>플레이 중이면 Destroy, 에디터 모드(EditMode 테스트)면 DestroyImmediate.</summary>
        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        /// <summary>시뮬레이션 방향 → 화면 각도(도, z 회전). 그림은 오른쪽(+x)을 본다고 본다.</summary>
        public static float ViewAngle(Vector2 simDir)
        {
            var v = SimConst.DirToView(simDir);
            if (v.sqrMagnitude <= 0f) return 0f;
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        /// <summary>그림(px) 하나를 지름 diameterCells칸으로 맞추는 배율.</summary>
        public static float FitScale(Sprite s, float diameterCells)
        {
            if (s == null) return 1f;
            float native = Mathf.Max(s.rect.width, s.rect.height) / Mathf.Max(1e-3f, s.pixelsPerUnit);
            return native > 0f ? diameterCells / native : 1f;
        }

        /// <summary>"{el}" 자리에 효과 속성 접미사를 넣는다.</summary>
        public static string WithElement(string pattern, string fxElement) =>
            pattern == null ? null : pattern.Replace("{el}", fxElement ?? string.Empty);
    }
}
