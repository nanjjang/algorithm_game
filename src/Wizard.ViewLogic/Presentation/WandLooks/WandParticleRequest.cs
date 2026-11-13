namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // spec 04 12.5절 지팡이별 단계 요청량(대기 / 총구 / 흘림 / 적중 / 꼬리px). 상한으로 자르는 것은 ParticleRequestPolicy 가 한다.
    // TrailSpecified 가 거짓이면 명세가 꼬리 길이를 적지 않은 칸(wand.3 +1 "잔상", wand.5 +11~+13 "—", Q-22)이고
    // TrailPx 는 로더가 가장 가까운 아래 단계의 값을 물려 넣은 것이다 [임시: Wm 4.2 "이후 교체 전까지 상속"]
    public readonly struct WandParticleRequest
    {
        public readonly int Idle;
        public readonly int Muzzle;
        public readonly int Shed;
        public readonly int Hit;
        public readonly int TrailPx;
        public readonly bool TrailSpecified;

        public WandParticleRequest(int idle, int muzzle, int shed, int hit, int trailPx, bool trailSpecified)
        {
            Idle = idle;
            Muzzle = muzzle;
            Shed = shed;
            Hit = hit;
            TrailPx = trailPx;
            TrailSpecified = trailSpecified;
        }

        public WandParticleRequest WithTrail(int trailPx) => new WandParticleRequest(Idle, Muzzle, Shed, Hit, trailPx, TrailSpecified);
    }
}
