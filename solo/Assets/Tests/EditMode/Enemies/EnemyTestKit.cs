using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Enemies;

namespace Wizard.Solo.Tests.Enemies
{
    // 적 AI 테스트용 가짜 모듈들. 다른 모듈은 기본 클래스를 상속해 호출만 적는다(00 문서 12절).

    public sealed class FakeTicket
    {
        public int Id;
        public TelegraphRequest Req;
        public int RequestTick;
        public bool Granted;
        public int Start, Det;
        public bool Cancelled;
        public readonly List<ZoneEntity> Zones = new List<ZoneEntity>();
    }

    public struct ShotRecord
    {
        public int Tick;
        public EnemyShotSpec Spec;
    }

    public struct DamageCellsCall
    {
        public int Tick;
        public int Base;
        public PlayerDamageSource Source;
        public int OwnerId;
    }

    /// <summary>예고 표를 계약대로 흉내 낸다: 7단계 요청 → 같은 틱 8단계 허가(s = 그 틱) → 판정 틱 = s + N.</summary>
    public sealed class FakeThreats : ThreatModule
    {
        public readonly List<FakeTicket> Tickets = new List<FakeTicket>();
        public readonly List<ShotRecord> Shots = new List<ShotRecord>();
        public readonly List<DamageCellsCall> DamageCalls = new List<DamageCellsCall>();
        public readonly List<int> CancelOwnerCalls = new List<int>();
        public readonly List<int> CancelTicketCalls = new List<int>();
        public List<string> Log;
        /// <summary>이 틱 전에는 허가하지 않는다(대기 줄 흉내).</summary>
        public int BlockUntilTick;
        public bool BlockAll;
        /// <summary>false면 GetZones가 빈 목록을 돌려준다(주인의 자체 계산을 본다).</summary>
        public bool ProvideZones = true;
        public int DamageBonus;
        public Func<Vector2, Vector2, bool> ObstacleHit;
        public Action<SimWorld, int> OnCancelOwner;
        int _next = 1;

        public override int RequestTelegraph(SimWorld w, TelegraphRequest req)
        {
            var t = new FakeTicket { Id = _next++, Req = req, RequestTick = w.Tick };
            Tickets.Add(t);
            return t.Id;
        }

        FakeTicket Find(int id)
        {
            for (int i = 0; i < Tickets.Count; i++) if (Tickets[i].Id == id) return Tickets[i];
            return null;
        }

        public override void Stage8_Queue(SimWorld w)
        {
            for (int i = 0; i < Tickets.Count; i++)
            {
                var t = Tickets[i];
                if (t.Granted || t.Cancelled) continue;
                if (BlockAll || w.Tick < BlockUntilTick) continue;
                t.Granted = true;
                t.Start = w.Tick;
                int n = 0;
                EnemyState owner = w.FindEnemy(t.Req.OwnerEnemyId);
                for (int z = 0; z < t.Req.Zones.Count; z++)
                {
                    ZoneSpec s = t.Req.Zones[z];
                    if (s.TelegraphTicks > n) n = s.TelegraphTicks;
                    PlayerState p = w.Players[s.TargetSlot];
                    Vector2 pos = s.Anchor == ZoneAnchor.Fixed ? s.Pos
                        : s.Anchor == ZoneAnchor.TargetPlayer ? p.Pos + s.Pos
                        : (owner != null ? owner.Pos : Vector2.zero) + s.Pos;
                    Vector2 dir = s.Dir;
                    if (s.AimAtPlayer && (p.Pos - pos).magnitude > 1e-4f) dir = (p.Pos - pos).normalized;
                    t.Zones.Add(new ZoneEntity
                    {
                        Id = w.NewEntityId(), CastId = t.Req.CastId, OwnerEnemyId = t.Req.OwnerEnemyId, Shape = s.Shape, Pos = pos, Dir = dir,
                        Radius = s.Radius, Width = s.Width, Length = s.Length, StartTick = w.Tick, DetonateTick = w.Tick + s.TelegraphTicks,
                        BaseDamage = s.BaseDamage, DamageSource = s.DamageSource, LeavesHazard = s.LeavesHazard
                    });
                }
                t.Det = t.Start + n;
            }
        }

