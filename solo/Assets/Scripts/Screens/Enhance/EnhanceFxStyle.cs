using UnityEngine;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 화면 덧연출의 표시 전용 값(입자 수, 빛 크기, 등급 색, 느린 화면). 확률·비용·결과·시간표와 무관하다 —
    /// 시간표(충전 길이, 결과 길이, 번쩍 한도)는 G23.json과 EnhanceFlashGuard가 그대로 정한다. 여기 값은 그 위에 얹는 그림의 양뿐이다.
    /// 위험색(색상각 330~15도, 채도·명도 70% 이상)은 적 전용이라 모든 색을 Safe()로 거른다(루트 CLAUDE.md 2.5절).
    /// </summary>
    public static class EnhanceFxStyle
    {
        /// <summary>도착 단계 → 성공 등급 0..4(+1~3 / +4~6 / +7~9 / +10~12 / +13). 칭호 구간과 같다.</summary>
        public static int Tier(int stage) => stage >= 13 ? 4 : stage >= 10 ? 3 : stage >= 7 ? 2 : stage >= 4 ? 1 : 0;

        // 등급별 성공 입자 수, 빛줄기 크기(px), 느린 화면 길이(초)와 빠르기 배율.
        public static readonly int[] BurstParticles = { 36, 56, 90, 130, 180 };
        public static readonly float[] SlowMoSeconds = { 0f, 0.12f, 0.3f, 0.45f, 0.7f };
        public const float SlowMoScale = 0.3f;
        /// <summary>성공 단계 숫자의 첫 프레임 배율.</summary>
        public static readonly float[] SlamScale = { 1.35f, 1.43f, 1.53f, 1.65f, 1.78f };

        // 충전: 강도별 빨려 드는 입자 배율(시간표의 초당 입자 수에 곱한다), 마법진 미세 떨림(px, 마법진·지팡이 묶음만).
        public const float ChargeParticleBoost = 2.5f;
        public static readonly float[] HoldTremblePx = { 0f, 1f, 1.5f, 2.5f, 3.5f, 4f };
        public const float ContinuousParticleScale = 0.4f;

        // 마법진(effects 2절의 회전 위에 겹을 더한다): 겹마다 지름, 돌기 방향·빠르기 배율.
        public static readonly float[] LayerDiameter = { 640f, 560f, 480f, 400f, 330f };
        public static readonly float[] LayerSpin = { 0.35f, -0.55f, 0.8f, -1.1f, 1.5f };

        /// <summary>등급 색: 0 속성색 / 1 속성색 + 금 / 2 금 / 3 흰 금 + 하늘빛 / 4 흰 금 + 보라빛.</summary>
        public static Color TierColor(int tier, Color element)
        {
            switch (tier)
            {
                case 0: return Safe(element);
                case 1: return Safe(Color.Lerp(element, UiTheme.Gold, 0.35f));
                case 2: return UiTheme.Gold;
                case 3: return Color.Lerp(UiTheme.Gold, Color.white, 0.55f);
                default: return Color.Lerp(UiTheme.Gold, Color.white, 0.7f);
            }
        }

        // 실패 계열(어두운 보라·회색·식은 금). 빨강은 쓰지 않는다.
        public static readonly Color FailSmoke = Hex(0x3B3354);
        public static readonly Color FailViolet = Hex(0x6E5A9C);
        public static readonly Color FailAsh = Hex(0x8E8A99);
        public static readonly Color CrackTint = Hex(0xB9B4D6);
        public static readonly Color DullGold = Hex(0xA8915A);
        public static readonly Color WardTeal = Hex(0x7FE0D0);

        /// <summary>확률 숫자·계기판 색 단계(높음 초록 → 금 → 호박 → 보라). 낮은 확률에 빨강을 쓰지 않는다.</summary>
        public static Color ProbColor(int pct)
        {
            if (pct >= 75) return UiTheme.Good;
            if (pct >= 40) return UiTheme.Gold;
            if (pct >= 15) return UiTheme.Amber;
            return Hex(0xB48CFF);
        }

        public static Color Hex(int rgb, float a = 1f) => UiTheme.Hex(rgb, a);

        public static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>위험색 영역(색상각 330~15도이면서 채도·명도 70% 이상)인가.</summary>
        public static bool IsDanger(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float deg = h * 360f;
            return (deg >= 330f || deg <= 15f) && s >= 0.7f && v >= 0.7f;
        }

        /// <summary>위험색이면 채도를 내려 영역 밖으로 뺀다(알파 유지).</summary>
        public static Color Safe(Color c)
        {
            if (!IsDanger(c)) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var r = Color.HSVToRGB(h, 0.55f, v);
            r.a = c.a;
            return r;
        }
    }
}
