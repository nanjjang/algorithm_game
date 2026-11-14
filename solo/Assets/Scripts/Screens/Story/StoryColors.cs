using UnityEngine;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>
    /// 연출 층의 색(요약서 값, sRGB). 메뉴 색은 UiTheme을 쓰고, 여기에는 UiTheme에 없는 연출 색만 둔다.
    /// 위험색 둘은 허용된 자리에만: 빨강 = 되비침의 만개 탄(적의 공격, 10 요약서 3.8절), 자홍 = 최종 격노 화면 가장자리(05 8.3절, HUD 층).
    /// 플레이어 쪽(쪽지·칭호 띠·머리 장식)에는 은색·분필색만 쓴다(금색 금지 — 10 3.8절).
    /// </summary>
    public static class StoryColors
    {
        /// <summary>허상·거울의 은색 #C8D0E0(10 요약서 3.8절).</summary>
        public static readonly Color Silver = UiTheme.Hex(0xC8D0E0);
        /// <summary>적 공격 위험색 빨강 #FF3B30.</summary>
        public static readonly Color DangerRed = UiTheme.Hex(0xFF3B30);
        /// <summary>최종 격노 가장자리 자홍 #FF2E88.</summary>
        public static readonly Color DangerMagenta = UiTheme.Hex(0xFF2E88);
        /// <summary>흰 번쩍(순백 100% 금지 — 불투명도는 관리기가 0.35 이하로 정한다). 분필색.</summary>
        public static readonly Color FlashWhite = UiTheme.Chalk;
        /// <summary>쓰러짐 무채색 전환을 대신하는 회색 덮개(채도만 빼는 셰이더가 없다 — 불투명도는 G25.json).</summary>
        public static readonly Color CollapseGray = new Color(0.5f, 0.5f, 0.5f, 1f);
    }
}
