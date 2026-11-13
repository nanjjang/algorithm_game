namespace Wizard.AssetCheck.Check
{
    public enum FindingSeverity
    {
        Error = 0,     // 반입 실패(그 자리는 "실패", 도구는 0 이 아닌 코드로 끝난다)
        Warning = 1,   // 사람이 볼 것(대체 그림 사용, 적 몸 색의 경고 규칙, 검사에서 뺀 그림)
    }
}
