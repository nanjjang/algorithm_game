using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 화면 색과 글자 크기(08 요약서 1.4·1.5절, UA 1.2). 화면 작업은 색을 직접 적지 않고 여기 상수만 쓴다.
    /// 위험색(빨강 #FF3B30, 자홍 #FF2E88)은 여기에 두지 않는다 — 메뉴·창·플레이어 쪽에 쓰지 않는다(HUD 세 곳은 HUD 작업이 따로 둔다).
    /// 조심 버튼은 빨강이 아니라 호박색 테 + 경고 삼각(K-24)이다.
    /// </summary>
    public static class UiTheme
    {
        // ---- 바탕·글자 (1.5절)
        /// <summary>먹. 외곽선, 창 뒤 덮개(60%). 순수 검정 금지.</summary>
        public static readonly Color Ink = Hex(0x12131C);
        /// <summary>나무 어두움 = 화면 바탕.</summary>
        public static readonly Color WoodDark = Hex(0x1F2130);
        /// <summary>나무 = 기본 판.</summary>
        public static readonly Color Wood = Hex(0x2A2D40);
        /// <summary>나무 밝음 = 올림 테, 켜짐 바탕.</summary>
        public static readonly Color WoodLight = Hex(0x3A3F5A);
        /// <summary>회청 = 보조 글자, 꺼진 괄호, 꺼진 글자.</summary>
        public static readonly Color SlateBlue = Hex(0x8E9AAE);
        /// <summary>분필 흰색 = 본문 글자, 켜짐 아랫줄.</summary>
        public static readonly Color Chalk = Hex(0xF2EDE0);
        /// <summary>쪽지 / 그늘 / 쪽지 위 글자 = 결과 카드, 확인 창.</summary>
        public static readonly Color Paper = Hex(0xE8DDBF);
        public static readonly Color PaperShade = Hex(0xC9BB96);
        public static readonly Color PaperText = Hex(0x2B2A33);
        /// <summary>선택 노랑 = 선택 괄호 전용.</summary>
        public static readonly Color SelectYellow = Hex(0xFFE94A);
        /// <summary>금색 = 골드, 영웅 등급, +13(값진 것만).</summary>
        public static readonly Color Gold = Hex(0xFFD24A);
        /// <summary>희귀 파랑 / 일반 회색 = 등급 테.</summary>
        public static readonly Color RareBlue = Hex(0x4F8CFF);
        public static readonly Color CommonGray = Hex(0x9AA0AE);
        /// <summary>알림 호박색 = 경고·오류·조심 버튼 테.</summary>
        public static readonly Color Amber = Hex(0xF2A33A);
        /// <summary>좋음 초록.</summary>
        public static readonly Color Good = Hex(0x6FD36B);
        /// <summary>연두 = 변이 전용.</summary>
        public static readonly Color Mutation = Hex(0xB8F04A);
        /// <summary>청록 = 비전 속성 전용.</summary>
        public static readonly Color ArcaneTeal = Hex(0x4FE3C1);

        // ---- 속성색 (UA 3.2)
        public static readonly Color ElementFire = Hex(0xFF8A1F);
        public static readonly Color ElementLightning = Hex(0xFFF36B);
        public static readonly Color ElementArcane = Hex(0x4FE3C1);
        public static readonly Color ElementIce = Hex(0x9FDCFF);
        public static readonly Color ElementDark = Hex(0x8A5CFF);
        public static readonly Color ElementNone = Hex(0x8E9AAE);

        // ---- 꾸밈 팔레트(솔로 UI 다듬기). 1.5절 색에서 만든 파생 색과 장식용 놋쇠색. 위험색 범위 밖(놋쇠 색상각 약 37도).
        /// <summary>놋쇠 = 장식 테·제목 띠 밑줄·마름모 장식(키트 판의 리벳 색). 정보 색으로 쓰지 않는다.</summary>
        public static readonly Color Brass = Hex(0xC8A46A);
        /// <summary>놋쇠 어두움 = 장식 줄의 그늘.</summary>
        public static readonly Color BrassDark = Hex(0x7A6440);
        /// <summary>밤 = 화면 바탕 그라데이션의 아래(나무 어두움보다 깊다).</summary>
        public static readonly Color Night = Hex(0x111320);
        /// <summary>밤 위 = 화면 바탕 그라데이션의 위.</summary>
        public static readonly Color NightTop = Hex(0x262A40);
        /// <summary>전장 위 오버레이 판의 바탕(먹 82%).</summary>
        public static readonly Color PlateFill = new Color(Ink.r, Ink.g, Ink.b, 0.82f);
        /// <summary>판 그림자(먹 50%).</summary>
        public static readonly Color Shadow = new Color(Ink.r, Ink.g, Ink.b, 0.5f);
        /// <summary>판 위쪽 은은한 빛줄(분필 10%).</summary>
        public static readonly Color Sheen = new Color(Chalk.r, Chalk.g, Chalk.b, 0.10f);
        /// <summary>올림·커서 빛(분필 12%).</summary>
        public static readonly Color HoverGlow = new Color(Chalk.r, Chalk.g, Chalk.b, 0.12f);

        // ---- 여백 체계(px, 4의 배수). 화면 배치는 이 값들을 쓴다.
        public const float Space1 = 4f, Space2 = 8f, Space3 = 12f, Space4 = 16f, Space6 = 24f, Space8 = 32f, Space12 = 48f;
        /// <summary>전체 화면의 좌우 바깥 여백.</summary>
        public const float Gutter = 48f;
        /// <summary>카드 사이 간격.</summary>
        public const float CardGap = 24f;
        /// <summary>판 안쪽 여백.</summary>
        public const float PanelPad = 24f;
        /// <summary>전투 HUD 오버레이 판의 화면 가장자리 여백.</summary>
        public const float HudEdge = 20f;

        // ---- 글자 쓰임(1.4절 여섯 크기를 역할로 부른다)
        public const UiFontId TypeCaption = UiFontId.F12, TypeBody = UiFontId.F16, TypeButton = UiFontId.F24,
            TypeTitle = UiFontId.F32, TypeRecord = UiFontId.F48, TypeHero = UiFontId.F64;

        // ---- 움직임(연출 시간, 초). 화면 규칙과 무관한 표시용 값.
        /// <summary>버튼 올림 시 커지는 배율, 누름 시 줄어드는 배율, 따라가는 빠르기(1/초).</summary>
        public const float HoverScale = 1.04f, PressScale = 0.97f, HoverLerpSpeed = 18f;
        /// <summary>화면 전환(페이드 + 밀려 들어옴).</summary>
        public const float ScreenFadeSeconds = 0.18f, ScreenSlidePx = 24f;
        /// <summary>판·카드 등장(커지며 나타남)과 차례 등장 간격.</summary>
        public const float PanelPopSeconds = 0.24f, PanelPopFromScale = 0.92f, StaggerSeconds = 0.05f;
        /// <summary>숫자가 바뀔 때 툭 튀는 배율과 길이.</summary>
        public const float NumberPopScale = 1.25f, NumberPopSeconds = 0.18f;
        /// <summary>숫자가 차르륵 올라가는 길이.</summary>
        public const float CountUpSeconds = 0.9f;
        /// <summary>쓸 수 있음 반짝임의 주기(느린 숨, 광과민 한도와 무관한 작은 면적).</summary>
        public const float ReadyPulsePeriod = 1.2f;

        /// <summary>창(Modal) 뒤 덮개: 먹색 60%(K-10).</summary>
        public static readonly Color ModalDim = new Color(Ink.r, Ink.g, Ink.b, 0.6f);
        /// <summary>꺼짐 상태의 그림 곱셈 색(채도 0은 그림에, 밝기 60%는 이 곱으로).</summary>
        public static readonly Color DisabledTint = new Color(0.6f, 0.6f, 0.6f, 1f);

        // ---- 글자 (1.4절): 12 / 16 / 24 / 32 / 48 / 64px, 줄 높이 16/24/32/40/56/72
        public const int FontTag = 12, FontBody = 16, FontButton = 24, FontTitle = 32, FontRecord = 48, FontBig = 64;

        /// <summary>글자 크기 → 줄 높이(px). 여섯 가지 밖이면 가장 가까운 아래 크기의 값.</summary>
        public static int LineHeight(UiFontId size)
        {
            switch (size)
            {
                case UiFontId.F12: return 16;
                case UiFontId.F16: return 24;
                case UiFontId.F24: return 32;
                case UiFontId.F32: return 40;
                case UiFontId.F48: return 56;
                case UiFontId.F64: return 72;
                default: return (int)size + (int)size / 2;
            }
        }

        public static int Px(UiFontId size) => (int)size;

        /// <summary>속성 id(element.*) → 속성색. 모르는 id는 무 속성색.</summary>
        public static Color ElementColor(string elementId)
        {
            switch (elementId)
            {
                case Ids.Element.Fire: return ElementFire;
                case Ids.Element.Lightning: return ElementLightning;
                case Ids.Element.Arcane: return ElementArcane;
                case Ids.Element.Ice: return ElementIce;
                case Ids.Element.Dark: return ElementDark;
                default: return ElementNone;
            }
        }

        /// <summary>0xRRGGBB → 불투명 색.</summary>
        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>
        /// 위험색 판정(색상각 330~15도, 채도·명도 70% 이상). 메뉴·창에 쓰려는 색을 검사하는 데 쓴다(테스트·단언용).
        /// </summary>
        public static bool IsDangerColor(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float deg = h * 360f;
            bool hue = deg >= 330f || deg <= 15f;
            return hue && s >= 0.7f && v >= 0.7f;
        }
    }
}
