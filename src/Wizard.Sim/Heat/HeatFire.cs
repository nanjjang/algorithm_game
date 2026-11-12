using Wizard.Content;

namespace Wizard.Sim.Heat
{
    // 6단계에서 J 를 눌렀을 때 일어난 일(spec 01 7.3 TryFire 의 결과). 세계는 이것으로 표시 이벤트를 내고,
    // 같은 단계의 Spell 모듈은 SimWorld.HeatFiredThisTick 으로 읽어 ISpellExecutor.Execute(isCrit, shotId)를 부른다.
    // 틱 안의 임시 값이다(상태가 아니다): 6단계 첫머리에 모든 플레이어 것을 지우고 다시 채운다.
    internal struct HeatFire
    {
        public HeatFireKind Kind;
        public uint Tick;
        public int ShotId;                 // Fired: 발사 1회의 묶음 id(ExecuteOptions.ShotId)
        public BlockId Core;
        public bool IsCrit;
        public bool ExemptUsed;            // 이번 발사가 열 면제를 썼다(HeatExemptConsumed)
        public int KilnAbsorbedHu;         // 불가마가 먹은 열(KilnAbsorbed). 0이면 없음
        public int HeatAddedHu;            // 게이지에 실제로 더한 열(면제·흡수 뒤)
        public int HeatAfterHu;
        public bool LockEntered;           // 이 발사로 과열 잠금에 들어갔다(HeatLockEntered)
        public bool UnstableBlowup;        // 불안정 핵 자폭(UnstableCoreExploded). 콤보 모듈이 콤보 0으로 읽는다(9.4)
    }
}
