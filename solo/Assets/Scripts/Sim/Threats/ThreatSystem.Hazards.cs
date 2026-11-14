using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Threats
{
    // 남는 구역(가시밭, 불씨 띠, 포자 안개, 서리밭 …)과 장애물. 05 요약서 1.2절·4절·6.10절·8.5절, 00 문서 D-63.
    public sealed partial class ThreatSystem
    {
        const float PercentToRatio = 0.01f;   // 단위 환산(% → 비율)

        // ------------------------------------------------------------ 9단계(구역 쪽 전부)

        public override void Stage9_Zones(SimWorld w)
        {
            var d = Data(w);
            StepObstacles(w);
            StepHazards(w, d);   // 이번 틱에 새로 생기는 남는 구역은 다음 틱부터 닿는다
            StepZones(w, d);
        }

        // ------------------------------------------------------------ 남는 구역

        public override HazardEntity SpawnHazard(SimWorld w, in HazardSpec spec)
        {
            var d = Data(w);
            var cfg = d.Config;
            var h = new HazardEntity
            {
                Id = w.NewEntityId(),
                Kind = spec.Kind,
                OwnerEnemyId = spec.OwnerEnemyId,
                Shape = spec.Shape,
                Pos = spec.Pos,
                Dir = ThreatGeometry.NormalizeOr(spec.Dir, new Vector2(1, 0)),
                Radius = spec.Radius,
                Width = spec.Width,
                Length = spec.Length,
                StartTick = w.Tick,
                UntilTick = spec.DurationTicks > 0 ? w.Tick + spec.DurationTicks : int.MaxValue,   // 0 이하 = 웨이브 끝까지
                // 피해 칸을 비워 둔 위험 구역은 접촉 피해 값(waves.json damage.contact)으로 센다.
                BaseDamage = spec.SlowOnly ? 0 : (spec.BaseDamage > 0 ? spec.BaseDamage : cfg.ContactDamage),
                ContactCooldownTicks = spec.ContactCooldownTicks > 0 ? spec.ContactCooldownTicks : cfg.ContactIntervalTicks,
                ContactReadyTick = w.Tick,
                SlowOnly = spec.SlowOnly,
                SlowPercent = spec.SlowPercent,
            };
            w.Hazards.Add(h);
            w.Emit(SimEventType.HazardSpawned, entityId: h.Id, a: (int)h.Kind, pos: h.Pos);

            // 같은 종류의 전장 동시 개수 상한: 넘으면 가장 오래된 것부터 지운다(05 요약서 6.10절 "전장 동시 3개").
            if (spec.MaxCount > 0)
            {
                int alive = 0;
                for (int i = 0; i < w.Hazards.Count; i++)
                    if (!w.Hazards[i].Dead && w.Hazards[i].Kind == h.Kind) alive++;
                for (int i = 0; i < w.Hazards.Count && alive > spec.MaxCount; i++)
                {
                    var old = w.Hazards[i];
                    if (old.Dead || old.Kind != h.Kind) continue;
                    KillHazard(w, d, old);
                    alive--;
                }
            }
            return h;
        }

        public override void RemoveHazard(SimWorld w, HazardEntity h)
        {
            if (h == null || h.Dead) return;
            KillHazard(w, Data(w), h);
        }

        public override void MoveHazard(SimWorld w, HazardEntity h, Vector2 pos)
        {
            if (h != null && !h.Dead) h.Pos = pos;
        }

        public override void DestroyObstacle(SimWorld w, ObstacleEntity o, bool broken)
        {
            if (o != null && !o.Dead) KillObstacle(w, o, broken);
        }

        static void KillHazard(SimWorld w, ThreatWorldData d, HazardEntity h)
        {
            if (h.Dead) return;
            h.Dead = true;
            d.HazardWarned.Remove(h.Id);
            w.Emit(SimEventType.HazardExpired, entityId: h.Id, pos: h.Pos);
        }

        public static HazardKind ParseHazardKind(string kind)
        {
            switch (kind)
            {
                case "ember_strip": return HazardKind.EmberStrip;
                case "spore_cloud": return HazardKind.SporeCloud;
                case "frost_field": return HazardKind.FrostField;
                case "thorn_border": return HazardKind.ThornBorder;
                case "death_circle": return HazardKind.DeathCircle;
                default: return HazardKind.ThornField;
            }
        }

        /// <summary>구역이 터진 자리에 남는 구역의 모양: 정의에 반지름이 있으면 그 원, 없으면 구역과 같은 모양.</summary>
        static ShapeGeom LeftHazardGeom(in ShapeGeom zone, HazardDef def)
        {
            if (def.Radius <= 0f) return zone;
            return new ShapeGeom { Shape = ZoneShape.Circle, Pos = zone.Pos, Dir = zone.Dir, Radius = def.Radius };
        }

        void SpawnLeftHazard(SimWorld w, ZoneEntity z, in ShapeGeom zone)
        {
            var def = z.LeavesHazard;
            var g = LeftHazardGeom(zone, def);
            var kind = ParseHazardKind(def.Kind);
            var spec = new HazardSpec
            {
                Kind = kind,
                OwnerEnemyId = z.OwnerEnemyId,
                Shape = g.Shape,
                Pos = g.Pos,
                Dir = g.Dir,
                Radius = g.Radius,
                Width = g.Width,
                Length = g.Length,
                DurationTicks = SimConst.Ticks(def.DurationSeconds),
                ContactCooldownTicks = SimConst.Ticks(def.ContactCooldownSeconds),
                SlowOnly = kind == HazardKind.FrostField,
                MaxCount = def.MaxCount,
            };
            SpawnHazard(w, spec);
        }

        float SlowRatio(ThreatConfig cfg, HazardEntity h)
        {
            if (h.SlowPercent <= 0f) return cfg.FrostSlowRatio;                 // 값을 비워 두면 서리밭 기본값
            return h.SlowPercent > 1f ? h.SlowPercent * PercentToRatio : h.SlowPercent;   // 30 과 0.3 둘 다 받는다
        }

        void StepHazards(SimWorld w, ThreatWorldData d)
        {
            var cfg = d.Config;
            for (int s = 0; s < w.Players.Length; s++) w.Players[s].EnvSpeedMul = 1f;

            int count = w.Hazards.Count;
            for (int i = 0; i < count; i++)
            {
                var h = w.Hazards[i];
                if (h.Dead) continue;
                if (w.Tick >= h.UntilTick) { KillHazard(w, d, h); continue; }
                if (h.UntilTick != int.MaxValue && cfg.HazardExpireWarnTicks > 0
                    && w.Tick >= h.UntilTick - cfg.HazardExpireWarnTicks && d.HazardWarned.Add(h.Id))
                    w.Emit(SimEventType.HazardExpiring, entityId: h.Id, pos: h.Pos);

                var g = ShapeGeom.Of(h);
                if (h.SlowOnly)
                {
                    // 서리밭: 판정 중심이 안에 있는 동안만 느려진다. 여러 개가 겹쳐도 가장 큰 감속 하나.
                    float mul = 1f - SlowRatio(cfg, h);
                    for (int s = 0; s < w.Players.Length; s++)
                    {
                        var p = w.Players[s];
                        if (!p.Active) continue;
                        if (ThreatGeometry.Distance(g, p.Pos) <= 0f && mul < p.EnvSpeedMul) p.EnvSpeedMul = mul;
                    }
                    continue;
                }

                // 접촉 규칙: 구역마다 1.5초에 1회. 후보로 올라갔을 때만(QueueHit = true) 쿨다운을 쓴다.
                if (h.BaseDamage <= 0 || w.Tick < h.ContactReadyTick) continue;
                EnemyState owner = h.OwnerEnemyId != 0 ? w.FindEnemy(h.OwnerEnemyId) : null;
                for (int s = 0; s < w.Players.Length; s++)
                {
                    var p = w.Players[s];
                    if (!CanBeHit(p)) continue;
                    if (ThreatGeometry.Distance(g, p.Pos) >= HitRadius(cfg, p)) continue;
                    var hit = new PlayerHitRequest
                    {
                        AttackId = h.Id,
                        SourceEnemyId = h.OwnerEnemyId,
                        DamageCells = DamageCells(w, h.BaseDamage, PlayerDamageSource.Lingering, owner),
                        Source = PlayerDamageSource.Lingering,
                        FromPos = h.Pos,
                    };
                    if (!w.Mods.Player.QueueHit(w, p, hit)) continue;
                    h.ContactReadyTick = w.Tick + h.ContactCooldownTicks;
                    break;
                }
            }
        }

        // ------------------------------------------------------------ 장애물

        public override ObstacleEntity SpawnObstacle(SimWorld w, in ObstacleSpec spec)
        {
            var o = new ObstacleEntity
            {
                Id = w.NewEntityId(),
                Kind = spec.Kind,
                Pos = spec.Pos,
                Radius = spec.Radius,
                Hp = spec.Hp,
                UntilTick = spec.DurationTicks > 0 ? w.Tick + spec.DurationTicks : int.MaxValue,   // 0 = 무한
                BlocksPlayerShots = spec.BlocksPlayerShots,
                BlocksEnemyShots = spec.BlocksEnemyShots,
            };
            w.Obstacles.Add(o);
            w.Emit(SimEventType.ObstacleSpawned, entityId: o.Id, pos: o.Pos, id: o.Kind);
            return o;
        }

        static void KillObstacle(SimWorld w, ObstacleEntity o, bool broken)
        {
            if (o.Dead) return;
            o.Dead = true;
            w.Emit(SimEventType.ObstacleDestroyed, entityId: o.Id, a: broken ? 1 : 0, pos: o.Pos, id: o.Kind);
        }

        void StepObstacles(SimWorld w)
        {
            for (int i = 0; i < w.Obstacles.Count; i++)
            {
                var o = w.Obstacles[i];
                if (!o.Dead && w.Tick >= o.UntilTick) KillObstacle(w, o, broken: false);
            }
        }

        public override bool SegmentHitsObstacle(SimWorld w, Vector2 a, Vector2 b, bool forPlayerShot, out ObstacleEntity hit)
        {
            return NearestObstacle(w, a, b, forPlayerShot, out hit, out _);
        }

        /// <summary>선분이 닿는 장애물 중 출발점에 가장 가까운 것. at = 선분 위에서 장애물 겉에 닿는 위치(0~1).</summary>
        static bool NearestObstacle(SimWorld w, Vector2 a, Vector2 b, bool forPlayerShot, out ObstacleEntity hit, out float at)
        {
            hit = null;
            at = 1f;
            for (int i = 0; i < w.Obstacles.Count; i++)
            {
                var o = w.Obstacles[i];
                if (o.Dead) continue;
                if (forPlayerShot ? !o.BlocksPlayerShots : !o.BlocksEnemyShots) continue;
                float enter;
                if (!ThreatGeometry.SegmentEntersCircle(a, b, o.Pos, o.Radius, out enter)) continue;
                if (hit == null || enter < at) { hit = o; at = enter; }
            }
            return hit != null;
        }

        public override void DamageObstacle(SimWorld w, ObstacleEntity o, float amount)
        {
            if (o == null || o.Dead || o.Hp <= 0f || amount <= 0f) return;   // Hp 0 = 플레이어 탄으로 안 깨진다
            o.Hp -= amount;
            if (o.Hp <= 0f) KillObstacle(w, o, broken: true);
        }
    }
}
