using NUnit.Framework;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.RunModuleTests
{
    /// <summary>판 중 조립(SlotRef 규칙, 서고 블록 대기 칸 D-53)과 판 끝 요약. 03 요약서 13.5절, 00 문서 5.5절.</summary>
    public class RunFlowLoadoutTests
    {
        const int Core = 0, Storage = 100, Bench = 300, None = -1;

        /// <summary>야영. 출발 조립 = 코어 magic_bolt, 칸1 split_two(서고 블록), 칸2~4 빔. 보관함에 야생 블록을 직접 넣는다.</summary>
        static Rig AtCampWith(params string[] wildBlocks)
        {
            var r = Rig.InCombatAt(2);
            r.ToCamp();
            r.Run.Storage.Clear();
            foreach (string id in wildBlocks)
                r.Run.Storage.Add(new WildBlock { Uid = r.Run.NextWildUid++, BlockId = id, Rarity = r.Db.Block(id).Rarity, AcquiredWave = 2 });
            return r;
        }

        static void Move(Rig r, int from, int to)
        {
            r.Cmd(RunCommandKind.LoadoutMove, from, to);
            r.Step();
        }

        [Test]
        public void Contract__LibraryBlockRemoved_GoesToBench_NotStorage()
        {
            var r = AtCampWith(Ids.Block.Pierce);
            Assert.AreEqual(0, r.P.Loadout.Links[0].WildUid, "출발 조립은 서고 블록");
            int recompiles = r.Player.RecompileCalls;

            Move(r, 1, None);
            Assert.IsTrue(r.P.Loadout.Links[0].IsEmpty);
            Assert.AreEqual(1, r.Run.Storage.Count, "보관함 수는 그대로");
            Assert.AreEqual(1, r.Run.LibraryBench.Count);
            Assert.AreEqual(Ids.Block.SplitTwo, r.Run.LibraryBench[0].BlockId);
            Assert.IsTrue(r.Run.LibraryBench[0].FromLibrary);
            Assert.AreNotEqual(0, r.Run.LibraryBench[0].Uid);
            Assert.AreEqual(recompiles + 1, r.Player.RecompileCalls, "조립이 바뀌면 다시 컴파일");

            // 판 끝 요약의 야생 블록에 없다
            var sum = r.W.Mods.Run.BuildSummary(r.W);
            Assert.AreEqual(1, sum.WildBlocks.Count);
            Assert.AreEqual(Ids.Block.Pierce, sum.WildBlocks[0].BlockId);
            foreach (var wb in sum.WildBlocks) Assert.IsFalse(wb.FromLibrary);

            // 다시 끼울 수 있고 WildUid 0으로 돌아간다
            Move(r, Bench + 0, 3);
            Assert.AreEqual(Ids.Block.SplitTwo, r.P.Loadout.Links[2].BlockId);
            Assert.AreEqual(0, r.P.Loadout.Links[2].WildUid);
            Assert.AreEqual(0, r.Run.LibraryBench.Count);
            Assert.AreEqual(1, r.Run.Storage.Count);
            Assert.AreEqual(recompiles + 2, r.Player.RecompileCalls);
        }

        [Test]
        public void Contract__LibraryBench_NotCountedInStorageCap_NorOverflow()
        {
            var r = AtCampWith();
            Move(r, 1, None);
            Move(r, Core, None);
            Assert.AreEqual(2, r.Run.LibraryBench.Count);
            Assert.AreEqual(0, r.Run.Storage.Count);
            Assert.IsTrue(r.P.Loadout.Core.IsEmpty);
            // 다음 웨이브의 전리품: 보관함 10 + 2장 = 12 → 대기 칸 2개가 있어도 넘치지 않는다
            for (int i = 0; i < 10; i++)
                r.Run.Storage.Add(new WildBlock { Uid = r.Run.NextWildUid++, BlockId = Ids.Block.Frost, Rarity = Ids.Rarity.Common });
            r.Depart();
            r.ToLoot();
            r.ForceCards(Ids.Block.Fireball, Ids.Block.IceSpike);   // 보관함·대기 칸에 없는 블록(별 겹치기 없이 2칸)
            r.PickAndConfirm();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase, "버릴 블록을 고르라고 하지 않는다");
            Assert.AreEqual(12, r.Run.Storage.Count);
            Assert.AreEqual(2, r.Run.LibraryBench.Count);
            Assert.AreEqual(12, r.W.Mods.Run.BuildSummary(r.W).WildBlocks.Count);
        }

        [Test]
        public void S02_13_5__WildBlockInserted_KeepsUid_AndComesBackToStorage()
        {
            var r = AtCampWith(Ids.Block.Pierce, Ids.Block.Frost);
            int pierceUid = r.Run.Storage[0].Uid;

            Move(r, Storage + 0, 2);
            Assert.AreEqual(Ids.Block.Pierce, r.P.Loadout.Links[1].BlockId);
            Assert.AreEqual(pierceUid, r.P.Loadout.Links[1].WildUid, "야생 블록의 uid는 그대로");
            Assert.AreEqual(1, r.Run.Storage.Count);
            Assert.AreEqual(Ids.Block.Frost, r.Run.Storage[0].BlockId);

            // 10 요약서 6절: 지팡이에 낀 야생 블록도 귀환 가방 후보(요약)에 든다
            var sum = r.W.Mods.Run.BuildSummary(r.W);
            Assert.AreEqual(2, sum.WildBlocks.Count);
            var socketed = sum.WildBlocks.Find(b => b.Uid == pierceUid);
            Assert.IsNotNull(socketed);
            Assert.AreEqual(Ids.Rarity.Common, socketed.Rarity);
            Assert.AreEqual(2, socketed.AcquiredWave);

            Move(r, 2, None);
            Assert.IsTrue(r.P.Loadout.Links[1].IsEmpty);
            Assert.AreEqual(2, r.Run.Storage.Count);
            Assert.AreEqual(pierceUid, r.Run.Storage[1].Uid);
            Assert.AreEqual(0, r.Run.LibraryBench.Count, "야생 블록은 대기 칸으로 가지 않는다");
        }

        [Test]
        public void S02_13_5__InsertOverLibraryBlock_SendsItToBench_OverWildBlock_Swaps()
        {
            var r = AtCampWith(Ids.Block.Pierce, Ids.Block.Frost);
            int pierceUid = r.Run.Storage[0].Uid, frostUid = r.Run.Storage[1].Uid;

            // 칸1(서고 split_two) 위에 야생 pierce → split_two는 대기 칸으로
            Move(r, Storage + 0, 1);
            Assert.AreEqual(pierceUid, r.P.Loadout.Links[0].WildUid);
            Assert.AreEqual(1, r.Run.LibraryBench.Count);
            Assert.AreEqual(Ids.Block.SplitTwo, r.Run.LibraryBench[0].BlockId);
            Assert.AreEqual(1, r.Run.Storage.Count);

            // 칸1(야생 pierce) 위에 야생 frost → 보관함의 그 자리와 맞바꿈
            Move(r, Storage + 0, 1);
            Assert.AreEqual(frostUid, r.P.Loadout.Links[0].WildUid);
            Assert.AreEqual(1, r.Run.Storage.Count);
            Assert.AreEqual(pierceUid, r.Run.Storage[0].Uid);

            // 칸1(야생 frost) 위에 대기 칸의 split_two → frost는 보관함으로
            Move(r, Bench + 0, 1);
            Assert.AreEqual(Ids.Block.SplitTwo, r.P.Loadout.Links[0].BlockId);
            Assert.AreEqual(0, r.P.Loadout.Links[0].WildUid);
            Assert.AreEqual(0, r.Run.LibraryBench.Count);
            Assert.AreEqual(2, r.Run.Storage.Count);
        }

        [Test]
        public void S02_13_5__CoreTakesProjectilesOnly()
        {
            var r = AtCampWith(Ids.Block.Pierce, Ids.Block.Fireball);
            int recompiles = r.Player.RecompileCalls;

            Move(r, Storage + 0, Core);
            Assert.AreEqual(Ids.Block.MagicBolt, r.P.Loadout.Core.BlockId, "변형 블록은 코어에 못 들어간다");
            Assert.AreEqual(2, r.Run.Storage.Count);
            Move(r, 1, Core);
            Assert.AreEqual(Ids.Block.MagicBolt, r.P.Loadout.Core.BlockId, "칸 ↔ 코어 교환도 투사체만");
            Assert.AreEqual(Ids.Block.SplitTwo, r.P.Loadout.Links[0].BlockId);
            Assert.AreEqual(recompiles, r.Player.RecompileCalls, "거절된 명령은 다시 컴파일하지 않는다");

            Move(r, Storage + 1, Core);
            Assert.AreEqual(Ids.Block.Fireball, r.P.Loadout.Core.BlockId);
            Assert.AreNotEqual(0, r.P.Loadout.Core.WildUid);
            Assert.AreEqual(1, r.Run.LibraryBench.Count, "있던 서고 magic_bolt는 대기 칸으로");
            Assert.AreEqual(Ids.Block.MagicBolt, r.Run.LibraryBench[0].BlockId);

            // 투사체끼리는 코어 ↔ 칸 교환이 된다
            Move(r, Bench + 0, 4);
            Move(r, Core, 4);
            Assert.AreEqual(Ids.Block.MagicBolt, r.P.Loadout.Core.BlockId);
            Assert.AreEqual(Ids.Block.Fireball, r.P.Loadout.Links[3].BlockId);
        }

        [Test]
        public void S02_13_5__SlotToSlot_Swaps()
        {
            var r = AtCampWith();
            Move(r, 1, 3);
            Assert.IsTrue(r.P.Loadout.Links[0].IsEmpty);
            Assert.AreEqual(Ids.Block.SplitTwo, r.P.Loadout.Links[2].BlockId);
            int recompiles = r.Player.RecompileCalls;
            Move(r, 2, 4);
            Assert.AreEqual(recompiles, r.Player.RecompileCalls, "빈 칸끼리는 아무 일 없다");
            Move(r, 9, 1);
            Move(r, Storage + 5, 1);
            Move(r, Bench + 5, 1);
            Move(r, None, 1);
            Assert.AreEqual(recompiles, r.Player.RecompileCalls, "없는 자리는 무시");
        }

        [Test]
        public void S02_13_5__WildBlockCannotLeaveToFullStorage()
        {
            var r = AtCampWith(Ids.Block.Pierce);
            Move(r, Storage + 0, 2);
            for (int i = 0; i < 12; i++)
                r.Run.Storage.Add(new WildBlock { Uid = r.Run.NextWildUid++, BlockId = Ids.Block.Frost, Rarity = Ids.Rarity.Common });
            Move(r, 2, None);
            Assert.AreEqual(Ids.Block.Pierce, r.P.Loadout.Links[1].BlockId, "보관함 12칸이 차 있으면 빼지 못한다");
            Assert.AreEqual(12, r.Run.Storage.Count);
            // 보관함의 블록과 맞바꾸는 것은 된다
            Move(r, 2, Storage + 3);
            Assert.AreEqual(Ids.Block.Frost, r.P.Loadout.Links[1].BlockId);
            Assert.AreEqual(12, r.Run.Storage.Count);
            Assert.AreEqual(Ids.Block.Pierce, r.Run.Storage[3].BlockId);
        }

        [Test]
        public void S02_13_5__RemoveAll_WildToStorage_LibraryToBench()
        {
            var r = AtCampWith(Ids.Block.Pierce, Ids.Block.Frost);
            Move(r, Storage + 0, 2);
            Move(r, Storage + 0, 3);
            Assert.AreEqual(0, r.Run.Storage.Count);
            int recompiles = r.Player.RecompileCalls;

            r.Cmd(RunCommandKind.LoadoutRemoveAll);
            r.Step();
            Assert.IsTrue(r.P.Loadout.Core.IsEmpty);
            foreach (var link in r.P.Loadout.Links) Assert.IsTrue(link.IsEmpty);
            Assert.AreEqual(2, r.Run.Storage.Count);
            Assert.AreEqual(2, r.Run.LibraryBench.Count);
            CollectionAssert.AreEquivalent(new[] { Ids.Block.MagicBolt, Ids.Block.SplitTwo },
                new[] { r.Run.LibraryBench[0].BlockId, r.Run.LibraryBench[1].BlockId });
            Assert.AreEqual(recompiles + 1, r.Player.RecompileCalls);
            Assert.AreEqual(2, r.W.Mods.Run.BuildSummary(r.W).WildBlocks.Count);
        }

        [Test]
        public void Contract__LoadoutCommands_AlsoInStartCamp()
        {
            var r = Rig.Create(c => c.StartWave = 11);
            Move(r, 1, None);
            Assert.AreEqual(1, r.Run.LibraryBench.Count);
            Move(r, Bench + 0, 2);
            Assert.AreEqual(Ids.Block.SplitTwo, r.P.Loadout.Links[1].BlockId);
        }

        // ---- 판 끝 요약

        [Test]
        public void Contract__BuildSummary_FillsAllFields()
        {
            var r = Rig.InCombatAt(1, c => { c.WandStage = 7; c.RunId = "run-abc"; c.WandId = 9001; c.CharId = Ids.Char.C3; });
            r.Kill(2);
            r.Kill(1, elite: true);
            r.ToLoot();
            r.ForceCards(Ids.Block.Fireball, Ids.Block.IceSpike);   // 조립에 없는 블록 2장(별 겹치기 없이 보관함 2칸)
            r.PickAndConfirm();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            r.Cmd(RunCommandKind.LoadoutMove, Storage + 0, 2);
            r.Step();
            r.Depart();
            r.P.Downed = true;
            r.W.Mods.Run.OnPlayerDowned(r.W, r.P);

            var sum = r.W.Mods.Run.BuildSummary(r.W);
            Assert.AreEqual("run-abc", sum.RunId);
            Assert.AreEqual(Ids.Char.C3, sum.CharId);
            Assert.AreEqual(9001, sum.WandId);
            Assert.AreEqual(RunMode.Endless, sum.Mode);
            Assert.AreEqual(7, sum.WandStage);
            Assert.AreEqual(1, sum.StartWave);
            Assert.AreEqual(2, sum.ReachedWave, "도달 웨이브 = 마지막으로 시작한 웨이브");
            Assert.AreEqual(1, sum.ClearedDirect);
            Assert.AreEqual(1, sum.LastClearedWave);
            Assert.AreEqual(RunEndReason.HpZero, sum.EndReason);
            Assert.AreEqual(r.Score.Fixed, sum.Score);
            Assert.AreEqual(0, sum.SkippedScore);
            Assert.AreEqual(4 + 6 + 20, sum.Purse);
            Assert.AreEqual(1, sum.WaveResults.Count);
            Assert.AreEqual(30, sum.WaveResults[0].GoldEarned);
            Assert.AreEqual(1, sum.DropChances.Count);
            Assert.AreEqual(DropChanceSource.EliteKill, sum.DropChances[0].Source);
            Assert.AreEqual(2, sum.TotalKills);
            Assert.IsFalse(sum.RevengePending);
            Assert.IsFalse(sum.RevengeActive);
            Assert.IsFalse(sum.PhantomFirst);
            // 판 끝의 조립: 코어 먼저, 빈 칸 null
            Assert.AreEqual(5, sum.LoadoutIds.Count);
            Assert.AreEqual(Ids.Block.MagicBolt, sum.LoadoutIds[0]);
            Assert.AreEqual(Ids.Block.SplitTwo, sum.LoadoutIds[1]);
            Assert.AreEqual(r.P.Loadout.Links[1].BlockId, sum.LoadoutIds[2]);
            Assert.IsNotNull(sum.LoadoutIds[2]);
            Assert.IsNull(sum.LoadoutIds[3]);
            Assert.IsNull(sum.LoadoutIds[4]);
            // 야생 블록: 보관함 1 + 끼운 것 1
            Assert.AreEqual(2, sum.WildBlocks.Count);
        }

        // 중간 지점 저장: 웨이브 5의 배수를 넘긴 시점(WaveCleared 직후)에 불려도 맞는 값
        [Test]
        public void Contract__BuildSummary_RightAfterWaveCleared_ForCheckpoint()
        {
            var r = Rig.InCombatAt(5);
            for (int i = 0; i < 40; i++) r.Kill(1);
            r.ClearWave();
            var sum = r.W.Mods.Run.BuildSummary(r.W);
            Assert.AreEqual(1, sum.ClearedDirect);
            Assert.AreEqual(5, sum.LastClearedWave);
            Assert.AreEqual(5, sum.ReachedWave);
            Assert.AreEqual(5, sum.StartWave);
            Assert.AreEqual(100, sum.Purse);
            Assert.AreEqual(RunEndReason.None, sum.EndReason);
            Assert.AreEqual(1, sum.WaveResults.Count);
            Assert.AreEqual(1, sum.DropChances.Count);
            Assert.AreEqual(DropChanceSource.FifthWave, sum.DropChances[0].Source);
            // 건너뛴 점수(가짜: 10 + 20 + 30 + 40) + 웨이브 점수
            Assert.AreEqual(100, sum.SkippedScore);
            Assert.AreEqual(100 + r.Score.Fixed, sum.Score);
            // 요약은 사본이다: 뒤의 진행이 저장된 요약을 바꾸지 않는다
            r.Run.DropChances.Clear();
            Assert.AreEqual(1, sum.DropChances.Count);
        }

        [Test]
        public void Contract__BuildSummary_RevengeActiveAfterFirstEncounterInThisRun()
        {
            var r = Rig.InCombatAt(10, c => c.PhantomFirst = true, rig =>
            {
                rig.Waves.BossFor = w => DayCycle.IsBossWave(w) ? Ids.Boss.MirrorPhantom : null;
                rig.Waves.PhantomFirstFor = w => w == 10;
            });
            r.W.Boss.State = BossFightState.CollapseDone;
            r.Step();
            var sum = r.W.Mods.Run.BuildSummary(r.W);
            Assert.IsFalse(sum.RevengePending, "출발 때는 복수 중이 아니었다(Config 값 그대로)");
            Assert.IsTrue(sum.RevengeActive, "이 판에서 첫 만남을 치렀다");
            Assert.IsTrue(sum.PhantomFirst);
            Assert.IsTrue(sum.PhantomFirstDone);
            Assert.AreEqual(10, sum.LastClearedWave);
        }
    }
}
