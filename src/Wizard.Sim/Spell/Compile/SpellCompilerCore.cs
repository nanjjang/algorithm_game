using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Heat;

namespace Wizard.Sim.Spell.Compile
{
    // spec 02 6절 컴파일러(6.4 의사코드), 7절 발사 1회의 열, 8.1 트리 실행 1회 상한의 가지치기, 14.2 표준 T.
    // 순수 함수다: 같은 원천이면 같은 결과(같은 Hash, 같은 글자 출력. 15절). 세계 생성과 Step 0단계에서만 부른다
    // (CONTRACTS 3.2: 0단계의 컴파일은 Step 할당 0의 유일한 예외). 가변 정적 상태가 없다.
    //
    // 블록은 BlockDef 의 값으로만 가른다: 종류(Kind), 분열·적중 시 발동(IsSplit·IsTrigger. BlockId 로 가르지 않는다, T-53 6번),
    // 변형 효과(Effect), 표식(Flags), 열(HeatHu·HeatMulPermille), 부채꼴 반각(Params.FanHalfAngleCenti).
    internal static class SpellCompilerCore
    {
        public const int NoSlot = SpellScope.NoSlot;
        public const int PermilleOne = 1000;              // 천분율 1.0(단위 환산)
        public const int MaxScopes = 4;                   // CONTRACTS 3.4 "Scopes 길이 1~4"(SF-1 트리거 깊이 최대 3). 자료 꼴의 크기
        public const int MaxDepth = MaxScopes - 1;
        public const int MirrorFactor = 2;                // 거울상은 루트 묶음을 하나 더 만든다(앞 묶음 + 뒤 묶음, 6.4 InstancesTotalMirrored)

