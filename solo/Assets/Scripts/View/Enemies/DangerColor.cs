using UnityEngine;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>
    /// 위험색 판정(05 요약서 1.1절): 색상각 330~15도 그리고 채도 ≥ 70% 그리고 명도 ≥ 70%.
    /// 적의 공격과 예고에만 쓴다. 이 층이 무해 구역·별똥·적 몸 표식·대체 도형에 쓰는 색이 위험색이 아닌지 테스트가 이것으로 본다.
    /// </summary>
    public static class DangerColor
    {
        public static bool IsDanger(float hueDeg, float satPct, float valPct) =>
            (hueDeg >= 330f || hueDeg <= 15f) && satPct >= 70f && valPct >= 70f;

        public static bool IsDanger(Color32 c)
        {
            ToHsv(c, out float h, out float s, out float v);
            return IsDanger(h, s, v);
        }

        /// <summary>sRGB 바이트 → 색상각(0~360), 채도·명도(0~100%).</summary>
        public static void ToHsv(Color32 c, out float hueDeg, out float satPct, out float valPct)
        {
            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
            float max = Mathf.Max(r, Mathf.Max(g, b)), min = Mathf.Min(r, Mathf.Min(g, b));
            float d = max - min;
            valPct = max * 100f;
            satPct = max <= 0f ? 0f : d / max * 100f;
            float h;
            if (d <= 0f) h = 0f;
            else if (max == r) h = 60f * (((g - b) / d) % 6f);
            else if (max == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
            if (h < 0f) h += 360f;
            hueDeg = h;
        }

        public static Color32 WithAlpha(Color32 c, float a01) => new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(Mathf.RoundToInt(a01 * 255f), 0, 255));
        public static Color32 ScaleAlpha(Color32 c, float mul) => new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * mul), 0, 255));
    }
}
