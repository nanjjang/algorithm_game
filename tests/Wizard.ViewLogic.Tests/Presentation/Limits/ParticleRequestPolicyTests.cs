using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // effects.md 9.3 "지팡이 문서가 채우는 몫", 내 탄 수 문턱, 결투·광장 나누기, 18절 "지팡이 효과"
    public sealed class ParticleRequestPolicyTests
    {
        private static readonly ParticleRequestPolicy P = new ParticleRequestPolicy(ParticleBudgetJson.Table);

        [Fact]
        public void RequestsAreCappedByTierBand()
        {
            Assert.Equal(8, P.Idle(3, 100, WandFxLevel.All));
            Assert.Equal(16, P.Idle(4, 100, WandFxLevel.All));
            Assert.Equal(60, P.Idle(13, 100, WandFxLevel.All));
            Assert.Equal(18, P.Idle(13, 18, WandFxLevel.All));           // 성에 +13 의 18 은 그대로
            Assert.Equal(8, P.Muzzle(13, 100, WandFxLevel.All, false));
            Assert.Equal(6, P.Muzzle(13, 6, WandFxLevel.All, false));    // 달구·갈피 +13 의 6
            Assert.Equal(0, P.ShedPerBullet(3, 5, WandFxLevel.All, 0, false));
            Assert.Equal(1, P.ShedPerBullet(5, 1, WandFxLevel.All, 0, false));   // 초롱 +5 의 1
            Assert.Equal(12, P.Hit(13, 100, WandFxLevel.All, 0, false, false, false, false));
            Assert.Equal(48, P.TrailLengthPx(13, 100, WandFxLevel.All, 0, false));
            Assert.Equal(16, P.TrailLengthPx(0, 100, WandFxLevel.All, 0, false));
        }

        [Fact]
        public void HitExtrasAreAddedAfterTheCap()
        {
            Assert.Equal(18, P.Hit(13, 12, WandFxLevel.All, 0, true, true, false, false));   // 임계와 약점이 다 붙은 18개짜리
            Assert.Equal(10, P.Hit(13, 12, WandFxLevel.All, 0, false, false, true, false));
            Assert.Equal(0, P.Hit(0, 0, WandFxLevel.All, 0, false, false, true, false));     // 음수가 되지 않는다
        }

        [Fact]
        public void WandFxReducedHalvesRoundingUpAndMinimalDropsAmbientParticles()
        {
            Assert.Equal(9, P.Idle(13, 18, WandFxLevel.Reduced));
            Assert.Equal(3, P.Muzzle(13, 5, WandFxLevel.Reduced, false));
            Assert.Equal(4, P.Hit(13, 7, WandFxLevel.Reduced, 0, false, false, false, false));
            Assert.Equal(0, P.Idle(13, 18, WandFxLevel.Minimal));
            Assert.Equal(0, P.Muzzle(13, 8, WandFxLevel.Minimal, false));
            Assert.Equal(0, P.ShedPerBullet(13, 2, WandFxLevel.Minimal, 0, false));
            Assert.Equal(2, P.Hit(13, 12, WandFxLevel.Minimal, 0, false, false, false, false));   // 최소형(입자 2개)
            Assert.Equal(0, P.TrailLengthPx(13, 48, WandFxLevel.Minimal, 0, false));
        }

        [Fact]
        public void MyBulletCountThresholds()
        {
            Assert.Equal(2, P.ShedPerBullet(13, 2, WandFxLevel.All, 100, false));
            Assert.Equal(0, P.ShedPerBullet(13, 2, WandFxLevel.All, 101, false));       // 흘리는 입자를 끈다
            Assert.Equal(24, P.TrailLengthPx(13, 48, WandFxLevel.All, 150, false));     // 꼬리 띠 절반
            Assert.Equal(0, P.TrailLengthPx(13, 48, WandFxLevel.All, 201, false));      // 꼬리 띠를 끈다
            Assert.Equal(6, P.Hit(13, 12, WandFxLevel.All, 150, false, false, false, false));   // 적중 장식 절반
            Assert.Equal(2, P.Hit(13, 12, WandFxLevel.All, 201, false, false, false, false));   // 최소형
            // [임시] 두 줄이기가 겹치면 가장 강한 하나: 줄임 + 150발 = 절반 한 번
            Assert.Equal(6, P.Hit(13, 12, WandFxLevel.Reduced, 150, false, false, false, false));
        }

        [Fact]
        public void DuelOpponentGetsTheSimplestDecor()
        {
            Assert.Equal(0, P.TrailLengthPx(13, 48, WandFxLevel.All, 0, true));
            Assert.Equal(0, P.ShedPerBullet(13, 2, WandFxLevel.All, 0, true));
            Assert.Equal(2, P.Hit(13, 12, WandFxLevel.All, 0, false, false, false, true));
            Assert.Equal(0, P.Muzzle(13, 8, WandFxLevel.All, true));
            Assert.Equal(30, P.DuelOpponentIdle(13, 60, WandFxLevel.All));   // 절반(올림), 30까지
            Assert.Equal(9, P.DuelOpponentIdle(13, 18, WandFxLevel.All));
            Assert.Equal(1, P.DuelOpponentIdle(0, 1, WandFxLevel.All));
        }

        [Fact]
        public void PlazaIdleShareWorstCaseIs300()
        {
            int sum = 0;
            for (int rank = 0; rank < 40; rank++)
            {
                sum += P.PlazaIdle(rank, 13, 60, WandFxLevel.All, OthersWandFx.All);
            }
            Assert.Equal(300, sum);                                                     // 60 + 8 × 30 + 31 × 0
            Assert.Equal(60, P.PlazaIdle(0, 13, 60, WandFxLevel.All, OthersWandFx.All));
            Assert.Equal(30, P.PlazaIdle(8, 13, 60, WandFxLevel.All, OthersWandFx.All));
            Assert.Equal(0, P.PlazaIdle(9, 13, 60, WandFxLevel.All, OthersWandFx.All));
            Assert.Equal(0, P.PlazaIdle(1, 13, 60, WandFxLevel.All, OthersWandFx.Reduced));   // 남의 지팡이 효과 줄임 → 8명 몫 0
            Assert.Equal(0, P.PlazaIdle(1, 13, 60, WandFxLevel.All, OthersWandFx.Off));
            // 지금 지팡이 문서로는 최악이 나 18 + 8명 × 9 = 90
            int now = P.PlazaIdle(0, 13, 18, WandFxLevel.All, OthersWandFx.All);
            for (int rank = 1; rank < 40; rank++)
            {
                now += P.PlazaIdle(rank, 13, 18, WandFxLevel.All, OthersWandFx.All);
            }
            Assert.Equal(90, now);
        }

        [Fact]
        public void KillShardsHalveWhenMoreThanFiveDieInOneFrame()
        {
            Assert.Equal(10, P.KillShards(10, 5));
            Assert.Equal(5, P.KillShards(10, 6));
            Assert.Equal(4, P.KillShards(7, 6));
        }

        [Fact]
        public void LifetimesAndCategoriesPerColumn()
        {
            Assert.Equal(2000, P.MaxLifetimeMs(WandParticleKind.Idle));
            Assert.Equal(200, P.MaxLifetimeMs(WandParticleKind.Muzzle));
            Assert.Equal(300, P.MaxLifetimeMs(WandParticleKind.Shed));
            Assert.Equal(400, P.MaxLifetimeMs(WandParticleKind.Hit));
            Assert.Equal(ParticleCategory.WandIdle, ParticleRequestPolicy.CategoryOf(WandParticleKind.Idle));
            Assert.Equal(ParticleCategory.Shot, ParticleRequestPolicy.CategoryOf(WandParticleKind.Muzzle));
            Assert.Equal(ParticleCategory.Shot, ParticleRequestPolicy.CategoryOf(WandParticleKind.Shed));
            Assert.Equal(ParticleCategory.Hit, ParticleRequestPolicy.CategoryOf(WandParticleKind.Hit));
            Assert.Equal(8, P.MergedHit);
        }
    }
}
