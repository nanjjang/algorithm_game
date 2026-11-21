using UnityEngine;
using Wizard.Solo.UI;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// HUD 전용 색(sRGB). 일반 색은 UiTheme을 쓴다.
    /// 위험색(빨강 #FF3B30, 자홍 #FF2E88)은 HUD 허용 세 곳(08 요약서 1.5절)에만: 체력 1칸 귀퉁이, 보스 최종 격노 테, (피격 화면 가장자리 = 표시 호스트·이펙트 몫).
    /// 열 링에는 금색·빨강을 쓰지 않는다(02 요약서 6.6절, 10 요약서 3.4절).
    /// </summary>
    public static class HudColors
    {
        /// <summary>위험색 빨강. 체력 1칸 귀퉁이·최종 격노 테에만.</summary>
        public static readonly Color Danger = UiTheme.Hex(0xFF3B30);

        // ---- 열 링 (10 요약서 3.4절)
        /// <summary>오르는 중·임박 회청 #AEB8C8.</summary>
        public static readonly Color RingSlate = UiTheme.Hex(0xAEB8C8);
        /// <summary>오르는 중 불투명도 60%.</summary>
        public const float RingRisingAlpha = 0.6f;
        /// <summary>임계 흰색(분필) + 바깥 검정(먹) 2px.</summary>
        public static readonly Color RingCritical = UiTheme.Chalk;
        public static readonly Color RingRim = UiTheme.Ink;
        /// <summary>잠금 식은 재색 #5A5F6B.</summary>
        public static readonly Color RingAsh = UiTheme.Hex(0x5A5F6B);
        /// <summary>링 바탕 자리(차가움의 희미한 눈금·바탕 고리).</summary>
        public static readonly Color RingTrack = new Color(RingSlate.r, RingSlate.g, RingSlate.b, 0.18f);

        // ---- 시야 화살표 (10 요약서 8절 Q-14)
        public static readonly Color ArrowNormal = UiTheme.Chalk;
        public static readonly Color ArrowStance = UiTheme.Chalk;
        /// <summary>반전 자세 옅은 파랑 RGB(0.56, 0.70, 1.0).</summary>
        public static readonly Color ArrowReverse = new Color(0.56f, 0.70f, 1.0f, 1f);

        // ---- 결정·빚 (10 요약서 2.3절)
        /// <summary>그믐 빚 칸의 보라 테(그림이 없을 때). 암흑 속성색.</summary>
        public static readonly Color DebtPurple = UiTheme.ElementDark;
        /// <summary>빈 결정 칸(그림이 없을 때).</summary>
        public static readonly Color CrystalEmpty = new Color(UiTheme.SlateBlue.r, UiTheme.SlateBlue.g, UiTheme.SlateBlue.b, 0.45f);
        /// <summary>얼린 결정(제자 4 지킴) 서리 방패 대체 색.</summary>
        public static readonly Color FrostShield = UiTheme.ElementIce;

        // ---- 콤보
        public static readonly Color ComboWhite = UiTheme.Chalk;
        public static readonly Color ComboFaded = UiTheme.SlateBlue;

        // ---- 그 밖
        /// <summary>회로 칸 켜짐 대체 색(그림이 없을 때).</summary>
        public static readonly Color CircuitOn = UiTheme.Chalk;
        public static readonly Color CircuitOff = UiTheme.Wood;
        /// <summary>웨이브 진행 바 채움 = 분필 흰색(08 6.1절).</summary>
        public static readonly Color WaveFill = UiTheme.Chalk;
        public static readonly Color BarRail = UiTheme.WoodDark;
        /// <summary>보스 체력 채움(그림이 없을 때). 위험색이 아니다.</summary>
        public static readonly Color BossFill = UiTheme.Chalk;

        /// <summary>콤보 단계 색: 5 미만 흰색 → 5 이상 속성 보조색 → 10 이상 속성 주색(10 요약서 1.3절).
        /// 보조색은 요약서에 값이 없어 흰색과 주색의 가운데로 만든다.</summary>
        public static Color ComboTierColor(int tier, Color elementMain)
        {
            if (tier <= 0) return ComboWhite;
            if (tier == 1) return Color.Lerp(ComboWhite, elementMain, 0.5f);
            return elementMain;
        }

        /// <summary>열 링 상태의 링 색과 두께(px). Cold면 알파 0(눈금만 희미).</summary>
        public static void Ring(HeatRingState s, out Color color, out float widthPx)
        {
            switch (s)
            {
                case HeatRingState.Rising: color = UiTheme.WithAlpha(RingSlate, RingRisingAlpha); widthPx = 4f; return;
                case HeatRingState.Imminent: color = RingSlate; widthPx = 6f; return;
                case HeatRingState.Critical:
                case HeatRingState.Brink: color = RingCritical; widthPx = 6f; return;
                case HeatRingState.Locked: color = RingAsh; widthPx = 6f; return;
                default: color = new Color(0, 0, 0, 0); widthPx = 0f; return;
            }
        }
    }
}
