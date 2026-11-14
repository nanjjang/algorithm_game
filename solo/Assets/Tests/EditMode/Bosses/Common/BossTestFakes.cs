using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;

namespace Wizard.Solo.Tests.Bosses.Common
{
    /// <summary>가짜 Threats: 요청을 적어 두고 같은 틱 8단계에 허가한다(시작 틱 = 요청 틱, 판정 틱 = 시작 + 예고 틱). 판정은 하지 않는다.</summary>
    public sealed class FakeThreats : ThreatModule
    {
        public sealed class Ticket
        {
            public int Id;
            public TelegraphRequest Request;
            public int RequestTick;
            public int StartTick = -1;
            public int DetonateTick;
            public bool Cancelled;
        }

        public readonly List<Ticket> Tickets = new List<Ticket>();
        public readonly List<EnemyShotSpec> Shots = new List<EnemyShotSpec>();
        public readonly List<int> ShotTicks = new List<int>();
        public readonly List<HazardSpec> HazardSpecs = new List<HazardSpec>();
        public readonly List<ObstacleSpec> ObstacleSpecs = new List<ObstacleSpec>();
        public readonly List<int> ClearAllTicks = new List<int>();
        public readonly List<int> ClearShotsTicks = new List<int>();
        public readonly List<int> CancelledOwners = new List<int>();
        /// <summary>CheckEscape의 답(R5).</summary>
        public bool EscapeOk = true;
        /// <summary>false면 대기 줄에 세워 둔다(허가하지 않는다).</summary>
        public bool Grant = true;

        // 남의 개체 조작: 실제 ThreatSystem과 같이 상태를 바꾸고 이벤트를 낸다(주인 모듈에 맡기는 계약, 00 문서 5.7절).
        public override void RemoveHazard(SimWorld w, HazardEntity h)
        {
            if (h == null || h.Dead) return;
            h.Dead = true;
            w.Emit(SimEventType.HazardExpired, entityId: h.Id, pos: h.Pos);
        }

        public override void MoveHazard(SimWorld w, HazardEntity h, Vector2 pos)
        {
            if (h != null && !h.Dead) h.Pos = pos;
        }

        public override void DestroyObstacle(SimWorld w, ObstacleEntity o, bool broken)
        {
            if (o == null || o.Dead) return;
            o.Dead = true;
            w.Emit(SimEventType.ObstacleDestroyed, entityId: o.Id, a: broken ? 1 : 0, pos: o.Pos, id: o.Kind);
        }

        public override int RequestTelegraph(SimWorld w, TelegraphRequest req)
        {
            var t = new Ticket { Id = Tickets.Count + 1, Request = req, RequestTick = w.Tick };
            Tickets.Add(t);
            return t.Id;
        }

        Ticket Find(int ticket) => ticket >= 1 && ticket <= Tickets.Count ? Tickets[ticket - 1] : null;

        public override void Stage8_Queue(SimWorld w)
        {
            if (!Grant) return;
            for (int i = 0; i < Tickets.Count; i++)
            {
                var t = Tickets[i];
                if (t.Cancelled || t.StartTick >= 0) continue;
                t.StartTick = w.Tick;
                int n = 0;
                for (int k = 0; k < t.Request.Zones.Count; k++) n = Mathf.Max(n, t.Request.Zones[k].TelegraphTicks);
                t.DetonateTick = t.StartTick + n;
            }
        }

        public override TicketStatus Status(SimWorld w, int ticket)
        {
            var t = Find(ticket);
            if (t == null) return TicketStatus.Unknown;
            if (t.Cancelled) return TicketStatus.Cancelled;
            if (t.StartTick < 0) return TicketStatus.Waiting;
            return w.Tick > t.DetonateTick ? TicketStatus.Detonated : TicketStatus.Active;
        }

        public override bool TryGetTiming(SimWorld w, int ticket, out int startTick, out int detonateTick)
        {
            var t = Find(ticket);
            startTick = 0;
            detonateTick = 0;
            if (t == null || t.Cancelled || t.StartTick < 0) return false;
            startTick = t.StartTick;
            detonateTick = t.DetonateTick;
            return true;
        }

        public override void GetZones(SimWorld w, int ticket, List<ZoneEntity> into)
        {
            var t = Find(ticket);
            if (t == null || t.StartTick < 0) return;
            for (int k = 0; k < t.Request.Zones.Count; k++)
            {
                var z = t.Request.Zones[k];
                into.Add(new ZoneEntity { Shape = z.Shape, Pos = z.Pos, Dir = z.Dir, Radius = z.Radius, Width = z.Width, StartTick = t.StartTick, DetonateTick = t.DetonateTick });
            }
        }

        public override void CancelTicket(SimWorld w, int ticket)
        {
            var t = Find(ticket);
            if (t != null) t.Cancelled = true;
        }

        public override void CancelTelegraphsOf(SimWorld w, int enemyId)
        {
            CancelledOwners.Add(enemyId);
            for (int i = 0; i < Tickets.Count; i++)
                if (Tickets[i].Request.OwnerEnemyId == enemyId && (Tickets[i].StartTick < 0 || w.Tick < Tickets[i].DetonateTick)) Tickets[i].Cancelled = true;
        }

