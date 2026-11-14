using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Enemies;

namespace Wizard.Solo.Tests.Enemies
{
    /// <summary>모듈 계약 벡터(Core/Sim/Modules.cs 공통 규칙 2·3·4·6, 00 문서 5.4절 (다)(라)(바), D-45·D-62~D-64)와 스폰·접촉·사망·허수아비.</summary>
    public class EnemyContractTests
    {
        // ------------------------------------------------------------ 스폰

        // 05 요약서 4절: Spawning 30틱(피해 주고받지 않음) → Act. 등장 예고 EnemySpawnNotice, 끝나면 EnemySpawned.
        [Test]
        public void S05_SPAWN__ThirtyTickNoticeThenAct()
        {
            var k = new Kit();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 12.2f, 7f, instant: false);
            Assert.AreEqual(EnemyAiState.Spawning, e.Ai);
            SimEvent ev;
            Assert.IsTrue(k.TryFindEvent(SimEventType.EnemySpawnNotice, 0, out ev));
            Assert.AreEqual(30, ev.A);
            Assert.AreEqual(Ids.Enemy.SlimeMoss, ev.Id);
            k.RunThrough(29);
            Assert.AreEqual(EnemyAiState.Spawning, e.Ai);
            Assert.AreEqual(0, k.Player.Hits.Count, "등장 중에는 접촉이 없다");
            Assert.AreEqual(0, k.CountEvents(SimEventType.EnemySpawned, e.Id));
            Kit.AreClose(new Vector2(12.2f, 7f), e.Pos);
            k.RunThrough(30);
            Assert.AreEqual(EnemyAiState.Act, e.Ai);
            Assert.AreEqual(1, k.CountEvents(SimEventType.EnemySpawned, e.Id));
            Assert.AreEqual(30, k.Player.Hits[0].Tick);
        }

        [Test]
        public void S05_SPAWN__InstantSkipsNoticeAndCopiesSummoner()
        {
            var k = new Kit();
            var e = k.Sys.Spawn(k.W, new EnemySpawnSpec { DefId = Ids.Enemy.BatDusk, Pos = new Vector2(3, 3), Instant = true, NoReward = true, SummonerId = 77 });
            Assert.AreEqual(EnemyAiState.Act, e.Ai);
            Assert.AreEqual(77, e.SummonerId);
            Assert.IsTrue(e.NoReward);
            Assert.AreEqual(0, k.CountEvents(SimEventType.EnemySpawnNotice));
            Assert.AreEqual(1, k.CountEvents(SimEventType.EnemySpawned, e.Id));
            Assert.IsTrue(e.Has(EnemyTags.Flying));
            Assert.AreEqual(Ids.Element.Fire, e.Weak);
            Assert.AreEqual(Ids.Element.Dark, e.Resist);
            Assert.AreEqual(0.25f, e.BodyRadius, 1e-6f);
        }

        // 6.13절: 보스가 부른 졸개는 꼬리표·상성·보상이 없다. 새싹 체력 20 × H(w).
        [Test]
        public void S05_MINION__BossSummonHasNoTagsNoAffinityNoReward()
        {
            var k = new Kit();
            k.Waves.Mul = wv => 2.08f;
            k.W.Wave.Wave = 10;
            var boss = k.AddBossBody(12, 3);
            var slime = k.Sys.Spawn(k.W, new EnemySpawnSpec { DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(5, 5), NoReward = true, SummonerId = boss.Id, Instant = true });
            Assert.AreEqual(EnemyTags.None, slime.Tags);
            Assert.AreEqual(Ids.Element.Neutral, slime.Weak);
            Assert.AreEqual(Ids.Element.Neutral, slime.Resist);
            Assert.IsTrue(slime.NoReward);
            var seed = k.Sys.Spawn(k.W, new EnemySpawnSpec { DefId = Ids.Enemy.SporeSeedling, Pos = new Vector2(6, 5), SummonerId = boss.Id, Instant = true });
            Assert.IsTrue(seed.NoReward, "졸개 정의는 보상이 없다");
            Assert.AreEqual(20f * 2.08f, seed.MaxHp, 1e-3f);
            Assert.AreEqual(0, seed.Cost);
        }

        [Test]
        public void S05_SPAWN__UnknownIdReturnsNull()
        {
            var k = new Kit();
            Assert.IsNull(k.Spawn("enemy.nope", 1, 1));
            Assert.AreEqual(0, k.W.Enemies.Count);
        }

