namespace Wizard.Core
{
    // CONTRACTS 1.3. 흐름 표는 여기 하나다. 번호는 추가만 한다.
    public enum RngStream : ushort
    {
        WaveComp = 1,    // 틀, 종, 엘리트 종, 개수, 묶음 나누기            (호출 때 파생, 상태 없음)
        WaveSpawn = 2,   // 스폰 위치                                      (웨이브 시작 때 파생, 세계가 보관)
        Modifier = 3,    // 변덕 선택. 서버가 상한 계산 때 이것만 돌린다     (호출 때 파생)
        Loot = 4,        // 전리품 카드. sub = 다시 뽑기 횟수, 빌린 블록은 0xFFFF   (호출 때 파생)
        HpShard = 5,     // 체력 조각                                      (세계가 보관)
        Event = 6,       // 별똥 결정 등 돌발 사건                          (세계가 보관)
        EnemyAi = 7,     // 적 AI의 무작위 선택                             (세계가 보관)
        Mutation = 8,    // 불안정 핵 5%, 뒤틀린 총열 흔들림                 (세계가 보관)
        Weather = 9,     // 규칙에 영향 없는 연출 사건(다시 보기에서 같게)    (세계가 보관)
        ArtUnlock = 10,  // 비기 해금 후보                                  (호출 때 파생)
        BossAi = 11,     // 보스 패턴 선택                                  (세계가 보관)
        DuelWild = 12,   // 결투 야생 난입                                  (세계가 보관, wave = 라운드 번호)
    }
}
