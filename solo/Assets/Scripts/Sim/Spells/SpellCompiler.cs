using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Spells
{
    /// <summary>
    /// 슬롯 배열 → 실행 트리(순수 함수). 03 요약서 4절(S02 6.4 의사코드 그대로).
    /// 문맥(밑줄, 감쇠, 연결 칸 수)은 인자로만 받는다 — 부르는 쪽이 Traits.CompileContext로 만든다.
    /// </summary>
    public static class SpellCompiler
    {
        struct ModEntry
        {
            public BlockDef Block;
            public int Slot;
            public int Scope;
        }

        public static CompiledSpell Compile(IContentDb db, SpellTuning tuning, string core, IReadOnlyList<string> links, in SpellCompileContext ctx)
        {
            var spell = new CompiledSpell { TreeShotCap = tuning.TreeShotCap };
            int linkCount = links != null ? links.Count : 0;
            spell.SlotBlockIds = new string[linkCount + 1];
            spell.SlotBlockIds[0] = string.IsNullOrEmpty(core) ? null : core;
            for (int i = 0; i < linkCount; i++)
                spell.SlotBlockIds[i + 1] = string.IsNullOrEmpty(links[i]) ? null : links[i];
            // 판 한정 블록 성장(★): 칸마다 별 수. 상한으로 자르고, 빈 칸은 0.
            spell.SlotStars = new int[linkCount + 1];
            if (ctx.SlotStars != null)
                for (int i = 0; i < spell.SlotStars.Length && i < ctx.SlotStars.Length; i++)
                    spell.SlotStars[i] = spell.SlotBlockIds[i] != null ? tuning.ClampStars(ctx.SlotStars[i]) : 0;
            float decay = ctx.DepthDecay > 0f ? ctx.DepthDecay : tuning.DepthDecayDefault;
            spell.DepthDecay = decay;

            // 오류는 처음 것 하나로 끝낸다(S02 6.4: return Invalid).
            if (linkCount != ctx.LinkSlotCount) return Invalid(spell, SpellDiagCode.E_SLOT_COUNT_MISMATCH);
            if (string.IsNullOrEmpty(core)) return Invalid(spell, SpellDiagCode.E_CORE_EMPTY);
            BlockDef coreDef = db != null ? db.Block(core) : null;
            if (coreDef == null || !coreDef.IsProjectile) return Invalid(spell, SpellDiagCode.E_CORE_NOT_PROJECTILE);

            spell.Core = coreDef;
            spell.FireIntervalTicks = coreDef.IntervalTicks;

            var scopes = spell.Scopes;
            var diags = spell.Diagnostics;
            var mods = new List<ModEntry>();
            var cur = new SpellScope { Depth = 0, TriggerSlot = -1 };
            scopes.Add(cur);
            AddProjectile(cur, coreDef, 0, spell.SlotStars[0], tuning);
            cur.HeatSumHu += StarHeatHu(coreDef.HeatHu, spell.SlotStars[0], tuning);

            for (int slot = 1; slot <= linkCount; slot++)
            {
                string id = links[slot - 1];
                if (string.IsNullOrEmpty(id)) continue;
                BlockDef b = db.Block(id);
                if (b == null) continue; // 모르는 id는 빈 칸으로 본다(저장 파일 방어)

                int stars = spell.SlotStars[slot];
                int h = b.KindEnum != BlockKind.Control ? StarHeatHu(b.HeatHu, stars, tuning) : b.HeatHu;
                // 밑줄(HT-5/6): 맨 끝 연결 칸의 투사체·변형 열을 min(상한, ceil(h/2)) 깎는다. 분열 배수는 깎은 뒤 곱한다.
                if (ctx.Underline && slot == linkCount && b.KindEnum != BlockKind.Control)
                    h -= UnderlineCutHu(h, tuning.UnderlineMaxCutHu);

                switch (b.KindEnum)
                {
                    case BlockKind.Projectile:
                        AddProjectile(cur, b, slot, stars, tuning);
                        cur.HeatSumHu += h;
                        break;
                    case BlockKind.Modifier:
                        Accumulate(ref cur.Mods, b);
                        cur.Mods.DamageBonusSum += tuning.StarModifierDamageBonus * stars;
                        mods.Add(new ModEntry { Block = b, Slot = slot, Scope = cur.Depth });
                        cur.HeatSumHu += h;
                        break;
                    default:
                        if (b.IsTrigger)
                        {
                            // SF-1·SF-7: 넷째 트리거는 무시하고 뒤 블록은 지금 스코프에 남는다.
                            if (cur.Depth >= tuning.TriggerMaxDepth)
                            {
                                diags.Add(new SpellDiagnostic { Code = SpellDiagCode.W_TRIGGER_DEPTH, Slot = slot });
                                break;
                            }
                            cur = new SpellScope { Depth = cur.Depth + 1, TriggerSlot = slot };
                            // 별 단 트리거: 그 하위 스코프의 탄이 피해 보너스를 받는다(부모 변형이 이어지지 않는 CP-3과 별개인 트리거 자신의 몫).
                            cur.Mods.DamageBonusSum += tuning.StarTriggerDamageBonus * stars;
                            scopes.Add(cur);
                        }
                        else if (b.IsSplit)
                        {
                            // 한 스코프에 첫 분열만 유효.
                            if (cur.SplitSlot < 0)
                            {
                                cur.SplitCount = b.SplitCount;
                                cur.SplitSlot = slot;
                                cur.HeatMul = StarSplitHeatMul(b.HeatMultiplier, stars, tuning);
                                cur.FanHalfAngleDegrees = b.Params != null ? b.Params.FanHalfAngleDegrees : 0f;
                            }
                            else
                            {
                                diags.Add(new SpellDiagnostic { Code = SpellDiagCode.W_SPLIT_DUPLICATE, Slot = slot });
                            }
                        }
                        break;
                }
            }

            int heatTotal = 0, instTotal = 0;
            for (int d = 0; d < scopes.Count; d++)
            {
                var s = scopes[d];
                s.HeatHu = (int)Math.Round((double)s.HeatSumHu * s.HeatMul, MidpointRounding.AwayFromZero);
                s.InstancesPerParent = s.Projectiles.Count * s.SplitCount;
                s.InstancesTotal = d == 0 ? s.InstancesPerParent : scopes[d - 1].InstancesTotal * s.InstancesPerParent;
                s.DepthMultiplier = Mathf.Pow(decay, d);
                heatTotal += s.HeatHu;
                instTotal += s.InstancesTotal;
                if (d > 0 && s.Projectiles.Count == 0)
                    diags.Add(new SpellDiagnostic { Code = SpellDiagCode.W_TRIGGER_NO_PROJECTILE, Slot = s.TriggerSlot });
            }
            CheckNoEffect(scopes, mods, diags);

            spell.HeatPerShotHu = heatTotal;
            spell.InstancesTotal = instTotal;
            spell.InstancesTotalMirrored = instTotal * 2;
            spell.MaxDepthNormal = Prune(scopes, 1, tuning.TreeShotCap);
            spell.MaxDepthMirrored = Prune(scopes, 2, tuning.TreeShotCap);
            if (spell.MaxDepthNormal < scopes.Count - 1)
                diags.Add(new SpellDiagnostic { Code = SpellDiagCode.W_PROJECTILE_CAP, Slot = spell.MaxDepthNormal + 1 });

            // 표준 T의 바탕(D-07): 루트 스코프만, 분열 반영, 거울상 제외.
            var root = scopes[0];
            float baseSum = 0f;
            for (int i = 0; i < root.Projectiles.Count; i++) baseSum += root.Projectiles[i].Block.BaseDamage * root.Projectiles[i].DamageScale;
            spell.StandardTBase = baseSum * root.SplitCount * (1f + root.Mods.DamageBonusSum);

            SortDiagnostics(diags);
            spell.IsValid = true;
            return spell;
        }

        /// <summary>밑줄이 깎는 양(HU): 그 블록 열의 절반(열 단위 올림), 상한 maxCutHu. +3 → 2, 18 → 5(상한).</summary>
        public static int UnderlineCutHu(int heatHu, int maxCutHu)
        {
            if (heatHu <= 0) return 0;
            int halfUnit = SimConst.HuPerHeat * 2;
            int cut = (heatHu + halfUnit - 1) / halfUnit * SimConst.HuPerHeat;
            return cut < maxCutHu ? cut : maxCutHu;
        }

        /// <summary>가장 깊은 스코프부터 통째로 빼며 Σ ≤ cap이 되는 가장 깊은 깊이를 찾는다(SF-2).</summary>
        public static int Prune(List<SpellScope> scopes, int rootFactor, int cap)
        {
            for (int d = scopes.Count - 1; d >= 0; d--)
            {
                long total = 0;
                for (int k = 0; k <= d; k++) total += (long)scopes[k].InstancesTotal * rootFactor;
                if (total <= cap) return d;
            }
            return 0;
        }

        /// <summary>그 변형이 그 투사체에 효과가 있나(03 요약서 4.6절 W_NO_EFFECT 표).</summary>
        public static bool ModifierAffects(BlockDef modifier, BlockDef projectile)
        {
            switch (modifier.Effect)
            {
                case Ids.BlockEffect.Pierce:
                    return !(projectile.HasFlag(Ids.BlockFlag.PassThrough) || projectile.Instant);
                case Ids.BlockEffect.Bounce:
                case Ids.BlockEffect.Homing:
                    return !projectile.Instant;
                default:
                    return true;
            }
        }

        static CompiledSpell Invalid(CompiledSpell spell, SpellDiagCode code)
        {
            spell.IsValid = false;
            spell.Diagnostics.Add(new SpellDiagnostic { Code = code, Slot = 0 });
            return spell;
        }

        static void AddProjectile(SpellScope s, BlockDef b, int slot, int stars, SpellTuning tuning)
        {
            if (s.Projectiles.Count == 0) s.OwnerSlot = slot;
            s.Projectiles.Add(new ScopeProjectile
            {
                Block = b, Slot = slot, Stars = stars,
                StarDamageMul = 1f + tuning.StarProjectileDamage * stars
            });
        }

        /// <summary>별이 깎은 블록 열(HU): h × (1 − 깎기 × ★), 0 아래로 가지 않는다. 밑줄은 이 값에서 깎는다.</summary>
        public static int StarHeatHu(int heatHu, int stars, SpellTuning tuning)
        {
            if (stars <= 0 || heatHu <= 0 || tuning.StarHeatCut <= 0f) return heatHu;
            double f = 1.0 - (double)tuning.StarHeatCut * stars;
            if (f < 0.0) f = 0.0;
            return (int)Math.Round(heatHu * f, MidpointRounding.AwayFromZero);
        }

        /// <summary>별 단 분열의 열 배수: 1 + (배수 − 1) × (1 − 깎기 × ★). 1 아래로 가지 않는다.</summary>
        public static float StarSplitHeatMul(float heatMul, int stars, SpellTuning tuning)
        {
            if (stars <= 0 || tuning.StarSplitHeatMulCut <= 0f || heatMul <= 1f) return heatMul;
            float f = 1f - tuning.StarSplitHeatMulCut * stars;
            if (f < 0f) f = 0f;
            return 1f + (heatMul - 1f) * f;
        }

        static void Accumulate(ref ScopeModifiers m, BlockDef b)
        {
            m.DamageBonusSum += b.DamageBonus;
            var p = b.Params;
            switch (b.Effect)
            {
                case Ids.BlockEffect.Bounce: m.BounceCount += p != null ? p.BounceCount : 0; break;
                case Ids.BlockEffect.Pierce: m.PierceCount += p != null ? p.PierceCount : 0; break;
                case Ids.BlockEffect.Size: m.SizeDoublings++; break;
                case Ids.BlockEffect.Frost: m.Frost = true; break;
                case Ids.BlockEffect.Homing: m.Homing = true; break;
                case Ids.BlockEffect.TripleHeat: m.TripleHeat = true; break;
                case Ids.BlockEffect.Corrosion: m.Corrosion = true; break;
            }
        }

        static void CheckNoEffect(List<SpellScope> scopes, List<ModEntry> mods, List<SpellDiagnostic> diags)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                var s = scopes[m.Scope];
                // 투사체 없는 스코프의 변형은 W_TRIGGER_NO_PROJECTILE 하나로만 알린다.
                if (s.Projectiles.Count == 0) continue;
                bool any = false;
                for (int k = 0; k < s.Projectiles.Count && !any; k++)
                    any = ModifierAffects(m.Block, s.Projectiles[k].Block);
                if (!any) diags.Add(new SpellDiagnostic { Code = SpellDiagCode.W_NO_EFFECT, Slot = m.Slot });
            }
        }

        /// <summary>슬롯 오름차순, 같으면 코드 순(안정 삽입 정렬).</summary>
        static void SortDiagnostics(List<SpellDiagnostic> diags)
        {
            for (int i = 1; i < diags.Count; i++)
            {
                var x = diags[i];
                int j = i - 1;
                while (j >= 0 && (diags[j].Slot > x.Slot || (diags[j].Slot == x.Slot && diags[j].Code > x.Code)))
                {
                    diags[j + 1] = diags[j];
                    j--;
                }
                diags[j + 1] = x;
            }
        }
    }
}
