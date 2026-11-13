namespace Wizard.AssetCheck.Color
{
    // spec 05 9.3 "채움 진하기 옵션 70% / 80% / 90%. 70% 아래로는 내릴 수 없다(설정 검증)", spec 13 설정 표 telegraph_fill.
    // 찬 곳의 색이 위험색 조건(채도·명도 70% 이상, TV-COL-16)을 지키는 하한이라 안전 규칙으로 둔다.
    // 설정 화면의 모델(M9-SET-01)이 고를 수 있는 값을 정하고, 이 검사는 저장된 값이나 데이터가 하한을 지키는지 본다
    public static class TelegraphFillSetting
    {
        public const int MinPercent = 70;
        public const int MaxPercent = 100;

        public static bool IsAllowed(int percent) => percent >= MinPercent && percent <= MaxPercent;
    }
}
