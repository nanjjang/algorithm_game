namespace Wizard.ViewLogic.Presentation.Limits
{
    // 번쩍을 낸 곳(spec 13 14.2 FlashRequest.src). 판정에 쓰지 않고 기록과 디버그 표시에만 쓴다. 번호는 더하기만 한다
    public enum FlashSource : byte
    {
        Other = 0,
        EnhanceRitual = 1,     // 강화 의식(effects.md 3절)
        BossEncounter = 2,     // 보스 첫 만남·격노(effects.md 15절)
        PlayerHit = 3,         // 피격(가장자리 표시는 25% 미만이라 관리기 횟수에 들지 않는다)
        CombatEffect = 4,      // 비기·적중 등 전장 효과
        ServerNotice = 5,      // 서버 전체 알림(effects.md 8절)
    }
}
