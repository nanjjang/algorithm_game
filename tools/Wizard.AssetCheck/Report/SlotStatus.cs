namespace Wizard.AssetCheck.Report
{
    // PIPELINE 6.5절의 네 상태. 보고서에는 소문자 이름으로 쓴다
    public enum SlotStatus
    {
        Missing = 0,    // 없음: 매니페스트에 없다. 대체 그림이 나온다
        Imported = 1,   // 반입됨: 검사를 통과했고 사람이 아직 확인하지 않았다
        Approved = 2,   // 검수됨: approved.json 의 해시가 지금 파일과 같다
        Failed = 3,     // 실패: 매니페스트에 있지만 검사를 통과하지 못했다
    }
}
