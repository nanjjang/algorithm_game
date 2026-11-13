namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57). 슬롯 하나가 트리에서 맡은 몫. 글자 출력(spec 02 14.5)과 미리보기의 커서 연동(14.4 "마디 ↔ 회로 칸")이 읽는다
    public enum SlotRole : byte
    {
        Empty = 0,
        Projectile = 1,
        Modifier = 2,
        Split = 3,             // 스코프의 유효한 분열
        SplitIgnored = 4,      // 같은 스코프의 두 번째 이후 분열(W_SPLIT_DUPLICATE)
        Trigger = 5,           // 새 스코프를 연 적중 시 발동
        TriggerIgnored = 6,    // 넷째 트리거(W_TRIGGER_DEPTH, SF-7)
    }
}
