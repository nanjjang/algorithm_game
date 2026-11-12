using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Proto;
using Wizard.Sim.Tests.Player;
using Xunit;

namespace Wizard.Sim.Tests.Proto
{
    // 고정 투사체 2종과 3갈래 전환(spec 01 13절 6번). 열과 간격은 Heat 가 정하고(SetHeatShot), 탄은 시제품이 낸다.
    // 기대값의 출처: 블록 수치는 spec 02 4.1(data/sim/blocks.json), 열 단위 120 HU 는 spec 01 2.4, 3갈래 ×2.2 는 spec 01 2.5·T-H19,
    // 3갈래 부채꼴 −15/0/+15도는 spec 02 9.3 표와 TV-F02, 시야 아래(S)는 spec 01 3.7.
    public sealed class ProtoSpellTests
    {
        private const int FacingSouthCenti = 27000;                // Dir8.S

        [Fact]
        public void FirstStepStartsWithMagicBoltAndOneTurret()
        {
            var rig = ProtoRig.Practice();
            Assert.False(ProtoReadout.IsActive(rig.World));
            rig.Step();
            Assert.True(ProtoReadout.IsActive(rig.World));
            Assert.Equal(BlockId.MagicBolt, ProtoReadout.Core(rig.World, ProtoRig.P0));
            Assert.False(ProtoReadout.SplitThree(rig.World, ProtoRig.P0));
            Assert.Equal(1, ProtoReadout.EnemyCount(rig.World));
            ProtoEnemyView t = ProtoRig.Turret(rig.World);
            Assert.Equal(new FixVec2(ProtoRig.Cells(12), ProtoRig.Cells(2)), t.Pos);
            SimEvent spawn = rig.Log.Single(SimEventType.EnemySpawn);
            Assert.Equal((int)ProtoEnemyKind.Turret, spawn.B);
            Assert.Equal(0U, spawn.Tick);
        }

        [Fact]
        public void HeldFireShootsMagicBoltsAtTheBlockInterval()
        {
            var rig = ProtoRig.Practice();
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            rig.RunTo(20);
            List<SimEvent> casts = rig.Log.Of(SimEventType.SpellCast);
            // 마력탄 간격 0.15초 = 9틱(spec 02 4.1)
            Assert.Equal(new uint[] { 0, 9, 18 }, casts.ConvertAll(e => e.Tick).ToArray());
            Assert.All(casts, e => Assert.Equal(1, e.B));
            // 발사 1회의 열: 마력탄 4 → 480 HU
            Assert.Equal(480L, rig.Log.Of(SimEventType.ShotFired)[0].V);
            List<SimEvent> spawned = rig.Log.Of(SimEventType.ProjectileSpawned);
            Assert.Equal(3, spawned.Count);
            Assert.All(spawned, e => Assert.Equal((int)BlockId.MagicBolt, e.A));
            Assert.All(spawned, e => Assert.Equal(FacingSouthCenti, e.C));
        }

        [Fact]
        public void SplitThreeFacingUpFansAtSpecAngles()
        {
            // spec 02 TV-F02 와 같은 경우: split_3, 시야 9000 → 7500, 9000, 10500(9.3 표)
            var rig = ProtoRig.Practice();
            rig.SetSlot(1, BlockId.SplitThree);
            rig.Step(PlayerRig.Press(LogicalKey.Up));
            rig.Step(PlayerRig.Release(LogicalKey.Up));
            uint fireTick = rig.Tick;
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            List<SimEvent> spawned = rig.At(SimEventType.ProjectileSpawned, fireTick);
            Assert.Equal(new[] { 7500, 9000, 10500 }, spawned.ConvertAll(e => e.C).ToArray());
        }

        [Fact]
        public void SplitThreeMakesThreeBulletsAndTimesHeatByTwoPointTwo()
        {
            var rig = ProtoRig.Practice();
            rig.SetSlot(1, BlockId.SplitThree);
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            SimEvent compiled = rig.Log.Single(SimEventType.CompileChanged);
            Assert.Equal((int)BlockId.MagicBolt, compiled.A);
            Assert.Equal(1, compiled.B);
            // 마력탄 + 3갈래: 8.8 → 1,056 HU (spec 01 T-H19 의 값)
            Assert.Equal(1056L, rig.Log.Single(SimEventType.ShotFired).V);
            List<SimEvent> spawned = rig.At(SimEventType.ProjectileSpawned, 0);
            // 시야 S(27000)에서 부채꼴 −15/0/+15도(spec 02 9.3): 25500, 27000, 28500
            Assert.Equal(new[] { 25500, FacingSouthCenti, 28500 }, spawned.ConvertAll(e => e.C).ToArray());
            Assert.True(spawned[0].Source.Value < spawned[1].Source.Value && spawned[1].Source.Value < spawned[2].Source.Value);
            Assert.Equal(3, rig.Log.Single(SimEventType.SpellCast).B);
            Assert.True(ProtoReadout.SplitThree(rig.World, ProtoRig.P0));

            // 다시 비우면 한 발, 열 480
            rig.Step(PlayerRig.Release(LogicalKey.Fire));
            rig.RunTo(40);
            rig.SetSlot(1, BlockId.None);
            rig.Log.Clear();
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            Assert.Single(rig.Log.Of(SimEventType.ProjectileSpawned));
            Assert.Equal(480, rig.World.HeatAt(0).Shot.HeatHu);
        }

