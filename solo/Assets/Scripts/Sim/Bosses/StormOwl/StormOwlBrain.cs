using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 밤눈(boss.storm_owl, 05 요약서 9.4절). 깃털 부채·낙뢰의 차례와 틱은 뼈대가 데이터대로 실행하고, 여기서는
    ///  - 활강: 전장을 가로지르는 띠(중심선이 그 틱의 플레이어 위치를 지난다). 4축(가로·세로·대각 둘) 중 직전과 다른 축을 난수로.
    ///    판정 틱의 7단계에 띠가 들어오는 벽에서 출발해 반대쪽 벽까지 16칸/초로 지나간다(몸 충돌 = Dash). 다음 예고는 앞 활강이 끝난 틱.
    ///    2단계(count 2)는 서로 수직인 두 축이 동시에 뜬다 — 둘째 줄은 몸 없이 같은 속도로 지나가는 판정이고 같은 돌진으로 센다
    ///    (플레이어당 한 번, AttackId = 몸통 Id).
    ///  - 세 번째 활강 뒤 땅에 내린 빈틈(2.0초) 동안 비행 꼬리표를 뗀다.
    ///  - 번개 밭갈이: 전장 높이를 count(7)줄로 나눈 가로 띠. 첫 묶음은 짝수 줄 또는 홀수 줄(난수), 둘째 묶음은 나머지.
    /// </summary>
    public sealed class StormOwlBrain : BossBrainBase
    {
        public const string PatternGlide = "glide", PatternFan = "feather_fan", PatternStrike = "lightning_strike", PatternPlow = "lightning_plow";
        public const int AxisCount = 4;
        // 그림 클립 이름(monsters manifest의 boss.storm_owl, 땅 위는 boss.storm_owl.ground의 land).
        const string ClipFly = "fly", ClipDive = "dive", ClipFan = "fan", ClipWeather = "weather", ClipLand = "land", ClipPhase = "phase";

        /// <summary>활강 한 번(반복 하나)의 계획. 예고가 허가돼야 축이 확정된다.</summary>
        public sealed class GlidePlan
        {
            public int AxisA, AxisB = -1;
            public Vector2 EntryA, DirA, EntryB, DirB;
            public bool HasB;
            // 진행
            public bool BodyDone;
            public bool SecondActive;
            public Vector2 SecondPos;
        }

        int _lastAxisA = -1, _lastAxisB = -1;
        bool _plowEvenFirst;

        /// <summary>직전 활강의 축(0 가로, 1 세로, 2·3 대각). 없으면 -1.</summary>
        public int LastAxis => _lastAxisA;
        /// <summary>이번 밭갈이의 첫 묶음이 짝수 줄인가.</summary>
        public bool PlowEvenFirst => _plowEvenFirst;

        public override void Init(BossContext c)
        {
            c.SetAnim(ClipFly);
        }

        // ------------------------------------------------------------ 축

        /// <summary>축의 단위 방향: 0 가로, 1 세로, 2 대각(↗), 3 대각(↘).</summary>
        public static Vector2 AxisDir(int axis)
        {
            switch (axis)
            {
                case 0: return new Vector2(1f, 0f);
                case 1: return new Vector2(0f, 1f);
                case 2: return new Vector2(1f, 1f).normalized;
                default: return new Vector2(1f, -1f).normalized;
            }
        }

        /// <summary>수직인 축(가로 ↔ 세로, 대각 ↔ 대각).</summary>
        public static int PerpendicularAxis(int axis)
        {
            switch (axis)
            {
                case 0: return 1;
                case 1: return 0;
                case 2: return 3;
                default: return 2;
            }
        }

        /// <summary>exclude1·exclude2가 아닌 축 가운데 하나를 균등 난수로. 남는 축이 없으면 exclude1만 뺀다.</summary>
        public static int PickAxis(Pcg32 rng, int exclude1, int exclude2)
        {
            int n = 0;
            for (int a = 0; a < AxisCount; a++) if (a != exclude1 && a != exclude2) n++;
            if (n == 0) return PickAxis(rng, exclude1, -1);
            int k = rng.Range(0, n);
            for (int a = 0; a < AxisCount; a++)
            {
                if (a == exclude1 || a == exclude2) continue;
                if (k-- == 0) return a;
            }
            return 0;
        }

        /// <summary>점 through를 지나는 방향 dir의 직선이 전장에 들어오는 벽 위의 점과 전장을 가로지르는 길이.</summary>
        public static Vector2 EntryPoint(SimWorld w, Vector2 through, Vector2 dir, out float length)
        {
            float back = BossRules.DistanceToWall(w, through, -dir, 0f);
            float fwd = BossRules.DistanceToWall(w, through, dir, 0f);
            length = back + fwd;
            return through - dir * back;
        }

        /// <summary>벽 위의 점에서 dir로 나아가 몸 반지름만큼 벽 안쪽이 되는 첫 점(직선을 벗어나지 않게). 그런 점이 없으면 전장 안으로 당긴다.</summary>
        public static Vector2 InsideStart(SimWorld w, Vector2 entry, Vector2 dir, float radius)
        {
            float s = 0f;
            if (dir.x > 0f && entry.x < radius) s = Mathf.Max(s, (radius - entry.x) / dir.x);
            else if (dir.x < 0f && entry.x > w.ArenaWidth - radius) s = Mathf.Max(s, (w.ArenaWidth - radius - entry.x) / dir.x);
            if (dir.y > 0f && entry.y < radius) s = Mathf.Max(s, (radius - entry.y) / dir.y);
            else if (dir.y < 0f && entry.y > w.ArenaHeight - radius) s = Mathf.Max(s, (w.ArenaHeight - radius - entry.y) / dir.y);
            return w.ClampToArena(entry + dir * s, radius);
        }

        // ------------------------------------------------------------ 예고 구역

        public override void OnPatternStart(BossContext c, BossPattern pattern)
        {
            if (pattern.Key == PatternPlow) _plowEvenFirst = c.Rng.Range(0, 2) == 0;
        }

        public override bool BuildZones(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones)
        {
            if (c.Run == null || step.Attack == null) return false;
            string key = c.Run.Pattern.Key;
            if (key == PatternGlide && step.IsDash) { BuildGlide(c, step, inst, target, zones); return true; }
            if (key == PatternPlow) { BuildPlow(c, step, target, zones); return true; }
            return false;
        }

        void BuildGlide(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones)
        {
            var w = c.W;
            var rng = c.Rng;
            bool cross = step.Count >= 2;
            var plan = new GlidePlan();
            // 직전과 다른 축. 십자는 직전 십자의 두 축을 다 피한다(같은 십자가 이어지지 않게).
            plan.AxisA = PickAxis(rng, _lastAxisA, cross ? _lastAxisB : -1);
            plan.DirA = AxisDir(plan.AxisA) * (rng.Range(0, 2) == 0 ? 1f : -1f);
            float lengthA;
            plan.EntryA = EntryPoint(w, target.Pos, plan.DirA, out lengthA);
            zones.Add(GlideZone(step, target, plan.EntryA, plan.DirA, lengthA));
            if (cross)
            {
                plan.HasB = true;
                plan.AxisB = PerpendicularAxis(plan.AxisA);
                plan.DirB = AxisDir(plan.AxisB) * (rng.Range(0, 2) == 0 ? 1f : -1f);
                float lengthB;
                plan.EntryB = EntryPoint(w, target.Pos, plan.DirB, out lengthB);
                zones.Add(GlideZone(step, target, plan.EntryB, plan.DirB, lengthB));
            }
            inst.Scratch = plan;
        }

        /// <summary>활강 띠. 피해는 몸 충돌이 준다(띠는 길을 보여 줄 뿐 — 뼈대의 돌진 띠와 같은 규칙).</summary>
        static ZoneSpec GlideZone(BossStep step, PlayerState target, Vector2 entry, Vector2 dir, float length)
        {
            var a = step.Attack;
            return new ZoneSpec
            {
                Shape = ZoneShape.Band, Anchor = ZoneAnchor.Fixed, Pos = entry, Dir = dir, Width = a.Width, Length = length,
                TelegraphTicks = SimConst.Ticks(a.TelegraphSeconds), BaseDamage = 0, DamageSource = PlayerDamageSource.Dash,
                Grazeable = false, TargetSlot = target.Slot
            };
        }

        /// <summary>줄 i(0부터)의 가운데 높이. 전장 높이를 rows줄로 나눈다(14칸 ÷ 7 = 폭 2칸).</summary>
        public static float PlowRowCenterY(SimWorld w, int row, int rows)
        {
            return (row + row + 1) * w.ArenaHeight / (rows + rows);
        }

        void BuildPlow(BossContext c, BossStep step, PlayerState target, List<ZoneSpec> zones)
        {
            var w = c.W;
            var a = step.Attack;
            int rows = Math.Max(1, step.Count);
            // 첫 step = 첫 묶음, 그 뒤 step = 나머지 줄.
            bool even = step.Index == 0 ? _plowEvenFirst : !_plowEvenFirst;
            for (int i = 0; i < rows; i++)
            {
                if ((i % 2 == 0) != even) continue;
                zones.Add(new ZoneSpec
                {
                    Shape = ZoneShape.Band, Anchor = ZoneAnchor.Fixed, Pos = new Vector2(0f, PlowRowCenterY(w, i, rows)),
                    Dir = new Vector2(1f, 0f), Width = a.Width, Length = w.ArenaWidth,
                    TelegraphTicks = SimConst.Ticks(a.TelegraphSeconds), BaseDamage = a.Damage,
                    DamageSource = BossRules.ParseDamageSource(a.DamageSource), Grazeable = a.Grazeable, TargetSlot = target.Slot
                });
            }
        }

        public override void OnTelegraphStart(BossContext c, BossStep step, BossStepInstance inst)
        {
            var plan = inst.Scratch as GlidePlan;
            if (plan == null) return;
            // 예고가 실제로 뜬 축만 "직전 축"이 된다(R5로 다시 짠 계획은 버려진다).
            _lastAxisA = plan.AxisA;
            _lastAxisB = plan.AxisB;
        }

        // ------------------------------------------------------------ 활강

        public override BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
        {
            var plan = inst.Scratch as GlidePlan;
            if (plan == null || !step.IsDash) return BossActResult.Default;
            var body = c.Body;
            if (inst.Phase == BossStepPhase.Act)
            {
                // 판정 틱의 7단계: 띠가 들어오는 벽에서 출발한다(이 틱부터 몸이 움직인다 — 공통 규칙 4).
                body.Pos = InsideStart(c.W, plan.EntryA, plan.DirA, body.BodyRadius);
                c.Sys.StartDash(c, inst, plan.DirA, step.Attack.ActiveTicks, step.Attack.Damage);
                // 둘째 줄이 끝날 때까지 이 반복을 붙들어야 해서 뼈대의 돌진 상태 대신 두뇌가 매 틱 옮긴다.
                inst.Phase = BossStepPhase.Busy;
                plan.BodyDone = false;
                plan.SecondActive = plan.HasB;
                if (plan.HasB) plan.SecondPos = InsideStart(c.W, plan.EntryB, plan.DirB, body.BodyRadius);
                c.SetAnim(ClipDive);
            }
            if (!plan.BodyDone) plan.BodyDone = c.Sys.DashTick(c, step, inst);
            if (plan.SecondActive) StepSecond(c, step, inst, plan);
            return plan.BodyDone && !plan.SecondActive ? BossActResult.Done : BossActResult.Busy;
        }

        /// <summary>십자의 둘째 줄: 몸 없이 같은 속도·같은 반지름으로 지나가며 닿은 플레이어에게 같은 돌진의 피격 후보를 올린다.</summary>
        void StepSecond(BossContext c, BossStep step, BossStepInstance inst, GlidePlan plan)
        {
            var w = c.W;
            var body = c.Body;
            float radius = body.BodyRadius;
            float stepLen = step.Attack.DashSpeed * SimConst.TickSeconds;
            Vector2 from = plan.SecondPos;
            float toWall = BossRules.DistanceToWall(w, from, plan.DirB, radius);
            bool end = toWall <= stepLen || stepLen <= 0f;
            Vector2 to = w.ClampToArena(from + plan.DirB * (end ? toWall : stepLen), radius);

            int screenId;
            if (w.Mods.Arts.SegmentHitsScreen(w, from, to, out screenId)) w.Mods.Arts.OnEnemyTouchedScreen(w, screenId, body, true);
            plan.SecondPos = to;

            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active || p.Downed || (inst.DashHitMask & (1 << i)) != 0) continue;
                if (BossRules.PointSegmentDistance(p.Pos, from, to) >= radius + c.HitRadius(p)) continue;
                var hit = new PlayerHitRequest
                {
                    AttackId = body.Id, CastId = inst.CastId, SourceEnemyId = body.Id, Source = PlayerDamageSource.Dash, FromPos = from,
                    DamageCells = w.Mods.Threats.DamageCells(w, step.Attack.Damage, PlayerDamageSource.Dash, body)
                };
                if (w.Mods.Player.QueueHit(w, p, hit)) inst.DashHitMask |= 1 << i;
            }
            if (end) plan.SecondActive = false;
        }

        // ------------------------------------------------------------ 상태

        static EnemyTags PhaseTags(BossContext c)
        {
            var phase = c.PhaseDef;
            return (phase != null ? EnemyTagsUtil.Parse(phase.Tags) : EnemyTags.None) | EnemyTags.Boss;
        }

        public override void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState)
        {
            // 활강 뒤 땅에 내린 빈틈 동안만 비행을 뗀다(05 요약서 9.4절 [임시 U35]). 빈틈이 끝나면 단계의 꼬리표로 돌아간다.
            if (oldState == BossFightState.Recovery) c.Body.Tags = PhaseTags(c);
            switch (newState)
            {
                case BossFightState.Waiting:
                case BossFightState.R5Blocked:
                    c.SetAnim(ClipFly);
                    break;
                case BossFightState.Telegraph:
                {
                    string p = c.Boss.CurrentPattern;
                    c.SetAnim(p == PatternGlide ? ClipFly : p == PatternFan ? ClipFan : ClipWeather);
                    break;
                }
                case BossFightState.Recovery:
                    if (c.Boss.LastPattern == PatternGlide)
                    {
                        c.Body.Tags &= ~EnemyTags.Flying;
                        c.SetAnim(ClipLand);
                    }
                    else c.SetAnim(ClipFly);
                    break;
                case BossFightState.PhaseTransition:
                    c.SetAnim(ClipPhase);
                    break;
            }
        }
    }
}
