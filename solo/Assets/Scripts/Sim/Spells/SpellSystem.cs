using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Spells
{
    /// <summary>
    /// 주문 모듈 구현: 컴파일러, 실행, 플레이어 탄, 피해 공식. 03 요약서, 00 문서 5.4절 (가)(나).
    /// 세계 상태(w.Projectiles, 적 체력)는 세계가 갖고, 이 객체는 임시 버퍼와 수치 묶음(SpellTuning)만 든다.
    /// </summary>
    public sealed partial class SpellSystem : SpellModule
    {
        /// <summary>실수 비교 여유(거리·사거리 경계).</summary>
        const float Eps = 1e-4f;
        const float HalfTurnDegrees = 180f;

        /// <summary>발사 1회(Execute)의 스냅숏. 자손 탄이 물려받는다(03 요약서 6절 8번 "탄 문맥").
        /// ProjectileEntity에 저장해 모듈 객체의 수명과 무관하게 유지한다.</summary>
        sealed class ShotContext
        {
            /// <summary>강화 × 변이 × 비기 배율 × 캐릭터 배율(스코프마다 달라지는 항 제외).</summary>
            public float BaseMul;
            /// <summary>임계 배율 덧셈(유리 지팡이).</summary>
            public int CritBonus;
        }

        SpellTuning _tuning;
        IContentDb _tuningDb;

        SpellTuning Tune(IContentDb db)
        {
            if (_tuning == null || !ReferenceEquals(db, _tuningDb))
            {
                _tuning = SpellTuning.From(db);
                _tuningDb = db;
            }
            return _tuning;
        }

        // ------------------------------------------------------------ 컴파일

        public override CompiledSpell Compile(IContentDb db, string core, IReadOnlyList<string> links, in SpellCompileContext ctx)
        {
            return SpellCompiler.Compile(db, Tune(db), core, links, ctx);
        }

        // ------------------------------------------------------------ 실행 (03 요약서 6절)

        public override ExecuteResult Execute(SimWorld w, in ExecuteRequest req)
        {
            var res = default(ExecuteResult);
            var tree = req.Tree;
            if (tree == null || !tree.IsValid || tree.Scopes.Count == 0) return res;
            if (req.OwnerSlot < 0 || req.OwnerSlot >= w.Players.Length) return res;

            var p = w.Players[req.OwnerSlot];
            var t = Tune(w.Content);
            // 거울상은 J 발사와 마력 방출에만(64 상한에 포함).
            bool mirror = p.Derived.MirrorImage && (req.Source == ShotSource.Fire || req.Source == ShotSource.ManaDischarge);
            int maxDepth = mirror ? tree.MaxDepthMirrored : tree.MaxDepthNormal;
            var root = tree.Scopes[0];
            int split = Mathf.Max(1, root.SplitCount);
            int sets = mirror ? 2 : 1;
            int total = root.Projectiles.Count * split * sets;

            res.Mirrored = mirror;
            res.Requested = total;
            if (!req.HeatExempt)
                res.HeatToAddHu = mirror
                    ? (int)Math.Round((double)tree.HeatPerShotHu * t.MirrorHeatMul, MidpointRounding.AwayFromZero)
                    : tree.HeatPerShotHu;

            // 예산(비기 160)은 뒤쪽부터 안 만든다(보전 없음). 화면 상한은 앞쪽 M발만 만들고 N/M으로 보전한다.
            int n = total;
            if (req.ProjectileBudget > 0 && n > req.ProjectileBudget) n = req.ProjectileBudget;
            int m = CapBatch(w, t, req.OwnerSlot, n, out float comp);

            // 임계는 J 발사에만 붙는다(비기·사본은 임계 없음, DM-7).
            bool isCrit = req.IsCrit && req.Source == ShotSource.Fire;
            var ctx = new ShotContext
            {
                BaseMul = OneIfUnset(p.Derived.EnhanceMul) * OneIfUnset(p.Derived.MutationDamageMul)
                          * OneIfUnset(req.DamageMul) * OneIfUnset(req.CharacterMul),
                CritBonus = p.Derived.CritBonus
            };
            bool spread = p.Derived.TwistedBarrel && !req.NoSpread;

            int k = 0;
            for (int set = 0; set < sets; set++)
            {
                // 앞 묶음 전부 → 뒤 묶음(+180도) 전부. 투사체는 슬롯 순, 부채 각은 오름차순.
                for (int i = 0; i < root.Projectiles.Count; i++)
                {
                    for (int f = 0; f < split; f++)
                    {
                        float deg = req.DirDegrees + FanOffset(root, f) + (set == 0 ? 0f : HalfTurnDegrees);
                        // 뒤틀린 총열: 루트 탄마다 한 번씩 굴린다(잘려서 안 나가는 탄도 순서를 지키려고 굴린다).
                        if (spread) deg += w.Mods.Traits.RollSpreadDegrees(w, p);
                        if (k < m)
                            Spawn(w, t, ctx, tree, root, root.Projectiles[i], req.OwnerSlot, req.ShotId, req.Source,
                                req.Pos, deg, isCrit, maxDepth, comp, -1, req.HitEffect);
                        k++;
                    }
                }
            }
            res.Spawned = m;
            return res;
        }

        /// <summary>부채 각(도). 1 → 0 / 2 → −h, +h / 3 → −h, 0, +h (오름차순).</summary>
        static float FanOffset(SpellScope scope, int index)
        {
            int n = scope.SplitCount;
            if (n <= 1) return 0f;
            float h = scope.FanHalfAngleDegrees;
            return -h + index * (2f * h / (n - 1));
        }

        static float OneIfUnset(float mul) => mul > 0f ? mul : 1f;

        static int LiveCount(SimWorld w)
        {
            int n = 0;
            var list = w.Projectiles;
            for (int i = 0; i < list.Count; i++) if (!list[i].Dead) n++;
            return n;
        }

        /// <summary>화면 상한(03 요약서 8절): N발 요청에 여유 M발이면 M을 돌려주고 보전 배율 N/M을 준다.</summary>
        int CapBatch(SimWorld w, SpellTuning t, int ownerSlot, int requested, out float comp)
        {
            comp = 1f;
            if (requested <= 0) return 0;
            int free = t.PlayerShotCap - LiveCount(w);
            int m = Mathf.Clamp(free, 0, requested);
            if (m < requested)
            {
                w.Emit(SimEventType.ShotCapHit, ownerSlot, a: requested, b: m);
                if (m > 0) comp = requested / (float)m;
            }
            return m;
        }

        float ScopeFactor(SpellTuning t, SpellScope scope, bool isCrit, int critBonus)
        {
            float crit = 1f;
            // 과열 3배는 스코프 단위(U4): 그 스코프 탄의 임계 배율 2 → 3. 유리 지팡이는 +1.
            if (isCrit) crit = (scope.Mods.TripleHeat ? t.TripleHeatCritMultiplier : t.CritMultiplier) + critBonus;
            return (1f + scope.Mods.DamageBonusSum) * crit * scope.DepthMultiplier;
        }

        ProjectileEntity Spawn(SimWorld w, SpellTuning t, ShotContext ctx, CompiledSpell tree, SpellScope scope, ScopeProjectile sp,
            int ownerSlot, int shotId, ShotSource source, Vector2 pos, float degrees, bool isCrit, int maxDepth, float comp,
            int ignoreEnemyId, ShotHitEffect hitEffect)
        {
            var b = sp.Block;
            float sizeMul = 1f;
            for (int i = 0; i < scope.Mods.SizeDoublings; i++) sizeMul *= t.SizeRadiusMul;
            if (sizeMul > t.SizeRadiusCap) sizeMul = t.SizeRadiusCap;

            var pr = new ProjectileEntity
            {
                Id = w.NewEntityId(),
                OwnerSlot = ownerSlot,
                ShotId = shotId,
                Source = source,
                Block = b,
                Tree = tree,
                ScopeDepth = scope.Depth,
                MaxDepth = maxDepth,
                State = ProjectileState.Pending,
                SpawnTick = w.Tick,
                Pos = pos,
                PrevPos = pos,
                Dir = Dir8Util.DegreesToSimVector(degrees),
                Speed = b.Speed,
                Radius = b.HitRadius * sizeMul,
                BouncesLeft = scope.Mods.BounceCount,
                PierceLeft = scope.Mods.PierceCount,
                IgnoreEnemyId = ignoreEnemyId,
                Mods = scope.Mods,
                IsCrit = isCrit,
                BaseMul = ctx.BaseMul,
                CritBonus = ctx.CritBonus,
                DamageMul = ctx.BaseMul * ScopeFactor(t, scope, isCrit, ctx.CritBonus) * comp,
                HitEffect = hitEffect
            };
            w.Projectiles.Add(pr);
            // A = 슬롯, B = 스코프 깊이(HUD 지팡이 회로가 켤 칸).
            w.Emit(SimEventType.ProjectileSpawned, ownerSlot, pr.Id, a: sp.Slot, b: scope.Depth, pos: pos, id: b.Id);
            return pr;
        }

        /// <summary>탄의 발사 문맥. 이 모듈이 만들지 않은 탄이면 탄의 배율에서 스코프 항을 걷어 내 되짚는다.</summary>
        ShotContext ContextOf(SimWorld w, SpellTuning t, ProjectileEntity pr)
        {
            if (pr.BaseMul > 0f) return new ShotContext { BaseMul = pr.BaseMul, CritBonus = pr.CritBonus };
            int critBonus = 0;
            if (pr.OwnerSlot >= 0 && pr.OwnerSlot < w.Players.Length) critBonus = w.Players[pr.OwnerSlot].Derived.CritBonus;
            float own = 1f;
            if (pr.Tree != null && pr.ScopeDepth >= 0 && pr.ScopeDepth < pr.Tree.Scopes.Count)
                own = ScopeFactor(t, pr.Tree.Scopes[pr.ScopeDepth], pr.IsCrit, critBonus);
            return new ShotContext { BaseMul = own > 0f ? pr.DamageMul / own : pr.DamageMul, CritBonus = critBonus };
        }

        // ------------------------------------------------------------ 표준 T, 조립 수치, 지우기

        public override float StandardT(SimWorld w, PlayerState p)
        {
            if (p == null || p.Spell == null || !p.Spell.IsValid) return 0f;
            return p.Spell.StandardTBase * OneIfUnset(p.Derived.EnhanceMul) * OneIfUnset(p.Derived.MutationDamageMul);
        }

        public override void ClearProjectiles(SimWorld w, int ownerSlot)
        {
            var list = w.Projectiles;
            for (int i = 0; i < list.Count; i++)
            {
                var pr = list[i];
                if (pr.Dead) continue;
                if (ownerSlot >= 0 && pr.OwnerSlot != ownerSlot) continue;
                Kill(w, pr, ExpireRange);
            }
        }

        /// <summary>
        /// 조립 수치(03 요약서 13.3절). 발사당 열과 임계 사격 가능 횟수는 실제 J 발사의 열(거울상이면 ×1.5)로 센다.
        /// 허수아비 DPS는 명세에 식이 없어(M8) "J를 누르고만 있을 때의 1주기 DPS"로 둔다: 열 0에서 잠길 때까지 쏘고
        /// (발사 뒤 열 ≥ 임계 시작이면 임계, 잠그는 발사도 임계) 주기 = 발사 수 × 간격 + 잠금 틱. 실행되는 스코프의 탄이
        /// 전부 한 번씩 맞는다고 본다(속성·캐릭터 배율 제외). 검산: 마력탄 기준값 310 ÷ 6.9초 = 44.93(02 요약서 T-H02).
        /// </summary>
        public override SpellStats ComputeStats(IContentDb db, CompiledSpell spell, CharacterDef ch, in PlayerDerived derived)
        {
            var stats = default(SpellStats);
            if (spell == null || !spell.IsValid || spell.Scopes.Count == 0) return stats;
            var t = Tune(db);

            bool mirror = derived.MirrorImage;
            int heatHu = mirror
                ? (int)Math.Round((double)spell.HeatPerShotHu * t.MirrorHeatMul, MidpointRounding.AwayFromZero)
                : spell.HeatPerShotHu;
            int maxDepth = mirror ? spell.MaxDepthMirrored : spell.MaxDepthNormal;
            int count = 0;
            for (int d = 0; d <= maxDepth && d < spell.Scopes.Count; d++) count += spell.Scopes[d].InstancesTotal;

            stats.HeatPerShot = heatHu / (float)SimConst.HuPerHeat;
            stats.ProjectilesPerShot = count * (mirror ? 2 : 1);
            stats.StandardT = spell.StandardTBase * OneIfUnset(derived.EnhanceMul) * OneIfUnset(derived.MutationDamageMul);

            int maxHu = t.HeatMaxHu;
            if (heatHu <= 0) stats.CritShots = -1;
            else if (heatHu > maxHu) stats.CritShots = 0;
            else stats.CritShots = Mathf.Max(0, (maxHu - derived.CritStartHu) / heatHu + 1);

            float normal = ShotDamage(t, spell, derived, maxDepth, false) * (mirror ? 2 : 1);
            float crit = ShotDamage(t, spell, derived, maxDepth, true) * (mirror ? 2 : 1);
            int interval = Mathf.Max(1, spell.FireIntervalTicks);
            if (heatHu <= 0)
            {
                // 열이 안 오르면 잠기지 않는다: 열 0이 임계 구간이 아니면 보통 발사만.
                float per = derived.CritStartHu <= 0 ? crit : normal;
                stats.DummyDps = per * SimConst.TicksPerSecond / interval;
            }
            else
            {
                float damage = 0f;
                int shots = 0, heat = 0;
                while (true)
                {
                    heat += heatHu;
                    shots++;
                    bool locks = heat > maxHu;
                    damage += (locks || heat >= derived.CritStartHu) ? crit : normal;
                    if (locks) break;
                }
                int cycle = shots * interval + Mathf.Max(0, derived.LockTicks);
                stats.DummyDps = damage * SimConst.TicksPerSecond / cycle;
            }
            return stats;
        }

        /// <summary>발사 1회에 실행되는 스코프의 탄이 전부 한 번씩 맞았을 때의 피해 합(거울상·속성·캐릭터 배율 제외).</summary>
        float ShotDamage(SpellTuning t, CompiledSpell spell, in PlayerDerived derived, int maxDepth, bool isCrit)
        {
            float sum = 0f;
            float baseMul = OneIfUnset(derived.EnhanceMul) * OneIfUnset(derived.MutationDamageMul);
            for (int d = 0; d <= maxDepth && d < spell.Scopes.Count; d++)
            {
                var s = spell.Scopes[d];
                if (s.Projectiles.Count == 0) continue;
                float blockSum = 0f;
                for (int i = 0; i < s.Projectiles.Count; i++) blockSum += s.Projectiles[i].Block.BaseDamage;
                // InstancesTotal = (부모 탄 수) × 투사체 수 × 분열 수 → 투사체 종류마다 InstancesTotal / 투사체 수 발.
                float copies = s.InstancesTotal / (float)s.Projectiles.Count;
                sum += blockSum * copies * baseMul * ScopeFactor(t, s, isCrit, derived.CritBonus);
            }
            return sum;
        }
    }
}
