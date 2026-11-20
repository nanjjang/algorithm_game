using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 가마등(boss.kiln_boar, 05 요약서 9.1절). 패턴 셋(삼연 돌진, 발구르기, 낙석)의 예고·반복·고리·바위는 뼈대가 데이터대로 실행하고,
    /// 여기서는 돌진에 딸린 것만 한다:
    ///  - 지나간 길에 불씨 띠(폭 1.0, 3초, 접촉 피해 1, 1칸 조각 단위)를 깐다.
    ///  - 새 돌진 예고가 뜨는 순간 예고 띠 중심선에서 폭/2 + clear_hazard_margin(= 2.5칸) 안에 걸친 불씨 조각을 끈다.
    ///  - 돌진이 지나간 바위를 깨고 파편(6발, 60도)을 쏜다.
    /// 마지막 돌진의 벽 기절(2.5초, 받는 피해 ×1.5)은 패턴의 recovery_seconds·recovery_damage_ratio로 뼈대가 처리한다.
    /// 설치 벽(얼음 병풍)은 경직 없이 부수고 계속 달린다(뼈대의 돌진).
    /// </summary>
    public sealed class KilnBoarBrain : BossBrainBase
    {
        public const string PatternTripleCharge = "triple_charge", PatternStomp = "stomp", PatternRockfall = "rockfall";
        // 그림 클립 이름(monsters manifest의 boss.kiln_boar).
        const string ClipWalk = "walk", ClipChargeWarn = "charge_warn", ClipCharge = "charge", ClipStomp = "stomp",
            ClipRoar = "roar", ClipStun = "stun";

        public override void Init(BossContext c)
        {
            c.SetAnim(ClipWalk);
        }

        public override void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState)
        {
            switch (newState)
            {
                case BossFightState.Waiting:
                case BossFightState.R5Blocked:
                    c.SetAnim(ClipWalk);
                    break;
                case BossFightState.Telegraph:
                    c.SetAnim(c.Boss.CurrentPattern == PatternTripleCharge ? ClipChargeWarn : c.Boss.CurrentPattern == PatternStomp ? ClipStomp : ClipRoar);
                    break;
                case BossFightState.Recovery:
                    c.SetAnim(c.Boss.LastPattern == PatternTripleCharge ? ClipStun : ClipWalk);
                    break;
                case BossFightState.PhaseTransition:
                    c.SetAnim(ClipRoar);
                    break;
            }
        }

        /// <summary>새 돌진 예고가 시작되면 그 길 가까이의 불씨가 꺼진다(9.1절 [임시 U30]).</summary>
        public override void OnTelegraphStart(BossContext c, BossStep step, BossStepInstance inst)
        {
            if (!step.IsDash) return;
            var w = c.W;
            var body = c.Body;
            float reach = step.Attack.Width / 2f + step.Def.ClearHazardMargin;
            Vector2 a = body.Pos;
            Vector2 b = a + inst.Dir * BossRules.DistanceToWall(w, a, inst.Dir, 0f);
            for (int i = 0; i < w.Hazards.Count; i++)
            {
                var h = w.Hazards[i];
                if (h.Dead || h.Kind != HazardKind.EmberStrip || h.OwnerEnemyId != body.Id) continue;
                Vector2 h2 = h.Pos + h.Dir * h.Length;
                if (BossRules.SegmentSegmentDistance(a, b, h.Pos, h2) < reach) c.ExpireHazard(h);
            }
        }

        public override BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
        {
            if (step.IsDash && inst.Phase == BossStepPhase.Act) c.SetAnim(ClipCharge);
            return BossActResult.Default;
        }

        public override void OnDashMove(BossContext c, BossStep step, BossStepInstance inst, Vector2 from, Vector2 to)
        {
            LayEmbers(c, step, inst, to, false);
            BreakBoulders(c, from, to);
        }

        public override void OnDashEnd(BossContext c, BossStep step, BossStepInstance inst, bool hitWall)
        {
            LayEmbers(c, step, inst, c.Body.Pos, true);
        }

        /// <summary>돌진이 조각 길이(1칸)만큼 나아갈 때마다 불씨 띠 조각 하나를 깐다. flush면 남은 자투리도 깐다.</summary>
        void LayEmbers(BossContext c, BossStep step, BossStepInstance inst, Vector2 now, bool flush)
        {
            var hz = step.Attack != null ? step.Attack.LeavesHazard : null;
            if (hz == null) return;
            float seg = c.Tuning.EmberSegmentLength;
            if (seg <= 0f) return;
            // ScratchPos = 아직 불씨를 깔지 않은 구간의 시작점(StartDash가 출발점으로 둔다).
            float pending = Vector2.Dot(now - inst.ScratchPos, inst.Dir);
            while (pending >= seg)
            {
                SpawnEmber(c, hz, inst.ScratchPos, inst.Dir, seg);
                inst.ScratchPos += inst.Dir * seg;
                pending -= seg;
            }
            if (flush && pending > 0f)
            {
                SpawnEmber(c, hz, inst.ScratchPos, inst.Dir, pending);
                inst.ScratchPos = now;
            }
        }

        static void SpawnEmber(BossContext c, HazardDef hz, Vector2 start, Vector2 dir, float length)
        {
            var spec = new HazardSpec
            {
                Kind = BossRules.ParseHazardKind(hz.Kind), OwnerEnemyId = c.Body.Id, Shape = ZoneShape.Band, Pos = start, Dir = dir,
                Radius = hz.Radius, Width = hz.Radius + hz.Radius, Length = length,
                DurationTicks = SimConst.Ticks(hz.DurationSeconds), BaseDamage = c.Tuning.EmberDamage,
                ContactCooldownTicks = SimConst.Ticks(hz.ContactCooldownSeconds), MaxCount = hz.MaxCount
            };
            c.W.Mods.Threats.SpawnHazard(c.W, spec);
        }

        /// <summary>돌진이 지나간 바위는 깨져 파편을 쏜다(9.1절: 6발, 60도, 속도 6, 대 탄, 피해 1).</summary>
        void BreakBoulders(BossContext c, Vector2 from, Vector2 to)
        {
            var w = c.W;
            var rock = c.Pattern(PatternRockfall);
            JObject obstacle = rock != null && rock.Steps.Count > 0 ? rock.Steps[0].Def.Obstacle : null;
            if (obstacle == null) return;
            string kind = BossTuning.Str(obstacle, "kind", null);
            var shards = obstacle["shards"] as JObject;
            for (int i = 0; i < w.Obstacles.Count; i++)
            {
                var o = w.Obstacles[i];
                if (o.Dead || o.Kind != kind) continue;
                if (BossRules.PointSegmentDistance(o.Pos, from, to) >= c.Body.BodyRadius + o.Radius) continue;
                c.BreakObstacle(o);
                if (shards == null) continue;
                int count = BossTuning.Int(shards, "count", 0);
                if (count <= 0) continue;
                // 첫 파편은 플레이어 쪽(고리 탄의 관례), 없으면 돌진 방향.
                var p = w.NearestPlayer(o.Pos);
                Vector2 first = p != null && (p.Pos - o.Pos).sqrMagnitude > 0f ? (p.Pos - o.Pos).normalized : c.Body.Facing;
                c.FireRing(o.Pos, first, count, BossTuning.Float(shards, "spread_degrees", BossContext.FullTurnDegrees / count),
                    BossTuning.Float(shards, "speed", 0f), BossTuning.Float(shards, "hit_radius", 0f), -1f,
                    c.Tuning.BoulderShardDamage, true, w.NewCastId());
            }
        }
    }
}
