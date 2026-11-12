#nullable enable
using System;
using UnityEngine;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 자리 표시 위젯의 색. 값은 spec 14 4.2절 "화면(UI)의 재료와 색" 표의 토큰 값 그대로다(UI 1.2).
    // 위험색(빨강·자홍), 금색, 연두, 청록은 여기에 두지 않는다: 메뉴와 창에 쓰지 않는 색이다(CLAUDE.md 2.5, spec 14 4.2).
    // 그림(K 부품, V-09)이 들어오면 위젯의 겉만 바뀌고 이 색은 대체 도형에만 남는다.
    public static class UiPalette
    {
        public static readonly Color32 Ink = new Color32(0x12, 0x13, 0x1C, 0xFF);           // ink: 판 바깥 외곽선, 괄호 바깥 테
        public static readonly Color32 WoodDark = new Color32(0x1F, 0x21, 0x30, 0xFF);      // wood_dark: 화면 바탕, 눌린 칸
        public static readonly Color32 Wood = new Color32(0x2A, 0x2D, 0x40, 0xFF);          // wood: 기본 판
        public static readonly Color32 WoodLight = new Color32(0x3A, 0x3F, 0x5A, 0xFF);     // wood_light: 윗변 1px, 올림의 테, 켜짐 바탕
        public static readonly Color32 Slate = new Color32(0x8E, 0x9A, 0xAE, 0xFF);         // slate: 보조 글자, 꺼진 괄호
        public static readonly Color32 Chalk = new Color32(0xF2, 0xED, 0xE0, 0xFF);         // chalk: 본문 글자, 켜짐의 아랫줄
        public static readonly Color32 SelectYellow = new Color32(0xFF, 0xE9, 0x4A, 0xFF);  // select_yellow: 선택 괄호 전용
        public static readonly Color32 AlertAmber = new Color32(0xF2, 0xA3, 0x3A, 0xFF);    // alert_amber: 조심 버튼의 테와 경고 삼각

        // 모달 뒤를 어둡게 덮는 막. 먹색의 반투명 [임시: 명세에 값이 없다]
        public static readonly Color32 Scrim = new Color32(0x12, 0x13, 0x1C, 0xA0);

        // 꺼진 것의 바탕: 채도 0, 밝기 60%(spec 14 4.3 "꺼짐"). 나무 판의 밝기(가장 큰 성분)에 0.6을 곱한 회색
        public static readonly Color32 DisabledFill = Gray(Wood, DisabledBrightnessPercent);
        public static readonly Color32 DisabledBorder = Gray(WoodLight, DisabledBrightnessPercent);

        public const int DisabledBrightnessPercent = 60;

        // 모든 대체 색(검사용)
        public static readonly Color32[] All =
        {
            Ink, WoodDark, Wood, WoodLight, Slate, Chalk, SelectYellow, AlertAmber, Scrim, DisabledFill, DisabledBorder,
        };

        // ViewLogic 이 식별자로만 가리키는 색 토큰(UiColorTokens) → 값
        public static Color32 ColorOf(string token)
        {
            switch (token)
            {
                case UiColorTokens.SelectYellow: return SelectYellow;
                case UiColorTokens.Slate: return Slate;
                case UiColorTokens.AlertAmber: return AlertAmber;
                case UiColorTokens.Ink: return Ink;
                default: throw new ArgumentOutOfRangeException(nameof(token), token, "unknown UI color token");
            }
        }

        public static Color32 ColorOf(BracketTone tone) => tone == BracketTone.Muted ? Slate : SelectYellow;

        private static Color32 Gray(Color32 c, int percent)
        {
            const int Full = 100;   // 백분율 환산
            int max = Math.Max(c.r, Math.Max(c.g, c.b));
            byte v = (byte)(max * percent / Full);
            return new Color32(v, v, v, c.a);
        }
    }
}
