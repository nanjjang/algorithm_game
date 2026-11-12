using System.Collections.Generic;
using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // effects.md 9.3 "넘쳤을 때의 처리 — 하나뿐인 규칙" 1~4번
    public sealed class ParticleBudgetTests
    {
        private static ParticleBudgetTable Data => ParticleBudgetJson.Table;

        [Fact]
        public void Rule2_RecurringStopsCreatingWhenFullAndNeverEvicts()
        {
            var b = new ParticleBudget(Data, ParticleScene.Battle, MotionPreset.Full);
            Assert.Equal(50, b.Request(ParticleCategory.WandIdle, 50, 2000, 0).Granted);
            Assert.Equal(10, b.Request(ParticleCategory.WandIdle, 50, 2000, 100).Granted);   // 몫 60 까지만
            Assert.Equal(0, b.Request(ParticleCategory.WandIdle, 5, 2000, 200).Granted);     // 차면 새로 만들지 않는다
            Assert.Equal(60, b.Live(ParticleCategory.WandIdle));
            Assert.Equal(0, b.PendingEvictionCount);                                         // 이미 나온 것은 지우지 않는다(깜빡임 없음)

            Assert.Equal(50, b.Request(ParticleCategory.WandIdle, 50, 2000, 2000).Granted);  // 첫 묶음이 수명대로 사라진 뒤
            Assert.Equal(60, b.Live(ParticleCategory.WandIdle));
        }

        [Fact]
        public void Rule3_OneShotAlwaysShowsTheNewEventAndFadesTheOldestFirst()
        {
            var b = new ParticleBudget(Data, ParticleScene.Battle, MotionPreset.Full);
            ParticleGrant a = b.Request(ParticleCategory.Hit, 200, 400, 0);
            ParticleGrant c = b.Request(ParticleCategory.Hit, 100, 400, 10);
            Assert.Equal(300, b.Live(ParticleCategory.Hit));

            ParticleGrant d = b.Request(ParticleCategory.Hit, 12, 400, 20);
            Assert.Equal(12, d.Granted);                                   // 새 사건은 항상 보인다
            Assert.Equal(300, b.Live(ParticleCategory.Hit));
            var ev = new List<ParticleEviction>();
            b.DrainEvictions(ev);
            Assert.Single(ev);
            Assert.Equal(a.BatchId, ev[0].BatchId);                        // 가장 오래된 것부터
            Assert.Equal(12, ev[0].Count);
            Assert.Equal(100, ev[0].FadeMs);                               // 0.1초에 걸쳐 흐려진다
            Assert.Equal(ParticleCategory.Hit, ev[0].Category);
            Assert.NotEqual(c.BatchId, ev[0].BatchId);

            // 한 사건이 몫보다 크면 몫만큼만(나머지 묶음은 전부 자리를 내준다)
            ParticleGrant big = b.Request(ParticleCategory.Hit, 1000, 400, 30);
            Assert.Equal(300, big.Granted);
            Assert.Equal(300, b.Live(ParticleCategory.Hit));
        }

        [Fact]
        public void Rule4_TotalOverflowCutsLowerPrioritiesFirstAndArtLast()
        {
            // 합계(10)가 몫의 합보다 작은 표로 4번 규칙만 본다
            var pools = new[]
            {
                new ParticlePoolDef(ParticleCategory.Art, 1, ParticleRecurrence.OneShot, new[] { 10, 10, 10 }, false),
                new ParticlePoolDef(ParticleCategory.Hit, 2, ParticleRecurrence.OneShot, new[] { 10, 10, 10 }, false),
                new ParticlePoolDef(ParticleCategory.Kill, 3, ParticleRecurrence.OneShot, new[] { 10, 10, 10 }, false),
                new ParticlePoolDef(ParticleCategory.Shot, 4, ParticleRecurrence.Recurring, new[] { 10, 10, 10 }, false),
                new ParticlePoolDef(ParticleCategory.Environment, 6, ParticleRecurrence.Recurring, new[] { 10, 10, 10 }, false),
            };
            var table = new ParticleBudgetTable(new[] { new ParticleSceneDef(ParticleScene.Battle, new[] { 10, 10, 10 }, pools) },
                Data.WandTiers, Data.Lifetimes, Data.Hit, Data.BulletLoadSteps, Data.Trail, Data.Shares);
            var b = new ParticleBudget(table, ParticleScene.Battle, MotionPreset.Full);

            Assert.Equal(5, b.Request(ParticleCategory.Kill, 5, 1000, 0).Granted);
            Assert.Equal(5, b.Request(ParticleCategory.Hit, 5, 1000, 1).Granted);
            Assert.Equal(10, b.LiveTotal);

            // 반복 분류(환경, 탄)는 합계가 찬 동안 새로 만들지 않는다
            Assert.Equal(0, b.Request(ParticleCategory.Environment, 3, 1000, 2).Granted);
            Assert.Equal(0, b.Request(ParticleCategory.Shot, 3, 1000, 2).Granted);

            // 비기 4: 처치(3)부터 줄인다
            Assert.Equal(4, b.Request(ParticleCategory.Art, 4, 1000, 3).Granted);
            Assert.Equal((1, 5, 4), (b.Live(ParticleCategory.Kill), b.Live(ParticleCategory.Hit), b.Live(ParticleCategory.Art)));

            // 비기 4 더: 처치의 남은 1, 그다음 적중(2)에서 3
            Assert.Equal(4, b.Request(ParticleCategory.Art, 4, 1000, 4).Granted);
            Assert.Equal((0, 2, 8), (b.Live(ParticleCategory.Kill), b.Live(ParticleCategory.Hit), b.Live(ParticleCategory.Art)));

            // 적중 5: 비기(1)는 건드리지 않는다. 자기 분류의 오래된 2를 내주고, 그래도 모자라면 발급을 줄인다
            Assert.Equal(2, b.Request(ParticleCategory.Hit, 5, 1000, 5).Granted);
            Assert.Equal((0, 2, 8), (b.Live(ParticleCategory.Kill), b.Live(ParticleCategory.Hit), b.Live(ParticleCategory.Art)));
            Assert.Equal(10, b.LiveTotal);

            var ev = new List<ParticleEviction>();
            b.DrainEvictions(ev);
            foreach (ParticleEviction e in ev)
            {
                Assert.NotEqual(ParticleCategory.Art, e.Category);
                Assert.NotEqual(ParticleCategory.Environment, e.Category);
                Assert.NotEqual(ParticleCategory.Shot, e.Category);
            }
        }

        [Fact]
        public void NeverExceedsQuotaOrTotalUnderAnyRequestStream()
        {
            foreach (MotionPreset preset in new[] { MotionPreset.Full, MotionPreset.Reduced, MotionPreset.Minimal })
            {
                foreach (ParticleScene scene in new[] { ParticleScene.Battle, ParticleScene.Duel, ParticleScene.Plaza, ParticleScene.Enhance })
                {
                    var b = new ParticleBudget(Data, scene, preset);
                    var rng = new DisplayRandom(7u);
                    var cats = new List<ParticleCategory>();
                    foreach (ParticlePoolDef p in Data.Scene(scene).Pools)
                    {
                        cats.Add(p.Category);
                    }
                    for (int i = 0; i < 4000; i++)
                    {
                        ParticleCategory c = cats[(int)(rng.NextUInt() % (uint)cats.Count)];
                        b.Request(c, 1 + (int)(rng.NextUInt() % 40u), 100 + (int)(rng.NextUInt() % 2000u), i * 5);
                        Assert.True(b.LiveTotal <= b.Total, scene + "/" + preset + " total");
                        foreach (ParticleCategory k in cats)
                        {
                            Assert.True(b.Live(k) <= b.Quota(k) && b.Live(k) >= 0, scene + "/" + preset + "/" + k);
                        }
                    }
                }
            }
        }

        [Fact]
        public void PresetChangeAppliesToNewRequests()
        {
            var b = new ParticleBudget(Data, ParticleScene.Battle, MotionPreset.Full);
            Assert.Equal(240, b.Request(ParticleCategory.Shot, 240, 1000, 0).Granted);
            b.SetPreset(MotionPreset.Minimal);
            Assert.Equal(0, b.Quota(ParticleCategory.Shot));
            Assert.Equal(0, b.Request(ParticleCategory.Shot, 1, 1000, 10).Granted);
            Assert.Equal(240, b.Live(ParticleCategory.Shot));      // 이미 나온 것은 제 수명대로
            b.Advance(1000);
            Assert.Equal(0, b.Live(ParticleCategory.Shot));
            Assert.Equal(300, b.Total);
        }

        [Fact]
        public void PlazaPracticePoolsSitOutsideThePlazaTotal()
        {
            var b = new ParticleBudget(Data, ParticleScene.Plaza, MotionPreset.Full);
            Assert.Equal(400, b.Request(ParticleCategory.WandIdle, 400, 2000, 0).Granted);
            Assert.Equal(200, b.Request(ParticleCategory.Environment, 200, 2000, 0).Granted);
            Assert.Equal(200, b.Request(ParticleCategory.Emote, 200, 2000, 0).Granted);
            Assert.Equal(800, b.LiveTotal);
            Assert.Equal(60, b.Request(ParticleCategory.Hit, 60, 400, 0).Granted);     // 연습 허수아비: 따로 60
            Assert.Equal(60, b.Request(ParticleCategory.Shot, 60, 200, 0).Granted);
            Assert.Equal(800, b.LiveTotal);
        }

        [Fact]
        public void CategoryOutsideTheSceneGetsNothing()
        {
            var b = new ParticleBudget(Data, ParticleScene.Duel, MotionPreset.Full);
            Assert.Equal(0, b.Request(ParticleCategory.Environment, 10, 1000, 0).Granted);   // 결투에는 날씨가 없다
            Assert.Equal(0, b.Request(ParticleCategory.Kill, 10, 1000, 0).Granted);          // 처치가 없다
            Assert.Equal(0, b.Request(ParticleCategory.Emote, 10, 1000, 0).Granted);         // 표에 없는 분류
            Assert.Equal(400, b.Request(ParticleCategory.ArtOpponent, 400, 1000, 0).Granted); // 상대 비기는 따로 400
            Assert.Equal(400, b.Request(ParticleCategory.Art, 400, 1000, 0).Granted);
        }

        [Fact]
        public void ReleaseFreesTheBatch()
        {
            var b = new ParticleBudget(Data, ParticleScene.Enhance, MotionPreset.Minimal);
            ParticleGrant g = b.Request(ParticleCategory.EnhanceCharge, 100, 1000, 0);
            Assert.Equal(100, g.Granted);
            Assert.Equal(0, b.Request(ParticleCategory.EnhanceCharge, 1, 1000, 1).Granted);   // 넘치면 충전 입자부터 새로 만들지 않는다
            b.Release(g.BatchId);
            Assert.Equal(0, b.LiveTotal);
            Assert.Equal(1, b.Request(ParticleCategory.EnhanceCharge, 1, 1000, 2).Granted);
        }
    }
}
