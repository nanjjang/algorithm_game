using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Settings
{
    /// <summary>작은 전장 미리보기(424×240)가 그릴 값(D-71).</summary>
    public struct SettingsPreview
    {
        /// <summary>원형 예고의 채움 불투명도(telegraph_fill 70/80/90 → 0.70/0.80/0.90).</summary>
        public float TelegraphFillAlpha;
        /// <summary>색각 보조: 차오르는 채움에 줄무늬, 적 탄에 주황 줄무늬(08 12.2절).</summary>
        public bool Stripes;
        /// <summary>내 탄 불투명도(my_bullet_opacity 30~100% → 0.30~1.00).</summary>
        public float MyBulletAlpha;
        /// <summary>연출 감도 0 전체 / 1 줄임 / 2 최소.</summary>
        public int EffectsLevel;
        /// <summary>예고의 흰 번쩍(08 14.2절): 전체 = 구역 전체, 줄임·최소 = 테두리만 굵게.</summary>
        public bool FlashWholeZone;
        public bool FlashThickBorder => !FlashWholeZone;
    }

    /// <summary>설정 미리보기 규칙(엔진 없음). 네 옵션(연출 줄이기·색각 보조·예고 채움 진하기·내 탄 투명도)을 고르면 보인다.</summary>
    public static class SettingsPreviewModel
    {
        /// <summary>% → 0~1 단위 환산(옵션의 범위 끝이 100이 아닌 것 — telegraph_fill 최대 90 — 도 %이다).</summary>
        const float PercentFull = 100f;

        public static readonly string[] PreviewKeys =
        {
            OptionKeys.MotionPreset, OptionKeys.ColorblindAssist, OptionKeys.TelegraphFill, OptionKeys.MyBulletOpacity
        };

        public static bool HasPreview(string optionKey)
        {
            for (int i = 0; i < PreviewKeys.Length; i++) if (PreviewKeys[i] == optionKey) return true;
            return false;
        }

        public static SettingsPreview Build(SettingsData s)
        {
            int level = SettingsOptions.EffectsLevel(s);
            return new SettingsPreview
            {
                TelegraphFillAlpha = SettingsOptions.Int(s, OptionKeys.TelegraphFill) / PercentFull,
                Stripes = SettingsOptions.Bool(s, OptionKeys.ColorblindAssist),
                MyBulletAlpha = SettingsOptions.Int(s, OptionKeys.MyBulletOpacity) / PercentFull,
                EffectsLevel = level,
                FlashWholeZone = level == 0
            };
        }
    }
}
