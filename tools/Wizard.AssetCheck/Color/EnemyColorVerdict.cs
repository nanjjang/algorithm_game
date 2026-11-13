namespace Wizard.AssetCheck.Color
{
    // 적 몸 색 하나의 판정(spec 05 1.4 COL-1~8). 오류는 Danger, Teal(U-30). 나머지는 경고다
    public enum EnemyColorVerdict
    {
        Ok = 0,
        Danger = 1,             // 위험색 범위(DECISIONS 2.2) — 오류
        Teal = 2,               // COL-5 청록 150~180도 — 오류
        ForbiddenHue = 3,       // COL-4 280~15도 — 경고
        ForbiddenHueEdge = 4,   // COL-4 경계값 정확히 280·15도(C-06) — 경고
        OverSaturated = 5,      // COL-2 채도 50% 초과 — 경고
        BrightSky = 6,          // COL-5 밝은 하늘색 — 경고
        Gold = 7,               // COL-6 금색 — 경고
        WeaponTint = 8,         // COL-8 #FF2E88: 무기 부위에만 허용 — 경고(부위는 사람이 본다)
    }
}
