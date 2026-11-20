using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 덮어쓰기(단계 overrides, 순환 2회차 second_cycle)를 반영한 step 하나. Def는 원본 그대로이고 고치지 않는다.
    /// 자주 쓰는 필드(repeat, count, 간격, 각, 공격 모양 값)는 풀어 두고, 그 밖의 덮어쓰기 열쇠는 Float/Int로 읽는다
    /// (예: sweep_degrees, shard_count, hazard_drift_speed).
    /// </summary>
    public sealed class BossStep
    {
        public int Index;
        public BossStepDef Def;
        /// <summary>덮어쓰기를 반영한 공격 정의(덮어쓴 것이 있으면 사본). 공격이 없는 step(copy, summon)은 null.</summary>
        public AttackDef Attack;
        /// <summary>반복 횟수. 0 = 상시 행동이 계속(패턴 실행기는 1번으로 본다).</summary>
        public int Repeat = 1;
        public int Count = 1;
        public float RepeatIntervalSeconds;
        public float RepeatAngleDegrees;
        public float StartDelaySeconds;
        public bool TelegraphEachRepeat;
        public readonly Dictionary<string, JToken> Overrides = new Dictionary<string, JToken>();

        public bool Has(string key) => Overrides.ContainsKey(key);

        public float Float(string key, float fallback)
        {
            JToken v;
            if (Overrides.TryGetValue(key, out v) && v != null && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer)) return (float)v;
            return fallback;
        }

        public int Int(string key, int fallback)
        {
            JToken v;
            if (Overrides.TryGetValue(key, out v) && v != null && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer)) return (int)v;
            return fallback;
        }

        public bool IsDash => Attack != null && Attack.Kind == BossRules.KindDash;
        /// <summary>예고 구역을 내는 step인가(모양과 예고 시간이 있다).</summary>
        public bool HasTelegraph => Attack != null && !string.IsNullOrEmpty(Attack.Shape) && Attack.TelegraphSeconds > 0f;
    }

    public sealed class BossPattern
    {
        public string Key;
        public BossPatternDef Def;
        public readonly List<BossStep> Steps = new List<BossStep>();
        public int RecoveryTicks => SimConst.Ticks(Def.RecoverySeconds);
    }

    /// <summary>보스 정의 + (단계, 회차)에서 패턴 표를 만든다. 단계가 바뀌면 다시 만든다.</summary>
    public static class BossPatternData
    {
        public static Dictionary<string, BossPattern> Resolve(BossDef def, int phaseIndex, int cycle)
        {
            var table = new Dictionary<string, BossPattern>();
            if (def == null || def.Patterns == null) return table;
            for (int i = 0; i < def.Patterns.Count; i++)
            {
                var pd = def.Patterns[i];
                var pattern = new BossPattern { Key = pd.Key, Def = pd };
                for (int s = 0; s < pd.Steps.Count; s++)
                {
                    var sd = pd.Steps[s];
                    pattern.Steps.Add(new BossStep
                    {
                        Index = s, Def = sd, Attack = sd.Attack, Repeat = sd.Repeat, Count = sd.Count,
                        RepeatIntervalSeconds = sd.RepeatIntervalSeconds, RepeatAngleDegrees = sd.RepeatAngleDegrees,
                        StartDelaySeconds = sd.StartDelaySeconds, TelegraphEachRepeat = sd.TelegraphEachRepeat
                    });
                }
                table[pd.Key] = pattern;
            }
            if (def.Phases != null && phaseIndex >= 0 && phaseIndex < def.Phases.Count)
                Apply(table, def.Phases[phaseIndex].Overrides);
            if (cycle >= 2) Apply(table, def.SecondCycle);
            return table;
        }

        static void Apply(Dictionary<string, BossPattern> table, List<BossOverrideDef> overrides)
        {
            if (overrides == null) return;
            for (int i = 0; i < overrides.Count; i++)
            {
                var o = overrides[i];
                BossPattern pattern;
                if (o == null || o.Pattern == null || !table.TryGetValue(o.Pattern, out pattern)) continue;
                if (o.Step < 0 || o.Step >= pattern.Steps.Count || o.Key == null || o.Value == null) continue;
                var step = pattern.Steps[o.Step];
                step.Overrides[o.Key] = o.Value;
                bool number = o.Value.Type == JTokenType.Float || o.Value.Type == JTokenType.Integer;
                switch (o.Key)
                {
                    case "repeat": if (number) step.Repeat = (int)o.Value; break;
                    case "count": if (number) step.Count = (int)o.Value; break;
                    case "repeat_interval_seconds": if (number) step.RepeatIntervalSeconds = (float)o.Value; break;
                    case "repeat_angle_degrees": if (number) step.RepeatAngleDegrees = (float)o.Value; break;
                    case "start_delay_seconds": if (number) step.StartDelaySeconds = (float)o.Value; break;
                    case "telegraph_each_repeat": if (o.Value.Type == JTokenType.Boolean) step.TelegraphEachRepeat = (bool)o.Value; break;
                    default: if (number) ApplyAttack(step, o.Key, (float)o.Value); break;
                }
            }
        }

        /// <summary>공격 모양 값의 덮어쓰기(밤삼킴 또아리의 radius·inner_radius 등). 원본 AttackDef는 고치지 않고 사본에 쓴다.</summary>
        static void ApplyAttack(BossStep step, string key, float value)
        {
            if (step.Attack == null) return;
            switch (key)
            {
                case "radius": case "inner_radius": case "width": case "length": case "fan_degrees":
                case "telegraph_seconds": case "damage": case "dash_speed": case "active_ticks":
                    break;
                default:
                    return;
            }
            if (ReferenceEquals(step.Attack, step.Def.Attack)) step.Attack = Clone(step.Def.Attack);
            var a = step.Attack;
            switch (key)
            {
                case "radius": a.Radius = value; break;
                case "inner_radius": a.InnerRadius = value; break;
                case "width": a.Width = value; break;
                case "length": a.Length = value; break;
                case "fan_degrees": a.FanDegrees = value; break;
                case "telegraph_seconds": a.TelegraphSeconds = value; break;
                case "damage": a.Damage = (int)value; break;
                case "dash_speed": a.DashSpeed = value; break;
                case "active_ticks": a.ActiveTicks = (int)value; break;
            }
        }

        static AttackDef Clone(AttackDef a)
        {
            return new AttackDef
            {
                Key = a.Key, Kind = a.Kind, Shape = a.Shape, Radius = a.Radius, InnerRadius = a.InnerRadius, Width = a.Width,
                Length = a.Length, FanDegrees = a.FanDegrees, Range = a.Range, TriggerMin = a.TriggerMin, TriggerMax = a.TriggerMax,
                TelegraphSeconds = a.TelegraphSeconds, CastSeconds = a.CastSeconds, Lock = a.Lock, LockSeconds = a.LockSeconds,
                Anchor = a.Anchor, ActiveTicks = a.ActiveTicks, DashSpeed = a.DashSpeed, RecoverySeconds = a.RecoverySeconds,
                Damage = a.Damage, DamageSource = a.DamageSource, Proj = a.Proj, Grazeable = a.Grazeable, IsDash = a.IsDash,
                Lob = a.Lob, EscapeOverrideMilli = a.EscapeOverrideMilli, CooldownSeconds = a.CooldownSeconds,
                LeavesHazard = a.LeavesHazard, Extra = a.Extra
            };
        }
    }
}
