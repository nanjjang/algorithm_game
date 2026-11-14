namespace Wizard.Solo.Audio
{
    /// <summary>
    /// 섞는 규칙의 사건 묶음(10 요약서 5.2절). 우선순위는 SoundMixPolicy.Priority:
    /// 내가 맞음 &gt; 예고 &gt; 스침·임계·잠금 &gt; 비기 &gt; 적중 &gt; 처치 &gt; 환경.
    /// 화면(ui.*·강화 enhance.*) 소리는 원문 순서에 없어 비기와 같은 우선순위로 둔다(솔로 임시).
    /// </summary>
    public enum SoundCategory
    {
        Ambient = 0,
        Kill = 1,
        Hit = 2,
        Art = 3,
        GrazeHeat = 4,
        Telegraph = 5,
        PlayerHit = 6,
        Interface = 7
    }
}
