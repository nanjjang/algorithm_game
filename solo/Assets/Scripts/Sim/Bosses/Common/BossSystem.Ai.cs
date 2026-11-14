using System;
using UnityEngine;

namespace Wizard.Solo.Sim.Bosses
{
    public sealed partial class BossSystem
    {
        /// <summary>
        /// 7단계. 싸움 상태 시계는 RunPhase·w.CombatActive와 무관하게 센다(Intro 72틱, PhaseTransition 36틱, Reflection 180틱, Collapse 240틱).
        /// 이동·패턴 고르기·실행·접촉은 w.CombatActive일 때만 한다. 대상 플레이어가 없으면 그 틱은 가만히 있는다(공통 규칙 3).
        /// </summary>
        public override void Stage7_Ai(SimWorld w)
        {
            var b = w.Boss;
            var c = Context(w);
            if (b == null || c == null) return;
            var body = w.FindEnemy(b.EnemyId);
            if (body != null) c.Body = body;
            body = c.Body;
            if (body != null) body.PrevPos = body.Pos;

            int elapsed = w.Tick - b.StateStartTick;
            switch (b.State)
            {
                case BossFightState.Intro:
                    if (elapsed >= SimConst.Ticks(c.Common.IntroSeconds))
                    {
                        // 격노·최종 격노 시계는 등장이 끝난 틱부터 센다(8.2절).
                        c.FightStartTick = w.Tick;
                        b.FightTicks = 0;
                        EnterWaiting(c);
                        w.Emit(SimEventType.BossIntroEnd, entityId: b.EnemyId);
                    }
                    return;
                case BossFightState.Reflection:
                    TickReflection(c, elapsed);
                    return;
                case BossFightState.Collapse:
                    TickCollapse(c, elapsed);
                    return;
                case BossFightState.CollapseDone:
                case BossFightState.Dead:
                    return;
            }

            b.FightTicks = w.Tick - c.FightStartTick;
            UpdateRage(c);

            if (b.State == BossFightState.PhaseTransition)
            {
                if (elapsed >= SimConst.Ticks(c.Common.PhaseTransitionSeconds)) EnterWaiting(c);
                return;
            }

            if (!w.CombatActive) return;
            if (body == null || !body.Alive) return;
            var target = w.NearestPlayer(body.Pos);
            if (target == null) return;

            bool halted = c.Halted;
            if (!halted && !body.DashActive) c.Brain.Move(c, target);
            c.Brain.Always(c, target, halted);
            TickFight(c, target, halted);
            Contact(c);
        }

        void UpdateRage(BossContext c)
        {
            var b = c.Boss;
            // 첫 만남은 처음부터 최대 스택이고 최종 격노가 없다(10.2절).
            if (b.FirstEncounter) return;
            int stacks = BossRules.RageStacks(c.Common, b.FightTicks);
            if (stacks > b.RageStacks)
            {
                b.RageStacks = stacks;
                c.W.Emit(SimEventType.BossRageStack, entityId: b.EnemyId, a: stacks);
            }
            if (!b.FinalRage && BossRules.FinalRage(c.Common, b.FightTicks))
            {
                b.FinalRage = true;
                c.W.Emit(SimEventType.BossFinalRage, entityId: b.EnemyId);
            }
        }

        void TickFight(BossContext c, PlayerState target, bool halted)
        {
            var b = c.Boss;
            switch (b.State)
            {
                case BossFightState.Waiting:
                    if (c.Run == null)
                    {
                        if (halted) return;
                        if (c.WaitLeft > 0) c.WaitLeft--;
                        if (c.WaitLeft > 0) return;
                        if (!StartPattern(c))
                        {
                            // 고를 패턴이 없다 → 30틱 더 기다린다(8.3절).
                            c.WaitLeft = SimConst.Ticks(c.Common.NoCandidateWaitSeconds);
                            return;
                        }
                    }
                    RunPattern(c, target, halted);
                    break;
                case BossFightState.R5Blocked:
                case BossFightState.Telegraph:
                case BossFightState.Execute:
                    if (c.Run == null) { EnterWaiting(c); return; }
                    RunPattern(c, target, halted);
                    break;
                case BossFightState.Recovery:
                    if (halted) return;
                    if (c.RecoveryLeft > 0) c.RecoveryLeft--;
                    if (c.RecoveryLeft <= 0) EnterWaiting(c);
                    break;
            }
        }

