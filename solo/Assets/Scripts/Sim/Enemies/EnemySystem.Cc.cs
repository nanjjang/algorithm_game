using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Enemies
{
    // 적 쪽 피해 조정, 꼬리표별 군중 제어, 감속·잠식, 돌진 취소, 사망(10단계), 치우기. 05 요약서 3.3절·5절, 02 요약서 8.5절, 03 요약서 10절.
    public sealed partial class EnemySystem
    {
        // ------------------------------------------------------------ 받는 피해

        public override float ModifyIncomingDamage(SimWorld w, EnemyState e, float amount, bool isCrit, bool ignoresArmor, Vector2 fromPos, Vector2 hitPos)
        {
            if (e == null || !e.Alive) return 0f;
            // 땅속: 피해 포함 어떤 효과도 없다.
            if (e.Burrowed) return 0f;
            if (e.IsBoss) return w.Mods.Bosses.ModifyIncomingDamage(w, e, amount, isCrit, fromPos, hitPos);
            // 등장 예고 동안은 피해를 주고받지 않는다(U-07).
            if (e.Ai == EnemyAiState.Spawning) return 0f;

            var b = e.Brain as EnemyBrain;
            float a = amount;
            // 곱 순서(U-18): (기본·상성은 이미 곱해져 온다) → 철갑 → 기절.
            if (b != null && e.ArmorActive && !isCrit && !ignoresArmor && e.Ai != EnemyAiState.Recovery)
            {
                float half = b.Params.Has("armor_half_angle_degrees") ? b.Params.F("armor_half_angle_degrees") : b.Ai.F("armor_half_angle_degrees");
                if (EnemyRules.InFrontArc(e.Pos, e.Facing, fromPos, half))
                {
                    float fallback = w.Content != null && w.Content.Const != null ? w.Content.Const.ArmorDamageMul : 1f;
                    a *= b.Ai.F("armor_damage_ratio", fallback);
                }
            }
            a *= e.DamageTakenMul;
            // 북두꺼비 울음 끊기: 우는 동안 받은 피해를 쌓는다.
            if (b != null && b.Sub == BrainSub.Croak) b.CroakDamage += a;
            return a;
        }

        // ------------------------------------------------------------ 군중 제어

        static bool CcBlocked(EnemyState e) => e == null || !e.Alive || e.Burrowed || (!e.IsBoss && e.Ai == EnemyAiState.Spawning);

        static EnemyTags EffectiveTags(EnemyState e)
        {
            EnemyTags t = e.Tags;
            if (e.IsBoss) t |= EnemyTags.Boss;
            if (e.IsElite) t |= EnemyTags.Elite;
            return t;
        }

        public override float ApplyCc(SimWorld w, EnemyState e, CcKind kind, float amount, CcSource source, Vector2 dir)
        {
            // 죽어 가는 적·땅속·등장 중인 적에는 아무 일도 없다.
            if (CcBlocked(e) || amount <= 0f) return 0f;
            EnemyTags tags = EffectiveTags(e);
            float applied;
            CcVerdict verdict = EnemyRules.ResolveCc(kind, tags, source, amount, out applied);
            var tun = Tuning(w);
            var b = e.Brain as EnemyBrain;
            switch (verdict)
            {
                case CcVerdict.None:
                    return 0f;

                case CcVerdict.BossHalt:
                    {
                        // 뇌광창 → 보스 동작 멈춤 0.3초(18틱).
                        var common = w.Content != null ? w.Content.BossCommon : null;
                        int halt = common != null ? SimConst.Ticks(common.ActionHaltSeconds) : 0;
                        if (halt <= 0) return 0f;
                        w.Mods.Bosses.ActionHalt(w, e, halt);
                        w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)CcKind.Stun, b: halt);
                        return halt;
                    }

                case CcVerdict.ConvertToSlow:
                    // 살얼음 빙결 → 중량은 감속 −30%, 보스는 −15%(보스 절반은 ApplySlow가 한다). 빙결은 걸리지 않았으므로 0을 돌려준다.
                    ApplySlow(w, e, SlowSlot.ThinIce, tun.HeavyFreezeSlow, EnemyRules.RoundTicks(applied), source);
                    return 0f;
            }

            switch (kind)
            {
                case CcKind.Knockback:
                case CcKind.Pull:
                    {
                        if (e.Def != null && e.Def.KnockbackImmune) return 0f;
                        Vector2 d = EnemyRules.Norm(dir);
                        if (d == Vector2.zero) return 0f;
                        Vector2 from = e.Pos;
                        e.Pos = PushTo(w, e, from, d, applied);
                        float moved = (e.Pos - from).magnitude;
                        if (b != null) b.WavePoint += e.Pos - from;
                        w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)kind, f: moved);
                        return moved;
                    }

                case CcKind.Freeze:
                    {
                        // 빙결 내성: 한 번 언 적은 6초 동안 다시 얼지 않는다(기준 = 빙결 시작 틱, U-67).
                        if (w.Tick < e.FreezeImmuneUntilTick) return 0f;
                        int ticks = EnemyRules.RoundTicks(applied);
                        if (ticks <= 0) return 0f;
                        e.FrozenUntilTick = Mathf.Max(e.FrozenUntilTick, w.Tick + ticks);
                        var c = w.Content != null ? w.Content.Const : null;
                        e.FreezeImmuneUntilTick = w.Tick + (c != null ? SimConst.Ticks(c.FreezeImmuneSeconds) : ticks);
                        HardCcInterrupt(w, e, b, tags);
                        w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)kind, b: ticks);
                        return ticks;
                    }

                case CcKind.Stagger:
                    {
                        int ticks = EnemyRules.RoundTicks(applied);
                        if (ticks <= 0) return 0f;
                        e.StaggerUntilTick = Mathf.Max(e.StaggerUntilTick, w.Tick + ticks);
                        // 경직은 예고를 취소하지 않는다. 달리던 돌진만 끊는다(얼음 병풍 충돌).
                        if (b != null && e.DashActive) Interrupt(w, e, b);
                        w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)kind, b: ticks);
                        return ticks;
                    }

                case CcKind.Stun:
                    {
                        int ticks = EnemyRules.RoundTicks(applied);
                        if (ticks <= 0) return 0f;
                        e.StunUntilTick = Mathf.Max(e.StunUntilTick, w.Tick + ticks);
                        HardCcInterrupt(w, e, b, tags);
                        w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)kind, b: ticks);
                        return ticks;
                    }

                default:
                    {
                        // 감속: ApplyCc에는 시간 인자가 없어 기본 시간을 쓴다. 칸은 출처로 고른다.
                        SlowSlot slot = source == CcSource.Kiln ? SlowSlot.Kiln : source == CcSource.ThinIce ? SlowSlot.ThinIce : SlowSlot.Frost;
                        ApplySlow(w, e, slot, applied, SimConst.Ticks(tun.CcSlowDefaultSeconds), source);
                        return (tags & EnemyTags.Boss) != 0 ? EnemyRules.Half(applied) : applied;
                    }
            }
        }

        /// <summary>빙결·기절: 꼬리표 없는 일반 적과 뿌리 적은 예고·시전·돌진이 취소된다(엘리트는 유지). 05 요약서 1.3절.</summary>
        void HardCcInterrupt(SimWorld w, EnemyState e, EnemyBrain b, EnemyTags tags)
        {
            if (b == null || e.IsBoss) return;
            bool busy = e.Ai == EnemyAiState.Waiting || e.Ai == EnemyAiState.WindUp || e.DashActive;
            if (!busy) return;
            if (!EnemyRules.CcCancelsTelegraph(tags)) return;
            Interrupt(w, e, b);
        }

        /// <summary>하던 공격을 끊는다: 미판정 예고·대기 요청 취소, 시전·울음·울부짖음·돌진 중단 → Interrupted(풀리면 Act).</summary>
        void Interrupt(SimWorld w, EnemyState e, EnemyBrain b)
        {
            w.Mods.Threats.CancelTelegraphsOf(w, e.Id);
            b.HasTicket = false;
            b.Granted = false;
            b.HasFollowTicket = false;
            b.Pending = null;
            b.PendingIndex = -1;
            b.ForcedDash = false;
            b.ChainLeft = 0;
            b.BouncesLeft = 0;
            b.HowlTargets.Clear();
            b.HowlNextIndex = 0;
            if (b.Aims != null) for (int k = 0; k < b.Aims.Length; k++) b.Aims[k].Done = true;
            if (b.Sub == BrainSub.Cast || b.Sub == BrainSub.Howl || b.Sub == BrainSub.Croak || b.Sub == BrainSub.Dodge) b.Sub = BrainSub.None;
            e.DashActive = false;
            e.WeaponTint = false;
            e.CurrentAttack = -1;
            SetAi(w, e, EnemyAiState.Interrupted);
        }

        /// <summary>넉백이 장애물에 막힐 때 멈출 자리를 찾는 이분 횟수.</summary>
        const int PushSearchSteps = 8;

        /// <summary>넉백·당김의 도착 자리: 전장 벽과 장애물 앞에서 멈춘다.</summary>
        static Vector2 PushTo(SimWorld w, EnemyState e, Vector2 from, Vector2 dir, float distance)
        {
            Vector2 to = from + dir * distance;
            ObstacleEntity hit;
            if (w.Mods.Threats.SegmentHitsObstacle(w, from, to, false, out hit))
            {
                // 막히지 않는 가장 먼 자리를 이분으로 찾는다.
                float lo = 0f, hi = distance;
                for (int i = 0; i < PushSearchSteps; i++)
                {
                    float mid = (lo + hi) / 2f;
                    if (w.Mods.Threats.SegmentHitsObstacle(w, from, from + dir * mid, false, out hit)) hi = mid; else lo = mid;
                }
                to = from + dir * lo;
            }
            return w.ClampToArena(to, e.BodyRadius);
        }

        public override void ApplySlow(SimWorld w, EnemyState e, SlowSlot slot, float strength, int ticks, CcSource source)
        {
            // 땅속에는 안 걸린다. 비행은 걸린다. 빙결 내성과 무관하게 걸린다.
            if (CcBlocked(e) || strength <= 0f || ticks <= 0) return;
            if (slot < 0 || slot >= SlowSlot.Count) return;
            // 보스는 절반. 엘리트·중량은 그대로.
            if (e.IsBoss || e.Has(EnemyTags.Boss)) strength = EnemyRules.Half(strength);
            // 같은 출처 칸은 덮어쓴다(세기도 새 값). 실효 감속은 칸 중 가장 큰 값 하나.
            e.Slows[(int)slot] = new SlowEntry { Strength = Mathf.Clamp01(strength), UntilTick = w.Tick + ticks };
            w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)CcKind.Slow, b: ticks, f: strength);
        }

        public override void ApplyCorrosion(SimWorld w, EnemyState e, int ticks)
        {
            // 다시 걸면 시간만 새로. 땅속 무시.
            if (CcBlocked(e) || ticks <= 0) return;
            e.CorrosionUntilTick = w.Tick + ticks;
        }

        public override bool TryCancelDash(SimWorld w, EnemyState e, int staggerTicks)
        {
            // 꼬리표 없는 일반 적(덤불 늑대)의 돌진 예고만 끊긴다(05 요약서 1.3절). 엘리트·중량·비행은 아니다.
            if (CcBlocked(e) || e.IsBoss || e.IsElite || e.Tags != EnemyTags.None) return false;
            var b = e.Brain as EnemyBrain;
            if (b == null || e.Ai != EnemyAiState.WindUp || b.Pending == null || !b.Pending.IsDash) return false;
            Interrupt(w, e, b);
            if (staggerTicks > 0)
            {
                e.StaggerUntilTick = Mathf.Max(e.StaggerUntilTick, w.Tick + staggerTicks);
                w.Emit(SimEventType.EnemyCc, entityId: e.Id, a: (int)CcKind.Stagger, b: staggerTicks);
            }
            SetClip(w, e, "stagger");
            return true;
        }

        // ------------------------------------------------------------ 사망(10단계)과 치우기

        public override void Stage10_Deaths(SimWorld w)
        {
            // 훅은 RunPhase와 무관하게 부른다. 판이 끝난 뒤의 처리는 Run·Bosses가 스스로 거른다(D-45).
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var e = w.Enemies[i];
                if (e.Dead || !e.PendingDeath) continue;
                var b = e.Brain as EnemyBrain;
                // 00 문서 5.4절 (다): 부수효과 → 예고 취소 → Run → Traits → Waves → 이벤트 → Dead.
                if (b != null && !e.IsBoss) DeathEffects(w, e, b);
                w.Mods.Threats.CancelTelegraphsOf(w, e.Id);
                w.Mods.Run.OnEnemyKilled(w, e, e.KillingHit);
                w.Mods.Traits.OnEnemyKilled(w, e, e.KillingHit);
                w.Mods.Waves.OnEnemyKilled(w, e);
                int kind = e.IsBoss ? 2 : e.NoReward ? 3 : e.IsElite ? 1 : 0;
                var drops = w.Content != null ? w.Content.Drops : null;
                int gold = e.IsBoss || e.NoReward || drops == null ? 0 : e.Cost * drops.GoldPerCost;
                w.Emit(SimEventType.EnemyKilled, entityId: e.Id, id: e.DefId, a: gold, b: kind, pos: e.Pos);
                e.Dead = true;
                e.Ai = EnemyAiState.Dead;
                e.DashActive = false;
                e.WeaponTint = false;
            }
        }

        void DeathEffects(SimWorld w, EnemyState e, EnemyBrain b)
        {
            if (b.Ability == EliteAbility.Split)
            {
                // 왕이끼 슬라임: 사망 위치 둘레에 슬라임 셋(엘리트 아님, 보상 0). 동시 개체 상한과 무관하게 만든다(U-70).
                int count = (int)b.Params.F("split_count");
                float radius = b.Params.F("split_radius");
                float start = b.Params.F("split_start_angle_degrees");
                for (int k = 0; k < count; k++)
                {
                    Vector2 pos = e.Pos + Dir8Util.DegreesToSimVector(start + k * (360f / count)) * radius;
                    Spawn(w, new EnemySpawnSpec { DefId = e.DefId, Pos = pos, NoReward = true, SummonerId = e.Id, Instant = true });
                }
            }
            if (b.Species == EnemySpecies.Bug && !b.Exploded && b.Attacks.Length > 0 && b.Attacks[0] != null)
            {
                // 유폭: 점화 폭발 전에 죽으면(점화 중 포함, U-38) 0.4초 뒤 같은 반경으로 터져 적에게만 20 × H(w)(왕꽈리 60).
                float damage = (b.Params.Has("sympathetic_damage") ? b.Params.F("sympathetic_damage") : b.Ai.F("sympathetic_damage")) * HpMul(w);
                Scratch(w).Blasts.Add(new PendingBlast
                {
                    Tick = w.Tick + b.Ai.Ticks("sympathetic_delay_seconds"),
                    Pos = e.Pos,
                    Radius = b.Attacks[0].Radius,
                    Damage = damage
                });
            }
        }

        public override void RemoveAll(SimWorld w, bool minionsOnly)
        {
            // 보상·사망 부수효과·훅 없이 치운다. minionsOnly = IsBoss가 아닌 전부.
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (e.Dead) continue;
                if (minionsOnly && e.IsBoss) continue;
                w.Mods.Threats.CancelTelegraphsOf(w, e.Id);
                e.Dead = true;
                e.Ai = EnemyAiState.Dead;
                e.DashActive = false;
                e.WeaponTint = false;
                var b = e.Brain as EnemyBrain;
                if (b != null) b.Shots.Clear();
            }
            Scratch(w).Blasts.Clear();
        }
    }
}
