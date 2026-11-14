using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;

namespace Wizard.Solo.Tests.RunModuleTests
{
    // 다른 모듈의 가짜(기본 클래스를 상속). RunFlow가 계약대로 부르는지와 상태 전이를 본다.

    internal sealed class FakePlayer : PlayerModule
    {
        public int HealCalls, RiseCalls, ResetCalls, RecompileCalls;
        /// <summary>10단계에서 Run.OnPlayerDowned를 부르게 한다(같은 틱 사망 순서 시험).</summary>
        public bool DownAtStage10;

        public override void SetupPlayer(SimWorld w, PlayerState p, RunConfig cfg)
        {
            p.CharId = cfg.CharId;
            p.MaxHp = 8 * SimConst.HpPerCell;
            p.Hp = p.MaxHp;
            p.Pos = new Vector2(w.ArenaWidth * 0.5f, w.ArenaHeight * 0.5f);
        }
        public override void Heal(SimWorld w, PlayerState p, int cells)
        {
            HealCalls++;
            p.Hp = Mathf.Min(p.MaxHp, p.Hp + cells * SimConst.HpPerCell);
        }
        public override void Rise(SimWorld w, PlayerState p)
        {
            RiseCalls++;
            p.Hp = p.MaxHp;
            p.Downed = false;
        }
        public override void ResetForWaveEnd(SimWorld w, PlayerState p) { ResetCalls++; }
        public override void RecompileSpell(SimWorld w, PlayerState p) { RecompileCalls++; }
        public override void Stage10_ResolveHits(SimWorld w)
        {
            if (!DownAtStage10) return;
            DownAtStage10 = false;
            w.P0.Hp = 0;
            w.P0.Downed = true;
            w.Mods.Run.OnPlayerDowned(w, w.P0);
        }
    }

    internal sealed class FakeWaves : WaveModule
    {
        public bool Cleared;
        public int PreludeTicks;
        public int BeginCalls, EndCalls, BuildCalls;
        public Func<int, string> BossFor;
        public Func<int, bool> PhantomFirstFor;

        public override WavePlan BuildPlan(SimWorld w, int wave)
        {
            BuildCalls++;
            var plan = new WavePlan { Wave = wave, Tod = DayCycle.Tod(wave) };
            if (BossFor != null) plan.BossId = BossFor(wave);
            else if (DayCycle.IsBossWave(wave)) plan.BossId = Ids.Boss.KilnBoar;
            if (PhantomFirstFor != null) plan.PhantomFirst = PhantomFirstFor(wave);
            return plan;
        }
        public override int BeginWave(SimWorld w, WavePlan plan)
        {
            BeginCalls++;
            Cleared = false;
            base.BeginWave(w, plan);
            return PreludeTicks;
        }
        public override bool IsCleared(SimWorld w) => Cleared;
        public override void EndWave(SimWorld w) { EndCalls++; }
        // 계약: 점수용 시계는 WaveCombat 동안만 흐른다.
        public override void Stage11_TickEnd(SimWorld w)
        {
            if (w.Run.Phase == RunPhase.WaveCombat) w.Wave.WaveTick++;
        }
    }

    internal sealed class FakeBosses : BossModule
    {
        public int SpawnCalls, DismissCalls, CollapseCalls;
        /// <summary>등장이 끝나는 틱 수. 일부러 72가 아닌 값으로 둔다(Run이 따로 세지 않는지 본다).</summary>
        public int IntroTicks = 100;
        public float HpMax = 7111f;

        public override EnemyState SpawnBoss(SimWorld w, WavePlan plan)
        {
            SpawnCalls++;
            var body = new EnemyState { Id = w.NewEntityId(), DefId = plan.BossId, IsBoss = true, MaxHp = HpMax, Hp = HpMax };
            w.Enemies.Add(body);
            w.Boss = new BossState
            {
                EnemyId = body.Id, Def = w.Content.Boss(plan.BossId), Wave = plan.Wave, FirstEncounter = plan.PhantomFirst,
                State = BossFightState.Intro, StateStartTick = w.Tick, HpMin = HpMax
            };
            return body;
        }
        public override float MaxHp(IContentDb db, string bossId, int wave, bool firstEncounter) => HpMax;
        public override void Stage7_Ai(SimWorld w)
        {
            if (w.Boss != null && w.Boss.State == BossFightState.Intro && w.Tick - w.Boss.StateStartTick >= IntroTicks)
                w.Boss.State = BossFightState.Waiting;
        }
        public override void BeginCollapse(SimWorld w)
        {
            CollapseCalls++;
            w.Boss.State = BossFightState.Collapse;
        }
        public override void Dismiss(SimWorld w)
        {
            DismissCalls++;
            for (int i = 0; i < w.Enemies.Count; i++) if (w.Enemies[i].IsBoss) w.Enemies[i].Dead = true;
            w.Boss = null;
        }
    }

