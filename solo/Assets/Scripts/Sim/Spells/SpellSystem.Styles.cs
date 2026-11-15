using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Spells
{
    // 기본 공격 유형(핵 블록 block.core.*, BlockDef.Style != null)의 탄 동작. Docs/12-attack-styles.md 2·3절이 정본.
    // 상태는 전부 세계의 ProjectileEntity(StylePhase·TargetPos·TimerTick …)와 EnemyState(화상·냉기 누적)에 있다 — 이 객체는 버퍼만 든다.
    // 피해는 언제나 HitEnemy(→ ApplyDamage 한 함수)를 거친다. 둘째 적부터는 extra_target_mul(범위 유형이 무리에서 과하지 않게).
    // 변형 블록의 뜻(Docs/12 2.2절): 분열 = 여러 발(Execute가 부채로 낸다), 크기 = 범위 반경 ×(AreaScale), 관통 = 몸 탄은 관통 수 / 범위 유형은
    // 피해 보너스, 튕김 = 몸 탄은 벽 튕김 / 범위 유형은 "되풀이"(메아리·한 번 더 튐·다시 떨어짐·다시 새김) / 고드름은 개수 +, 유도 = 겨누기 원뿔이
    // 넓어진다(몸 탄은 조향), 서리·잠식·과열 3배·적중 시 발동 = 블록 그대로(트리거는 첫 피해 순간).
    public sealed partial class SpellSystem
    {
        // ProjectileExpired A에 더하는 끝 사유는 없다: 범위 동작이 끝나면 2(적중) 또는 0(시간).
        readonly List<EnemyState> _styleArea = new List<EnemyState>();
        readonly List<float> _styleGap = new List<float>();

        static int StyleTicks(float seconds) => seconds > 0f ? Mathf.Max(1, SimConst.Ticks(seconds)) : 0;

        static PlayerState OwnerOf(SimWorld w, ProjectileEntity pr) =>
            pr.OwnerSlot >= 0 && pr.OwnerSlot < w.Players.Length ? w.Players[pr.OwnerSlot] : null;

        static int Blocks(int count, int perBlock) => perBlock <= 0 ? count : (count + perBlock - 1) / perBlock;

        // ------------------------------------------------------------ 생성 (Spawn 끝에서)

        /// <summary>핵 블록 탄을 만든 직후: 변형의 뜻을 유형에 맞게 바꾸고 동작의 처음 상태를 정한다.</summary>
        void InitStyle(SimWorld w, SpellTuning t, ProjectileEntity pr, SpellScope scope, float sizeMul)
        {
            var st = pr.Block.Style;
            pr.AreaScale = sizeMul;
            pr.Origin = pr.Pos;
            pr.AimDir = pr.Dir;
            pr.ArmTick = pr.SpawnTick;
            int bounceBlocks = Blocks(scope.Mods.BounceCount, t.BouncePerBlock);

            // 관통: 이미 꿰뚫는 범위 유형은 관통 수만큼 피해 보너스, 몸 탄은 자기 관통 수를 더한다.
            if (st.PierceIsBonus)
            {
                if (scope.Mods.PierceCount > 0) pr.DamageMul *= 1f + st.PierceBonusPerCount * scope.Mods.PierceCount;
                pr.PierceLeft = 0;
            }
            else pr.PierceLeft += st.Pierce;
            // 비기·사본 마법진이 트리를 돌릴 때: 발사 1회 피해가 큰 유형이 비기로 몇 배가 되지 않게(마력탄 한 발 = 비기 단위 T).
            if (pr.Source != ShotSource.Fire) pr.DamageMul *= AttackStyleParams.OneIfZero(st.ArtDamageScale);

            switch (st.Kind)
            {
                case AttackBehavior.Lob:
                    pr.BouncesLeft = bounceBlocks;
                    InitLob(w, t, pr, st);
                    break;
                case AttackBehavior.Cone:
                    pr.BouncesLeft = bounceBlocks;
                    InitCone(w, t, pr, st, false);
                    break;
                case AttackBehavior.Strike:
                    pr.BouncesLeft = bounceBlocks;
                    InitStrike(w, t, pr, st);
                    break;
                case AttackBehavior.Icicles:
                    pr.BouncesLeft = 0;
                    pr.StepCount = Mathf.Max(1, st.DropCount + bounceBlocks);
                    InitIcicles(w, t, pr, st);
                    break;
                case AttackBehavior.Mine:
                    pr.BouncesLeft = bounceBlocks;
                    InitMine(w, t, pr, st);
                    break;
                case AttackBehavior.Ricochet:
                    pr.BouncesLeft += st.WallBounces;
                    pr.RicochetsLeft = st.Ricochets;
                    break;
                case AttackBehavior.Orb:
                    pr.TimerTick = pr.SpawnTick + StyleTicks(st.ZapIntervalSeconds);
                    break;
                case AttackBehavior.Boomerang:
                    // 책은 벽에서 돌아온다(튕기지 않는다) → 튕김 블록 하나 = 편도 사거리 덤.
                    pr.RangeBonus = bounceBlocks * st.BounceRangeBonus;
                    pr.BouncesLeft = 0;
                    break;
                case AttackBehavior.Chain:
                    // 즉발: 튕김 수를 옮겨 붙는 반경 덤으로 읽는다(ResolveInstant). 관통 수는 옮겨 붙기 +.
                    break;
            }
        }

        // ------------------------------------------------------------ 9단계 (true = 이 틱 처리 끝)

        bool StepStyle(SimWorld w, SpellTuning t, ProjectileEntity pr, bool combat)
        {
            var st = pr.Block.Style;
            switch (st.Kind)
            {
                case AttackBehavior.Lob: StepLob(w, t, pr, st, combat); return true;
                case AttackBehavior.Cone: StepCone(w, t, pr, st, combat); return true;
                case AttackBehavior.Strike: StepStrike(w, t, pr, st, combat); return true;
                case AttackBehavior.Icicles: StepIcicles(w, t, pr, st, combat); return true;
                case AttackBehavior.Mine: StepMine(w, t, pr, st, combat); return true;
                case AttackBehavior.Orb:
                    if (combat) StepOrbZap(w, t, pr, st);
                    return false;
                case AttackBehavior.Pages:
                    StepPageCurve(pr, st);
                    return false;
                case AttackBehavior.Seeker:
                    if (combat) StepSeek(w, t, pr, st);
                    return false;
                default:
                    return false;
            }
        }

        // ------------------------------------------------------------ 적중 공통

        /// <summary>
        /// 핵 블록 탄의 적중 하나: 둘째 적부터 extra_target_mul → 피해 공식 → 화상·냉기·당김 → 콤보 덤(J 발사만, 첫 적중 1회 + 낫은 둘째부터 1씩).
        /// </summary>
        DamageResult StyleHit(SimWorld w, SpellTuning t, ProjectileEntity pr, EnemyState e, float mul, Vector2 hitPos, Vector2 fromPos)
        {
            var st = pr.Block.Style;
            float m = mul * (pr.StyleHits > 0 ? AttackStyleParams.OneIfZero(st.ExtraTargetMul) : 1f);
            var res = HitEnemy(w, t, pr, e, m, hitPos, fromPos);
            if (res.Immune) return res;
            pr.StyleHits++;
            if (!res.Killed) ApplyStyleStatuses(w, pr, st, e, m);
            if (pr.Source == ShotSource.Fire)
            {
                var owner = OwnerOf(w, pr);
                if (owner != null && owner.Active)
                {
                    if (!pr.ComboBonusGiven && st.ComboOnHit > 0)
                    {
                        pr.ComboBonusGiven = true;
                        w.Mods.Player.AddCombo(w, owner, st.ComboOnHit);
                    }
                    if (pr.StyleHits > 1 && st.ComboPerExtraHit > 0) w.Mods.Player.AddCombo(w, owner, st.ComboPerExtraHit);
                }
            }
            return res;
        }

        void ApplyStyleStatuses(SimWorld w, ProjectileEntity pr, AttackStyleParams st, EnemyState e, float mul)
        {
            if (st.BurnStacksPerHit > 0 && st.BurnDamagePerStack > 0f)
            {
                float perStack = pr.Block.BaseDamage * pr.DamageMul * mul * st.BurnDamagePerStack;
                ApplyBurn(w, e, pr.OwnerSlot, st.BurnStacksPerHit, perStack, Mathf.Max(1, st.BurnMaxStacks),
                    StyleTicks(st.BurnSeconds), StyleTicks(st.BurnIntervalSeconds));
            }
            if (st.ChillSlowPerStack > 0f || st.ChillFreezeAt > 0) ApplyChill(w, e, pr.OwnerSlot, st);
            if (st.PullDistance > 0f && e.Targetable)
            {
                // 쏜 쪽(부채꼴 원점)으로 살짝 당긴다. 원점을 넘지 않는다(몸이 닿을 만큼까지만).
                Vector2 to = pr.Origin - e.Pos;
                float dist = to.magnitude;
                float room = dist - e.BodyRadius;
                float pull = Mathf.Min(st.PullDistance, room);
                if (pull > Eps && dist > Eps) w.Mods.Enemies.ApplyCc(w, e, CcKind.Pull, pull, CcSource.AttackStyle, to / dist);
            }
        }

        /// <summary>몸 탄이 적을 맞힌 뒤(SweepEnemies). true = 이 탄의 이 틱 이동이 여기서 끝났다(튕겨 나감).</summary>
        bool AfterStyleBodyHit(SimWorld w, SpellTuning t, ProjectileEntity pr, EnemyState e, in DamageResult res, Vector2 contact, float at)
        {
            var st = pr.Block.Style;
            switch (st.Kind)
            {
                case AttackBehavior.Ricochet:
                    {
                        if (pr.RicochetsLeft <= 0) return false;
                        var next = NearestOther(w, contact, st.RicochetRadius, pr);
                        if (next == null) return false;
                        pr.RicochetsLeft--;
                        pr.Pos = contact;
                        pr.Traveled += at;
                        Vector2 to = next.Pos - contact;
                        if (to.sqrMagnitude > Eps * Eps) pr.Dir = to.normalized;
                        w.Emit(SimEventType.StyleRicochet, pr.OwnerSlot, pr.Id, a: next.Id, pos: contact, pos2: next.Pos, id: pr.Block.Id);
                        return true;
                    }
                case AttackBehavior.Lance:
                    // 멈추는 적(관통이 다한 적)을 파편이 다시 맞히지 않게 적어 둔다.
                    pr.TargetEnemyId = e.Id;
                    return false;
                case AttackBehavior.Seeker:
                    if (res.Killed && pr.Generation < st.SplitGenerations && st.SplitCount > 1) SplitSeeker(w, t, pr, st, contact, e.Id);
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>몸 탄이 사라지는 순간(Kill 안, 지우기 제외): 얼음 창은 그 자리에서 파편으로 깨진다.</summary>
        void OnStyleBodyEnd(SimWorld w, SpellTuning t, ProjectileEntity pr, int reason)
        {
            pr.StyleEnded = true;
            var st = pr.Block.Style;
            if (st.Kind != AttackBehavior.Lance || reason == ExpireReturned) return;
            var shard = !string.IsNullOrEmpty(st.ShardBlock) ? w.Content.Block(st.ShardBlock) : null;
            int n = Mathf.Max(0, st.ShardCount);
            if (shard == null || n <= 0) return;
            int m = CapBatch(w, t, pr.OwnerSlot, n, out float comp);
            float baseDeg = Dir8Util.SimVectorToDegrees(pr.Dir);
            for (int i = 0; i < m; i++)
            {
                float off = n <= 1 ? 0f : -st.ShardSpreadDegrees + i * (2f * st.ShardSpreadDegrees / (n - 1));
                SpawnSub(w, pr, shard, pr.Pos, baseDeg + off, comp, pr.TargetEnemyId, pr.Generation);
            }
            w.Emit(SimEventType.StyleShatter, pr.OwnerSlot, pr.Id, a: m, pos: pr.Pos, id: pr.Block.Id);
        }

        /// <summary>
        /// 부속 탄(얼음 창 파편, 갈라진 추적탄): 부모의 발사 문맥(주인·발사 번호·출처·임계·배율·스코프)을 그대로 물려받는다.
        /// 트리거는 다시 걸지 않는다(부모가 이미 걸었다). 기본 피해는 부속 블록의 값, 배율 = 부모 배율 × factor.
        /// </summary>
        ProjectileEntity SpawnSub(SimWorld w, ProjectileEntity parent, BlockDef b, Vector2 pos, float degrees, float factor, int ignoreEnemyId,
            int generation)
        {
            var pr = new ProjectileEntity
            {
                Id = w.NewEntityId(),
                OwnerSlot = parent.OwnerSlot,
                ShotId = parent.ShotId,
                Source = parent.Source,
                Block = b,
                Tree = parent.Tree,
                ScopeDepth = parent.ScopeDepth,
                MaxDepth = parent.MaxDepth,
                State = ProjectileState.Pending,
                SpawnTick = w.Tick,
                Pos = pos,
                PrevPos = pos,
                Dir = Dir8Util.DegreesToSimVector(degrees),
                Speed = b.Speed,
                Radius = b.HitRadius * parent.AreaScale,
                IgnoreEnemyId = ignoreEnemyId,
                Mods = parent.Mods,
                IsCrit = parent.IsCrit,
                BaseMul = parent.BaseMul,
                CritBonus = parent.CritBonus,
                DamageMul = parent.DamageMul * factor,
                HitEffect = ShotHitEffect.None,
                TriggerFired = true,
                ComboBonusGiven = true,
                AreaScale = parent.AreaScale,
                Generation = generation,
                Origin = pos,
                AimDir = Dir8Util.DegreesToSimVector(degrees)
            };
            // 부속 탄은 부모의 튕김·관통·유도를 옮기지 않는다(파편이 끝없이 늘지 않게). 감속·잠식은 Mods로 따라간다.
            pr.Mods.BounceCount = 0;
            pr.Mods.PierceCount = 0;
            pr.Mods.Homing = false;
            w.Projectiles.Add(pr);
            w.Emit(SimEventType.ProjectileSpawned, pr.OwnerSlot, pr.Id, a: 0, b: pr.ScopeDepth, pos: pos, id: b.Id);
            return pr;
        }

        // ------------------------------------------------------------ 겨누기·범위 공통

        /// <summary>from에서 dir 앞 ±coneDeg, range 안(몸 겉면까지)의 가장 가까운 적. 같은 거리면 id가 작은 쪽.</summary>
        EnemyState AimTarget(SimWorld w, Vector2 from, Vector2 dir, float coneDeg, float range, int ignoreId)
        {
            if (coneDeg <= 0f || range <= 0f) return null;
            EnemyState best = null;
            float bestD = float.MaxValue;
            float minCos = Mathf.Cos(Mathf.Min(coneDeg, HalfTurnDegrees) * Mathf.Deg2Rad);
            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Targetable || e.Id == ignoreId) continue;
                Vector2 to = e.Pos - from;
                float d = to.magnitude;
                if (d - e.BodyRadius > range + Eps) continue;
                if (d > Eps && Vector2.Dot(to / d, dir) < minCos - Eps) continue;
                if (best != null && d >= bestD) continue;
                best = e;
                bestD = d;
            }
            return best;
        }

        /// <summary>겨누기 원뿔·사거리(유도 변형이면 넓힌다).</summary>
        static void AimCone(SpellTuning t, ProjectileEntity pr, AttackStyleParams st, out float cone, out float range)
        {
            cone = st.AimConeDegrees;
            range = st.AimRange;
            if (pr.Mods.Homing)
            {
                cone = Mathf.Max(cone, t.HomingConeHalfDegrees);
                range = Mathf.Max(range, t.HomingRange);
            }
        }

        /// <summary>겨눈 자리: 대상이 있으면 대상 위치(가장 가까운 거리 아래면 그 방향으로 min), 없으면 기본 거리. 전장 안으로 자른다.</summary>
        Vector2 AimPoint(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st, out EnemyState target)
        {
            AimCone(t, pr, st, out float cone, out float range);
            target = AimTarget(w, pr.Pos, pr.Dir, cone, range, pr.IgnoreEnemyId);
            // 바짝 붙은 적(겉면이 가장 가까운 거리 안)은 시야와 무관하게 겨눈다 — 몸에 붙은 적을 못 맞히지 않게.
            if (target == null && st.MinDistance > 0f) target = AimTarget(w, pr.Pos, pr.Dir, HalfTurnDegrees, st.MinDistance, pr.IgnoreEnemyId);
            Vector2 point;
            if (target != null)
            {
                Vector2 to = target.Pos - pr.Pos;
                float d = to.magnitude;
                // 룬은 적 발밑에 새긴다(쏜 사람을 다치게 하지 않는다). 포탄·낙뢰는 가장 가까운 거리보다 가까우면 그 방향으로 그 거리에.
                if (st.Kind == AttackBehavior.Mine) point = target.Pos;
                else if (d <= Eps) point = pr.Pos + pr.Dir * st.MinDistance;
                else point = d < st.MinDistance ? pr.Pos + to / d * st.MinDistance : target.Pos;
            }
            else point = pr.Pos + pr.Dir * st.DefaultDistance;
            return w.ClampToArena(point, 0f);
        }

        /// <summary>
        /// 범위 타격 하나(폭발·낙뢰·고드름·룬·불바닥 한 번): 중심에서 (반경 + 몸) 안의 적을 겉면 거리 순(같으면 id 순)으로 맞힌다.
        /// 이번 발동의 첫 적은 온전한 피해, 둘째부터 extra_target_mul. 맞은 적이 있으면 첫 발동에서 트리거(빈 땅은 발동 안 함, U27과 같다).
        /// </summary>
        int StyleBlast(SimWorld w, SpellTuning t, ProjectileEntity pr, Vector2 center, float radius, float mul, StyleBlastKind kind,
            bool combat, bool canTrigger, bool explosionFx)
        {
            int hits = 0;
            var area = _styleArea;
            var gaps = _styleGap;
            area.Clear();
            gaps.Clear();
            if (combat)
            {
                var enemies = w.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Targetable) continue;
                    if (!NearestBodyPart(w, e, center, out _, out _, out float gap) || gap > radius + Eps) continue;
                    // 삽입 정렬: 겉면 거리 오름차순, 같으면 id 오름차순(목록이 id 순이라 뒤에 붙는다).
                    int j = area.Count;
                    area.Add(e);
                    gaps.Add(gap);
                    while (j > 0 && gaps[j - 1] > gap)
                    {
                        area[j] = area[j - 1];
                        gaps[j] = gaps[j - 1];
                        j--;
                    }
                    area[j] = e;
                    gaps[j] = gap;
                }
            }
            pr.StyleHits = 0;
            bool triggered = false;
            for (int i = 0; i < area.Count; i++)
            {
                var e = area[i];
                if (!e.Targetable) continue;
                NearestBodyPart(w, e, center, out Vector2 partC, out float partR, out _);
                var res = StyleHit(w, t, pr, e, mul, Surface(center, partC, partR), center);
                if (res.Immune) continue;
                hits++;
                if (canTrigger && !triggered)
                {
                    triggered = true;
                    FireTrigger(w, t, pr, center, pr.AimDir, -1);
                }
            }
            w.Emit(SimEventType.StyleBlast, pr.OwnerSlot, pr.Id, a: hits, b: (int)kind, f: radius, pos: center, id: pr.Block.Id);
            // 지금 그림이 이 이벤트를 모를 때를 위해 속성 폭발도 낸다(B = 1: 유형이 낸 폭발. 전용 그림은 StyleBlast를 본다).
            if (explosionFx) w.Emit(SimEventType.Explosion, pr.OwnerSlot, pr.Id, b: 1, f: radius, pos: center, id: pr.Block.Element);
            return hits;
        }

        EnemyState NearestOther(SimWorld w, Vector2 from, float radius, ProjectileEntity pr)
        {
            EnemyState best = null;
            float bestSq = radius * radius;
            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Targetable || pr.HitSet.Contains(e.Id) || e.Id == pr.IgnoreEnemyId) continue;
                float sq = (e.Pos - from).sqrMagnitude;
                if (sq > bestSq + Eps) continue;
                if (best != null && sq >= bestSq) continue;
                best = e;
                bestSq = sq;
            }
            return best;
        }

        void EndStyle(SimWorld w, ProjectileEntity pr, int reason)
        {
            pr.StyleEnded = true;
            Kill(w, pr, reason);
        }

        // ------------------------------------------------------------ 화로 포탄 (lob)

        void InitLob(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st)
        {
            Vector2 land = AimPoint(w, t, pr, st, out EnemyState target);
            pr.TargetPos = land;
            pr.TargetEnemyId = pr.Mods.Homing && target != null ? target.Id : -1;
            Vector2 d = land - pr.Pos;
            if (d.sqrMagnitude > Eps * Eps) pr.Dir = pr.AimDir = d.normalized;
            int flight = StyleTicks(st.FlightSeconds);
            pr.TimerTick = pr.SpawnTick + Mathf.Max(1, flight);
            w.Emit(SimEventType.StyleLobLaunched, pr.OwnerSlot, pr.Id, a: pr.TimerTick - pr.SpawnTick, b: 0, pos: pr.Pos, pos2: land, id: pr.Block.Id);
        }

        void StepLob(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st, bool combat)
        {
            int now = w.Tick;
            if (pr.StylePhase == 0)
            {
                // 유도: 떨어질 자리가 쫓는 적을 따라간다(전장 안).
                if (pr.TargetEnemyId >= 0)
                {
                    var target = w.FindEnemy(pr.TargetEnemyId);
                    if (target != null && target.Targetable) pr.TargetPos = w.ClampToArena(target.Pos, 0f);
                    else pr.TargetEnemyId = -1;
                }
                int legTicks = Mathf.Max(1, pr.TimerTick - pr.ArmTick);
                float k = Mathf.Clamp01((now - pr.ArmTick) / (float)legTicks);
                pr.Pos = Vector2.Lerp(pr.Origin, pr.TargetPos, k);
                if (now < pr.TimerTick) return;
                pr.Pos = pr.TargetPos;

                float radius = st.BlastRadius * pr.AreaScale;
                var owner = OwnerOf(w, pr);
                if (owner != null) radius *= w.Mods.Traits.ExplosionRadiusMul(owner);
                StyleBlast(w, t, pr, pr.TargetPos, radius, 1f, StyleBlastKind.MortarLand, combat, true, true);

                if (pr.BouncesLeft > 0)
                {
                    // 튕김: 떨어진 자리에서 앞으로 한 번 더 튀어 터진다(약하게).
                    pr.BouncesLeft--;
                    pr.StepIndex++;
                    pr.Origin = pr.TargetPos;
                    pr.TargetPos = w.ClampToArena(pr.TargetPos + pr.AimDir * st.BounceHopDistance, 0f);
                    pr.ArmTick = now;
                    pr.TimerTick = now + Mathf.Max(1, StyleTicks(st.BounceFlightSeconds));
                    pr.TargetEnemyId = -1;
                    pr.DamageMul *= AttackStyleParams.OneIfZero(st.EchoDamageMul);
                    w.Emit(SimEventType.StyleLobLaunched, pr.OwnerSlot, pr.Id, a: pr.TimerTick - now, b: pr.StepIndex, pos: pr.Origin,
                        pos2: pr.TargetPos, id: pr.Block.Id);
                    return;
                }
                int floor = StyleTicks(st.FloorSeconds);
                int every = StyleTicks(st.FloorIntervalSeconds);
                if (floor <= 0 || every <= 0 || st.FloorDamage <= 0f) { EndStyle(w, pr, ExpireHit); return; }
                pr.StylePhase = 1;
                pr.TimerTick = now + every;
                pr.EndTick = now + floor / every * every;
                w.Emit(SimEventType.StyleGroundSpawned, pr.OwnerSlot, pr.Id, a: pr.EndTick - now, b: 0, f: st.FloorRadius * pr.AreaScale,
                    pos: pr.Pos, id: pr.Block.Id);
                return;
            }

            // 불바닥: 간격마다 반경 안의 적에게 불바닥 피해(화상). 트리거는 다시 걸지 않는다.
            if (now >= pr.TimerTick)
            {
                pr.TimerTick += Mathf.Max(1, StyleTicks(st.FloorIntervalSeconds));
                StyleBlast(w, t, pr, pr.Pos, st.FloorRadius * pr.AreaScale, st.FloorDamage, StyleBlastKind.MortarFloorTick, combat, false, false);
            }
            if (now >= pr.EndTick)
            {
                w.Emit(SimEventType.StyleGroundExpired, pr.OwnerSlot, pr.Id, b: 0, pos: pr.Pos, id: pr.Block.Id);
                EndStyle(w, pr, ExpireRange);
            }
        }

        // ------------------------------------------------------------ 부채꼴 (불꽃 방사, 눈보라, 그림자 파동, 그림자 낫)

        void InitCone(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st, bool echo)
        {
            float radius = st.ConeRadius * pr.AreaScale;
            if (!echo && pr.Mods.Homing)
            {
                // 유도: 가운데 방향을 유도 원뿔 안의 가장 가까운 적 쪽으로 돌린다.
                var target = AimTarget(w, pr.Origin, pr.AimDir, t.HomingConeHalfDegrees, Mathf.Max(radius, t.HomingRange), pr.IgnoreEnemyId);
                if (target != null)
                {
                    Vector2 to = target.Pos - pr.Origin;
                    if (to.sqrMagnitude > Eps * Eps) pr.AimDir = pr.Dir = to.normalized;
                }
            }
            int sweep = Mathf.Max(1, StyleTicks(st.SweepSeconds));
            pr.StepCount = sweep;
            pr.StepIndex = 0;
            pr.StyleHits = 0;
            pr.HitSet.Clear();
            // 낫은 번갈아 휘두른다(발사 번호의 홀짝): 0 = −반각 → +반각, 1 = +반각 → −반각.
            pr.StylePhase = st.AngularSweep && (pr.ShotId & 1) != 0 ? 1 : 0;
            int code = !st.AngularSweep ? sweep : pr.StylePhase == 0 ? -sweep : -(1000 + sweep);
            w.Emit(SimEventType.StyleConeStarted, pr.OwnerSlot, pr.Id, a: Mathf.RoundToInt(st.ConeHalfDegrees), b: code, f: radius,
                pos: pr.Origin, pos2: pr.AimDir, id: pr.Block.Id);
        }

        void StepCone(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st, bool combat)
        {
            var owner = OwnerOf(w, pr);
            if (st.Attached && owner != null && owner.Active) pr.Origin = owner.Pos;
            pr.Pos = pr.Origin;
            pr.StepIndex++;
            float frac = Mathf.Clamp01(pr.StepIndex / (float)Mathf.Max(1, pr.StepCount));
            float radius = st.ConeRadius * pr.AreaScale;
            float half = st.ConeHalfDegrees;
            float aim = Dir8Util.SimVectorToDegrees(pr.AimDir);

            if (combat)
            {
                var area = _styleArea;
                var gaps = _styleGap;
                area.Clear();
                gaps.Clear();
                var enemies = w.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Targetable || pr.HitSet.Contains(e.Id)) continue;
                    Vector2 to = e.Pos - pr.Origin;
                    float dist = to.magnitude;
                    float gap = dist - e.BodyRadius;
                    if (gap > radius + Eps) continue;
                    if (!st.AngularSweep && gap > radius * frac + Eps) continue;
                    if (dist > e.BodyRadius + Eps)
                    {
                        float rel = Mathf.DeltaAngle(aim, Dir8Util.SimVectorToDegrees(to));
                        float allow = Mathf.Asin(Mathf.Clamp01(e.BodyRadius / dist)) * Mathf.Rad2Deg;
                        if (Mathf.Abs(rel) > half + allow + Eps) continue;
                        if (st.AngularSweep)
                        {
                            // 휩쓴 쪽까지만: 0 = −반각에서 +쪽으로, 1 = +반각에서 −쪽으로.
                            float edge = -half + 2f * half * frac;
                            if (pr.StylePhase == 0 ? rel > edge + allow + Eps : -rel > edge + allow + Eps) continue;
                        }
                    }
                    int j = area.Count;
                    area.Add(e);
                    gaps.Add(gap);
                    while (j > 0 && gaps[j - 1] > gap)
                    {
                        area[j] = area[j - 1];
                        gaps[j] = gaps[j - 1];
                        j--;
                    }
                    area[j] = e;
                    gaps[j] = gap;
                }
                for (int i = 0; i < area.Count; i++)
                {
                    var e = area[i];
                    if (!e.Targetable) continue;
                    pr.HitSet.Add(e.Id);
                    var res = StyleHit(w, t, pr, e, 1f, Surface(pr.Origin, e.Pos, e.BodyRadius), pr.Origin);
                    if (!res.Immune) FireTrigger(w, t, pr, e.Pos, pr.AimDir, e.Id);
                }
            }

            if (pr.StepIndex < pr.StepCount) return;
            if (pr.BouncesLeft > 0)
            {
                // 튕김: 같은 자리에서 한 번 더 휩쓴다(메아리, 약하게). 쫓아가는 원점은 그대로 따라간다.
                pr.BouncesLeft--;
                pr.DamageMul *= AttackStyleParams.OneIfZero(st.EchoDamageMul);
                InitCone(w, t, pr, st, true);
                return;
            }
            EndStyle(w, pr, ExpireRange);
        }

        // ------------------------------------------------------------ 낙뢰 (strike)

        void InitStrike(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st)
        {
            pr.TargetPos = AimPoint(w, t, pr, st, out _);
            pr.Pos = pr.TargetPos;
            pr.Speed = 0f;
            pr.TimerTick = pr.SpawnTick + Mathf.Max(1, StyleTicks(st.DelaySeconds));
            w.Emit(SimEventType.StyleStrikeWarned, pr.OwnerSlot, pr.Id, a: pr.TimerTick - pr.SpawnTick, b: 0, f: st.BlastRadius * pr.AreaScale,
                pos: pr.TargetPos, id: pr.Block.Id);
        }

        void StepStrike(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st, bool combat)
        {
            if (w.Tick < pr.TimerTick) return;
            float radius = st.BlastRadius * pr.AreaScale;
            StyleBlast(w, t, pr, pr.TargetPos, radius, 1f, StyleBlastKind.SkyStrike, combat, pr.StepIndex == 0, true);
            if (pr.BouncesLeft > 0)
            {
                // 튕김: 같은 자리에 한 번 더 떨어진다(예고부터 다시, 약하게).
                pr.BouncesLeft--;
                pr.StepIndex++;
                pr.DamageMul *= AttackStyleParams.OneIfZero(st.EchoDamageMul);
                pr.TimerTick = w.Tick + Mathf.Max(1, StyleTicks(st.RestrikeSeconds));
                w.Emit(SimEventType.StyleStrikeWarned, pr.OwnerSlot, pr.Id, a: pr.TimerTick - w.Tick, b: pr.StepIndex, f: radius,
                    pos: pr.TargetPos, id: pr.Block.Id);
                return;
            }
            EndStyle(w, pr, ExpireHit);
        }

        // ------------------------------------------------------------ 고드름 낙하 (icicles)

        Vector2 IciclePoint(SimWorld w, ProjectileEntity pr, AttackStyleParams st, int index) =>
            w.ClampToArena(pr.Origin + pr.AimDir * (st.DropStart + index * st.DropStep), 0f);

        void InitIcicles(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st)
        {
            // 줄 방향: 앞 원뿔(유도 변형이면 넓힌 원뿔) 안의 가장 가까운 적 쪽으로 튼다. 없으면 시야 그대로.
            AimCone(t, pr, st, out float cone, out float range);
            var target = AimTarget(w, pr.Origin, pr.AimDir, cone, range, pr.IgnoreEnemyId);
            if (target != null)
            {
                Vector2 to = target.Pos - pr.Origin;
                if (to.sqrMagnitude > Eps * Eps) pr.AimDir = pr.Dir = to.normalized;
            }
            pr.StepIndex = 0;
            pr.TimerTick = pr.SpawnTick + Mathf.Max(1, StyleTicks(st.DelaySeconds));
            pr.Speed = 0f;
            pr.Pos = IciclePoint(w, pr, st, 0);
            int gap = StyleTicks(st.DropGapSeconds);
            float radius = st.DropRadius * pr.AreaScale;
            for (int i = 0; i < pr.StepCount; i++)
                w.Emit(SimEventType.StyleStrikeWarned, pr.OwnerSlot, pr.Id, a: pr.TimerTick + i * gap - pr.SpawnTick, b: i, f: radius,
                    pos: IciclePoint(w, pr, st, i), id: pr.Block.Id);
        }

        void StepIcicles(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st, bool combat)
        {
            int gap = StyleTicks(st.DropGapSeconds);
            float radius = st.DropRadius * pr.AreaScale;
            while (pr.StepIndex < pr.StepCount && w.Tick >= pr.TimerTick + pr.StepIndex * gap)
            {
                Vector2 at = IciclePoint(w, pr, st, pr.StepIndex);
                pr.Pos = at;
                StyleBlast(w, t, pr, at, radius, 1f, StyleBlastKind.IcicleDrop, combat, !pr.TriggerFired, false);
                pr.StepIndex++;
            }
            if (pr.StepIndex >= pr.StepCount) EndStyle(w, pr, ExpireHit);
        }

        // ------------------------------------------------------------ 룬 문장 (mine)

        void InitMine(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st)
        {
            pr.TargetPos = AimPoint(w, t, pr, st, out _);
            pr.Pos = pr.TargetPos;
            pr.Speed = 0f;
            pr.StylePhase = 0;
            pr.ArmTick = pr.SpawnTick + StyleTicks(st.ArmSeconds);
            pr.EndTick = pr.SpawnTick + Mathf.Max(1, StyleTicks(st.FuseSeconds));
            w.Emit(SimEventType.StyleGroundSpawned, pr.OwnerSlot, pr.Id, a: pr.EndTick - pr.SpawnTick, b: 1, f: st.TriggerRadius * pr.AreaScale,
                pos: pr.Pos, id: pr.Block.Id);

            // 동시 개수 상한: 넘으면 가장 오래된 것(id가 가장 작은 것)이 이번 틱 9단계에 바로 터진다.
            int max = Mathf.Max(1, st.MaxMines);
            int count = 0;
            ProjectileEntity oldest = null;
            var list = w.Projectiles;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o.Dead || o == pr || o.OwnerSlot != pr.OwnerSlot || o.Block != pr.Block) continue;
                if (o.EndTick <= w.Tick) continue;   // 이미 이번 틱에 터질 것
                count++;
                if (oldest == null) oldest = o;
            }
            if (count >= max && oldest != null)
            {
                oldest.ArmTick = Mathf.Min(oldest.ArmTick, w.Tick);
                oldest.EndTick = w.Tick;
            }
        }

        void StepMine(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st, bool combat)
        {
            int now = w.Tick;
            if (now < pr.ArmTick) return;
            if (pr.StylePhase == 0)
            {
                pr.StylePhase = 1;
                w.Emit(SimEventType.StyleMineArmed, pr.OwnerSlot, pr.Id, f: st.TriggerRadius * pr.AreaScale, pos: pr.Pos, id: pr.Block.Id);
            }
            bool stepped = false;
            if (combat)
            {
                float trigger = st.TriggerRadius * pr.AreaScale;
                var enemies = w.Enemies;
                for (int i = 0; i < enemies.Count && !stepped; i++)
                {
                    var e = enemies[i];
                    if (!e.Targetable) continue;
                    if (NearestBodyPart(w, e, pr.Pos, out _, out _, out float gap) && gap <= trigger + Eps) stepped = true;
                }
            }
            if (!stepped && now < pr.EndTick) return;

            StyleBlast(w, t, pr, pr.Pos, st.BlastRadius * pr.AreaScale, 1f, StyleBlastKind.RuneBurst, combat, !pr.TriggerFired, true);
            w.Emit(SimEventType.StyleGroundExpired, pr.OwnerSlot, pr.Id, b: 1, pos: pr.Pos, id: pr.Block.Id);
            if (pr.BouncesLeft > 0)
            {
                // 튕김: 같은 자리에 다시 새긴다(약하게).
                pr.BouncesLeft--;
                pr.DamageMul *= AttackStyleParams.OneIfZero(st.EchoDamageMul);
                pr.StylePhase = 0;
                pr.ArmTick = now + Mathf.Max(1, StyleTicks(st.RearmSeconds));
                pr.EndTick = pr.ArmTick + Mathf.Max(1, StyleTicks(st.FuseSeconds));
                w.Emit(SimEventType.StyleGroundSpawned, pr.OwnerSlot, pr.Id, a: pr.EndTick - now, b: 1, f: st.TriggerRadius * pr.AreaScale,
                    pos: pr.Pos, id: pr.Block.Id);
                return;
            }
            EndStyle(w, pr, ExpireHit);
        }

        // ------------------------------------------------------------ 번개 구체 (orb): 지나가며 지진다

        void StepOrbZap(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st)
        {
            int every = StyleTicks(st.ZapIntervalSeconds);
            if (every <= 0 || w.Tick < pr.TimerTick) return;
            pr.TimerTick += every;
            float radius = st.ZapRadius * pr.AreaScale;
            var area = _styleArea;
            var gaps = _styleGap;
            area.Clear();
            gaps.Clear();
            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Targetable) continue;
                if (!NearestBodyPart(w, e, pr.Pos, out _, out _, out float gap) || gap > radius + Eps) continue;
                int j = area.Count;
                area.Add(e);
                gaps.Add(gap);
                while (j > 0 && gaps[j - 1] > gap)
                {
                    area[j] = area[j - 1];
                    gaps[j] = gaps[j - 1];
                    j--;
                }
                area[j] = e;
                gaps[j] = gap;
            }
            // 지짐 한 번 = 새 발동(첫 적 온전). 몸으로 꿰뚫은 수는 따로 이어 센다.
            int keep = pr.StyleHits;
            pr.StyleHits = 0;
            int n = Mathf.Min(area.Count, Mathf.Max(1, st.ZapTargets));
            for (int i = 0; i < n; i++)
            {
                var e = area[i];
                if (!e.Targetable) continue;
                NearestBodyPart(w, e, pr.Pos, out Vector2 partC, out float partR, out _);
                Vector2 hit = Surface(pr.Pos, partC, partR);
                w.Emit(SimEventType.StyleZap, pr.OwnerSlot, pr.Id, a: e.Id, pos: pr.Pos, pos2: hit, id: pr.Block.Id);
                StyleHit(w, t, pr, e, st.ZapDamage, hit, pr.Pos);
            }
            pr.StyleHits = keep;
        }

        // ------------------------------------------------------------ 책장 (pages): 가운데 쪽으로 휜다

        static void StepPageCurve(ProjectileEntity pr, AttackStyleParams st)
        {
            if (pr.CurveLeftDegrees == 0f || pr.State != ProjectileState.Flying) return;
            float rate = st.CurveDegreesPerSecond * SimConst.TickSeconds;
            if (rate <= 0f) return;
            float step = Mathf.Sign(pr.CurveLeftDegrees) * Mathf.Min(Mathf.Abs(pr.CurveLeftDegrees), rate);
            pr.CurveLeftDegrees -= step;
            if (Mathf.Abs(pr.CurveLeftDegrees) < Eps) pr.CurveLeftDegrees = 0f;
            pr.Dir = Dir8Util.DegreesToSimVector(Dir8Util.SimVectorToDegrees(pr.Dir) + step);
        }

        // ------------------------------------------------------------ 눈알 추적탄 (seeker)

        void StepSeek(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st)
        {
            if (pr.State != ProjectileState.Flying) return;
            EnemyState target = pr.TargetEnemyId >= 0 ? w.FindEnemy(pr.TargetEnemyId) : null;
            if (target == null || !target.Targetable || pr.HitSet.Contains(target.Id))
            {
                target = AimTarget(w, pr.Pos, pr.Dir, st.SeekConeDegrees, st.SeekRange, pr.IgnoreEnemyId);
                if (target != null && pr.HitSet.Contains(target.Id)) target = null;
                pr.TargetEnemyId = target != null ? target.Id : -1;
            }
            if (target == null) return;
            Vector2 to = target.Pos - pr.Pos;
            if (to.sqrMagnitude <= Eps * Eps) return;
            float turn = st.SeekTurnDegreesPerSecond * SimConst.TickSeconds;
            // 유도 변형: 더 날카롭게 꺾는다.
            if (pr.Mods.Homing) turn += t.HomingTurnDegreesPerTick;
            float cur = Dir8Util.SimVectorToDegrees(pr.Dir);
            float delta = Mathf.Clamp(Mathf.DeltaAngle(cur, Dir8Util.SimVectorToDegrees(to)), -turn, turn);
            pr.Dir = Dir8Util.DegreesToSimVector(cur + delta);
        }

        void SplitSeeker(SimWorld w, SpellTuning t, ProjectileEntity pr, AttackStyleParams st, Vector2 at, int killedId)
        {
            int n = st.SplitCount;
            int m = CapBatch(w, t, pr.OwnerSlot, n, out float comp);
            float baseDeg = Dir8Util.SimVectorToDegrees(pr.Dir);
            for (int i = 0; i < m; i++)
            {
                float off = n <= 1 ? 0f : -st.SplitSpreadDegrees + i * (2f * st.SplitSpreadDegrees / (n - 1));
                var child = SpawnSub(w, pr, pr.Block, at, baseDeg + off, AttackStyleParams.OneIfZero(st.SplitDamageMul) * comp, killedId,
                    pr.Generation + 1);
                child.Radius = pr.Radius;
                child.Mods = pr.Mods;
                child.Mods.Homing = false;
                child.Mods.BounceCount = 0;
                child.Mods.PierceCount = 0;
            }
            w.Emit(SimEventType.StyleSplit, pr.OwnerSlot, pr.Id, a: m, b: pr.Generation + 1, pos: at, id: pr.Block.Id);
        }

        // ------------------------------------------------------------ 적의 누적 상태 (화상·냉기)

        /// <summary>화상을 건다: 겹 +n(상한), 한 겹 피해는 큰 쪽, 끝 틱은 새로. 처음 걸면 다음 화상 틱 = 지금 + 간격.</summary>
        void ApplyBurn(SimWorld w, EnemyState e, int ownerSlot, int add, float perStack, int maxStacks, int ticks, int interval)
        {
            if (e == null || !e.Targetable || ticks <= 0 || interval <= 0 || perStack <= 0f) return;
            bool active = e.BurnStacks > 0 && w.Tick < e.BurnUntilTick;
            if (!active)
            {
                e.BurnStacks = 0;
                e.BurnPerStack = 0f;
                e.BurnNextTick = w.Tick + interval;
            }
            int before = e.BurnStacks;
            e.BurnStacks = Mathf.Min(maxStacks, e.BurnStacks + add);
            if (perStack > e.BurnPerStack) e.BurnPerStack = perStack;
            e.BurnUntilTick = w.Tick + ticks;
            e.BurnOwnerSlot = ownerSlot;
            e.BurnIntervalTicks = interval;
            if (e.BurnStacks != before)
                w.Emit(SimEventType.StyleStatus, ownerSlot, e.Id, a: e.BurnStacks, b: (int)StyleStatusKind.Burn, pos: e.Pos);
        }

        /// <summary>냉기 누적: 겹 +1 → 감속(겹 × 세기, 얼음 송곳 칸) → 정해진 겹이면 빙결하고 0으로.</summary>
        void ApplyChill(SimWorld w, EnemyState e, int ownerSlot, AttackStyleParams st)
        {
            if (e == null || !e.Targetable) return;
            int ticks = StyleTicks(st.ChillSeconds);
            if (e.ChillStacks > 0 && w.Tick >= e.ChillUntilTick) e.ChillStacks = 0;
            e.ChillStacks++;
            e.ChillUntilTick = w.Tick + ticks;
            if (st.ChillSlowPerStack > 0f && ticks > 0)
                w.Mods.Enemies.ApplySlow(w, e, SlowSlot.IceSpike, Mathf.Min(1f, st.ChillSlowPerStack * e.ChillStacks), ticks, CcSource.AttackStyle);
            if (st.ChillFreezeAt > 0 && e.ChillStacks >= st.ChillFreezeAt)
            {
                e.ChillStacks = 0;
                w.Mods.Enemies.ApplyCc(w, e, CcKind.Freeze, StyleTicks(st.FreezeSeconds), CcSource.AttackStyle, Vector2.zero);
            }
            w.Emit(SimEventType.StyleStatus, ownerSlot, e.Id, a: e.ChillStacks, b: (int)StyleStatusKind.Chill, pos: e.Pos);
        }

        /// <summary>
        /// 9단계 머리: 화상이 탈 틱이면 겹 × 한 겹 피해(화염, 불바닥과 같은 지대 피해 — 콤보·트리거 없음). 끝 틱이 지났으면 꺼진다.
        /// 냉기 겹은 끝 틱이 지나면 0(감속 자체는 Enemies의 감속 칸이 센다).
        /// </summary>
        void TickStyleStatuses(SimWorld w, SpellTuning t)
        {
            var enemies = w.Enemies;
            int n = enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var e = enemies[i];
                if (e.ChillStacks > 0 && w.Tick >= e.ChillUntilTick)
                {
                    e.ChillStacks = 0;
                    w.Emit(SimEventType.StyleStatus, -1, e.Id, a: 0, b: (int)StyleStatusKind.Chill, pos: e.Pos);
                }
                if (e.BurnStacks <= 0) continue;
                if (!e.Targetable || w.Tick >= e.BurnUntilTick)
                {
                    e.BurnStacks = 0;
                    e.BurnPerStack = 0f;
                    w.Emit(SimEventType.StyleStatus, e.BurnOwnerSlot, e.Id, a: 0, b: (int)StyleStatusKind.Burn, pos: e.Pos);
                    continue;
                }
                if (w.Tick < e.BurnNextTick) continue;
                e.BurnNextTick += Mathf.Max(1, e.BurnIntervalTicks);
                var d = new DamageInput
                {
                    AttackerSlot = e.BurnOwnerSlot,
                    Amount = e.BurnPerStack * e.BurnStacks,
                    Element = Ids.Element.Fire,
                    Origin = DamageOrigin.Zone,
                    Source = ShotSource.Fire,
                    ShotId = 0,
                    IsCrit = false,
                    IgnoresArmor = false,
                    FromPos = e.Pos,
                    HitPos = e.Pos,
                    Projectile = null
                };
                ApplyDamage(w, t, e, d);
            }
        }

    }
}
