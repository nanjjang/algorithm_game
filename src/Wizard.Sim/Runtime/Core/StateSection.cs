namespace Wizard.Sim
{
    // CONTRACTS 3.2 "상태 절과 해시". 절 id = 훅 표의 모듈 번호(u16). 번호는 더하기만 하고 바꾸지 않는다.
    // 직렬화 순서는 SimWorld.State.cs 한 곳에 있다(id 오름차순).
    //
    // 절의 종류(언제나 쓰는가, 비면 생략하는가)는 SimWorld.State.cs 의 순서표가 정한다:
    //   스칼라 절 — 언제나 (id, 내용)을 쓴다. 틀(id)은 세계가 쓰고 모듈 훅은 내용만 쓴다.
    //   풀 절     — 개체 풀과 목록만으로 이루어진 절. 개수가 0이면 id까지 통째로 생략한다.
    //               틀은 모듈 훅이 SimWorld.TryBeginPoolSection 으로 쓴다(개수를 아는 쪽이 모듈이다).
    internal enum StateSection : ushort
    {
        Player = 1,        // 스칼라
        Heat = 2,          // 스칼라
        Graze = 3,         // 스칼라(플레이어별 스침 기록)
        Combo = 4,         // 스칼라
        Spell = 5,         // 풀(투사체, 지대)
        SpellTable = 6,    // 스칼라(길이와 원천. CONTRACTS 3.2 "SpellTable 은 세계 상태다")
        Arts = 7,          // 스칼라(판정기 상태, 보관된 K)
        Characters = 8,    // 스칼라(캐릭터 패시브)
        Enemies = 9,       // 풀(적, 적 탄)
        Telegraph = 10,    // 풀(예고 구역, 대기 줄)
        Bosses = 11,       // 풀(보스가 없으면 빈다)
        Waves = 12,        // 스칼라
        Camp = 13,         // 스칼라(야영 하위 상태. 해시에 들어간다)
        Outcome = 14,      // 스칼라
        Duel = 15,         // 스칼라(결투 라운드)
        Rng = 16,          // 스칼라. Runtime/Core 가 직접 쓴다: 세계 공통 스칼라(모드, 플레이어 수, 씨앗, 틱, 끝남, 개체 id 카운터)와 보관 난수 흐름
        Debug = 17,        // 스칼라
    }
}
