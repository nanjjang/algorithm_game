using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>step 처리 결과. Default = 뼈대의 기본 처리에 맡긴다 / Done = 두뇌가 끝냈다 / Busy = 다음 틱에 다시 부른다.</summary>
    public enum BossActResult { Default, Done, Busy }

    /// <summary>
    /// 보스별 특수 행동. 뼈대(BossSystem)는 싸움 상태 시계·격노·단계·패턴 고르기·step 실행을 하고,
    /// 보스마다 다른 것(이동, 고를 수 있는 패턴, 특수 step, 상시 행동, 부위별 피해)을 이 인터페이스로 묻는다.
    /// 구현은 상태를 자기 필드에 둔다(보스 한 마리에 두뇌 객체 하나. 정적 가변 필드 금지).
    /// **두뇌는 BossState.State·StateStartTick과 PlayerState.InputLocked를 바꾸지 않는다**(시계의 주인은 뼈대).
    /// 대부분은 <see cref="BossBrainBase"/>를 상속해 필요한 것만 덮어쓴다. 서명·틱 규칙·예는 Common/README.md.
    /// </summary>
    public interface IBossBrain
    {
        /// <summary>SpawnBoss 직후 한 번(몸통과 BossState가 만들어진 뒤).</summary>
        void Init(BossContext c);
        /// <summary>이동. 전투 중(w.CombatActive)이고 동작 멈춤이 아닌 틱마다 불린다(돌진 중에는 안 불린다). 상태를 보고 스스로 가린다.</summary>
        void Move(BossContext c, PlayerState target);
        /// <summary>상시 행동(passives). 전투 중 틱마다, 이동 뒤·패턴 처리 앞에 불린다. halted = 뇌광창 동작 멈춤 중.</summary>
        void Always(BossContext c, PlayerState target, bool halted);
        /// <summary>그 패턴을 지금 고를 수 있나(05 요약서 8.3절 Eligible).</summary>
        bool Eligible(BossContext c, BossPattern pattern);
        /// <summary>균등 난수로 뽑은 picked를 보스별 강제 규칙으로 바꾼다(흰뿔). 보통은 picked 그대로.</summary>
        string Choose(BossContext c, IReadOnlyList<string> candidates, string picked);
        /// <summary>패턴이 시작됐다(첫 step을 띄우기 직전). aborted 없이 끝나면 OnPatternEnd(aborted: false).</summary>
        void OnPatternStart(BossContext c, BossPattern pattern);
        void OnPatternEnd(BossContext c, BossPattern pattern, bool aborted);
        /// <summary>예고 구역을 직접 짠다. true = zones를 채웠다(비어 있으면 그 반복은 건너뛴다) / false = 뼈대의 기본 배치.</summary>
        bool BuildZones(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones);
        /// <summary>예고가 허가돼 구역이 떴다(시작 틱의 다음 틱 7단계). 가마등의 불씨 소화 등.</summary>
        void OnTelegraphStart(BossContext c, BossStep step, BossStepInstance inst);
        /// <summary>step의 자체 타격 차례(예고가 있으면 판정 틱과 같은 틱의 7단계). Default면 뼈대가 kind대로 처리한다.</summary>
        BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target);
        /// <summary>돌진으로 몸이 from → to로 움직였다(틱마다). 지나간 길의 불씨·장애물 처리.</summary>
        void OnDashMove(BossContext c, BossStep step, BossStepInstance inst, Vector2 from, Vector2 to);
        void OnDashEnd(BossContext c, BossStep step, BossStepInstance inst, bool hitWall);
        void OnPhaseChanged(BossContext c, int oldPhase, int newPhase);
        /// <summary>싸움 상태가 바뀌었다(표시용 클립 고르기 등). 상태를 다시 바꾸지 않는다.</summary>
        void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState);
        /// <summary>부위별·상태별 피해 조정(밤삼킴 마디 ×0.5 등). 뼈대의 공통 조정 뒤에 불린다. hitPos = 맞은 자리(몸 겉면).</summary>
        float ModifyIncomingDamage(BossContext c, float amount, bool isCrit, Vector2 fromPos, Vector2 hitPos);
        /// <summary>몸이 플레이어에 닿았나(접촉 피해). 기본은 몸통 원 하나. 밤삼킴은 머리·마디·꼬리.</summary>
        bool TouchesPlayer(BossContext c, PlayerState p);
        /// <summary>싸움이 끝났다(사망, 되비침·쓰러짐 진입, Dismiss). 두뇌가 든 표·개체를 정리한다. 여러 번 불려도 된다.</summary>
        void OnFightEnd(BossContext c);
    }

    /// <summary>두뇌의 기본 구현: 플레이어를 쫓고, 모든 패턴을 고를 수 있고, step은 뼈대의 기본 처리.</summary>
    public abstract class BossBrainBase : IBossBrain
    {
        public virtual void Init(BossContext c) { }

        public virtual void Move(BossContext c, PlayerState target)
        {
            var s = c.Boss.State;
            if (s != BossFightState.Waiting && s != BossFightState.R5Blocked) return;
            c.Chase(target.Pos, c.Def.MoveSpeed);
        }

        public virtual void Always(BossContext c, PlayerState target, bool halted) { }
        public virtual bool Eligible(BossContext c, BossPattern pattern) { return true; }
        public virtual string Choose(BossContext c, IReadOnlyList<string> candidates, string picked) { return picked; }
        public virtual void OnPatternStart(BossContext c, BossPattern pattern) { }
        public virtual void OnPatternEnd(BossContext c, BossPattern pattern, bool aborted) { }
        public virtual bool BuildZones(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones) { return false; }
        public virtual void OnTelegraphStart(BossContext c, BossStep step, BossStepInstance inst) { }
        public virtual BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target) { return BossActResult.Default; }
        public virtual void OnDashMove(BossContext c, BossStep step, BossStepInstance inst, Vector2 from, Vector2 to) { }
        public virtual void OnDashEnd(BossContext c, BossStep step, BossStepInstance inst, bool hitWall) { }
        public virtual void OnPhaseChanged(BossContext c, int oldPhase, int newPhase) { }
        public virtual void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState) { }
        public virtual float ModifyIncomingDamage(BossContext c, float amount, bool isCrit, Vector2 fromPos, Vector2 hitPos) { return amount; }

        public virtual bool TouchesPlayer(BossContext c, PlayerState p)
        {
            float r = c.Body.BodyRadius + c.HitRadius(p);
            return (p.Pos - c.Body.Pos).sqrMagnitude < r * r;
        }

        public virtual void OnFightEnd(BossContext c) { }
    }

    /// <summary>두뇌가 등록되지 않은 보스용: 기본 행동 그대로(데이터의 패턴은 뼈대가 실행한다).</summary>
    public sealed class DefaultBossBrain : BossBrainBase { }
}
