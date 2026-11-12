namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "지팡이 문서가 채우는 몫" — 단계 구간별 요청 상한(다섯 지팡이 공통). 지팡이마다 따로 주는 몫은 없다
    public readonly struct WandTierCap
    {
        public readonly int MinTier;
        public readonly int MaxTier;
        public readonly int Idle;            // 대기 입자(동시)
        public readonly int Muzzle;          // 발사 1회의 총구 입자
        public readonly int ShedPerBullet;   // 탄 1발이 흘리는 입자(동시)
        public readonly int Hit;             // 적중 1회
        public readonly int TrailLengthPx;   // 꼬리 띠 길이(입자가 아니다)

        public WandTierCap(int minTier, int maxTier, int idle, int muzzle, int shedPerBullet, int hit, int trailLengthPx)
        {
            MinTier = minTier;
            MaxTier = maxTier;
            Idle = idle;
            Muzzle = muzzle;
            ShedPerBullet = shedPerBullet;
            Hit = hit;
            TrailLengthPx = trailLengthPx;
        }

        public int Of(WandParticleKind kind)
        {
            switch (kind)
            {
                case WandParticleKind.Idle: return Idle;
                case WandParticleKind.Muzzle: return Muzzle;
                case WandParticleKind.Shed: return ShedPerBullet;
                case WandParticleKind.Hit: return Hit;
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
