using UnityEngine;

namespace Wizard.Solo.View.Hud
{
    /// <summary>HUD 위젯 하나의 자리(1920×1080 기준 px, 왼쪽 위 원점, y 아래 +)와 글자 크기.</summary>
    public struct HudRect
    {
        public float X, Y, W, H;
        public int FontPx;
        public HudRect(float x, float y, float w, float h, int fontPx = 0) { X = x; Y = y; W = w; H = h; FontPx = fontPx; }
        public override string ToString() => "(" + X + "," + Y + " " + W + "x" + H + " f" + FontPx + ")";
    }

    /// <summary>
    /// 전투 HUD의 자리와 크기(08 요약서 6.1절 표, 10 요약서 1.3·2.3·3.4절). 값은 요약서 표 그대로다.
    /// 요약서에 값이 없는 솔로 임시 값은 여기 두지 않고 DataSolo/G17.json(HudTuning)에 둔다.
    /// 캐릭터 둘레의 자리는 캐릭터 발밑(판정 중심 + 0.4칸) 기준 px 오프셋(오른쪽 +, 아래 +)이다.
    /// </summary>
    public static class HudLayout
    {
        // ---- 위쪽 띠
        /// <summary>웨이브 진행 바 (480,16) 960×12.</summary>
        public static readonly HudRect WaveProgress = new HudRect(480, 16, 960, 12);
        /// <summary>날과 웨이브 (760,36) 400×40, 32px.</summary>
        public static readonly HudRect DayWave = new HudRect(760, 36, 400, 40, 32);
        /// <summary>보스 웨이브의 날과 웨이브: 16px 한 줄 (760,76) 400×24 (TV-046).</summary>
        public static readonly HudRect DayWaveBoss = new HudRect(760, 76, 400, 24, 16);
        /// <summary>시간대 아이콘(32), 변덕 아이콘(최대 2).</summary>
        public const int TodIconPx = 32, WhimIconPx = 32, MaxWhimIcons = 2;
        /// <summary>경과 시간 (912,80) 96×16.</summary>
        public static readonly HudRect Elapsed = new HudRect(912, 80, 96, 16, 12);
        /// <summary>허상까지 n웨이브 (1016,80) 144×16.</summary>
        public static readonly HudRect RevengeLine = new HudRect(1016, 80, 144, 16, 12);
        /// <summary>보스 표시 (480,8) 960×60. 윗줄 y8~40, 아랫줄 y48~64(체력 바 960×16).</summary>
        public static readonly HudRect Boss = new HudRect(480, 8, 960, 60);
        public static readonly HudRect BossTopRow = new HudRect(480, 8, 960, 32, 24);
        public static readonly HudRect BossBar = new HudRect(480, 48, 960, 16);
        /// <summary>보스 체력 바 단계 눈금 4×16(S-0803).</summary>
        public const int BossTickW = 4;

        // ---- 왼쪽 위
        /// <summary>체력 (48,40), 칸 32×32, 간격 4.</summary>
        public const float HpX = 48, HpY = 40;
        public const int HpCellPx = 32, HpGapPx = 4;
        /// <summary>초상 (48,80) 64×64 + 테 72×72.</summary>
        public static readonly HudRect Portrait = new HudRect(48, 80, 64, 64);
        public static readonly HudRect PortraitFrame = new HudRect(44, 76, 72, 72);
        /// <summary>초상 피격 시 밝아지는 시간(초).</summary>
        public const float PortraitHitSeconds = 0.2f;

        // ---- 오른쪽 위
        /// <summary>원정 지갑·점수·처치 (1744,40) 160×24 세 줄, 줄 간격 28, 오른쪽 맞춤.</summary>
        public const float WalletX = 1744, WalletY = 40, WalletW = 160, WalletRowH = 24, WalletRowStep = 28;
        /// <summary>줄 머리 아이콘 S-0804 24×24.</summary>
        public const int WalletIconPx = 24;

