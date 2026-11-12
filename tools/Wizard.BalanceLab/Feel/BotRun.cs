namespace Wizard.BalanceLab.Feel
{
    // 봇 하나의 한 번의 실행 결과. 창 = 완전한 주기만 담은 틱 구간 [WindowStartTick, WindowEndTick)
    internal sealed class BotRun
    {
        public BotKind Kind;
        public uint Ticks;                    // 돌린 틱
        public int Cycles;                    // 창 안의 완전한 주기 수(0이면 재지 못함)
        public long WindowStartTick;
        public long WindowEndTick;
        public long WindowDamageRaw;          // 창 안에서 쏜 피해 합(Fix64 Raw). 발사한 탄의 기본 피해 × 임계 배율, 맞았는지와 무관
        public int WindowShots;
        public int WindowCritShots;
        public int TotalShots;                // 실행 전체
        public int GrazeEvents;               // 실행 중 스침 보상(GrazeAwarded, HeatExemptArmed)의 수. 0이 아니면 열 주기가 오염됐다

        public long WindowTicks => WindowEndTick - WindowStartTick;
    }
}
