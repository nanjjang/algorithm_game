using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Player;

namespace Wizard.Solo.Tests.Player
{
    /// <summary>주문 모듈의 가짜: 컴파일 결과를 정해 준 값으로 돌려주고 실행 요청을 적어 둔다.</summary>
    public sealed class FakeSpells : SpellModule
    {
        public int IntervalTicks;
        public int HeatPerShotHu;
        public bool Valid = true;
        public int CompileCalls;
        public SpellCompileContext LastContext;
        public readonly List<ExecuteRequest> Shots = new List<ExecuteRequest>();
        public readonly List<int> ShotTicks = new List<int>();
        public List<string> Log;

        public override CompiledSpell Compile(IContentDb db, string core, IReadOnlyList<string> links, in SpellCompileContext ctx)
        {
            CompileCalls++;
            LastContext = ctx;
            return Make(IntervalTicks, HeatPerShotHu, Valid);
        }

        public static CompiledSpell Make(int intervalTicks, int heatHu, bool valid = true)
        {
            return new CompiledSpell { IsValid = valid, FireIntervalTicks = intervalTicks, HeatPerShotHu = heatHu };
        }

        public override ExecuteResult Execute(SimWorld w, in ExecuteRequest req)
        {
            Shots.Add(req);
            ShotTicks.Add(w.Tick);
            if (Log != null) Log.Add("execute");
            return default;
        }
    }

    /// <summary>비기 모듈의 가짜: 판정기에 넘어온 것을 적고, 불가마 흡수를 흉내 낸다.</summary>
    public sealed class FakeArts : ArtModule
    {
        public readonly List<CommandResetReason> Resets = new List<CommandResetReason>();
        public readonly List<CmdDir> Dirs = new List<CmdDir>();
        public readonly List<Dir8> Physical = new List<Dir8>();
        public readonly List<long> DirTimes = new List<long>();
        public readonly List<long> SkillTimes = new List<long>();
        public readonly List<int> FiredShotIds = new List<int>();
        public int SetupCalls;
        public bool InKiln;
        public int KilnLeftHu;
        public List<string> Log = new List<string>();

        public override void SetupPlayer(SimWorld w, PlayerState p) { SetupCalls++; }
        public override void OnCommandReset(SimWorld w, PlayerState p, CommandResetReason reason) { Resets.Add(reason); Log.Add("reset:" + reason); }
        public override void OnDirectionPress(SimWorld w, PlayerState p, CmdDir dir, Dir8 physical, long timeU)
        {
            Dirs.Add(dir); Physical.Add(physical); DirTimes.Add(timeU); Log.Add("dir:" + dir);
        }
        public override void OnSkillPress(SimWorld w, PlayerState p, long timeU) { SkillTimes.Add(timeU); Log.Add("k"); }
        public override void OnPlayerFired(SimWorld w, PlayerState p, int shotId) { FiredShotIds.Add(shotId); Log.Add("arts.fired"); }
        public override int AbsorbKilnHeat(SimWorld w, PlayerState p, int heatHu)
        {
            Log.Add("absorb");
            if (!InKiln || KilnLeftHu <= 0) return 0;
            int a = Math.Min(KilnLeftHu, heatHu);
            KilnLeftHu -= a;
            p.KilnAbsorbLeftHu = KilnLeftHu;
            return a;
        }
    }

    /// <summary>특성 모듈의 가짜: 훅이 불린 횟수를 세고, 지팡이 특성·변이 값을 덧붙인다.</summary>
    public sealed class FakeTraits : TraitModule
    {
        public int RollCalls, FiredCalls, PlayerHitCalls, GrazeCalls, CompileContextCalls;
        public bool NextUnstable;
        public readonly List<bool> LockEntered = new List<bool>();
        public readonly List<int> FiredIntervals = new List<int>();
        public bool Credit, KeepCrystal, Mirror;
        public int MaxDebt;
        public List<string> Log;

