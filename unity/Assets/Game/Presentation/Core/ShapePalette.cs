#nullable enable
using UnityEngine;

namespace Wizard.Unity.Presentation.Core
{
    // 회색 도형의 색(그림이 들어오기 전의 자리 표시. CLAUDE.md 6절 "회색 도형과 빈 구현").
    // 위험색(색상각 330~15도, 채도·명도 70% 이상)은 적의 공격과 예고에만 쓴다(DECISIONS, GDD 0.1). 플레이어 쪽과 바닥, 대체 도형에는
    // 위험색·금색·연두·청록을 쓰지 않는다(unity-engineer 규칙). 열 링의 색은 effects 10절의 값이다(금색과 빨강을 쓰지 않는다, GDD 5).
    // 이 표의 색이 그 규칙을 지키는지는 Presentation/Core/Tests/ShapePaletteTests 가 본다.
    public static class ShapePalette
    {
        // ── 전장
        public static readonly Color Background = new Color(0.08f, 0.08f, 0.09f, 1f);
        public static readonly Color Floor = new Color(0.16f, 0.16f, 0.17f, 1f);
        public static readonly Color Pillar = new Color(0.34f, 0.34f, 0.36f, 1f);

        // ── 플레이어 쪽
        public static readonly Color PlayerBody = new Color(0.82f, 0.82f, 0.84f, 1f);
        public static readonly Color PlayerBodyInvulnerable = new Color(0.82f, 0.82f, 0.84f, 0.45f);
        public static readonly Color PlayerBullet = new Color(0.93f, 0.93f, 0.95f, 1f);
        public static readonly Color PlayerBulletAlt = new Color(0.70f, 0.70f, 0.72f, 1f);  // 코어 블록이 다른 탄(화염구 등)
        public static readonly Color Outline = new Color(0f, 0f, 0f, 1f);

        // 열 링(effects 10): 오르는 중 회청 #AEB8C8 4px 60%, 임박 회청 6px, 임계 흰 6px + 바깥 검정 2px, 잠금 재색 #5A5F6B 6px + 검정 테
        public static readonly Color HeatRising = new Color(0xAE / 255f, 0xB8 / 255f, 0xC8 / 255f, 0.6f);
        public static readonly Color HeatImminent = new Color(0xAE / 255f, 0xB8 / 255f, 0xC8 / 255f, 1f);
        public static readonly Color HeatCritical = new Color(1f, 1f, 1f, 1f);
        public static readonly Color HeatLocked = new Color(0x5A / 255f, 0x5F / 255f, 0x6B / 255f, 1f);
        public static readonly Color HeatExempt = new Color(1f, 1f, 1f, 1f);
        public static readonly Color HeatMark = new Color(1f, 1f, 1f, 0.9f);
        public static readonly Color HeatMarkFaint = new Color(1f, 1f, 1f, 0.25f);   // 차가움: 눈금만 희미하게
        public static readonly Color LockCross = new Color(0x5A / 255f, 0x5F / 255f, 0x6B / 255f, 1f);

        // 시야 화살표(GDD 3): 평소 반투명, 자세 불투명, 반전 다른 색. [임시] 반전의 색은 명세가 정하지 않았다(OPEN-QUESTIONS)
        public static readonly Color FacingNormal = new Color(1f, 1f, 1f, 0.45f);
        public static readonly Color FacingStance = new Color(1f, 1f, 1f, 1f);
        public static readonly Color FacingReverse = new Color(0.56f, 0.70f, 1f, 1f);

        // ── 적(본체는 회색)과 적의 공격·예고(위험색을 쓸 수 있는 유일한 자리)
        public static readonly Color Slime = new Color(0.52f, 0.52f, 0.54f, 1f);
        public static readonly Color Turret = new Color(0.42f, 0.42f, 0.44f, 1f);
        public static readonly Color Enemy = new Color(0.47f, 0.47f, 0.49f, 1f);           // 그 밖의 종(종별 그림은 에셋이 들어온 뒤)
        public static readonly Color EnemyHpBack = new Color(0.10f, 0.10f, 0.11f, 1f);
        public static readonly Color EnemyHpFill = new Color(0.78f, 0.78f, 0.80f, 1f);
        public static readonly Color EnemyShot = new Color(0.95f, 0.16f, 0.22f, 1f);
        public static readonly Color EnemyTelegraph = new Color(0.95f, 0.16f, 0.22f, 0.85f);

        // ── 디버그 글자
        public static readonly Color DebugText = new Color(0.90f, 0.90f, 0.92f, 1f);
    }
}
