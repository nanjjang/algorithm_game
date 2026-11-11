namespace Wizard.Sim.Input
{
    // CONTRACTS 3.1. 키로 적을 수 없는 판 안의 조작
    public enum RunCommandOp : byte
    {
        PickCard = 1,          // A = 카드 번호(0~2)
        RerollCards = 2,
        DiscardBlock = 3,      // A = 야생 블록 인스턴스 번호
        SetSlot = 4,           // A = 슬롯 번호(0 = 코어), B = 블록 인스턴스 번호(0이면 비움)
        SwapSlots = 5,         // A, B = 슬롯 번호
        Heal = 6,
        UnlockArt = 7,         // A = (int)ArtId
        Depart = 8,
        AbandonRun = 9,
        CloseCampScreen = 10,  // CampScreen → FireIdle (spec 07 11.4의 "닫기")
        EnterPractice = 11,    // CampScreen → Practice
        ExitPractice = 12,     // Practice → CampScreen
        // 200~255: 디버그 전용. SimConfig.AllowDebugOps 가 꺼져 있으면 무효 명령이다(서버 프로필의 판과 재실행은 언제나 꺼짐)
        DebugSkipWave = 200,
        DebugInvulnerable = 201,
        DebugGrant = 202,      // A = 종류(골드·결정·블록), B = 양 또는 id
        DebugSetStage = 203,   // A = 단계. 이 판의 세계 안에서만 실효 단계를 바꾼다(DECISIONS 1.7)
    }
}
