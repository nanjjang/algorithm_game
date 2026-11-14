using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Enemies
{
    // 종별 두뇌(05 요약서 6.1~6.13절)와 엘리트 능력(7절). 전부 7단계에서 돈다: 상태 전이, 이동 의도(b.Vel), 예고 요청, 탄.
    // 몸을 실제로 옮기는 것은 9단계(EnemySystem.Move.cs)다.
    public sealed partial class EnemySystem
    {
        static void Face(EnemyState e, Vector2 dir) { if (dir != Vector2.zero) e.Facing = dir; }

        /// <summary>목표 자리로 걷는다. eps 안이면 멈추고, 한 틱 거리보다 가까우면 딱 그만큼만 간다.</summary>
        static void MoveToward(EnemyBrain b, Vector2 from, Vector2 target, float speed, float eps)
        {
            Vector2 delta = target - from;
            float dist = delta.magnitude;
            if (dist <= eps) { b.Vel = Vector2.zero; return; }
            b.Vel = dist < speed * SimConst.TickSeconds ? delta * SimConst.TicksPerSecond : delta / dist * speed;
        }

        EnemyState NearestOf(SimWorld w, EnemyState self, string defId)
        {
            EnemyState best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var o = w.Enemies[i];
                if (o == self || !o.Alive || o.DefId != defId) continue;
                float d = (o.Pos - self.Pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = o; }   // 동률은 id가 작은 쪽(목록 순)
            }
            return best;
        }

        // ------------------------------------------------------------ 6.1 이끼 슬라임, 6.13 졸개

        void ThinkChaser(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            Vector2 dir = EnemyRules.Norm(p.Pos - e.Pos);
            Face(e, dir);
            b.Vel = dir * WalkSpeed(e, b);
        }

        // ------------------------------------------------------------ 6.2 덤불 늑대, 우두머리 늑대(howl)

        void ThinkWolf(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef dash = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            if (dash == null) { ThinkChaser(w, e, b, p); return; }
            HowlDispatch(w, b);
            Vector2 to = p.Pos - e.Pos;
            float d = to.magnitude;
            Vector2 dir = EnemyRules.Norm(to);
            Vector2 chase = dir * WalkSpeed(e, b);
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    if (b.Sub == BrainSub.Dodge)
                    {
                        if (w.Tick < b.SubEndTick)
                        {
                            float secs = b.Ai.F("dodge_dash_seconds");
                            b.Vel = secs > 0f ? b.DodgeDir * (b.Ai.F("dodge_distance") / secs) : Vector2.zero;
                            b.VelIsWalk = false;
                            return;
                        }
                        b.Sub = BrainSub.None;
                    }
                    if (b.Sub == BrainSub.Howl)
                    {
                        if (w.Tick < b.HowlEndTick) return;
                        // 울부짖음이 끝났다: 무리를 차례로 부르고 자신은 돌진을 잇는다(05 요약서 7절 U-71).
                        b.Sub = BrainSub.None;
                        CollectHowlTargets(w, e, b);
                        HowlDispatch(w, b);
                        b.ChainLeft = Mathf.Max(1, (int)b.Params.F("chain_count", 1f));
                        RequestAttack(w, e, b, dash, 0, p, true);
                        return;
                    }
                    if (w.Tick >= b.DodgeReadyTick && TryDodge(w, e, b, chase)) return;
                    if (w.Tick >= b.NextAttackTick && d >= dash.TriggerMin && d <= dash.TriggerMax)
                    {
                        if (b.Ability == EliteAbility.Howl)
                        {
                            // 자기 돌진 묶음마다 앞에 한 번 운다(10 요약서 Q-24).
                            b.Sub = BrainSub.Howl;
                            b.HowlEndTick = w.Tick + b.Params.Ticks("howl_seconds");
                            b.HowlTargets.Clear();
                            b.HowlNextIndex = 0;
                            SetClip(w, e, "warn");
                            return;
                        }
                        b.ChainLeft = 1;
                        RequestAttack(w, e, b, dash, 0, p, true);
                        return;
                    }
                    Face(e, dir);
                    b.Vel = chase;
                    SetClip(w, e, "move");
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    if (StrikeDue(w, e, b))
                    {
                        StartDash(w, e, b, b.Pending ?? dash);
                        SetClip(w, e, "lunge");
                    }
                    else if (e.Ai == EnemyAiState.Waiting)
                    {
                        // 대기 줄: 추적은 계속한다. 발동 거리를 벗어나면 요청을 버린다(시킨 돌진·우두머리의 연속 돌진은 유지).
                        if (b.Ability != EliteAbility.Howl && !b.ForcedDash && (d < dash.TriggerMin || d > dash.TriggerMax))
                        {
                            DropTicket(w, e, b, true);
                            return;
                        }
                        Face(e, dir);
                        b.Vel = chase;
                    }
                    break;

                case EnemyAiState.Strike:
                    if (e.DashActive && w.Tick < b.DashEndTick) break;
                    e.DashActive = false;
                    b.ChainLeft--;
                    if (b.ChainLeft > 0)
                    {
                        // 우두머리: 첫 돌진 뒤 경직 없이 둘째 예고.
                        RequestAttack(w, e, b, dash, 0, p, true);
                    }
                    else
                    {
                        BeginRecovery(w, e, b, SimConst.Ticks(dash.RecoverySeconds));
                        SetClip(w, e, "stagger");
                    }
                    break;

                case EnemyAiState.Recovery:
                    if (RecoveryDone(w, e, b)) SetClip(w, e, "move");
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        bool TryDodge(SimWorld w, EnemyState e, EnemyBrain b, Vector2 wolfVel)
        {
            float window = b.Ai.F("dodge_window_seconds");
            if (window <= 0f) return false;
            for (int i = 0; i < w.Projectiles.Count; i++)
            {
                var pr = w.Projectiles[i];
                if (pr.Dead || pr.State == ProjectileState.Pending || pr.State == ProjectileState.Dead) continue;
                // 즉발과 회피 무시 탄은 못 피한다(03 요약서: 연쇄 번개).
                if (pr.Block != null && (pr.Block.Instant || pr.Block.HasFlag("ignores_wolf_dodge"))) continue;
                Vector2 dd;
                if (!EnemyRules.WolfShouldDodge(e.Pos, wolfVel, e.BodyRadius, pr.Pos, pr.Dir * pr.Speed, pr.Radius, window, out dd)) continue;
                b.Sub = BrainSub.Dodge;
                b.DodgeDir = dd;
                b.SubEndTick = w.Tick + b.Ai.Ticks("dodge_dash_seconds");
                b.DodgeReadyTick = w.Tick + b.Ai.Ticks("dodge_cooldown_seconds");
                float secs = b.Ai.F("dodge_dash_seconds");
                b.Vel = secs > 0f ? dd * (b.Ai.F("dodge_distance") / secs) : Vector2.zero;
                b.VelIsWalk = false;
                SetClip(w, e, "dodge");
                return true;
            }
            return false;
        }

        /// <summary>반경 안의 일반 늑대(추적 중, 자신 제외)를 거리 오름차순(동률 id)으로 모은다.</summary>
        void CollectHowlTargets(SimWorld w, EnemyState e, EnemyBrain b)
        {
            b.HowlTargets.Clear();
            b.HowlNextIndex = 0;
            float radius = b.Params.F("howl_radius");
            var tmp = Scratch(w).Tmp;
            tmp.Clear();
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var o = w.Enemies[i];
                if (o == e || !o.Alive || o.IsElite || o.DefId != e.DefId || o.Ai != EnemyAiState.Act) continue;
                float od = (o.Pos - e.Pos).magnitude;
                if (od > radius) continue;
                int at = tmp.Count;
                while (at > 0 && (tmp[at - 1].Pos - e.Pos).magnitude > od) at--;
                tmp.Insert(at, o);
            }
            for (int i = 0; i < tmp.Count; i++) b.HowlTargets.Add(tmp[i].Id);
            tmp.Clear();
        }

        /// <summary>울부짖음이 끝난 틱 h + 60에서 15틱 간격으로 i번째 늑대에게 돌진을 청하게 한다(각자 상한·R5).</summary>
        void HowlDispatch(SimWorld w, EnemyBrain b)
        {
            if (b.HowlNextIndex >= b.HowlTargets.Count) return;
            int step = b.Params.Ticks("howl_step_seconds");
            while (b.HowlNextIndex < b.HowlTargets.Count && w.Tick >= b.HowlEndTick + step * b.HowlNextIndex)
            {
                EnemyState o = w.FindEnemy(b.HowlTargets[b.HowlNextIndex]);
                b.HowlNextIndex++;
                var ob = o != null ? o.Brain as EnemyBrain : null;
                if (ob == null || !o.Alive || o.IsElite || o.Ai != EnemyAiState.Act || ob.Sub != BrainSub.None || !o.CanAct(w.Tick)) continue;
                if (ob.Attacks.Length == 0) continue;
                PlayerState tp = w.NearestPlayer(o.Pos);
                if (tp == null) continue;
                ob.ChainLeft = 1;
                RequestAttack(w, o, ob, ob.Attacks[0], 0, tp, true);
                ob.ForcedDash = true;
            }
        }

        // ------------------------------------------------------------ 6.3 선돌지기, 고인돌지기(shards)

        void ThinkGolem(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            Vector2 to = p.Pos - e.Pos;
            float d = to.magnitude;
            Vector2 dir = EnemyRules.Norm(to);
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    Face(e, dir);
                    if (w.Tick >= b.NextAttackTick && b.Attacks.Length > 1)
                    {
                        AttackDef slam = b.Attacks[0], sweep = b.Attacks[1];
                        // d ≤ 3.0 휩쓸기, 3.0 < d ≤ 8 내려찍기(U-31).
                        if (d <= sweep.Range) { RequestAttack(w, e, b, sweep, 1, p, false); break; }
                        if (d <= slam.Range) { RequestAttack(w, e, b, slam, 0, p, false); break; }
                    }
                    b.Vel = dir * WalkSpeed(e, b);
                    SetClip(w, e, "move");
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    if (StrikeDue(w, e, b))
                    {
                        AttackDef atk = b.Pending;
                        int idx = b.PendingIndex;
                        AttackDef follow = idx >= 0 && idx < b.Follows.Length ? b.Follows[idx] : null;
                        if (follow != null)
                        {
                            // 판정 틱에 그 원의 중심에서 파편(첫 탄이 플레이어 쪽).
                            FireAttack(w, e, b, follow, b.TargetPos, EnemyRules.Norm(p.Pos - b.TargetPos), w.NewCastId(), 0f);
                        }
                        FinishStrike(e, b);
                        b.NextAttackTick = w.Tick + SimConst.Ticks(atk.CooldownSeconds);
                        SetClip(w, e, idx == 1 ? "sweep" : "slam");
                        BeginRecovery(w, e, b, SimConst.Ticks(atk.RecoverySeconds));
                    }
                    else if (e.Ai == EnemyAiState.Waiting)
                    {
                        Face(e, dir);
                        b.Vel = dir * WalkSpeed(e, b);
                    }
                    break;

                case EnemyAiState.Recovery:
                    SetClip(w, e, "open");
                    if (RecoveryDone(w, e, b)) SetClip(w, e, "move");
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
            // 철갑은 빈틈 동안 꺼진다(05 요약서 6.3절).
            e.ArmorActive = e.Ai != EnemyAiState.Recovery;
        }

        // ------------------------------------------------------------ 6.4 갓버섯 술사, 큰갓 술사(wide_spread)

        void BeginCast(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk, Vector2 dir, int repeats)
        {
            b.Sub = BrainSub.Cast;
            b.CastEndTick = w.Tick + SimConst.Ticks(atk.CastSeconds);
            b.CastDir = dir != Vector2.zero ? dir : e.Facing;
            b.CastId = w.NewCastId();
            b.CastRepeatLeft = repeats;
            b.CastRepeatTotal = repeats;
            e.WeaponTint = true;
            e.CurrentAttack = 0;
            SetAi(w, e, EnemyAiState.WindUp);
            SetClip(w, e, "cast");
        }

        /// <summary>구역 없는 탄 시전을 시작하는 거리: 공격의 range, 없으면 탄의 사거리(도깨비불 고리).</summary>
        static float CastRange(AttackDef atk)
        {
            if (atk.Range > 0f) return atk.Range;
            return atk.Proj != null && atk.Proj.Range.HasValue ? atk.Proj.Range.Value : float.MaxValue;
        }

        void EndCast(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk)
        {
            b.Sub = BrainSub.None;
            e.WeaponTint = false;
            b.NextAttackTick = w.Tick + SimConst.Ticks(atk.CooldownSeconds);
            BeginRecovery(w, e, b, SimConst.Ticks(atk.RecoverySeconds));
        }

        void ThinkShaman(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef atk = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            Vector2 to = p.Pos - e.Pos;
            float d = to.magnitude;
            Vector2 dir = EnemyRules.Norm(to);
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    Face(e, dir);
                    if (atk != null && atk.Proj != null && w.Tick >= b.NextAttackTick && d <= CastRange(atk))
                    {
                        BeginCast(w, e, b, atk, dir, 1);
                        break;
                    }
                    ShamanMove(w, e, b, p, d, dir);
                    break;

                case EnemyAiState.WindUp:
                    if (b.Sub != BrainSub.Cast || atk == null) { SetAi(w, e, EnemyAiState.Act); break; }
                    if (w.Tick < b.CastEndTick) break;
                    // 가운데 탄 = 시전 시작 순간의 플레이어 쪽(lock at_start).
                    FireAttack(w, e, b, atk, e.Pos, b.CastDir, b.CastId, 0f);
                    SetClip(w, e, "fire");
                    EndCast(w, e, b, atk);
                    break;

                case EnemyAiState.Recovery:
                    if (RecoveryDone(w, e, b)) SetClip(w, e, "move");
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        void ShamanMove(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p, float d, Vector2 dir)
        {
            float walk = WalkSpeed(e, b);
            if (e.Hp <= e.MaxHp * b.Ai.F("cover_hp_ratio"))
            {
                // 엄폐물 = 가장 가까운 선돌지기, 없으면 가장 가까운 가시덩굴.
                EnemyState cover = NearestOf(w, e, Ids.Enemy.GolemMenhir) ?? NearestOf(w, e, Ids.Enemy.VineThorn);
                if (cover != null)
                {
                    Vector2 target = EnemyRules.CoverPoint(cover.Pos, p.Pos, b.Ai.F("cover_offset"));
                    MoveToward(b, e.Pos, w.ClampToArena(target, e.BodyRadius), walk, Tuning(w).ArriveEpsilon);
                    SetClip(w, e, "hide");
                    return;
                }
                if (d < b.Ai.F("kite_distance_min")) b.Vel = -dir * walk;
                SetClip(w, e, "move");
                return;
            }
            if (d < b.Ai.F("keep_distance_min")) b.Vel = -dir * walk;
            else if (d > b.Ai.F("keep_distance_max")) b.Vel = dir * walk;
            SetClip(w, e, "move");
        }

        // ------------------------------------------------------------ 6.5 꽈리 벌레, 왕꽈리 벌레(big_blast)

        void ThinkBug(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef atk = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            Vector2 to = p.Pos - e.Pos;
            float d = to.magnitude;
            Vector2 dir = EnemyRules.Norm(to);
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    Face(e, dir);
                    if (atk != null && d <= atk.Range) { RequestAttack(w, e, b, atk, 0, p, false); break; }
                    b.Vel = dir * WalkSpeed(e, b);
                    SetClip(w, e, "move");
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    // 대기·점화 중엔 그 자리에 선다. 구역이 터진(9단계) 다음 틱에 스스로 죽는다 — 점화 폭발은 유폭을 남기지 않는다.
                    if (StrikeDue(w, e, b) && w.Tick > b.DetTick)
                    {
                        b.Exploded = true;
                        FinishStrike(e, b);
                        KillSelf(e);
                    }
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        // ------------------------------------------------------------ 6.6 솔침 고슴도치, 삼연 고슴도치(triple_aim)

        void HogMove(SimWorld w, EnemyState e, EnemyBrain b, float d, Vector2 dir)
        {
            // 7칸보다 가까우면 뒷걸음, 10칸보다 멀면 접근, 사이는 정지(U-41).
            float walk = WalkSpeed(e, b);
            if (d < b.Ai.F("keep_distance_min")) b.Vel = -dir * walk;
            else if (d > b.Ai.F("keep_distance_max")) b.Vel = dir * walk;
            SetClip(w, e, "move");
        }

        /// <summary>조준선의 지금 방향: 구역이 있으면 구역의 방향, 없으면 고정 틱(판정 틱 − 고정 구간)까지 플레이어를 따라간다.</summary>
        Vector2 AimDir(SimWorld w, int ticket, int detTick, AttackDef atk, Vector2 current, Vector2 toPlayer)
        {
            ZoneEntity z = FirstZone(w, ticket);
            if (z != null) return z.Dir;
            int lockTick = detTick - SimConst.Ticks(atk.LockSeconds);
            return w.Tick <= lockTick || current == Vector2.zero ? toPlayer : current;
        }

        void ThinkHog(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef atk = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            Vector2 to = p.Pos - e.Pos;
            float d = to.magnitude;
            Vector2 dir = EnemyRules.Norm(to);
            if (atk == null) { HogMove(w, e, b, d, dir); return; }
            if (b.Ability == EliteAbility.TripleAim) { ThinkHogElite(w, e, b, p, atk, d, dir); return; }
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    Face(e, dir);
                    if (w.Tick >= b.NextAttackTick && d >= atk.TriggerMin && d <= atk.TriggerMax)
                    {
                        RequestAttack(w, e, b, atk, 0, p, true);
                        break;
                    }
                    HogMove(w, e, b, d, dir);
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    {
                        // 조준 중에는 플레이어가 가까이 와도 계속 조준한다(U-42).
                        bool due = StrikeDue(w, e, b);
                        if (b.HasTicket && b.Granted)
                        {
                            b.LockedDir = AimDir(w, b.Ticket, b.DetTick, atk, b.LockedDir, dir);
                            Face(e, b.LockedDir);
                        }
                        if (due)
                        {
                            FireAttack(w, e, b, atk, e.Pos, b.LockedDir, b.CastId, 0f);
                            FinishStrike(e, b);
                            b.NextAttackTick = w.Tick + SimConst.Ticks(atk.CooldownSeconds);
                            SetClip(w, e, "fire");
                            BeginRecovery(w, e, b, SimConst.Ticks(atk.RecoverySeconds));
                        }
                    }
                    break;

                case EnemyAiState.Recovery:
                    if (e.AnimClip == "fire") SetClip(w, e, "recover");
                    if (RecoveryDone(w, e, b)) SetClip(w, e, "move");
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        void RequestAim(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk, PlayerState p, int k)
        {
            var req = new TelegraphRequest { OwnerEnemyId = e.Id, CastId = w.NewCastId() };
            req.Zones.Add(ZoneFrom(atk, e, p, true));
            b.Aims[k].Ticket = w.Mods.Threats.RequestTelegraph(w, req);
            b.Aims[k].CastId = req.CastId;
            b.Aims[k].Requested = true;
        }

        /// <summary>삼연 고슴도치: 조준선 셋을 s, s + 30, s + 60에 따로 청한다(U-73). 빈틈은 마지막 발사 뒤.</summary>
        void ThinkHogElite(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p, AttackDef atk, float d, Vector2 dir)
        {
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    Face(e, dir);
                    if (w.Tick >= b.NextAttackTick && d >= atk.TriggerMin && d <= atk.TriggerMax)
                    {
                        int n = Mathf.Max(1, (int)b.Params.F("aim_count", 1f));
                        if (b.Aims == null || b.Aims.Length != n) b.Aims = new AimSlot[n];
                        for (int k = 0; k < n; k++) b.Aims[k] = default(AimSlot);
                        b.AimFired = false;
                        RequestAim(w, e, b, atk, p, 0);
                        e.CurrentAttack = 0;
                        SetAi(w, e, EnemyAiState.Waiting);
                        break;
                    }
                    HogMove(w, e, b, d, dir);
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    {
                        if (b.Aims == null) { SetAi(w, e, EnemyAiState.Act); break; }
                        int step = b.Params.Ticks("aim_step_seconds");
                        bool allDone = true;
                        for (int k = 0; k < b.Aims.Length; k++)
                        {
                            if (b.Aims[k].Done) continue;
                            if (!b.Aims[k].Requested)
                            {
                                if (b.Aims[0].Done && !b.Aims[0].Granted) { b.Aims[k].Done = true; continue; }
                                if (b.Aims[0].Granted && w.Tick >= b.Aims[0].StartTick + step * k) RequestAim(w, e, b, atk, p, k);
                                allDone = false;
                                continue;
                            }
                            if (!b.Aims[k].Granted)
                            {
                                int s, dt;
                                if (w.Mods.Threats.TryGetTiming(w, b.Aims[k].Ticket, out s, out dt))
                                {
                                    b.Aims[k].Granted = true;
                                    b.Aims[k].StartTick = s;
                                    b.Aims[k].DetTick = dt;
                                    if (e.Ai != EnemyAiState.WindUp)
                                    {
                                        SetAi(w, e, EnemyAiState.WindUp);
                                        e.AiStateStartTick = s;
                                        e.WeaponTint = true;
                                        SetClip(w, e, "aim");
                                    }
                                }
                                else
                                {
                                    TicketStatus st = w.Mods.Threats.Status(w, b.Aims[k].Ticket);
                                    if (st != TicketStatus.Waiting && st != TicketStatus.Active) { b.Aims[k].Done = true; continue; }
                                }
                            }
                            if (b.Aims[k].Granted)
                            {
                                b.Aims[k].Dir = AimDir(w, b.Aims[k].Ticket, b.Aims[k].DetTick, atk, b.Aims[k].Dir, dir);
                                Face(e, b.Aims[k].Dir);
                                if (w.Tick >= b.Aims[k].DetTick)
                                {
                                    FireAttack(w, e, b, atk, e.Pos, b.Aims[k].Dir, b.Aims[k].CastId, 0f);
                                    b.Aims[k].Done = true;
                                    b.AimFired = true;
                                    continue;
                                }
                            }
                            allDone = false;
                        }
                        if (!allDone) break;
                        e.WeaponTint = false;
                        if (b.AimFired)
                        {
                            b.NextAttackTick = w.Tick + SimConst.Ticks(atk.CooldownSeconds);
                            SetClip(w, e, "fire");
                            BeginRecovery(w, e, b, SimConst.Ticks(atk.RecoverySeconds));
                        }
                        else SetAi(w, e, EnemyAiState.Act);
                    }
                    break;

                case EnemyAiState.Recovery:
                    if (e.AnimClip == "fire") SetClip(w, e, "recover");
                    if (RecoveryDone(w, e, b)) SetClip(w, e, "move");
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        // ------------------------------------------------------------ 6.7 화분 족제비, 화분 어미(earth_bands)

        static void SetBurrowed(EnemyState e, bool on)
        {
            e.Burrowed = on;
            if (on) e.Tags |= EnemyTags.Burrowed; else e.Tags &= ~EnemyTags.Burrowed;
        }

        void RequestSelfErupt(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk, PlayerState p)
        {
            var req = new TelegraphRequest { OwnerEnemyId = e.Id, CastId = w.NewCastId() };
            ZoneSpec z = ZoneFrom(atk, e, p, false);
            z.Anchor = ZoneAnchor.Owner;
            z.AimAtPlayer = false;
            req.Zones.Add(z);
            b.LockedDir = Vector2.zero;
            Submit(w, e, b, atk, 0, req);
            b.SelfTarget = true;
        }

        void ThinkWeasel(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef atk = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            Vector2 to = p.Pos - e.Pos;
            float d = to.magnitude;
            Vector2 dir = EnemyRules.Norm(to);
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    SetBurrowed(e, true);
                    Face(e, dir);
                    SetClip(w, e, "burrow");
                    if (atk != null)
                    {
                        // 플레이어 2칸 안 → 그 순간 플레이어 위치에. 땅속 4초 → 자기 위치에.
                        if (d <= atk.Range) { RequestAttack(w, e, b, atk, 0, p, false); break; }
                        if (w.Tick - b.BurrowStartTick >= b.Ai.Ticks("burrow_max_seconds")) { RequestSelfErupt(w, e, b, atk, p); break; }
                    }
                    b.Vel = dir * WalkSpeed(e, b);
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    if (StrikeDue(w, e, b))
                    {
                        // 판정 틱 e: 지상으로(꼬리표 없음, 이동 0, 접촉 1).
                        SetBurrowed(e, false);
                        b.Sub = BrainSub.Surface;
                        b.SubEndTick = w.Tick + b.Ai.Ticks("surface_seconds");
                        FinishStrike(e, b);
                        SetAi(w, e, EnemyAiState.Strike);
                        SetClip(w, e, "stand");
                        AttackDef follow = b.Follows.Length > 0 ? b.Follows[0] : null;
                        if (follow != null) RequestEarthBands(w, e, b, follow, p);
                    }
                    else if (e.Ai == EnemyAiState.Waiting && !b.SelfTarget && atk != null
                             && w.Tick - b.RequestTick >= b.Ai.Ticks("burrow_retarget_seconds"))
                    {
                        // 3초 더 막히면 목표를 자기 위치로 바꿔 다시 청한다(U-50).
                        w.Mods.Threats.CancelTicket(w, b.Ticket);
                        RequestSelfErupt(w, e, b, atk, p);
                    }
                    break;

                case EnemyAiState.Strike:
                    if (w.Tick < b.SubEndTick) break;
                    b.Sub = BrainSub.Dig;
                    b.SubEndTick = w.Tick + b.Ai.Ticks("dig_seconds");
                    SetAi(w, e, EnemyAiState.Recovery);
                    SetClip(w, e, "dive");
                    break;

                case EnemyAiState.Recovery:
                    if (w.Tick < b.SubEndTick) break;
                    b.Sub = BrainSub.None;
                    SetBurrowed(e, true);
                    b.BurrowStartTick = w.Tick;
                    SetAi(w, e, EnemyAiState.Act);
                    SetClip(w, e, "burrow");
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        /// <summary>화분 어미: 솟은 자리에서 흙 띠 넷을 한 묶음으로 청한다(전부 아니면 전무. 주인은 기다리지 않는다).</summary>
        void RequestEarthBands(SimWorld w, EnemyState e, EnemyBrain b, AttackDef follow, PlayerState p)
        {
            int count = Mathf.Max(1, (int)b.Params.F("band_count", 1f));
            float start = b.Params.F("band_start_angle_degrees");
            var req = new TelegraphRequest { OwnerEnemyId = e.Id, CastId = w.NewCastId() };
            for (int k = 0; k < count; k++)
            {
                ZoneSpec z = ZoneFrom(follow, e, p, false);
                z.Anchor = ZoneAnchor.Fixed;
                z.Pos = e.Pos;
                z.AimAtPlayer = false;
                z.Dir = Dir8Util.DegreesToSimVector(start + k * (360f / count));
                req.Zones.Add(z);
            }
            b.FollowTicket = w.Mods.Threats.RequestTelegraph(w, req);
            b.HasFollowTicket = true;
        }

        // ------------------------------------------------------------ 6.8 해거름 박쥐, 박쥐 여왕(summon + dive)

        void WaveFlight(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            Vector2 dir = EnemyRules.Norm(p.Pos - b.WavePoint);
            if (dir == Vector2.zero) dir = b.WaveDir != Vector2.zero ? b.WaveDir : e.Facing;
            b.WaveDir = dir;
            int period = Mathf.Max(1, b.Ai.Ticks("wave_period_seconds"));
            float phase = 2f * Mathf.PI * (w.Tick - b.WavePhase) / period;
            Vector2 target = b.WavePoint + EnemyRules.LeftOf(dir) * (b.Ai.F("wave_amplitude") * Mathf.Sin(phase));
            // 가상점 A가 플레이어 쪽으로 한 틱 나아간 자리를 겨눈다. 실제 이동량은 이동 속도로 자른다(R2 우선, C-07).
            float speed = WalkSpeed(e, b);
            target += dir * Mathf.Min(speed * SimConst.TickSeconds, (p.Pos - b.WavePoint).magnitude);
            MoveToward(b, e.Pos, target, speed, 0f);
            Face(e, dir);
        }

        void QueenSummon(SimWorld w, EnemyState e, EnemyBrain b)
        {
            if (b.SummonRatios == null) b.SummonRatios = b.Params.Floats("summon_hp_ratios");
            int count = (int)b.Params.F("summon_count");
            float radius = b.Params.F("summon_radius");
            // 불러낸 수는 SummonerId로 센다. 상한 = 한 번에 부르는 수 × 부르는 횟수.
            int cap = count * b.SummonRatios.Length;
            while (b.SummonsDone < b.SummonRatios.Length && e.Hp < e.MaxHp * b.SummonRatios[b.SummonsDone])
            {
                b.SummonsDone++;
                int room = Mathf.Min(count, cap - CountSummonedBy(w, e.Id));
                for (int k = 0; k < room; k++)
                {
                    Vector2 pos = e.Pos + Dir8Util.DegreesToSimVector(k * (360f / Mathf.Max(1, count))) * radius;
                    Spawn(w, new EnemySpawnSpec { DefId = e.DefId, Pos = pos, NoReward = true, SummonerId = e.Id, Instant = true });
                }
            }
        }

        void ThinkBat(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            if (b.Ability == EliteAbility.Summon) QueenSummon(w, e, b);
            AttackDef dive = b.Extra;
            float d = (p.Pos - e.Pos).magnitude;
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    if (dive != null && w.Tick >= b.NextAttackTick && d <= Tuning(w).QueenDiveTrigger)
                    {
                        RequestAttack(w, e, b, dive, -1, p, true);
                        break;
                    }
                    WaveFlight(w, e, b, p);
                    SetClip(w, e, "fly");
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    if (StrikeDue(w, e, b))
                    {
                        AttackDef atk = b.Pending ?? dive;
                        if (atk == null) { DropTicket(w, e, b, false); break; }
                        StartDash(w, e, b, atk);
                        b.NextAttackTick = w.Tick + SimConst.Ticks(atk.CooldownSeconds);
                        SetClip(w, e, "dive");
                    }
                    else if (e.Ai == EnemyAiState.Waiting) WaveFlight(w, e, b, p);
                    break;

                case EnemyAiState.Strike:
                    if (e.DashActive && w.Tick < b.DashEndTick) break;
                    e.DashActive = false;
                    b.WavePoint = e.Pos;
                    SetClip(w, e, "fly");
                    // 급강하 뒤 빈틈 0틱(10 요약서 Q-23).
                    BeginRecovery(w, e, b, dive != null ? SimConst.Ticks(dive.RecoverySeconds) : 0);
                    break;

                case EnemyAiState.Recovery:
                    RecoveryDone(w, e, b);
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        // ------------------------------------------------------------ 6.9 도깨비불, 쌍둥이불(twin_ring)

        void ThinkWisp(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef atk = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            Vector2 to = p.Pos - e.Pos;
            float d = to.magnitude;
            Vector2 dir = EnemyRules.Norm(to);
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    if (b.Sub == BrainSub.Vanish)
                    {
                        if (w.Tick < b.SubEndTick) break;
                        Blink(w, e, b, p);
                        b.Sub = BrainSub.Appear;
                        b.SubEndTick = w.Tick + b.Ai.Ticks("appear_seconds");
                        break;
                    }
                    if (b.Sub == BrainSub.Appear)
                    {
                        if (w.Tick < b.SubEndTick) break;
                        b.Sub = BrainSub.None;
                        b.NoCastUntilTick = w.Tick + b.Ai.Ticks("no_cast_after_appear_seconds");
                        SetClip(w, e, "hover");
                    }
                    // 깜박: 시전·빈틈 중이면 끝난 뒤(U-54) — 이 가지는 평소 상태에서만 돈다.
                    if (b.Ai.Has("blink_period_seconds") && w.Tick >= b.NextBlinkTick)
                    {
                        b.Sub = BrainSub.Vanish;
                        b.SubEndTick = w.Tick + b.Ai.Ticks("vanish_seconds");
                        b.NextBlinkTick = w.Tick + b.Ai.Ticks("blink_period_seconds");
                        SetClip(w, e, "blink");
                        break;
                    }
                    Face(e, dir);
                    if (atk != null && atk.Proj != null && w.Tick >= b.NextAttackTick && w.Tick >= b.NoCastUntilTick && d <= CastRange(atk))
                    {
                        int repeats = b.Ability == EliteAbility.TwinRing ? Mathf.Max(1, (int)b.Params.F("ring_repeat", 1f)) : 1;
                        BeginCast(w, e, b, atk, dir, repeats);
                        break;
                    }
                    {
                        // 평소 거리 5~7칸 유지(U-53).
                        float walk = WalkSpeed(e, b);
                        if (d < b.Ai.F("keep_distance_min")) b.Vel = -dir * walk;
                        else if (d > b.Ai.F("keep_distance_max")) b.Vel = dir * walk;
                        SetClip(w, e, "hover");
                    }
                    break;

                case EnemyAiState.WindUp:
                    if (b.Sub != BrainSub.Cast || atk == null) { SetAi(w, e, EnemyAiState.Act); break; }
                    if (w.Tick < b.CastEndTick) break;
                    {
                        // 첫 탄이 플레이어 쪽(U-57). 쌍둥이불의 둘째 고리는 22.5도 어긋난다.
                        int done = b.CastRepeatTotal - b.CastRepeatLeft;
                        FireAttack(w, e, b, atk, e.Pos, b.CastDir, b.CastId, done * b.Params.F("ring_repeat_angle_degrees"));
                        b.CastRepeatLeft--;
                        if (b.CastRepeatLeft > 0) { b.CastEndTick = w.Tick + b.Params.Ticks("ring_repeat_step_seconds"); break; }
                        EndCast(w, e, b, atk);
                        SetClip(w, e, "hover");
                    }
                    break;

                case EnemyAiState.Recovery:
                    RecoveryDone(w, e, b);
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        void Blink(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            int cand = (int)b.Ai.F("blink_candidates");
            if (cand <= 0) return;
            var rng = w.Rng(RngStream.EnemyAi);
            float rot = rng.Range(0f, 360f / cand);
            float r = rng.Range(b.Ai.F("blink_distance_min"), b.Ai.F("blink_distance_max"));
            // 후보 16각(난수 회전). 실패하면 반지름 6으로 다시, 그래도 없으면 생략(U-56).
            if (!TryBlinkAt(w, e, b, p, r, rot, cand)) TryBlinkAt(w, e, b, p, b.Ai.F("blink_retry_distance"), rot, cand);
        }

        bool TryBlinkAt(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p, float r, float rot, int cand)
        {
            float margin = b.Ai.F("blink_wall_margin");
            for (int k = 0; k < cand; k++)
            {
                Vector2 pos = p.Pos + Dir8Util.DegreesToSimVector(rot + k * (360f / cand)) * r;
                if (!w.InArena(pos, margin)) continue;
                bool blocked = false;
                for (int i = 0; i < w.Enemies.Count && !blocked; i++)
                {
                    var o = w.Enemies[i];
                    if (o != e && o.Alive && EnemyRules.Overlap(pos, e.BodyRadius, o.Pos, o.BodyRadius)) blocked = true;
                }
                for (int i = 0; i < w.Obstacles.Count && !blocked; i++)
                {
                    var ob = w.Obstacles[i];
                    if (!ob.Dead && EnemyRules.Overlap(pos, e.BodyRadius, ob.Pos, ob.Radius)) blocked = true;
                }
                if (blocked) continue;
                // 자기 자리를 떠나 옮겨 가는 것이라 자기 몸은 보지 않도록 예고·남는 구역만 본다.
                if (!HazardFree(w, pos, e.BodyRadius)) continue;
                e.Pos = pos;
                e.PrevPos = pos;
                b.WavePoint = pos;
                return true;
            }
            return false;
        }

        /// <summary>그 자리가 예고 구역·남는 구역과 겹치지 않는가(원으로 어림: 띠·부채는 반지름 자리에 길이를 쓴다).</summary>
        static bool HazardFree(SimWorld w, Vector2 pos, float radius)
        {
            for (int i = 0; i < w.Hazards.Count; i++)
            {
                var h = w.Hazards[i];
                if (h.Dead) continue;
                if (h.Shape == ZoneShape.Circle || h.Shape == ZoneShape.Ring || h.Shape == ZoneShape.Fan)
                {
                    if (EnemyRules.Overlap(pos, radius, h.Pos, h.Radius)) return false;
                }
                else if (EnemyRules.DistToSegment(pos, h.Pos, h.Pos + h.Dir * h.Length) < radius + h.Width / 2f) return false;
            }
            for (int i = 0; i < w.Zones.Count; i++)
            {
                var z = w.Zones[i];
                if (z.Cancelled || z.Detonated || z.Harmless) continue;
                if (z.Shape == ZoneShape.Circle || z.Shape == ZoneShape.Ring || z.Shape == ZoneShape.Fan)
                {
                    if (EnemyRules.Overlap(pos, radius, z.Pos, z.Radius)) return false;
                }
                else
                {
                    float len = z.Length > 0f ? z.Length : w.ArenaWidth + w.ArenaHeight;
                    if (EnemyRules.DistToSegment(pos, z.Pos, z.Pos + z.Dir * len) < radius + z.Width / 2f) return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------ 6.10 가시덩굴, 꽃 핀 가시덩굴(bloom)

        void ThinkVine(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef atk = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            Face(e, EnemyRules.Norm(p.Pos - e.Pos));
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    SetClip(w, e, "idle");
                    if (atk == null || w.Tick < b.NextAttackTick) break;
                    {
                        var req = new TelegraphRequest { OwnerEnemyId = e.Id, CastId = w.NewCastId() };
                        ZoneSpec z = ZoneFrom(atk, e, p, false);
                        req.Zones.Add(z);
                        if (b.Ability == EliteAbility.Bloom)
                        {
                            // 씨앗 둘을 한 묶음으로: 플레이어 위치, 그리고 마지막 이동 8방향(정지면 시선) 3칸 앞. 전장 밖이면 벽 안 0.5칸.
                            int count = Mathf.Max(1, (int)b.Params.F("seed_count", 1f));
                            Vector2 lead = Dir8Util.ToSimVector(p.Moving ? p.MoveDir : p.Facing) * b.Params.F("seed_lead_distance");
                            for (int k = 1; k < count; k++)
                            {
                                ZoneSpec z2 = z;
                                z2.Anchor = ZoneAnchor.Fixed;
                                z2.Pos = w.ClampToArena(p.Pos + lead * k, b.Params.F("seed_wall_inset"));
                                req.Zones.Add(z2);
                            }
                        }
                        b.SelfTarget = false;
                        b.LockedDir = Vector2.zero;
                        Submit(w, e, b, atk, 0, req);
                    }
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    if (StrikeDue(w, e, b))
                    {
                        // 판정과 가시밭은 Threats가 한다.
                        FinishStrike(e, b);
                        SetAi(w, e, EnemyAiState.Act);
                        SetClip(w, e, "idle");
                    }
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        // ------------------------------------------------------------ 6.11 엄니 멧돼지, 외엄니(bounce)

        void ThinkBoar(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef atk = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            Vector2 dir = EnemyRules.Norm(p.Pos - e.Pos);
            if (atk == null) { ThinkChaser(w, e, b, p); return; }
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    if (b.Sub == BrainSub.BounceStop)
                    {
                        if (w.Tick < b.SubEndTick) break;
                        // 외엄니: 벽 법선으로 반사한 방향의 새 띠를 청한다(대기 가능).
                        b.Sub = BrainSub.None;
                        var req = new TelegraphRequest { OwnerEnemyId = e.Id, CastId = w.NewCastId() };
                        ZoneSpec z = ZoneFrom(atk, e, p, true);
                        z.AimAtPlayer = false;
                        z.Dir = b.BounceDir;
                        req.Zones.Add(z);
                        b.SelfTarget = false;
                        Submit(w, e, b, atk, 0, req);
                        b.LockedDir = b.BounceDir;
                        b.Bounced = true;
                        break;
                    }
                    Face(e, dir);
                    {
                        ObstacleEntity hit;
                        // 플레이어와 직선이 열리면 준비(다른 적은 무시, U-61).
                        if (w.Tick >= b.NextAttackTick && !w.Mods.Threats.SegmentHitsObstacle(w, e.Pos, p.Pos, false, out hit))
                        {
                            b.BouncesLeft = b.Ability == EliteAbility.Bounce ? (int)b.Params.F("bounce_count") : 0;
                            b.Bounced = false;
                            RequestAttack(w, e, b, atk, 0, p, true);
                            break;
                        }
                    }
                    b.Vel = dir * WalkSpeed(e, b);
                    SetClip(w, e, "move");
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    if (StrikeDue(w, e, b))
                    {
                        StartDash(w, e, b, atk);
                        if (!b.Bounced) b.NextAttackTick = w.Tick + SimConst.Ticks(atk.CooldownSeconds);
                        SetClip(w, e, "charge");
                    }
                    break;

                case EnemyAiState.Strike:
                    // 벽 충돌은 9단계가 처리하며 상태를 옮긴다(기절 또는 튕김 멈춤).
                    if (!e.DashActive) SetAi(w, e, EnemyAiState.Act);
                    break;

                case EnemyAiState.Recovery:
                    if (b.Sub != BrainSub.WallStun) SetAi(w, e, EnemyAiState.Act);
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        // ------------------------------------------------------------ 6.12 북두꺼비, 큰북 두꺼비(big_drum)

        void ToadMove(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p, Vector2 dir)
        {
            float walk = WalkSpeed(e, b);
            float radius = b.Ai.F("follow_radius");
            Vector2 sum = Vector2.zero;
            int n = 0;
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var o = w.Enemies[i];
                if (o == e || !o.Alive || o.IsBoss) continue;
                if ((o.Pos - e.Pos).magnitude > radius) continue;
                sum += o.Pos;
                n++;
            }
            // 다른 적이 없으면 플레이어 접근(U-64).
            if (n == 0) { b.Vel = dir * walk; return; }
            Vector2 c = sum / n;
            Vector2 target = c + EnemyRules.Norm(c - p.Pos) * b.Ai.F("follow_offset");
            MoveToward(b, e.Pos, w.ClampToArena(target, e.BodyRadius), walk, Tuning(w).ArriveEpsilon);
        }

        void ThinkToad(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            AttackDef atk = b.Attacks.Length > 0 ? b.Attacks[0] : null;
            Vector2 to = p.Pos - e.Pos;
            float d = to.magnitude;
            Vector2 dir = EnemyRules.Norm(to);
            switch (e.Ai)
            {
                case EnemyAiState.Act:
                    Face(e, dir);
                    if (b.Ai.Has("croak_period_seconds") && w.Tick >= b.NextCroakTick)
                    {
                        // 주기는 울음 시작 기준이고 취소돼도 유지된다(U-65).
                        b.Sub = BrainSub.Croak;
                        b.SubEndTick = w.Tick + b.Ai.Ticks("croak_seconds");
                        b.NextCroakTick = w.Tick + b.Ai.Ticks("croak_period_seconds");
                        b.CroakDamage = 0f;
                        SetAi(w, e, EnemyAiState.WindUp);
                        SetClip(w, e, "call");
                        break;
                    }
                    if (atk != null && atk.Kind != "contact" && w.Tick >= b.NextAttackTick && d <= atk.Range)
                    {
                        RequestAttack(w, e, b, atk, 0, p, false);
                        break;
                    }
                    ToadMove(w, e, b, p, dir);
                    SetClip(w, e, "hop");
                    break;

                case EnemyAiState.Waiting:
                case EnemyAiState.WindUp:
                    if (b.Sub == BrainSub.Croak)
                    {
                        float breakDamage = (b.Params.Has("croak_break_damage") ? b.Params.F("croak_break_damage") : b.Ai.F("croak_break_damage")) * HpMul(w);
                        if (b.CroakDamage >= breakDamage)
                        {
                            // 울음 끊기: 취소 + 경직, 버프 없음.
                            int stagger = b.Ai.Ticks("croak_stagger_seconds");
                            b.Sub = BrainSub.None;
                            e.StaggerUntilTick = Mathf.Max(e.StaggerUntilTick, w.Tick + stagger);
                            SetAi(w, e, EnemyAiState.Interrupted);
                            w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)CcKind.Stagger, b: stagger);
                            break;
                        }
                        if (w.Tick < b.SubEndTick) break;
                        b.Sub = BrainSub.None;
                        ApplyDrumBuff(w, e, b);
                        SetAi(w, e, EnemyAiState.Act);
                        break;
                    }
                    if (StrikeDue(w, e, b))
                    {
                        AttackDef used = b.Pending ?? atk;
                        FinishStrike(e, b);
                        b.NextAttackTick = w.Tick + SimConst.Ticks(used.CooldownSeconds);
                        SetClip(w, e, "tongue");
                        BeginRecovery(w, e, b, SimConst.Ticks(used.RecoverySeconds));
                    }
                    break;

                case EnemyAiState.Recovery:
                    if (RecoveryDone(w, e, b)) SetClip(w, e, "hop");
                    break;

                default:
                    SetAi(w, e, EnemyAiState.Act);
                    break;
            }
        }

        /// <summary>울음이 끝났다: 반경 안의 다른 적(보스 제외)에게 버프. 다시 받으면 만료만 갱신한다.</summary>
        void ApplyDrumBuff(SimWorld w, EnemyState e, EnemyBrain b)
        {
            float radius = b.Params.Has("croak_radius") ? b.Params.F("croak_radius") : b.Ai.F("buff_radius");
            int ticks = b.Ai.Ticks("buff_seconds");
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var o = w.Enemies[i];
                if (o == e || !o.Alive || o.IsBoss) continue;
                var ob = o.Brain as EnemyBrain;
                if (ob == null) continue;
                if ((o.Pos - e.Pos).magnitude > radius) continue;
                ob.BuffUntilTick = w.Tick + ticks;
                ob.BuffSpeedRatio = b.Ai.F("buff_speed_ratio", 1f);
                ob.BuffSpeedCap = b.Ai.F("buff_speed_cap");
                ob.BuffRecoveryRatio = b.Ai.F("buff_recovery_ratio", 1f);
                if (!o.DrumBuffed)
                {
                    o.DrumBuffed = true;
                    w.Emit(SimEventType.EnemyBuff, entityId: o.Id, id: EnemyTuning.DrumBuffId, a: 1);
                }
            }
        }

        // ------------------------------------------------------------ 허수아비

        void ThinkDummy(SimWorld w, EnemyState e, EnemyBrain b, PlayerState p)
        {
            if (e.Ai == EnemyAiState.Spawning)
            {
                if (w.Tick - e.AiStateStartTick < b.SpawnTicks) return;
                FinishSpawn(w, e, b);
            }
            // 움직이지 않고 체력이 바닥나지 않는다.
            e.Hp = e.MaxHp;
            // 연습 옵션 "적 탄 쏘기"(08 요약서 8.5절): 켜져 있는 동안만 일정 간격으로 직선 탄 하나.
            if (!w.Run.PracticeEnemyFire) { b.NextFireTick = -1; b.Shots.Clear(); return; }
            if (p == null) return;
            FlushShots(w, b);
            var tun = Tuning(w);
            int interval = Mathf.Max(1, SimConst.Ticks(tun.DummyFireIntervalSeconds));
            if (b.NextFireTick < 0) b.NextFireTick = w.Tick + interval;
            if (w.Tick < b.NextFireTick) return;
            b.NextFireTick = w.Tick + interval;
            Vector2 dir = EnemyRules.Norm(p.Pos - e.Pos);
            Face(e, dir);
            FireShots(w, e, b, e.Pos, dir, 1, 0f, false, tun.DummyShotSpeed, tun.DummyShotRadius, -1f, tun.DummyShotDamage, true, w.NewCastId(), 0f);
        }
    }
}