        public override PlayerDerived ComputeDerived(IContentDb db, CharacterDef ch, int effectiveStage, IReadOnlyList<string> mutations)
        {
            PlayerDerived d = base.ComputeDerived(db, ch, effectiveStage, mutations);
            d.HasCredit = Credit;
            d.MaxDebt = MaxDebt;
            d.KeepCrystal = KeepCrystal;
            d.MirrorImage = Mirror;
            return d;
        }
        public override SpellCompileContext CompileContext(in PlayerDerived derived, int linkSlotCount)
        {
            CompileContextCalls++;
            return base.CompileContext(derived, linkSlotCount);
        }
        public override bool RollUnstableCore(SimWorld w, PlayerState p)
        {
            RollCalls++;
            if (Log != null) Log.Add("roll");
            return NextUnstable;
        }
        public override void OnPlayerFired(SimWorld w, PlayerState p, int shotId, int intervalTicks)
        {
            FiredCalls++; FiredIntervals.Add(intervalTicks);
            if (Log != null) Log.Add("traits.fired");
        }
        public override void OnHeatLockEntered(SimWorld w, PlayerState p, bool fromUnstableCore)
        {
            LockEntered.Add(fromUnstableCore);
            if (Log != null) Log.Add("lock");
        }
        public override void OnPlayerHit(SimWorld w, PlayerState p) { PlayerHitCalls++; }
        public override void OnGrazeAwarded(SimWorld w, PlayerState p, int castId) { GrazeCalls++; }
    }

    /// <summary>위협 모듈의 가짜: 9단계에 피격 후보를 올리고, 스침 후보를 그대로 넘긴다.</summary>
    public sealed class FakeThreats : ThreatModule
    {
        /// <summary>이번 틱(와 그 뒤)의 스침 후보. OneShot이면 한 번 넘기고 비운다.</summary>
        public readonly List<GrazeCandidate> Candidates = new List<GrazeCandidate>();
        public bool OneShot = true;
        /// <summary>9단계(적 탄 ↔ 플레이어)에 부를 것.</summary>
        public Action<SimWorld> OnStage9;
        public Func<Vector2, Vector2, bool> Blocks;
        public ObstacleEntity Obstacle;

        public override void Stage9_Shots(SimWorld w) { if (OnStage9 != null) OnStage9(w); }
        public override void CollectGraze(SimWorld w, PlayerState p, List<GrazeCandidate> into)
        {
            into.AddRange(Candidates);
            if (OneShot) Candidates.Clear();
        }
        public override bool SegmentHitsObstacle(SimWorld w, Vector2 a, Vector2 b, bool forPlayerShot, out ObstacleEntity hit)
        {
            hit = null;
            if (Blocks == null || !Blocks(a, b)) return false;
            hit = Obstacle;
            return true;
        }
    }

    public sealed class FakeRun : RunModule
    {
        public int DownedCalls, HitCalls, GrazeCalls;
        public bool EndRunOnDowned;
        public override void OnPlayerDowned(SimWorld w, PlayerState p)
        {
            DownedCalls++;
            if (EndRunOnDowned) w.Run.Phase = RunPhase.RunEnded;
        }
        public override void OnPlayerHit(SimWorld w, PlayerState p) { HitCalls++; }
        public override void OnGraze(SimWorld w, PlayerState p) { GrazeCalls++; }
    }

    /// <summary>플레이어 모듈만 진짜로 끼운 세계 한 벌.</summary>
    public sealed class Kit
    {
        // 02 요약서 6.2절 표: 마력탄 간격 9틱, 열 4(480 HU), 기본 피해 10(8.4절 "마력탄 +0 = 10").
        public const int BoltIntervalTicks = 9;
        public const int BoltHeatHu = 480;
        public const int BoltDamage = 10;

        public SimWorld W;
        public PlayerState P;
        public PlayerSystem Sys;
        public FakeSpells Spells = new FakeSpells { IntervalTicks = BoltIntervalTicks, HeatPerShotHu = BoltHeatHu };
        public FakeArts Arts = new FakeArts();
        public FakeTraits Traits = new FakeTraits();
        public FakeThreats Threats = new FakeThreats();
        public FakeRun Run = new FakeRun();
        public RunConfig Config;

