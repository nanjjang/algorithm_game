namespace Wizard.Client
{
    // CONTRACTS 6.1. 결투에 들고 들어갈 것. 서버가 지팡이를 잠그고 리그 제한을 적용한다
    public sealed class DuelEntrySelection
    {
        public int Character { get; set; }                // 1..5
        public long WandId { get; set; }                  // 그 캐릭터의 지팡이여야 한다(아니면 NotEligible). 조립은 그 지팡이에 저장된 것
    }
}
