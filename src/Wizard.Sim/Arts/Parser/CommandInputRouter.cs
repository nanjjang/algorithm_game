using Wizard.Sim.Input;
using Wizard.Sim.Player;

namespace Wizard.Sim.Arts
{
    // 2단계(입력 이벤트마다)에서 Player 모듈이 낸 결과(PlayerInputResult: 자세 변화, 초기화, 상대 방향)를 판정기로 옮긴다
    // (spec 01 3.8: KeyState → 자세 → 시야 → 판정기). 이벤트는 이미 InputOrder 순이다(같은 오프셋이면 방향이 K보다 먼저, R-14).
    // 받아들이지 않은 이벤트(눌린 키의 누름 = 자동 반복, 안 눌린 키의 뗌)는 판정기에 가지 않는다(R-07, U-11).
    // Stage02_Arts 를 구현하는 작업(시제품 M1-SIM-09, 비기 공통 M5-ART-01)이 부른다.
    internal static class CommandInputRouter
    {
        // 참이면 이 이벤트가 K 누름이었고 decision 이 지금 적용할 판정이다(OnSkillPress 와 같은 뜻)
        public static bool Route(ICommandParser parser, in PlayerInputResult r, ICrystalWallet wallet, in UnlockSet unlocks,
                                 out ArtDecision decision)
        {
            decision = default;
            if (!r.Accepted)
            {
                return false;
            }
            if (r.StanceChanged)
            {
                parser.OnStanceChanged(r.InStance, r.Time);
            }
            if (r.HasReset)
            {
                parser.Reset(r.ResetReason);
            }
            if (r.HasDirection)
            {
                parser.OnDirection(r.Direction, r.Time);
            }
            if (r.Key == LogicalKey.Skill && r.Down)
            {
                return parser.OnSkillPress(r.Time, wallet, in unlocks, out decision);
            }
            return false;
        }
    }
}
