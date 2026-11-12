using Wizard.Core;

namespace Wizard.Sim.Heat
{
    // spec 01 7.6의 변이가 과열에 거는 값. 수치는 변이 데이터(data/sim/mutations.json, M5-CHR-05)가 주인이고,
    // 그 데이터를 읽는 모듈(Characters)이 SimWorld.SetHeatModifiers 로 넣는다. 이 모듈은 받은 값을 그대로 쓴다(코드에 수치가 없다).
    // 기본값(None)은 "변이 없음"이다.
    internal struct HeatModifiers
    {
        public int CritStartCapHu;          // 과민 회로: 임계 시작(8,400). 0이면 없음(7.6.3)
        public int ExtraLockTicks;          // 과민 회로: 잠금 추가(90)(7.6.3)
        public int CoolPermille;            // 냉각 배율(탐식 500, 없음 1000)(7.6.5)
        public int KillHeatHu;              // 탐식: 처치당 열 감소(480). 0이면 없음(7.6.5)
        public int KillWindowCapHu;         // 탐식: 최근 60틱 창의 감소 합 상한(2,400)(7.6.5)
        public int UnstableChancePermille;  // 불안정 핵: 발사당 자폭 확률(50). 0이면 없음. 폭발 코어일 때만 넣는다(7.6.4)
        public int UnstableLockTicks;       // 불안정 핵: 자폭 잠금(120)(7.6.4)

        private const int PermilleOne = 1000;    // 천분율 1.0(단위 환산)

        public static HeatModifiers None => new HeatModifiers { CoolPermille = PermilleOne };

        public bool IsValid =>
            CritStartCapHu >= 0 && ExtraLockTicks >= 0 && CoolPermille >= 0 && KillHeatHu >= 0 && KillWindowCapHu >= 0
            && UnstableChancePermille >= 0 && UnstableChancePermille <= PermilleOne && UnstableLockTicks >= 0;

        // 바이트: 위 필드 순서대로 i32 × 7
        public void Write(ref StateWriter w)
        {
            w.I32(CritStartCapHu);
            w.I32(ExtraLockTicks);
            w.I32(CoolPermille);
            w.I32(KillHeatHu);
            w.I32(KillWindowCapHu);
            w.I32(UnstableChancePermille);
            w.I32(UnstableLockTicks);
        }

        public void Read(ref StateReader r)
        {
            CritStartCapHu = r.I32();
            ExtraLockTicks = r.I32();
            CoolPermille = r.I32();
            KillHeatHu = r.I32();
            KillWindowCapHu = r.I32();
            UnstableChancePermille = r.I32();
            UnstableLockTicks = r.I32();
            if (!IsValid)
            {
                throw new SimInvariantException("heat: modifiers out of range");
            }
        }
    }
}
