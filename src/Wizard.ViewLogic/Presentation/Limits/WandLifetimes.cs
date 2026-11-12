namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "열마다 세는 법"의 수명 한도(ms)
    public readonly struct WandLifetimes
    {
        public readonly int IdleMs;
        public readonly int MuzzleMs;
        public readonly int ShedMs;
        public readonly int HitMs;

        public WandLifetimes(int idleMs, int muzzleMs, int shedMs, int hitMs)
        {
            IdleMs = idleMs;
            MuzzleMs = muzzleMs;
            ShedMs = shedMs;
            HitMs = hitMs;
        }

        public int Of(WandParticleKind kind)
        {
            switch (kind)
            {
                case WandParticleKind.Idle: return IdleMs;
                case WandParticleKind.Muzzle: return MuzzleMs;
                case WandParticleKind.Shed: return ShedMs;
                case WandParticleKind.Hit: return HitMs;
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
