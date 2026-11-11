using Wizard.Core;

namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5. 시각은 전부 입력 시각(long)이다(0.07초 = 1,075). 판정기 상태는 스냅숏·WriteState·해시에 들어간다. 순번(Seq)은 넣지 않는다
    public interface ICommandParser
    {
        void Reset(CommandResetReason reason);
        void OnStanceChanged(bool inStance, long time);
        void OnDirection(CmdDir dir, long time);                                // 자세 밖이면 무시
        bool OnSkillPress(long time, ICrystalWallet wallet, in UnlockSet unlocks, out ArtDecision decision);
        bool TryTakeBuffered(long tickEndTime, out ArtDecision decision);       // 재사용 대기 끝에 나가는 보관된 K
        CommandPreview Preview(long now, ICrystalWallet wallet, in UnlockSet unlocks);   // HUD "지금 K ="
        void WriteState(ref StateWriter w);                                     // 커서, 방향 시각 8개, 대기 끝, 보관 결과
        void CopyFrom(ICommandParser other);                                    // 스냅숏
    }
}