        // ------------------------------------------------------------ 공통 규칙 2: 죽어 가는 적

        [Test]
        public void S00_RULE2__PendingDeathEnemyDoesNothingInStage7And9()
        {
            var k = new Kit();
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 12.3f, 7f);      // 플레이어와 겹친다
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 7.5f, 7f);      // 발동 거리 4.5
            var shaman = k.Spawn(Ids.Enemy.ShamanCap, 12f, 13f);      // 사거리 안
            slime.PendingDeath = true;
            wolf.PendingDeath = true;
            shaman.PendingDeath = true;
            k.W.Step();
            Assert.AreEqual(0, k.Player.Hits.Count, "접촉 없음");
            Assert.AreEqual(0, k.Threats.Tickets.Count, "예고 요청 없음");
            Assert.AreEqual(0, k.Threats.Shots.Count);
            Kit.AreClose(new Vector2(12.3f, 7f), slime.Pos);
            Kit.AreClose(new Vector2(7.5f, 7f), wolf.Pos);
            Assert.IsTrue(slime.Dead && wolf.Dead && shaman.Dead, "10단계가 치운다");
        }

        [Test]
        public void S00_RULE2__PendingDeathMidDashStopsMovingAndHitting()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            k.RunThrough(32);
            Assert.IsTrue(wolf.DashActive);
            Vector2 at = wolf.Pos;
            wolf.PendingDeath = true;
            k.W.Step();
            Kit.AreClose(at, wolf.Pos);
            Assert.AreEqual(0, k.Player.Hits.Count);
        }

        // ------------------------------------------------------------ 공통 규칙 3: 대상 없음

        [Test]
        public void S00_RULE3__NoTargetMeansIdleWithoutException()
        {
            var k = new Kit();
            k.W.Run.Phase = RunPhase.Practice;
            k.W.Run.PracticeEnemyFire = true;
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 12.3f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 7.5f, 7f);
            var shaman = k.Spawn(Ids.Enemy.ShamanCap, 12f, 13f);
            var bat = k.Spawn(Ids.Enemy.BatDusk, 3f, 3f);
            var vine = k.Spawn(Ids.Enemy.VineThorn, 18f, 4f);
            var dummy = k.Spawn(Ids.Enemy.Dummy, 12f, 4f);
            k.P.Downed = true;
            Assert.IsNull(k.W.NearestPlayer(Vector2.zero));
            Assert.DoesNotThrow(() => k.RunThrough(299));
            Assert.AreEqual(0, k.Threats.Tickets.Count);
            Assert.AreEqual(0, k.Threats.Shots.Count);
            Assert.AreEqual(0, k.Player.Hits.Count);
            Kit.AreClose(new Vector2(12.3f, 7f), slime.Pos);
            Kit.AreClose(new Vector2(7.5f, 7f), wolf.Pos);
            Kit.AreClose(new Vector2(12f, 13f), shaman.Pos);
            Kit.AreClose(new Vector2(3f, 3f), bat.Pos);
            Assert.IsFalse(vine.Dead || dummy.Dead);
        }

        // ------------------------------------------------------------ 공통 규칙 4: 예고 표의 틱

        // 대기 줄에서 기다린 틱은 WindUp에 들지 않는다: 10틱 막힘 → 허가 틱 10이 s, 돌진은 10 + 30 = 40틱의 7단계에 시작.
        [Test]
        public void S00_RULE4__WaitingTicksNotCountedInWindUp()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(10.4f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);       // 거리 5.4. 기다리는 동안 쫓아가도 3.5~5.5 안에 있다
            k.Threats.BlockUntilTick = 10;
            k.RunThrough(0);
            Assert.AreEqual(1, k.Threats.Tickets.Count);
            Assert.AreEqual(0, k.Threats.Tickets[0].RequestTick);
            Assert.AreEqual(EnemyAiState.Waiting, wolf.Ai);
            k.RunThrough(9);
            Assert.AreEqual(EnemyAiState.Waiting, wolf.Ai);
            Assert.IsFalse(wolf.WeaponTint);
            k.RunThrough(10);
            Assert.AreEqual(EnemyAiState.WindUp, wolf.Ai, "허가된 틱의 8단계 뒤(9단계 머리)에 WindUp");
            Assert.AreEqual(10, wolf.AiStateStartTick);
            Assert.IsTrue(wolf.WeaponTint);
            Assert.AreEqual(1, k.Threats.Tickets.Count, "다시 청하지 않는다");
            k.RunThrough(39);
            Assert.IsFalse(wolf.DashActive);
            k.RunThrough(40);
            Assert.IsTrue(wolf.DashActive);
        }

        // 요청한 틱에 바로 허가되면 그 틱이 s다(같은 틱에 WindUp, 시계 = 요청 틱).
        [Test]
        public void S00_RULE4__GrantedSameTickStartsWindUpClockAtRequestTick()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            k.RunThrough(0);
            Assert.AreEqual(EnemyAiState.WindUp, wolf.Ai);
            Assert.AreEqual(0, wolf.AiStateStartTick);
            Kit.AreClose(new Vector2(5f, 7f), wolf.Pos);
        }

        // 표가 밖에서 취소되면(웨이브 끝, 단계 전환) 주인은 평소 행동으로 돌아간다.
        [Test]
        public void S00_RULE4__TicketCancelledOutsideReturnsToAct()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            wolf.MoveSpeed = 0f;
            k.RunThrough(5);
            k.Threats.Tickets[0].Cancelled = true;
            k.RunThrough(6);
            Assert.AreEqual(EnemyAiState.Act, wolf.Ai);
            Assert.IsFalse(wolf.WeaponTint);
            k.RunThrough(40);
            Assert.IsFalse(wolf.DashActive && k.Threats.Tickets.Count == 1, "취소된 표로 돌진하지 않는다");
        }

        // ------------------------------------------------------------ 공통 규칙 6, D-62: 돌진 충돌

        [Test]
        public void S00_RULE6__DashHitIsSourceDashAndCellsComputedAtCollisionTick()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            k.RunThrough(34);
            Assert.IsTrue(wolf.DashActive);
            Assert.AreEqual(0, k.Player.Hits.Count, "아직 닿지 않았다");
            for (int i = 0; i < k.Threats.DamageCalls.Count; i++)
                Assert.AreNotEqual(PlayerDamageSource.Dash, k.Threats.DamageCalls[i].Source, "충돌 전에는 칸을 계산하지 않는다");
            // 돌진이 시작된 뒤에 성장이 켜져도(최종 격노 등) 충돌 틱의 값으로 들어간다.
            k.Threats.DamageBonus = 1;
            k.RunThrough(49);
            var hits = k.Player.AcceptedHits();
            Assert.AreEqual(1, hits.Count, "돌진 한 번에 한 번");
            var h = hits[0];
            Assert.AreEqual(PlayerDamageSource.Dash, h.Hit.Source);
            Assert.AreEqual(wolf.Id, h.Hit.AttackId);
            Assert.AreEqual(wolf.Id, h.Hit.SourceEnemyId);
            Assert.AreEqual(k.Threats.Tickets[0].Req.CastId, h.Hit.CastId);
            Assert.AreEqual(1 + 1, h.Hit.DamageCells);
            bool found = false;
            for (int i = 0; i < k.Threats.DamageCalls.Count; i++)
            {
                var c = k.Threats.DamageCalls[i];
                if (c.Source != PlayerDamageSource.Dash) continue;
                Assert.AreEqual(h.Tick, c.Tick);
                Assert.AreEqual(1, c.Base, "DashBaseDamage = 공격 정의의 damage");
                Assert.AreEqual(wolf.Id, c.OwnerId);
                found = true;
            }
            Assert.IsTrue(found);
        }

        // 돌진 예고 구역은 피해 0·스침 없음(피해는 몸 충돌이 낸다 — 구역이 터질 때 한 번 더 때리지 않는다).
        [Test]
        public void S00_RULE6__DashTelegraphZoneCarriesNoDamage()
        {
            var k = new Kit();
            k.P.Pos = new Vector2(9.5f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 5f, 7f);
            k.RunThrough(0);
            ZoneSpec z = k.Threats.Tickets[0].Req.Zones[0];
            Assert.AreEqual(0, z.BaseDamage);
            Assert.IsFalse(z.Grazeable);
            Assert.IsFalse(z.Harmless, "위험색 예고다");
            Assert.AreEqual(wolf.Id, k.Threats.Tickets[0].Req.OwnerEnemyId);
        }

        // ------------------------------------------------------------ 접촉(05 요약서 4절, D-63)

        // 같은 적에게서 1.5초(90틱)에 1회, 피해 1, 분류 Contact, AttackId = 적 Id
        [Test]
        public void S05_CONTACT__OncePer90Ticks()
        {
            var k = new Kit();
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 12.3f, 7f);
            k.RunThrough(180);
            var hits = k.Player.AcceptedHits();
            Assert.AreEqual(3, hits.Count);
            Assert.AreEqual(0, hits[0].Tick);
            Assert.AreEqual(90, hits[1].Tick);
            Assert.AreEqual(180, hits[2].Tick);
            Assert.AreEqual(PlayerDamageSource.Contact, hits[0].Hit.Source);
            Assert.AreEqual(slime.Id, hits[0].Hit.AttackId);
            Assert.AreEqual(1, hits[0].Hit.DamageCells);
            Assert.AreEqual(k.Player.Hits.Count, hits.Count, "쿨다운 중에는 QueueHit을 부르지 않는다");
        }

        // 무적 중 접촉은 쿨다운을 쓰지 않는다(U-15): QueueHit이 false면 다음 틱에 다시 청한다.
        [Test]
        public void S05_CONTACT__InvulnerableContactDoesNotConsumeCooldown()
        {
            var k = new Kit();
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 12.3f, 7f);
            k.Player.Accept = false;
            k.RunThrough(9);
            Assert.AreEqual(10, k.Player.Hits.Count, "무적 동안 매 틱 후보를 올린다");
            Assert.AreEqual(0, slime.ContactReadyTick);
            k.Player.Accept = true;
            k.RunThrough(10);
            Assert.AreEqual(100, slime.ContactReadyTick);
            k.RunThrough(99);
            Assert.AreEqual(11, k.Player.Hits.Count);
            k.RunThrough(100);
            Assert.AreEqual(12, k.Player.Hits.Count);
        }

        // 판정 겹침: 거리 < 반지름 합(같으면 안 맞음, U-10). 슬라임 0.35 + 플레이어 0.15 = 0.5
        [Test]
        public void S05_CONTACT__OverlapIsStrictlyLess()
        {
            Assert.IsFalse(EnemyRules.Overlap(new Vector2(0, 0), 0.25f, new Vector2(0.5f, 0), 0.25f));
            Assert.IsTrue(EnemyRules.Overlap(new Vector2(0, 0), 0.25f, new Vector2(0.49f, 0), 0.25f));
            var k = new Kit();
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 12.6f, 7f);
            slime.MoveSpeed = 0f;
            k.RunThrough(5);
            Assert.AreEqual(0, k.Player.Hits.Count);
        }

        // 늑대는 접촉 0, 족제비는 땅속일 때 0
        [Test]
        public void S05_CONTACT__WolfAndBurrowedWeaselHaveNone()
        {
            var k = new Kit();
            k.Threats.BlockAll = true;
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 12.2f, 7f);
            var weasel = k.Spawn(Ids.Enemy.WeaselPot, 12f, 7.2f);
            k.RunThrough(60);
            Assert.AreEqual(0, k.Player.Hits.Count);
            Assert.IsTrue(weasel.Burrowed);
            Assert.IsFalse(wolf.Dead);
        }

        // ------------------------------------------------------------ 사망(00 문서 5.4절 (다), D-45)

        [Test]
        public void S00_DEATH__HookOrderAndEventAfterHooks()
        {
            var k = new Kit();
            k.LogHooks();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5);
            e.PendingDeath = true;
            e.KillingHit = new HitInfo { AttackerSlot = 0, EnemyId = e.Id, Killed = true };
            k.Sys.Stage10_Deaths(k.W);
            CollectionAssert.AreEqual(new List<string>
            {
                "threats.cancel:" + e.Id, "run.killed:" + e.Id, "traits.killed:" + e.Id, "waves.killed:" + e.Id
            }, k.Log);
            Assert.AreEqual(0, k.Waves.KilledEventsSeenAtHook, "EnemyKilled 이벤트는 훅 뒤");
            Assert.IsFalse(k.Waves.DeadAtHook, "Dead는 맨 끝");
            Assert.AreEqual(1, k.CountEvents(SimEventType.EnemyKilled, e.Id));
            Assert.IsTrue(e.Dead);
            Assert.AreEqual(EnemyAiState.Dead, e.Ai);
            // 다시 불러도 두 번 처리하지 않는다
            k.Sys.Stage10_Deaths(k.W);
            Assert.AreEqual(4, k.Log.Count);
        }

        // 부수효과(갈라짐)는 예고 취소보다 먼저다.
        [Test]
        public void S00_DEATH__SideEffectsBeforeCancelTelegraphs()
        {
            var k = new Kit();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5, elite: true);
            int enemiesAtCancel = -1;
            k.Threats.OnCancelOwner = (w, id) => { if (id == e.Id) enemiesAtCancel = w.Enemies.Count; };
            e.PendingDeath = true;
            k.Sys.Stage10_Deaths(k.W);
            Assert.AreEqual(1 + 3, enemiesAtCancel);
        }

        // 훅은 RunPhase와 무관하게 부른다(RunEnded 뒤의 처리는 Run·Bosses가 거른다). 보스 몸통도 같은 길.
        [Test]
        public void S00_D45__HooksCalledEvenAfterRunEndedAndBossBodySamePath()
        {
            var k = new Kit();
            k.LogHooks();
            var e = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5);
            var boss = k.AddBossBody(12, 3);
            k.W.Run.Phase = RunPhase.RunEnded;
            e.PendingDeath = true;
            boss.PendingDeath = true;
            k.Sys.Stage10_Deaths(k.W);
            Assert.AreEqual(8, k.Log.Count);
            Assert.AreEqual("run.killed:" + e.Id, k.Log[1]);
            Assert.AreEqual("run.killed:" + boss.Id, k.Log[5]);
            SimEvent ev;
            Assert.IsTrue(k.TryFindEvent(SimEventType.EnemyKilled, boss.Id, out ev));
            Assert.AreEqual(2, ev.B);
            Assert.IsTrue(boss.Dead);
        }

        [Test]
        public void S05_REWARD__NoRewardKillEventKindThreeGoldZero()
        {
            var k = new Kit();
            var e = k.Sys.Spawn(k.W, new EnemySpawnSpec { DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(5, 5), NoReward = true, Instant = true });
            e.PendingDeath = true;
            k.Sys.Stage10_Deaths(k.W);
            SimEvent ev;
            Assert.IsTrue(k.TryFindEvent(SimEventType.EnemyKilled, e.Id, out ev));
            Assert.AreEqual(3, ev.B);
            Assert.AreEqual(0, ev.A);
        }

        // ------------------------------------------------------------ RemoveAll

        [Test]
        public void S00_REMOVE_ALL__MinionsOnlyKeepsBossBodyAndCallsNoHooks()
        {
            var k = new Kit();
            k.LogHooks();
            var a = k.Spawn(Ids.Enemy.SlimeMoss, 5, 5);
            var b = k.Spawn(Ids.Enemy.SlimeMoss, 6, 5, elite: true);
            var bug = k.Spawn(Ids.Enemy.BugLantern, 7, 5);
            var boss = k.AddBossBody(12, 3);
            k.Sys.RemoveAll(k.W, true);
            Assert.IsTrue(a.Dead && b.Dead && bug.Dead);
            Assert.IsFalse(boss.Dead);
            Assert.AreEqual(4, k.W.Enemies.Count, "갈라짐 같은 부수효과가 없다");
            Assert.AreEqual(0, k.CountEvents(SimEventType.EnemyKilled));
            for (int i = 0; i < k.Log.Count; i++) StringAssert.StartsWith("threats.cancel", k.Log[i]);
            // 유폭도 없다
            k.RunThrough(60);
            Assert.AreEqual(0, k.Spells.Direct.Count);

            k.Sys.RemoveAll(k.W, false);
            Assert.IsTrue(boss.Dead);
        }

        // ------------------------------------------------------------ 허수아비(08 요약서 8.5절 '적 탄 쏘기')

        [Test]
        public void S13_DUMMY__FiresOnlyWhileOptionOnAndNeverDies()
        {
            var k = new Kit();
            k.W.Run.Phase = RunPhase.Practice;
            k.W.Wave.Wave = 0;
            var d = k.Spawn(Ids.Enemy.Dummy, 16f, 7f);
            Assert.IsNotNull(d);
            Assert.Greater(d.HpFloor, 0f);
            Assert.Greater(d.MaxHp, d.HpFloor);
            int interval = SimConst.Ticks(k.Sys.Tuning(k.W).DummyFireIntervalSeconds);
            Assert.Greater(interval, 0);

            k.RunThrough(299);
            Assert.AreEqual(0, k.Threats.Shots.Count, "옵션이 꺼져 있으면 쏘지 않는다");
            Kit.AreClose(new Vector2(16f, 7f), d.Pos);

            k.W.Run.PracticeEnemyFire = true;
            k.RunThrough(300 + interval - 1);
            Assert.AreEqual(0, k.Threats.Shots.Count);
            k.RunThrough(300 + interval);
            Assert.AreEqual(1, k.Threats.Shots.Count);
            var shot = k.Threats.Shots[0].Spec;
            Kit.AreClose(new Vector2(-1, 0), shot.Dir);
            Assert.AreEqual(d.Id, shot.OwnerEnemyId);
            Assert.AreEqual(1, shot.BaseDamage);
            Assert.Less(shot.Range, 0f, "직선 탄, 벽까지");
            k.RunThrough(300 + 2 * interval);
            Assert.AreEqual(2, k.Threats.Shots.Count);

            k.W.Run.PracticeEnemyFire = false;
            k.RunThrough(300 + 6 * interval);
            Assert.AreEqual(2, k.Threats.Shots.Count, "끄면 멈춘다");

            // 체력이 바닥나지 않는다: 깎여도 다음 틱에 가득.
            d.Hp = d.HpFloor;
            k.W.Step();
            Assert.AreEqual(d.MaxHp, d.Hp);
            Assert.IsFalse(d.PendingDeath || d.Dead);
            Assert.AreEqual(0, k.Player.Hits.Count, "접촉 피해 0");
        }

        // ------------------------------------------------------------ 느린 탄막(06 요약서 1.4절, D-64)

        [Test]
        public void S07_SLOW_BARRAGE__RingExtraShotsFollowSameDirectionsOneTileBehind()
        {
            var k = new Kit();
            k.W.Wave.EnemyShotCountMul = 1.5f;
            k.W.Wave.EnemyShotSpeedMul = 0.8f;
            var wisp = k.Spawn(Ids.Enemy.WispStray, 6f, 7f);
            k.RunThrough(120);
            Assert.AreEqual(12, k.Threats.Shots.Count, "floor(8 × 3/2)");
            var first = k.Threats.ShotsAt(36);
            Assert.AreEqual(8, first.Count);
            // 고리의 각은 그대로(45도)
            for (int i = 0; i < 8; i++)
                Assert.Less(Kit.AngleDelta(Kit.Angle(first[i].Spec.Dir), 45f * i), 0.01f);
            int late = 0;
            for (int i = 0; i < k.Threats.Shots.Count; i++)
            {
                var s = k.Threats.Shots[i];
                if (s.Tick == 36) continue;
                late++;
                Assert.AreEqual(wisp.Id, s.Spec.OwnerEnemyId);
                // 같은 방향의 앞 탄보다 1.0칸 이상 뒤에서 따라간다(탄 속도 4.5 × 0.8)
                float behind = (s.Tick - 36) * s.Spec.Speed * 0.8f / 60f;
                Assert.GreaterOrEqual(behind, 1.0f);
                bool sameDir = false;
                for (int j = 0; j < 8; j++) if (Kit.AngleDelta(Kit.Angle(first[j].Spec.Dir), Kit.Angle(s.Spec.Dir)) < 0.01f) sameDir = true;
                Assert.IsTrue(sameDir);
                Assert.AreEqual(first[0].Spec.CastId, s.Spec.CastId, "한 시전");
            }
            Assert.AreEqual(4, late);
        }

        [Test]
        public void S07_SLOW_BARRAGE__FanKeepsNeighbourAngle()
        {
            var k = new Kit();
            k.W.Wave.EnemyShotCountMul = 1.5f;
            k.W.Wave.EnemyShotSpeedMul = 0.8f;
            k.Spawn(Ids.Enemy.ShamanCap, 6f, 7f);
            k.RunThrough(24);
            var shots = k.Threats.ShotsAt(24);
            Assert.AreEqual(4, shots.Count, "floor(3 × 3/2)");
            var angles = new List<float>();
            for (int i = 0; i < shots.Count; i++)
            {
                float a = Kit.Angle(shots[i].Spec.Dir);
                angles.Add(a > 180f ? a - 360f : a);
            }
            angles.Sort();
            for (int i = 1; i < angles.Count; i++) Assert.AreEqual(20f, angles[i] - angles[i - 1], 0.01f, "이웃 탄 사이 20도 그대로");
            Assert.AreEqual(0f, angles[0] + angles[3], 0.01f, "플레이어 쪽에 가운데를 둔다");
            // R3: 발사점 3칸 지점 이웃 탄 사이 호 길이 3 × 20도(rad) ≥ 1.0칸
            Assert.GreaterOrEqual(3f * 20f * Mathf.Deg2Rad, 1.0f);
            Assert.AreEqual(6.5f, shots[0].Spec.Speed, 1e-5f, "속도 배율은 Threats.SpawnShot이 건다");
        }

        [Test]
        public void S07_SLOW_BARRAGE__NoWhimNoExtraShots()
        {
            var k = new Kit();
            k.Spawn(Ids.Enemy.WispStray, 6f, 7f);
            k.RunThrough(120);
            Assert.AreEqual(8, k.Threats.Shots.Count);
        }

        // ------------------------------------------------------------ 소환 상한은 SummonerId로 센다

        [Test]
        public void S05_SUMMON__CapCountedBySummonerId()
        {
            var k = new Kit();
            var queen = k.Spawn(Ids.Enemy.BatDusk, 3f, 3f, elite: true);
            // 이미 이 여왕이 부른 6마리와 남이 부른 5마리가 있다.
            for (int i = 0; i < 6; i++)
                k.Sys.Spawn(k.W, new EnemySpawnSpec { DefId = Ids.Enemy.BatDusk, Pos = new Vector2(20, 2 + i), NoReward = true, SummonerId = queen.Id, Instant = true });
            for (int i = 0; i < 5; i++)
                k.Sys.Spawn(k.W, new EnemySpawnSpec { DefId = Ids.Enemy.BatDusk, Pos = new Vector2(22, 2 + i), NoReward = true, SummonerId = 9999, Instant = true });
            Assert.AreEqual(6, k.Sys.CountSummonedBy(k.W, queen.Id));
            Assert.AreEqual(5, k.Sys.CountSummonedBy(k.W, 9999));
            queen.Hp = queen.MaxHp * 0.5f;
            k.W.Step();
            // 상한 4 × 2 = 8 → 2마리만 더 나온다.
            Assert.AreEqual(8, k.Sys.CountSummonedBy(k.W, queen.Id));
            Assert.AreEqual(5, k.Sys.CountSummonedBy(k.W, 9999));
        }

        // ------------------------------------------------------------ 전투가 아닌 단계, 결정론

        [Test]
        public void S00_PHASE__NoAiOutsideCombat()
        {
            var k = new Kit();
            k.W.Run.Phase = RunPhase.Camp;
            var slime = k.Spawn(Ids.Enemy.SlimeMoss, 12.3f, 7f);
            var wolf = k.Spawn(Ids.Enemy.WolfBramble, 7.5f, 7f);
            k.RunThrough(30);
            Assert.AreEqual(0, k.Player.Hits.Count);
            Assert.AreEqual(0, k.Threats.Tickets.Count);
            Kit.AreClose(new Vector2(12.3f, 7f), slime.Pos);
            Kit.AreClose(new Vector2(7.5f, 7f), wolf.Pos);
        }

        [Test]
        public void S00_DETERMINISM__SameSeedSameResult()
        {
            Vector2[] a = RunScenario(7), b = RunScenario(7);
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i]);
        }

        static Vector2[] RunScenario(ulong seed)
        {
            var k = new Kit(seed);
            k.P.Pos = new Vector2(18f, 9f);
            var list = new List<EnemyState>
            {
                k.Spawn(Ids.Enemy.BatDusk, 2f, 3f), k.Spawn(Ids.Enemy.BatDusk, 3f, 11f), k.Spawn(Ids.Enemy.WispStray, 12f, 3f),
                k.Spawn(Ids.Enemy.WolfBramble, 6f, 6f), k.Spawn(Ids.Enemy.ToadDrum, 8f, 8f), k.Spawn(Ids.Enemy.ShamanCap, 10f, 12f)
            };
            k.RunThrough(700);
            var r = new Vector2[list.Count];
            for (int i = 0; i < r.Length; i++) r[i] = list[i].Pos;
            return r;
        }
    }
}
