using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;
using Wizard.Solo.Tests.Bosses.Common;

namespace Wizard.Solo.Tests.Bosses.Four
{
    /// <summary>
    /// 진짜 두뇌를 감싸 한 패턴만 고르게 하는 두뇌(그 밖의 행동은 전부 안쪽 두뇌에 넘긴다).
    /// Only = null이면 고르기도 안쪽 두뇌 그대로, "none"처럼 없는 열쇠면 아무 패턴도 고르지 않는다. Still이면 걷지 않는다.
    /// </summary>
    public sealed class ForcedBrain : IBossBrain
    {
        public readonly IBossBrain Inner;
        public string Only;
        public bool Still;

        public ForcedBrain(IBossBrain inner, string only, bool still)
        {
            Inner = inner;
            Only = only;
            Still = still;
        }

        public void Init(BossContext c) { Inner.Init(c); }
        public void Move(BossContext c, PlayerState target) { if (!Still) Inner.Move(c, target); }
        public void Always(BossContext c, PlayerState target, bool halted) { Inner.Always(c, target, halted); }
        public bool Eligible(BossContext c, BossPattern pattern)
        {
            return Only == null ? Inner.Eligible(c, pattern) : pattern.Key == Only && Inner.Eligible(c, pattern);
        }
        public string Choose(BossContext c, IReadOnlyList<string> candidates, string picked)
        {
            return Only == null ? Inner.Choose(c, candidates, picked) : picked;
        }
        public void OnPatternStart(BossContext c, BossPattern pattern) { Inner.OnPatternStart(c, pattern); }
        public void OnPatternEnd(BossContext c, BossPattern pattern, bool aborted) { Inner.OnPatternEnd(c, pattern, aborted); }
        public bool BuildZones(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones)
        {
            return Inner.BuildZones(c, step, inst, target, zones);
        }
        public void OnTelegraphStart(BossContext c, BossStep step, BossStepInstance inst) { Inner.OnTelegraphStart(c, step, inst); }
        public BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target) { return Inner.Act(c, step, inst, target); }
        public void OnDashMove(BossContext c, BossStep step, BossStepInstance inst, Vector2 from, Vector2 to) { Inner.OnDashMove(c, step, inst, from, to); }
        public void OnDashEnd(BossContext c, BossStep step, BossStepInstance inst, bool hitWall) { Inner.OnDashEnd(c, step, inst, hitWall); }
        public void OnPhaseChanged(BossContext c, int oldPhase, int newPhase) { Inner.OnPhaseChanged(c, oldPhase, newPhase); }
        public void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState) { Inner.OnStateChanged(c, oldState, newState); }
        public float ModifyIncomingDamage(BossContext c, float amount, bool isCrit, Vector2 fromPos, Vector2 hitPos) { return Inner.ModifyIncomingDamage(c, amount, isCrit, fromPos, hitPos); }
        public bool TouchesPlayer(BossContext c, PlayerState p) { return Inner.TouchesPlayer(c, p); }
        public void OnFightEnd(BossContext c) { Inner.OnFightEnd(c); }
    }

    /// <summary>네 보스 테스트의 공용 도우미. 세계는 G07의 BossWorld(가짜 Threats·Player·Enemies)를 그대로 쓴다.</summary>
    public static class FourKit
    {
        public const string NoPattern = "none";
        public const float Eps = 0.001f;

        /// <summary>진짜 두뇌(등록표에서 만든 것)를 ForcedBrain으로 감싸 보스를 등장시킨다. 등장은 아직 끝나지 않았다.</summary>
        public static BossWorld Make(string bossId, int wave, string only, bool still, out ForcedBrain brain)
        {
            var bw = BossWorld.Create();
            var forced = new ForcedBrain(BossBrainRegistry.Build().Create(bossId), only, still);
            bw.Sys.Brains.Add(bossId, () => forced);
            bw.Spawn(bossId, wave);
            brain = forced;
            return bw;
        }

        public static BossWorld Make(string bossId, int wave, string only, bool still = true)
        {
            ForcedBrain brain;
            return Make(bossId, wave, only, still, out brain);
        }

        /// <summary>체력을 그 단계의 경계에 맞춰 단계 전환을 일으킨다(등장이 끝난 뒤에 부른다). 돌아올 때 단계가 바뀌어 있다.</summary>
        public static void ToPhase(BossWorld bw, int phaseIndex)
        {
            var def = bw.Boss.Def;
            // 경계 바로 아래로 둔다(경계 값 그대로는 float 반올림으로 경계 위가 될 수 있다. 경계 등호는 G07의 단계 벡터가 본다).
            bw.Body.Hp = bw.Body.MaxHp * def.Phases[phaseIndex].EnterAtHpPercent / 100f - 1f;
            bw.Step(1);
            NUnit.Framework.Assert.AreEqual(phaseIndex, bw.Boss.Phase, "단계 전환이 일어나지 않았다");
        }

        public static int CountPattern(BossWorld bw, string key)
        {
            int n = 0;
            var ev = bw.W.Events;
            for (int i = 0; i < ev.Count; i++)
                if (ev[i].Type == SimEventType.BossPatternStarted && ev[i].Id == key) n++;
            return n;
        }

        public static SimEvent PatternEvent(BossWorld bw, string key, int index)
        {
            var ev = bw.W.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                if (ev[i].Type != SimEventType.BossPatternStarted || ev[i].Id != key) continue;
                if (index-- == 0) return ev[i];
            }
            return default;
        }

        /// <summary>그 패턴의 다음 BossPatternStarted가 날 때까지 진행하고 그 틱을 돌려준다(돌아올 때 w.Tick = 그 틱 + 1). 안 나면 -1.</summary>
        public static int WaitPattern(BossWorld bw, string key, int maxTicks = 2000)
        {
            int before = CountPattern(bw, key);
            for (int i = 0; i < maxTicks; i++)
            {
                bw.Step(1);
                if (CountPattern(bw, key) > before) return PatternEvent(bw, key, before).Tick;
            }
            return -1;
        }

        /// <summary>그 틱까지 처리한다(돌아올 때 w.Tick = tick + 1).</summary>
        public static void RunThrough(BossWorld bw, int tick)
        {
            bw.StepTo(tick + 1);
        }

        /// <summary>돌진이 끝날 때까지 진행하고 끝난 틱을 돌려준다.</summary>
        public static int RunDash(BossWorld bw, int maxTicks = 400)
        {
            for (int i = 0; i < maxTicks && bw.Body.DashActive; i++) bw.Step(1);
            return bw.W.Tick - 1;
        }

        public static int CountHits(BossWorld bw, PlayerDamageSource source)
        {
            int n = 0;
            for (int i = 0; i < bw.Player.Hits.Count; i++) if (bw.Player.Hits[i].Source == source) n++;
            return n;
        }

        public static PlayerHitRequest FirstHit(BossWorld bw, PlayerDamageSource source)
        {
            for (int i = 0; i < bw.Player.Hits.Count; i++) if (bw.Player.Hits[i].Source == source) return bw.Player.Hits[i];
            return default;
        }

        /// <summary>점에서 직선(origin, dir)까지의 거리.</summary>
        public static float LineDistance(Vector2 p, Vector2 origin, Vector2 dir)
        {
            Vector2 v = p - origin;
            return Mathf.Abs(v.x * dir.y - v.y * dir.x);
        }

        public static bool OnBorder(SimWorld w, Vector2 p)
        {
            return Mathf.Abs(p.x) < Eps || Mathf.Abs(p.x - w.ArenaWidth) < Eps || Mathf.Abs(p.y) < Eps || Mathf.Abs(p.y - w.ArenaHeight) < Eps;
        }

        public static HazardEntity AddHazard(BossWorld bw, HazardKind kind, Vector2 pos, float radius, int ownerId)
        {
            var h = new HazardEntity
            {
                Id = bw.W.NewEntityId(), Kind = kind, OwnerEnemyId = ownerId, Pos = pos, Radius = radius, StartTick = bw.W.Tick,
                UntilTick = int.MaxValue, SlowOnly = kind == HazardKind.FrostField
            };
            bw.W.Hazards.Add(h);
            return h;
        }

        public static ObstacleEntity AddObstacle(BossWorld bw, string kind, Vector2 pos, float radius, float hp)
        {
            var o = new ObstacleEntity
            {
                Id = bw.W.NewEntityId(), Kind = kind, Pos = pos, Radius = radius, Hp = hp, UntilTick = int.MaxValue,
                BlocksPlayerShots = true, BlocksEnemyShots = true
            };
            bw.W.Obstacles.Add(o);
            return o;
        }

        public static EnemyState AddMinion(BossWorld bw, string defId, int summonerId)
        {
            var e = new EnemyState
            {
                Id = bw.W.NewEntityId(), DefId = defId, Def = bw.W.Content.Enemy(defId), Pos = new Vector2(1f, 1f), NoReward = true,
                SummonerId = summonerId, Hp = 1f, MaxHp = 1f, Ai = EnemyAiState.Act
            };
            bw.W.Enemies.Add(e);
            return e;
        }

        public static BossPatternDef PatternDef(BossDef def, string key)
        {
            for (int i = 0; i < def.Patterns.Count; i++) if (def.Patterns[i].Key == key) return def.Patterns[i];
            return null;
        }
    }
}
