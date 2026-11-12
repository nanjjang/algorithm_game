#nullable enable
using Wizard.ViewLogic.Convert;

namespace Wizard.Unity.Presentation.Core
{
    // 회색 도형의 크기(px, 1칸 = 64px. DisplayUnits 가 칸으로 바꾼다)와 그리는 순서.
    // 명세에 값이 있는 것은 그 값이고(열 링 effects 10, 지팡이·탄 그림 크기), 없는 것은 [임시] 자리 표시 값이다(OPEN-QUESTIONS: 회색 도형의 크기).
    // 판정 크기가 아니다. 판정은 세계가 하고 표시는 세계가 준 반지름(RadiusCells)으로 몸과 탄을 그린다.
    public static class ShapeStyle
    {
        // ── 열 링(effects 10): 발밑 중심, 반지름 44px
        public const float HeatRingRadiusPx = 44f;
        public const float HeatRingThinPx = 4f;               // 오르는 중
        public const float HeatRingThickPx = 6f;              // 임박·임계·잠금
        public const float HeatRingOutlinePx = 2f;            // 임계·잠금의 바깥 검정 테
        public const float HeatExemptPx = 2f;                 // 열 면제 대기의 가는 흰 링
        public const float HeatExemptGapPx = 4f;              // [임시] 임계 링 바깥과의 틈
        public const float HeatMarkLengthPx = 12f;            // [임시] 임계 시작 눈금의 길이
        public const float HeatMarkWidthPx = 3f;              // [임시]
        public const float BrinkShakePx = 2f;                 // 넘치기 직전: 임계 링이 2px 떤다
        public const float LockCrossPx = 24f;                 // 잠금 X(fx_lock_x 24×24, spec 01 17.2 에셋 표)
        public const float LockCrossWidthPx = 4f;             // [임시]
        public const int RingSegments = 48;                   // 자료구조 크기: 원 하나를 이루는 막대 수

        // ── 시야 화살표 [임시]: 발밑에서 시야 쪽으로
        public const float FacingArrowStartPx = 20f;
        public const float FacingArrowLengthPx = 40f;
        public const float FacingArrowWidthPx = 5f;
        public const float FacingArrowHeadPx = 16f;

        // ── 적 [임시]
        public const float EnemyHpBarWidthPx = 40f;
        public const float EnemyHpBarHeightPx = 5f;
        public const float EnemyHpBarGapPx = 8f;              // 몸 위쪽 끝과 막대 사이
        public const float TelegraphRingPx = 3f;              // 포탑 시전(예고) 링의 굵기
        public const float TelegraphRingGapPx = 6f;

        // ── 탄 [임시]
        public const float BulletOutlinePx = 2f;

        // ── 카메라 [임시]: 전장 둘레의 여백(칸)
        public const float CameraMarginCells = 1f;

        // ── 그리는 순서(SpriteRenderer.sortingOrder). 효과 층 표(effects 9.1)의 순서를 흉내 낸다: 바닥 < 적 < 탄 < 내 몸 < 내 몸의 표시
        public const int OrderFloor = 0;
        public const int OrderPillar = 10;
        public const int OrderTelegraph = 20;
        public const int OrderEnemy = 30;
        public const int OrderEnemyHp = 35;
        public const int OrderPlayerBullet = 40;
        public const int OrderPlayer = 50;
        public const int OrderEnemyShot = 60;                 // 적 탄은 가려지지 않는다(effects 9.2 "줄일 수 없는 것")
        public const int OrderHeatRing = 70;                  // 열 링은 11층(effects 10 끝): 폭발 아래에 묻히지 않는다
        public const int OrderFacing = 75;
        public const int OrderLockCross = 80;

        public static float Cells(float px) => DisplayUnits.PxToCells(px);
    }
}
