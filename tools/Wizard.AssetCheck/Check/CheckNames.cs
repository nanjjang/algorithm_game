namespace Wizard.AssetCheck.Check
{
    // 검사 이름(보고서의 check 칸). spec 14 5.7.1절 1~5번과 수락 조건의 그믐 팔레트 검사
    public static class CheckNames
    {
        public const string Manifest = "manifest";        // 매니페스트를 읽다 만난 문제(M9-AST-01 로더)
        public const string File = "file";                // 1. 파일이 있고 PNG 로 읽힌다
        public const string Size = "size";                // 1. PNG 크기 = (폭 × 프레임, 높이) 또는 사각형을 담는다
        public const string Settings = "settings";        // 2. 점 필터, 밉맵 없음, 압축 없음
        public const string Danger = "danger";            // 3. 플레이어 쪽 그림의 위험색 0픽셀
        public const string EnemyColor = "enemy-color";   // 적 몸의 색 규칙(spec 05 1.4, spec 14 4.2절 CheckEnemyBody)
        public const string Atlas = "atlas";              // 4. 1920×1080 배경은 아틀라스에 넣지 않는다
        public const string Content = "content";          // 5. 콘텐츠 id 마다 필요한 그림
        public const string Palette = "palette";          // char.5 의 16색 이하·원본 팔레트의 부분집합, 지팡이 +0 머리의 보라(DECISIONS 2.2)
        public const string Slots = "slots";              // data/presentation/asset_slots.json
    }
}
