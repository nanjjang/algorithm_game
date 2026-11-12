using Wizard.Content;

namespace Wizard.BalanceLab.Feel
{
    // 두 번 누르는 커맨드(앞, 앞, K) 시도 하나
    internal sealed class AttemptRecord
    {
        public uint Tick;                 // K 를 누른 틱
        public long GapUnits;             // 마지막 방향 입력에서 K 까지(입력 시각 단위). PracticeGapReport 가 준 값
        public int FirstGapTicks;         // 첫째 앞에서 둘째 앞까지(틱. 방향 이벤트에는 오프셋이 없어 틱 단위다)
        public ArtId Outcome;             // 나간 기술. None = 나가지 않음(재사용 대기에 무시됨, 끝까지 풀리지 않음)
        public bool Ignored;              // 재사용 대기라 K 가 무시됨

        public bool Success => Outcome == ArtId.Spear;

        // 판정 근거에 적는 결과 이름
        public string ResultName
        {
            get
            {
                switch (Outcome)
                {
                    case ArtId.Spear:
                        return "spear";
                    case ArtId.Burst:
                        return "burst";
                    case ArtId.Recoil:
                        return "recoil";
                    case ArtId.None:
                        return Ignored ? "ignored_cooldown" : "unresolved";
                    default:
                        return "other";
                }
            }
        }
    }
}
