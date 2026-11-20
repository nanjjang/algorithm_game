using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Spells
{
    // 플레이어 탄의 이동·벽·적 충돌·사거리. 03 요약서 7절(S02 10절).
    public sealed partial class SpellSystem
    {
        // ProjectileExpired 이벤트의 A.
        const int ExpireRange = 0, ExpireWall = 1, ExpireHit = 2, ExpireReturned = 3;

        struct SweepHit
        {
            public EnemyState Enemy;
            public float T;
            /// <summary>맞은 몸 원(머리 또는 보스 마디)의 중심과 반지름.</summary>
            public Vector2 PartCenter;
            public float PartRadius;
        }

        readonly List<SweepHit> _sweep = new List<SweepHit>();
        readonly List<EnemyState> _area = new List<EnemyState>();

        /// <summary>
        /// 9단계. 목록 순서 = 탄 id(생성순) 오름차순. 이 틱에 생긴 탄은 움직이지 않는다(U30) — 즉발만 생긴 틱에 판정한다.
        /// 적중으로 생긴 하위 탄은 목록 끝에 붙어 같은 순회에서 만난다(Pending이라 건너뛰고, 즉발이면 바로 판정).
        /// 내 탄의 피해는 w.CombatActive일 때만 돈다(그 밖에는 적을 지나간다).
        /// </summary>
        public override void Stage9_Projectiles(SimWorld w)
        {
            var t = Tune(w.Content);
            bool combat = w.CombatActive;
            // 기본 공격 유형이 건 화상(적 id 오름차순). 탄보다 먼저 — 이번 틱에 새로 건 화상은 다음 간격부터 탄다.
            if (combat) TickStyleStatuses(w, t);
            var list = w.Projectiles;
            for (int i = 0; i < list.Count; i++)
            {
                var pr = list[i];
                if (pr.Dead) continue;
                if (pr.State == ProjectileState.Dead || pr.Block == null) { pr.Dead = true; continue; }
                if (pr.Block.Instant)
                {
                    ResolveInstant(w, t, pr, combat);
                    continue;
                }
                if (pr.State == ProjectileState.Pending)
                {
                    if (pr.SpawnTick >= w.Tick) continue;
                    pr.State = ProjectileState.Flying;
                }
                pr.PrevPos = pr.Pos;
                // 기본 공격 유형: 범위·설치·지연 동작은 여기서 끝나고(true), 날아가는 몸 탄은 덧처리(지짐·휘기·쫓기) 뒤 아래 길을 그대로 탄다.
                if (pr.Block.Style != null && StepStyle(w, t, pr, combat)) continue;
                if (pr.State == ProjectileState.Returning) StepReturning(w, t, pr, combat);
                else StepFlying(w, t, pr, combat);
            }
        }

        // ------------------------------------------------------------ 날아가는 탄

        void StepFlying(SimWorld w, SpellTuning t, ProjectileEntity pr, bool combat)
        {
            // 눈알 추적탄은 자기 조향(StepStyle)을 쓴다.
            if (pr.Mods.Homing && combat && !(pr.Block.Style != null && pr.Block.Style.Kind == AttackBehavior.Seeker)) Steer(w, t, pr);

            float remaining = pr.Speed * SimConst.TickSeconds;
            // 한 틱에 벽을 여러 번 만날 수 있다(구석). 남은 튕김보다 많이 돌 일은 없다.
            int guard = pr.BouncesLeft + 2;
            while (remaining > Eps && !pr.Dead && pr.State == ProjectileState.Flying && guard-- > 0)
            {
                Vector2 a = pr.Pos;
                Vector2 dir = pr.Dir;
                float len = remaining;
                bool wall = WallHit(w, a, dir, ref len, out Vector2 reflected);

                // 장애물(바위, 얼음 기둥): 벽처럼 다룬다. 얼음 병풍은 장애물이 아니라 지나간다.
                // 방금 튕겨 나와 멀어지는 중인 장애물(겉면에서 출발)은 다시 맞지 않는다.
                ObstacleEntity obstacle;
                if (w.Mods.Threats.SegmentHitsObstacle(w, a, a + dir * len, true, out obstacle) && obstacle != null
                    && Vector2.Dot(obstacle.Pos - a, dir) > 0f)
                {
                    len = CircleEntryOrStart(a, dir, len, obstacle.Pos, obstacle.Radius);
                    wall = false;
                }
                else obstacle = null;

                // 적: 이번 구간(벽·장애물 앞까지)을 쓸어 선분 위 거리 순으로 처리.
                if (combat && SweepEnemies(w, t, pr, a, dir, len)) return;

                pr.Pos = a + dir * len;
                pr.Traveled += len;
                remaining -= len;

                if (obstacle != null)
                {
                    w.Mods.Threats.DamageObstacle(w, obstacle, pr.Block.BaseDamage * pr.DamageMul);
                    Vector2 normal = pr.Pos - obstacle.Pos;
                    normal = normal.sqrMagnitude > Eps * Eps ? normal.normalized : -dir;
                    HitSolid(w, t, pr, Vector2.Reflect(dir, normal), combat);
                }
                else if (wall)
                {
                    HitSolid(w, t, pr, reflected, combat);
                }
            }

            // 사거리: 이동 거리가 사거리 이상이 된 틱에 끝난다(마력탄 43틱째, TV-G01).
            if (!pr.Dead && pr.State == ProjectileState.Flying && pr.Traveled >= pr.Block.Range + pr.RangeBonus - Eps)
            {
                if (pr.Block.HasFlag(Ids.BlockFlag.Returns)) BeginReturn(pr);
                else
                {
                    // 화염구는 사거리 끝에서도 터진다. 그림자 구체는 터지지 않고 사라진다.
                    if (pr.Block.HasFlag(Ids.BlockFlag.Explodes)) Explode(w, t, pr, pr.Pos, -1, combat);
                    Kill(w, pr, ExpireRange);
                }
            }
        }

        /// <summary>벽·장애물에 닿았다: 폭발 블록은 그 자리에서 터지고, 부메랑은 돌아오고, 튕김이 남았으면 튕기고, 아니면 사라진다.</summary>
        void HitSolid(SimWorld w, SpellTuning t, ProjectileEntity pr, Vector2 reflectedDir, bool combat)
        {
            if (pr.Block.HasFlag(Ids.BlockFlag.Explodes))
            {
                Explode(w, t, pr, pr.Pos, -1, combat);
                Kill(w, pr, ExpireWall);
            }
            else if (pr.Block.HasFlag(Ids.BlockFlag.Returns))
            {
                BeginReturn(pr);
            }
            else if (pr.BouncesLeft > 0)
            {
                // 입사각 반사. 이동 거리는 이어서 센다.
                pr.BouncesLeft--;
                pr.Dir = reflectedDir;
                w.Emit(SimEventType.ProjectileBounced, pr.OwnerSlot, pr.Id, pos: pr.Pos);
            }
            else
            {
                Kill(w, pr, ExpireWall);
            }
        }

        /// <summary>
        /// 선분 a → a + dir × len 을 (탄 반지름 + 적 반지름)으로 쓸어 닿은 적을 거리 순(같으면 id 순)으로 처리한다.
        /// 탄이 여기서 끝났으면 true(위치·이동 거리는 적중 지점으로 맞춰 둔다).
        /// 후보는 Targetable인 적만이다: 같은 틱에 이미 죽어 가는 적과 땅속 적은 지나가고 관통 횟수를 쓰지 않는다.
        /// </summary>
        bool SweepEnemies(SimWorld w, SpellTuning t, ProjectileEntity pr, Vector2 a, Vector2 dir, float len)
        {
            var hits = _sweep;
            hits.Clear();
            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Targetable || e.Id == pr.IgnoreEnemyId || pr.HitSet.Contains(e.Id) || OwnProxy(pr, e)) continue;
                if (!BodyEntry(w, e, a, dir, len, pr.Radius, out float at, out Vector2 partC, out float partR)) continue;
                int j = hits.Count;
                hits.Add(default);
                while (j > 0 && (hits[j - 1].T > at || (hits[j - 1].T == at && hits[j - 1].Enemy.Id > e.Id)))
                {
                    hits[j] = hits[j - 1];
                    j--;
                }
                hits[j] = new SweepHit { Enemy = e, T = at, PartCenter = partC, PartRadius = partR };
            }

            bool explodes = pr.Block.HasFlag(Ids.BlockFlag.Explodes);
            // 그림자 구체와 부메랑은 적을 지나간다(같은 적은 HitSet으로 한 번만. 부메랑은 다리마다 새로).
            bool passThrough = pr.Block.HasFlag(Ids.BlockFlag.PassThrough);
            bool passes = passThrough || pr.Block.HasFlag(Ids.BlockFlag.Returns);
            for (int i = 0; i < hits.Count; i++)
            {
                var e = hits[i].Enemy;
                if (!e.Targetable) continue;
                Vector2 contact = a + dir * hits[i].T;

                if (explodes)
                {
                    // 직격 피해는 따로 없다: 그 자리에서 터져 범위 안의 적이 각 1회 맞는다.
                    pr.Pos = contact;
                    pr.Traveled += hits[i].T;
                    Explode(w, t, pr, contact, e.Id, true);
                    Kill(w, pr, ExpireHit);
                    return true;
                }

                pr.HitSet.Add(e.Id);
                Vector2 surface = Surface(contact, hits[i].PartCenter, hits[i].PartRadius);
                var res = pr.Block.Style != null ? StyleHit(w, t, pr, e, 1f, surface, a) : HitEnemy(w, t, pr, e, 1f, surface, a);
                if (!res.Immune) FireTrigger(w, t, pr, contact, dir, e.Id);
                if (passThrough) w.Emit(SimEventType.ProjectilePassed, pr.OwnerSlot, pr.Id, a: e.Id, pos: contact);
                if (pr.Block.Style != null && !res.Immune && AfterStyleBodyHit(w, t, pr, e, res, contact, hits[i].T)) return true;
                if (passes) continue;
                if (pr.PierceLeft > 0)
                {
                    pr.PierceLeft--;
                    continue;
                }
                pr.Pos = contact;
                pr.Traveled += hits[i].T;
                Kill(w, pr, ExpireHit);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ 부메랑

        void BeginReturn(ProjectileEntity pr)
        {
            pr.State = ProjectileState.Returning;
            pr.ReturnTraveled = 0f;
            pr.HitSet.Clear();
            // 돌아오는 다리는 새 발동: 첫 적은 다시 온전한 피해(책 던지기 "두 번 맞힘").
            pr.StyleHits = 0;
        }

        /// <summary>쏜 사람의 지금 위치로 같은 속도로 돌아온다. 도착 반경 안이거나 돌아오는 길이 한도를 넘으면 사라진다(U12).</summary>
        void StepReturning(SimWorld w, SpellTuning t, ProjectileEntity pr, bool combat)
        {
            PlayerState owner = pr.OwnerSlot >= 0 && pr.OwnerSlot < w.Players.Length ? w.Players[pr.OwnerSlot] : null;
            if (owner == null || !owner.Active) { Kill(w, pr, ExpireRange); return; }

            var prm = pr.Block.Params;
            float arrive = prm != null ? prm.ReturnArriveRadius : 0f;
            float maxBack = prm != null ? prm.ReturnMaxDistance : 0f;
            Vector2 to = owner.Pos - pr.Pos;
            float dist = to.magnitude;
            if (dist <= arrive + Eps) { Kill(w, pr, ExpireReturned); return; }

            Vector2 dir = to / dist;
            pr.Dir = dir;
            float step = pr.Speed * SimConst.TickSeconds;
            bool arrives = step >= dist - arrive;
            float len = arrives ? dist - arrive : step;

            ObstacleEntity obstacle;
            bool blocked = w.Mods.Threats.SegmentHitsObstacle(w, pr.Pos, pr.Pos + dir * len, true, out obstacle) && obstacle != null;
            if (blocked) len = CircleEntryOrStart(pr.Pos, dir, len, obstacle.Pos, obstacle.Radius);

            if (combat) SweepEnemies(w, t, pr, pr.Pos, dir, len);
            if (pr.Dead) return;
            pr.Pos += dir * len;
            pr.ReturnTraveled += len;

            if (blocked)
            {
                w.Mods.Threats.DamageObstacle(w, obstacle, pr.Block.BaseDamage * pr.DamageMul);
                Kill(w, pr, ExpireWall);
            }
            else if (arrives) Kill(w, pr, ExpireReturned);
            else if (maxBack > 0f && pr.ReturnTraveled > maxBack + Eps) Kill(w, pr, ExpireRange);
        }

        // ------------------------------------------------------------ 유도

        /// <summary>유도(U9): 시야 앞 ±원뿔 반각, 사거리 안 가장 가까운 적으로 틱당 정해진 각만큼만 튼다. 대상이 사라지면 다시 고른다.</summary>
        void Steer(SimWorld w, SpellTuning t, ProjectileEntity pr)
        {
            EnemyState target = pr.HomingTargetId >= 0 ? w.FindEnemy(pr.HomingTargetId) : null;
            if (target == null || !target.Targetable || pr.HitSet.Contains(target.Id))
            {
                target = PickHomingTarget(w, t, pr);
                pr.HomingTargetId = target != null ? target.Id : -1;
            }
            if (target == null) return;

            Vector2 to = target.Pos - pr.Pos;
            if (to.sqrMagnitude <= Eps * Eps) return;
            float cur = Dir8Util.SimVectorToDegrees(pr.Dir);
            float delta = Mathf.DeltaAngle(cur, Dir8Util.SimVectorToDegrees(to));
            delta = Mathf.Clamp(delta, -t.HomingTurnDegreesPerTick, t.HomingTurnDegreesPerTick);
            pr.Dir = Dir8Util.DegreesToSimVector(cur + delta);
        }

        EnemyState PickHomingTarget(SimWorld w, SpellTuning t, ProjectileEntity pr)
        {
            EnemyState best = null;
            float bestSq = t.HomingRange * t.HomingRange;
            float minCos = Mathf.Cos(t.HomingConeHalfDegrees * Mathf.Deg2Rad);
            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Targetable || e.Id == pr.IgnoreEnemyId || pr.HitSet.Contains(e.Id) || OwnProxy(pr, e)) continue;
                Vector2 to = e.Pos - pr.Pos;
                float sq = to.sqrMagnitude;
                if (sq > bestSq + Eps) continue;
                if (sq > Eps * Eps && Vector2.Dot(to / Mathf.Sqrt(sq), pr.Dir) < minCos - Eps) continue;
                // 같은 거리면 먼저 생긴 적(id가 작은 쪽)을 둔다.
                if (best != null && sq >= bestSq) continue;
                best = e;
                bestSq = sq;
            }
            return best;
        }

        // ------------------------------------------------------------ 즉발(연쇄 번개)

        /// <summary>
        /// 즉발 탄: 생긴 틱에 판정하고 사라진다. 발사 방향 사거리 안의 광선(반폭 = 탄 반지름 + 적 반지름)에서 가장 가까운 적 1체,
        /// 그 적에서 전이 반경 안의 아직 안 맞은 가장 가까운 적으로 전이(k번째 피해 × 전이 배수^k). 하위 스코프는 첫 대상의 중심에서
        /// 발사 방향으로 나간다(U10·U13).
        /// </summary>
        void ResolveInstant(SimWorld w, SpellTuning t, ProjectileEntity pr, bool combat)
        {
            var prm = pr.Block.Params;
            Vector2 a = pr.Pos;
            Vector2 dir = pr.Dir;
            float len = pr.Block.Range;
            WallHit(w, a, dir, ref len, out _);

            ObstacleEntity obstacle;
            if (w.Mods.Threats.SegmentHitsObstacle(w, a, a + dir * len, true, out obstacle) && obstacle != null)
                len = CircleEntryOrStart(a, dir, len, obstacle.Pos, obstacle.Radius);
            else obstacle = null;

            EnemyState first = null;
            float firstAt = 0f;
            Vector2 firstHitPos = default;
            if (combat)
            {
                var enemies = w.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Targetable || e.Id == pr.IgnoreEnemyId || OwnProxy(pr, e)) continue;
                    if (!BodyEntry(w, e, a, dir, len, pr.Radius, out float at, out Vector2 partC, out float partR)) continue;
                    if (first != null && at >= firstAt) continue;
                    first = e;
                    firstAt = at;
                    firstHitPos = Surface(a + dir * at, partC, partR);
                }
                // 기본 공격 유형(연쇄 번개 핵) + 유도: 선에 걸린 적이 없으면 유도 원뿔 안의 가장 가까운 적을 겨눈다(Docs/12 2.2절).
                if (first == null && pr.Block.Style != null && pr.Mods.Homing)
                {
                    first = AimTarget(w, a, dir, t.HomingConeHalfDegrees, len, pr.IgnoreEnemyId);
                    if (first != null)
                    {
                        Vector2 to = first.Pos - a;
                        if (to.sqrMagnitude > Eps * Eps) dir = to.normalized;
                        firstHitPos = Surface(a, first.Pos, first.BodyRadius);
                    }
                }
            }

            pr.PrevPos = a;
            if (first == null)
            {
                Vector2 end = a + dir * len;
                if (obstacle != null) w.Mods.Threats.DamageObstacle(w, obstacle, pr.Block.BaseDamage * pr.DamageMul);
                w.Emit(SimEventType.ChainLightningArc, pr.OwnerSlot, pr.Id, a: 0, pos: a, pos2: end);
                pr.Pos = end;
                Kill(w, pr, obstacle != null ? ExpireWall : ExpireRange);
                return;
            }

            int maxHops = prm != null ? prm.ChainHops : 0;
            float hopMul = prm != null ? prm.ChainHopMultiplier : 1f;
            float chainR = prm != null ? prm.ChainRadius : 0f;
            var cst = pr.Block.Style;
            if (cst != null)
            {
                // 연쇄 번개 핵: 관통 블록 하나 = 옮겨 붙기 +1, 튕김 블록 하나 = 옮겨 붙는 반경 +덤, 크기 = 반경 ×(Docs/12 2.2절).
                maxHops += pr.PierceLeft / Mathf.Max(1, t.PiercePerBlock);
                chainR = (chainR + pr.BouncesLeft / Mathf.Max(1, t.BouncePerBlock) * cst.HopRadiusBonusPerBounce) * pr.AreaScale;
            }
            float chainSq = chainR * chainR;
            pr.HitSet.Clear();
            Vector2 from = a;
            EnemyState cur = first;
            float mul = 1f;
            int hop = 0;
            while (cur != null)
            {
                // 첫 대상은 번개가 닿은 몸 겉면(보스 마디 포함), 전이 대상은 몸 중심.
                Vector2 at = hop == 0 ? firstHitPos : cur.Pos;
                w.Emit(SimEventType.ChainLightningArc, pr.OwnerSlot, pr.Id, a: hop, pos: from, pos2: at);
                pr.HitSet.Add(cur.Id);
                var res = cst != null ? StyleHit(w, t, pr, cur, mul, at, from) : HitEnemy(w, t, pr, cur, mul, at, from);
                if (hop == 0 && !res.Immune) FireTrigger(w, t, pr, at, dir, cur.Id);
                from = at;
                if (hop >= maxHops) break;
                hop++;
                mul *= hopMul;

                cur = null;
                float bestSq = chainSq;
                var enemies = w.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Targetable || pr.HitSet.Contains(e.Id) || OwnProxy(pr, e)) continue;
                    float sq = (e.Pos - from).sqrMagnitude;
                    if (sq > chainSq + Eps) continue;
                    if (cur != null && sq >= bestSq) continue;
                    cur = e;
                    bestSq = sq;
                }
            }
            pr.Pos = from;
            Kill(w, pr, ExpireHit);
        }

        // ------------------------------------------------------------ 폭발, 트리거

        /// <summary>
        /// 폭발(U11): 중심에서 (반경 + 적 반지름) 안의 모든 적이 탄 피해를 각 1회 받는다. 반경 = 블록 값 × Traits.ExplosionRadiusMul.
        /// 맞은 적이 하나라도 있으면 하위 스코프가 폭발 중심에서 1회 나간다(빈 땅 폭발은 발동 안 함, U27).
        /// </summary>
        void Explode(SimWorld w, SpellTuning t, ProjectileEntity pr, Vector2 center, int directEnemyId, bool combat)
        {
            float radius = pr.Block.Params != null ? pr.Block.Params.ExplosionRadius : 0f;
            if (pr.OwnerSlot >= 0 && pr.OwnerSlot < w.Players.Length)
                radius *= w.Mods.Traits.ExplosionRadiusMul(w.Players[pr.OwnerSlot]);
            w.Emit(SimEventType.Explosion, pr.OwnerSlot, pr.Id, f: radius, pos: center, id: pr.Block.Element);
            if (!combat) return;

            var area = _area;
            area.Clear();
            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Targetable) continue;
                if (NearestBodyPart(w, e, center, out _, out _, out float gap) && gap <= radius + Eps) area.Add(e);
            }
            int damaged = 0;
            for (int i = 0; i < area.Count; i++)
            {
                var e = area[i];
                if (!e.Targetable) continue;
                NearestBodyPart(w, e, center, out Vector2 partC, out float partR, out _);
                var res = HitEnemy(w, t, pr, e, 1f, Surface(center, partC, partR), center);
                if (!res.Immune) damaged++;
            }
            if (damaged > 0) FireTrigger(w, t, pr, center, pr.Dir, directEnemyId);
        }

        /// <summary>
        /// 첫 적중의 하위 스코프(03 요약서 6.1절). 투사체당 한 번. 하위 탄은 적중 지점에서 부모가 날던 방향으로 나가고,
        /// 자기를 만든 적과는 부딪치지 않으며(U13) 이 틱에는 움직이지 않는다(Pending).
        /// </summary>
        void FireTrigger(SimWorld w, SpellTuning t, ProjectileEntity pr, Vector2 point, Vector2 dir, int hitEnemyId)
        {
            if (pr.TriggerFired) return;
            pr.TriggerFired = true;
            var tree = pr.Tree;
            if (tree == null) return;
            int depth = pr.ScopeDepth + 1;
            if (depth >= tree.Scopes.Count || depth > pr.MaxDepth) return;
            var child = tree.Scopes[depth];
            if (child.Projectiles.Count == 0) return;

            var ctx = ContextOf(w, t, pr);
            int split = Mathf.Max(1, child.SplitCount);
            int n = child.Projectiles.Count * split;
            int m = CapBatch(w, t, pr.OwnerSlot, n, out float comp);
            float baseDeg = Dir8Util.SimVectorToDegrees(dir);
            int k = 0;
            for (int i = 0; i < child.Projectiles.Count && k < m; i++)
            {
                for (int f = 0; f < split && k < m; f++)
                {
                    Spawn(w, t, ctx, tree, child, child.Projectiles[i], pr.OwnerSlot, pr.ShotId, pr.Source, point,
                        baseDeg + FanOffset(child, f), pr.IsCrit, pr.MaxDepth, comp, hitEnemyId, ShotHitEffect.None);
                    k++;
                }
            }
        }

        /// <summary>결투: 쏜 사람 자신의 과녁(DuelProxyOf == 쏜 슬롯)은 맞히지 않는다.</summary>
        static bool OwnProxy(ProjectileEntity pr, EnemyState e) => e.DuelProxyOf >= 0 && e.DuelProxyOf == pr.OwnerSlot;

        void Kill(SimWorld w, ProjectileEntity pr, int reason)
        {
            if (pr.Dead) return;
            // 얼음 창: 멈추는 곳(적중·사거리·벽)에서 파편으로 깨진다. 지우기(웨이브 끝)는 StyleEnded로 건너뛴다.
            if (pr.Block != null && pr.Block.Style != null && !pr.StyleEnded) OnStyleBodyEnd(w, Tune(w.Content), pr, reason);
            pr.State = ProjectileState.Dead;
            pr.Dead = true;
            w.Emit(SimEventType.ProjectileExpired, pr.OwnerSlot, pr.Id, a: reason, pos: pr.Pos);
        }

        // ------------------------------------------------------------ 기하

        /// <summary>
        /// a에서 dir로 len만큼 가는 길이 전장 가장자리에 닿으면 len을 그 거리로 줄이고 반사 방향을 준다.
        /// 구석에 정확히 닿으면 두 축을 다 뒤집는다.
        /// </summary>
        static bool WallHit(SimWorld w, Vector2 a, Vector2 dir, ref float len, out Vector2 reflected)
        {
            reflected = dir;
            float tx = float.MaxValue, ty = float.MaxValue;
            if (dir.x > Eps) tx = (w.ArenaWidth - a.x) / dir.x;
            else if (dir.x < -Eps) tx = (0f - a.x) / dir.x;
            if (dir.y > Eps) ty = (w.ArenaHeight - a.y) / dir.y;
            else if (dir.y < -Eps) ty = (0f - a.y) / dir.y;
            float at = Mathf.Min(tx, ty);
            if (at > len) return false;
            if (at < 0f) at = 0f;
            len = at;
            if (tx <= at + Eps) reflected.x = -reflected.x;
            if (ty <= at + Eps) reflected.y = -reflected.y;
            return true;
        }

        /// <summary>선분(a, dir, len)이 원(c, r)에 처음 닿는 거리. 시작점이 이미 원 안이면 0. 닿지 않으면 false.</summary>
        /// <summary>
        /// 선분이 적의 몸에 처음 닿는 거리. 몸 = 몸통 원(e.Pos, BodyRadius), 그 적이 보스 몸통이고 마디가 있으면
        /// 마디 원(BossState.Segments, SegmentRadius)도 몸이다(밤삼킴, 05 요약서 9.6절). 가장 먼저 닿은 원을 돌려준다.
        /// </summary>
        static bool BodyEntry(SimWorld w, EnemyState e, Vector2 a, Vector2 dir, float len, float shotRadius,
            out float at, out Vector2 partCenter, out float partRadius)
        {
            partCenter = e.Pos;
            partRadius = e.BodyRadius;
            bool hit = CircleEntry(a, dir, len, e.Pos, shotRadius + e.BodyRadius, out at);
            var boss = w.Boss;
            if (boss == null || boss.EnemyId != e.Id || boss.SegmentRadius <= 0f) return hit;
            var seg = boss.Segments;
            for (int i = 0; i < seg.Count; i++)
            {
                if (!CircleEntry(a, dir, len, seg[i], shotRadius + boss.SegmentRadius, out float t2)) continue;
                if (hit && t2 >= at) continue;
                hit = true;
                at = t2;
                partCenter = seg[i];
                partRadius = boss.SegmentRadius;
            }
            return hit;
        }

        /// <summary>점에서 가장 가까운 몸 원(몸통 또는 보스 마디)과 그 겉면까지의 거리(안이면 0 이하).</summary>
        static bool NearestBodyPart(SimWorld w, EnemyState e, Vector2 point, out Vector2 partCenter, out float partRadius, out float gap)
        {
            partCenter = e.Pos;
            partRadius = e.BodyRadius;
            gap = (e.Pos - point).magnitude - e.BodyRadius;
            var boss = w.Boss;
            if (boss == null || boss.EnemyId != e.Id || boss.SegmentRadius <= 0f) return true;
            var seg = boss.Segments;
            for (int i = 0; i < seg.Count; i++)
            {
                float g = (seg[i] - point).magnitude - boss.SegmentRadius;
                if (g >= gap) continue;
                gap = g;
                partCenter = seg[i];
                partRadius = boss.SegmentRadius;
            }
            return true;
        }

        /// <summary>맞은 몸 원의 겉면에서 point 쪽 점(맞은 자리). point가 중심이면 중심.</summary>
        static Vector2 Surface(Vector2 point, Vector2 center, float radius)
        {
            Vector2 d = point - center;
            float m = d.magnitude;
            if (m <= Eps || m <= radius) return point;
            return center + d * (radius / m);
        }

        static bool CircleEntry(Vector2 a, Vector2 dir, float len, Vector2 c, float r, out float at)
        {
            at = 0f;
            Vector2 m = c - a;
            float mm = m.sqrMagnitude;
            float rr = r * r;
            if (mm <= rr) return true;
            float along = Vector2.Dot(m, dir);
            if (along < 0f) return false;
            float perpSq = mm - along * along;
            if (perpSq > rr) return false;
            at = along - Mathf.Sqrt(rr - perpSq);
            if (at < 0f) at = 0f;
            return at <= len + Eps;
        }

        static float CircleEntryOrStart(Vector2 a, Vector2 dir, float len, Vector2 c, float r)
        {
            return CircleEntry(a, dir, len, c, r, out float at) ? Mathf.Min(at, len) : len;
        }
    }
}
