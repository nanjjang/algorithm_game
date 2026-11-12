using System.Collections.Generic;
using Wizard.Content;
using Wizard.Sim.Events;
using Wizard.Sim.Graze;
using Wizard.Sim.Heat;
using Xunit;
using static Wizard.Sim.Tests.Heat.Graze.GrazeHeatKit;

namespace Wizard.Sim.Tests.Heat.Graze
{
    // 작업 카드 M1-SIM-10 의 수락 조건 중 벡터 밖의 것: 비기 스침(5단계)과 9단계 스침이 6단계 발사에 반영되는 때,
    // 같은 틱 두 시전의 열 면제, 결투의 줄어든 보상(spec 01 12.2), 결투 스침 간격 안의 연출만 스침.
    public sealed class GrazeHeatPipelineTests
    {
        private static ContentDb Db => HeatData.Baseline;

        // 열 40(마력탄 10발, 틱 0~81, 틱 82 에 J 뗌). 다음 발사 가능 틱 90, 냉각은 102 부터
        private static HeatRig Heat40(HeatRig rig)
        {
            FireShotsThenRelease(rig, 10);
            Assert.Equal(4800, rig.H.HeatHu);
            return rig;
        }

        // 결투 세계. 결투 수치는 duel_rules 데이터가 채울 자리(SimConfig.FillDuel, 아직 없음)라 spec 01 12.1 의 값
        // (스침 간격 60틱, 피격 반지름 0.30)을 시험이 넣는다(GrazePipelineTests.DuelReady 와 같다). 보상 종류(GrazeFullReward = 거짓)는 ForDuel 의 것이다
        private static HeatRig DuelRig()
        {
            HeatRig rig = HeatRig.Duel(Db);
            Assert.False(rig.World.Config.GrazeFullReward);
            rig.World.Config.GrazeCooldownTicks = 60;
            rig.World.Config.HitRadiusOverride = Milli(300);
            return rig;
        }

        // ── 단계 순서(CONTRACTS 3.2, T-50 "비기 스침은 5단계에서 인정되므로 그 보상은 같은 틱 6단계의 발사보다 먼저")

        [Fact]
        public void SkillGrazeBeforeTheShotIsUsedByThatTicksShot()
        {
            HeatRig rig = Heat40(HeatRig.Endless(Db));
            rig.RunTo(90);
            rig.World.AwardSkillGraze(P0, rig.Log);                      // 5단계의 비기 스침(벼락 질주·잔상 걸음) 자리
            rig.Step(true);                                              // 6단계 발사

            SimEvent shot = LastShot(rig);
            Assert.Equal(90U, shot.Tick);
            Assert.Equal(1, shot.C);                                     // 임계(열 = 임계 시작 + 0)
            Assert.Equal(BaseCritStartHu, shot.V);                       // 열이 오르지 않았다
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);
            Assert.False(rig.H.ExemptArmed);
            Assert.Equal(1, CountAt(rig, SimEventType.HeatExemptConsumed, 90));
            SimEvent award = rig.Log.Single(SimEventType.GrazeAwarded);
            Assert.Equal((90U, (int)GrazeKind.Skill, 0), (award.Tick, award.B, award.A));
            List<SimEvent> tick = At(rig, 90);
            Assert.True(IndexOf(tick, SimEventType.HeatExemptArmed) < IndexOf(tick, SimEventType.ShotFired));
        }

        [Fact]
        public void Stage9GrazeIsUsedFromTheNextShot()
        {
            HeatRig rig = Heat40(HeatRig.Endless(Db));
            rig.RunTo(90);
            OfferGraze(rig, 100, 7);                                     // 9단계에서 판정된다
            rig.Step(true);                                              // 같은 틱 6단계 발사는 스침 전이다

            SimEvent shot = LastShot(rig);
            Assert.Equal(90U, shot.Tick);
            Assert.Equal(0, shot.C);                                     // 40 + 4 = 44 < 85
            Assert.Equal(4800 + MagicBoltHeatHu, shot.V);
            Assert.Equal(0, rig.Log.Count(SimEventType.HeatExemptConsumed));
            List<SimEvent> tick = At(rig, 90);
            Assert.True(IndexOf(tick, SimEventType.ShotFired) < IndexOf(tick, SimEventType.GrazeAwarded));
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);                 // 9단계 뒤: 임계 시작으로 올랐다
            Assert.True(rig.H.ExemptArmed);

