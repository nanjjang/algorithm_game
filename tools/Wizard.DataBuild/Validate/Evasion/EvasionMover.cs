namespace Wizard.DataBuild.Validate.Evasion
{
    // R2 가 보는 몸(spec 05 1.2 R2, 10.5): 예고 없이 몸으로 부딪치는가와 걷는 속도
    public sealed class EvasionMover
    {
        public string File { get; set; } = "";
        public string Pointer { get; set; } = "";
        public string Owner { get; set; } = "";

        public int ContactDamage { get; set; }                  // 0 이면 몸 접촉이 없다(R2 대상 아님. 늑대)

        public int MoveSpeedMilli { get; set; }                 // 걷는 속도(milli 칸/초)

        public bool IsBoss { get; set; }                        // 보스는 북두꺼비 버프를 받지 않는다(spec 05 3.8)

        // 플레이어가 서리밭 안에 있을 때의 걷는 속도(흰뿔 2.4, spec 05 10.4). null 이면 서리밭과 상관없다
        public int? FrostMoveSpeedMilli { get; set; }
    }
}
