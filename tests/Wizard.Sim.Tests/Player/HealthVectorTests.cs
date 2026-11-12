using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Player;
using Wizard.Sim.Tests.World;
using Xunit;
using static Wizard.Sim.Tests.Player.PlayerRig;

namespace Wizard.Sim.Tests.Player
{
    // spec 01 14.10 체력과 무적(T-P01~T-P06). 규칙은 spec 01 6.1~6.2. 피격 후보는 9단계(충돌)가 QueuePlayerHit 로 넣고
    // 10단계가 확정한다. 적 모듈이 아직 없어 시험은 Step 직전에 후보를 넣는다(같은 Step 의 10단계가 확정한다).
    public sealed class HealthVectorTests
    {
        private static void HitAt(PlayerRig rig, uint tick, int attackId, int cells)
        {
            rig.RunTo(tick);
            rig.World.QueuePlayerHit(new PlayerSlot(0), new EntityId(attackId), cells);
            rig.Step();
        }

        [Fact]
        public void S01_T_P01__TouchAt159AfterHitAt100IsIgnored()
        {
            PlayerRig rig = Endless();
            HitAt(rig, 100, 1, 1);
            Assert.Equal(500, rig.P.HpPoints);
            Assert.Equal(160U, rig.P.InvulnUntil);                               // 60틱 무적(기준값 1초)
            rig.Log.Clear();
            HitAt(rig, 159, 2, 1);
            Assert.Equal(500, rig.P.HpPoints);
            Assert.Equal(0, rig.Log.Count(SimEventType.PlayerHit));
            Assert.Equal(0, rig.Log.Count(SimEventType.InvulnEnded));
            Assert.False(rig.P.Hit.Active);                                       // 막힌 후보도 그 틱에 비운다
            Assert.False(rig.World.PlayerWasHitThisTick(0));
        }

        [Fact]
        public void S01_T_P02__TouchAt160AfterHitAt100Hits()
        {
            PlayerRig rig = Endless();
            HitAt(rig, 100, 1, 1);
            rig.Log.Clear();
            rig.RunTo(160);
            rig.World.QueuePlayerHit(new PlayerSlot(0), new EntityId(2), 1);
            rig.Step();
            Assert.Equal(400, rig.P.HpPoints);
            Assert.Equal(220U, rig.P.InvulnUntil);
            SimEvent ended = rig.Log.Single(SimEventType.InvulnEnded);
            SimEvent hit = rig.Log.Single(SimEventType.PlayerHit);
            Assert.Equal(160U, ended.Tick);
            Assert.Equal(160U, hit.Tick);
            Assert.True(rig.Log.Events.IndexOf(ended) < rig.Log.Events.IndexOf(hit));   // 무적이 끝난 뒤에 맞는다
            Assert.Equal(100, hit.A);
            Assert.Equal(400, hit.B);
            Assert.Equal(2, hit.Source.Value);
        }

        [Fact]
        public void S01_T_P03__LastCellHitDownsPlayerOnce()
        {
            PlayerRig rig = Endless();
            HitAt(rig, 10, 1, 3);                                                 // 600 → 300
            HitAt(rig, 70, 2, 2);                                                 // 300 → 100 (체력 1칸)
            Assert.Equal(100, rig.P.HpPoints);
            rig.Log.Clear();
            HitAt(rig, 130, 3, 1);
            Assert.Equal(0, rig.P.HpPoints);
            SimEvent hit = rig.Log.Single(SimEventType.PlayerHit);
            Assert.Equal(0, hit.B);
            rig.Log.Single(SimEventType.PlayerDowned);
            Assert.True(rig.Log.Events.IndexOf(hit) < rig.Log.Events.IndexOf(rig.Log.Single(SimEventType.PlayerDowned)));

            // 쓰러진 뒤의 접촉은 다시 쓰러짐을 내지 않는다(한 번씩만)
            rig.Log.Clear();
            HitAt(rig, 300, 4, 1);
            Assert.Equal(0, rig.Log.Count(SimEventType.PlayerDowned));
            Assert.Equal(0, rig.Log.Count(SimEventType.PlayerHit));
        }

