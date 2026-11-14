namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 전장 위 오버레이 HUD의 배치(솔로 UI 다듬기 [임시], 1920×1080 기준 px). 전투 카메라가 전장을 화면 가득 채우므로
    /// 08 요약서 6.1절의 "여백에 둔다" 자리 대신 화면 가장자리에 붙는 반투명 판으로 묶는다(사용자 요청). 6.1절 값(HudLayout)은
    /// 칸 크기·폭 규칙(체력 칸 32/4 = TV-048 폭, 보스 바 960)으로 그대로 쓰고, 여기에는 판의 자리·크기만 둔다.
    /// 각 판은 화면 모서리·가장자리에 고정점으로 붙는다(HudLayer가 HUD 루트를 캔버스 크기로 맞춘다 → 다른 화면비에서도 가장자리에 붙음).
    /// </summary>
    public static class HudOverlayLayout
    {
        /// <summary>화면 가장자리 여백.</summary>
        public const float Edge = 20f;
        /// <summary>판 그림자 거리.</summary>
        public const float ShadowPx = 5f;

        // ---- 왼쪽 위: 상태 판(초상 + 체력 + 과열)
        public const float StatusW = 412f, StatusH = 136f;
        public const float PortraitFrameX = 14f, PortraitFrameY = 14f, PortraitFramePx = 72f, PortraitPx = 64f;
        /// <summary>지팡이 단계 꼬리표(초상 아래).</summary>
        public const float StageX = 14f, StageY = 92f, StageW = 72f, StageH = 28f;
        /// <summary>체력 칸 줄의 왼쪽 위(칸 크기·간격은 HudLayout.HpCellPx/HpGapPx).</summary>
        public const float HpX = 102f, HpY = 16f;
        /// <summary>과열 게이지: 머리 글(열) + 막대.</summary>
        public const float HeatCaptionX = 102f, HeatY = 64f, HeatCaptionW = 32f, HeatBarX = 138f, HeatBarW = 256f, HeatBarH = 14f;
        /// <summary>콤보 자리 "HUD"(옵션)의 왼쪽 위(판 안).</summary>
        public const float ComboX = 102f, ComboY = 94f;

        // ---- 위 가운데: 웨이브 띠
        public const float BannerW = 600f, BannerH = 102f, BannerBossH = 40f, BannerY = 12f;
        public const float BannerTextY = 4f, BannerTextH = 44f;
        public const float BannerBarX = 60f, BannerBarY = 58f, BannerBarW = 480f, BannerBarH = 12f;
        public const float BannerSubY = 82f, BannerSubH = 16f;

        // ---- 위 가운데: 보스 판(보스 웨이브에 웨이브 띠 아래)
        public const float BossW = 1040f, BossH = 124f, BossY = 60f;
        /// <summary>이름판 = 공통 그림 boss_nameplate 512×64 원래 크기.</summary>
        public const float BossNameW = 512f, BossNameH = 64f, BossNameY = 4f;
        public const float BossBarX = 40f, BossBarY = 86f, BossBarH = 20f;

        // ---- 오른쪽 위: 지갑·점수·처치
        public const float StatsW = 300f, StatsH = 140f, StatsRowY = 14f, StatsRowStep = 38f, StatsRowH = 36f, StatsIconPx = 24f;

        // ---- 아래 가운데: 회로·비기 칸
        public const float TrayH = 100f, TrayPad = 16f;
        public const float CellPx = 64f, CellGap = 8f, CellIconPx = 48f, CellY = 14f;
        public const float ArtSlotPx = 80f, ArtSlotY = 8f, ArtSlotGap = 24f;

        // ---- 안내
        public const float LockPillW = 300f, LockPillH = 40f;
        public const float BannerNoticeW = 680f, BannerNoticeH = 96f, BannerNoticeAnchorY = 0.26f;
        public const float HintW = 720f, HintH = 40f;
        public const float AdviceW = 960f, AdviceH = 32f, AdviceGap = 8f;

        /// <summary>회로 n칸 줄의 폭(오버레이 칸).</summary>
        public static float CircuitRowWidth(int cells) => cells <= 0 ? 0f : cells * CellPx + (cells - 1) * CellGap;
        public static float CellX(int i) => i * (CellPx + CellGap);

        /// <summary>회로 칸 n개 + 비기 칸(있으면)의 아래 판 폭.</summary>
        public static float TrayWidth(int cells, bool artSlot) =>
            TrayPad * 2f + CircuitRowWidth(cells) + (artSlot ? (cells > 0 ? ArtSlotGap : 0f) + ArtSlotPx : 0f);
    }
}
