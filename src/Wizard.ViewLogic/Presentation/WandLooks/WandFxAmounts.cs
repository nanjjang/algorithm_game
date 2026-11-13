namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 한 화면에서 지팡이 한 자루가 실제로 쓰는 장식의 양(상한·연출 줄이기·화면 규칙을 적용한 뒤).
    // TrailFx 가 null 이면 꼬리 띠를 그리지 않는다. MuzzleFrames 는 총구 섬광 그림의 장 수(0 = 전체 클립).
    // 본체 그림과 빛 깜빡임은 입자가 아니라 언제나 보인다(spec 04 15.12절 154)
    public readonly struct WandFxAmounts
    {
        public readonly int Idle;
        public readonly int Muzzle;
        public readonly int ShedPerBullet;
        public readonly int Hit;
        public readonly int TrailPx;
        public readonly string? TrailFx;
        public readonly string MuzzleFx;
        public readonly int MuzzleFrames;
        public readonly string HitFx;
        public readonly bool GroundMarks;
        public readonly bool BodyVisible;
        public readonly bool LightBlinkVisible;

        public WandFxAmounts(int idle, int muzzle, int shedPerBullet, int hit, int trailPx, string? trailFx, string muzzleFx, int muzzleFrames,
            string hitFx, bool groundMarks)
        {
            Idle = idle;
            Muzzle = muzzle;
            ShedPerBullet = shedPerBullet;
            Hit = hit;
            TrailPx = trailPx;
            TrailFx = trailFx;
            MuzzleFx = muzzleFx;
            MuzzleFrames = muzzleFrames;
            HitFx = hitFx;
            GroundMarks = groundMarks;
            BodyVisible = true;
            LightBlinkVisible = true;
        }
    }
}