        public static CompiledSpell Compile(ContentDb db, CompileSource src, in CompileRules rules)
        {
            var m = new CompiledSpell();
            m.Hash = src.ComputeHash();

            // ── 6.4 앞머리의 오류(첫 오류에서 멈춘다)
            int linkCount = src.BaseLinks + src.ExtraSlots;
            if (src.LinkCount != linkCount)
            {
                return Invalid(m, DiagCode.SlotCountMismatch, 0);
            }
            if (src.Core == BlockId.None)
            {
                return Invalid(m, DiagCode.CoreEmpty, 0);
            }
            if (!TryDef(db, src.Core, out BlockDef? coreDef))
            {
                return Invalid(m, DiagCode.UnknownBlock, 0);
            }
            if (coreDef.Kind != BlockKind.Projectile)
            {
                return Invalid(m, DiagCode.CoreNotProjectile, 0);
            }
            var defs = new BlockDef?[linkCount + 1];
            defs[0] = coreDef;
            for (int slot = 1; slot <= linkCount; slot++)
            {
                BlockId b = src.LinkAt(slot);
                if (b == BlockId.None)
                {
                    continue;
                }
                if (!TryDef(db, b, out BlockDef? d))
                {
                    return Invalid(m, DiagCode.UnknownBlock, slot);
                }
                defs[slot] = d;
            }

            // ── 슬롯 순서 = 실행 순서. 스코프는 한 줄의 사슬이다(CP-2)
            var diags = new List<CompileDiagnostic>();
            var slots = new CompiledSlot[linkCount + 1];
            var heatBlocks = new ShotHeatBlock[linkCount + 1];
            int heatBlockCount = 0;
            var builders = new Builder[MaxScopes];
            int scopeCount = 1;
            builders[0] = new Builder(0, NoSlot);
            Builder cur = builders[0];
            // 갈피의 밑줄: 맨 끝 연결 칸(L번, 확장 포함. SL-3, HT-6). 연결 칸이 없으면 깎을 칸이 없다
            int underlineSlot = src.UnderlinePassive && linkCount > 0 ? linkCount : ShotHeat.NoUnderline;

            cur.AddProjectile(src.Core, 0);
            cur.HeatSumHu += coreDef.HeatHu;
            heatBlocks[heatBlockCount++] = new ShotHeatBlock(0, 0, coreDef.HeatHu, 0);
            slots[0] = new CompiledSlot(0, src.Core, SlotRole.Projectile, 0);

            for (int slot = 1; slot <= linkCount; slot++)
            {
                BlockDef? d = defs[slot];
                if (d == null)
                {
                    slots[slot] = new CompiledSlot(slot, BlockId.None, SlotRole.Empty, cur.Depth);
                    continue;
                }
                switch (d.Kind)
                {
                    case BlockKind.Projectile:
                        cur.AddProjectile(d.Id, slot);
                        cur.HeatSumHu += HeatAfterUnderline(d.HeatHu, slot, underlineSlot, rules);
                        heatBlocks[heatBlockCount++] = new ShotHeatBlock(cur.Depth, slot, d.HeatHu, 0);
                        slots[slot] = new CompiledSlot(slot, d.Id, SlotRole.Projectile, cur.Depth);
                        break;
                    case BlockKind.Modifier:
                        cur.AddModifier(d, slot);
                        cur.HeatSumHu += HeatAfterUnderline(d.HeatHu, slot, underlineSlot, rules);
                        heatBlocks[heatBlockCount++] = new ShotHeatBlock(cur.Depth, slot, d.HeatHu, 0);
                        slots[slot] = new CompiledSlot(slot, d.Id, SlotRole.Modifier, cur.Depth);
                        break;
                    case BlockKind.Control when d.IsSplit:
                        // 한 스코프에 1개만 유효하다. 두 번째부터는 경고하고 무시(6.1). 분열은 열을 더하지 않고 배수만 준다(HT-2)
                        if (cur.SplitSlot == NoSlot)
                        {
                            cur.SplitSlot = slot;
                            cur.SplitCount = d.SplitCount;
                            cur.SplitHeatMulPermille = d.HeatMulPermille;
                            cur.FanHalfAngleCenti = d.Params.FanHalfAngleCenti;
                            heatBlocks[heatBlockCount++] = new ShotHeatBlock(cur.Depth, slot, 0, d.HeatMulPermille);
                            slots[slot] = new CompiledSlot(slot, d.Id, SlotRole.Split, cur.Depth);
                        }
                        else
                        {
                            diags.Add(new CompileDiagnostic(DiagCode.SplitDuplicate, DiagSeverity.Warning, slot, cur.Depth));
                            slots[slot] = new CompiledSlot(slot, d.Id, SlotRole.SplitIgnored, cur.Depth);
                        }
                        break;
                    case BlockKind.Control when d.IsTrigger:
                        // 넷째 트리거는 무시하고 경고한다. 뒤 블록은 깊이 3 스코프에 남는다(SF-7)
                        if (cur.Depth == MaxDepth)
                        {
                            diags.Add(new CompileDiagnostic(DiagCode.TriggerDepth, DiagSeverity.Warning, slot, cur.Depth));
                            slots[slot] = new CompiledSlot(slot, d.Id, SlotRole.TriggerIgnored, cur.Depth);
                            break;
                        }
                        cur = new Builder(cur.Depth + 1, slot);
                        builders[scopeCount++] = cur;
                        slots[slot] = new CompiledSlot(slot, d.Id, SlotRole.Trigger, cur.Depth);
                        break;
                    default:
                        // 팩의 변환기가 제어 블록을 분열·적중 시 발동 둘로만 받는다(T-53 6번). 여기에 오면 팩이 깨졌다
                        throw new SimInvariantException("compile: control block is neither split nor trigger");
                }
            }

            // ── 스코프 마무리(6.4 둘째 고리)
            var scopes = new SpellScope[scopeCount];
            long parentTotal = 1L;
            int heatSum = 0;
            for (int d = 0; d < scopeCount; d++)
            {
                SpellScope s = builders[d]!.Finish();
                s.HeatHu = FixMath.SaturateToInt((long)s.HeatSumHu * s.SplitHeatMulPermille / PermilleOne);
                long perParent = (long)s.Projectiles.Length * s.SplitCount;
                s.InstancesPerParent = FixMath.SaturateToInt(perParent);
                parentTotal = Saturate(parentTotal * perParent);
                s.InstancesTotal = FixMath.SaturateToInt(parentTotal);
                s.DepthMultiplier = FixMath.PowTable(src.DepthDecay, d);
                if (d > 0 && s.Projectiles.Length == 0)
                {
                    diags.Add(new CompileDiagnostic(DiagCode.TriggerNoProjectile, DiagSeverity.Warning, s.TriggerSlot, d));
                }
                CheckNoEffect(s, defs, diags);
                heatSum = unchecked(heatSum + s.HeatHu);
                scopes[d] = s;
            }

            // ── 7.1 발사 1회의 열. 과열 모듈의 식(ShotHeat)이 정본이고 스코프별 값의 합과 같아야 한다
            int heatPerShot = ShotHeat.ShotHeatHu(new System.ReadOnlySpan<ShotHeatBlock>(heatBlocks, 0, heatBlockCount),
                                                  underlineSlot, rules.UnderlineMaxCutHu, ShotHeat.NoMirrorPermille);
            if (heatPerShot != heatSum)
            {
                throw new SimInvariantException("compile: scope heat does not add up to ShotHeat");
            }

            m.Scopes = scopes;
            m.Slots = slots;
            m.FireIntervalTicks = coreDef.IntervalTicks;
            m.HeatPerShotHu = heatPerShot;
            // HT-7: 거울상 변이가 있으면 J 발사 1회의 열은 총열(밑줄·분열 배수 뒤)에 배수를 곱한 값이다. 같은 식(ShotHeat)으로 센다
            m.FireHeatHu = src.HasMutation(MutationId.MirrorImage)
                ? ShotHeat.ShotHeatHu(new System.ReadOnlySpan<ShotHeatBlock>(heatBlocks, 0, heatBlockCount),
                                      underlineSlot, rules.UnderlineMaxCutHu, rules.MirrorHeatMulPermille)
                : heatPerShot;
            long total = 0L;
            for (int d = 0; d < scopeCount; d++)
            {
                total += scopes[d].InstancesTotal;
            }
            m.InstancesTotal = FixMath.SaturateToInt(total);
            m.InstancesTotalMirrored = FixMath.SaturateToInt(total * MirrorFactor);
            m.MaxDepthNormal = Prune(scopes, 1, rules.TreeProjectileCap);
            m.MaxDepthMirrored = Prune(scopes, MirrorFactor, rules.TreeProjectileCap);
            if (m.MaxDepthNormal < scopeCount - 1)
            {
                SpellScope omitted = scopes[m.MaxDepthNormal + 1];
                diags.Add(new CompileDiagnostic(DiagCode.ProjectileCap, DiagSeverity.Warning, omitted.TriggerSlot, omitted.Depth));
            }
            m.StandardT = StandardT(scopes[0], defs);
            m.Diagnostics = SortDiagnostics(diags);
            m.IsValid = true;
            return m;
        }

