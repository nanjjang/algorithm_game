using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // 수락 조건: "입자 예산과 넘쳤을 때의 처리가 effects.md 9절 표와 같다(effects.md가 유일한 기준)".
    // 기대값은 effects.md 9.3절의 표에서 옮겨 적었다(데이터에서 베끼지 않는다).
    public sealed class ParticleBudgetDataTests
    {
        private static ParticleBudgetTable T => ParticleBudgetJson.Table;

        private static void AssertPool(ParticleScene scene, ParticleCategory c, ParticleRecurrence kind, int full, int reduced, int minimal)
        {
            ParticlePoolDef? p = T.Scene(scene).Pool(c);
            Assert.True(p != null, scene + "/" + c + " missing");
            Assert.Equal(kind, p!.Recurrence);
            Assert.Equal(full, p.Quota(MotionPreset.Full));
            Assert.Equal(reduced, p.Quota(MotionPreset.Reduced));
            Assert.Equal(minimal, p.Quota(MotionPreset.Minimal));
        }

        [Fact]
        public void BattleTableMatchesEffects9_3()
        {
            // | 우선순위 | 분류 | 전체 | 줄임 | 최소 | 넘쳤을 때 |
            AssertPool(ParticleScene.Battle, ParticleCategory.Art, ParticleRecurrence.OneShot, 400, 250, 120);
            AssertPool(ParticleScene.Battle, ParticleCategory.Hit, ParticleRecurrence.OneShot, 300, 150, 60);
            AssertPool(ParticleScene.Battle, ParticleCategory.Kill, ParticleRecurrence.OneShot, 300, 150, 60);
            AssertPool(ParticleScene.Battle, ParticleCategory.Shot, ParticleRecurrence.Recurring, 240, 120, 0);
            AssertPool(ParticleScene.Battle, ParticleCategory.WandIdle, ParticleRecurrence.Recurring, 60, 30, 0);
            AssertPool(ParticleScene.Battle, ParticleCategory.Environment, ParticleRecurrence.Recurring, 200, 100, 60);
            Assert.Equal(6, T.Scene(ParticleScene.Battle).Pools.Count);
            // 우선순위 1~6 은 표의 번호 그대로(합계가 넘치면 6 → 5 → 4 → 3 → 2, 1은 마지막)
            foreach (ParticlePoolDef p in T.Scene(ParticleScene.Battle).Pools)
            {
                Assert.Equal((int)p.Category, p.Priority);
                Assert.False(p.OutsideTotal);
            }
        }

        [Fact]
        public void DuelTableMatchesEffects9_3()
        {
            // 내 비기 / 상대 비기 각 400. 환경, 처치 0. 상대 지팡이 대기 30까지
            AssertPool(ParticleScene.Duel, ParticleCategory.Art, ParticleRecurrence.OneShot, 400, 250, 120);
            AssertPool(ParticleScene.Duel, ParticleCategory.ArtOpponent, ParticleRecurrence.OneShot, 400, 250, 120);
            Assert.Equal(0, T.Quota(ParticleScene.Duel, ParticleCategory.Environment, MotionPreset.Full));
            Assert.Equal(0, T.Quota(ParticleScene.Duel, ParticleCategory.Kill, MotionPreset.Full));
            Assert.Equal(30, T.Quota(ParticleScene.Duel, ParticleCategory.WandIdleOpponent, MotionPreset.Full));
            Assert.Equal(30, T.Shares.DuelOpponentIdleCap);
        }

        [Fact]
        public void PlazaTableMatchesEffects9_3()
        {
            AssertPool(ParticleScene.Plaza, ParticleCategory.WandIdle, ParticleRecurrence.Recurring, 400, 200, 60);
            AssertPool(ParticleScene.Plaza, ParticleCategory.Environment, ParticleRecurrence.Recurring, 200, 100, 60);
            AssertPool(ParticleScene.Plaza, ParticleCategory.Emote, ParticleRecurrence.OneShot, 200, 100, 30);
            Assert.Equal(800, T.SceneTotal(ParticleScene.Plaza, MotionPreset.Full));
            Assert.Equal(400, T.SceneTotal(ParticleScene.Plaza, MotionPreset.Reduced));
            Assert.Equal(150, T.SceneTotal(ParticleScene.Plaza, MotionPreset.Minimal));
            // 연습 허수아비를 칠 때만 전투의 분류 2와 4를 각 60씩 따로 준다(합계 밖)
            ParticlePoolDef hit = T.Scene(ParticleScene.Plaza).Pool(ParticleCategory.Hit)!;
            ParticlePoolDef shot = T.Scene(ParticleScene.Plaza).Pool(ParticleCategory.Shot)!;
            Assert.True(hit.OutsideTotal);
            Assert.True(shot.OutsideTotal);
            Assert.Equal(60, hit.Quota(MotionPreset.Full));
            Assert.Equal(60, shot.Quota(MotionPreset.Full));
            // 지팡이 대기 400을 나누는 법: 나 60까지, 가까운 순 8명은 절반(올림) 30까지
            Assert.Equal(60, T.Shares.PlazaSelfIdleCap);
            Assert.Equal(8, T.Shares.PlazaNearCount);
            Assert.Equal(30, T.Shares.PlazaNearIdleCap);
        }

        [Fact]
        public void EnhanceScreenTotalsMatchEffects9_3()
        {
            Assert.Equal(600, T.SceneTotal(ParticleScene.Enhance, MotionPreset.Full));
            Assert.Equal(300, T.SceneTotal(ParticleScene.Enhance, MotionPreset.Reduced));
            Assert.Equal(100, T.SceneTotal(ParticleScene.Enhance, MotionPreset.Minimal));
            Assert.Equal(ParticleRecurrence.Recurring, T.Scene(ParticleScene.Enhance).Pool(ParticleCategory.EnhanceCharge)!.Recurrence);
        }

        [Theory]
        // | 단계 구간 | 대기 | 총구 | 탄 1발이 흘리는 | 적중 1회 | 꼬리 띠 길이 |
        [InlineData(0, 3, 8, 2, 0, 4, 16)]
        [InlineData(4, 6, 16, 4, 1, 7, 24)]
        [InlineData(7, 9, 30, 5, 1, 8, 32)]
        [InlineData(10, 12, 45, 6, 2, 10, 40)]
        [InlineData(13, 13, 60, 8, 2, 12, 48)]
        public void WandTierCapsMatchEffects9_3(int from, int to, int idle, int muzzle, int shed, int hit, int trail)
        {
            for (int tier = from; tier <= to; tier++)
            {
                WandTierCap c = T.TierCap(tier);
                Assert.Equal((idle, muzzle, shed, hit, trail), (c.Idle, c.Muzzle, c.ShedPerBullet, c.Hit, c.TrailLengthPx));
            }
            Assert.Equal(13, T.MaxTier);
        }

        [Fact]
        public void LifetimesExtrasAndRulesMatchEffects9_3()
        {
            Assert.Equal(2000, T.Lifetimes.IdleMs);
            Assert.Equal(200, T.Lifetimes.MuzzleMs);
            Assert.Equal(300, T.Lifetimes.ShedMs);
            Assert.Equal(400, T.Lifetimes.HitMs);
            Assert.Equal((4, 2, -2), (T.Hit.ExtraCrit, T.Hit.ExtraWeak, T.Hit.ExtraResist));
            Assert.Equal(2, T.Hit.MinimalParticles);
            Assert.Equal((50, 3, 8), (T.Hit.MergeWindowMs, T.Hit.MergeOverHits, T.Hit.MergedParticles));
            Assert.Equal(5, T.Hit.KillHalveOverPerFrame);
            Assert.Equal(100, T.Shares.OneShotFadeMs);
            Assert.Equal((40, 200), (T.Shares.DamageNumbersMax, T.Shares.DamageMergeWindowMs));
            Assert.Equal((48, 4, 70), (T.Trail.MaxLengthPx, T.Trail.MaxFrames, T.Trail.MaxOpacityPercent));
            Assert.Equal((24, 16, 60, 800), (T.Trail.GroundMarksPerWand, T.Trail.GroundMarkMaxDiameterPx, T.Trail.GroundMarkMaxOpacityPercent, T.Trail.GroundMarkMaxLifeMs));
        }

        [Theory]
        // | 화면의 내 탄 수 | 처리 |
        [InlineData(0, true, 100, HitDecorMode.Full)]
        [InlineData(100, true, 100, HitDecorMode.Full)]
        [InlineData(101, false, 50, HitDecorMode.Half)]
        [InlineData(200, false, 50, HitDecorMode.Half)]
        [InlineData(201, false, 0, HitDecorMode.Minimal)]
        [InlineData(300, false, 0, HitDecorMode.Minimal)]
        public void BulletLoadThresholdsMatchEffects9_3(int bullets, bool shed, int trailPercent, HitDecorMode hit)
        {
            BulletLoadStep s = T.BulletLoad(bullets);
            Assert.Equal((shed, trailPercent, hit), (s.ShedEnabled, s.TrailLengthPercent, s.HitDecor));
        }

        [Fact]
        public void TableRejectsMalformedData()
        {
            var tiers = new List<WandTierCap>(T.WandTiers);
            var load = new List<BulletLoadStep>(T.BulletLoadSteps);
            var badScene = new ParticleSceneDef(ParticleScene.Battle, new[] { 10, 10, 10 }, new[]
            {
                new ParticlePoolDef(ParticleCategory.Art, 1, ParticleRecurrence.OneShot, new[] { 1, 1, 1 }, false),
            });
            Assert.Throws<ArgumentException>(() => new ParticleBudgetTable(new[] { badScene }, tiers, T.Lifetimes, T.Hit, load, T.Trail, T.Shares));

            var dupPriority = new ParticleSceneDef(ParticleScene.Battle, new[] { 2, 2, 2 }, new[]
            {
                new ParticlePoolDef(ParticleCategory.Art, 1, ParticleRecurrence.OneShot, new[] { 1, 1, 1 }, false),
                new ParticlePoolDef(ParticleCategory.Hit, 1, ParticleRecurrence.OneShot, new[] { 1, 1, 1 }, false),
            });
            Assert.Throws<ArgumentException>(() => new ParticleBudgetTable(new[] { dupPriority }, tiers, T.Lifetimes, T.Hit, load, T.Trail, T.Shares));

            var gapTiers = new[] { new WandTierCap(0, 3, 8, 2, 0, 4, 16), new WandTierCap(5, 13, 60, 8, 2, 12, 48) };
            Assert.Throws<ArgumentException>(() => new ParticleBudgetTable(T.Scenes, gapTiers, T.Lifetimes, T.Hit, load, T.Trail, T.Shares));
        }
    }
}
