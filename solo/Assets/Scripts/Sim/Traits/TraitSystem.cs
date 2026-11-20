using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Traits
{
    /// <summary>
    /// 캐릭터 패시브 5종, 지팡이 특성 5종, 변이 6종(02 요약서 6.1·8·9절, 03 요약서 9·11절, 07 요약서 4.7·4.10절).
    /// 수치는 전부 Def에서 읽는다(characters.json, solo/const.json, solo/mutations.json, solo/enhance.json, tries.json).
    /// 모듈 객체는 판 상태를 갖지 않는다: 플레이어 쪽 상태는 PlayerState.TraitScratch(TraitScratch), 적 쪽 상태는
    /// EnemyState.ThinIceCount·FreezeImmuneUntilTick에 둔다.
    /// </summary>
    public sealed class TraitSystem : TraitModule
    {
        /// <summary>대상 고르기 순서: 거리 오름차순, 같으면 id 오름차순(02 요약서 8.3절). 동률 없는 완전 순서다.</summary>
        static readonly Comparison<TraitTarget> ByDistanceThenId = (a, b) =>
        {
            int c = a.SqrDistance.CompareTo(b.SqrDistance);
            return c != 0 ? c : a.Enemy.Id.CompareTo(b.Enemy.Id);
        };

        static readonly Comparison<TraitTarget> ById = (a, b) => a.Enemy.Id.CompareTo(b.Enemy.Id);

        // ------------------------------------------------------------ PlayerDerived

        public override PlayerDerived ComputeDerived(IContentDb db, CharacterDef ch, int effectiveStage, IReadOnlyList<string> mutations)
        {
            var d = base.ComputeDerived(db, ch, effectiveStage, mutations);
            if (db == null) return d;

            var mc = db.Mutations;
            var cc = db.Const;

            d.TwistedBarrel = HasMutation(mutations, Ids.Mutation.TwistedBarrel);
            d.UnstableCore = HasMutation(mutations, Ids.Mutation.UnstableCore);
            d.Hypersensitive = HasMutation(mutations, Ids.Mutation.HypersensitiveCircuit);
            d.Gluttony = HasMutation(mutations, Ids.Mutation.Gluttony);
            d.MirrorImage = HasMutation(mutations, Ids.Mutation.MirrorImage);
            d.GlassWand = HasMutation(mutations, Ids.Mutation.GlassWand);

            if (ch != null)
            {
                // 임계 시작: 여럿이면 가장 낮은 값(02 요약서 6.1절). 지팡이 특성 80은 characters.json의 crit_start에 이미 들어 있다.
                float critStart = ch.CritStart;
                if (d.Hypersensitive && mc != null && mc.HypersensitiveCritStart < critStart) critStart = mc.HypersensitiveCritStart;
                d.CritStartHu = SimConst.HeatToHu(critStart);

                // 잠금: 과민 회로 +1.5초(+90틱)를 여기서 더한다(02 요약서 9.1절: 4.5 / 4.0 / 5.0 / 4.5 / 4.5초).
                d.LockTicks = SimConst.Ticks(ch.LockSeconds);
                if (d.Hypersensitive && mc != null) d.LockTicks += SimConst.Ticks(mc.HypersensitiveLockPlusSeconds);

                // 냉각(HU/틱): 45 → 90, 50 → 100. 탐식이면 절반(02 요약서 6.4절).
                float coolHuPerSecond = SimConst.HeatToHu(ch.CoolPerSecond);
                if (d.Gluttony && mc != null) coolHuPerSecond *= mc.GluttonyCoolMul;
                d.CoolHuPerTick = (int)Math.Round(coolHuPerSecond / SimConst.TicksPerSecond, MidpointRounding.AwayFromZero);

                d.CoolDelayTicks = SimConst.Ticks(ch.CoolDelaySeconds);
                d.GrazeBuffTicks = SimConst.Ticks(ch.GrazeBuffSeconds);
                d.InvulnTicks = SimConst.Ticks(ch.InvulnSeconds);

                d.Underline = ch.Passive == Ids.Passive.Underline;
                d.KeepCrystal = ch.WandTrait == Ids.WandTrait.KeepCrystal;
                d.HasCredit = ch.WandTrait == Ids.WandTrait.OnCredit;
                d.MaxDebt = d.HasCredit && db.Tries != null && db.Tries.Parser != null ? db.Tries.Parser.MaxDebt : 0;
            }

            // 깊이 감쇠: 지팡이 특성이면 0.92, 아니면 0.85(00 문서 D-06).
            if (cc != null)
                d.DepthDecay = ch != null && ch.WandTrait == Ids.WandTrait.DepthDecay ? cc.DepthDecayTrait : cc.DepthDecayDefault;

            // 강화 배율 = stage_damage_base ^ 실효 단계(07 요약서 4.10절).
            int stage = effectiveStage < 0 ? 0 : effectiveStage;
            if (db.Enhance != null && db.Enhance.StageDamageBase > 0f)
                d.EnhanceMul = (float)Math.Pow(db.Enhance.StageDamageBase, stage);

            if (mc != null)
            {
                d.MutationDamageMul = d.TwistedBarrel ? mc.TwistedBarrelDamageMul : 1f;
                d.CritBonus = d.GlassWand ? mc.GlassWandCritBonus : 0;
            }
            return d;
        }

        public override void RecomputeDerived(SimWorld w, PlayerState p)
        {
            p.Derived = ComputeDerived(w.Content, p.Def, p.Wand.EffectiveStage, p.Wand.Mutations);
            var s = Scratch(p);
            var mc = w.Content != null ? w.Content.Mutations : null;
            s.ExplosionRadiusMul = p.Derived.UnstableCore && mc != null ? mc.UnstableCoreRadiusMul : 1f;

            if (!p.Derived.UnstableCore)
            {
                ClearUnstableRoll(s);
                return;
            }
            // 판의 첫 발사분은 여기서 굴린다. 이미 굴려 둔 것이 있으면(유리 지팡이 임시 하락 뒤 다시 불림) 다시 굴리지 않는다.
            if (!s.HasUnstableRoll && CoreExplodes(w, p)) RollNextShot(w, p, s, 0);
        }

        // ------------------------------------------------------------ 끓어넘침 (passive.overflow)

        public override void OnHeatLockEntered(SimWorld w, PlayerState p, bool fromUnstableCore)
        {
            if (p == null || p.Def == null || p.Def.Passive != Ids.Passive.Overflow) return;
            // 불안정 핵 자폭 잠금은 끓어넘침을 일으키지 않는다(00 문서 D-04).
            if (fromUnstableCore) return;
            var cc = w.Content.Const;
            if (cc == null) return;

            var s = Scratch(p);
            CollectTargets(w, p.Pos, cc.OverflowRadius, s.Targets);
            s.Targets.Sort(ById);

            w.Emit(SimEventType.BoiloverTriggered, p.Slot, pos: p.Pos);

            float amount = cc.OverflowDamageT * w.Mods.Spells.StandardT(w, p) * CharacterMul(w, p, p.Crystals);
            for (int i = 0; i < s.Targets.Count; i++)
            {
                var e = s.Targets[i].Enemy;
                if (!e.Targetable) continue;
                w.Mods.Spells.DealDirect(w, new DirectDamage
                {
                    AttackerSlot = p.Slot, TargetEnemyId = e.Id, Amount = amount, Element = p.Def.Element,
                    Origin = DamageOrigin.Passive, SourceId = Ids.Passive.Overflow
                });
                // 이 피해로 죽어 가는 적은 남은 단계에서 없는 것이다(공통 규칙 2). 넉백의 꼬리표별 변환은 Enemies가 한다.
                if (!e.Targetable) continue;
                Vector2 delta = e.Pos - p.Pos;
                Vector2 dir = delta.sqrMagnitude > 1e-12f ? delta.normalized : Dir8Util.ToSimVector(p.Facing);
                w.Mods.Enemies.ApplyCc(w, e, CcKind.Knockback, cc.OverflowKnockback, CcSource.Overflow, dir);
            }
            s.Targets.Clear();
        }

        // ------------------------------------------------------------ 피뢰침 체질 (passive.lightning_rod)

        public override void OnGrazeAwarded(SimWorld w, PlayerState p, int castId)
        {
            if (p == null || p.Def == null || p.Def.Passive != Ids.Passive.LightningRod) return;
            var cc = w.Content.Const;
            if (cc == null) return;

            var s = Scratch(p);
            CollectTargets(w, p.Pos, cc.LightningRodRadius, s.Targets);
            s.Targets.Sort(ByDistanceThenId);
            int n = Mathf.Min(cc.LightningRodTargets, s.Targets.Count);

            float amount = cc.LightningRodDamageT * w.Mods.Spells.StandardT(w, p) * CharacterMul(w, p, p.Crystals);
            for (int i = 0; i < n; i++)
            {
                var e = s.Targets[i].Enemy;
                w.Emit(SimEventType.LightningRodStrike, p.Slot, e.Id, pos: e.Pos);
                w.Mods.Spells.DealDirect(w, new DirectDamage
                {
                    AttackerSlot = p.Slot, TargetEnemyId = e.Id, Amount = amount, Element = p.Def.Element,
                    Origin = DamageOrigin.Passive, SourceId = Ids.Passive.LightningRod
                });
            }
            s.Targets.Clear();
        }

        // ------------------------------------------------------------ 살얼음 (passive.thin_ice)

        public override void OnEnemyHit(SimWorld w, in HitInfo hit)
        {
            var p = PlayerOf(w, hit.AttackerSlot);
            if (p == null || p.Def == null || p.Def.Passive != Ids.Passive.ThinIce) return;
            if (hit.Origin == DamageOrigin.Environment) return;
            var cc = w.Content.Const;
            if (cc == null) return;
            var e = w.FindEnemy(hit.EnemyId);
            if (e == null || e.Dead) return;
            // 땅속: 효과 없음, 횟수 유지.
            if (e.Burrowed) return;
            // Immune: 빙결 시작부터 6초 동안 적중을 무시한다. 끝나면 Counting(0)부터 — 횟수는 빙결 때 0으로 돌려 두었다.
            if (w.Tick < e.FreezeImmuneUntilTick) return;

            e.ThinIceCount++;
            if (e.ThinIceCount < cc.ThinIceHits) return;

            e.ThinIceCount = 0;
            int freezeTicks = SimConst.Ticks(cc.ThinIceFreezeSeconds);
            float applied = 0f;
            // 이 적중으로 죽어 가는 적에게는 걸지 않는다(공통 규칙 2). 꼬리표별 변환(엘리트 절반, 중량·보스 감속)은 Enemies가 한다.
            if (e.Alive) applied = w.Mods.Enemies.ApplyCc(w, e, CcKind.Freeze, freezeTicks, CcSource.ThinIce, Vector2.zero);
            int immuneUntil = w.Tick + SimConst.Ticks(cc.ThinIceImmuneSeconds);
            if (immuneUntil > e.FreezeImmuneUntilTick) e.FreezeImmuneUntilTick = immuneUntil;
            w.Emit(SimEventType.ThinIceFroze, p.Slot, e.Id, a: Mathf.RoundToInt(applied), pos: e.Pos);
        }

        // ------------------------------------------------------------ 그림자 계약 (passive.shadow_contract)

        public override float CharacterMul(SimWorld w, PlayerState p, int crystalsHeld)
        {
            if (p == null || p.Def == null || p.Def.Passive != Ids.Passive.ShadowContract) return 1f;
            var cc = w.Content.Const;
            if (cc == null) return 1f;
            int k = crystalsHeld < 0 ? 0 : crystalsHeld;   // 빚은 0개로 센다
            return 1f + cc.ShadowContractPerCrystal * k;
        }

        // ------------------------------------------------------------ 불안정 핵 (mutation.unstable_core)

        public override bool RollUnstableCore(SimWorld w, PlayerState p)
        {
            if (p == null || !p.Derived.UnstableCore) return false;
            if (!CoreExplodes(w, p)) return false;
            var s = Scratch(p);
            // 앞선 틱에 이미 돌려준 결과가 OnPlayerFired 없이 남아 있으면(발사가 끝내 안 나감) 그 결과는 다시 쓰지 않는다.
            if (s.HasUnstableRoll && s.UnstableConsumedTick != TraitScratch.NoTick && s.UnstableConsumedTick != w.Tick)
                s.HasUnstableRoll = false;
            // 굴려 둔 것이 없을 때만(야영에서 코어를 폭발 블록으로 바꾼 직후의 첫 발사) 여기서 굴린다.
            if (!s.HasUnstableRoll) RollNextShot(w, p, s, 0);
            s.UnstableConsumedTick = w.Tick;
            return s.UnstableExplodes;
        }

        public override void OnPlayerFired(SimWorld w, PlayerState p, int shotId, int intervalTicks)
        {
            if (p == null || !p.Derived.UnstableCore) return;
            var s = Scratch(p);
            ClearUnstableRoll(s);
            if (CoreExplodes(w, p)) RollNextShot(w, p, s, intervalTicks);
        }

        public override float RollSpreadDegrees(SimWorld w, PlayerState p)
        {
            if (p == null || !p.Derived.TwistedBarrel) return 0f;
            var mc = w.Content.Mutations;
            if (mc == null) return 0f;
            int spread = mc.TwistedBarrelSpreadDegrees;
            // [−25°, +25°] 정수도 균등(02 요약서 9.1절).
            return w.Rng(RngStream.Combat).Range(-spread, spread + 1);
        }

        public override float ExplosionRadiusMul(PlayerState p)
        {
            if (p == null || !p.Derived.UnstableCore) return 1f;
            var s = p.TraitScratch as TraitScratch;
            return s != null && s.ExplosionRadiusMul > 0f ? s.ExplosionRadiusMul : 1f;
        }

        // ------------------------------------------------------------ 탐식 (mutation.gluttony)

        public override void OnEnemyKilled(SimWorld w, EnemyState e, in HitInfo killingHit)
        {
            var p = PlayerOf(w, killingHit.AttackerSlot);
            if (p == null || !p.Derived.Gluttony) return;
            // 잠금 중 처치는 깎지 않는다(창에도 넣지 않는다).
            if (p.Locked) return;
            var mc = w.Content.Mutations;
            if (mc == null) return;

            var s = Scratch(p);
            int windowTicks = SimConst.Ticks(mc.GluttonyWindowSeconds);
            int capHu = SimConst.HeatToHu(mc.GluttonyHeatPerSecondCap);
            int room = capHu - s.GluttonySumHu(w.Tick, windowTicks);
            int hu = Mathf.Min(SimConst.HeatToHu(mc.GluttonyHeatPerKill), room);
            hu = Mathf.Min(hu, p.HeatHu);   // 실제로 깎이는 만큼만 센다
            if (hu <= 0) return;
            s.GluttonyWindow.Add(new GluttonyEntry { Tick = w.Tick, Hu = hu });
            w.Mods.Player.ReduceHeat(w, p, hu);
        }

        // ------------------------------------------------------------ 유리 지팡이 (mutation.glass_wand)

        public override void OnPlayerHit(SimWorld w, PlayerState p)
        {
            if (p == null || !p.Derived.GlassWand) return;
            var mc = w.Content.Mutations;
            if (mc == null) return;
            var s = Scratch(p);
            if (p.Wand.GlassDroppedThisWave && s.GlassDropsThisWave < 1) s.GlassDropsThisWave = 1;
            if (s.GlassDropsThisWave >= mc.GlassWandDropsPerWave) return;
            // 실효 단계가 바닥(+3)보다 클 때만 내려간다. 그 아래로는 내려가지 않는다.
            if (p.Wand.EffectiveStage <= mc.GlassWandMinStage) return;

            p.Wand.TempStageDrop++;
            s.GlassDropsThisWave++;
            p.Wand.GlassDroppedThisWave = true;
            RecomputeDerived(w, p);
            w.Emit(SimEventType.WandTempStageDropped, p.Slot, a: p.Wand.EffectiveStage);
        }

        public override void OnWaveStart(SimWorld w)
        {
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p == null) continue;
                p.Wand.GlassDroppedThisWave = false;
                var s = p.TraitScratch as TraitScratch;
                if (s != null) s.GlassDropsThisWave = 0;
            }
        }

        // ------------------------------------------------------------ 11단계

        public override void Stage11_TickEnd(SimWorld w)
        {
            var mc = w.Content != null ? w.Content.Mutations : null;
            if (mc == null) return;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p == null || !p.Active) continue;
                var s = p.TraitScratch as TraitScratch;
                if (s == null) continue;

                // 탐식 창: 지난 건을 치운다.
                if (s.GluttonyWindow.Count > 0) s.PruneGluttony(w.Tick, SimConst.Ticks(mc.GluttonyWindowSeconds));

                // 불안정 핵 예고: 자폭으로 정해진 다음 발사의 0.3초 전. 발사가 늦어지면 0.3초마다 다시 낸다
                // (실제 자폭 앞 0.3초 안에 예고가 하나는 있게). 공격할 수 없는 동안(야영 등)에는 내지 않는다.
                if (s.NextWarnTick != TraitScratch.NoTick && w.Tick >= s.NextWarnTick)
                {
                    if (!s.HasUnstableRoll || !s.UnstableExplodes || !p.Derived.UnstableCore)
                        s.NextWarnTick = TraitScratch.NoTick;
                    else if (w.AttackAllowed(p) && CoreExplodes(w, p))
                    {
                        w.Emit(SimEventType.UnstableCoreWarn, p.Slot);
                        s.NextWarnTick = w.Tick + WarnTicks(mc);
                    }
                }
            }
        }

        // ------------------------------------------------------------ 안쪽

        static TraitScratch Scratch(PlayerState p)
        {
            var s = p.TraitScratch as TraitScratch;
            if (s == null)
            {
                s = new TraitScratch();
                p.TraitScratch = s;
            }
            return s;
        }

        static PlayerState PlayerOf(SimWorld w, int slot)
        {
            if (slot < 0 || slot >= w.Players.Length) return null;
            var p = w.Players[slot];
            return p != null && p.Active ? p : null;
        }

        static bool HasMutation(IReadOnlyList<string> mutations, string id)
        {
            if (mutations == null) return false;
            for (int i = 0; i < mutations.Count; i++) if (mutations[i] == id) return true;
            return false;
        }

        static int WarnTicks(MutationsConfig mc)
        {
            int t = SimConst.Ticks(mc.UnstableCoreWarnSeconds);
            return t < 1 ? 1 : t;
        }

        /// <summary>지금 조립의 코어가 폭발 투사체인가(00 문서 D-30). 조립이 정본이고, 조립이 비어 있으면 컴파일된 주문을 본다.</summary>
        static bool CoreExplodes(SimWorld w, PlayerState p)
        {
            BlockDef core = null;
            if (p.Loadout != null && !p.Loadout.Core.IsEmpty && w.Content != null) core = w.Content.Block(p.Loadout.Core.BlockId);
            else if (p.Spell != null) core = p.Spell.Core;
            return core != null && core.IsProjectile && core.HasFlag(Ids.BlockFlag.Explodes);
        }

        static void ClearUnstableRoll(TraitScratch s)
        {
            s.HasUnstableRoll = false;
            s.UnstableExplodes = false;
            s.UnstableConsumedTick = TraitScratch.NoTick;
            s.NextWarnTick = TraitScratch.NoTick;
        }

        /// <summary>다음 J 발사분을 굴린다(난수 1회). 자폭이면 예고를 낸다: 다음 발사까지의 간격이 예고 시간 이하면 지금,
        /// 길면 "지금 + 간격 − 예고 시간" 틱의 11단계에.</summary>
        static void RollNextShot(SimWorld w, PlayerState p, TraitScratch s, int intervalTicks)
        {
            var mc = w.Content.Mutations;
            s.HasUnstableRoll = true;
            s.UnstableConsumedTick = TraitScratch.NoTick;
            s.NextWarnTick = TraitScratch.NoTick;
            s.UnstableExplodes = mc != null && w.Rng(RngStream.Combat).RollPercent(mc.UnstableCoreChancePercent);
            if (!s.UnstableExplodes) return;

            int warn = WarnTicks(mc);
            if (intervalTicks <= warn)
            {
                w.Emit(SimEventType.UnstableCoreWarn, p.Slot);
                s.NextWarnTick = w.Tick + warn;
            }
            else s.NextWarnTick = w.Tick + intervalTicks - warn;
        }

        /// <summary>반경 안(경계 포함)의 대상 후보를 모은다: Targetable(죽어 가는 적·땅속 제외)이고 등장 예고 중이 아닌 적
        /// (05 요약서 4절: Spawning은 피해를 주고받지 않는다).</summary>
        static void CollectTargets(SimWorld w, Vector2 center, float radius, List<TraitTarget> into)
        {
            into.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (!e.Targetable || e.Ai == EnemyAiState.Spawning) continue;
                float d2 = (e.Pos - center).sqrMagnitude;
                if (d2 <= r2) into.Add(new TraitTarget { SqrDistance = d2, Enemy = e });
            }
        }
    }
}
