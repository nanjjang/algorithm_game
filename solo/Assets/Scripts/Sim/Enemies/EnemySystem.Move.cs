using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Enemies
{
    // 9단계: 적 이동(감속·병풍·장애물), 돌진과 몸 충돌, 접촉 피해 후보. 05 요약서 4절·5절.
    public sealed partial class EnemySystem
    {
        /// <summary>벽에 닿았다고 보는 여유(실수 오차).</summary>
        const float WallEps = 1e-4f;

        public override void Stage9_Move(SimWorld w)
        {
            if (!w.CombatActive) return;
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var e = w.Enemies[i];
                // 죽어 가는 적은 이동·접촉·돌진이 없다(공통 규칙 2). 보스 몸통은 Bosses가 옮긴다.
                if (!e.Alive || e.IsBoss || e.DuelProxyOf >= 0) continue;
                var b = e.Brain as EnemyBrain;
                if (b == null) continue;
                e.PrevPos = e.Pos;
                // 7단계에 청한 예고가 8단계에 허가됐으면 같은 틱에 WindUp으로(시계는 허가 틱부터).
                if (b.HasTicket && !b.Granted) PollGrant(w, e, b);
                if (b.Species == EnemySpecies.Dummy || e.Ai == EnemyAiState.Spawning) continue;
                // 대상이 없으면 가만히(공통 규칙 3).
                if (w.NearestPlayer(e.Pos) == null) continue;

                bool canAct = e.CanAct(w.Tick);
                if (e.DashActive)
                {
                    if (canAct) MoveDash(w, e, b);
                }
                else if (canAct && b.Vel != Vector2.zero && e.Ai != EnemyAiState.WindUp && e.Ai != EnemyAiState.Recovery)
                {
                    MoveWalk(w, e, b);
                }
                if (e.Alive) Contact(w, e, b);
            }
        }

        void MoveWalk(SimWorld w, EnemyState e, EnemyBrain b)
        {
            // 감속 = 1 − 실효 감속(가장 큰 값 하나). 회피 대시에는 걸지 않는다.
            float mul = b.VelIsWalk ? 1f - e.SlowStrength(w.Tick) : 1f;
            if (mul <= 0f) return;
            Vector2 from = e.Pos;
            Vector2 to = from + b.Vel * (mul * SimConst.TickSeconds);
            bool flying = e.Has(EnemyTags.Flying);
            if (!flying && !e.Burrowed)
            {
                // 얼음 병풍: 못 지나간다(중량은 부순다). 비행은 넘어간다.
                int zoneId;
                if (w.Mods.Arts.SegmentHitsScreen(w, from, to, out zoneId))
                {
                    bool broke = w.Mods.Arts.OnEnemyTouchedScreen(w, zoneId, e, false);
                    if (!broke) to = from;
                    if (!e.Alive) return;
                }
                to = SlideObstacles(w, e, to);
            }
            to = w.ClampToArena(to, e.BodyRadius);
            e.Pos = to;
            if (b.Species == EnemySpecies.Bat)
            {
                // 가상점 A는 실제로 나아간 만큼만 전진한다(05 요약서 6.8절).
                float forward = Vector2.Dot(to - from, b.WaveDir);
                if (forward > 0f) b.WavePoint += b.WaveDir * forward;
            }
        }

        /// <summary>장애물 미끄러짐(길찾기 없음, U-26): 겹치면 장애물 겉으로 밀어낸다.</summary>
        static Vector2 SlideObstacles(SimWorld w, EnemyState e, Vector2 to)
        {
            for (int i = 0; i < w.Obstacles.Count; i++)
            {
                var o = w.Obstacles[i];
                if (o.Dead) continue;
                float min = o.Radius + e.BodyRadius;
                Vector2 away = to - o.Pos;
                float dist = away.magnitude;
                if (dist >= min) continue;
                Vector2 n = dist > WallEps ? away / dist : EnemyRules.Norm(e.Pos - o.Pos);
                if (n == Vector2.zero) n = EnemyRules.LeftOf(e.Facing);
                to = o.Pos + n * min;
            }
            return to;
        }

        void MoveDash(SimWorld w, EnemyState e, EnemyBrain b)
        {
            AttackDef atk = b.DashAttack;
            if (atk == null) { e.DashActive = false; return; }
            Vector2 from = e.Pos;
            Vector2 to = from + e.DashDir * (atk.DashSpeed * SimConst.TickSeconds);
            bool heavy = e.Has(EnemyTags.Heavy) || e.Has(EnemyTags.Boss);

            // 설치 벽(얼음 병풍): 꼬리표 없음·엘리트는 벽을 부수고 경직(Arts가 ApplyCc로 건다), 중량은 부수고 계속 달린다.
            // 급강하 중인 박쥐 여왕도 땅 높이라 부딪힌다. 설치 벽은 전장 벽이 아니라 기절·튕김에 세지 않는다.
            int zoneId;
            if (w.Mods.Arts.SegmentHitsScreen(w, from, to, out zoneId))
            {
                w.Mods.Arts.OnEnemyTouchedScreen(w, zoneId, e, true);
                if (!e.Alive) return;
                if (!heavy) { e.DashActive = false; return; }
                if (!e.DashActive) return;
            }

            bool hitWall = false;
            ObstacleEntity obstacle;
            if (w.Mods.Threats.SegmentHitsObstacle(w, from, to, false, out obstacle))
            {
                hitWall = true;
                to = from;
            }
            Vector2 clamped = w.ClampToArena(to, e.BodyRadius);
            Vector2 normal = Vector2.zero;
            if (Mathf.Abs(clamped.x - to.x) > WallEps) normal.x = clamped.x > to.x ? 1f : -1f;
            if (Mathf.Abs(clamped.y - to.y) > WallEps) normal.y = clamped.y > to.y ? 1f : -1f;
            if (normal != Vector2.zero) hitWall = true;
            else if (hitWall) normal = -e.DashDir;
            e.Pos = clamped;

            if (b.Species == EnemySpecies.Boar) BoarPath(w, e, b, atk, from, clamped);

            // 몸 충돌 → 피격 후보(분류 Dash, AttackId = 적 Id). 칸 수는 충돌한 이 틱에 계산한다(공통 규칙 6).
            for (int s = 0; s < w.Players.Length; s++)
            {
                var p = w.Players[s];
                if (!p.Active || p.Downed) continue;
                int bit = 1 << s;
                if ((b.DashHitMask & bit) != 0) continue;
                if (!(EnemyRules.DistToSegment(p.Pos, from, clamped) < e.BodyRadius + HitRadius(w, p))) continue;
                var hit = new PlayerHitRequest
                {
                    AttackId = e.Id,
                    CastId = e.DashCastId,
                    SourceEnemyId = e.Id,
                    DamageCells = w.Mods.Threats.DamageCells(w, e.DashBaseDamage, PlayerDamageSource.Dash, e),
                    Source = PlayerDamageSource.Dash,
                    FromPos = e.Pos
                };
                if (w.Mods.Player.QueueHit(w, p, hit)) b.DashHitMask |= bit;
            }

            if (hitWall) OnDashWall(w, e, b, atk, normal);
        }

        void OnDashWall(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk, Vector2 normal)
        {
            e.DashActive = false;
            if (b.Species != EnemySpecies.Boar) return;   // 늑대·박쥐 여왕: 멈추고 보통 경직(7단계가 잇는다, U-29)

            if (b.BouncesLeft > 0)
            {
                // 외엄니: 첫 전장 벽에서 멈췄다가 벽 법선으로 반사한 방향으로 다시 달린다(U-78).
                b.BouncesLeft--;
                Vector2 d = e.DashDir;
                if (normal.x != 0f) d.x = -d.x;
                if (normal.y != 0f) d.y = -d.y;
                b.BounceDir = EnemyRules.Norm(d);
                b.Sub = BrainSub.BounceStop;
                b.SubEndTick = w.Tick + b.Params.Ticks("bounce_stop_seconds");
                SetAi(w, e, EnemyAiState.Act);
                SetClip(w, e, "stagger");
                return;
            }
            // 전장 벽 충돌 → 기절(= 공격 정의의 빈틈 2.0초. 북두꺼비 버프면 84틱, 10 요약서 Q-25). 기절 중 받는 피해 ×1.5.
            int stun = SimConst.Ticks(atk.RecoverySeconds);
            if (e.DrumBuffed) stun = EnemyRules.BuffedRecovery(stun, b.BuffRecoveryRatio);
            e.StunUntilTick = Mathf.Max(e.StunUntilTick, w.Tick + stun);
            float fallback = w.Content != null && w.Content.Const != null ? w.Content.Const.StunDamageTakenMul : 1f;
            e.DamageTakenMul = b.Ai.F("stun_damage_ratio", fallback);
            b.Sub = BrainSub.WallStun;
            SetAi(w, e, EnemyAiState.Recovery);
            SetClip(w, e, "stagger");
            w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)CcKind.Stun, b: stun);
        }

        /// <summary>멧돼지 돌진 경로의 다른 적: 처음 겹친 틱에 1회 10 × H(w) 무속성, 띠 밖으로 수직 밀어냄. 꽈리는 그 자리에서 유폭.</summary>
        void BoarPath(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk, Vector2 from, Vector2 to)
        {
            float half = atk.Width / 2f;
            float damage = b.Ai.F("path_damage") * HpMul(w);
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var o = w.Enemies[i];
                if (o == e || !o.Targetable || o.Ai == EnemyAiState.Spawning) continue;
                if (b.PathHitIds.Contains(o.Id)) continue;
                if (!(EnemyRules.DistToSegment(o.Pos, from, to) < half + o.BodyRadius)) continue;
                b.PathHitIds.Add(o.Id);
                var ob = o.Brain as EnemyBrain;
                if (ob != null && ob.Species == EnemySpecies.Bug && !o.IsBoss)
                {
                    KillSelf(o);
                    continue;
                }
                DealEnvironment(w, o, damage);
                // 중량·뿌리·보스는 밀지 않고 피해만(U-63).
                if (o.IsBoss || o.Has(EnemyTags.Heavy) || o.Has(EnemyTags.Rooted) || o.Has(EnemyTags.Boss)) continue;
                Vector2 left = EnemyRules.LeftOf(e.DashDir);
                float side = Vector2.Dot(o.Pos - from, left);
                Vector2 along = from + e.DashDir * Vector2.Dot(o.Pos - from, e.DashDir);
                o.Pos = w.ClampToArena(along + left * ((side < 0f ? -1f : 1f) * (half + o.BodyRadius)), o.BodyRadius);
            }
        }

        /// <summary>접촉 피해 후보(05 요약서 4절): 1.5초에 1회. QueueHit이 true일 때만 쿨다운을 쓴다(00 문서 D-63).</summary>
        void Contact(SimWorld w, EnemyState e, EnemyBrain b)
        {
            if (e.DashActive || e.Burrowed || e.Def == null) return;
            int damage = e.Def.ContactDamage;
            if (damage <= 0 || w.Tick < e.ContactReadyTick) return;
            for (int s = 0; s < w.Players.Length; s++)
            {
                var p = w.Players[s];
                if (!p.Active || p.Downed) continue;
                if (!EnemyRules.Overlap(e.Pos, e.BodyRadius, p.Pos, HitRadius(w, p))) continue;
                var hit = new PlayerHitRequest
                {
                    AttackId = e.Id,
                    SourceEnemyId = e.Id,
                    DamageCells = w.Mods.Threats.DamageCells(w, damage, PlayerDamageSource.Contact, e),
                    Source = PlayerDamageSource.Contact,
                    FromPos = e.Pos
                };
                if (w.Mods.Player.QueueHit(w, p, hit))
                {
                    e.ContactReadyTick = w.Tick + b.ContactTicks;
                    return;
                }
            }
        }
    }
}
