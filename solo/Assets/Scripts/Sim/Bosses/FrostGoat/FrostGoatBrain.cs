using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 흰뿔(boss.frost_goat, 05 요약서 9.5절). 기둥 원·휘두르기 부채·뿔 돌진 띠의 예고와 판정 틱은 뼈대가 데이터대로 실행하고, 여기서는
    ///  - 이동: 플레이어 피격 원 중심이 서리밭 안이면 move_speed_on_frost(2.4), 나가는 틱에 바로 3.0.
    ///  - 고르기 규칙(S6 7.4): 서리밭 안 플레이어에게 숨결 금지, 뿔 돌진 바로 뒤 숨결 금지, 기둥이 max_count(6)면 기둥 금지,
    ///    숨결이 뽑혔는데 기둥이 2개 미만이면 기둥을 먼저 세우고 다음에 숨결(그 사이 뿔 돌진은 고르지 않는다).
    ///  - 얼음 기둥: 뿔 돌진이 지나가거나 체력이 0이 되면 깨져 파편(8방향, 2회차 12방향)을 쏜다.
    ///  - 뿔 휘두르기: 예고 시작 틱에 플레이어가 서리밭 안이면 예고가 frost_telegraph_seconds(1.2초).
    ///  - 서리 숨결: 시작 변에서 초당 45도로 쓸며 15틱마다 1발(쓸 각 90도 = 120틱 8발, 2단계 120도 = 160틱 11발).
    ///  - 서리밭: 원이 터지는 틱에 느려지기만 하는 남는 구역(8초, −30%)을 Threats.SpawnHazard로 깐다.
    /// </summary>
    public sealed class FrostGoatBrain : BossBrainBase
    {
        public const string PatternPillar = "ice_pillar", PatternSwing = "horn_swing", PatternBreath = "frost_breath",
            PatternCharge = "horn_charge", PatternField = "frost_field";
        /// <summary>단계 덮어쓰기 열쇠: 숨결이 쓰는 각(도).</summary>
        public const string SweepKey = "sweep_degrees";
        /// <summary>2회차 덮어쓰기 열쇠: 기둥 파편 수와 간격(도).</summary>
        public const string ShardCountKey = "shard_count", ShardSpreadKey = "shard_spread_degrees";
        public const string ExtraName = "G08", ExtraSection = "frost_goat";
        /// <summary>숨결 전에 서 있어야 하는 기둥 수(S6 7.4 "기둥 &lt; 2면 기둥 먼저").</summary>
        public const int PillarsBeforeBreath = 2;
        // 그림 클립 이름(monsters manifest의 boss.frost_goat).
        const string ClipWalk = "walk", ClipPillars = "pillars", ClipSweep = "sweep", ClipBreath = "breath", ClipChargeWarn = "charge_warn",
            ClipCharge = "charge", ClipImpact = "impact", ClipPhase = "phase";

        /// <summary>기둥 파편의 피해 칸(G08.json. 05 요약서 9.5절 "파편 1").</summary>
        public int PillarShardDamage = 1;

        bool _breathPriority;
        readonly List<ObstacleEntity> _pillars = new List<ObstacleEntity>();
        // 숨결
        Vector2 _breathStart = new Vector2(1f, 0f);
        int _breathSign = 1;
        float _breathTotalDegrees;
        int _breathTick, _breathDurationTicks, _breathIntervalTicks = 1;

        /// <summary>기둥을 먼저 세웠으니 다음에 숨결을 고른다는 표시.</summary>
        public bool BreathPriority => _breathPriority;
        /// <summary>이번 숨결의 시작 변(단위 벡터)과 쓰는 방향(+1 반시계 / −1 시계, 시뮬레이션 좌표).</summary>
        public Vector2 BreathStart => _breathStart;
        public int BreathSign => _breathSign;

        public override void Init(BossContext c)
        {
            JObject extra = c.W.Content != null ? c.W.Content.SoloExtra(ExtraName) : null;
            JObject j = extra != null ? extra[ExtraSection] as JObject : null;
            PillarShardDamage = BossTuning.Int(j, "pillar_shard_damage", PillarShardDamage);
            c.SetAnim(ClipWalk);
        }

        // ------------------------------------------------------------ 서리밭·기둥 읽기

        /// <summary>플레이어 피격 원 중심이 서리밭 안인가.</summary>
        public static bool InFrost(SimWorld w, Vector2 pos)
        {
            for (int i = 0; i < w.Hazards.Count; i++)
            {
                var h = w.Hazards[i];
                if (h.Dead || h.Kind != HazardKind.FrostField) continue;
                if ((pos - h.Pos).sqrMagnitude <= h.Radius * h.Radius) return true;
            }
            return false;
        }

        static JObject PillarDef(BossContext c)
        {
            var p = c.Pattern(PatternPillar);
            return p != null && p.Steps.Count > 0 ? p.Steps[0].Def.Obstacle : null;
        }

        static string PillarKind(BossContext c) => BossTuning.Str(PillarDef(c), "kind", null);

        public static int PillarCount(BossContext c)
        {
            string kind = PillarKind(c);
            return kind != null ? BossSystem.CountObstacles(c.W, kind) : 0;
        }

        // ------------------------------------------------------------ 이동

        public override void Move(BossContext c, PlayerState target)
        {
            var s = c.Boss.State;
            if (s != BossFightState.Waiting && s != BossFightState.R5Blocked) return;
            float speed = c.Def.MoveSpeedOnFrost.HasValue && InFrost(c.W, target.Pos) ? c.Def.MoveSpeedOnFrost.Value : c.Def.MoveSpeed;
            if ((target.Pos - c.Body.Pos).magnitude <= c.Tuning.ChaseStopDistance) return;
            if (c.Chase(target.Pos, speed)) return;
            // 제 기둥에 막혔으면 옆으로 돌아간다.
            Vector2 d = c.DirTo(target.Pos);
            Vector2 side = new Vector2(-d.y, d.x) * (speed * (1f - c.Body.SlowStrength(c.W.Tick)) * SimConst.TickSeconds);
            if (!c.MoveBody(side)) c.MoveBody(-side);
            c.Body.Facing = d;
        }

        // ------------------------------------------------------------ 고르기 (S6 7.4)

        public override bool Eligible(BossContext c, BossPattern pattern)
        {
            if (pattern.Key == PatternBreath)
            {
                if (c.Boss.LastPattern == PatternCharge) return false;
                var target = c.W.NearestPlayer(c.Body.Pos);
                return target == null || !InFrost(c.W, target.Pos);
            }
            if (pattern.Key == PatternPillar)
            {
                int max = BossTuning.Int(PillarDef(c), "max_count", 0);
                return max <= 0 || PillarCount(c) < max;
            }
            return true;
        }

        public override string Choose(BossContext c, IReadOnlyList<string> candidates, string picked)
        {
            bool breathPriority = _breathPriority;
            string chosen = ChooseForced(candidates, picked, PillarCount(c), ref breathPriority, c.Rng);
            _breathPriority = breathPriority;
            return chosen;
        }

        /// <summary>
        /// 강제 규칙(순수 함수). 숨결이 뽑혔는데 기둥이 2개 미만이고 기둥을 고를 수 있으면 기둥 먼저(breathPriority 켬) /
        /// breathPriority가 켜져 있고 숨결을 고를 수 있으면 숨결(끔) / 그러고도 뿔 돌진이고 breathPriority가 켜져 있으면 돌진이 아닌 후보.
        /// </summary>
        public static string ChooseForced(IReadOnlyList<string> candidates, string picked, int pillarCount, ref bool breathPriority, Pcg32 rng)
        {
            if (picked == PatternBreath && pillarCount < PillarsBeforeBreath && Contains(candidates, PatternPillar))
            {
                picked = PatternPillar;
                breathPriority = true;
            }
            else if (breathPriority && Contains(candidates, PatternBreath))
            {
                picked = PatternBreath;
                breathPriority = false;
            }
            if (picked == PatternCharge && breathPriority)
            {
                int others = 0;
                for (int i = 0; i < candidates.Count; i++) if (candidates[i] != PatternCharge) others++;
                if (others > 0)
                {
                    int k = rng != null ? rng.Range(0, others) : 0;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (candidates[i] == PatternCharge) continue;
                        if (k-- == 0) { picked = candidates[i]; break; }
                    }
                }
            }
            return picked;
        }

        static bool Contains(IReadOnlyList<string> list, string key)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == key) return true;
            return false;
        }

        // ------------------------------------------------------------ 패턴

        public override void OnPatternStart(BossContext c, BossPattern pattern)
        {
            if (pattern.Key != PatternBreath || pattern.Steps.Count == 0) return;
            // 쓸어 갈 부채꼴은 예고(0.8초) 시작 틱에 정한다: 시작 변 = 플레이어 방향에서 쓸 각의 절반만큼 반대, 방향은 난수.
            var step = pattern.Steps[0];
            var stream = step.Def.Stream;
            _breathTotalDegrees = step.Float(SweepKey, stream != null ? stream.SweepDegrees : 0f);
            _breathSign = c.Rng.Range(0, 2) == 0 ? 1 : -1;
            var target = c.W.NearestPlayer(c.Body.Pos);
            Vector2 aim = target != null ? c.DirTo(target.Pos) : c.Body.Facing;
            _breathStart = BossRules.Rotate(aim, -_breathSign * _breathTotalDegrees / 2f);
            c.Body.Facing = _breathStart;
            // 표시 층이 테두리·조준선·화살표를 그릴 수 있게 이벤트에 싣는다(A = 방향, B = 쓸 각).
            c.PatternEventA = _breathSign;
            c.PatternEventB = Mathf.RoundToInt(_breathTotalDegrees);
        }

        public override bool BuildZones(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones)
        {
            if (c.Run == null) return false;
            string key = c.Run.Pattern.Key;
            if (key == PatternSwing)
            {
                c.DefaultZones(step, inst, target, zones);
                // 예고 시작 틱에 플레이어가 서리밭 안이면 느린 발로도 벗어날 수 있게 예고가 길어진다(1.2초).
                if (step.Def.FrostTelegraphSeconds > 0f && InFrost(c.W, target.Pos))
                {
                    int ticks = SimConst.Ticks(step.Def.FrostTelegraphSeconds);
                    for (int i = 0; i < zones.Count; i++)
                    {
                        var z = zones[i];
                        z.TelegraphTicks = ticks;
                        zones[i] = z;
                    }
                }
                return true;
            }
            if (key == PatternField)
            {
                // 서리밭은 터지는 틱에 이 두뇌가 SpawnHazard로 깐다(느려지는 비율을 step의 hazard_speed_ratio로 주려고).
                c.DefaultZones(step, inst, target, zones);
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    z.LeavesHazard = null;
                    zones[i] = z;
                }
                return true;
            }
            return false;
        }

        public override void OnTelegraphStart(BossContext c, BossStep step, BossStepInstance inst)
        {
            if (step.IsDash) c.SetAnim(ClipChargeWarn);
        }

        public override BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
        {
            if (step.Def.Stream != null) return Breath(c, step, inst);
            if (inst.Phase != BossStepPhase.Act) return BossActResult.Default;
            if (step.IsDash) c.SetAnim(ClipCharge);
            else if (c.Run != null && c.Run.Pattern.Key == PatternField) SpawnFrostFields(c, step, inst);
            return BossActResult.Default;
        }

        /// <summary>숨결 한 발의 간격(틱): 60 ÷ shots_per_second, 연사형이면 격노로 줄고 하한은 min_interval_ticks.</summary>
        public static int BreathIntervalTicks(BossContext c, BossStep step)
        {
            var stream = step.Def.Stream;
            int baseTicks = stream != null && stream.ShotsPerSecond > 0 ? SimConst.Ticks(1f / stream.ShotsPerSecond) : 1;
            int ticks = step.Def.RapidFire
                ? BossRules.RapidIntervalTicks(c.Common, baseTicks, c.Boss.RageStacks, step.Def.MinIntervalTicks)
                : baseTicks;
            return Mathf.Max(1, ticks);
        }

        /// <summary>쓰는 시간(틱) = 쓸 각 ÷ 초당 각 × 60.</summary>
        public static int BreathDurationTicks(float sweepDegrees, float degreesPerSecond)
        {
            return degreesPerSecond > 0f ? SimConst.Ticks(sweepDegrees / degreesPerSecond) : 0;
        }

        BossActResult Breath(BossContext c, BossStep step, BossStepInstance inst)
        {
            var stream = step.Def.Stream;
            if (inst.Phase == BossStepPhase.Act)
            {
                _breathTick = 0;
                _breathDurationTicks = BreathDurationTicks(_breathTotalDegrees, stream.SweepDegreesPerSecond);
                _breathIntervalTicks = BreathIntervalTicks(c, step);
                c.SetAnim(ClipBreath);
            }
            if (_breathDurationTicks <= 0) return BossActResult.Done;
            // 뇌광창의 동작 멈춤 동안은 쓸지 않는다. 플레이어가 서리밭에 들어가도 멈추지 않는다.
            if (c.Halted) return BossActResult.Busy;
            Vector2 dir = BossRules.Rotate(_breathStart, _breathSign * stream.SweepDegreesPerSecond * SimConst.TickSeconds * _breathTick);
            c.Body.Facing = dir;
            if (_breathTick % _breathIntervalTicks == 0) c.FireAttack(step.Attack, c.Body.Pos, dir, 0f, inst.CastId);
            _breathTick++;
            return _breathTick >= _breathDurationTicks ? BossActResult.Done : BossActResult.Busy;
        }

        void SpawnFrostFields(BossContext c, BossStep step, BossStepInstance inst)
        {
            var hz = step.Def.Attack != null ? step.Def.Attack.LeavesHazard : null;
            if (hz == null) return;
            float ratio = step.Def.HazardSpeedRatio;
            for (int i = 0; i < inst.Zones.Count; i++)
            {
                var spec = new HazardSpec
                {
                    Kind = BossRules.ParseHazardKind(hz.Kind), OwnerEnemyId = c.Body.Id, Shape = ZoneShape.Circle, Pos = inst.Zones[i].Pos,
                    Dir = new Vector2(1f, 0f), Radius = hz.Radius, DurationTicks = SimConst.Ticks(hz.DurationSeconds),
                    ContactCooldownTicks = SimConst.Ticks(hz.ContactCooldownSeconds), SlowOnly = true,
                    // 이동 속도 ×0.7 = 0.3만큼 느려진다. 비율이 비어 있으면 0(Threats의 서리밭 기본값).
                    SlowPercent = ratio > 0f && ratio < 1f ? 1f - ratio : 0f, MaxCount = hz.MaxCount
                };
                c.W.Mods.Threats.SpawnHazard(c.W, spec);
            }
        }

        // ------------------------------------------------------------ 기둥

        public override void Always(BossContext c, PlayerState target, bool halted)
        {
            string kind = PillarKind(c);
            if (kind == null) return;
            var obstacles = c.W.Obstacles;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                if (!o.Dead && o.Kind == kind && !_pillars.Contains(o)) _pillars.Add(o);
            }
            // 플레이어 탄에 체력이 0이 된 기둥(Threats가 치웠다)은 파편을 쏜다. 그냥 걷힌 기둥은 조용히 잊는다.
            for (int i = _pillars.Count - 1; i >= 0; i--)
            {
                var o = _pillars[i];
                if (!o.Dead) continue;
                _pillars.RemoveAt(i);
                if (o.Hp <= 0f) FireShards(c, o.Pos);
            }
        }

        public override void OnDashMove(BossContext c, BossStep step, BossStepInstance inst, Vector2 from, Vector2 to)
        {
            // 뿔 돌진이 지나간 기둥은 깨져 파편을 쏜다.
            string kind = PillarKind(c);
            if (kind == null) return;
            var obstacles = c.W.Obstacles;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                if (o.Dead || o.Kind != kind) continue;
                if (BossRules.PointSegmentDistance(o.Pos, from, to) >= c.Body.BodyRadius + o.Radius) continue;
                c.BreakObstacle(o);
                _pillars.Remove(o);
                FireShards(c, o.Pos);
            }
        }

        public override void OnDashEnd(BossContext c, BossStep step, BossStepInstance inst, bool hitWall)
        {
            c.SetAnim(ClipImpact);
        }

        /// <summary>기둥 파편: 8방향 45도(2회차 12방향 30도), 속도 6, 소 탄. 첫 파편은 플레이어 쪽(고리 탄의 관례).</summary>
        void FireShards(BossContext c, Vector2 pos)
        {
            var shards = PillarDef(c) != null ? PillarDef(c)["shards"] as JObject : null;
            if (shards == null) return;
            var step = c.Pattern(PatternPillar).Steps[0];
            int count = step.Int(ShardCountKey, BossTuning.Int(shards, "count", 0));
            if (count <= 0) return;
            float spread = step.Float(ShardSpreadKey, BossTuning.Float(shards, "spread_degrees", BossContext.FullTurnDegrees / count));
            var p = c.W.NearestPlayer(pos);
            Vector2 first = p != null && (p.Pos - pos).sqrMagnitude > 0f ? (p.Pos - pos).normalized : c.Body.Facing;
            c.FireRing(pos, first, count, spread, BossTuning.Float(shards, "speed", 0f), BossTuning.Float(shards, "hit_radius", 0f),
                BossTuning.Float(shards, "range", -1f), PillarShardDamage, true, c.W.NewCastId());
        }

        // ------------------------------------------------------------ 상태

        public override void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState)
        {
            switch (newState)
            {
                case BossFightState.Waiting:
                case BossFightState.R5Blocked:
                    c.SetAnim(ClipWalk);
                    break;
                case BossFightState.Telegraph:
                {
                    string p = c.Boss.CurrentPattern;
                    c.SetAnim(p == PatternSwing ? ClipSweep : p == PatternBreath ? ClipBreath : p == PatternCharge ? ClipChargeWarn : ClipPillars);
                    break;
                }
                case BossFightState.Recovery:
                    c.SetAnim(c.Boss.LastPattern == PatternCharge ? ClipImpact : ClipWalk);
                    break;
                case BossFightState.PhaseTransition:
                    c.SetAnim(ClipPhase);
                    break;
            }
        }

        public override void OnFightEnd(BossContext c)
        {
            _pillars.Clear();
        }
    }
}