        public override TicketStatus Status(SimWorld w, int ticket)
        {
            var t = Find(ticket);
            if (t == null) return TicketStatus.Unknown;
            if (t.Cancelled) return TicketStatus.Cancelled;
            if (!t.Granted) return TicketStatus.Waiting;
            return w.Tick > t.Det ? TicketStatus.Detonated : TicketStatus.Active;
        }

        public override bool TryGetTiming(SimWorld w, int ticket, out int startTick, out int detonateTick)
        {
            var t = Find(ticket);
            startTick = 0; detonateTick = 0;
            if (t == null || !t.Granted || t.Cancelled) return false;
            startTick = t.Start; detonateTick = t.Det;
            return true;
        }

        public override void GetZones(SimWorld w, int ticket, List<ZoneEntity> into)
        {
            var t = Find(ticket);
            if (t != null && ProvideZones) into.AddRange(t.Zones);
        }

        public override void CancelTicket(SimWorld w, int ticket)
        {
            CancelTicketCalls.Add(ticket);
            var t = Find(ticket);
            if (t != null && !(t.Granted && w.Tick > t.Det)) t.Cancelled = true;
        }

        public override void CancelTelegraphsOf(SimWorld w, int enemyId)
        {
            CancelOwnerCalls.Add(enemyId);
            if (Log != null) Log.Add("threats.cancel:" + enemyId);
            if (OnCancelOwner != null) OnCancelOwner(w, enemyId);
            for (int i = 0; i < Tickets.Count; i++)
            {
                var t = Tickets[i];
                if (t.Req.OwnerEnemyId != enemyId) continue;
                if (t.Granted && w.Tick > t.Det) continue;
                t.Cancelled = true;
            }
        }

        public override EnemyShotEntity SpawnShot(SimWorld w, in EnemyShotSpec spec)
        {
            Shots.Add(new ShotRecord { Tick = w.Tick, Spec = spec });
            return new EnemyShotEntity { Id = w.NewEntityId(), OwnerEnemyId = spec.OwnerEnemyId, CastId = spec.CastId, Pos = spec.Pos };
        }

        public override int DamageCells(SimWorld w, int baseDamage, PlayerDamageSource source, EnemyState owner)
        {
            DamageCalls.Add(new DamageCellsCall { Tick = w.Tick, Base = baseDamage, Source = source, OwnerId = owner != null ? owner.Id : 0 });
            return baseDamage + DamageBonus;
        }

        public override bool SegmentHitsObstacle(SimWorld w, Vector2 a, Vector2 b, bool forPlayerShot, out ObstacleEntity hit)
        {
            hit = null;
            return ObstacleHit != null && ObstacleHit(a, b);
        }

        public int CountOf(int ownerId)
        {
            int n = 0;
            for (int i = 0; i < Tickets.Count; i++) if (Tickets[i].Req.OwnerEnemyId == ownerId) n++;
            return n;
        }

        public FakeTicket LastOf(int ownerId)
        {
            for (int i = Tickets.Count - 1; i >= 0; i--) if (Tickets[i].Req.OwnerEnemyId == ownerId) return Tickets[i];
            return null;
        }

        public List<ShotRecord> ShotsAt(int tick)
        {
            var r = new List<ShotRecord>();
            for (int i = 0; i < Shots.Count; i++) if (Shots[i].Tick == tick) r.Add(Shots[i]);
            return r;
        }
    }

    public struct HitRecord
    {
        public int Tick;
        public int Slot;
        public bool Accepted;
        public PlayerHitRequest Hit;
    }

    public sealed class FakePlayer : PlayerModule
    {
        /// <summary>false = 무적 흉내(QueueHit이 false를 돌려준다).</summary>
        public bool Accept = true;
        public readonly List<HitRecord> Hits = new List<HitRecord>();

        public override bool QueueHit(SimWorld w, PlayerState p, in PlayerHitRequest hit)
        {
            Hits.Add(new HitRecord { Tick = w.Tick, Slot = p.Slot, Accepted = Accept, Hit = hit });
            return Accept;
        }

        public List<HitRecord> AcceptedHits()
        {
            var r = new List<HitRecord>();
            for (int i = 0; i < Hits.Count; i++) if (Hits[i].Accepted) r.Add(Hits[i]);
            return r;
        }
    }

    public struct DirectRecord
    {
        public int Tick;
        public DirectDamage Damage;
    }

