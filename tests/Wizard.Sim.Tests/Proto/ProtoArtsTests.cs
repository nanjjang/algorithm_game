using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Proto;
using Wizard.Sim.Tests.Player;
using Xunit;

namespace Wizard.Sim.Tests.Proto
{
    // 판정기 + 임시 효과의 기술 ①②⑥(GDD 16 1단계). 기대값의 출처: 커맨드(K / 뒤, K / 앞, 앞, K)와 비용은 spec 03 12절,
    // 효과의 배율(2T, 1T, 3T)·넉백 2칸·창 폭 0.5·사거리 12는 12.2절, 재사용 대기 0.4초와 K 보관 0.1초는 1.2절·10절,
    // 입력 시각 단위(1틱 = 256)는 spec 01 2.1. 자세 중 아래(S)를 보면 아래 키 = 앞, 위 키 = 뒤(spec 01 5.5).
    public sealed class ProtoArtsTests
    {
        private static readonly Fix64 Ten = Fix64.FromInt(10);

        [Fact]
        public void KInStanceFiresManaReleaseAtDoubleDamageWithoutHeat()
        {
            var rig = ProtoRig.Practice();
            rig.Step(PlayerRig.Press(LogicalKey.Stance));
            rig.Step(PlayerRig.Press(LogicalKey.Skill));
            SimEvent cast = rig.Log.Single(SimEventType.ArtCast);
            Assert.Equal((int)ArtId.Burst, cast.A);
            Assert.Equal(1, cast.B);                                               // 결정 1
            Assert.Equal(1U, cast.Tick);
            Assert.Equal(1, rig.Log.Single(SimEventType.ArtManaReleaseFire).A);
            Assert.Equal(1, rig.World.ProtoBulletCount);
            Assert.Equal(Ten * 2, rig.World.ProtoBulletAt(0).Damage);             // 2T(마력탄 T = 10)
            Assert.Equal(0, rig.World.ProtoBulletAt(0).ShotId);
            Assert.Equal(0, rig.Log.Count(SimEventType.ShotFired));               // J 발사가 아니다: 열이 오르지 않는다
            Assert.Equal(0, rig.World.HeatAt(0).HeatHu);
        }

        [Fact]
        public void KOutsideStanceIsAlwaysManaRelease()
        {
            var rig = ProtoRig.Practice();
            rig.Step();
            rig.Step(PlayerRig.Press(LogicalKey.Skill));
            Assert.Equal((int)ArtId.Burst, rig.Log.Single(SimEventType.ArtCast).A);
            Assert.Equal(-1, rig.Log.Single(SimEventType.PracticeGapReport).A);   // 자세 밖: 간격 없음
        }

        [Fact]
        public void BackThenKFiresRecoilShotThatPushesTheSlimeItHits()
        {
            var rig = ProtoRig.Practice();
            rig.Step(PlayerRig.Press(LogicalKey.Stance));
            EntityId slime = rig.PlaceSlime(12000, 9500);
            rig.Step(PlayerRig.Press(LogicalKey.Up));                               // 뒤
            rig.Step(PlayerRig.Release(LogicalKey.Up), PlayerRig.Press(LogicalKey.Skill));
            Assert.Equal((int)ArtId.Recoil, rig.Log.Single(SimEventType.ArtCast).A);
            Assert.True(rig.World.ProtoBulletAt(0).Knockback);
            Assert.Equal(Ten, rig.World.ProtoBulletAt(0).Damage);                   // 1T
            Fix64 before = Fix64.Zero;
            while (rig.Log.Of(SimEventType.HitEnemy).Count == 0)
            {
                before = ProtoReadout.Enemy(rig.World, rig.SlimeIndex(slime)).Pos.Y;
                rig.Step();
                Assert.True(rig.Tick < 40);
            }
            Fix64 after = ProtoReadout.Enemy(rig.World, rig.SlimeIndex(slime)).Pos.Y;
            // 같은 틱: 9단계에 0.05칸 다가오고, 10단계에 탄 방향(아래)으로 2칸 밀린다
            Assert.Equal(before - Fix64.FromRatio(300, 6000) + Fix64.FromInt(2), after);
            Assert.Equal(Fix64.FromInt(10), ProtoReadout.Enemy(rig.World, rig.SlimeIndex(slime)).Hp);
        }