            rig.RunTo(99);                                               // 다음 발사부터 쓴다
            rig.Step(true);
            SimEvent next = LastShot(rig);
            Assert.Equal((99U, (long)BaseCritStartHu, 1), (next.Tick, next.V, next.C));
            Assert.Equal(1, CountAt(rig, SimEventType.HeatExemptConsumed, 99));
        }

        [Fact]
        public void SkillGrazeWhileLockedLetsThatTicksShotFire()
        {
            HeatRig rig = HeatRig.Endless(Db);
            FireUntilLockedThenRelease(rig);                             // 225 잠금, FireReadyTick 234
            rig.RunTo(234);
            rig.World.AwardSkillGraze(P0, rig.Log);
            rig.Step(true);
            SimEvent shot = LastShot(rig);
            Assert.Equal((234U, (long)BaseCritStartHu, 1), (shot.Tick, shot.V, shot.C));
            Assert.Equal((int)HeatUnlockReason.Graze, rig.Log.Single(SimEventType.HeatUnlocked).A);
            Assert.Equal(0, CountAt(rig, SimEventType.FireDryWhileLocked, 234));
        }

        // ── 같은 틱 두 시전(spec 01 8.3 "보상은 중복돼도 결과가 같다", 8.4 "겹치지 않는다")

        [Fact]
        public void TwoCastsInOneTickArmTheExemptOnce()
        {
            HeatRig rig = Heat40(HeatRig.Endless(Db));
            uint g = rig.Tick;
            OfferGraze(rig, 101, 2);
            OfferGraze(rig, 100, 1);
            rig.Step(false);
            Assert.Equal(2, CountAt(rig, SimEventType.GrazeAwarded, g));
            Assert.Equal(1, CountAt(rig, SimEventType.HeatExemptArmed, g));
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);

            rig.RunTo(90);
            rig.Step(true);
            rig.RunTo(99);
            rig.Step(true);
            List<SimEvent> shots = rig.Shots();
            Assert.Equal(BaseCritStartHu, shots[shots.Count - 2].V);     // 첫 발만 면제
            Assert.Equal(BaseCritStartHu + MagicBoltHeatHu, shots[shots.Count - 1].V);
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatExemptConsumed));
        }

        [Fact]
        public void TwoCastsInOneTickWhileLockedUnlockOnce()
        {
            HeatRig rig = HeatRig.Endless(Db);
            FireUntilLockedThenRelease(rig);
            uint g = rig.Tick;
            OfferGraze(rig, 100, 1);
            OfferGraze(rig, 101, 2);
            rig.Step(false);
            Assert.Equal(2, CountAt(rig, SimEventType.GrazeAwarded, g));
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatUnlocked));
            Assert.Equal(1, CountAt(rig, SimEventType.HeatExemptArmed, g));
            Assert.False(rig.H.Locked);
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);
        }

        // ── 결투: 줄어든 보상(spec 01 12.2, CONTRACTS 3.2 OnGrazeAwarded_Heat)

        [Fact]
        public void DuelGrazeRaisesHeatToCritStartWithoutTheExempt()
        {
            HeatRig rig = Heat40(DuelRig());
            uint g = rig.Tick;
            OfferGraze(rig, 100, 7);
            rig.Step(false);
            Assert.Equal(1, CountAt(rig, SimEventType.GrazeAwarded, g));
            Assert.Equal(BaseCritStartHu, rig.H.HeatHu);                 // 임계 시작으로 올림
            Assert.False(rig.H.ExemptArmed);                             // 다음 발사 열 면제 없음
            Assert.Equal(0, rig.Log.Count(SimEventType.HeatExemptArmed));
            Assert.Equal(BaseCritStartHu, rig.World.HeatAt(0).HeatHu);
            Assert.Equal(0, rig.World.HeatAt(1).HeatHu);                 // 상대는 그대로

            rig.RunTo(90);
            rig.Step(true);
            SimEvent shot = LastShot(rig);
            Assert.Equal((90U, (long)(BaseCritStartHu + MagicBoltHeatHu), 1), (shot.Tick, shot.V, shot.C));
            Assert.Equal(0, rig.Log.Count(SimEventType.HeatExemptConsumed));
        }

        [Fact]
        public void DuelGrazeAboveCritStartKeepsHeat()
        {
            HeatRig rig = DuelRig();
            FireShotsThenRelease(rig, 23);                               // 92
            OfferGraze(rig, 100, 7);
            rig.Step(false);
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(11040, rig.H.HeatHu);
            Assert.False(rig.H.ExemptArmed);
        }

        [Fact]
        public void DuelGrazeWhileLockedNeitherUnlocksNorChangesHeat()
        {
            HeatRig rig = DuelRig();
            FireUntilLockedThenRelease(rig);
            Assert.True(rig.H.Locked);
            int heatBefore = rig.H.HeatHu;                               // 12,480(104)
            uint unlockTick = rig.H.UnlockTick;
            OfferGraze(rig, 100, 7);
            rig.Step(false);
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.True(rig.H.Locked);                                   // 잠금 해제 없음
            Assert.Equal(heatBefore, rig.H.HeatHu);                      // 열은 풀릴 때 0 이 된다
            Assert.Equal(unlockTick, rig.H.UnlockTick);
            Assert.False(rig.H.ExemptArmed);
            Assert.Equal(0, rig.Log.Count(SimEventType.HeatUnlocked));

            rig.RunTo(unlockTick + 1U);
            SimEvent unlocked = rig.Log.Single(SimEventType.HeatUnlocked);
            Assert.Equal((unlockTick, (int)HeatUnlockReason.Timeout), (unlocked.Tick, unlocked.A));
            Assert.Equal(0, rig.H.HeatHu);
        }

        [Fact]
        public void DuelCosmeticOnlyGrazeGivesNoHeat()
        {
            // 같은 각본의 두 세계. 둘 다 틱 83 에 스치고(보상, 간격 60틱 시작), B 만 간격 안의 틱 130 에 다시 스친다
            HeatRig a = Heat40(DuelRig());
            HeatRig b = Heat40(DuelRig());
            OfferGraze(a, 100, 1);
            OfferGraze(b, 100, 1);
            a.Step(false);
            b.Step(false);
            a.RunTo(130);
            b.RunTo(130);
            Assert.True(b.H.HeatHu < BaseCritStartHu);                   // 102 부터 식었다
            OfferGraze(b, 101, 2);
            a.Step(false);
            b.Step(false);
            Assert.Equal(1, b.Log.Count(SimEventType.GrazeCosmeticOnly));
            Assert.Equal(1, b.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(a.H.HeatHu, b.H.HeatHu);                        // 연출만: 열 보상 없음

            // 간격이 지난 뒤(83 + 60 = 143)의 스침은 다시 보상이 있다
            b.RunTo(150);
            OfferGraze(b, 102, 3);
            b.Step(false);
            Assert.Equal(2, b.Log.Count(SimEventType.GrazeAwarded));
            // 9단계에서 임계 시작으로 오르고 같은 틱 11단계에 한 틱 식는다(냉각 대기는 이미 지났다, 틱당 0.75 = 90 HU, T-H12)
            Assert.Equal(BaseCritStartHu - 90, b.H.HeatHu);
        }

        // ── 이음 훅은 전부 보상인지만 SimConfig 로 가른다: 무한 웨이브 설정에서 GrazeFullReward 만 끄면 결투처럼 줄어든 보상이다
        [Fact]
        public void RewardKindFollowsTheConfigSwitchOnly()
        {
            SimConfig config = SimConfig.ForEndless(Db);
            config.GrazeFullReward = false;
            var world = SimWorld.CreateEndless(Db, config, new EndlessSetup { Seed = 1UL, StartWave = 1 });
            BlockDef bolt = Db.Block(BlockId.MagicBolt);
            world.SetHeatShot(P0, BlockId.MagicBolt, bolt.HeatHu, bolt.IntervalTicks);
            var log = new Wizard.Sim.Tests.Player.EventLog();
            world.Step(System.ReadOnlySpan<Wizard.Sim.Input.PlayerInput>.Empty, System.ReadOnlySpan<Wizard.Sim.Input.RunCommand>.Empty, log);
            world.AwardSkillGraze(P0, log);
            Assert.Equal(BaseCritStartHu, world.HeatAt(0).HeatHu);
            Assert.False(world.HeatAt(0).ExemptArmed);
            Assert.Equal(0, log.Count(SimEventType.HeatExemptArmed));
        }
    }
}
