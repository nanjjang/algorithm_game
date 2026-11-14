using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Sim.Threats
{
    // 적 탄과 스침 후보. 05 요약서 0절(탄 상한 150), 5절(포물선·병풍), 02 요약서 7.1절(스침 판정).
    public sealed partial class ThreatSystem
    {
        public override EnemyShotEntity SpawnShot(SimWorld w, in EnemyShotSpec spec)
        {
            var cfg = Data(w).Config;
            if (cfg.EnemyShotCap > 0)
            {
                int alive = 0;
                for (int i = 0; i < w.EnemyShots.Count; i++) if (!w.EnemyShots[i].Dead) alive++;
                if (alive >= cfg.EnemyShotCap) return null;   // 상한이면 새 탄을 만들지 않는다
            }

            var s = new EnemyShotEntity
            {
                Id = w.NewEntityId(),
                CastId = spec.CastId,
                OwnerEnemyId = spec.OwnerEnemyId,
                Pos = spec.Pos,
                PrevPos = spec.Pos,
                Radius = spec.Radius,
                BaseDamage = spec.BaseDamage,
                Grazeable = spec.Grazeable,
                Lob = spec.Lob,
                BouncesLeft = spec.Bounces,
                SpriteKey = spec.SpriteKey,
                SpawnTick = w.Tick,
            };
            if (spec.Lob)
            {
                // 포물선: 만든 틱부터 착지 틱까지 매 틱 같은 걸음으로 가서 착지 틱에 목표에 닿는다. 느린 탄막 배율을 받지 않는다
                // (비행 시간이 예고 길이에 묶여 있다).
                int flight = spec.LobFlightTicks > 0 ? spec.LobFlightTicks : 0;
                s.LandTick = w.Tick + flight;
                s.Vel = (spec.LobTarget - spec.Pos) / ((flight + 1) * SimConst.TickSeconds);
                s.RangeLeft = -1f;
            }
            else
            {
                float mul = w.Wave != null && w.Wave.EnemyShotSpeedMul > 0f ? w.Wave.EnemyShotSpeedMul : 1f;
                s.Vel = ThreatGeometry.NormalizeOr(spec.Dir, new Vector2(1, 0)) * (spec.Speed * mul);
                s.RangeLeft = spec.Range > 0f ? spec.Range : -1f;   // 0 이하 = 벽까지
            }
            w.EnemyShots.Add(s);
            w.Emit(SimEventType.EnemyShotFired, entityId: s.Id, a: s.OwnerEnemyId, pos: s.Pos, id: s.SpriteKey);
            return s;
        }

        public override void Stage9_Shots(SimWorld w)
        {
            var d = Data(w);
            int count = w.EnemyShots.Count;
            for (int i = 0; i < count; i++)
            {
                var s = w.EnemyShots[i];
                if (s.Dead) continue;
                s.PrevPos = s.Pos;
                Vector2 step = s.Vel * SimConst.TickSeconds;

                if (s.Lob)
                {
                    // 설치 벽과 장애물을 넘는다. 떨어지는 틱에만 판정한다.
                    s.Pos += step;
                    if (w.Tick >= s.LandTick)
                    {
                        TryHitPlayers(w, d, s, s.Pos, s.Pos);
                        s.Dead = true;
                    }
                    continue;
                }

                float dist = step.magnitude;
                bool spent = false;
                if (s.RangeLeft >= 0f)
                {
                    if (dist >= s.RangeLeft)
                    {
                        step = dist > 0f ? step * (s.RangeLeft / dist) : Vector2.zero;
                        dist = s.RangeLeft;
                        spent = true;
                    }
                    s.RangeLeft -= dist;
                }
                Vector2 target = s.Pos + step;

                // 얼음 병풍: 직선 탄은 막힌다(스침 아님).
                int screenId;
                if (dist > 0f && w.Mods.Arts.SegmentHitsScreen(w, s.Pos, target, out screenId))
                {
                    s.Blocked = true;
                    s.Dead = true;
                    w.Emit(SimEventType.EnemyShotBlocked, entityId: s.Id, a: screenId, pos: s.Pos);
                    continue;
                }

                // 장애물: 닿는 데까지만 간다(그 앞에 선 플레이어는 맞는다).
                ObstacleEntity wall;
                if (dist > 0f && NearestObstacle(w, s.Pos, target, false, out wall, out float at))
                {
                    Vector2 stop = s.Pos + step * at;
                    bool hitFirst = TryHitPlayers(w, d, s, s.Pos, stop);
                    s.Pos = stop;
                    s.Dead = true;
                    if (!hitFirst) w.Emit(SimEventType.EnemyShotBlocked, entityId: s.Id, pos: stop);
                    continue;
                }

                // 전장 벽: 튕김이 남았으면 되튀고, 아니면 사라진다.
                bool outX = target.x < 0f || target.x > w.ArenaWidth;
                bool outY = target.y < 0f || target.y > w.ArenaHeight;
                if (outX || outY)
                {
                    if (s.BouncesLeft > 0)
                    {
                        s.BouncesLeft--;
                        if (outX)
                        {
                            target.x = target.x < 0f ? -target.x : 2 * w.ArenaWidth - target.x;
                            s.Vel.x = -s.Vel.x;
                        }
                        if (outY)
                        {
                            target.y = target.y < 0f ? -target.y : 2 * w.ArenaHeight - target.y;
                            s.Vel.y = -s.Vel.y;
                        }
                    }
                    else
                    {
                        Vector2 edge = w.ClampToArena(target, 0f);
                        TryHitPlayers(w, d, s, s.Pos, edge);
                        s.Pos = edge;
                        s.Dead = true;
                        continue;
                    }
                }

                s.Pos = target;
                if (TryHitPlayers(w, d, s, s.PrevPos, s.Pos)) s.Dead = true;
                else if (spent) s.Dead = true;
            }
        }

        /// <summary>탄이 이번 틱에 지나간 선분이 플레이어 판정원에 닿았으면 피격 후보를 올린다(슬롯 순, 한 사람만).
        /// 후보로 올라갔을 때만 true(무적 중인 사람은 탄이 지나간다).</summary>
        bool TryHitPlayers(SimWorld w, ThreatWorldData d, EnemyShotEntity s, Vector2 a, Vector2 b)
        {
            if (s.BaseDamage <= 0) return false;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!CanBeHit(p)) continue;
                if (ThreatGeometry.DistanceToSegment(p.Pos, a, b) >= s.Radius + HitRadius(d.Config, p)) continue;
                EnemyState owner = s.OwnerEnemyId != 0 ? w.FindEnemy(s.OwnerEnemyId) : null;
                var hit = new PlayerHitRequest
                {
                    AttackId = s.Id,
                    CastId = s.CastId,
                    SourceEnemyId = s.OwnerEnemyId,
                    DamageCells = DamageCells(w, s.BaseDamage, PlayerDamageSource.Projectile, owner),
                    Source = PlayerDamageSource.Projectile,
                    FromPos = s.PrevPos,
                };
                if (w.Mods.Player.QueueHit(w, p, hit)) return true;
            }
            return false;
        }

        public override int ClearShots(SimWorld w, Vector2 center, float radius)
        {
            int n = 0;
            for (int i = 0; i < w.EnemyShots.Count; i++)
            {
                var s = w.EnemyShots[i];
                if (s.Dead) continue;
                if (radius > 0f && (s.Pos - center).magnitude > radius) continue;
                s.Dead = true;
                n++;
            }
            if (n > 0) w.Emit(SimEventType.EnemyShotsCleared, a: n, f: radius > 0f ? radius : 0f, pos: center);
            return n;
        }

        // ------------------------------------------------------------ 스침 후보 (02 요약서 7.1절)

        public override void CollectGraze(SimWorld w, PlayerState p, List<GrazeCandidate> into)
        {
            if (into == null || p == null) return;
            var cfg = Data(w).Config;
            float hitR = HitRadius(cfg, p);
            float grazeR = GrazeRadius(cfg, p);

            // 탄: 겉면까지 d − r. 병풍이 막은 탄·사라진 탄은 넣지 않는다(포물선은 떨어진 틱에만).
            for (int i = 0; i < w.EnemyShots.Count; i++)
            {
                var s = w.EnemyShots[i];
                if (!s.Grazeable || s.Blocked || s.BaseDamage <= 0) continue;
                if (s.Lob ? w.Tick != s.LandTick : s.Dead) continue;
                float surface = (p.Pos - s.Pos).magnitude - s.Radius;
                if (surface <= hitR || surface > grazeR) continue;
                into.Add(new GrazeCandidate { CastId = s.CastId, Kind = GrazeKind.Bullet, SurfaceDistance = surface, Pos = s.Pos, ThreatId = s.Id });
            }

            // 돌진 중인 적의 몸
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (!e.DashActive || !e.Alive) continue;
                float surface = (p.Pos - e.Pos).magnitude - e.BodyRadius;
                if (surface <= hitR || surface > grazeR) continue;
                into.Add(new GrazeCandidate { CastId = e.DashCastId, Kind = GrazeKind.Dash, SurfaceDistance = surface, Pos = e.Pos, ThreatId = e.Id });
            }

            // 이번 틱에 터진 구역: 플레이어가 밖에 있을 때 경계까지의 거리
            for (int i = 0; i < w.Zones.Count; i++)
            {
                var z = w.Zones[i];
                if (!z.Detonated || z.Cancelled || !z.Grazeable || z.Harmless || z.BaseDamage <= 0) continue;
                float surface = ThreatGeometry.Distance(ShapeGeom.Of(z), p.Pos);
                if (surface <= hitR || surface > grazeR) continue;
                into.Add(new GrazeCandidate { CastId = z.CastId, Kind = GrazeKind.Zone, SurfaceDistance = surface, Pos = z.Pos, ThreatId = z.Id });
            }
        }
    }
}
