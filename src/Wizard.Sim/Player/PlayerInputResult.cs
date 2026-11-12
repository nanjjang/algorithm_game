using Wizard.Sim.Arts;
using Wizard.Sim.Input;

namespace Wizard.Sim.Player
{
    // 2단계에서 입력 이벤트 하나를 Player 모듈이 처리한 결과. 같은 이벤트로 이어서 불리는 Stage02_Arts(판정기)가 읽는다
    // (spec 01 3.8: KeyState → 자세 → 시야 → 판정기). 이벤트마다 덮어쓰는 세계의 임시 값이고 상태가 아니다
    // (같은 이벤트 안에서 쓰고 읽으므로 틱 경계를 넘지 않는다. 그래서 스냅숏·직렬화·해시에 넣지 않는다).
    internal struct PlayerInputResult
    {
        public LogicalKey Key;
        public bool Down;
        public long Time;                          // 입력 시각(spec 01 2.1). 판정기의 시각

        // KeyState 를 바꿨는가. 거짓이면 눌린 키의 누름·안 눌린 키의 뗌이라 무시했다(T-I07·T-I08). 판정기에도 넘기지 않는다
        public bool Accepted;

        // 자세 중 방향 누름: 잠긴 시야 기준의 상대 방향(spec 01 5.5). 자세 밖의 방향은 커맨드가 아니다(GDD 8)
        public bool HasDirection;
        public CmdDir Direction;

        // 자세(Stance·Reverse)에 들고 남이 바뀌었는가. 판정기의 OnStanceChanged(InStance, Time)
        public bool StanceChanged;
        public bool InStance;

        // 커맨드 초기화 신호(spec 01 4.4): 자세 진입, 해제, 반전, 일시정지
        public bool HasReset;
        public CommandResetReason ResetReason;
    }
}
