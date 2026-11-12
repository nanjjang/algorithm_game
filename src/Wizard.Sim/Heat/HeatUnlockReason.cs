namespace Wizard.Sim.Heat
{
    // spec 01 13절. HeatUnlocked 이벤트의 A. 번호는 추가만 한다
    public enum HeatUnlockReason : byte
    {
        Timeout = 0,     // 잠금 시간이 끝남(7.5 ProcessUnlock)
        Graze = 1,       // 스쳐 피하기(8.4)
        VentBlast = 2,   // 방열 폭발(7.6.7)
        Reset = 3,       // 라운드 시작 같은 초기화
    }
}
