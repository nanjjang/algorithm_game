using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 데이터 구동 패턴 실행기(05 요약서 8.4절). steps는 차례로 돈다: 다음 step은 앞 step의 판정 틱에 시작한다(+ start_delay).
    /// 한 step은 반복(repeat)을 "띄우고", 띄운 반복마다 예고 → 판정 틱의 7단계에 자체 타격(공통 규칙 4) → 끝의 길을 간다.
    ///   반복 간격 0            : 앞 반복이 끝난 틱에 다음 반복(가마등 연속 돌진).
    ///   간격 > 0, 반복마다 예고 : 예고 시작 사이가 간격(밤눈 낙뢰처럼 예고가 겹친다).
    ///   간격 > 0, 예고 한 번    : 첫 타격 뒤 간격마다 타격(고리 여러 겹). 연사형이면 격노로 간격이 준다.
    /// </summary>
    public sealed partial class BossSystem
    {
        enum RunResult { Running, Finished, Repick, Aborted }

        /// <summary>한 틱에 이어서 처리할 수 있는 전이의 상한(무한 고리 막이).</summary>
        const int MaxChainPerTick = 64;

        RunResult TickRun(BossContext c, PlayerState target, bool halted)
        {
            var w = c.W;
            var run = c.Run;
            var steps = run.Pattern.Steps;
            if (halted)
            {
                // 뇌광창의 동작 멈춤: 패턴 타이머를 한 틱씩 민다. 이미 뜬 예고와 진행 중인 돌진은 그대로 간다(8.2절).
                run.StepNotBeforeTick++;
                run.NextLaunchTick++;
                for (int i = 0; i < run.Active.Count; i++)
                    if (run.Active[i].Phase == BossStepPhase.Warn) run.Active[i].WarnUntilTick++;
            }
            if (w.Tick - run.StartTick > c.Tuning.PatternStallTicks) return RunResult.Aborted;

            for (int guard = 0; guard < MaxChainPerTick; guard++)
            {
                if (run.StepIndex >= steps.Count) return RunResult.Finished;
                var step = steps[run.StepIndex];
                int total = Math.Max(1, step.Repeat);
                bool progressed = false;

                if (!halted && run.Launched < total && w.Tick >= run.StepNotBeforeTick
                    && (run.Launched == 0 || (run.GateOpen && w.Tick >= run.NextLaunchTick)))
                {
                    Launch(c, run, step);
                    progressed = true;
                }

                for (int i = 0; i < run.Active.Count; i++)
                {
                    var result = RunResult.Running;
                    if (Advance(c, run, step, run.Active[i], target, ref result)) progressed = true;
                    if (result != RunResult.Running) return result;
                    if (c.Run != run) return RunResult.Running;
                }

                for (int i = run.Active.Count - 1; i >= 0; i--)
                {
                    var inst = run.Active[i];
                    if (inst.Phase != BossStepPhase.Done) continue;
                    run.Active.RemoveAt(i);
                    // 간격 0이거나 타격 없이 끝난 반복이면 여기서 다음 반복의 문을 연다.
                    if (!run.GateOpen && (c.IntervalTicks(step) == 0 || !inst.Acted))
                    {
                        run.GateOpen = true;
                        run.NextLaunchTick = w.Tick;
                    }
                    progressed = true;
                }

                if (run.Launched >= total && run.Active.Count == 0)
                {
                    run.StepIndex++;
                    run.Launched = 0;
                    run.GateOpen = true;
                    run.StepDirSet = false;
                    if (run.StepIndex < steps.Count)
                        run.StepNotBeforeTick = w.Tick + SimConst.Ticks(steps[run.StepIndex].StartDelaySeconds);
                    progressed = true;
                }
                if (!progressed) return RunResult.Running;
            }
            return RunResult.Running;
        }

        void Launch(BossContext c, BossPatternRun run, BossStep step)
        {
            var w = c.W;
            var inst = new BossStepInstance
            {
                RepeatIndex = run.Launched, LaunchTick = w.Tick, BlockedSinceTick = w.Tick, CastId = w.NewCastId()
            };
            if (run.StepDirSet) { inst.Dir = run.StepDir; inst.DirSet = true; }
            run.Launched++;
            run.GateOpen = false;
            bool announce = inst.RepeatIndex == 0 || step.TelegraphEachRepeat;
            if (step.HasTelegraph && announce) inst.Phase = BossStepPhase.Pending;
            else
            {
                int warn = announce ? WarnTicks(step) : 0;
                if (warn > 0)
                {
                    inst.Phase = BossStepPhase.Warn;
                    inst.WarnUntilTick = w.Tick + warn;
                    run.FirstGranted = true;
                    SetState(c, BossFightState.Telegraph);
                }
                else inst.Phase = BossStepPhase.Act;
            }
            int interval = c.IntervalTicks(step);
            if (interval > 0 && step.TelegraphEachRepeat)
            {
                run.NextLaunchTick = w.Tick + interval;
                run.GateOpen = true;
            }
            run.Active.Add(inst);
        }

        /// <summary>구역 없는 예고(머리 위 화살표, 땅의 금, 시전 동작)의 길이.</summary>
        static int WarnTicks(BossStep step)
        {
            var d = step.Def;
            if (d.Copy != null && d.Copy.WarnSeconds > 0f) return SimConst.Ticks(d.Copy.WarnSeconds);
            if (d.Summon != null && d.Summon.WarnSeconds > 0f) return SimConst.Ticks(d.Summon.WarnSeconds);
            if (step.Attack != null && step.Attack.CastSeconds > 0f) return SimConst.Ticks(step.Attack.CastSeconds);
            return 0;
        }

        bool Advance(BossContext c, BossPatternRun run, BossStep step, BossStepInstance inst, PlayerState target, ref RunResult result)
        {
            var w = c.W;
            var threats = w.Mods.Threats;
            int repick = SimConst.Ticks(c.Common.R5RepickSeconds);
            switch (inst.Phase)
            {
                case BossStepPhase.Pending:
                {
                    // R5: 막히면 3틱마다 다시 본다. 구역은 그때의 플레이어 위치로 다시 짠다.
                    int retry = Math.Max(1, SimConst.Ticks(c.Common.R5RetrySeconds));
                    if (inst.LastTickProcessed == w.Tick || (w.Tick - inst.BlockedSinceTick) % retry != 0) return false;
                    inst.LastTickProcessed = w.Tick;
                    inst.Zones.Clear();
                    if (!c.Brain.BuildZones(c, step, inst, target, inst.Zones)) c.DefaultZones(step, inst, target, inst.Zones);
                    if (inst.Zones.Count == 0)
                    {
                        inst.Phase = BossStepPhase.Done;
                        return true;
                    }
                    if (!threats.CheckEscape(w, inst.Zones, c.Body.Id))
                    {
                        if (!run.FirstGranted && c.Boss.State != BossFightState.R5Blocked) SetState(c, BossFightState.R5Blocked);
                        if (w.Tick - inst.BlockedSinceTick >= repick) result = run.FirstGranted ? RunResult.Aborted : RunResult.Repick;
                        return false;
                    }
                    var req = new TelegraphRequest { OwnerEnemyId = c.Body.Id, CastId = inst.CastId, CountAsOne = true };
                    req.Zones.AddRange(inst.Zones);
                    inst.Ticket = threats.RequestTelegraph(w, req);
                    inst.RequestTick = w.Tick;
                    inst.Dir = inst.Zones[0].Dir;
                    inst.DirSet = true;
                    inst.Phase = BossStepPhase.AwaitGrant;
                    return true;
                }
                case BossStepPhase.AwaitGrant:
                {
                    int start, detonate;
                    if (threats.TryGetTiming(w, inst.Ticket, out start, out detonate))
                    {
                        inst.StartTick = start;
                        inst.DetonateTick = detonate;
                        inst.Phase = BossStepPhase.Telegraph;
                        run.FirstGranted = true;
                        RefreshDir(c, run, inst);
                        SetState(c, BossFightState.Telegraph);
                        c.Brain.OnTelegraphStart(c, step, inst);
                        return true;
                    }
                    if (threats.Status(w, inst.Ticket) == TicketStatus.Cancelled)
                    {
                        inst.Phase = BossStepPhase.Done;
                        return true;
                    }
                    // 동시 예고 상한의 대기 줄에서 너무 오래 기다리면 표를 거두고 R5 막힘과 같이 다룬다.
                    if (w.Tick - inst.RequestTick >= repick)
                    {
                        threats.CancelTicket(w, inst.Ticket);
                        result = run.FirstGranted ? RunResult.Aborted : RunResult.Repick;
                    }
                    return false;
                }
                case BossStepPhase.Telegraph:
                    // 자체 타격은 판정 틱과 같은 틱의 7단계에 시작한다(Status == Detonated를 기다리면 1틱 늦는다).
                    if (w.Tick >= inst.DetonateTick)
                    {
                        RefreshDir(c, run, inst);
                        inst.Phase = BossStepPhase.Act;
                        return true;
                    }
                    if (threats.Status(w, inst.Ticket) == TicketStatus.Cancelled)
                    {
                        inst.Phase = BossStepPhase.Done;
                        return true;
                    }
                    return false;
                case BossStepPhase.Warn:
                    if (w.Tick < inst.WarnUntilTick) return false;
                    inst.Phase = BossStepPhase.Act;
                    return true;
                case BossStepPhase.Act:
                {
                    if (c.Boss.State != BossFightState.Execute) SetState(c, BossFightState.Execute);
                    if (!inst.Acted)
                    {
                        inst.Acted = true;
                        int interval = c.IntervalTicks(step);
                        if (interval > 0 && !step.TelegraphEachRepeat)
                        {
                            run.NextLaunchTick = w.Tick + interval;
                            run.GateOpen = true;
                        }
                    }
                    inst.LastTickProcessed = w.Tick;
                    var res = c.Brain.Act(c, step, inst, target);
                    if (res == BossActResult.Default) res = DefaultAct(c, step, inst, target);
                    if (res == BossActResult.Done) inst.Phase = BossStepPhase.Done;
                    else if (inst.Phase == BossStepPhase.Act) inst.Phase = BossStepPhase.Busy;
                    return true;
                }
                case BossStepPhase.Busy:
                    if (inst.LastTickProcessed == w.Tick) return false;
                    inst.LastTickProcessed = w.Tick;
                    if (c.Brain.Act(c, step, inst, target) == BossActResult.Busy) return false;
                    if (inst.Phase == BossStepPhase.Busy) inst.Phase = BossStepPhase.Done;
                    return true;
                case BossStepPhase.Dash:
                    if (inst.LastTickProcessed == w.Tick) return false;
                    inst.LastTickProcessed = w.Tick;
                    if (!DashTick(c, step, inst)) return false;
                    inst.Phase = BossStepPhase.Done;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>표가 만든 첫 구역의 방향을 읽어 온다(Threats가 시작·고정 때 다시 잡은 방향). 구역을 못 읽으면 요청한 방향 그대로.</summary>
        void RefreshDir(BossContext c, BossPatternRun run, BossStepInstance inst)
        {
            c.ZoneEntityBuf.Clear();
            c.W.Mods.Threats.GetZones(c.W, inst.Ticket, c.ZoneEntityBuf);
            if (c.ZoneEntityBuf.Count > 0 && c.ZoneEntityBuf[0].Dir.sqrMagnitude > 0f)
            {
                inst.Dir = c.ZoneEntityBuf[0].Dir;
                inst.DirSet = true;
            }
            if (inst.DirSet)
            {
                run.StepDir = inst.Dir;
                run.StepDirSet = true;
            }
        }

        /// <summary>공격 종류(kind)별 기본 타격: 돌진 / 탄(ring_shot, spread, aimed_shot) / 구역(판정은 Threats, 장애물만 세운다) / 졸개.</summary>
        BossActResult DefaultAct(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
        {
            var a = step.Attack;
            if (a == null)
            {
                if (step.Def.Summon != null) c.Summon(step.Def.Summon, null);
                return BossActResult.Done;
            }
            switch (a.Kind)
            {
                case BossRules.KindDash:
                    StartDash(c, inst, inst.DirSet ? inst.Dir : c.DirTo(target.Pos), a.ActiveTicks, a.Damage);
                    inst.LastTickProcessed = c.W.Tick;
                    // 판정 틱부터 몸이 움직인다.
                    return DashTick(c, step, inst) ? BossActResult.Done : BossActResult.Busy;
                case BossRules.KindRingShot:
                case BossRules.KindSpread:
                case BossRules.KindAimedShot:
                {
                    Vector2 aim = a.Kind == BossRules.KindAimedShot && inst.DirSet ? inst.Dir : c.DirTo(target.Pos);
                    c.FireAttack(a, c.Body.Pos, aim, step.RepeatAngleDegrees * inst.RepeatIndex, inst.CastId);
                    return BossActResult.Done;
                }
                default:
                    SpawnObstacles(c, step, inst);
                    return BossActResult.Done;
            }
        }

        /// <summary>step.obstacle이 있으면 판정 틱에 구역 자리마다 장애물을 세운다(가마등 바위, 흰뿔 얼음 기둥). max_count를 넘는 만큼은 생략.</summary>
        public void SpawnObstacles(BossContext c, BossStep step, BossStepInstance inst)
        {
            JObject o = step.Def.Obstacle;
            if (o == null) return;
            var w = c.W;
            string kind = BossTuning.Str(o, "kind", null);
            int max = BossTuning.Int(o, "max_count", 0);
            var spec = new ObstacleSpec
            {
                Kind = kind, Radius = BossTuning.Float(o, "radius", 0f), Hp = BossTuning.Float(o, "hp", 0f),
                DurationTicks = SimConst.Ticks(BossTuning.Float(o, "duration_seconds", 0f)),
                BlocksPlayerShots = BossTuning.Bool(o, "blocks_player_shots", false),
                BlocksEnemyShots = BossTuning.Bool(o, "blocks_enemy_shots", false)
            };
            for (int i = 0; i < inst.Zones.Count; i++)
            {
                if (max > 0 && CountObstacles(w, kind) >= max) break;
                spec.Pos = inst.Zones[i].Pos;
                w.Mods.Threats.SpawnObstacle(w, spec);
            }
        }

        public static int CountObstacles(SimWorld w, string kind)
        {
            int n = 0;
            for (int i = 0; i < w.Obstacles.Count; i++)
                if (!w.Obstacles[i].Dead && w.Obstacles[i].Kind == kind) n++;
            return n;
        }

        // ------------------------------------------------------------ 돌진

        /// <summary>
        /// 돌진을 시작한다(두뇌가 직접 부를 수도 있다 — Act에서 부르고 Busy를 돌려주면 뼈대가 매 틱 몸을 옮긴다).
        /// 몸 충돌·스침이 읽는 EnemyState.Dash* 필드를 켠다. activeTicks 0 = 벽까지.
        /// </summary>
        public void StartDash(BossContext c, BossStepInstance inst, Vector2 dir, int activeTicks, int baseDamage)
        {
            var body = c.Body;
            if (dir.sqrMagnitude <= 0f) dir = body.Facing;
            dir = dir.normalized;
            body.DashActive = true;
            body.DashCastId = inst.CastId;
            body.DashBaseDamage = baseDamage;
            body.DashDir = dir;
            body.Facing = dir;
            inst.Dir = dir;
            inst.DirSet = true;
            inst.DashTicksLeft = activeTicks;
            inst.DashHitMask = 0;
            inst.DashTravelled = 0f;
            inst.ScratchPos = body.Pos;
            inst.ScratchFloat = 0f;
            inst.Phase = BossStepPhase.Dash;
        }

        /// <summary>
        /// 돌진 한 틱: 몸을 옮기고(설치 벽은 부수고 계속 — 보스), 지나간 선분에 닿은 플레이어에게 피격 후보를 올린다
        /// (Source = Dash, AttackId = 몸통 Id, 칸 = 충돌 틱의 Threats.DamageCells — D-62, 공통 규칙 6). 끝났으면 true.
        /// 동작 멈춤(뇌광창) 중에도 돌진은 계속된다.
        /// </summary>
        public bool DashTick(BossContext c, BossStep step, BossStepInstance inst)
        {
            var w = c.W;
            var body = c.Body;
            float speed = step.Attack != null ? step.Attack.DashSpeed : 0f;
            float stepLen = speed * SimConst.TickSeconds;
            Vector2 from = body.Pos;
            float toWall = BossRules.DistanceToWall(w, from, inst.Dir, body.BodyRadius);
            bool hitWall = toWall <= stepLen;
            float move = hitWall ? toWall : stepLen;
            Vector2 to = w.ClampToArena(from + inst.Dir * move, body.BodyRadius);

            int screenId;
            if (w.Mods.Arts.SegmentHitsScreen(w, from, to, out screenId)) w.Mods.Arts.OnEnemyTouchedScreen(w, screenId, body, true);

            body.Pos = to;
            inst.DashTravelled += move;

            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active || p.Downed || (inst.DashHitMask & (1 << i)) != 0) continue;
                if (BossRules.PointSegmentDistance(p.Pos, from, to) >= body.BodyRadius + c.HitRadius(p)) continue;
                var hit = new PlayerHitRequest
                {
                    AttackId = body.Id, CastId = inst.CastId, SourceEnemyId = body.Id, Source = PlayerDamageSource.Dash, FromPos = from,
                    DamageCells = w.Mods.Threats.DamageCells(w, body.DashBaseDamage, PlayerDamageSource.Dash, body)
                };
                if (w.Mods.Player.QueueHit(w, p, hit)) inst.DashHitMask |= 1 << i;
            }

            c.Brain.OnDashMove(c, step, inst, from, to);

            bool timeUp = false;
            if (inst.DashTicksLeft > 0)
            {
                inst.DashTicksLeft--;
                timeUp = inst.DashTicksLeft == 0;
            }
            if (!hitWall && !timeUp) return false;
            body.DashActive = false;
            c.Brain.OnDashEnd(c, step, inst, hitWall);
            return true;
        }
    }
}