        // ---- 아래
        /// <summary>지팡이 회로 (760,1016)부터 칸 40×40, 간격 4. 코어 1 + 연결 최대 8 = 9칸(TV-049).</summary>
        public const float CircuitX = 760, CircuitY = 1016;
        public const int CircuitCellPx = 40, CircuitGapPx = 4, CircuitMaxCells = 9;
        /// <summary>회로 칸 안 블록 아이콘(I-B2 24×24).</summary>
        public const int CircuitIconPx = 24;
        /// <summary>한 줄 안내 (16,1008) 720×32. 과열 잠금이면 앞에 남은 시간 딱지 64×24.</summary>
        public static readonly HudRect Notice = new HudRect(16, 1008, 720, 32, 24);
        public static readonly HudRect NoticeBadge = new HudRect(16, 1012, 64, 24);

        // ---- 캐릭터 둘레 (발밑 기준 px)
        /// <summary>열 링 반지름 44px(줄일 수 없음).</summary>
        public const float HeatRingRadiusPx = 44f;
        /// <summary>결정 칸 24×24, 간격 2(머리 위 가로, 칸 수 = 상한).</summary>
        public const int CrystalCellPx = 24, CrystalGapPx = 2;
        /// <summary>콤보 숫자 캐릭터 기준 (+40,−72). 숫자 글리프 combo_digits 24×32.</summary>
        public static readonly Vector2 ComboOffset = new Vector2(40, -72);
        public const int ComboDigitW = 24, ComboDigitH = 32;
        /// <summary>최근 입력 5개 발밑 (−50,+60) 100×16, 문양 16×16 5개 + 이음줄.</summary>
        public static readonly HudRect RecentInputs = new HudRect(-50, 60, 100, 16);
        public const int RecentInputCount = 5, RecentGlyphPx = 16;
        /// <summary>지금 K 줄 (−96,+80) 192×16, 12px.</summary>
        public static readonly HudRect NowK = new HudRect(-96, 80, 192, 16, 12);

        // ---- 시간(요약서 값)
        /// <summary>콤보 끊김 2초 = 120틱(10 요약서 1.1절).</summary>
        public const int ComboTimeoutTicks = 120;
        /// <summary>+1 때 1.2배로 튀었다 돌아옴 0.08초(10 요약서 1.3절).</summary>
        public const float ComboPopScale = 1.2f, ComboPopSeconds = 0.08f;
        /// <summary>결정 직전 = 남은 콤보 3 이하(10 요약서 1.3절).</summary>
        public const int ComboNearCrystalLeft = 3;
        /// <summary>결정 칸 연출(10 요약서 2.3절): 생김 0.25초, 가득 숨 1.5초 주기, 씀 0.1초, 그믐 두 개 깨짐 0.06초 차.</summary>
        public const float CrystalGainSeconds = 0.25f, CrystalFullBreathPeriod = 1.5f, CrystalSpendSeconds = 0.1f, CrystalSecondBreakDelay = 0.06f;
        /// <summary>피격 깨짐 조각 수(콤보 피격 끊김, 10 요약서 1.3절).</summary>
        public const int ComboShatterPieces = 6;

        /// <summary>체력 n칸 줄의 폭(TV-048: 8칸 → 284).</summary>
        public static int HpRowWidth(int cells) => cells <= 0 ? 0 : cells * HpCellPx + (cells - 1) * HpGapPx;
        /// <summary>회로 n칸 줄의 폭(TV-049: 9칸 → 392).</summary>
        public static int CircuitWidth(int cells) => cells <= 0 ? 0 : cells * CircuitCellPx + (cells - 1) * CircuitGapPx;
        /// <summary>결정 n칸 줄의 폭.</summary>
        public static int CrystalRowWidth(int cells) => cells <= 0 ? 0 : cells * CrystalCellPx + (cells - 1) * CrystalGapPx;
        public static float HpCellX(int i) => HpX + i * (HpCellPx + HpGapPx);
        public static float CircuitCellX(int i) => CircuitX + i * (CircuitCellPx + CircuitGapPx);
    }
}