    public sealed class FakeSpells : SpellModule
    {
        public readonly List<DirectRecord> Direct = new List<DirectRecord>();
        /// <summary>true면 체력을 깎고 0 이하에서 PendingDeath를 세운다(연쇄 유폭 확인용).</summary>
        public bool Apply;

        public override DamageResult DealDirect(SimWorld w, in DirectDamage dmg)
        {
            Direct.Add(new DirectRecord { Tick = w.Tick, Damage = dmg });
            var e = w.FindEnemy(dmg.TargetEnemyId);
            if (!Apply || e == null || !e.Targetable) return default(DamageResult);
            e.Hp -= dmg.Amount;
            if (e.Hp <= 0f)
            {
                e.PendingDeath = true;
                e.KillingHit = new HitInfo { AttackerSlot = dmg.AttackerSlot, EnemyId = e.Id, Origin = dmg.Origin, Killed = true };
            }
            return new DamageResult { Applied = dmg.Amount, Killed = e.PendingDeath };
        }

        public List<DirectRecord> At(int tick)
        {
            var r = new List<DirectRecord>();
            for (int i = 0; i < Direct.Count; i++) if (Direct[i].Tick == tick) r.Add(Direct[i]);
            return r;
        }
    }

    public sealed class FakeWaves : WaveModule
    {
        public Func<int, float> Mul = wv => 1f;
        public readonly List<int> HpMulArgs = new List<int>();
        public List<string> Log;
        public override float HpMul(int wave) { HpMulArgs.Add(wave); return Mul(wave); }
        /// <summary>훅이 불린 순간 이미 나와 있던 EnemyKilled 이벤트 수(이벤트는 훅 뒤에 나와야 한다).</summary>
        public int KilledEventsSeenAtHook = -1;
        public bool DeadAtHook;
        public override void OnEnemyKilled(SimWorld w, EnemyState e)
        {
            if (Log != null) Log.Add("waves.killed:" + e.Id);
            int n = 0;
            for (int i = 0; i < w.Events.Count; i++) if (w.Events[i].Type == SimEventType.EnemyKilled && w.Events[i].EntityId == e.Id) n++;
            KilledEventsSeenAtHook = n;
            DeadAtHook = e.Dead;
        }
    }

    public sealed class FakeRun : RunModule
    {
        public List<string> Log;
        public override void OnEnemyKilled(SimWorld w, EnemyState e, in HitInfo killingHit) { if (Log != null) Log.Add("run.killed:" + e.Id); }
    }

    public sealed class FakeTraits : TraitModule
    {
        public List<string> Log;
        public override void OnEnemyKilled(SimWorld w, EnemyState e, in HitInfo killingHit) { if (Log != null) Log.Add("traits.killed:" + e.Id); }
    }

    public struct TouchRecord
    {
        public int Tick;
        public int EnemyId;
        public bool Dash;
    }

    public sealed class FakeArts : ArtModule
    {
        public Func<Vector2, Vector2, bool> Hits;
        public bool Breaks;
        public readonly List<TouchRecord> Touched = new List<TouchRecord>();

        public override bool SegmentHitsScreen(SimWorld w, Vector2 a, Vector2 b, out int zoneId)
        {
            zoneId = 1;
            return Hits != null && Hits(a, b);
        }

        public override bool OnEnemyTouchedScreen(SimWorld w, int zoneId, EnemyState e, bool dash)
        {
            Touched.Add(new TouchRecord { Tick = w.Tick, EnemyId = e.Id, Dash = dash });
            if (Breaks) Hits = null;
            return Breaks;
        }
    }

    public struct HaltRecord
    {
        public int EnemyId;
        public int Ticks;
    }

    public sealed class FakeBosses : BossModule
    {
        public readonly List<HaltRecord> Halts = new List<HaltRecord>();
        public float DamageMul = 1f;
        public int ModifyCalls;
        public override void ActionHalt(SimWorld w, EnemyState e, int ticks) { Halts.Add(new HaltRecord { EnemyId = e.Id, Ticks = ticks }); }
        public override float ModifyIncomingDamage(SimWorld w, EnemyState e, float amount, bool isCrit, Vector2 fromPos, Vector2 hitPos)
        {
            ModifyCalls++;
            return amount * DamageMul;
        }
    }