        public override EnemyShotEntity SpawnShot(SimWorld w, in EnemyShotSpec spec)
        {
            Shots.Add(spec);
            ShotTicks.Add(w.Tick);
            var e = new EnemyShotEntity
            {
                Id = w.NewEntityId(), CastId = spec.CastId, OwnerEnemyId = spec.OwnerEnemyId, Pos = spec.Pos, PrevPos = spec.Pos,
                Vel = spec.Dir * spec.Speed, Radius = spec.Radius, RangeLeft = spec.Range, BaseDamage = spec.BaseDamage,
                Grazeable = spec.Grazeable, BouncesLeft = spec.Bounces, SpawnTick = w.Tick
            };
            w.EnemyShots.Add(e);
            return e;
        }

        public override HazardEntity SpawnHazard(SimWorld w, in HazardSpec spec)
        {
            HazardSpecs.Add(spec);
            var h = new HazardEntity
            {
                Id = w.NewEntityId(), Kind = spec.Kind, OwnerEnemyId = spec.OwnerEnemyId, Shape = spec.Shape, Pos = spec.Pos, Dir = spec.Dir,
                Radius = spec.Radius, Width = spec.Width, Length = spec.Length, StartTick = w.Tick,
                UntilTick = spec.DurationTicks > 0 ? w.Tick + spec.DurationTicks : int.MaxValue, BaseDamage = spec.BaseDamage,
                ContactCooldownTicks = spec.ContactCooldownTicks
            };
            w.Hazards.Add(h);
            return h;
        }

        public override ObstacleEntity SpawnObstacle(SimWorld w, in ObstacleSpec spec)
        {
            ObstacleSpecs.Add(spec);
            var o = new ObstacleEntity
            {
                Id = w.NewEntityId(), Kind = spec.Kind, Pos = spec.Pos, Radius = spec.Radius, Hp = spec.Hp,
                UntilTick = spec.DurationTicks > 0 ? w.Tick + spec.DurationTicks : int.MaxValue,
                BlocksPlayerShots = spec.BlocksPlayerShots, BlocksEnemyShots = spec.BlocksEnemyShots
            };
            w.Obstacles.Add(o);
            return o;
        }

        public override int ClearShots(SimWorld w, Vector2 center, float radius)
        {
            ClearShotsTicks.Add(w.Tick);
            int n = w.EnemyShots.Count;
            for (int i = 0; i < n; i++) w.EnemyShots[i].Dead = true;
            return n;
        }

        public override void ClearAll(SimWorld w, bool keepObstacles)
        {
            ClearAllTicks.Add(w.Tick);
            for (int i = 0; i < w.EnemyShots.Count; i++) w.EnemyShots[i].Dead = true;
            for (int i = 0; i < Tickets.Count; i++)
                if (Tickets[i].StartTick < 0 || w.Tick < Tickets[i].DetonateTick) Tickets[i].Cancelled = true;
        }

        public readonly List<int> EscapeCheckTicks = new List<int>();

        public override bool CheckEscape(SimWorld w, IReadOnlyList<ZoneSpec> proposed, int ownerEnemyId)
        {
            EscapeCheckTicks.Add(w.Tick);
            return EscapeOk;
        }
    }

    /// <summary>가짜 Player: QueueHit·ForceDown이 불린 틱과 내용을 적는다.</summary>
    public sealed class FakePlayer : PlayerModule
    {
        public readonly List<PlayerHitRequest> Hits = new List<PlayerHitRequest>();
        public readonly List<int> HitTicks = new List<int>();
        public readonly List<int> ForceDownTicks = new List<int>();
        public readonly List<PlayerDamageSource> ForceDownSources = new List<PlayerDamageSource>();
        public bool QueueResult = true;

        public override bool QueueHit(SimWorld w, PlayerState p, in PlayerHitRequest hit)
        {
            Hits.Add(hit);
            HitTicks.Add(w.Tick);
            return QueueResult;
        }

        public override void ForceDown(SimWorld w, PlayerState p, PlayerDamageSource source)
        {
            ForceDownTicks.Add(w.Tick);
            ForceDownSources.Add(source);
            p.Hp = 0;
            p.Downed = true;
        }
    }

    /// <summary>가짜 Enemies: 졸개를 만들어 목록에 넣고, RemoveAll 호출을 적는다.</summary>
    public sealed class FakeEnemies : EnemyModule
    {
        public readonly List<EnemySpawnSpec> Spawned = new List<EnemySpawnSpec>();
        public readonly List<int> SpawnTicks = new List<int>();
        public readonly List<bool> RemoveAllArgs = new List<bool>();

        public override EnemyState Spawn(SimWorld w, in EnemySpawnSpec spec)
        {
            Spawned.Add(spec);
            SpawnTicks.Add(w.Tick);
            var e = new EnemyState
            {
                Id = w.NewEntityId(), DefId = spec.DefId, Def = w.Content.Enemy(spec.DefId), Pos = spec.Pos,
                NoReward = spec.NoReward, SummonerId = spec.SummonerId, Hp = 1f, MaxHp = 1f, Ai = EnemyAiState.Act
            };
            w.Enemies.Add(e);
            return e;
        }