        [Fact]
        public void FireballCoreUsesFireballHeatAndInterval()
        {
            var rig = ProtoRig.Practice();
            rig.SetSlot(0, BlockId.Fireball);
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            rig.RunTo(40);
            // 화염구: 열 18 → 2,160 HU, 간격 0.6초 = 36틱(spec 02 4.1)
            Assert.Equal(2160L, rig.Log.Of(SimEventType.ShotFired)[0].V);
            Assert.Equal(new uint[] { 0, 36 }, rig.Log.Of(SimEventType.SpellCast).ConvertAll(e => e.Tick).ToArray());
            Assert.All(rig.Log.Of(SimEventType.ProjectileSpawned), e => Assert.Equal((int)BlockId.Fireball, e.A));
            Assert.Equal(BlockId.Fireball, ProtoReadout.Core(rig.World, ProtoRig.P0));
        }

        [Fact]
        public void SetSlotOutsideThePrototypeShelfIsAnInvalidCommand()
        {
            var rig = ProtoRig.Practice();
            rig.Step();
            var bad = new[]
            {
                new RunCommand(rig.Tick, 1, ProtoRig.P0, RunCommandOp.SetSlot, 0, (int)BlockId.SplitThree),
                new RunCommand(rig.Tick, 2, ProtoRig.P0, RunCommandOp.SetSlot, 0, 0),
                new RunCommand(rig.Tick, 3, ProtoRig.P0, RunCommandOp.SetSlot, 1, (int)BlockId.MagicBolt),
                new RunCommand(rig.Tick, 4, ProtoRig.P0, RunCommandOp.SetSlot, 2, 0),
                new RunCommand(rig.Tick, 5, ProtoRig.P0, RunCommandOp.SetSlot, 0, 256 + (int)BlockId.MagicBolt),
            };
            foreach (RunCommand c in bad)
            {
                Assert.False(rig.World.CanApply(in c));
            }
            Assert.True(rig.World.CanApply(new RunCommand(rig.Tick, 6, ProtoRig.P0, RunCommandOp.SetSlot, 0, (int)BlockId.Fireball)));
            Assert.True(rig.World.CanApply(new RunCommand(rig.Tick, 7, ProtoRig.P0, RunCommandOp.SetSlot, 1, 0)));
            rig.World.Step(System.ReadOnlySpan<PlayerInput>.Empty, bad, rig.Log);
            Assert.Equal(bad.Length, rig.Log.Count(SimEventType.DebugCommandRejected));
            Assert.Equal(0, rig.Log.Count(SimEventType.CompileChanged));
            Assert.Equal(BlockId.MagicBolt, ProtoReadout.Core(rig.World, ProtoRig.P0));
        }

        [Fact]
        public void BulletDisappearsAfterItsRange()
        {
            var rig = ProtoRig.Practice();
            rig.Tap(LogicalKey.Right);                                      // 시야 동쪽(E). 동쪽 벽까지 12칸 > 사거리 10
            uint fireTick = rig.Tick;
            rig.Step(PlayerRig.Press(LogicalKey.Fire));
            rig.Step(PlayerRig.Release(LogicalKey.Fire));
            EntityId id = rig.Log.Single(SimEventType.ProjectileSpawned).Source;
            rig.RunTo(fireTick + 60);
            SimEvent expired = rig.Log.Of(SimEventType.ProjectileExpired).Find(e => e.Source == id);
            // 사거리 10칸 ÷ 틱당 14/60칸 = 42.86 → 43번째 이동 뒤(발사 틱의 9단계가 첫 이동)
            Assert.Equal(fireTick + 42U, expired.Tick);
            Assert.True(Fix64.FromRaw(expired.X) < ProtoRig.Cells(24));
            Assert.Equal(0, ProtoReadout.BulletCount(rig.World));
        }

        [Fact]
        public void EndlessWorldHasNoPrototypeContent()
        {
            var endless = PlayerRig.Endless();
            var c = new RunCommand(0, 1, ProtoRig.P0, RunCommandOp.SetSlot, 1, (int)BlockId.SplitThree);
            Assert.False(endless.World.CanApply(in c));
            endless.Step(PlayerRig.Press(LogicalKey.Fire));
            endless.RunTo(200);
            Assert.False(ProtoReadout.IsActive(endless.World));
            Assert.Equal(0, ProtoReadout.BulletCount(endless.World));
            Assert.Equal(0, ProtoReadout.EnemyCount(endless.World));
            Assert.Equal(0, endless.Log.Count(SimEventType.ProjectileSpawned));
            Assert.Equal(0, endless.Log.Count(SimEventType.EnemySpawn));
            Assert.Equal(0, endless.Log.Count(SimEventType.ShotFired));   // 무한 웨이브에는 아직 주문이 없다(M2)
        }

        [Fact]
        public void DuelWorldHasNoPrototypeContent()
        {
            var duel = PlayerRig.Duel();
            duel.StepSlots((PlayerRig.Press(LogicalKey.Fire), 0), (PlayerRig.Press(LogicalKey.Skill), 1));
            duel.RunTo(200);
            Assert.False(ProtoReadout.IsActive(duel.World));
            Assert.Equal(0, duel.Log.Count(SimEventType.ProjectileSpawned));
            Assert.Equal(0, duel.Log.Count(SimEventType.EnemySpawn));
            Assert.Equal(0, duel.Log.Count(SimEventType.ArtCast));
        }
    }
}
