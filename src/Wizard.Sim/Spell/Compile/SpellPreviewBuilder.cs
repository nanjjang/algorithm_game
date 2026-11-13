using Wizard.Core;

namespace Wizard.Sim.Spell.Compile
{
    // spec 02 14.4 조립 화면 미리보기 자료(CONTRACTS 3.4 SpellPreview, DECISIONS-TECH T-57). 컴파일 결과만 읽는 순수 함수다.
    //   마디      — 스코프마다 투사체 마디(슬롯 순). 마디 번호는 스코프 깊이 순으로 이어서 매긴다(= SpellNodeExecuted 의 마디 번호)
    //   꼬리      — 그 스코프의 변형 칸(슬롯 순)
    //   가지      — 유효한 분열의 갈래 수와 칸
    //   꼬리표    — DepthMultiplier 를 소수 둘째 자리에서 내린 값(0.7225 → 72, 0.8464 → 84)
    //   생략      — MaxDepthNormal 보다 깊은 스코프
    //   경고 딱지 — 그 칸에 걸린 진단이 있다
    //   맨 아래 줄 — N = 가지치기 뒤 투사체 수, H = HeatPerShotHu
    internal static class SpellPreviewBuilder
    {
        private const long Millionths = 1000000L;         // 소수 여섯째 자리(단위 환산). 감쇠 표의 값은 여섯 자리 안에서 끝난다(0.614125, 0.778688)
        private const long MillionthsPerHundredth = 10000L;

        public static SpellPreview Build(CompiledSpell spell)
        {
            var p = new SpellPreview
            {
                IsValid = spell.IsValid,
                Diagnostics = spell.Diagnostics,
                ProjectileCount = spell.ExecutedCount(false),
                HeatPerShotHu = spell.HeatPerShotHu,
            };
            if (!spell.IsValid)
            {
                return p;
            }
            int nodeCount = 0;
            foreach (SpellScope s in spell.Scopes)
            {
                nodeCount += s.Projectiles.Length;
            }
            var nodes = new PreviewNode[nodeCount];
            var scopes = new PreviewScope[spell.Scopes.Length];
            int next = 0;
            for (int d = 0; d < spell.Scopes.Length; d++)
            {
                SpellScope s = spell.Scopes[d];
                var ps = new PreviewScope
                {
                    Depth = s.Depth,
                    TriggerSlot = s.TriggerSlot,
                    TriggerHasWarning = s.TriggerSlot != SpellScope.NoSlot && HasDiagnostic(spell, s.TriggerSlot),
                    FirstNode = next,
                    NodeCount = s.Projectiles.Length,
                    SplitCount = s.SplitCount,
                    SplitSlot = s.SplitSlot,
                    ModifierSlots = s.Mods.SourceSlots,
                    ModifierHasWarning = new bool[s.Mods.SourceSlots.Length],
                    DepthLabelHundredths = LabelHundredths(s.DepthMultiplier),
                    Omitted = s.Depth > spell.MaxDepthNormal,
                };
                for (int k = 0; k < ps.ModifierSlots.Length; k++)
                {
                    ps.ModifierHasWarning[k] = HasDiagnostic(spell, ps.ModifierSlots[k]);
                }
                foreach (ScopeProjectile pr in s.Projectiles)
                {
                    nodes[next] = new PreviewNode(next, s.Depth, pr.Slot, pr.Block, pr.Slot == s.OwnerSlot, HasDiagnostic(spell, pr.Slot));
                    next++;
                }
                scopes[d] = ps;
            }
            p.Scopes = scopes;
            p.Nodes = nodes;
            return p;
        }

        // 14.4 "×0.72"(0.7225 를 소수 둘째 자리 내림). 감쇠 표의 값은 Q32.32 에서 참값보다 조금 작을 수 있으므로(0.92 → 0.9199999…)
        // 먼저 소수 여섯째 자리로 반올림해 참값의 자릿수를 되찾고, 그다음 둘째 자리에서 내린다. 배율은 0 이상 1 이하다
        public static int LabelHundredths(Fix64 multiplier)
        {
            long millionths = (multiplier.Raw * Millionths + (Fix64.OneRaw >> 1)) >> Fix64.FracBits;
            return (int)(millionths / MillionthsPerHundredth);
        }

        private static bool HasDiagnostic(CompiledSpell spell, int slot)
        {
            foreach (CompileDiagnostic d in spell.Diagnostics)
            {
                if (d.Slot == slot)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