        public override void RemoveAll(SimWorld w, bool minionsOnly)
        {
            RemoveAllArgs.Add(minionsOnly);
            for (int i = 0; i < w.Enemies.Count; i++)
                if (!minionsOnly || !w.Enemies[i].IsBoss) w.Enemies[i].Dead = true;
        }
    }

    /// <summary>한 패턴만 고르게 하는 두뇌(그 밖은 기본 행동). 움직이지 않는다.</summary>
    public sealed class ForcedPatternBrain : BossBrainBase
    {
        public string Only;
        public bool Still = true;
        public ForcedPatternBrain(string only) { Only = only; }
        public override bool Eligible(BossContext c, BossPattern pattern) => Only == null || pattern.Key == Only;
        public override void Move(BossContext c, PlayerState target)
        {
            if (!Still) base.Move(c, target);
        }
    }

    /// <summary>가짜 모듈을 끼운 세계 하나.</summary>
    public sealed class BossWorld
    {
        public SimWorld W;
        public BossSystem Sys;
        public FakeThreats Threats;
        public FakePlayer Player;
        public FakeEnemies Enemies;
        public PlayerState P => W.Players[0];
        public BossState Boss => W.Boss;
        public BossContext Ctx => BossSystem.Context(W);
        public EnemyState Body;

        public static BossWorld Create(ulong seed = 7)
        {
            var bw = new BossWorld();
            var mods = SimModuleSet.Stubs();
            mods.Bosses = bw.Sys = new BossSystem();
            mods.Threats = bw.Threats = new FakeThreats();
            mods.Player = bw.Player = new FakePlayer();
            mods.Enemies = bw.Enemies = new FakeEnemies();
            bw.W = new SimWorld(TestContent.Db, mods, seed);
            var p = bw.W.Players[0];
            p.Active = true;
            p.CharId = Ids.Char.C1;
            p.Def = TestContent.Db.Character(Ids.Char.C1);
            p.MaxHp = p.Def.MaxHpCells * SimConst.HpPerCell;
            p.Hp = p.MaxHp;
            p.Pos = new Vector2(12f, 10f);
            bw.W.Run.Phase = RunPhase.BossIntro;
            return bw;
        }

        public EnemyState Spawn(string bossId, int wave, bool phantomFirst = false)
        {
            var plan = new WavePlan { Wave = wave, BossId = bossId, PhantomFirst = phantomFirst };
            Body = Sys.SpawnBoss(W, plan);
            return Body;
        }

        public void Step(int ticks)
        {
            for (int i = 0; i < ticks; i++) W.Step();
        }

        /// <summary>w.Tick이 tick이 될 때까지 진행한다(그 틱은 아직 처리 전).</summary>
        public void StepTo(int tick)
        {
            while (W.Tick < tick) W.Step();
        }

        /// <summary>등장(72틱)을 끝내고 Run이 하듯 WaveCombat으로 옮긴다. 돌아올 때 w.Tick = 등장이 끝난 틱 + 1.</summary>
        public int FinishIntro()
        {
            int guard = 0;
            while (Boss.State == BossFightState.Intro && guard++ < 1000) W.Step();
            W.Run.Phase = RunPhase.WaveCombat;
            return W.Tick - 1;
        }

        public int CountEvents(SimEventType type)
        {
            int n = 0;
            for (int i = 0; i < W.Events.Count; i++) if (W.Events[i].Type == type) n++;
            return n;
        }

        /// <summary>그 종류의 k번째(0부터) 이벤트의 틱. 없으면 -1.</summary>
        public int TickOf(SimEventType type, int index = 0)
        {
            for (int i = 0; i < W.Events.Count; i++)
            {
                if (W.Events[i].Type != type) continue;
                if (index-- == 0) return W.Events[i].Tick;
            }
            return -1;
        }

        public SimEvent EventOf(SimEventType type, int index = 0)
        {
            for (int i = 0; i < W.Events.Count; i++)
            {
                if (W.Events[i].Type != type) continue;
                if (index-- == 0) return W.Events[i];
            }
            return default;
        }

        /// <summary>마력탄 코어만 낀 지팡이의 컴파일 결과를 손으로 만든다(Spells 구현 없이). split = 루트 분열 수.</summary>
        public static CompiledSpell Spell(IContentDb db, string blockId, int split = 1, float fanHalf = 0f, int copies = 1)
        {
            var block = db.Block(blockId);
            var spell = new CompiledSpell { IsValid = true, Core = block, FireIntervalTicks = block.IntervalTicks };
            var scope = new SpellScope { Depth = 0, SplitCount = split, FanHalfAngleDegrees = fanHalf };
            for (int i = 0; i < copies; i++) scope.Projectiles.Add(new ScopeProjectile { Block = block, Slot = i });
            spell.Scopes.Add(scope);
            return spell;
        }
    }
}
