namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "넘쳤을 때" 5번: 화면의 내 탄 수에 따라 자동으로 줄인다(다섯 지팡이가 같은 문턱).
    // MinBullets 이상이면 이 단계다(100개 이하 / 101~200 / 201 이상)
    public readonly struct BulletLoadStep
    {
        public readonly int MinBullets;
        public readonly bool ShedEnabled;          // 탄이 흘리는 입자
        public readonly int TrailLengthPercent;    // 꼬리 띠 길이 배율(0 = 끔)
        public readonly HitDecorMode HitDecor;     // 적중 장식

        public BulletLoadStep(int minBullets, bool shedEnabled, int trailLengthPercent, HitDecorMode hitDecor)
        {
            MinBullets = minBullets;
            ShedEnabled = shedEnabled;
            TrailLengthPercent = trailLengthPercent;
            HitDecor = hitDecor;
        }
    }
}
