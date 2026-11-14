using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 천 갓(boss.spore_queen, 05 요약서 9.3절). 고리 탄·구름 예고·도약 예고의 차례와 틱은 뼈대가 데이터대로 실행하고, 여기서는
    ///  - 1단계의 뿌리: 전장 가운데에서 anchor_radius(1칸) 안으로만 움직인다. 2단계(뿌리 뽑음)는 그냥 쫓는다.
    ///  - 포자 구름: 포자 안개가 동시에 max_count(6)개까지 — 넘는 새 구름은 생략하고 자리가 없으면 고를 수 없다.
    ///    남는 안개는 구역의 leaves_hazard로 Threats가 깐다(R5도 그 값으로 본다). 2회차에는 안개가 플레이어 쪽으로 흐른다.
    ///  - 새싹 부르기: 금 4곳을 패턴 시작 틱에 정해 알리고(EnemySpawnNotice), 1.0초 뒤 그 자리에 부른다. 전장에 field_cap(8)마리까지 —
    ///    이 보스가 부른 것(SummonerId)만 세고, 넘는 만큼 생략하며 0마리면 고를 수 없다.
    ///  - 도약: 그림자 원이 터지는 틱에 그 자리로 내려앉고, 이어지는 8방향 탄은 내려앉은 자리에서 나간다.
    /// </summary>
    public sealed class SporeQueenBrain : BossBrainBase
    {
        public const string PatternRing = "spore_ring", PatternCloud = "spore_cloud", PatternSprouts = "summon_sprouts", PatternLeap = "leap";
        /// <summary>2회차 덮어쓰기 열쇠(bosses.json second_cycle): 포자 안개가 흐르는 속도(칸/초).</summary>
        public const string DriftKey = "hazard_drift_speed";
        public const string ExtraName = "G08", ExtraSection = "spore_queen";
        // 그림 클립 이름(monsters manifest의 boss.spore_queen).
        const string ClipWalk = "walk", ClipRing = "ring", ClipCloud = "cloud", ClipCall = "call", ClipJump = "jump", ClipOpen = "open",
            ClipPhase = "phase";

        // 보충 수치(G08.json). 처음 값은 조각을 못 읽었을 때 쓰고 조각과 같다.
        public float SproutMinDistance = 2f, SproutMaxDistance = 4f, SproutMinSpacing = 1.5f;
        public int SproutMaxTries = 32;

        Vector2 _anchor;
        readonly List<Vector2> _sproutSpots = new List<Vector2>();

        /// <summary>이번 새싹 부르기에서 금이 간 자리(패턴 시작 틱에 정한다).</summary>
        public IReadOnlyList<Vector2> SproutSpots => _sproutSpots;

        public override void Init(BossContext c)
        {
            JObject extra = c.W.Content != null ? c.W.Content.SoloExtra(ExtraName) : null;
            JObject j = extra != null ? extra[ExtraSection] as JObject : null;
            SproutMinDistance = BossTuning.Float(j, "sprout_min_distance", SproutMinDistance);
            SproutMaxDistance = BossTuning.Float(j, "sprout_max_distance", SproutMaxDistance);
            SproutMinSpacing = BossTuning.Float(j, "sprout_min_spacing", SproutMinSpacing);
            SproutMaxTries = BossTuning.Int(j, "sprout_max_tries", SproutMaxTries);

            _anchor = new Vector2(c.W.ArenaWidth / 2f, c.W.ArenaHeight / 2f);
            // 뿌리 내린 단계로 시작하면 전장 가운데에 선다(1단계는 중앙 반경 1칸 안에서만 움직인다).
            if (AnchorRadius(c) > 0f)
            {
                c.Body.Pos = c.W.ClampToArena(_anchor, c.Body.BodyRadius);
                c.Body.PrevPos = c.Body.Pos;
                var near = c.W.NearestPlayer(c.Body.Pos);
                if (near != null) c.Body.Facing = c.DirTo(near.Pos);
            }
            c.SetAnim(ClipWalk);
        }

        static float AnchorRadius(BossContext c)
        {
            var phase = c.PhaseDef;
            return phase != null ? phase.AnchorRadius : 0f;
        }

        public override void Move(BossContext c, PlayerState target)
        {
            var s = c.Boss.State;
            if (s != BossFightState.Waiting && s != BossFightState.R5Blocked) return;
            float anchorRadius = AnchorRadius(c);
            if (anchorRadius <= 0f)
            {
                c.Chase(target.Pos, c.Def.MoveSpeed);
                return;
            }
            // 플레이어 쪽으로 가되 가운데에서 anchor_radius를 넘지 않는다.
            Vector2 off = target.Pos - _anchor;
            float d = off.magnitude;
            if (d > anchorRadius) off = off / d * anchorRadius;
            c.Chase(_anchor + off, c.Def.MoveSpeed);
        }

        // ------------------------------------------------------------ 고르기

        public override bool Eligible(BossContext c, BossPattern pattern)
        {
            if (pattern.Key == PatternCloud) return CloudRoom(c, pattern) > 0;
            if (pattern.Key == PatternSprouts) return SproutRoom(c, pattern) > 0;
            return true;
        }

        /// <summary>전장에 살아 있는 포자 안개 수.</summary>
        public static int CountClouds(SimWorld w)
        {
            int n = 0;
            for (int i = 0; i < w.Hazards.Count; i++)
                if (!w.Hazards[i].Dead && w.Hazards[i].Kind == HazardKind.SporeCloud) n++;
            return n;
        }

        /// <summary>새로 깔 수 있는 구름 수(상한 − 지금 수). 상한이 없으면 step의 count.</summary>
        public static int CloudRoom(BossContext c, BossPattern pattern)
        {
            if (pattern == null || pattern.Steps.Count == 0) return 0;
            var step = pattern.Steps[0];
            var hz = step.Attack != null ? step.Attack.LeavesHazard : null;
            int want = Math.Max(1, step.Count);
            if (hz == null || hz.MaxCount <= 0) return want;
            return Math.Max(0, Math.Min(want, hz.MaxCount - CountClouds(c.W)));
        }

        /// <summary>지금 부를 수 있는 새싹 수(field_cap − 이 보스가 부른 살아 있는 새싹). 상한이 없으면 count.</summary>
        public static int SproutRoom(BossContext c, BossPattern pattern)
        {
            var summon = SummonOf(pattern);
            if (summon == null) return 0;
            if (summon.FieldCap <= 0) return summon.Count;
            return Math.Max(0, Math.Min(summon.Count, summon.FieldCap - c.CountSummoned(summon.Enemy)));
        }

        static BossSummonDef SummonOf(BossPattern pattern)
        {
            if (pattern == null) return null;
            for (int i = 0; i < pattern.Steps.Count; i++)
                if (pattern.Steps[i].Def.Summon != null) return pattern.Steps[i].Def.Summon;
            return null;
        }

        // ------------------------------------------------------------ 패턴

        public override void OnPatternStart(BossContext c, BossPattern pattern)
        {
            if (pattern.Key != PatternSprouts) return;
            // 금이 가는 자리는 패턴 시작 틱에 정한다(1.0초 동안 보인다).
            var summon = SummonOf(pattern);
            _sproutSpots.Clear();
            int count = SproutRoom(c, pattern);
            if (summon == null || count <= 0) return;
            var def = c.W.Content != null ? c.W.Content.Enemy(summon.Enemy) : null;
            float radius = def != null ? def.BodyRadius : 0f;
            PickSproutSpots(c, count, radius);
            int warn = SimConst.Ticks(summon.WarnSeconds);
            for (int i = 0; i < _sproutSpots.Count; i++)
                c.W.Emit(SimEventType.EnemySpawnNotice, a: warn, pos: _sproutSpots[i], id: summon.Enemy);
        }

        void PickSproutSpots(BossContext c, int count, float radius)
        {
            var w = c.W;
            var rng = c.Rng;
            int tries = Math.Max(1, SproutMaxTries);
            for (int i = 0; i < count; i++)
            {
                bool placed = false;
                for (int t = 0; t < tries && !placed; t++)
                {
                    Vector2 dir = BossRules.Rotate(new Vector2(1f, 0f), rng.Range(0f, BossContext.FullTurnDegrees));
                    Vector2 pos = c.Body.Pos + dir * rng.Range(SproutMinDistance, SproutMaxDistance);
                    if (!w.InArena(pos, radius)) continue;
                    bool ok = true;
                    for (int k = 0; k < _sproutSpots.Count && ok; k++)
                        if ((_sproutSpots[k] - pos).magnitude < SproutMinSpacing) ok = false;
                    if (!ok) continue;
                    _sproutSpots.Add(pos);
                    placed = true;
                }
                if (placed) continue;
                // 자리를 못 찾으면 몸 둘레에 고르게 둔다(전장 안으로 당긴다).
                Vector2 ring = BossRules.Rotate(new Vector2(1f, 0f), BossContext.FullTurnDegrees * i / count);
                _sproutSpots.Add(w.ClampToArena(c.Body.Pos + ring * SproutMinDistance, radius));
            }
        }

        public override bool BuildZones(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones)
        {
            if (c.Run == null || c.Run.Pattern.Key != PatternCloud) return false;
            // 기본 배치(1개는 플레이어 위치, 나머지는 서로·플레이어와 4.0 이상)에서 상한을 넘는 뒤쪽 구름을 뺀다.
            c.DefaultZones(step, inst, target, zones);
            int room = CloudRoom(c, c.Run.Pattern);
            if (zones.Count > room) zones.RemoveRange(room, zones.Count - room);
            return true;
        }

        public override BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
        {
            if (step.Attack == null && step.Def.Summon != null)
            {
                SpawnSprouts(c, step.Def.Summon);
                return BossActResult.Done;
            }
            if (c.Run != null && c.Run.Pattern.Key == PatternLeap && step.Index == 0 && inst.Phase == BossStepPhase.Act && inst.Zones.Count > 0)
            {
                // 착지: 그림자 원의 자리로 내려앉는다. 판정은 Threats(같은 틱 9단계), 8방향 탄은 다음 step이 이 자리에서 쏜다.
                c.Body.Pos = c.W.ClampToArena(inst.Zones[0].Pos, c.Body.BodyRadius);
                c.SetAnim(ClipOpen);
            }
            return BossActResult.Default;
        }

        /// <summary>금이 간 자리에 새싹을 부른다(예고가 이미 끝났으므로 바로 나온다). 그 사이 상한이 찼으면 넘는 만큼 생략.</summary>
        void SpawnSprouts(BossContext c, BossSummonDef summon)
        {
            var w = c.W;
            int room = summon.FieldCap > 0 ? summon.FieldCap - c.CountSummoned(summon.Enemy) : _sproutSpots.Count;
            int count = Math.Min(_sproutSpots.Count, Math.Max(0, room));
            for (int i = 0; i < count; i++)
            {
                var spec = new EnemySpawnSpec
                {
                    DefId = summon.Enemy, Pos = _sproutSpots[i], NoReward = true, SummonerId = c.Body.Id, Instant = true
                };
                var e = w.Mods.Enemies.Spawn(w, spec);
                if (e == null) continue;
                w.Emit(SimEventType.BossMinionSpawned, entityId: e.Id, pos: spec.Pos, id: summon.Enemy);
            }
            _sproutSpots.Clear();
        }

        // ------------------------------------------------------------ 상시

        public override void Always(BossContext c, PlayerState target, bool halted)
        {
            // 2회차: 이 보스의 포자 안개가 플레이어 쪽으로 흐른다(05 요약서 9.3절). R5는 검사 때의 지금 위치로 본다.
            var cloud = c.Pattern(PatternCloud);
            float speed = cloud != null && cloud.Steps.Count > 0 ? cloud.Steps[0].Float(DriftKey, 0f) : 0f;
            if (speed <= 0f) return;
            float step = speed * SimConst.TickSeconds;
            var hazards = c.W.Hazards;
            for (int i = 0; i < hazards.Count; i++)
            {
                var h = hazards[i];
                if (h.Dead || h.Kind != HazardKind.SporeCloud || h.OwnerEnemyId != c.Body.Id) continue;
                Vector2 d = target.Pos - h.Pos;
                float dist = d.magnitude;
                // 남는 구역의 주인은 Threats지만 옮기는 메서드가 계약에 없어 자리만 여기서 바꾼다(결과 보고의 계약 추가 요청).
                c.W.Mods.Threats.MoveHazard(c.W, h, dist <= step ? target.Pos : h.Pos + d / dist * step);
            }
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
                {
                    string p = c.Boss.CurrentPattern;
                    c.SetAnim(p == PatternRing ? ClipRing : p == PatternCloud ? ClipCloud : p == PatternSprouts ? ClipCall : ClipJump);
                    break;
                }
                case BossFightState.Recovery:
                    c.SetAnim(ClipOpen);
                    break;
                case BossFightState.PhaseTransition:
                    c.SetAnim(ClipPhase);
                    break;
            }
        }

        public override void OnFightEnd(BossContext c)
        {
            _sproutSpots.Clear();
        }
    }
}