        /// <summary>패턴을 골라 시작한다. 고를 것이 없으면 false.</summary>
        bool StartPattern(BossContext c)
        {
            var b = c.Boss;
            var phase = c.PhaseDef;
            if (phase == null) return false;
            BossRules.Candidates(phase.Patterns, b.LastPattern, key =>
            {
                if (c.Excluded.Contains(key)) return false;
                var p = c.Pattern(key);
                return p != null && !p.Def.Passive && c.Brain.Eligible(c, p);
            }, c.CandidateBuf);
            if (c.CandidateBuf.Count == 0) return false;
            // 균등 난수(8.3절 [임시 U9]). 보스별 강제 규칙은 두뇌가 바꾼다.
            string picked = c.CandidateBuf[c.Rng.Range(0, c.CandidateBuf.Count)];
            string chosen = c.Brain.Choose(c, c.CandidateBuf, picked);
            var pattern = c.Pattern(chosen) ?? c.Pattern(picked);
            if (pattern == null) return false;

            c.Run = new BossPatternRun { Pattern = pattern, StartTick = c.W.Tick, StepNotBeforeTick = c.W.Tick };
            b.CurrentPattern = pattern.Key;
            c.PatternEventA = 0;
            c.PatternEventB = 0;
            c.Brain.OnPatternStart(c, pattern);
            c.W.Emit(SimEventType.BossPatternStarted, entityId: b.EnemyId, a: c.PatternEventA, b: c.PatternEventB, id: pattern.Key);
            return true;
        }

        void RunPattern(BossContext c, PlayerState target, bool halted)
        {
            var b = c.Boss;
            var run = c.Run;
            switch (TickRun(c, target, halted))
            {
                case RunResult.Finished:
                    FinishPattern(c, run);
                    break;
                case RunResult.Aborted:
                    AbortRun(c);
                    EnterWaiting(c);
                    break;
                case RunResult.Repick:
                {
                    // 첫 예고가 R5로 120틱 넘게 막혔다 → 같은 단계의 다른 패턴으로 다시 고른다(8.3절).
                    string key = run.Pattern.Key;
                    string last = b.LastPattern;
                    AbortRun(c);
                    b.LastPattern = last;
                    if (!c.Excluded.Contains(key)) c.Excluded.Add(key);
                    if (StartPattern(c))
                    {
                        if (b.State != BossFightState.Waiting) SetState(c, BossFightState.Waiting);
                    }
                    else
                    {
                        EnterWaiting(c);
                        c.WaitLeft = SimConst.Ticks(c.Common.NoCandidateWaitSeconds);
                    }
                    break;
                }
            }
        }

        void FinishPattern(BossContext c, BossPatternRun run)
        {
            var b = c.Boss;
            var pattern = run.Pattern;
            c.Run = null;
            b.LastPattern = pattern.Key;
            b.CurrentPattern = null;
            if (c.Body != null) c.Body.DashActive = false;
            c.Brain.OnPatternEnd(c, pattern, false);
            int recovery = pattern.RecoveryTicks;
            if (recovery > 0)
            {
                c.Excluded.Clear();
                c.RecoveryLeft = recovery;
                c.RecoveryDamageRatio = pattern.Def.RecoveryDamageRatio > 0f ? pattern.Def.RecoveryDamageRatio : 1f;
                SetState(c, BossFightState.Recovery);
            }
            else EnterWaiting(c);
        }

        /// <summary>진행 중인 패턴을 끊는다(단계 전환, 사망, 되비침, 걷기). 미판정 예고 표는 취소한다. 상태는 바꾸지 않는다.</summary>
        void AbortRun(BossContext c)
        {
            var run = c.Run;
            if (run == null) return;
            var threats = c.W.Mods.Threats;
            for (int i = 0; i < run.Active.Count; i++)
            {
                var inst = run.Active[i];
                if (inst.Phase == BossStepPhase.AwaitGrant || inst.Phase == BossStepPhase.Telegraph) threats.CancelTicket(c.W, inst.Ticket);
            }
            run.Active.Clear();
            if (c.Body != null) c.Body.DashActive = false;
            c.Run = null;
            c.Boss.LastPattern = run.Pattern.Key;
            c.Boss.CurrentPattern = null;
            c.Brain.OnPatternEnd(c, run.Pattern, true);
        }

        /// <summary>몸 접촉 피해(같은 적 1.5초에 1회). 쿨다운은 QueueHit이 true일 때만 소비한다(D-63).</summary>
        void Contact(BossContext c)
        {
            var w = c.W;
            var body = c.Body;
            var def = c.Def;
            if (def.ContactDamage <= 0 || body == null || !body.Alive || body.DashActive || body.Burrowed) return;
            var s = c.Boss.State;
            if (s == BossFightState.Intro || s == BossFightState.PhaseTransition || s == BossFightState.Reflection
                || s == BossFightState.Collapse || s == BossFightState.CollapseDone || s == BossFightState.Dead) return;
            if (w.Tick < body.ContactReadyTick) return;
            bool consumed = false;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active || p.Downed) continue;
                if (!c.Brain.TouchesPlayer(c, p)) continue;
                var hit = new PlayerHitRequest
                {
                    AttackId = body.Id, SourceEnemyId = body.Id, Source = PlayerDamageSource.Contact, FromPos = body.Pos,
                    DamageCells = w.Mods.Threats.DamageCells(w, def.ContactDamage, PlayerDamageSource.Contact, body)
                };
                if (w.Mods.Player.QueueHit(w, p, hit)) consumed = true;
            }
            if (consumed) body.ContactReadyTick = w.Tick + SimConst.Ticks(c.Common.ContactCooldownSeconds);
        }
    }
}