        [Fact]
        public void ForwardForwardKFiresPiercingLanceThroughEverySlimeInLine()
        {
            var rig = ProtoRig.Practice();
            rig.Step(PlayerRig.Press(LogicalKey.Stance));
            EntityId near = rig.PlaceSlime(12000, 10000);
            EntityId far = rig.PlaceSlime(12000, 13000);
            EntityId aside = rig.PlaceSlime(14500, 10000);
            rig.Tap(LogicalKey.Down);                                               // 앞
            rig.Step(PlayerRig.Press(LogicalKey.Down));                             // 앞
            rig.Step(PlayerRig.Release(LogicalKey.Down), PlayerRig.Press(LogicalKey.Skill, 1));
            SimEvent cast = rig.Log.Single(SimEventType.ArtCast);
            Assert.Equal((int)ArtId.Spear, cast.A);
            Assert.Equal(2, cast.B);                                               // 결정 2
            Assert.Equal(0, rig.World.ProtoBulletCount);                            // 즉발: 탄이 없다
            SimEvent lance = rig.Log.Single(SimEventType.ArtPiercingLanceFire);
            Assert.Equal(2, lance.A);
            Assert.Equal((Ten * 3).Raw, lance.V);                                   // 3T
            List<SimEvent> hits = rig.Log.Of(SimEventType.HitEnemy);
            Assert.Equal(new[] { near, far }, hits.ConvertAll(e => e.Target).ToArray());
            Assert.All(hits, e => Assert.Equal(cast.Tick, e.Tick));
            List<SimEvent> deaths = rig.Log.Of(SimEventType.EnemyDeath);           // 30 ≥ 20: 같은 틱 10단계에 둘 다
            Assert.Equal(new[] { near, far }, deaths.ConvertAll(e => e.Source).ToArray());
            Assert.True(rig.SlimeIndex(aside) >= 0);
        }

        [Fact]
        public void GapReportCarriesTheInputTimeFromTheLastDirectionToK()
        {
            var rig = ProtoRig.Practice();
            rig.Step(PlayerRig.Press(LogicalKey.Stance));
            rig.Step(PlayerRig.Press(LogicalKey.Down, 0));                         // 시각 1 × 256 = 256
            rig.Step(PlayerRig.Release(LogicalKey.Down), PlayerRig.Press(LogicalKey.Skill, 128));   // 2 × 256 + 128 = 640
            SimEvent g = rig.Log.Single(SimEventType.PracticeGapReport);
            Assert.Equal(384, g.A);                                                 // 25 ms
            Assert.Equal(384L, g.V);
            Assert.Equal(1, g.C);
            SimEvent dir = rig.Log.Single(SimEventType.CmdDirInput);
            Assert.Equal((int)CmdDir.Forward, dir.A);
        }

        [Fact]
        public void SecondKDuringCooldownIsIgnoredOrBufferedNearItsEnd()
        {
            var rig = ProtoRig.Practice();
            rig.Step();
            rig.Step(PlayerRig.Press(LogicalKey.Skill));                           // 시각 256 → 대기 끝 256 + 6,144 = 6,400 = 25틱
            rig.Step(PlayerRig.Release(LogicalKey.Skill), PlayerRig.Press(LogicalKey.Skill, 128));  // 640: 보관 창(4,864~) 밖 → 무시
            rig.RunTo(20);
            rig.Step(PlayerRig.Release(LogicalKey.Skill), PlayerRig.Press(LogicalKey.Skill, 128));  // 5,248: 보관
            rig.RunTo(30);
            Assert.Equal(new uint[] { 1, 25 }, rig.Log.Of(SimEventType.ArtCast).ConvertAll(e => e.Tick).ToArray());
            List<SimEvent> reports = rig.Log.Of(SimEventType.PracticeGapReport);
            Assert.Equal(new[] { 1, 0, 0 }, reports.ConvertAll(e => e.C).ToArray());
            SimEvent kb = rig.Log.Single(SimEventType.ArtKBuffered);
            Assert.Equal(20U, kb.Tick);
            Assert.Equal(6400, kb.A);
        }

        [Fact]
        public void SkillSwitchOffKeepsKOutOfTheParser()
        {
            SimConfig c = ProtoRig.PracticeConfig();
            c.SkillEnabled = false;
            var rig = ProtoRig.Practice(c);
            rig.Step(PlayerRig.Press(LogicalKey.Stance));
            rig.Step(PlayerRig.Press(LogicalKey.Skill));
            rig.RunTo(10);
            Assert.Equal(0, rig.Log.Count(SimEventType.ArtCast));
            Assert.Equal(0, rig.Log.Count(SimEventType.PracticeGapReport));
            Assert.True(ProtoReadout.IsActive(rig.World));                         // 나머지 시제품은 돈다
        }

        [Fact]
        public void FireSwitchOffStopsShotsButNotArts()
        {
            SimConfig c = ProtoRig.PracticeConfig();
            c.FireEnabled = false;
            var rig = ProtoRig.Practice(c);
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            rig.Step(PlayerRig.Press(LogicalKey.Skill));
            rig.RunTo(10);
            Assert.Equal(0, rig.Log.Count(SimEventType.SpellCast));
            Assert.Equal(1, rig.Log.Count(SimEventType.ArtCast));
        }
    }
}
