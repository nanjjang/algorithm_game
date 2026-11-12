using Wizard.Core;

namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5. 시각은 전부 입력 시각(long)이다(0.07초 = 1,075). 판정기 상태는 스냅숏·WriteState·해시에 들어간다. 순번(Seq)은 넣지 않는다
    public interface ICommandParser
    {
        void Reset(CommandResetReason reason);
        void OnStanceChanged(bool inStance, long time);
        void OnDirection(CmdDir dir, long time);                                // 자세 밖이면 무시
        // 참이면 decision 이 지금 적용할 판정(발동 또는 불발). 거짓이면 재사용 대기로 무시했거나 보관했다
        bool OnSkillPress(long time, ICrystalWallet wallet, in UnlockSet unlocks, out ArtDecision decision);
        // 재사용 대기 끝에 나가는 보관된 K. tickStartTime = 이번 틱의 첫 입력 시각(틱 × 256). 그 값이 대기 끝 이상이면 낸다
        // → 발동 틱 = ceil(대기 끝 ÷ 256)(spec 03 10절). 5단계에서 틱마다 한 번 부른다
        bool TryTakeBuffered(long tickStartTime, out ArtDecision decision);
        CommandPreview Preview(long now, ICrystalWallet wallet, in UnlockSet unlocks);   // HUD "지금 K =". 상태와 지갑을 바꾸지 않는다
        void WriteState(ref StateWriter w);                                     // 커서, 자세, 방향 시각 8개, 대기 끝, 보관 결과
        void ReadState(ref StateReader r);                                      // WriteState 의 역
        void CopyFrom(ICommandParser other);                                    // 스냅숏. 같은 캐릭터의 트라이를 쓰는 판정기끼리
    }
}