    internal sealed class FakeScore : ScoreRules
    {
        public int Calls;
        public int ElapsedSeen = -1;
        public int Fixed = 1000;
        public override int WaveScore(IContentDb db, WaveStats stats, out ScoreBreakdown breakdown)
        {
            Calls++;
            ElapsedSeen = stats.ElapsedTicks;
            int score = stats.PhantomFirst ? db.Scoring.PhantomFirst : Fixed;
            breakdown = new ScoreBreakdown { Total = score };
            return score;
        }
        public override int SkipScore(IContentDb db, int wave) => wave * 10;
    }

    internal sealed class FakeArts : ArtModule
    {
        public int ClearZoneCalls, ResetCalls, OfferCalls;
        /// <summary>차례마다 내줄 후보(없으면 빈 차례).</summary>
        public readonly Dictionary<int, string[]> Candidates = new Dictionary<int, string[]>();
        public string[] DefaultCandidates;

        public override ArtOffer BuildOffer(SimWorld w, PlayerState p, int turnWave)
        {
            OfferCalls++;
            var offer = new ArtOffer { TurnWave = turnWave };
            string[] c;
            if (!Candidates.TryGetValue(turnWave, out c)) c = DefaultCandidates;
            if (c != null)
                for (int i = 0; i < c.Length; i++) if (!p.UnlockedArts.Contains(c[i])) offer.Candidates.Add(c[i]);
            return offer;
        }
        public override bool Unlock(SimWorld w, PlayerState p, string artId) => p.UnlockedArts.Add(artId);
        public override void ClearZones(SimWorld w) { ClearZoneCalls++; }
        public override void OnCommandReset(SimWorld w, PlayerState p, CommandResetReason reason) { ResetCalls++; }
    }

    internal sealed class FakeEnemies : EnemyModule
    {
        public int SpawnCalls, RemoveAllCalls;
        /// <summary>10단계에서 이 적들의 사망을 알린다(Run.OnEnemyKilled) — 계약 순서 흉내.</summary>
        public readonly List<EnemyState> DieAtStage10 = new List<EnemyState>();
        public Action AfterDeaths;

        public override EnemyState Spawn(SimWorld w, in EnemySpawnSpec spec)
        {
            SpawnCalls++;
            var e = new EnemyState
            {
                Id = w.NewEntityId(), DefId = spec.DefId, Def = w.Content.Enemy(spec.DefId), Pos = spec.Pos, NoReward = spec.NoReward,
                MaxHp = spec.HpOverride, Hp = spec.HpOverride
            };
            w.Enemies.Add(e);
            return e;
        }
        public override void RemoveAll(SimWorld w, bool minionsOnly)
        {
            RemoveAllCalls++;
            for (int i = 0; i < w.Enemies.Count; i++) if (!minionsOnly || !w.Enemies[i].IsBoss) w.Enemies[i].Dead = true;
        }
        public override void Stage10_Deaths(SimWorld w)
        {
            if (DieAtStage10.Count == 0) return;
            for (int i = 0; i < DieAtStage10.Count; i++) w.Mods.Run.OnEnemyKilled(w, DieAtStage10[i], default(HitInfo));
            DieAtStage10.Clear();
            if (AfterDeaths != null) AfterDeaths();
        }
    }

    internal sealed class FakeThreats : ThreatModule
    {
        public int ClearAllCalls;
        public override void ClearAll(SimWorld w, bool keepObstacles) { ClearAllCalls++; }
    }

    internal sealed class FakeSpells : SpellModule
    {
        public int ClearCalls;
        public override void ClearProjectiles(SimWorld w, int ownerSlot) { ClearCalls++; }
    }

    internal sealed class FakeTraits : TraitModule
    {
        public int WaveStartCalls;
        public override void OnWaveStart(SimWorld w) { WaveStartCalls++; }
    }

    /// <summary>RunFlow + 가짜 모듈로 세운 세계.</summary>
    internal sealed class Rig
    {
        public SimWorld W;
        public RunConfig Cfg;
        public readonly FakePlayer Player = new FakePlayer();
        public readonly FakeWaves Waves = new FakeWaves();
        public readonly FakeBosses Bosses = new FakeBosses();
        public readonly FakeScore Score = new FakeScore();
        public readonly FakeArts Arts = new FakeArts();
        public readonly FakeEnemies Enemies = new FakeEnemies();
        public readonly FakeThreats Threats = new FakeThreats();
        public readonly FakeSpells Spells = new FakeSpells();
        public readonly FakeTraits Traits = new FakeTraits();

        public PlayerState P => W.P0;
        public RunState Run => W.Run;
        public IContentDb Db => W.Content;

        public static Rig Create(Action<RunConfig> tweak = null, Action<Rig> before = null, ulong seed = 7)
        {
            var r = new Rig();
            var mods = SimModuleSet.Stubs();
            mods.Player = r.Player; mods.Waves = r.Waves; mods.Bosses = r.Bosses; mods.Score = r.Score; mods.Arts = r.Arts;
            mods.Enemies = r.Enemies; mods.Threats = r.Threats; mods.Spells = r.Spells; mods.Traits = r.Traits;
            mods.Run = new RunFlow();
            r.W = new SimWorld(TestContent.Db, mods, seed);
            r.Cfg = new RunConfig
            {
                Seed = seed, RunId = "run-test", CharId = Ids.Char.C1, WandId = 42, WandStage = 3, LinkSlotCount = 4,
                Loadout = new LoadoutSpec { Core = Ids.Block.MagicBolt, Links = { Ids.Block.SplitTwo, null, null, null } }
            };
            if (tweak != null) tweak(r.Cfg);
            if (before != null) before(r);
            r.W.StartRun(r.Cfg);
            return r;
        }

