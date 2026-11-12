namespace Wizard.DataBuild.Validate.Evasion
{
    // 공격이 쏘는 탄(R3·R4 가 본다). 한 번에 쏘는 묶음이면 ShotsPerSecond = 0, 이어 쏘는 줄기면 초당 발수와 초당 도는 각
    public sealed class EvasionProj
    {
        public int SpeedMilli { get; set; }                     // 탄속(milli 칸/초)

        public int Count { get; set; } = 1;                     // 한 번에 쏘는 탄 수

        public decimal SpreadDegrees { get; set; }              // 이웃한 탄 사이 각도(도)

        public int ShotsPerSecond { get; set; }                 // 이어 쏘는 줄기의 초당 발수. 0 이면 줄기가 아니다

        public decimal SweepDegreesPerSecond { get; set; }      // 줄기가 초당 도는 각(도)
    }
}