    /// <summary>적 모듈만 진짜인 세계 하나. 플레이어 0번은 (12, 7)에 서 있고 단계는 WaveCombat이다.</summary>
    public sealed class Kit
    {
        public readonly SimWorld W;
        public readonly EnemySystem Sys = new EnemySystem();
        public readonly FakeThreats Threats = new FakeThreats();
        public readonly FakePlayer Player = new FakePlayer();
        public readonly FakeSpells Spells = new FakeSpells();
        public readonly FakeWaves Waves = new FakeWaves();
        public readonly FakeRun Run = new FakeRun();
        public readonly FakeTraits Traits = new FakeTraits();
        public readonly FakeArts Arts = new FakeArts();
        public readonly FakeBosses Bosses = new FakeBosses();
        public readonly List<string> Log = new List<string>();

        public Kit(ulong seed = 1)
        {
            var mods = SimModuleSet.Stubs();
            mods.Enemies = Sys;
            mods.Threats = Threats;
            mods.Player = Player;
            mods.Spells = Spells;
            mods.Waves = Waves;
            mods.Run = Run;
            mods.Traits = Traits;
            mods.Arts = Arts;
            mods.Bosses = Bosses;
            W = new SimWorld(TestContent.Db, mods, seed);
            W.Run.Phase = RunPhase.WaveCombat;
            W.Wave.Wave = 1;
            P.Active = true;
            P.Def = TestContent.Db.BaselineCharacter;
            P.Pos = new Vector2(12f, 7f);
        }

        public PlayerState P => W.Players[0];

        public void LogHooks()
        {
            Threats.Log = Log; Waves.Log = Log; Run.Log = Log; Traits.Log = Log;
        }

        public EnemyState Spawn(string id, float x, float y, bool elite = false, bool instant = true)
        {
            return Sys.Spawn(W, new EnemySpawnSpec { DefId = id, Elite = elite, Pos = new Vector2(x, y), Instant = instant });
        }

        public static EnemyBrain Brain(EnemyState e) => (EnemyBrain)e.Brain;

        /// <summary>틱 tick까지(그 틱 포함) 돌린다. 끝나면 w.Tick == tick + 1.</summary>
        public void RunThrough(int tick)
        {
            while (W.Tick <= tick) W.Step();
        }

        /// <summary>틱 tick 직전까지 돌린다(다음에 처리할 틱이 tick).</summary>
        public void RunBefore(int tick)
        {
            while (W.Tick < tick) W.Step();
        }

        /// <summary>보스 몸통 흉내(Bosses가 직접 만든 것처럼 w.Enemies에 넣는다).</summary>
        public EnemyState AddBossBody(float x, float y)
        {
            var e = new EnemyState
            {
                Id = W.NewEntityId(), DefId = Ids.Boss.KilnBoar, IsBoss = true, Tags = EnemyTags.Boss, Pos = new Vector2(x, y),
                BodyRadius = 1f, Hp = 1000f, MaxHp = 1000f, Ai = EnemyAiState.Act
            };
            W.Enemies.Add(e);
            return e;
        }

        public int CountEvents(SimEventType type, int entityId = 0)
        {
            int n = 0;
            for (int i = 0; i < W.Events.Count; i++)
                if (W.Events[i].Type == type && (entityId == 0 || W.Events[i].EntityId == entityId)) n++;
            return n;
        }

        public bool TryFindEvent(SimEventType type, int entityId, out SimEvent ev)
        {
            for (int i = 0; i < W.Events.Count; i++)
                if (W.Events[i].Type == type && (entityId == 0 || W.Events[i].EntityId == entityId)) { ev = W.Events[i]; return true; }
            ev = default(SimEvent);
            return false;
        }

        public static void AreClose(Vector2 expected, Vector2 actual, float tol = 1e-3f)
        {
            NUnit.Framework.Assert.AreEqual(expected.x, actual.x, tol, "x");
            NUnit.Framework.Assert.AreEqual(expected.y, actual.y, tol, "y");
        }

        /// <summary>화면 각도(도, 반시계 +, 0~360).</summary>
        public static float Angle(Vector2 simDir) => Dir8Util.SimVectorToDegrees(simDir);

        public static float AngleDelta(float a, float b)
        {
            float d = Mathf.Abs(a - b) % 360f;
            return d > 180f ? 360f - d : d;
        }
    }
}
