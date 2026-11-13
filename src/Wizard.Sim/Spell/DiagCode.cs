namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 6.3. 값은 표의 순서다(진단 정렬의 둘째 열쇠 "같은 슬롯이면 코드 값 오름차순", 6.4).
    // 번호는 더하기만 한다. 이름 표 열쇠(warn.*)는 ViewLogic 이 코드에서 고른다(코어는 글자를 모른다).
    // UnknownBlock 은 명세 표에 없는 [임시] 값이다: 팩에 없는 블록 id(저장 파일·통신 방어). OPEN-QUESTIONS Q-21
    public enum DiagCode : byte
    {
        None = 0,
        CoreEmpty = 1,            // E_CORE_EMPTY            warn.core_empty
        CoreNotProjectile = 2,    // E_CORE_NOT_PROJECTILE   warn.core_not_projectile
        SlotCountMismatch = 3,    // E_SLOT_COUNT_MISMATCH   (개발용)
        TriggerNoProjectile = 4,  // W_TRIGGER_NO_PROJECTILE warn.trigger_no_projectile
        NoEffect = 5,             // W_NO_EFFECT             warn.no_effect
        SplitDuplicate = 6,       // W_SPLIT_DUPLICATE       warn.split_duplicate
        TriggerDepth = 7,         // W_TRIGGER_DEPTH         warn.trigger_depth
        ProjectileCap = 8,        // W_PROJECTILE_CAP        warn.projectile_cap
        UnknownBlock = 9,         // E_UNKNOWN_BLOCK [임시]   (개발용)
    }
}
