using System;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 보스 공통 뼈대(05 요약서 8·10절). 싸움 상태 시계(BossState.State·StateStartTick)의 주인은 이 모듈 하나다 —
    /// Run은 w.Boss.State만 읽어 RunPhase를 옮긴다(Modules.cs 공통 규칙 5, 00 문서 5.5절 표).
    /// PlayerState.InputLocked도 이 모듈만 켜고 끈다. AttackLocked는 건드리지 않는다(Run의 것).
    /// 보스마다 다른 행동은 IBossBrain(보스 id → 두뇌, BossBrainRegistry)이 맡는다.
    /// 파일: 이 파일(등장·걷기·피해 조정), BossSystem.Ai.cs(7단계), BossSystem.Runner.cs(패턴 실행기),
    /// BossSystem.Phases.cs(10단계: 단계 전환·사망·첫 만남).
    /// </summary>
    public sealed partial class BossSystem : BossModule
    {
        BossBrainTable _brains;

        /// <summary>두뇌 등록표(처음 쓸 때 BossBrainRegistry.Build). 테스트는 Add로 가짜 두뇌를 끼울 수 있다.</summary>
        public BossBrainTable Brains => _brains ?? (_brains = BossBrainRegistry.Build());

        /// <summary>지금 보스의 싸움 문맥(없으면 null).</summary>
        public static BossContext Context(SimWorld w)
        {
            return w != null && w.Boss != null ? w.Boss.Brain as BossContext : null;
        }

        public override string SelectBoss(IContentDb db, int wave, bool phantomFirst)
        {
            return BossRules.SelectBoss(db, wave, phantomFirst);
        }

        public override float MaxHp(IContentDb db, string bossId, int wave, bool firstEncounter)
        {
            return db == null ? 0f : BossRules.MaxHp(db, db.Boss(bossId), wave, firstEncounter);
        }

        /// <summary>웨이브 20 허상이고 복수 중인 판인가(복수 꼬리표·RevengeKill). 판 안의 실효 값 w.Run.RevengeActive로 본다.</summary>
        public static bool IsRevengeTarget(SimWorld w, BossDef def, int wave, bool firstEncounter, BossTuning tuning)
        {
            return !firstEncounter && def != null && def.Id == Ids.Boss.MirrorPhantom && wave == tuning.RevengeWave
                && w.Run != null && w.Run.RevengeActive;
        }

        public override EnemyState SpawnBoss(SimWorld w, WavePlan plan)
        {
            if (w == null || plan == null || w.Content == null) return null;
            var db = w.Content;
            string bossId = plan.BossId ?? BossRules.SelectBoss(db, plan.Wave, plan.PhantomFirst);
            var def = db.Boss(bossId);
            if (def == null) return null;
            if (w.Boss != null) Dismiss(w);

            bool first = plan.PhantomFirst && plan.Wave / DayCycle.DayLength == 1
                && def.Id == Ids.Boss.MirrorPhantom && def.FirstEncounter != null;
            var tuning = BossTuning.Load(db);
            float maxHp = BossRules.MaxHp(db, def, plan.Wave, first);
            int phaseCount = def.Phases != null ? def.Phases.Count : 0;
            int phase = first ? Mathf.Clamp(def.FirstEncounter.StartPhase - 1, 0, Math.Max(0, phaseCount - 1)) : 0;
            var phaseDef = phase < phaseCount ? def.Phases[phase] : null;

            var body = new EnemyState
            {
                Id = w.NewEntityId(), DefId = def.Id, Def = BodyDef(def, phaseDef), IsBoss = true, BossDef = def,
                Tags = PhaseTags(phaseDef), Weak = def.Weak, Resist = def.Resist, SpawnWave = plan.Wave,
                BodyRadius = def.BodyRadius, MoveSpeed = def.MoveSpeed, Hp = maxHp, MaxHp = maxHp,
                Ai = EnemyAiState.Act, AiStateStartTick = w.Tick, AnimStartTick = w.Tick
            };
            body.Pos = SpawnPos(w, tuning, body.BodyRadius);
            body.PrevPos = body.Pos;
            var near = w.NearestPlayer(body.Pos);
            if (near != null && (near.Pos - body.Pos).sqrMagnitude > 0f) body.Facing = (near.Pos - body.Pos).normalized;
            // 첫 만남: 체력 바닥(05 요약서 10.2절 hp_floor_percent). 피해 숫자는 계산값 그대로이고 체력만 여기서 멈춘다.
            if (first) body.HpFloor = maxHp * def.FirstEncounter.HpFloorPercent / PercentBase;

            var boss = new BossState
            {
                EnemyId = body.Id, Def = def, Wave = plan.Wave,
                Cycle = DayCycle.BossCycle(plan.Wave, Math.Max(1, db.Bosses != null ? db.Bosses.Count : 1)),
                FirstEncounter = first, State = BossFightState.Intro, StateStartTick = w.Tick, Phase = phase,
                RageStacks = first ? def.FirstEncounter.RageStacks : 0, HpMin = maxHp
            };
            var c = new BossContext
            {
                W = w, Boss = boss, Body = body, Common = db.BossCommon ?? new BossCommonDef(), Tuning = tuning,
                Brain = Brains.Create(def.Id), Sys = this, FightStartTick = w.Tick
            };
            c.Patterns = BossPatternData.Resolve(def, phase, boss.Cycle);
            boss.Brain = c;
            w.Enemies.Add(body);
            w.Boss = boss;
            c.Brain.Init(c);

            bool revengeTag = IsRevengeTarget(w, def, plan.Wave, first, tuning);
            w.Emit(SimEventType.EnemySpawned, entityId: body.Id, pos: body.Pos, id: def.Id);
            w.Emit(SimEventType.BossIntroBegin, entityId: body.Id, a: revengeTag ? 1 : 0, pos: body.Pos, id: def.Id);
            return body;
        }

        const float PercentBase = 100f;

        /// <summary>다른 모듈이 e.Def를 읽어도 되게 보스 정의에서 만든 EnemyDef(보스 몸통은 enemies.json에 없다).</summary>
        static EnemyDef BodyDef(BossDef def, BossPhaseDef phase)
        {
            var d = new EnemyDef
            {
                Id = def.Id, Role = "boss", BaseHp = def.BaseHp, MoveSpeed = def.MoveSpeed, ContactDamage = def.ContactDamage,
                Weak = def.Weak, Resist = def.Resist, KnockbackImmune = true, BodyRadius = def.BodyRadius
            };
            if (phase != null && phase.Tags != null) d.Tags.AddRange(phase.Tags);
            if (!d.Tags.Contains(Ids.Tag.Boss)) d.Tags.Add(Ids.Tag.Boss);
            return d;
        }

        static EnemyTags PhaseTags(BossPhaseDef phase)
        {
            return (phase != null ? EnemyTagsUtil.Parse(phase.Tags) : EnemyTags.None) | EnemyTags.Boss;
        }

        /// <summary>등장 자리: 전장 가로 가운데, 세로 두 후보 중 플레이어에게서 먼 쪽(솔로 임시, G07.json).</summary>
        static Vector2 SpawnPos(SimWorld w, BossTuning tuning, float radius)
        {
            var a = new Vector2(w.ArenaWidth * tuning.SpawnXRatio, w.ArenaHeight * tuning.SpawnYRatioA);
            var b = new Vector2(w.ArenaWidth * tuning.SpawnXRatio, w.ArenaHeight * tuning.SpawnYRatioB);
            var pa = w.NearestPlayer(a);
            var pb = w.NearestPlayer(b);
            float da = pa != null ? (pa.Pos - a).sqrMagnitude : float.MaxValue;
            float db = pb != null ? (pb.Pos - b).sqrMagnitude : float.MaxValue;
            return w.ClampToArena(db > da ? b : a, radius);
        }

        public override float ModifyIncomingDamage(SimWorld w, EnemyState e, float amount, bool isCrit, Vector2 fromPos, Vector2 hitPos)
        {
            var b = w.Boss;
            var c = Context(w);
            if (b == null || c == null || e == null || e.Id != b.EnemyId) return amount;
            switch (b.State)
            {
                // 등장 중에는 피해를 받지 않는다(8.2절). 되비침·쓰러짐·사망 뒤에도 없다.
                case BossFightState.Intro:
                case BossFightState.Reflection:
                case BossFightState.Collapse:
                case BossFightState.CollapseDone:
                case BossFightState.Dead:
                    return 0f;
                case BossFightState.Recovery:
                    amount *= c.RecoveryDamageRatio;
                    break;
            }
            return c.Brain.ModifyIncomingDamage(c, amount, isCrit, fromPos, hitPos);
        }

        public override void ActionHalt(SimWorld w, EnemyState e, int ticks)
        {
            var b = w.Boss;
            if (b == null || e == null || e.Id != b.EnemyId || ticks <= 0) return;
            b.ActionHaltUntilTick = Math.Max(b.ActionHaltUntilTick, w.Tick + ticks);
        }

        public override void Dismiss(SimWorld w)
        {
            var b = w.Boss;
            if (b == null) return;
            var c = b.Brain as BossContext;
            var body = w.FindEnemy(b.EnemyId);
            if (c != null)
            {
                if (body != null) c.Body = body;
                AbortRun(c);
                EndFight(c);
                // 이 모듈이 건 입력 잠금이 남아 있으면 푼다(정상 흐름에서는 CollapseDone에서 이미 풀렸다).
                if (c.InputLockApplied) SetInputLock(c, false);
            }
            if (body != null)
            {
                body.DashActive = false;
                if (!body.Dead)
                {
                    w.Mods.Threats.CancelTelegraphsOf(w, body.Id);
                    body.Dead = true;
                    body.Ai = EnemyAiState.Dead;
                }
            }
            w.Boss = null;
        }

        // ------------------------------------------------------------ 공용 도우미

        void SetState(BossContext c, BossFightState state)
        {
            var b = c.Boss;
            var old = b.State;
            b.State = state;
            b.StateStartTick = c.W.Tick;
            var body = c.Body;
            if (body != null)
            {
                body.Ai = AiOf(state);
                body.AiStateStartTick = c.W.Tick;
                body.WeaponTint = state == BossFightState.Telegraph;
            }
            if (old != state) c.Brain.OnStateChanged(c, old, state);
        }

        static EnemyAiState AiOf(BossFightState s)
        {
            switch (s)
            {
                case BossFightState.Intro: return EnemyAiState.Spawning;
                case BossFightState.R5Blocked: return EnemyAiState.Waiting;
                case BossFightState.Telegraph: return EnemyAiState.WindUp;
                case BossFightState.Execute: return EnemyAiState.Strike;
                case BossFightState.Recovery: return EnemyAiState.Recovery;
                case BossFightState.PhaseTransition: return EnemyAiState.Interrupted;
                case BossFightState.Dead: return EnemyAiState.Dead;
                default: return EnemyAiState.Act;
            }
        }

        void EnterWaiting(BossContext c)
        {
            c.Run = null;
            c.Excluded.Clear();
            c.RecoveryLeft = 0;
            c.RecoveryDamageRatio = 1f;
            c.WaitLeft = BossRules.WaitTicks(c.Common, c.Boss.RageStacks);
            SetState(c, BossFightState.Waiting);
        }

        void SetInputLock(BossContext c, bool locked)
        {
            var players = c.W.Players;
            for (int i = 0; i < players.Length; i++)
                if (players[i].Active) players[i].InputLocked = locked;
            c.InputLockApplied = locked;
        }

        void EndFight(BossContext c)
        {
            if (c.FightEnded) return;
            c.FightEnded = true;
            c.Brain.OnFightEnd(c);
        }
    }
}
