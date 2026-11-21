using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>미리보기 한 번의 결과: 컴파일 트리와 수치판 값.</summary>
    public sealed class SpellPreviewResult
    {
        public PlayerDerived Derived;
        public SpellCompileContext Context;
        public CompiledSpell Spell;
        public SpellStats Stats;
    }

    /// <summary>
    /// 미리보기 컴파일과 수치는 한 길로만 간다(G21 작업 지시, TraitModule 주석):
    /// derived = 판 중이면 p.Derived, 판 밖이면 rules.Traits.ComputeDerived(db, 캐릭터, 지팡이 단계, 변이)
    /// → rules.Traits.CompileContext(derived, 칸 수) → rules.Spells.Compile → rules.Spells.ComputeStats.
    /// 밑줄·감쇠를 화면이 직접 뽑지 않는다.
    /// </summary>
    public static class SpellPreview
    {
        /// <summary>판 밖(서고·조립 바꾸기): 지팡이 값에서 derived를 만든다.</summary>
        public static SpellPreviewResult ForWand(SimModuleSet rules, IContentDb db, CharacterDef ch, int stage,
            IReadOnlyList<string> mutations, int linkSlotCount, string core, IReadOnlyList<string> links)
        {
            if (rules == null) return Empty();
            var derived = rules.Traits.ComputeDerived(db, ch, stage, mutations ?? new List<string>());
            return Run(rules, db, ch, derived, linkSlotCount, core, links);
        }

        /// <summary>판 중(야영·튜토리얼): 플레이어의 지금 derived와 판 중 조립.</summary>
        public static SpellPreviewResult ForPlayer(SimModuleSet rules, IContentDb db, PlayerState p)
        {
            if (rules == null || p == null) return Empty();
            var lo = p.Loadout ?? new RunLoadout();
            return Run(rules, db, p.Def, p.Derived, lo.Links.Length, lo.Core.BlockId, lo.LinkIds(), lo.SlotStars());
        }

        /// <summary>stars = 칸마다 판 한정 별(0 = 코어, 1..L). null = 별 없음.</summary>
        public static SpellPreviewResult Run(SimModuleSet rules, IContentDb db, CharacterDef ch, PlayerDerived derived,
            int linkSlotCount, string core, IReadOnlyList<string> links, int[] stars = null)
        {
            var r = new SpellPreviewResult { Derived = derived };
            r.Context = rules.Traits.CompileContext(derived, linkSlotCount);
            r.Context.SlotStars = stars;
            r.Spell = rules.Spells.Compile(db, string.IsNullOrEmpty(core) ? null : core, links ?? new string[0], r.Context)
                      ?? new CompiledSpell();
            r.Stats = rules.Spells.ComputeStats(db, r.Spell, ch, derived);
            return r;
        }

        static SpellPreviewResult Empty() => new SpellPreviewResult { Spell = new CompiledSpell() };
    }
}