        // 6.4 Prune: 가장 깊은 스코프부터 통째로 뺀다. rootFactor = 1(보통) 또는 2(거울상)
        public static int Prune(SpellScope[] scopes, int rootFactor, int cap)
        {
            for (int d = scopes.Length - 1; d >= 0; d--)
            {
                long total = 0L;
                for (int k = 0; k <= d; k++)
                {
                    total += scopes[k].InstancesTotal;
                }
                if (total * rootFactor <= cap)
                {
                    return d;
                }
            }
            return 0;   // 루트만으로 상한을 넘는 일은 없다(6.6). 넘어도 실행기가 앞에서부터 상한만큼 만든다
        }

        // 14.2 [임시](U2): Σ(루트 탄 하나하나, 분열 반영, 거울상 제외) 기본 × (1 + 루트 변형 보너스 합).
        // 강화 배율·변이 배율·캐릭터 배율은 실행 순간의 값이라 컴파일 시점에는 1이다(부르는 쪽이 곱한다)
        private static Fix64 StandardT(SpellScope root, BlockDef?[] defs)
        {
            Fix64 t = Fix64.Zero;
            int bonus = PermilleOne + root.Mods.DamageBonusPermille;
            foreach (ScopeProjectile p in root.Projectiles)
            {
                Fix64 one = Fix64.MulPermille(defs[p.Slot]!.BaseDamage, bonus);
                t += one * root.SplitCount;
            }
            return t;
        }

        // 6.5: 변형이 스코프 안의 모든 투사체에 효과가 없을 때만 경고. 투사체가 없는 스코프는 W_TRIGGER_NO_PROJECTILE 하나로 알린다
        private static void CheckNoEffect(SpellScope s, BlockDef?[] defs, List<CompileDiagnostic> diags)
        {
            if (s.Projectiles.Length == 0)
            {
                return;
            }
            foreach (int slot in s.Mods.SourceSlots)
            {
                ModifierEffect effect = defs[slot]!.Effect;
                bool anyAffected = false;
                foreach (ScopeProjectile p in s.Projectiles)
                {
                    if (!Unaffected(effect, defs[p.Slot]!))
                    {
                        anyAffected = true;
                        break;
                    }
                }
                if (!anyAffected)
                {
                    diags.Add(new CompileDiagnostic(DiagCode.NoEffect, DiagSeverity.Warning, slot, s.Depth));
                }
            }
        }

        // 6.5 표: 관통은 지나가는 탄(PassThrough)과 즉발(IsInstant)에, 튕김·유도는 즉발에 효과가 없다(U22).
        // 블록을 이름이 아니라 표식으로 가른다(그림자 구체 = PassThrough, 연쇄 번개 = IsInstant)
        private static bool Unaffected(ModifierEffect effect, BlockDef projectile)
        {
            switch (effect)
            {
                case ModifierEffect.Pierce:
                    return projectile.IsInstant || (projectile.Flags & ProjectileFlags.PassThrough) != 0;
                case ModifierEffect.Bounce:
                case ModifierEffect.Homing:
                    return projectile.IsInstant;
                default:
                    return false;
            }
        }

