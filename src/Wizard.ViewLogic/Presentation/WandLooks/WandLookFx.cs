using System;
using Wizard.ViewLogic.Presentation.Limits;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 겉모습 자료의 요청량을 화면 규칙(ParticleRequestPolicy, effects.md 9.3·17절)에 넣어 실제 양을 낸다.
    public static class WandLookFx
    {
        // 가장 단순한 형태의 총구 섬광: 그림 1장만(effects.md 17절)
        public const int SimplestMuzzleFrames = 1;
        public const int FullClip = 0;

        // 내 화면(전투)
        public static WandFxAmounts Self(ParticleRequestPolicy policy, WandLookResolved look, WandFxLevel level, int myBulletsOnScreen)
        {
            WandParticleRequest r = look.Particles;
            int stage = look.Stage;
            int trail = policy.TrailLengthPx(stage, r.TrailPx, level, myBulletsOnScreen, false);
            return new WandFxAmounts(
                policy.Idle(stage, r.Idle, level),
                policy.Muzzle(stage, r.Muzzle, level, false),
                policy.ShedPerBullet(stage, r.Shed, level, myBulletsOnScreen, false),
                policy.Hit(stage, r.Hit, level, myBulletsOnScreen, false, false, false, false),
                trail, trail > 0 ? look.TrailFx : null, look.Set.Decor.Muzzle, FullClip, look.Set.Decor.Hit, true);
        }

        // 결투 상대 화면에서 본 내 지팡이(DECISIONS 2.7): 본체는 실제 단계 그대로, 탄 장식은 가장 단순한 형태 —
        // 꼬리 없음, 흘림 0, 총구 입자 0(섬광 그림 1장), 적중 최소형, 바닥 흔적 없음. 대기 입자는 요청량의 절반(올림).
        // 상대의 단계나 치장과 무관하게 같다(꼬리 덧씌움도 쓰지 않는다). level 은 보는 사람의 "지팡이 효과" 설정
        public static WandFxAmounts DuelOpponent(ParticleRequestPolicy policy, WandLookResolved look, WandFxLevel level)
        {
            WandParticleRequest r = look.Base.Particles;
            int stage = look.Stage;
            return new WandFxAmounts(
                policy.DuelOpponentIdle(stage, r.Idle, level),
                policy.Muzzle(stage, r.Muzzle, level, true),
                policy.ShedPerBullet(stage, r.Shed, level, 0, true),
                policy.Hit(stage, r.Hit, level, 0, false, false, false, true),
                policy.TrailLengthPx(stage, r.TrailPx, level, 0, true), null,
                look.Base.DuelDecor.Muzzle, SimplestMuzzleFrames, look.Base.DuelDecor.Hit, false);
        }

        // 광장: nearRank 0 = 나, 1.. = 가까운 순. 대기 입자만 나누고 본체·빛 깜빡임은 언제나 보인다(effects.md 9.3)
        public static WandFxAmounts Plaza(ParticleRequestPolicy policy, WandLookResolved look, int nearRank, WandFxLevel ownLevel, OthersWandFx others)
        {
            if (nearRank < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nearRank));
            }
            WandParticleRequest r = look.Particles;
            return new WandFxAmounts(
                policy.PlazaIdle(nearRank, look.Stage, r.Idle, ownLevel, others), 0, 0, 0, 0, null,
                look.Set.Decor.Muzzle, FullClip, look.Set.Decor.Hit, false);
        }
    }
}
