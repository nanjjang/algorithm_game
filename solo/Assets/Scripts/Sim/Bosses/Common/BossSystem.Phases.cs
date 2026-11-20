using System;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    public sealed partial class BossSystem
    {
        /// <summary>되비침의 만개 연출을 알리는 BossPatternStarted 이벤트의 Id(A = 몇 번째, 판정 없음).</summary>
        public const string ReflectionBloomKey = "reflection_bloom";

        /// <summary>
        /// 10단계(플레이어 피격 확정·적 사망 처리 뒤). HpMin 추적, 첫 만남 종료 조건(체력 0 > 50% > 45초), 사망, 단계 전환.
        /// 같은 틱에 플레이어가 먼저 쓰러졌으면 Run.OnPlayerDowned가 앞서 불려 있다(첫 만남 → BeginCollapse, 그 밖 → RunEnded).
        /// </summary>
        public override void Stage10_AfterDamage(SimWorld w)
        {
            var b = w.Boss;
            var c = Context(w);
            if (b == null || c == null) return;
            if (b.State == BossFightState.Dead || b.State == BossFightState.CollapseDone) return;
            var body = w.FindEnemy(b.EnemyId);
            if (body != null) c.Body = body;
            body = c.Body;
            if (body != null) b.HpMin = Mathf.Min(b.HpMin, Mathf.Max(0f, body.Hp));
            if (b.State == BossFightState.Intro || b.State == BossFightState.Reflection || b.State == BossFightState.Collapse) return;

            if (b.FirstEncounter)
            {
                // 종료 우선순위: 플레이어 체력 0(이미 Collapse로 옮겨져 위에서 걸러졌다) > 체력 바닥 > 45초.
                var fe = b.Def.FirstEncounter;
                bool floor = body != null && body.Hp <= body.HpFloor;
                bool time = w.Tick - c.FightStartTick >= SimConst.Ticks(fe.MaxFightSeconds);
                if (floor) w.Emit(SimEventType.PhantomMirrorCrack, entityId: b.EnemyId, pos: body.Pos);
                if (floor || time) EnterReflection(c);
                return;
            }

            if (body == null || body.Dead || body.PendingDeath || body.Hp <= 0f)
            {
                Die(c);
                return;
            }

            int next = BossRules.PhaseFor(b.Def, body.Hp, body.MaxHp, b.Phase);
            if (next != b.Phase) BeginPhaseTransition(c, next);
        }

        /// <summary>단계 전환(36틱 멈춤): 미판정 예고 삭제, 발사된 탄·깔린 구역은 유지, 단계의 덮어쓰기·꼬리표 적용.</summary>
        void BeginPhaseTransition(BossContext c, int next)
        {
            var w = c.W;
            var b = c.Boss;
            int old = b.Phase;
            AbortRun(c);
            if (c.Body != null) w.Mods.Threats.CancelTelegraphsOf(w, c.Body.Id);
            b.Phase = next;
            c.Patterns = BossPatternData.Resolve(b.Def, next, b.Cycle);
            c.Excluded.Clear();
            c.RecoveryLeft = 0;
            c.RecoveryDamageRatio = 1f;
            var phaseDef = c.PhaseDef;
            if (c.Body != null) c.Body.Tags = PhaseTags(phaseDef);
            SetState(c, BossFightState.PhaseTransition);
            w.Emit(SimEventType.BossPhaseChanged, entityId: b.EnemyId, a: next);
            c.Brain.OnPhaseChanged(c, old, next);
        }

        /// <summary>
        /// 보스 사망: State = Dead, 남은 졸개와 적 탄을 전부 치운다. 같은 틱에 플레이어가 먼저 쓰러져 판이 끝났으면(RunEnded)
        /// BossKilled·RevengeKill을 내지 않는다(D-45, 공통 규칙 8).
        /// </summary>
        void Die(BossContext c)
        {
            var w = c.W;
            var b = c.Boss;
            var body = c.Body;
            AbortRun(c);
            EndFight(c);
            b.HpMin = 0f;
            Vector2 pos = body != null ? body.Pos : Vector2.zero;
            if (body != null)
            {
                w.Mods.Threats.CancelTelegraphsOf(w, body.Id);
                body.DashActive = false;
                body.PendingDeath = true;
                body.Dead = true;
            }
            SetState(c, BossFightState.Dead);
            if (w.Run.Phase != RunPhase.RunEnded)
            {
                w.Emit(SimEventType.BossKilled, entityId: b.EnemyId, pos: pos, id: b.Def.Id);
                if (IsRevengeTarget(w, b.Def, b.Wave, b.FirstEncounter, c.Tuning))
                    w.Emit(SimEventType.RevengeKill, entityId: b.EnemyId, pos: pos);
            }
            w.Mods.Enemies.RemoveAll(w, true);
            w.Mods.Threats.ClearShots(w, pos, 0f);
        }

        // ------------------------------------------------------------ 허상 첫 만남 (05 요약서 10.2절, 00 문서 5.5절 표)

        /// <summary>되비침에 들어간다: 진입 틱에 입력 잠금, 예고·탄 전부 제거. Run은 같은 틱 11단계에 PhantomFinale로 옮긴다.</summary>
        void EnterReflection(BossContext c)
        {
            var w = c.W;
            AbortRun(c);
            EndFight(c);
            SetInputLock(c, true);
            w.Mods.Threats.ClearAll(w, false);
            c.ReflectionBloomIndex = 0;
            c.ReflectionDownDone = false;
            SetState(c, BossFightState.Reflection);
            w.Emit(SimEventType.PhantomReflection, entityId: c.Boss.EnemyId, pos: c.Body != null ? c.Body.Pos : Vector2.zero);
        }

        /// <summary>
        /// 되비침 180틱: 만개 연출(24/54/84틱, 판정 없음) → +114틱에 Player.ForceDown(Scripted) → 180틱 뒤 Collapse.
        /// PhantomFinale에서는 플레이어 피해가 꺼져 있어 QueueHit을 쓰지 않는다. ForceDown은 Run.OnPlayerDowned를 부르지 않으므로
        /// BeginCollapse가 다시 불리지 않고, 쓰러짐으로는 여기서 직접 넘어간다.
        /// </summary>
        void TickReflection(BossContext c, int elapsed)
        {
            var w = c.W;
            var fe = c.Def.FirstEncounter;
            var bloom = c.Tuning.ReflectionBloomTicks;
            while (bloom != null && c.ReflectionBloomIndex < bloom.Length && elapsed >= bloom[c.ReflectionBloomIndex])
            {
                c.ReflectionBloomIndex++;
                w.Emit(SimEventType.BossPatternStarted, entityId: c.Boss.EnemyId, a: c.ReflectionBloomIndex, id: ReflectionBloomKey,
                    pos: c.Body != null ? c.Body.Pos : Vector2.zero);
            }
            if (fe == null)
            {
                EnterCollapse(c);
                return;
            }
            if (!c.ReflectionDownDone && elapsed >= fe.FinisherHpZeroTicks)
            {
                c.ReflectionDownDone = true;
                for (int i = 0; i < w.Players.Length; i++)
                {
                    var p = w.Players[i];
                    if (p.Active && !p.Downed) w.Mods.Player.ForceDown(w, p, PlayerDamageSource.Scripted);
                }
            }
            if (elapsed >= SimConst.Ticks(fe.FinisherSeconds)) EnterCollapse(c);
        }

        void EnterCollapse(BossContext c)
        {
            c.MasterLineDone = false;
            SetState(c, BossFightState.Collapse);
            c.W.Emit(SimEventType.PhantomCollapse, entityId: c.Boss.EnemyId, pos: c.Body != null ? c.Body.Pos : Vector2.zero);
        }

        /// <summary>쓰러짐 240틱: 72틱에 대사, 240틱 뒤 CollapseDone + 입력 잠금 해제. 일어남·걷기·판 전이는 Run이 한다.</summary>
        void TickCollapse(BossContext c, int elapsed)
        {
            var fe = c.Def.FirstEncounter;
            if (!c.MasterLineDone && elapsed >= c.Tuning.CollapseMasterLineTicks)
            {
                c.MasterLineDone = true;
                c.W.Emit(SimEventType.PhantomMasterLine, entityId: c.Boss.EnemyId);
            }
            int total = fe != null ? SimConst.Ticks(fe.CollapseSeconds) : 0;
            if (elapsed < total) return;
            SetState(c, BossFightState.CollapseDone);
            SetInputLock(c, false);
        }

        /// <summary>
        /// 첫 만남의 싸움 중 플레이어 체력이 먼저 0이 됐다(Run.OnPlayerDowned가 10단계에 부른다) → 바로 쓰러짐.
        /// 되비침·쓰러짐 중이거나 첫 만남이 아니면 아무 일도 없다.
        /// </summary>
        public override void BeginCollapse(SimWorld w)
        {
            var b = w.Boss;
            var c = Context(w);
            if (b == null || c == null || !b.FirstEncounter) return;
            switch (b.State)
            {
                case BossFightState.Reflection:
                case BossFightState.Collapse:
                case BossFightState.CollapseDone:
                case BossFightState.Dead:
                    return;
            }
            AbortRun(c);
            EndFight(c);
            SetInputLock(c, true);
            w.Mods.Threats.ClearAll(w, false);
            EnterCollapse(c);
        }
    }
}