        private static int HeatAfterUnderline(int heatHu, int slot, int underlineSlot, in CompileRules rules)
        {
            if (slot != underlineSlot)
            {
                return heatHu;
            }
            return heatHu - ShotHeat.UnderlineCutHu(heatHu, rules.UnderlineMaxCutHu);
        }

        private static CompiledSpell Invalid(CompiledSpell m, DiagCode code, int slot)
        {
            m.IsValid = false;
            m.Diagnostics = new[] { new CompileDiagnostic(code, DiagSeverity.Error, slot, 0) };
            return m;
        }

        // 팩에 없는 id(저장 파일·통신의 깨진 값)는 진단으로 돌린다. ContentDb 는 없는 id 에 ContentException 을 던진다
        private static bool TryDef(ContentDb db, BlockId id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BlockDef? def)
        {
            try
            {
                def = db.Block(id);
                return true;
            }
            catch (ContentException)
            {
                def = null;
                return false;
            }
        }

        private static long Saturate(long v) => v > int.MaxValue ? int.MaxValue : v;

        // 6.4 "diags 정렬: 슬롯 번호 오름차순, 같으면 코드 값 오름차순"(안정 정렬)
        private static CompileDiagnostic[] SortDiagnostics(List<CompileDiagnostic> diags)
        {
            CompileDiagnostic[] a = diags.ToArray();
            DetSort.Stable(a, 0, a.Length, new DiagOrder(), new CompileDiagnostic[a.Length]);
            return a;
        }

        private struct DiagOrder : IComparer<CompileDiagnostic>
        {
            public int Compare(CompileDiagnostic x, CompileDiagnostic y)
            {
                if (x.Slot != y.Slot)
                {
                    return x.Slot < y.Slot ? -1 : 1;
                }
                return ((int)x.Code).CompareTo((int)y.Code);
            }
        }

        // 스코프 하나를 모으는 동안의 임시 값
        private sealed class Builder
        {
            private readonly List<ScopeProjectile> _projectiles = new List<ScopeProjectile>();
            private readonly List<int> _modSlots = new List<int>();
            private readonly SpellScope _scope = new SpellScope();

            public Builder(int depth, int triggerSlot)
            {
                _scope.Depth = depth;
                _scope.TriggerSlot = triggerSlot;
            }

            public int Depth => _scope.Depth;

            public int HeatSumHu
            {
                get => _scope.HeatSumHu;
                set => _scope.HeatSumHu = value;
            }

            public int SplitSlot
            {
                get => _scope.SplitSlot;
                set => _scope.SplitSlot = value;
            }

            public int SplitCount
            {
                set => _scope.SplitCount = value;
            }

            public int SplitHeatMulPermille
            {
                set => _scope.SplitHeatMulPermille = value;
            }

            public int FanHalfAngleCenti
            {
                set => _scope.FanHalfAngleCenti = value;
            }

            // 첫 투사체가 스코프의 주인이다(6.1, CP-5, CP-7)
            public void AddProjectile(BlockId id, int slot)
            {
                if (_scope.OwnerSlot == NoSlot)
                {
                    _scope.OwnerSlot = slot;
                }
                _projectiles.Add(new ScopeProjectile(id, slot));
            }

            // 변형은 슬롯 위치와 무관하게 자기 스코프의 모든 투사체에 붙는다(CP-1). 부모 스코프로는 이어지지 않는다(CP-3)
            public void AddModifier(BlockDef d, int slot)
            {
                ScopeModifiers mods = _scope.Mods;
                mods.DamageBonusPermille += d.DamageBonusPermille;
                switch (d.Effect)
                {
                    case ModifierEffect.Bounce:
                        mods.BounceCount += d.Params.BounceCount;
                        break;
                    case ModifierEffect.Pierce:
                        mods.PierceCount += d.Params.PierceCount;
                        break;
                    case ModifierEffect.Size:
                        mods.SizeDoublings++;
                        break;
                    case ModifierEffect.Homing:
                        mods.Homing = true;
                        break;
                    case ModifierEffect.Frost:
                        mods.Frost = true;
                        break;
                    case ModifierEffect.TripleHeat:
                        mods.TripleHeat = true;
                        break;
                    case ModifierEffect.Corrosion:
                        mods.Corrosion = true;
                        break;
                }
                _modSlots.Add(slot);
            }

            public SpellScope Finish()
            {
                _scope.Projectiles = _projectiles.ToArray();
                _scope.Mods.SourceSlots = _modSlots.ToArray();
                return _scope;
            }
        }
    }
}
