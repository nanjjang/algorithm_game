namespace Wizard.Netcode.Duel
{
    // DuelClientCore 가 낸 내 이벤트 하나의 처지(CONTRACTS 6.4 "버려진 내 입력과 키 상태 맞추기").
    // 확정 열이 정본이다: 유효 틱이 확정되면 그 틱의 확정 열에 (순번, 시각, 키, 누름)이 같은 이벤트가 있는지로만 가른다.
    internal enum OwnEventState : byte
    {
        Pending = 0,        // 유효 틱이 아직 확정되지 않았다. 예측 세계에 넣는다
        Confirmed = 1,      // 그 틱의 확정 열에 있었다
        Dropped = 2,        // 그 틱이 확정됐는데 확정 열에 없다(늦음, 이른 시각, 수량, 유실, 키 상태 모순). 예측에 넣지 않는다
        Superseded = 3,     // 전체 상태로 확정 세계를 덮어써서 받아들여졌는지 알 수 없다. 확정 세계의 키 상태가 결과를 담는다
    }
}
