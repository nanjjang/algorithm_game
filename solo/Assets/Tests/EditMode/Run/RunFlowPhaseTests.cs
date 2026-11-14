using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.RunModuleTests
{
    /// <summary>판 상태 기계: 06 요약서 4.1절 전이표, 00 문서 5.5절(보스 시계 표, WaveClearing 길이, 첫 만남), 10 요약서 6절.</summary>
    public class RunFlowPhaseTests
    {
        // ---- 06 4.1절 전이표

        [Test]
        public void S07_4_1__Start_Skip0_GoesToWavePrelude1_ThenCombat()
        {
            var r = Rig.Create();
            Assert.AreEqual(RunPhase.WavePrelude, r.Run.Phase);
            Assert.AreEqual(1, r.Run.Wave);
            Assert.AreEqual(0, r.Run.Purse);
            Assert.IsTrue(r.P.Active);
            Assert.AreEqual(1, r.Count(SimEventType.RunStarted));
            r.Step();
            // 좁은 들 예고가 없으면 0틱
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
            Assert.AreEqual(1, r.Run.ReachedWave);
            Assert.AreEqual(1, r.Waves.BeginCalls);
            Assert.AreEqual(1, r.Traits.WaveStartCalls);
            Assert.AreEqual(1, r.Arts.ResetCalls);
        }

        [Test]
        public void S07_4_1__Start_Skip10_GoesToStartCamp_FirstWave11_WithSkipScore()
        {
            var r = Rig.Create(c => c.StartWave = 11);
            Assert.AreEqual(RunPhase.StartCamp, r.Run.Phase);
            Assert.AreEqual(11, r.Run.Wave);
            // 가짜 SkipScore = 웨이브 × 10 → 1..10의 합 550
            Assert.AreEqual(550, r.Run.SkippedScore);
            Assert.AreEqual(550, r.Run.Score);
            Assert.AreEqual(0, r.Run.Purse);
            Assert.AreEqual(0, r.Run.ClearedDirect);
            Assert.IsNotNull(r.Run.NextPlan);
            Assert.AreEqual(11, r.Run.NextPlan.Wave);
            Assert.AreEqual(1, r.Count(SimEventType.CampEntered));
            r.Cmd(RunCommandKind.CampDepart);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
            Assert.AreEqual(11, r.Run.ReachedWave);
            Assert.AreEqual(11, r.W.Wave.Wave);
        }

        [Test]
        public void S07_4_1__WavePrelude_NarrowFieldNotice180Ticks_ThenCombat()
        {
            var r = Rig.Create(null, rig => rig.Waves.PreludeTicks = 180);
            Assert.AreEqual(RunPhase.WavePrelude, r.Run.Phase);
            r.Step(180);
            Assert.AreEqual(RunPhase.WavePrelude, r.Run.Phase, "틱 0~179는 예고");
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
        }

        [Test]
        public void S07_4_1__WaveCombat_AllDead_Clearing60Ticks_LootPick_Camp_NextWave()
        {
            var r = Rig.Create();
            r.StepUntil(RunPhase.WaveCombat);
            r.ClearWave();
            Assert.AreEqual(60, r.Run.ClearingTicks);
            Assert.IsFalse(r.W.PlayerDamageActive, "WaveClearing부터 피해 판정 없음");
            r.Step(59);
            Assert.AreEqual(RunPhase.WaveClearing, r.Run.Phase);
            r.Step();
            Assert.AreEqual(RunPhase.LootPick, r.Run.Phase);
            Assert.AreEqual(1, r.Count(SimEventType.LootOffered));
            r.PickAndConfirm();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub);
            Assert.AreEqual(2, r.Run.Wave, "야영 동안은 다음에 싸울 웨이브");
            Assert.AreEqual(1, r.Run.LastClearedWave);
            r.Depart();
            Assert.AreEqual(2, r.W.Wave.Wave);
            Assert.AreEqual(2, r.Run.ReachedWave);
        }

        [Test]
        public void S07_4_1__WaveCombat_HpZero_RunEndedHpZero()
        {
            var r = Rig.InCombatAt(1);
            r.P.Downed = true;
            r.W.Mods.Run.OnPlayerDowned(r.W, r.P);
            Assert.AreEqual(RunPhase.RunEnded, r.Run.Phase);
            Assert.AreEqual(RunEndReason.HpZero, r.Run.EndReason);
        }

        [Test]
        public void S07_4_1__AnyPhase_GiveUp_RunEndedAbandon()
        {
            var r = Rig.InCombatAt(1);
            r.Cmd(RunCommandKind.GiveUp);
            r.Step();
            Assert.AreEqual(RunPhase.RunEnded, r.Run.Phase);
            Assert.AreEqual(RunEndReason.Abandon, r.Run.EndReason);
            Assert.IsTrue(r.Run.GaveUp);

            var camp = Rig.Create(c => c.StartWave = 11);
            camp.Cmd(RunCommandKind.GiveUp);
            camp.Step();
            Assert.AreEqual(RunPhase.RunEnded, camp.Run.Phase);
            Assert.AreEqual(RunEndReason.Abandon, camp.Run.EndReason);
        }

        [Test]
        public void S07_4_2__ClearProcessing_CallsCleanupOnce_AndCountsDirectClear()
        {
            var r = Rig.InCombatAt(1);
            r.ClearWave();
            Assert.AreEqual(1, r.Run.ClearedDirect);
            Assert.AreEqual(1, r.Run.LastClearedWave);
            Assert.AreEqual(1, r.Player.ResetCalls);
            Assert.AreEqual(1, r.Threats.ClearAllCalls);
            Assert.AreEqual(1, r.Arts.ClearZoneCalls);
            Assert.AreEqual(1, r.Spells.ClearCalls);
            Assert.AreEqual(1, r.Waves.EndCalls);
            Assert.AreEqual(1, r.Count(SimEventType.WaveCleared));
            Assert.AreEqual(1, r.Run.WaveResults.Count);
            Assert.AreEqual(1, r.Run.WaveResults[0].Wave);
            Assert.AreEqual(r.Score.Fixed, r.Run.Score);
            Assert.AreEqual(0, r.Bosses.DismissCalls, "일반 웨이브는 보스를 걷지 않는다");
        }

        // ---- 계약: 보스 등장은 Bosses의 상태만 따라간다

        [Test]
        public void Contract__BossIntro_FollowsBossStateOnly_Not72Ticks()
        {
            var r = Rig.Create(c => c.StartWave = 10);
            r.Cmd(RunCommandKind.CampDepart);
            r.Step();
            // 틱 0: 출발 → 예고 0틱 → 보스 등장
            Assert.AreEqual(RunPhase.BossIntro, r.Run.Phase);
            Assert.AreEqual(1, r.Bosses.SpawnCalls);
            Assert.IsTrue(r.P.AttackLocked);
            Assert.IsFalse(r.W.AttackAllowed(r.P));
            r.Step(99);
            // 틱 1~99: 가짜 Bosses가 아직 Intro다. 72틱이 지났어도 그대로다.
            Assert.AreEqual(RunPhase.BossIntro, r.Run.Phase);
            Assert.IsTrue(r.P.AttackLocked);
            r.Step();
            // 틱 100: 가짜가 Waiting을 줬다 → 같은 틱 11단계에 전투
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
            Assert.IsFalse(r.P.AttackLocked);
            Assert.AreEqual(1, r.Bosses.SpawnCalls, "등장은 한 번");
        }

        [Test]
        public void Contract__BossIntro_EndsEarlyWhenBossSaysSo()
        {
            var r = Rig.Create(c => c.StartWave = 10, rig => rig.Bosses.IntroTicks = 5);
            r.Cmd(RunCommandKind.CampDepart);
            r.Step(5);
            Assert.AreEqual(RunPhase.BossIntro, r.Run.Phase);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
        }

        [Test]
        public void Contract__BossClear_DismissOnce_BossNull_ClearingIsBossLength()
        {
            var r = Rig.InCombatAt(10);
            Assert.IsNotNull(r.W.Boss);
            r.ClearWave();
            Assert.AreEqual(1, r.Bosses.DismissCalls);
            Assert.IsNull(r.W.Boss);
            // D-58: 보스 처치 2.0초 = 120틱
            Assert.AreEqual(120, r.Run.ClearingTicks);
            Assert.AreEqual(-1, r.Run.RevengeKillTick);
            r.Step(119);
            Assert.AreEqual(RunPhase.WaveClearing, r.Run.Phase);
            r.Step();
            Assert.AreEqual(RunPhase.LootPick, r.Run.Phase);
            Assert.AreEqual(1, r.Bosses.DismissCalls);
            Assert.IsTrue(r.Run.WaveResults[0].Stats.BossKilled);
        }

        // ---- 복수 처치(D-58, 05 요약서 10.3절)

        static Rig RevengeFight(bool revengePending)
        {
            return Rig.InCombatAt(20, c => c.RevengePending = revengePending,
                rig => rig.Waves.BossFor = w => w == 20 ? Ids.Boss.MirrorPhantom : (DayCycle.IsBossWave(w) ? Ids.Boss.KilnBoar : null));
        }

        [Test]
        public void Contract__RevengeKill_Clearing264Ticks_AndKillTickRecorded()
        {
            var r = RevengeFight(true);
            Assert.IsTrue(r.Run.RevengeActive);
            Assert.IsTrue(r.Run.PhantomAttempted);
            int killTick = r.W.Tick;
            r.ClearWave();
            Assert.AreEqual(264, r.Run.ClearingTicks);
            Assert.AreEqual(killTick, r.Run.RevengeKillTick);
            Assert.IsTrue(r.Run.KilledPhantomAtWave20);
            r.Step(263);
            Assert.AreEqual(RunPhase.WaveClearing, r.Run.Phase);
            r.Step();
            Assert.AreEqual(RunPhase.LootPick, r.Run.Phase);
        }

        [Test]
        public void Contract__RevengeKill_JAtTick83Ignored_JAtTick84Skips()
        {
            var r = RevengeFight(true);
            int killTick = r.W.Tick;
            r.ClearWave();
            r.Step(82);
            Assert.AreEqual(killTick + 83, r.W.Tick);
            r.StepPressJ();
            Assert.AreEqual(RunPhase.WaveClearing, r.Run.Phase, "83틱의 J는 무시");
            Assert.AreEqual(killTick + 84, r.W.Tick);
            r.StepPressJ();
            Assert.AreEqual(RunPhase.LootPick, r.Run.Phase, "84틱 뒤의 J로 넘긴다");
        }

        [Test]
        public void Contract__Wave20PhantomWithoutRevenge_UsesBossLength_NoSkip()
        {
            var r = RevengeFight(false);
            r.ClearWave();
            Assert.AreEqual(120, r.Run.ClearingTicks);
            Assert.AreEqual(-1, r.Run.RevengeKillTick);
            Assert.IsTrue(r.Run.KilledPhantomAtWave20);
            r.Step(90);
            r.StepPressJ();
            Assert.AreEqual(RunPhase.WaveClearing, r.Run.Phase, "복수 처치가 아니면 J로 넘기지 못한다");
        }

        [Test]
        public void Contract__NormalBossClearing_JDoesNotSkip()
        {
            var r = Rig.InCombatAt(10);
            r.ClearWave();
            r.Step(90);
            r.StepPressJ();
            Assert.AreEqual(RunPhase.WaveClearing, r.Run.Phase);
        }

        // 05 요약서 10.4절 T280: hpMax 7111, hpMin 2702 → 62%
        [Test]
        public void S06_T280__PhantomDentPercent_FloorOfDamageShare()
        {
            var r = RevengeFight(true);
            r.W.Boss.HpMin = 2702f;
            r.Step();
            Assert.AreEqual(62, r.Run.PhantomDentPercent);
            // 쓰러져 판이 끝나도 남는다
            r.W.Mods.Run.OnPlayerDowned(r.W, r.P);
            Assert.AreEqual(RunPhase.RunEnded, r.Run.Phase);
            var sum = r.W.Mods.Run.BuildSummary(r.W);
            Assert.IsTrue(sum.PhantomAttempted);
            Assert.AreEqual(62, sum.PhantomDentPercent);
            Assert.IsFalse(sum.KilledPhantomAtWave20);
        }

        [Test]
        public void S06_10_4__Wave10KilnBoar_IsNotAPhantomAttempt()
        {
            var r = Rig.InCombatAt(10);
            Assert.IsFalse(r.Run.PhantomAttempted);
            r.W.Boss.HpMin = 10f;
            r.Step();
            Assert.AreEqual(0, r.Run.PhantomDentPercent);
        }

        // ---- 첫 만남(01 요약서 5.2절, 00 문서 5.5절 표)

        static Rig FirstEncounter()
        {
            return Rig.InCombatAt(10, c => c.PhantomFirst = true, rig =>
            {
                rig.Waves.BossFor = w => DayCycle.IsBossWave(w) ? Ids.Boss.MirrorPhantom : null;
                rig.Waves.PhantomFirstFor = w => w == 10;
                rig.Bosses.HpMax = 4000f;
            });
        }

        [Test]
        public void S09_5_2__FirstEncounter_Reflection_Collapse_CollapseDone_FollowsBossStateOnly()
        {
            var r = FirstEncounter();
            Assert.IsTrue(r.W.Boss.FirstEncounter);
            Assert.IsFalse(r.Run.PhantomAttempted, "첫 만남은 도전 횟수에 세지 않는다");
            r.Step(300);
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase, "Run은 2,700틱·180틱·240틱을 세지 않는다");

            r.W.Boss.State = BossFightState.Reflection;
            r.Step();
            Assert.AreEqual(RunPhase.PhantomFinale, r.Run.Phase);
            r.Step(500);
            Assert.AreEqual(RunPhase.PhantomFinale, r.Run.Phase, "180틱이 지나도 Bosses가 넘기기 전에는 그대로");

            r.W.Boss.State = BossFightState.Collapse;
            r.Step();
            Assert.AreEqual(RunPhase.PhantomDowned, r.Run.Phase);
            r.Step(500);
            Assert.AreEqual(RunPhase.PhantomDowned, r.Run.Phase, "240틱이 지나도 그대로");
            Assert.AreEqual(0, r.Player.RiseCalls);
        }

        [Test]
        public void S09_5_2__FirstEncounter_CollapseDone_Rise_Dismiss_RevengeActive_Gold20_LowStoneChance()
        {
            var r = FirstEncounter();
            r.P.Hp = 0;
            r.P.Downed = true;
            r.P.Crystals = 2;
            r.W.Boss.State = BossFightState.Collapse;
            r.Step();
            Assert.AreEqual(RunPhase.PhantomDowned, r.Run.Phase);
            Assert.IsFalse(r.Run.RevengeActive);

            r.W.Boss.State = BossFightState.CollapseDone;
            r.Step();
            Assert.AreEqual(RunPhase.WaveClearing, r.Run.Phase);
            Assert.AreEqual(1, r.Player.RiseCalls);
            Assert.AreEqual(1, r.Bosses.DismissCalls);
            Assert.IsNull(r.W.Boss);
            Assert.IsTrue(r.Run.PhantomFirstDone);
            Assert.IsTrue(r.Run.RevengeActive);
            // 일어남: 체력 가득
            Assert.AreEqual(r.P.MaxHp, r.P.Hp);
            Assert.IsFalse(r.P.Downed);
            // 웨이브 10을 직접 넘긴 것으로 센다
            Assert.AreEqual(1, r.Run.ClearedDirect);
            Assert.AreEqual(10, r.Run.LastClearedWave);
            // 골드: 클리어 20만(보스 처치 380 없음) + 결정 환전 2 × 5
            Assert.AreEqual(20 + 10, r.Run.Purse);
            // 점수 고정 3,648
            Assert.AreEqual(3648, r.Run.WaveResults[0].Score.Total);
            // 재료: 하급 1 확정 기회 하나뿐(보스 처치 기회 없음)
            Assert.AreEqual(1, r.Run.DropChances.Count);
            Assert.AreEqual(DropChanceSource.PhantomFirst, r.Run.DropChances[0].Source);
            Assert.AreEqual(10, r.Run.DropChances[0].Wave);
            Assert.IsFalse(r.Run.KilledPhantomAtWave20);
            // 보통 길이의 WaveClearing 뒤 카드 4장, 확정 칸 없음
            Assert.AreEqual(60, r.Run.ClearingTicks);
            r.StepUntil(RunPhase.LootPick);
            Assert.AreEqual(4, r.Run.Loot.Cards.Count);
            Assert.AreEqual(2, r.Run.Loot.PickCount);
            foreach (var card in r.Run.Loot.Cards) Assert.IsFalse(card.Guaranteed);
            r.PickAndConfirm();
            // 새벽 야영 → 웨이브 11
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.IsTrue(r.Run.DawnCamp);
            Assert.AreEqual(11, r.Run.Wave);
        }

        [Test]
        public void Contract__FirstEncounter_PlayerDowned_BeginsCollapse_NotRunEnded()
        {
            var r = FirstEncounter();
            r.P.Hp = 0;
            r.P.Downed = true;
            r.W.Mods.Run.OnPlayerDowned(r.W, r.P);
            Assert.AreEqual(1, r.Bosses.CollapseCalls);
            Assert.AreNotEqual(RunPhase.RunEnded, r.Run.Phase);
            Assert.AreEqual(0, r.Count(SimEventType.RunEnded));
            r.Step();
            Assert.AreEqual(RunPhase.PhantomDowned, r.Run.Phase);
        }

        [Test]
        public void S07_4_1__GiveUp_IgnoredDuringReflectionAndCollapse()
        {
            var r = FirstEncounter();
            r.W.Boss.State = BossFightState.Reflection;
            r.P.InputLocked = true;
            r.Step();
            r.Cmd(RunCommandKind.GiveUp);
            r.Step();
            Assert.AreEqual(RunPhase.PhantomFinale, r.Run.Phase);
            r.W.Boss.State = BossFightState.Collapse;
            r.Step();
            r.Cmd(RunCommandKind.GiveUp);
            r.Step();
            Assert.AreEqual(RunPhase.PhantomDowned, r.Run.Phase);
            Assert.IsFalse(r.Run.GaveUp);
        }

        // 10 요약서 6절: 첫 만남 도중 판 포기 → w10 미클리어, "만난 적 없음" 유지
        [Test]
        public void S07_16__GiveUpDuringFirstEncounterFight_Wave10NotCleared()
        {
            var r = FirstEncounter();
            r.Cmd(RunCommandKind.GiveUp);
            r.Step();
            Assert.AreEqual(RunPhase.RunEnded, r.Run.Phase);
            var sum = r.W.Mods.Run.BuildSummary(r.W);
            Assert.IsFalse(sum.PhantomFirstDone);
            Assert.IsFalse(sum.RevengeActive);
            Assert.AreEqual(0, sum.ClearedDirect);
            Assert.AreEqual(0, sum.LastClearedWave);
            Assert.IsTrue(sum.PhantomFirst);
        }

        // ---- 판 종료(공통 규칙 8, D-45)

        [Test]
        public void Contract__OnPlayerDowned_EndsRunOnTheSpot_WithEvents()
        {
            var r = Rig.InCombatAt(3);
            int tick = r.W.Tick;
            r.P.Downed = true;
            r.W.Mods.Run.OnPlayerDowned(r.W, r.P);
            Assert.AreEqual(RunPhase.RunEnded, r.Run.Phase, "11단계를 기다리지 않는다");
            Assert.AreEqual(RunEndReason.HpZero, r.Run.EndReason);
            Assert.AreEqual(tick, r.Run.PhaseStartTick);
            SimEvent ev;
            Assert.IsTrue(r.TryLast(SimEventType.RunEnded, out ev));
            Assert.AreEqual((int)RunEndReason.HpZero, ev.A);
            Assert.IsTrue(r.TryLast(SimEventType.PhaseChanged, out ev));
            Assert.AreEqual((int)RunPhase.RunEnded, ev.A);
            Assert.AreEqual((int)RunPhase.WaveCombat, ev.B);
            // 두 번 불려도 한 번만
            r.W.Mods.Run.OnPlayerDowned(r.W, r.P);
            Assert.AreEqual(1, r.Count(SimEventType.RunEnded));
        }

        [Test]
        public void Contract__AfterRunEnded_OnEnemyKilledChangesNothing()
        {
            var r = Rig.InCombatAt(3);
            r.Kill(2);
            int purse = r.Run.Purse, kills = r.Run.TotalKills, score2 = r.W.Wave.Stats.KillScoreX2, chances = r.Run.DropChances.Count;
            r.P.Downed = true;
            r.W.Mods.Run.OnPlayerDowned(r.W, r.P);
            int events = r.W.Events.Count;
            r.Kill(5, elite: true);
            r.Kill(1);
            r.W.Mods.Run.OnEnemyKilled(r.W, r.MakeEnemy(0, boss: true), default(HitInfo));
            r.W.Mods.Run.AddPurse(r.W, 99, Vector2.zero);
            Assert.AreEqual(purse, r.Run.Purse);
            Assert.AreEqual(kills, r.Run.TotalKills);
            Assert.AreEqual(score2, r.W.Wave.Stats.KillScoreX2);
            Assert.AreEqual(chances, r.Run.DropChances.Count);
            Assert.AreEqual(0, r.W.Wave.Stats.ElitesKilled);
            Assert.IsFalse(r.W.Wave.Stats.BossKilled);
            Assert.AreEqual(0, r.W.Drops.FindAll(d => d.Kind == DropKind.MaterialBundle).Count);
            Assert.AreEqual(events, r.W.Events.Count, "이벤트도 내지 않는다");
        }

        // 10 요약서 6절(D-45): 마지막 적과 플레이어가 같은 틱에 죽음 → 플레이어 먼저 → 판 끝, 그 웨이브 미클리어
        [Test]
        public void S07_16__SameTickDeath_PlayerFirst_WaveNotCleared()
        {
            var r = Rig.InCombatAt(4);
            r.Player.DownAtStage10 = true;
            r.Enemies.DieAtStage10.Add(r.MakeEnemy(3));
            r.Enemies.AfterDeaths = () => r.Waves.Cleared = true;
            r.Step();
            Assert.AreEqual(RunPhase.RunEnded, r.Run.Phase);
            Assert.AreEqual(RunEndReason.HpZero, r.Run.EndReason);
            Assert.AreEqual(0, r.Run.ClearedDirect);
            Assert.AreEqual(0, r.Run.Purse);
            Assert.AreEqual(0, r.Run.WaveResults.Count);
            Assert.AreEqual(0, r.Count(SimEventType.WaveCleared));
        }

        // 10 요약서 6절: 보스 처치 직후(WaveClearing 중)에는 피해 판정이 없고, 넘긴 것은 이미 확정이다
        [Test]
        public void S07_16__AfterBossKill_ClearIsFinal_NoDamageDuringClearing()
        {
            var r = Rig.InCombatAt(10);
            r.ClearWave();
            Assert.IsFalse(r.W.PlayerDamageActive);
            Assert.AreEqual(1, r.Run.ClearedDirect);
            Assert.AreEqual(10, r.Run.LastClearedWave);
            Assert.AreEqual(400, r.Run.Purse);
            Assert.AreEqual(1, r.Run.WaveResults.Count);
        }

        [Test]
        public void Contract__ElapsedTicksWrittenBeforeScore()
        {
            var r = Rig.InCombatAt(1);
            r.W.Wave.WaveTick = 777;
            r.Waves.Cleared = true;
            // 가짜 Waves가 이 틱의 11단계에 시계를 한 번 더 올린 뒤 Run이 읽는다
            r.Step();
            Assert.AreEqual(1, r.Score.Calls);
            Assert.AreEqual(778, r.Score.ElapsedSeen, "Score.WaveScore가 불릴 때 ElapsedTicks가 이미 적혀 있다");
            Assert.AreEqual(778, r.Run.WaveResults[0].Stats.ElapsedTicks);
        }

        // 06 요약서 4.3절 TV-158: 야영 중에는 점수용 wave_tick이 흐르지 않는다
        [Test]
        public void S07_TV_158__WaveTickStopsOutsideCombat()
        {
            var r = Rig.InCombatAt(1);
            r.Step(10);
            r.ClearWave();
            int atClear = r.W.Wave.WaveTick;
            r.StepUntil(RunPhase.LootPick);
            r.PickAndConfirm();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            r.Step(300);
            Assert.AreEqual(atClear, r.W.Wave.WaveTick, "WaveClearing·LootPick·Camp는 WaveCombat이 아니다");
            Assert.AreEqual(atClear, r.Run.WaveResults[0].Stats.ElapsedTicks);
        }

        [Test]
        public void Contract__RunStateStartsFromConfig_RevengeAndAffinity()
        {
            var r = Rig.Create(c =>
            {
                c.RevengePending = true;
                c.KnownAffinities.Add(AffinityKey.Of(Ids.Enemy.SlimeMoss, Affinity.Weak));
            });
            Assert.IsTrue(r.Run.RevengeActive);
            Assert.IsTrue(r.Run.AffinityKnown.Contains(AffinityKey.Of(Ids.Enemy.SlimeMoss, Affinity.Weak)));
            // 판 중에 Spells가 더한 것까지 요약으로 나간다
            r.Run.AffinityKnown.Add(AffinityKey.Of(Ids.Enemy.WolfBramble, Affinity.Resist));
            var sum = r.W.Mods.Run.BuildSummary(r.W);
            Assert.AreEqual(2, sum.AffinityKnown.Count);
            CollectionAssert.Contains(sum.AffinityKnown, AffinityKey.Of(Ids.Enemy.SlimeMoss, Affinity.Weak));
            CollectionAssert.Contains(sum.AffinityKnown, AffinityKey.Of(Ids.Enemy.WolfBramble, Affinity.Resist));
            Assert.IsTrue(sum.RevengePending);
            Assert.IsTrue(sum.RevengeActive);

            var plain = Rig.Create();
            Assert.IsFalse(plain.Run.RevengeActive);
            Assert.AreEqual(0, plain.Run.AffinityKnown.Count);
        }

        [Test]
        public void Contract__CommandsOutsideTheirPhase_AreDropped()
        {
            var r = Rig.InCombatAt(2);
            r.Cmd(RunCommandKind.LootConfirm);
            r.Cmd(RunCommandKind.CampDepart);
            r.Cmd(RunCommandKind.CampOpen);
            r.Cmd(RunCommandKind.CampHeal);
            r.Cmd(RunCommandKind.LoadoutMove, 1, -1);
            r.Cmd(RunCommandKind.PracticeReset);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
            Assert.AreEqual(0, r.W.Commands.Count);
            Assert.AreEqual(Ids.Block.SplitTwo, r.P.Loadout.Links[0].BlockId, "전투 중에는 조립 명령을 받지 않는다");
        }
    }
}