        [Fact]
        public void S01_T_P04__MaxHpPointsPerCharacter()
        {
            // spec 01 5.1 표의 MaxHpCells(달구 8, 초롱 4, 갈피 6, 성에 7, 그믐 5). 다섯 캐릭터의 데이터 줄은 아직 팩에 없다(M5-DAT-01)
            Assert.Equal(800, PlayerHealth.MaxHpPoints(new CharacterDef { Id = CharacterId.Char1, MaxHpCells = 8 }));
            Assert.Equal(400, PlayerHealth.MaxHpPoints(new CharacterDef { Id = CharacterId.Char2, MaxHpCells = 4 }));
            Assert.Equal(600, PlayerHealth.MaxHpPoints(new CharacterDef { Id = CharacterId.Char3, MaxHpCells = 6 }));
            Assert.Equal(700, PlayerHealth.MaxHpPoints(new CharacterDef { Id = CharacterId.Char4, MaxHpCells = 7 }));
            Assert.Equal(500, PlayerHealth.MaxHpPoints(new CharacterDef { Id = CharacterId.Char5, MaxHpCells = 5 }));

            // 팩의 기준값 캐릭터(6칸)로 시작한 세계는 체력 600 가득(spec 01 3.7)
            Assert.Equal(6, WorldData.Baseline.MaxHpCells);
            PlayerRig rig = Endless();
            rig.Step();
            Assert.Equal(600, rig.P.MaxHpPoints);
            Assert.Equal(600, rig.P.HpPoints);
        }

        [Fact]
        public void S01_T_P05__SameTickOnlyLowestAttackIdLands()
        {
            foreach (bool lowFirst in new[] { false, true })
            {
                PlayerRig rig = Endless();
                rig.RunTo(50);
                if (lowFirst)
                {
                    rig.World.QueuePlayerHit(new PlayerSlot(0), new EntityId(4), 2);
                    rig.World.QueuePlayerHit(new PlayerSlot(0), new EntityId(7), 1);
                }
                else
                {
                    rig.World.QueuePlayerHit(new PlayerSlot(0), new EntityId(7), 1);
                    rig.World.QueuePlayerHit(new PlayerSlot(0), new EntityId(4), 2);
                }
                rig.Step();
                Assert.Equal(400, rig.P.HpPoints);                                // id 4 의 2칸만
                SimEvent hit = rig.Log.Single(SimEventType.PlayerHit);
                Assert.Equal(4, hit.Source.Value);
                Assert.Equal(200, hit.A);
            }
        }

        [Fact]
        public void S01_T_P06__ThreeCellAttackTakesThreeCells()
        {
            PlayerRig rig = Endless();
            HitAt(rig, 20, 9, 3);
            Assert.Equal(300, rig.P.HpPoints);
            Assert.Equal(300, rig.Log.Single(SimEventType.PlayerHit).A);
        }

        // ── 명세 규칙의 나머지(6.2): 피격 확정의 표시와 같은 단계 뒤 모듈이 읽는 값

        [Fact]
        public void HitIsVisibleToLaterStage10ModulesOnlyOnThatTick()
        {
            PlayerRig rig = Endless();
            HitAt(rig, 40, 1, 1);
            Assert.True(rig.P.HasBeenHit);
            Assert.Equal(40U, rig.P.LastHitTick);
            Assert.True(rig.World.PlayerInvulnerable(0));
            rig.Step();
            Assert.False(rig.World.PlayerWasHitThisTick(0));
        }

        [Fact]
        public void InvulnEndedFiresOnceAtInvulnUntil()
        {
            PlayerRig rig = Endless();
            HitAt(rig, 5, 1, 1);
            rig.RunTo(200);
            SimEvent ended = rig.Log.Single(SimEventType.InvulnEnded);
            Assert.Equal(65U, ended.Tick);
            Assert.False(rig.World.PlayerInvulnerable(0));
        }

        [Fact]
        public void HitDamageRules()
        {
            Assert.Equal(100, PlayerHealth.DamagePoints(1, false));
            Assert.Equal(300, PlayerHealth.DamagePoints(3, false));               // 3칸 상한은 적 쪽 값이라 그대로
            Assert.Equal(400, PlayerHealth.DamagePoints(3, true));                // 그믐 빚 +1칸은 상한 뒤(최대 4칸)
            Assert.Equal(0, PlayerHealth.HpAfter(100, 300));                      // 0 아래로 내려가지 않는다
            Assert.Equal(200, PlayerHealth.HpAfter(500, 300));
        }

        [Fact]
        public void QueuePlayerHitRejectsBrokenRequests()
        {
            PlayerRig rig = Endless();
            Assert.Throws<SimInvariantException>(() => rig.World.QueuePlayerHit(new PlayerSlot(1), new EntityId(1), 1));
            Assert.Throws<SimInvariantException>(() => rig.World.QueuePlayerHit(new PlayerSlot(0), EntityId.None, 1));
            Assert.Throws<SimInvariantException>(() => rig.World.QueuePlayerHit(new PlayerSlot(0), new EntityId(1), 0));
        }
    }
}
