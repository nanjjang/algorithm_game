namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3절의 입자 분류. 1~6 은 전투 화면 표의 우선순위 번호 그대로다. 그 뒤는 결투·광장·강화 화면의 몫.
    // 데이터(particle_budget.json)의 열쇠는 ParticleBudgetIds 가 정한다. 번호는 더하기만 한다.
    // 예고 구역, 적 탄, 열 링, 피격 표시(우선순위 0)는 입자가 아니라 여기에 없다. 줄이지 않는다.
    public enum ParticleCategory : byte
    {
        None = 0,
        Art = 1,                 // 내 비기
        Hit = 2,                 // 적중(지팡이 단계별 적중 장식, 약점·저항 표시)
        Kill = 3,                // 처치 조각과 연기
        Shot = 4,                // 탄이 흘리는 입자와 발사 1회의 총구 입자
        WandIdle = 5,            // 지팡이 대기
        Environment = 6,         // 환경(반딧불, 비, 먼지)
        ArtOpponent = 7,         // 결투: 상대 비기(같은 몫을 따로 준다)
        WandIdleOpponent = 8,    // 결투: 상대 지팡이의 대기 입자
        Emote = 9,               // 광장: 감정표현, 알림 띠, +13 하늘 빛띠
        EnhanceCharge = 10,      // 강화 화면: 충전 입자(반복)
        EnhanceBurst = 11,       // 강화 화면: 성공 고리, 깨지는 조각, 꽃가루(일회성)
    }
}