        /// <summary>charId = null이면 기준값 캐릭터(characters.json baseline).</summary>
        public static Kit Create(string charId = null, Action<Kit> before = null)
        {
            var k = new Kit();
            if (before != null) before(k);
            var mods = SimModuleSet.Stubs();
            k.Sys = new PlayerSystem();
            mods.Player = k.Sys;
            mods.Spells = k.Spells;
            mods.Arts = k.Arts;
            mods.Traits = k.Traits;
            mods.Threats = k.Threats;
            mods.Run = k.Run;
            k.W = new SimWorld(TestContent.Db, mods, 1);
            k.W.Run.Phase = RunPhase.WaveCombat;
            k.Config = new RunConfig
            {
                CharId = charId,
                WandStage = 0,
                LinkSlotCount = 4,
                Loadout = new LoadoutSpec { Core = Ids.Block.MagicBolt, Links = new List<string> { null, null, null, null } }
            };
            k.W.Run.Config = k.Config;
            k.P = k.W.Players[0];
            k.Sys.SetupPlayer(k.W, k.P, k.Config);
            k.W.Events.Clear();
            k.Arts.Log.Clear();
            return k;
        }

        public static long U(int tick, int offset = 0) => SimConst.TickStartU(tick) + offset;

        /// <summary>지금 틱의 offset 시각에 키를 누른다.</summary>
        public void Press(LogicalKey key, int offset = 0) => W.EnqueueInput(0, new InputEvent(key, true, U(W.Tick, offset)));
        public void Release(LogicalKey key, int offset = 0) => W.EnqueueInput(0, new InputEvent(key, false, U(W.Tick, offset)));
        public void PressAt(LogicalKey key, long timeU) => W.EnqueueInput(0, new InputEvent(key, true, timeU));
        public void ReleaseAt(LogicalKey key, long timeU) => W.EnqueueInput(0, new InputEvent(key, false, timeU));

        public void Step(int n = 1) { for (int i = 0; i < n; i++) W.Step(); }
        /// <summary>w.Tick이 tick이 될 때까지 돌린다(tick 자신은 아직 처리 전).</summary>
        public void StepTo(int tick) { while (W.Tick < tick) W.Step(); }

        public int Count(SimEventType type)
        {
            int n = 0;
            for (int i = 0; i < W.Events.Count; i++) if (W.Events[i].Type == type) n++;
            return n;
        }

        public bool TryLast(SimEventType type, out SimEvent e)
        {
            for (int i = W.Events.Count - 1; i >= 0; i--)
                if (W.Events[i].Type == type) { e = W.Events[i]; return true; }
            e = default;
            return false;
        }

        /// <summary>9단계에 피격 후보 하나를 올리게 예약한다(그 틱에만). 돌려준 값은 results에 쌓인다.</summary>
        public void HitOnTick(int tick, int attackId, int cells, List<bool> results = null)
        {
            Action<SimWorld> prev = Threats.OnStage9;
            Threats.OnStage9 = w =>
            {
                if (prev != null) prev(w);
                if (w.Tick != tick) return;
                bool r = w.Mods.Player.QueueHit(w, P, new PlayerHitRequest
                {
                    AttackId = attackId, DamageCells = cells, Source = PlayerDamageSource.Projectile, FromPos = P.Pos
                });
                if (results != null) results.Add(r);
            };
        }

        public static GrazeCandidate Bullet(int castId, float surfaceDistance, int threatId = 1)
        {
            return new GrazeCandidate { CastId = castId, Kind = GrazeKind.Bullet, SurfaceDistance = surfaceDistance, ThreatId = threatId };
        }

        public static HitInfo FireHit(int shotId, bool crit = false, int slot = 0)
        {
            return new HitInfo { AttackerSlot = slot, EnemyId = 1, Origin = DamageOrigin.Projectile, ShotSource = ShotSource.Fire, ShotId = shotId, IsCrit = crit };
        }
    }
}
