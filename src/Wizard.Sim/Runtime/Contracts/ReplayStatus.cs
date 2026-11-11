namespace Wizard.Sim
{
    // CONTRACTS 3.8
    public enum ReplayStatus : byte
    {
        Completed = 0,
        RecordCorrupt = 1,   // 구조 오류, 꼬리표·해시 불일치, 무효 명령, 중간 지점 레코드와 실제 해시의 불일치
        EndedEarly = 2,      // 기록의 end_tick 전에 판이 끝났다
        EndedLate = 3,       // end_tick 에 닿았는데 판이 끝나지 않았다
        SimFault = 4,        // 코어가 SimInvariantException 을 던졌다(구현 결함일 수 있다)
        Timeout = 5,         // 호출자가 준 시간·틱 한도를 넘었다
        PackMissing = 6,     // 그 판의 data_version 팩을 찾지 못했다(호출자가 채운다)
    }
}