        /// <summary>웨이브 wave의 전투에서 시작한다(1이 아니면 출발 야영에서 바로 출발).</summary>
        public static Rig InCombatAt(int wave, Action<RunConfig> tweak = null, Action<Rig> before = null)
        {
            var r = Create(c => { c.StartWave = wave; if (tweak != null) tweak(c); }, before);
            if (wave > 1) r.Cmd(RunCommandKind.CampDepart);
            r.StepUntil(RunPhase.WaveCombat);
            return r;
        }

        public void Step(int n = 1) { for (int i = 0; i < n; i++) W.Step(); }

        /// <summary>이번 틱에 J를 눌렀다고 치고 한 틱 간다(가짜 Player는 입력을 읽지 않는다).</summary>
        public void StepPressJ()
        {
            P.JPressedThisTick = true;
            W.Step();
            P.JPressedThisTick = false;
        }

        public void StepUntil(RunPhase phase, int max = 2000)
        {
            for (int i = 0; i < max && Run.Phase != phase; i++) W.Step();
            Assert.AreEqual(phase, Run.Phase, "그 상태에 닿지 못했다");
        }

        public void Cmd(RunCommandKind kind, int a = 0, int b = 0, string id = null) => W.Enqueue(new RunCommand(kind, a, b, id));

        public int Count(SimEventType type)
        {
            int n = 0;
            for (int i = 0; i < W.Events.Count; i++) if (W.Events[i].Type == type) n++;
            return n;
        }

        public bool TryLast(SimEventType type, out SimEvent ev, string id = null)
        {
            for (int i = W.Events.Count - 1; i >= 0; i--)
            {
                if (W.Events[i].Type != type || (id != null && W.Events[i].Id != id)) continue;
                ev = W.Events[i];
                return true;
            }
            ev = default(SimEvent);
            return false;
        }

        public EnemyState MakeEnemy(int cost, bool elite = false, bool noReward = false, bool boss = false)
        {
            return new EnemyState
            {
                Id = W.NewEntityId(), DefId = Ids.Enemy.SlimeMoss, Def = new EnemyDef { Id = Ids.Enemy.SlimeMoss, Cost = cost }, Cost = cost,
                Elite = elite ? new EliteDef { Id = Ids.Enemy.EliteOf(Ids.Enemy.SlimeMoss) } : null, NoReward = noReward, IsBoss = boss,
                Pos = new Vector2(3f, 3f)
            };
        }

        public EnemyState Kill(int cost, bool elite = false, bool noReward = false)
        {
            var e = MakeEnemy(cost, elite, noReward);
            W.Mods.Run.OnEnemyKilled(W, e, default(HitInfo));
            return e;
        }

        /// <summary>지금 웨이브를 넘긴다(일반 = 전멸, 보스 = 사망) → WaveClearing.</summary>
        public void ClearWave()
        {
            Assert.AreEqual(RunPhase.WaveCombat, Run.Phase);
            if (W.Boss != null) W.Boss.State = BossFightState.Dead; else Waves.Cleared = true;
            W.Step();
            Assert.AreEqual(RunPhase.WaveClearing, Run.Phase);
        }

        public void ToLoot()
        {
            if (Run.Phase == RunPhase.WaveCombat) ClearWave();
            StepUntil(RunPhase.LootPick);
        }

        public void PickAndConfirm()
        {
            Assert.AreEqual(RunPhase.LootPick, Run.Phase);
            Cmd(RunCommandKind.LootPick, 0);
            Cmd(RunCommandKind.LootPick, 1);
            Cmd(RunCommandKind.LootConfirm);
            W.Step();
        }

        /// <summary>지금 제시의 앞 카드들을 정해진 블록으로 바꾼다(별 겹치기·보관함 수를 정확히 보려는 시험용). 등급은 블록의 등급.</summary>
        public void ForceCards(params string[] blockIds)
        {
            Assert.IsNotNull(Run.Loot);
            for (int i = 0; i < blockIds.Length && i < Run.Loot.Cards.Count; i++)
            {
                Run.Loot.Cards[i].BlockId = blockIds[i];
                Run.Loot.Cards[i].Rarity = Db.Block(blockIds[i]).Rarity;
            }
        }

        /// <summary>전투 → 클리어 → 전리품 2장 → 야영.</summary>
        public void ToCamp()
        {
            ToLoot();
            PickAndConfirm();
            Assert.AreEqual(RunPhase.Camp, Run.Phase);
        }

        public void Depart()
        {
            Cmd(RunCommandKind.CampDepart);
            StepUntil(RunPhase.WaveCombat);
        }
    }
}
